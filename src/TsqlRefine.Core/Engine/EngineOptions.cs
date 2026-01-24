using TsqlRefine.Core.Config;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

public sealed record EngineOptions(
    int CompatLevel = 150,
    DiagnosticSeverity MinimumSeverity = DiagnosticSeverity.Hint,
    Ruleset? Ruleset = null,
    RuleSettings? RuleSettings = null
);

