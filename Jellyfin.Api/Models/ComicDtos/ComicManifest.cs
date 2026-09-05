namespace Jellyfin.Api.Models.ComicDtos;

/// <summary>
/// Describes the pages available in a comic archive.
/// </summary>
/// <param name="SchemaVersion">The response schema version.</param>
/// <param name="SourceVersion">A version that changes when the source archive changes.</param>
/// <param name="PageCount">The number of supported image pages.</param>
public sealed record ComicManifest(int SchemaVersion, string SourceVersion, int PageCount);
