namespace TsqlRefine.PluginSdk;

public sealed record Position(int Line, int Character);

public sealed record Range(Position Start, Position End);

public enum DiagnosticSeverity
{
    None = 0,
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

public enum DiagnosticTag
{
    None = 0,
    Unnecessary = 1,
    Deprecated = 2
}

public sealed record DiagnosticData(
    string? RuleId = null,
    string? Category = null,
    bool? Fixable = null
);

public sealed record Diagnostic(
    Range Range,
    string Message,
    DiagnosticSeverity? Severity = null,
    string? Code = null,
    string Source = "tsqlrefine",
    IReadOnlyList<DiagnosticTag>? Tags = null,
    DiagnosticData? Data = null
);
