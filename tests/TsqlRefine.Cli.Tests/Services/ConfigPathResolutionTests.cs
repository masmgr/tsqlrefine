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
    public void GetCandidatePaths_WithoutExplicit_ReturnsTwoCandidatesInOrder()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "/home/user");

        Assert.Equal(2, result.Count);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.json"), result[0]);
        Assert.Equal(Path.Combine("/home/user", ".tsqlrefine", "tsqlrefine.json"), result[1]);
    }

    [Fact]
    public void GetCandidatePaths_WithNullHome_ReturnsSingleCandidate()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", null);

        Assert.Single(result);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.json"), result[0]);
    }

    [Fact]
    public void GetCandidatePaths_WithEmptyHome_ReturnsSingleCandidate()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "");

        Assert.Single(result);
    }

    [Fact]
    public void GetCandidatePaths_ForIgnoreFile_ReturnsCorrectPaths()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.ignore", "/project", "/home/user");

        Assert.Equal(2, result.Count);
        Assert.Equal(Path.Combine("/project", ".tsqlrefine", "tsqlrefine.ignore"), result[0]);
        Assert.Equal(Path.Combine("/home/user", ".tsqlrefine", "tsqlrefine.ignore"), result[1]);
    }

    [Fact]
    public void GetCandidatePaths_DotTsqlrefineIsFirstCandidate()
    {
        var result = ConfigLoader.GetCandidatePaths(null, "tsqlrefine.json", "/project", "/home/user");

        // .tsqlrefine/ subdirectory should be the first candidate
        Assert.Contains(".tsqlrefine", result[0]);
        Assert.StartsWith(Path.Combine("/project", ".tsqlrefine"), result[0]);
    }

    // ========================================================
    // IsRulesetNameReference tests
    // ========================================================

    [Theory]
    [InlineData("my-custom")]
    [InlineData("team-ruleset")]
    [InlineData("strict-plus")]
    public void IsRulesetNameReference_NameOnly_ReturnsTrue(string value)
    {
        Assert.True(ConfigLoader.IsRulesetNameReference(value));
    }

    [Theory]
    [InlineData("rulesets/custom.json")]
    [InlineData("./custom.json")]
    [InlineData("../rulesets/custom.json")]
    public void IsRulesetNameReference_WithForwardSlash_ReturnsFalse(string value)
    {
        Assert.False(ConfigLoader.IsRulesetNameReference(value));
    }

    [Theory]
    [InlineData("rulesets\\custom.json")]
    [InlineData(".\\custom.json")]
    public void IsRulesetNameReference_WithBackslash_ReturnsFalse(string value)
    {
        Assert.False(ConfigLoader.IsRulesetNameReference(value));
    }

    [Theory]
    [InlineData("custom.json")]
    [InlineData("my-ruleset.JSON")]
    public void IsRulesetNameReference_WithJsonExtension_ReturnsFalse(string value)
    {
        Assert.False(ConfigLoader.IsRulesetNameReference(value));
    }

    // ========================================================
    // ResolveNamedRulesetPath tests
    // ========================================================

    [Fact]
    public void ResolveNamedRulesetPath_FoundInCwdRulesets_ReturnsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var rulesetsDir = Path.Combine(tempDir, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            var rulesetFile = Path.Combine(rulesetsDir, "my-custom.json");
            File.WriteAllText(rulesetFile, """{"rules": []}""");

            var result = ConfigLoader.ResolveNamedRulesetPath("my-custom", cwd: tempDir, homePath: null);

            Assert.NotNull(result);
            Assert.Equal(Path.GetFullPath(rulesetFile), result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveNamedRulesetPath_FoundInHomeRulesets_ReturnsPath()
    {
        var tempCwd = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempCwd);
            var rulesetsDir = Path.Combine(tempHome, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            var rulesetFile = Path.Combine(rulesetsDir, "shared.json");
            File.WriteAllText(rulesetFile, """{"rules": []}""");

            var result = ConfigLoader.ResolveNamedRulesetPath("shared", cwd: tempCwd, homePath: tempHome);

            Assert.NotNull(result);
            Assert.Equal(Path.GetFullPath(rulesetFile), result);
        }
        finally
        {
            if (Directory.Exists(tempCwd))
                Directory.Delete(tempCwd, true);
            if (Directory.Exists(tempHome))
                Directory.Delete(tempHome, true);
        }
    }

    [Fact]
    public void ResolveNamedRulesetPath_NotFound_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);

            var result = ConfigLoader.ResolveNamedRulesetPath("nonexistent", cwd: tempDir, homePath: tempDir);

            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveNamedRulesetPath_CwdTakesPriorityOverHome()
    {
        var tempCwd = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempHome = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var cwdRulesetsDir = Path.Combine(tempCwd, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(cwdRulesetsDir);
            var cwdFile = Path.Combine(cwdRulesetsDir, "team.json");
            File.WriteAllText(cwdFile, """{"rules": [{"id": "cwd-version"}]}""");

            var homeRulesetsDir = Path.Combine(tempHome, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(homeRulesetsDir);
            var homeFile = Path.Combine(homeRulesetsDir, "team.json");
            File.WriteAllText(homeFile, """{"rules": [{"id": "home-version"}]}""");

            var result = ConfigLoader.ResolveNamedRulesetPath("team", cwd: tempCwd, homePath: tempHome);

            Assert.NotNull(result);
            Assert.Equal(Path.GetFullPath(cwdFile), result);
        }
        finally
        {
            if (Directory.Exists(tempCwd))
                Directory.Delete(tempCwd, true);
            if (Directory.Exists(tempHome))
                Directory.Delete(tempHome, true);
        }
    }
}
