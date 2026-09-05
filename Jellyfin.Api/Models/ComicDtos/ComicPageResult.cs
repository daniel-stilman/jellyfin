using System;

namespace Jellyfin.Api.Models.ComicDtos;

/// <summary>
/// Identifies one extracted page in the bounded server cache.
/// </summary>
/// <param name="FilePath">The absolute cached file path.</param>
/// <param name="ContentType">The image MIME type.</param>
/// <param name="SourceVersion">The source archive version.</param>
/// <param name="LastModifiedUtc">The source archive modification time.</param>
public sealed record ComicPageResult(
    string FilePath,
    string ContentType,
    string SourceVersion,
    DateTime LastModifiedUtc);
