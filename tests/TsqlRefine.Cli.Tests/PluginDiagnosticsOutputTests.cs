using TsqlRefine.Cli.Services;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Tests;

public sealed class PluginDiagnosticsOutputTests
{
    private static LoadedPlugin CreateLoadErrorPlugin(
        string? stackTrace = null,
        IReadOnlyList<string>? probeAttempts = null,
        string? missingNativeDll = null)
    {
        return new LoadedPlugin(
            "test-plugin.dll",
            true,
            Array.Empty<IRuleProvider>(),
            new PluginLoadDiagnostic(
                PluginLoadStatus.LoadError,
                "Test load error",
                ExceptionType: "DllNotFoundException",
                StackTrace: stackTrace,
                NativeDllProbeAttempts: probeAttempts,
                MissingNativeDll: missingNativeDll));
    }

    [Fact]
    public async Task WritePluginSummary_NonVerbose_DoesNotShowStackTrace()
    {
        var plugin = CreateLoadErrorPlugin(
            stackTrace: "   at System.Runtime.Loader.Load()\n   at PluginLoader.Load()\n   at Main()");

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: false, stdout);

        var output = stdout.ToString();
        Assert.DoesNotContain("Stack Trace", output);
        Assert.DoesNotContain("at System.Runtime", output);
        Assert.DoesNotContain("at PluginLoader", output);
    }

    [Fact]
    public async Task WritePluginSummary_NonVerbose_DoesNotShowProbeAttempts()
    {
        var plugin = CreateLoadErrorPlugin(
            missingNativeDll: "native.dll",
            probeAttempts: ["/usr/lib/native.dll", "/opt/plugins/native.dll"]);

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: false, stdout);

        var output = stdout.ToString();
        Assert.DoesNotContain("Probe Attempts", output);
        Assert.DoesNotContain("/usr/lib/native.dll", output);
        Assert.DoesNotContain("Missing Native DLL", output);
    }

    [Fact]
    public async Task WritePluginSummary_Verbose_ShowsStackTrace()
    {
        var plugin = CreateLoadErrorPlugin(
            stackTrace: "   at System.Runtime.Loader.Load()\n   at PluginLoader.Load()");

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: true, stdout);

        var output = stdout.ToString();
        Assert.Contains("Stack Trace", output);
        Assert.Contains("at System.Runtime.Loader.Load()", output);
    }

    [Fact]
    public async Task WritePluginSummary_Verbose_ShowsProbeAttempts()
    {
        var plugin = CreateLoadErrorPlugin(
            missingNativeDll: "native.dll",
            probeAttempts: ["/usr/lib/native.dll", "/opt/plugins/native.dll"]);

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: true, stdout);

        var output = stdout.ToString();
        Assert.Contains("Probe Attempts", output);
        Assert.Contains("/usr/lib/native.dll", output);
        Assert.Contains("/opt/plugins/native.dll", output);
    }

    [Fact]
    public async Task WritePluginSummary_NonVerbose_ShowsErrorMessage()
    {
        var plugin = CreateLoadErrorPlugin(stackTrace: "   at System.Something()");

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: false, stdout);

        var output = stdout.ToString();
        Assert.Contains("DllNotFoundException", output);
        Assert.Contains("Test load error", output);
    }

    [Fact]
    public async Task WritePluginSummary_NonVerbose_ShowsVerboseHint()
    {
        var plugin = CreateLoadErrorPlugin(
            stackTrace: "   at System.Something()",
            missingNativeDll: "native.dll",
            probeAttempts: ["/path/native.dll"]);

        var stdout = new StringWriter();
        await PluginDiagnostics.WritePluginSummaryAsync([plugin], verbose: false, stdout);

        var output = stdout.ToString();
        Assert.Contains("--verbose", output);
    }
}
