using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Services.ComicPages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Api.Tests;

public sealed class ComicPageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "comic-page-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ManifestAndPagesUseNaturalImageOrder()
    {
        var archivePath = Path.Combine(_root, "natural.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]>
        {
            ["page10.jpg"] = [10],
            ["notes.txt"] = [99],
            ["page2.jpg"] = [2],
            ["page1.jpg"] = [1]
        });

        using var service = CreateService();
        var itemId = Guid.NewGuid();
        var manifest = await service.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);
        var first = await service.GetPageAsync(itemId, archivePath, 0, TestContext.Current.CancellationToken);
        var second = await service.GetPageAsync(itemId, archivePath, 1, TestContext.Current.CancellationToken);
        var third = await service.GetPageAsync(itemId, archivePath, 2, TestContext.Current.CancellationToken);

        Assert.Equal(3, manifest.PageCount);
        Assert.Equal([1], await File.ReadAllBytesAsync(first.FilePath, TestContext.Current.CancellationToken));
        Assert.Equal([2], await File.ReadAllBytesAsync(second.FilePath, TestContext.Current.CancellationToken));
        Assert.Equal([10], await File.ReadAllBytesAsync(third.FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReusesExtractedPageAndInvalidatesChangedArchive()
    {
        var archivePath = Path.Combine(_root, "versioned.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]> { ["page1.png"] = [1, 2, 3] });
        using var service = CreateService();
        var itemId = Guid.NewGuid();

        var firstManifest = await service.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);
        var firstPage = await service.GetPageAsync(itemId, archivePath, 0, TestContext.Current.CancellationToken);
        var cachedPage = await service.GetPageAsync(itemId, archivePath, 0, TestContext.Current.CancellationToken);
        Assert.Equal(firstPage.FilePath, cachedPage.FilePath);

        await Task.Delay(20, TestContext.Current.CancellationToken);
        CreateArchive(archivePath, new Dictionary<string, byte[]> { ["page1.png"] = [4, 5, 6, 7] });
        File.SetLastWriteTimeUtc(archivePath, DateTime.UtcNow.AddSeconds(1));
        var secondManifest = await service.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);
        var changedPage = await service.GetPageAsync(itemId, archivePath, 0, TestContext.Current.CancellationToken);

        Assert.NotEqual(firstManifest.SourceVersion, secondManifest.SourceVersion);
        Assert.NotEqual(firstPage.FilePath, changedPage.FilePath);
        Assert.Equal([4, 5, 6, 7], await File.ReadAllBytesAsync(changedPage.FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PersistsManifestIndexAcrossServiceInstances()
    {
        var archivePath = Path.Combine(_root, "persistent-index.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]>
        {
            ["page1.jpg"] = [1],
            ["page2.jpg"] = [2]
        });
        var itemId = Guid.NewGuid();
        var sourceLastWriteTimeUtc = File.GetLastWriteTimeUtc(archivePath);
        string sourceVersion;
        int pageCount;

        using (var firstService = CreateService())
        {
            var firstManifest = await firstService.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);
            sourceVersion = firstManifest.SourceVersion;
            pageCount = firstManifest.PageCount;
        }

        var indexPath = Path.Combine(
            _root,
            "cache",
            itemId.ToString("N"),
            sourceVersion,
            "index-v1.json");
        Assert.True(File.Exists(indexPath));

        var archiveBytes = await File.ReadAllBytesAsync(archivePath, TestContext.Current.CancellationToken);
        Array.Fill<byte>(archiveBytes, 0);
        await File.WriteAllBytesAsync(archivePath, archiveBytes, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(archivePath, sourceLastWriteTimeUtc);

        using var secondService = CreateService();
        var restoredManifest = await secondService.GetManifestAsync(
            itemId,
            archivePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(sourceVersion, restoredManifest.SourceVersion);
        Assert.Equal(pageCount, restoredManifest.PageCount);
    }

    [Fact]
    public async Task ManifestWarmsRequestedPageDuringInitialIndexing()
    {
        var archivePath = Path.Combine(_root, "warm-page.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]>
        {
            ["page1.jpg"] = [1],
            ["page2.jpg"] = [2]
        });
        var itemId = Guid.NewGuid();
        using var service = CreateService();

        var manifest = await service.GetManifestAsync(itemId, archivePath, 1, TestContext.Current.CancellationToken);

        var warmedPagePath = Path.Combine(
            _root,
            "cache",
            itemId.ToString("N"),
            manifest.SourceVersion,
            "page-000001.jpg");

        Assert.True(File.Exists(warmedPagePath));
        Assert.Equal([2], await File.ReadAllBytesAsync(warmedPagePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MalformedPersistentIndexFallsBackToArchiveScan()
    {
        var archivePath = Path.Combine(_root, "malformed-index.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]> { ["page1.jpg"] = [1] });
        var itemId = Guid.NewGuid();
        string sourceVersion;

        using (var firstService = CreateService())
        {
            var firstManifest = await firstService.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);
            sourceVersion = firstManifest.SourceVersion;
        }

        var indexPath = Path.Combine(
            _root,
            "cache",
            itemId.ToString("N"),
            sourceVersion,
            "index-v1.json");
        await File.WriteAllTextAsync(indexPath, "not json", TestContext.Current.CancellationToken);

        using var secondService = CreateService();
        var rebuiltManifest = await secondService.GetManifestAsync(itemId, archivePath, TestContext.Current.CancellationToken);

        Assert.Equal(sourceVersion, rebuiltManifest.SourceVersion);
        Assert.Equal(1, rebuiltManifest.PageCount);
    }

    [Fact]
    public async Task RejectsOutOfRangePage()
    {
        var archivePath = Path.Combine(_root, "range.cbz");
        CreateArchive(archivePath, new Dictionary<string, byte[]> { ["page1.webp"] = [1] });
        using var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPageAsync(
            Guid.NewGuid(),
            archivePath,
            1,
            TestContext.Current.CancellationToken));
    }

    private ComicPageService CreateService()
        => new(Path.Combine(_root, "cache"), NullLogger<ComicPageService>.Instance);

    private static void CreateArchive(string path, IReadOnlyDictionary<string, byte[]> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".new";
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.Key, CompressionLevel.NoCompression);
                using var output = zipEntry.Open();
                output.Write(entry.Value);
            }
        }

        File.Move(temporaryPath, path, true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
