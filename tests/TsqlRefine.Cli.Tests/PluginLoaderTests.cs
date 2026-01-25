using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Tests;

public sealed class PluginLoaderTests
{
    [Fact]
    public void Load_WhenDisabled_ReturnsDisabledStatus()
    {
        var plugins = new[] { new PluginDescriptor("test.dll", Enabled: false) };
        var results = PluginLoader.Load(plugins);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(PluginLoadStatus.Disabled, result.Diagnostic.Status);
        Assert.False(result.Enabled);
        Assert.Empty(result.Providers);
        Assert.NotNull(result.Diagnostic.Message);
    }

    [Fact]
    public void Load_WhenFileNotFound_ReturnsFileNotFoundStatus()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
        var plugins = new[] { new PluginDescriptor(nonExistentPath, Enabled: true) };
        var results = PluginLoader.Load(plugins);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(PluginLoadStatus.FileNotFound, result.Diagnostic.Status);
        Assert.True(result.Enabled);
        Assert.Empty(result.Providers);
        Assert.Contains("not found", result.Diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenEmpty_ReturnsEmpty()
    {
        var results = PluginLoader.Load(Array.Empty<PluginDescriptor>());
        Assert.Empty(results);
    }

    [Fact]
    public void LoadedPlugin_ErrorProperty_ReturnsNullOnSuccess()
    {
        var diagnostic = new PluginLoadDiagnostic(PluginLoadStatus.Success, "Success message");
        var plugin = new LoadedPlugin("test.dll", true, Array.Empty<IRuleProvider>(), diagnostic);

        Assert.Null(plugin.Error);
    }

    [Fact]
    public void LoadedPlugin_ErrorProperty_ReturnsMessageOnError()
    {
        var diagnostic = new PluginLoadDiagnostic(PluginLoadStatus.LoadError, "Load failed");
        var plugin = new LoadedPlugin("test.dll", true, Array.Empty<IRuleProvider>(), diagnostic);

        Assert.Equal("Load failed", plugin.Error);
    }

    [Fact]
    public void PluginLoadDiagnostic_VersionMismatch_ContainsVersionInfo()
    {
        var diagnostic = new PluginLoadDiagnostic(
            PluginLoadStatus.VersionMismatch,
            "Version mismatch",
            ActualApiVersion: 2,
            ExpectedApiVersion: 1);

        Assert.Equal(PluginLoadStatus.VersionMismatch, diagnostic.Status);
        Assert.Equal("Version mismatch", diagnostic.Message);
        Assert.Equal(2, diagnostic.ActualApiVersion);
        Assert.Equal(1, diagnostic.ExpectedApiVersion);
    }

    [Fact]
    public void PluginLoadDiagnostic_LoadError_ContainsExceptionInfo()
    {
        var diagnostic = new PluginLoadDiagnostic(
            PluginLoadStatus.LoadError,
            "Failed to load",
            ExceptionType: "FileNotFoundException",
            StackTrace: "at System.IO.File.Load()");

        Assert.Equal(PluginLoadStatus.LoadError, diagnostic.Status);
        Assert.Equal("Failed to load", diagnostic.Message);
        Assert.Equal("FileNotFoundException", diagnostic.ExceptionType);
        Assert.Equal("at System.IO.File.Load()", diagnostic.StackTrace);
    }
}
