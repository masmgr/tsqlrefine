using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

/// <summary>
/// Groups a diagnostic with its associated rule and available fixes.
/// </summary>
internal sealed record DiagnosticFixGroup(IRule Rule, Diagnostic Diagnostic, IReadOnlyList<Fix> Fixes);
