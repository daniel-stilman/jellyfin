using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Controllers;
using Xunit;

namespace Jellyfin.Api.Tests;

public sealed class ComicPagesControllerTests
{
    [Fact]
    public void MissingArchiveIsRecognizedByItsExactPath()
    {
        const string archivePath = @"E:\comics\issue.cbr";
        var exception = new FileNotFoundException("Missing archive.", archivePath);

        Assert.True(ComicPagesController.IsMissingArchive(exception, archivePath));
    }

    [Fact]
    public void MissingRuntimeAssemblyIsNotMisreportedAsMissingComic()
    {
        const string archivePath = @"E:\comics\issue.cbr";
        var exception = new FileNotFoundException(
            "Could not load file or assembly.",
            "SharpCompress, Version=0.38.0.0, Culture=neutral, PublicKeyToken=afb0a02973931d96");

        Assert.False(ComicPagesController.IsMissingArchive(exception, archivePath));
    }
}
