using System.Text;
using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Config;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;

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

    [Fact]
    public void ValidatePluginPath_CaseDifferentSibling_IsRejectedOnCaseSensitivePlatforms()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var baseDirectory = Path.Combine(root, "project");

        var result = PluginLoader.ValidatePluginPath("../PROJECT/plugin.dll", baseDirectory);

        Assert.NotNull(result);
        Assert.Contains("escapes", result);
    }

    [Fact]
    public void ValidateResolvedPluginPath_WithinTrustedDirectory_ReturnsNull()
    {
        var trustedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var resolvedPath = Path.Combine(trustedDirectory, "plugin.dll");

        var result = PluginLoader.ValidateResolvedPluginPath(resolvedPath, [trustedDirectory]);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateResolvedPluginPath_OutsideTrustedDirectory_ReturnsError()
    {
        var trustedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var resolvedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "plugin.dll");

        var result = PluginLoader.ValidateResolvedPluginPath(resolvedPath, [trustedDirectory]);

        Assert.NotNull(result);
        Assert.Contains("outside", result);
    }

    [Fact]
    public void ValidateResolvedPluginPath_RelativePath_ReturnsError()
    {
        var result = PluginLoader.ValidateResolvedPluginPath("plugin.dll", [Path.GetTempPath()]);

        Assert.NotNull(result);
        Assert.Contains("absolute", result);
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
    public void Load_WithResolvedFullPath_OutsideTrustedDirectory_IsRejected()
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
            // but ResolvedFullPath points outside the trusted directory.
            var descriptor = new PluginDescriptor("test.dll", true, pluginFile);
            var result = PluginLoader.Load([descriptor], tempDir);

            Assert.Single(result);
            Assert.Equal(PluginLoadStatus.PathRejected, result[0].Diagnostic.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(otherDir))
                Directory.Delete(otherDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WithResolvedFullPath_InAdditionalTrustedDirectory_IsAllowed()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pluginDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(pluginDir);

        try
        {
            var pluginFile = Path.Combine(pluginDir, "test.dll");
            File.WriteAllBytes(pluginFile, [0x00]);

            var descriptor = new PluginDescriptor("test.dll", true, pluginFile);
            var result = PluginLoader.Load([descriptor], baseDir, [pluginDir]);

            Assert.Single(result);
            Assert.NotEqual(PluginLoadStatus.PathRejected, result[0].Diagnostic.Status);
            Assert.NotEqual(PluginLoadStatus.FileNotFound, result[0].Diagnostic.Status);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
            if (Directory.Exists(pluginDir))
                Directory.Delete(pluginDir, recursive: true);
        }
    }

    [Fact]
    public void TryAppendPluginRules_WhenRuleIdDuplicatesExistingRule_DisablesPlugin()
    {
        var existingRules = new List<IRule> { new TestRule("duplicate-rule") };
        var knownRuleIds = new HashSet<string>(
            existingRules.Select(r => r.Metadata.RuleId),
            StringComparer.OrdinalIgnoreCase);
        var plugin = CreateLoadedPlugin("plugin.dll", new TestRule("duplicate-rule"));
        var stderr = new StringWriter();

        var added = ConfigLoader.TryAppendPluginRules(
            existingRules, plugin, knownRuleIds, stderr, quiet: false);

        Assert.False(added);
        Assert.Single(existingRules);
        Assert.Contains("duplicate-rule", stderr.ToString());
    }

    [Fact]
    public void TryAppendPluginRules_WhenRuleIdDuplicatesWithinPlugin_DisablesPlugin()
    {
        var existingRules = new List<IRule>();
        var knownRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plugin = CreateLoadedPlugin(
            "plugin.dll",
            new TestRule("plugin-rule"),
            new TestRule("PLUGIN-RULE"));
        var stderr = new StringWriter();

        var added = ConfigLoader.TryAppendPluginRules(
            existingRules, plugin, knownRuleIds, stderr, quiet: false);

        Assert.False(added);
        Assert.Empty(existingRules);
        Assert.Contains("plugin-rule", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryAppendPluginRules_WhenRuleIdsAreUnique_AddsPluginRules()
    {
        var existingRules = new List<IRule> { new TestRule("existing-rule") };
        var knownRuleIds = new HashSet<string>(
            existingRules.Select(r => r.Metadata.RuleId),
            StringComparer.OrdinalIgnoreCase);
        var plugin = CreateLoadedPlugin(
            "plugin.dll",
            new TestRule("plugin-rule-1"),
            new TestRule("plugin-rule-2"));
        var stderr = new StringWriter();

        var added = ConfigLoader.TryAppendPluginRules(
            existingRules, plugin, knownRuleIds, stderr, quiet: false);

        Assert.True(added);
        Assert.Equal(3, existingRules.Count);
        Assert.Contains("plugin-rule-1", knownRuleIds);
        Assert.Contains("plugin-rule-2", knownRuleIds);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    private static LoadedPlugin CreateLoadedPlugin(string path, params IRule[] rules)
    {
        return new LoadedPlugin(
            path,
            enabled: true,
            providers: [new TestRuleProvider(rules)],
            diagnostic: new PluginLoadDiagnostic(PluginLoadStatus.Success));
    }

    private sealed class TestRuleProvider(IReadOnlyList<IRule> rules) : IRuleProvider
    {
        public string Name => "Test Provider";

        public int PluginApiVersion => PluginApi.CurrentVersion;

        public IReadOnlyList<IRule> GetRules() => rules;
    }

    private sealed class TestRule(string ruleId) : IRule
    {
        public RuleMetadata Metadata { get; } = new(
            ruleId,
            "Test rule",
            "Test",
            RuleSeverity.Information,
            Fixable: false);

        public IEnumerable<Diagnostic> Analyze(RuleContext context) => [];

        public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) => [];
    }
}
