using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Models.ComicDtos;
using Jellyfin.Api.Services.ComicPages;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Serves individual comic pages to authenticated Jellyfin users.
/// </summary>
[Authorize]
[Route("ComicPages")]
public sealed class ComicPagesController : BaseJellyfinApiController
{
    private const string UserIdClaim = "Jellyfin-UserId";
    private readonly ILibraryManager _libraryManager;
    private readonly ComicPageService _pageService;
    private readonly ILogger<ComicPagesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComicPagesController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="pageService">The comic page cache and extraction service.</param>
    /// <param name="logger">The logger.</param>
    public ComicPagesController(
        ILibraryManager libraryManager,
        ComicPageService pageService,
        ILogger<ComicPagesController> logger)
    {
        _libraryManager = libraryManager;
        _pageService = pageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the page count and source version for a comic archive.
    /// </summary>
    /// <param name="itemId">The comic item identifier.</param>
    /// <param name="initialPage">The optional zero-based page to prepare while indexing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comic manifest.</returns>
    [HttpGet("{itemId:guid}/manifest")]
    [ProducesResponseType<ComicManifest>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ComicManifest>> GetManifest(
        [FromRoute] Guid itemId,
        [FromQuery] int? initialPage,
        CancellationToken cancellationToken)
    {
        var itemResult = GetAccessibleItem(itemId);
        if (itemResult.Error is not null)
        {
            return itemResult.Error;
        }

        var archivePath = itemResult.Item!.Path;
        if (!ComicPageService.IsSupportedArchive(archivePath))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            Response.Headers.CacheControl = "private, no-store";
            return await _pageService
                .GetManifestAsync(itemId, archivePath, initialPage, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException exception) when (IsMissingArchive(exception, archivePath))
        {
            return NotFound();
        }
        catch (FileNotFoundException exception)
        {
            _logger.LogError(
                exception,
                "A comic reader runtime dependency was not found while opening item {ItemId}",
                itemId);
            return Problem(
                "The comic reader could not load a required runtime dependency.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "Could not index comic archive for item {ItemId}", itemId);
            return Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    /// <summary>
    /// Gets one image page from a comic archive.
    /// </summary>
    /// <param name="itemId">The comic item identifier.</param>
    /// <param name="pageIndex">The zero-based image page index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The requested page image.</returns>
    [HttpGet("{itemId:guid}/pages/{pageIndex:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult> GetPage(
        [FromRoute] Guid itemId,
        [FromRoute] int pageIndex,
        CancellationToken cancellationToken)
    {
        var itemResult = GetAccessibleItem(itemId);
        if (itemResult.Error is not null)
        {
            return itemResult.Error;
        }

        var archivePath = itemResult.Item!.Path;
        if (!ComicPageService.IsSupportedArchive(archivePath))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            var page = await _pageService
                .GetPageAsync(itemId, archivePath, pageIndex, cancellationToken)
                .ConfigureAwait(false);
            Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            Response.Headers.ETag = string.Create(
                CultureInfo.InvariantCulture,
                $"\"{page.SourceVersion}-{pageIndex}\"");
            Response.GetTypedHeaders().LastModified = page.LastModifiedUtc;
            return PhysicalFile(page.FilePath, page.ContentType, true);
        }
        catch (ArgumentOutOfRangeException)
        {
            return NotFound();
        }
        catch (FileNotFoundException exception) when (IsMissingArchive(exception, archivePath))
        {
            return NotFound();
        }
        catch (FileNotFoundException exception)
        {
            _logger.LogError(
                exception,
                "A comic reader runtime dependency was not found while opening item {ItemId}",
                itemId);
            return Problem(
                "The comic reader could not load a required runtime dependency.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "Could not extract comic page {PageIndex} for item {ItemId}", pageIndex, itemId);
            return Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    internal static bool IsMissingArchive(FileNotFoundException exception, string archivePath)
        => !string.IsNullOrEmpty(exception.FileName)
            && exception.FileName.Equals(archivePath, StringComparison.OrdinalIgnoreCase);

    private (BaseItem? Item, ActionResult? Error) GetAccessibleItem(Guid itemId)
    {
        var claimValue = User.Claims
            .FirstOrDefault(claim => claim.Type.Equals(UserIdClaim, StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (!Guid.TryParse(claimValue, out var userId) || userId.Equals(Guid.Empty))
        {
            return (null, Unauthorized());
        }

        var item = _libraryManager.GetItemById<BaseItem>(itemId, userId);
        return item is null ? (null, NotFound()) : (item, null);
    }
}
