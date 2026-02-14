using TsqlRefine.Cli.Services;

namespace TsqlRefine.Cli.Tests.Services;

public sealed class ConfigPathResolutionTests
{
    [Fact]
    public void GetCandidatePaths_WithExplicitPath_ReturnsSingleCandidate()
    {
        var result = ConfigLoader.GetCandidatePaths("/explicit/config.json", "tsqlrefine.json", "/project", "/home/user");

        Assert.Single(result);
        Assert.Equal("/explicit/config.json", result[0]);
    }

    [Fact]
    public void GetCandidatePaths_WithoutExplicit_ReturnsThreeCandidatesInOrder()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "/home/user");

        Assert.Equal(3, result.Count);
        Assert.Equal(Path.Combine("/project", "tsqlrefine.json"), result[0]);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.json"), result[1]);
        Assert.Equal(Path.Combine("/home/user", ".tsqlrefine", "tsqlrefine.json"), result[2]);
    }

    [Fact]
    public void GetCandidatePaths_WithNullHome_ReturnsTwoCandidates()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", null);

        Assert.Equal(2, result.Count);
        Assert.Equal(Path.Combine("/project", "tsqlrefine.json"), result[0]);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.json"), result[1]);
    }

    [Fact]
    public void GetCandidatePaths_WithEmptyHome_ReturnsTwoCandidates()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCandidatePaths_ForIgnoreFile_ReturnsCorrectPaths()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.ignore", "/project", "/home/user");

        Assert.Equal(3, result.Count);
        Assert.Equal(Path.Combine("/project", "tsqlrefine.ignore"), result[0]);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.ignore"), result[1]);
        Assert.Equal(Path.Combine("/home/user", ".tsqlrefine", "tsqlrefine.ignore"), result[2]);
    }

    [Fact]
    public void GetCandidatePaths_CwdDirectIsFirstCandidate()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "/home/user");

        // CWD direct should be checked before .tsqlrefine/ subdirectory
        Assert.StartsWith(Path.Combine("/project", "tsqlrefine.json"), result[0]);
        Assert.Contains(".tsqlrefine", result[1]);
    }
}
