using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Models.ComicDtos;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;

namespace Jellyfin.Api.Services.ComicPages;

/// <summary>
/// Indexes comic archives and extracts requested pages into a bounded disk cache.
/// </summary>
public sealed class ComicPageService : IDisposable
{
    private const int ArchiveIndexSchemaVersion = 1;
    private const long MaximumPageBytes = 256L * 1024 * 1024;
    private const long MaximumIndexBytes = 16L * 1024 * 1024;
    private const long MaximumCacheBytes = 2L * 1024 * 1024 * 1024;
    private const long CacheCleanupTargetBytes = 1800L * 1024 * 1024;
    private const int MaximumIndexedArchives = 64;
    private const string ArchiveIndexFileName = "index-v1.json";
    private const int MaximumIndexedPages = 100000;
    private static readonly TimeSpan IndexIdleLifetime = TimeSpan.FromMinutes(30);
    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cb7", ".cbr", ".cbt", ".cbz"
    };

    private static readonly IReadOnlyDictionary<string, string> SupportedImageTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".avif"] = "image/avif",
            [".bmp"] = "image/bmp",
            [".dib"] = "image/bmp",
            [".gif"] = "image/gif",
            [".jfif"] = "image/jpeg",
            [".jfi"] = "image/jpeg",
            [".jif"] = "image/jpeg",
            [".jpe"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".tif"] = "image/tiff",
            [".tiff"] = "image/tiff",
            [".webp"] = "image/webp"
        };

    private readonly string _cacheRoot;
    private readonly ILogger<ComicPageService> _logger;
    private readonly ConcurrentDictionary<Guid, ArchiveSlot> _slots = new();
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private int _cacheMisses;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComicPageService"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application cache paths.</param>
    /// <param name="logger">The logger.</param>
    public ComicPageService(IApplicationPaths applicationPaths, ILogger<ComicPageService> logger)
        : this(Path.Combine(applicationPaths.CachePath, "custom-comic-pages"), logger)
    {
    }

    internal ComicPageService(string cacheRoot, ILogger<ComicPageService> logger)
    {
        _cacheRoot = cacheRoot;
        _logger = logger;
        Directory.CreateDirectory(_cacheRoot);
    }

    /// <summary>
    /// Returns whether a path is a supported comic archive.
    /// </summary>
    /// <param name="path">The archive path.</param>
    /// <returns>Whether the extension identifies a supported comic archive.</returns>
    public static bool IsSupportedArchive(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && SupportedArchiveExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Gets the archive manifest.
    /// </summary>
    /// <param name="itemId">The comic item identifier.</param>
    /// <param name="archivePath">The local archive path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comic manifest.</returns>
    public Task<ComicManifest> GetManifestAsync(Guid itemId, string archivePath, CancellationToken cancellationToken)
        => GetManifestAsync(itemId, archivePath, null, cancellationToken);

    /// <summary>
    /// Gets the archive manifest and optionally warms the requested first page
    /// while a previously unseen archive is already open for indexing.
    /// </summary>
    /// <param name="itemId">The comic item identifier.</param>
    /// <param name="archivePath">The local archive path.</param>
    /// <param name="initialPageIndex">The optional zero-based page to prepare while indexing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comic manifest.</returns>
    public Task<ComicManifest> GetManifestAsync(
        Guid itemId,
        string archivePath,
        int? initialPageIndex,
        CancellationToken cancellationToken)
        => WithArchiveAsync(
            itemId,
            archivePath,
            (state, _) => Task.FromResult(new ComicManifest(1, state.SourceVersion, state.Pages.Count)),
            cancellationToken,
            initialPageIndex);

    /// <summary>
    /// Gets or extracts one comic page.
    /// </summary>
    /// <param name="itemId">The comic item identifier.</param>
    /// <param name="archivePath">The local archive path.</param>
    /// <param name="pageIndex">The zero-based image page index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached page location and response metadata.</returns>
    public Task<ComicPageResult> GetPageAsync(
        Guid itemId,
        string archivePath,
        int pageIndex,
        CancellationToken cancellationToken)
        => WithArchiveAsync(
            itemId,
            archivePath,
            (state, token) => ExtractPageAsync(itemId, state, pageIndex, token),
            cancellationToken,
            pageIndex);

    private async Task<T> WithArchiveAsync<T>(
        Guid itemId,
        string archivePath,
        Func<ArchiveState, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        int? initialPageIndex = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupportedArchive(archivePath))
        {
            throw new NotSupportedException("The item is not a supported comic archive.");
        }

        var slot = _slots.GetOrAdd(itemId, _ => new ArchiveSlot());
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var file = new FileInfo(archivePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("The comic archive was not found.", archivePath);
            }

            if (slot.State is null || !slot.State.Matches(file))
            {
                slot.State = await CreateArchiveStateAsync(
                        itemId,
                        file,
                        initialPageIndex,
                        cancellationToken)
                    .ConfigureAwait(false);
                RemoveObsoleteVersions(itemId, slot.State.SourceVersion);
            }

            slot.LastUsedUtc = DateTime.UtcNow;
            return await action(slot.State, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            slot.Gate.Release();
            EvictIdleIndexes(itemId);
        }
    }

    private async Task<ArchiveState> CreateArchiveStateAsync(
        Guid itemId,
        FileInfo file,
        int? initialPageIndex,
        CancellationToken cancellationToken)
    {
        var version = GetSourceVersion(file);
        var cachedState = TryLoadArchiveState(itemId, file, version);
        if (cachedState is not null)
        {
            _logger.LogDebug(
                "Restored comic archive index {ArchivePath}: {PageCount} pages, source {SourceVersion}",
                file.FullName,
                cachedState.Pages.Count,
                version);
            return cachedState;
        }

        using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            128 * 1024,
            FileOptions.RandomAccess);
        using var archive = ArchiveFactory.OpenArchive(stream);
        var pages = archive.Entries
            .Where(entry => !entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
            .Select(entry => new
            {
                Key = entry.Key!,
                Extension = Path.GetExtension(entry.Key!) ?? string.Empty
            })
            .Where(page => SupportedImageTypes.ContainsKey(page.Extension))
            .OrderBy(page => page.Key, NaturalStringComparer.Instance)
            .Select(page => new ArchivePage(
                page.Key,
                page.Extension.ToLowerInvariant(),
                SupportedImageTypes[page.Extension]))
            .ToArray();

        if (pages.Length == 0)
        {
            throw new InvalidDataException("The comic archive contains no supported image pages.");
        }

        if (pages.Length > MaximumIndexedPages)
        {
            throw new InvalidDataException("The comic archive contains too many image pages.");
        }

        var state = new ArchiveState(
            file.FullName,
            file.Length,
            file.LastWriteTimeUtc,
            version,
            pages);
        await SaveArchiveIndexAsync(itemId, state, cancellationToken).ConfigureAwait(false);

        if (initialPageIndex is int pageIndex && pageIndex >= 0 && pageIndex < pages.Length)
        {
            await ExtractPageFromOpenArchiveAsync(itemId, state, pageIndex, archive, cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Indexed comic archive {ArchivePath}: {PageCount} pages, source {SourceVersion}",
            file.FullName,
            pages.Length,
            version);
        return state;
    }

    private ArchiveState? TryLoadArchiveState(Guid itemId, FileInfo file, string version)
    {
        var indexPath = Path.Combine(GetVersionDirectory(itemId, version), ArchiveIndexFileName);
        try
        {
            var indexFile = new FileInfo(indexPath);
            if (!indexFile.Exists || indexFile.Length <= 0 || indexFile.Length > MaximumIndexBytes)
            {
                return null;
            }

            var index = JsonSerializer.Deserialize<ArchiveIndex>(File.ReadAllText(indexPath));
            if (index is null
                || index.SchemaVersion != ArchiveIndexSchemaVersion
                || !string.Equals(index.SourceVersion, version, StringComparison.Ordinal)
                || index.SourceLength != file.Length
                || index.SourceLastWriteTimeUtcTicks != file.LastWriteTimeUtc.Ticks
                || index.Pages is null
                || index.Pages.Count <= 0
                || index.Pages.Count > MaximumIndexedPages
                || index.Pages.Any(page => !IsValidIndexedPage(page)))
            {
                return null;
            }

            return new ArchiveState(
                file.FullName,
                file.Length,
                file.LastWriteTimeUtc,
                version,
                index.Pages.ToArray());
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            _logger.LogDebug(exception, "Could not restore comic archive index {IndexPath}", indexPath);
            return null;
        }
    }

    private async Task SaveArchiveIndexAsync(
        Guid itemId,
        ArchiveState state,
        CancellationToken cancellationToken)
    {
        var versionDirectory = GetVersionDirectory(itemId, state.SourceVersion);
        Directory.CreateDirectory(versionDirectory);
        var indexPath = Path.Combine(versionDirectory, ArchiveIndexFileName);
        var temporaryPath = indexPath + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        var index = new ArchiveIndex(
            ArchiveIndexSchemaVersion,
            state.SourceVersion,
            state.Length,
            state.LastWriteTimeUtc.Ticks,
            state.Pages);

        try
        {
            using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             16 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(output, index, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, indexPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not persist comic archive index {IndexPath}", indexPath);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not remove comic archive index temporary file {IndexPath}", temporaryPath);
            }
        }
    }

    private static bool IsValidIndexedPage(ArchivePage? page)
        => page is not null
            && !string.IsNullOrWhiteSpace(page.EntryKey)
            && !string.IsNullOrWhiteSpace(page.Extension)
            && SupportedImageTypes.TryGetValue(page.Extension, out var contentType)
            && string.Equals(contentType, page.ContentType, StringComparison.Ordinal)
            && Path.GetExtension(page.EntryKey).Equals(page.Extension, StringComparison.OrdinalIgnoreCase);

    private static string GetSourceVersion(FileInfo file)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}");

    private string GetVersionDirectory(Guid itemId, string sourceVersion)
        => Path.Combine(
            _cacheRoot,
            itemId.ToString("N", CultureInfo.InvariantCulture),
            sourceVersion);

    private async Task<ComicPageResult> ExtractPageAsync(
        Guid itemId,
        ArchiveState state,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        if (pageIndex < 0 || pageIndex >= state.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        using var archiveStream = new FileStream(
            state.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            128 * 1024,
            FileOptions.RandomAccess);
        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        return await ExtractPageFromOpenArchiveAsync(itemId, state, pageIndex, archive, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ComicPageResult> ExtractPageFromOpenArchiveAsync(
        Guid itemId,
        ArchiveState state,
        int pageIndex,
        IArchive archive,
        CancellationToken cancellationToken)
    {
        if (pageIndex < 0 || pageIndex >= state.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        var page = state.Pages[pageIndex];
        var versionDirectory = GetVersionDirectory(itemId, state.SourceVersion);
        Directory.CreateDirectory(versionDirectory);
        var cachePath = Path.Combine(
            versionDirectory,
            string.Create(CultureInfo.InvariantCulture, $"page-{pageIndex:D6}{page.Extension}"));
        if (IsValidCacheFile(cachePath))
        {
            TouchCacheFile(cachePath);
            return new ComicPageResult(cachePath, page.ContentType, state.SourceVersion, state.LastWriteTimeUtc);
        }

        var temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        try
        {
            var entry = archive.Entries.FirstOrDefault(candidate =>
                !candidate.IsDirectory
                && candidate.Key?.Equals(page.EntryKey, StringComparison.Ordinal) == true);
            if (entry is null)
            {
                throw new InvalidDataException("The requested comic page no longer exists in the archive.");
            }

            if (entry.Size > MaximumPageBytes)
            {
                throw new InvalidDataException("The requested comic page exceeds the safety limit.");
            }

            var input = await entry.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (input.ConfigureAwait(false))
            using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (output.Length == 0)
                {
                    throw new InvalidDataException("The requested comic page was empty.");
                }
            }

            File.Move(temporaryPath, cachePath, true);
            TouchCacheFile(cachePath);
            if (Interlocked.Increment(ref _cacheMisses) % 16 == 0)
            {
                await EnforceCacheLimitAsync(cancellationToken).ConfigureAwait(false);
            }

            return new ComicPageResult(cachePath, page.ContentType, state.SourceVersion, state.LastWriteTimeUtc);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task EnforceCacheLimitAsync(CancellationToken cancellationToken)
    {
        if (!await _cleanupGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await Task.Run(
                () =>
                {
                    var files = new DirectoryInfo(_cacheRoot)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Where(file => !file.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                            && !file.Name.Equals(ArchiveIndexFileName, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(file => file.LastWriteTimeUtc)
                        .ToArray();
                    var totalBytes = files.Sum(file => file.Length);
                    if (totalBytes <= MaximumCacheBytes)
                    {
                        return;
                    }

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            totalBytes -= file.Length;
                            file.Delete();
                        }
                        catch (IOException exception)
                        {
                            _logger.LogDebug(exception, "Could not evict comic page cache file {CachePath}", file.FullName);
                        }

                        if (totalBytes <= CacheCleanupTargetBytes)
                        {
                            break;
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cleanupGate.Release();
        }
    }

    private void EvictIdleIndexes(Guid currentItemId)
    {
        var now = DateTime.UtcNow;
        var indexedSlots = _slots
            .Where(pair => pair.Value.State is not null)
            .OrderBy(pair => pair.Value.LastUsedUtc)
            .ToArray();
        var excess = Math.Max(0, indexedSlots.Length - MaximumIndexedArchives);
        foreach (var pair in indexedSlots)
        {
            if (pair.Key.Equals(currentItemId))
            {
                continue;
            }

            var idle = now - pair.Value.LastUsedUtc;
            if (excess <= 0 && idle < IndexIdleLifetime)
            {
                continue;
            }

            if (!pair.Value.Gate.Wait(0))
            {
                continue;
            }

            try
            {
                pair.Value.State = null;
                excess--;
            }
            finally
            {
                pair.Value.Gate.Release();
            }
        }
    }

    private void RemoveObsoleteVersions(Guid itemId, string currentVersion)
    {
        var itemDirectory = Path.Combine(_cacheRoot, itemId.ToString("N", CultureInfo.InvariantCulture));
        if (!Directory.Exists(itemDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(itemDirectory))
        {
            if (Path.GetFileName(directory).Equals(currentVersion, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException exception)
            {
                _logger.LogDebug(exception, "Could not remove obsolete comic cache directory {CacheDirectory}", directory);
            }
        }
    }

    private static bool IsValidCacheFile(string path)
    {
        try
        {
            return new FileInfo(path) is { Exists: true, Length: > 0 };
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TouchCacheFile(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (IOException)
        {
            // Cache recency is advisory; serving the valid page takes precedence.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var slot in _slots.Values)
        {
            slot.Gate.Wait();
            try
            {
                slot.State = null;
            }
            finally
            {
                slot.Gate.Release();
                slot.Gate.Dispose();
            }
        }

        _cleanupGate.Dispose();
    }

    private sealed class ArchiveSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public ArchiveState? State { get; set; }

        public DateTime LastUsedUtc { get; set; }
    }

    private sealed class ArchiveState
    {
        public ArchiveState(
            string path,
            long length,
            DateTime lastWriteTimeUtc,
            string sourceVersion,
            IReadOnlyList<ArchivePage> pages)
        {
            Path = path;
            Length = length;
            LastWriteTimeUtc = lastWriteTimeUtc;
            SourceVersion = sourceVersion;
            Pages = pages;
        }

        public string Path { get; }

        public long Length { get; }

        public DateTime LastWriteTimeUtc { get; }

        public string SourceVersion { get; }

        public IReadOnlyList<ArchivePage> Pages { get; }

        public bool Matches(FileInfo file)
            => Path.Equals(file.FullName, StringComparison.OrdinalIgnoreCase)
                && Length == file.Length
                && LastWriteTimeUtc == file.LastWriteTimeUtc;
    }

    private sealed record ArchiveIndex(
        int SchemaVersion,
        string SourceVersion,
        long SourceLength,
        long SourceLastWriteTimeUtcTicks,
        IReadOnlyList<ArchivePage> Pages);

    private sealed record ArchivePage(string EntryKey, string Extension, string ContentType);
}
