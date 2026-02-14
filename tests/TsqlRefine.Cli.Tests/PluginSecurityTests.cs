using System.Text;
using TsqlRefine.PluginHost;

namespace TsqlRefine.Cli.Tests;

public sealed class PluginSecurityTests
{
    // ================================================================
    // ValidatePluginPath tests
    // ================================================================

    [Fact]
    public void ValidatePluginPath_RelativePath_ReturnsNull()
    {
        var result = PluginLoader.ValidatePluginPath("plugins/my.dll", "/project");
        Assert.Null(result);
    }

    [Fact]
    public void ValidatePluginPath_SubdirectoryRelative_ReturnsNull()
    {
        var result = PluginLoader.ValidatePluginPath("plugins/sub/my.dll", "/project");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(@"\\server\share\evil.dll")]
    [InlineData("//server/share/evil.dll")]
    public void ValidatePluginPath_UncPath_ReturnsError(string path)
    {
        var result = PluginLoader.ValidatePluginPath(path, "/project");
        Assert.NotNull(result);
        Assert.Contains("UNC", result);
    }

    [Fact]
    public void ValidatePluginPath_AbsolutePath_ReturnsError()
    {
        var result = PluginLoader.ValidatePluginPath("/usr/lib/evil.dll", "/project");
        Assert.NotNull(result);
        Assert.Contains("Absolute", result);
    }

    [Fact]
    public void ValidatePluginPath_WindowsAbsolutePath_ReturnsError()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = PluginLoader.ValidatePluginPath(@"C:\evil.dll", @"C:\project");
        Assert.NotNull(result);
        Assert.Contains("Absolute", result);
    }

    [Fact]
    public void ValidatePluginPath_PathTraversal_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "sub");
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = PluginLoader.ValidatePluginPath("../../evil.dll", tempDir);
            Assert.NotNull(result);
            Assert.Contains("escapes", result);
        }
        finally
        {
            var parentDir = Path.GetDirectoryName(tempDir)!;
            if (Directory.Exists(parentDir))
                Directory.Delete(parentDir, recursive: true);
        }
    }

    // ================================================================
    // --allow-plugins CLI flag tests
    // ================================================================

    [Fact]
    public void Parse_AllowPluginsFlag_ParsedCorrectly()
    {
        var args = CliParser.Parse(["lint", "--allow-plugins", "--stdin"]);
        Assert.True(args.AllowPlugins);
    }

    [Fact]
    public void Parse_NoAllowPluginsFlag_DefaultsFalse()
    {
        var args = CliParser.Parse(["lint", "--stdin"]);
        Assert.False(args.AllowPlugins);
    }

    [Fact]
    public async Task Lint_WithPluginsConfigured_WithoutFlag_ShowsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            var configContent = """
                {
                    "compatLevel": 150,
                    "plugins": [
                        { "path": "test-plugin.dll", "enabled": true }
                    ]
                }
                """;
            await File.WriteAllTextAsync(configPath, configContent, Encoding.UTF8);

            var sqlPath = Path.Combine(tempDir, "test.sql");
            await File.WriteAllTextAsync(sqlPath, "SELECT 1;", Encoding.UTF8);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            await CliApp.RunAsync(["lint", "--config", configPath, sqlPath], stdin, stdout, stderr);

            var stderrOutput = stderr.ToString();
            Assert.Contains("--allow-plugins", stderrOutput);
            Assert.Contains("not loaded", stderrOutput);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ListPlugins_WithoutFlag_ShowsNotLoaded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            var configContent = """
                {
                    "compatLevel": 150,
                    "plugins": [
                        { "path": "test-plugin.dll", "enabled": true }
                    ]
                }
                """;
            await File.WriteAllTextAsync(configPath, configContent, Encoding.UTF8);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["list-plugins", "--config", configPath], stdin, stdout, stderr);

            Assert.Equal(0, code);
            var output = stdout.ToString();
            Assert.Contains("not loaded", output);
            var stderrOutput = stderr.ToString();
            Assert.Contains("--allow-plugins", stderrOutput);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
