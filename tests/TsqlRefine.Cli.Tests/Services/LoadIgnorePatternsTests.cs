using TsqlRefine.Cli.Services;

namespace TsqlRefine.Cli.Tests.Services;

[Collection("DirectoryChanging")]
public sealed class LoadIgnorePatternsTests
{
    [Fact]
    public void LoadIgnorePatterns_WithCommentLines_SkipsComments()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "# comment line\nbin/\n# another comment\nobj/\n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Equal(2, patterns.Count);
            Assert.Equal("bin/", patterns[0]);
            Assert.Equal("obj/", patterns[1]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_WithBlankLines_SkipsBlanks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "\n\nbin/\n\n\nobj/\n\n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Equal(2, patterns.Count);
            Assert.Equal("bin/", patterns[0]);
            Assert.Equal("obj/", patterns[1]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_WithWhitespace_TrimsPatterns()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "  bin/  \n\tobj/\t\n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Equal(2, patterns.Count);
            Assert.Equal("bin/", patterns[0]);
            Assert.Equal("obj/", patterns[1]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_WithMixedContent_ReturnsOnlyPatterns()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "# Header comment\n\nbin/\n# Inline comment\n\nobj/\n  \n*.bak\n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Equal(3, patterns.Count);
            Assert.Equal("bin/", patterns[0]);
            Assert.Equal("obj/", patterns[1]);
            Assert.Equal("*.bak", patterns[2]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_WithNullPath_NoFileExists_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            // Set CWD to a temp dir with no .tsqlrefine/ directory
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            var patterns = ConfigLoader.LoadIgnorePatterns(null);

            Assert.Empty(patterns);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_WithExplicitPath_NotFound_ThrowsConfigException()
    {
        var ex = Assert.Throws<ConfigException>(
            () => ConfigLoader.LoadIgnorePatterns("/nonexistent/path/to/ignore.txt"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void LoadIgnorePatterns_WithHashOnlyLines_SkipsAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "#\n##\n# \n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Empty(patterns);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadIgnorePatterns_PreservesPatternOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            File.WriteAllText(ignoreFile, "zebra/\nalpha/\nmiddle/\n");

            var patterns = ConfigLoader.LoadIgnorePatterns(ignoreFile);

            Assert.Equal(3, patterns.Count);
            Assert.Equal("zebra/", patterns[0]);
            Assert.Equal("alpha/", patterns[1]);
            Assert.Equal("middle/", patterns[2]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
