namespace TsqlRefine.Core.Engine;

/// <summary>
/// Type of disable directive.
/// </summary>
public enum DisableDirectiveType
{
    /// <summary>Disables rule(s) from this point onward.</summary>
    Disable,

    /// <summary>Re-enables rule(s) from this point onward.</summary>
    Enable
}

/// <summary>
/// Represents a tsqlrefine disable/enable directive found in a comment.
/// </summary>
/// <param name="Type">Whether this is a disable or enable directive.</param>
/// <param name="RuleIds">Specific rule IDs to disable/enable, or empty for all rules.</param>
/// <param name="Line">The 0-based line number where the directive appears.</param>
/// <param name="Reason">Optional reason text explaining why rules are disabled.</param>
public sealed record DisableDirective(
    DisableDirectiveType Type,
    IReadOnlyList<string> RuleIds,
    int Line,
    string? Reason = null
);

/// <summary>
/// Represents a range of lines where a rule is disabled.
/// </summary>
/// <param name="RuleId">The rule ID, or null for all rules.</param>
/// <param name="StartLine">The 0-based start line (inclusive).</param>
/// <param name="EndLine">The 0-based end line (exclusive), or null for end of file.</param>
public sealed record DisabledRange(
    string? RuleId,
    int StartLine,
    int? EndLine
);
