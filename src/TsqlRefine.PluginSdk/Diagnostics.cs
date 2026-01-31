namespace TsqlRefine.PluginSdk;

/// <summary>
/// Represents a position in a source file using 0-based line and character indices.
/// </summary>
/// <param name="Line">0-based line number.</param>
/// <param name="Character">0-based character position within the line.</param>
public sealed record Position(int Line, int Character);

/// <summary>
/// Represents a range in a source file.
/// </summary>
/// <param name="Start">The inclusive start position.</param>
/// <param name="End">The exclusive end position.</param>
public sealed record Range(Position Start, Position End);

/// <summary>
/// Severity level of a diagnostic, following LSP (Language Server Protocol) conventions.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>No severity specified.</summary>
    None = 0,
    /// <summary>A critical issue that should be addressed immediately.</summary>
    Error = 1,
    /// <summary>An important issue that should be addressed but is not critical.</summary>
    Warning = 2,
    /// <summary>An informational message.</summary>
    Information = 3,
    /// <summary>A lightweight suggestion or hint.</summary>
    Hint = 4
}

/// <summary>
/// Tags that provide additional context for a diagnostic.
/// </summary>
public enum DiagnosticTag
{
    /// <summary>No tag specified.</summary>
    None = 0,
    /// <summary>Indicates code that is unnecessary and can be removed.</summary>
    Unnecessary = 1,
    /// <summary>Indicates deprecated syntax or features.</summary>
    Deprecated = 2
}

/// <summary>
/// Additional metadata associated with a diagnostic.
/// </summary>
/// <param name="RuleId">The rule identifier for grouping and filtering. May duplicate Diagnostic.Code for metadata purposes.</param>
/// <param name="Category">The category of the rule (e.g., "Performance", "Security", "Style").</param>
/// <param name="Fixable">Whether this diagnostic can be auto-fixed.</param>
public sealed record DiagnosticData(
    string? RuleId = null,
    string? Category = null,
    bool? Fixable = null
);

/// <summary>
/// Represents a diagnostic (issue, warning, or error) reported by a rule.
/// </summary>
/// <param name="Range">The source location of the diagnostic.</param>
/// <param name="Message">A human-readable description of the issue.</param>
/// <param name="Severity">The severity level. If null, falls back to the rule's default severity.</param>
/// <param name="Code">The rule identifier that produced this diagnostic, used for display and filtering.</param>
/// <param name="Source">The source of the diagnostic. Defaults to "tsqlrefine".</param>
/// <param name="Tags">Optional tags providing additional context (e.g., Unnecessary, Deprecated).</param>
/// <param name="Data">Optional metadata for grouping and categorization.</param>
public sealed record Diagnostic(
    Range Range,
    string Message,
    DiagnosticSeverity? Severity = null,
    string? Code = null,
    string Source = "tsqlrefine",
    IReadOnlyList<DiagnosticTag>? Tags = null,
    DiagnosticData? Data = null
);
