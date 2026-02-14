using System.Text;
using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Config;
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

    // ================================================================
    // Plugin search path tests
    // ================================================================

    [Fact]
    public void ResolvePluginDescriptors_FilenameOnly_FoundInBaseDir_SetsResolvedPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var pluginFile = Path.Combine(tempDir, "MyPlugin.dll");
            File.WriteAllBytes(pluginFile, []);

            var configs = new[] { new PluginConfig("MyPlugin.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, tempDir, tempDir, tempDir);

            Assert.Single(result);
            Assert.NotNull(result[0].ResolvedFullPath);
            Assert.Equal(Path.GetFullPath(pluginFile), result[0].ResolvedFullPath);
            Assert.Equal("MyPlugin.dll", result[0].Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePluginDescriptors_FilenameOnly_FoundInCwdPlugins_SetsResolvedPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var cwdDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pluginsDir = Path.Combine(cwdDir, ".tsqlrefine", "plugins");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(pluginsDir);

        try
        {
            var pluginFile = Path.Combine(pluginsDir, "MyPlugin.dll");
            File.WriteAllBytes(pluginFile, []);

            var configs = new[] { new PluginConfig("MyPlugin.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, baseDir, cwdDir, baseDir);

            Assert.Single(result);
            Assert.NotNull(result[0].ResolvedFullPath);
            Assert.Equal(Path.GetFullPath(pluginFile), result[0].ResolvedFullPath);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
            if (Directory.Exists(cwdDir))
                Directory.Delete(cwdDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePluginDescriptors_FilenameOnly_FoundInHomePlugins_SetsResolvedPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var cwdDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var homeDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pluginsDir = Path.Combine(homeDir, ".tsqlrefine", "plugins");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(cwdDir);
        Directory.CreateDirectory(pluginsDir);

        try
        {
            var pluginFile = Path.Combine(pluginsDir, "MyPlugin.dll");
            File.WriteAllBytes(pluginFile, []);

            var configs = new[] { new PluginConfig("MyPlugin.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, baseDir, cwdDir, homeDir);

            Assert.Single(result);
            Assert.NotNull(result[0].ResolvedFullPath);
            Assert.Equal(Path.GetFullPath(pluginFile), result[0].ResolvedFullPath);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
            if (Directory.Exists(cwdDir))
                Directory.Delete(cwdDir, recursive: true);
            if (Directory.Exists(homeDir))
                Directory.Delete(homeDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePluginDescriptors_FilenameOnly_NotFound_ResolvedPathIsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configs = new[] { new PluginConfig("NonExistent.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, tempDir, tempDir, tempDir);

            Assert.Single(result);
            Assert.Null(result[0].ResolvedFullPath);
            Assert.Equal("NonExistent.dll", result[0].Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePluginDescriptors_RelativePathWithSeparator_NoSearchPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configs = new[] { new PluginConfig("plugins/my.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, tempDir, tempDir, tempDir);

            Assert.Single(result);
            Assert.Null(result[0].ResolvedFullPath);
            Assert.Equal("plugins/my.dll", result[0].Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePluginDescriptors_SearchOrder_BaseDirTakesPriority()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var cwdDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var cwdPluginsDir = Path.Combine(cwdDir, ".tsqlrefine", "plugins");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(cwdPluginsDir);

        try
        {
            // Place plugin in both baseDir and CWD/.tsqlrefine/plugins/
            File.WriteAllBytes(Path.Combine(baseDir, "MyPlugin.dll"), []);
            File.WriteAllBytes(Path.Combine(cwdPluginsDir, "MyPlugin.dll"), []);

            var configs = new[] { new PluginConfig("MyPlugin.dll") };
            var result = ConfigLoader.ResolvePluginDescriptors(configs, baseDir, cwdDir, cwdDir);

            Assert.Single(result);
            Assert.NotNull(result[0].ResolvedFullPath);
            // Should resolve to baseDir (first in search order)
            Assert.Equal(
                Path.GetFullPath(Path.Combine(baseDir, "MyPlugin.dll")),
                result[0].ResolvedFullPath);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
            if (Directory.Exists(cwdDir))
                Directory.Delete(cwdDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WithResolvedFullPath_UsesResolvedPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a dummy file (not a valid DLL, but will pass File.Exists)
            var pluginFile = Path.Combine(tempDir, "test.dll");
            File.WriteAllBytes(pluginFile, [0x00]);

            var descriptor = new PluginDescriptor("test.dll", true, pluginFile);
            var result = PluginLoader.Load([descriptor], tempDir);

            Assert.Single(result);
            // Should attempt to load (and fail because it's not a valid assembly),
            // but NOT get FileNotFound or PathRejected
            Assert.NotEqual(PluginLoadStatus.FileNotFound, result[0].Diagnostic.Status);
            Assert.NotEqual(PluginLoadStatus.PathRejected, result[0].Diagnostic.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WithResolvedFullPath_SkipsValidation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var otherDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(otherDir);

        try
        {
            // Create a dummy file in a directory outside baseDirectory
            var pluginFile = Path.Combine(otherDir, "test.dll");
            File.WriteAllBytes(pluginFile, [0x00]);

            // Path "test.dll" would normally be resolved relative to tempDir,
            // but ResolvedFullPath points to otherDir — should NOT be rejected
            var descriptor = new PluginDescriptor("test.dll", true, pluginFile);
            var result = PluginLoader.Load([descriptor], tempDir);

            Assert.Single(result);
            Assert.NotEqual(PluginLoadStatus.PathRejected, result[0].Diagnostic.Status);
            Assert.NotEqual(PluginLoadStatus.FileNotFound, result[0].Diagnostic.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(otherDir))
                Directory.Delete(otherDir, recursive: true);
        }
    }
}
