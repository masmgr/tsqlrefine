using TsqlRefine.Core.Config;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

/// <summary>
/// Configuration options for the TsqlRefine analysis engine.
/// </summary>
/// <param name="CompatLevel">SQL Server compatibility level (100-160). Default is 150 (SQL Server 2019).</param>
/// <param name="MinimumSeverity">Minimum diagnostic severity to report. Default is Hint (reports all diagnostics).</param>
/// <param name="Ruleset">The ruleset configuration specifying which rules to run.</param>
/// <param name="RuleSettings">Per-rule configuration settings.</param>
public sealed record EngineOptions(
    int CompatLevel = 150,
    DiagnosticSeverity MinimumSeverity = DiagnosticSeverity.Hint,
    Ruleset? Ruleset = null,
    RuleSettings? RuleSettings = null
);

