using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

/// <summary>
/// Parses tsqlrefine disable/enable directives from comments.
/// </summary>
public static class DisableDirectiveParser
{
    private const string DisablePrefix = "tsqlrefine-disable";
    private const string EnablePrefix = "tsqlrefine-enable";

    /// <summary>
    /// Parses disable/enable directives from token stream.
    /// </summary>
    /// <param name="tokens">The token stream to parse.</param>
    /// <returns>List of directives found in comments.</returns>
    public static IReadOnlyList<DisableDirective> ParseDirectives(IReadOnlyList<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var directives = new List<DisableDirective>();

        foreach (var token in tokens)
        {
            if (!IsComment(token))
            {
                continue;
            }

            var directive = TryParseDirective(token);
            if (directive is not null)
            {
                directives.Add(directive);
            }
        }

        return directives;
    }

    /// <summary>
    /// Builds disabled ranges from parsed directives.
    /// </summary>
    /// <param name="directives">The directives to process.</param>
    /// <param name="totalLines">Total line count in the file (for open-ended ranges).</param>
    /// <returns>List of disabled ranges.</returns>
    public static IReadOnlyList<DisabledRange> BuildDisabledRanges(
        IReadOnlyList<DisableDirective> directives,
        int totalLines)
    {
        ArgumentNullException.ThrowIfNull(directives);

        if (directives.Count == 0)
        {
            return Array.Empty<DisabledRange>();
        }

        var ranges = new List<DisabledRange>();

        // Track open disables: key is rule ID (or empty string for "all rules")
        // Value is list of start lines (stack behavior for nested disables)
        var openDisables = new Dictionary<string, Stack<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var directive in directives)
        {
            if (directive.RuleIds.Count == 0)
            {
                // Global disable/enable (affects all rules)
                ProcessDirective(directive, string.Empty, openDisables, ranges);
            }
            else
            {
                // Rule-specific disable/enable
                foreach (var ruleId in directive.RuleIds)
                {
                    ProcessDirective(directive, ruleId, openDisables, ranges);
                }
            }
        }

        // Close any remaining open disables with end of file
        foreach (var (key, stack) in openDisables)
        {
            while (stack.Count > 0)
            {
                var startLine = stack.Pop();
                var ruleId = string.IsNullOrEmpty(key) ? null : key;
                ranges.Add(new DisabledRange(ruleId, startLine, null));
            }
        }

        return ranges;
    }

    /// <summary>
    /// Checks if a diagnostic should be suppressed based on disabled ranges.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to check.</param>
    /// <param name="disabledRanges">The disabled ranges.</param>
    /// <returns>True if the diagnostic should be suppressed.</returns>
    public static bool IsSuppressed(Diagnostic diagnostic, IReadOnlyList<DisabledRange> disabledRanges)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(disabledRanges);

        if (disabledRanges.Count == 0)
        {
            return false;
        }

        var line = diagnostic.Range.Start.Line;
        var ruleId = diagnostic.Code;

        foreach (var range in disabledRanges)
        {
            if (!IsLineInRange(line, range))
            {
                continue;
            }

            // Check if this range applies to the diagnostic
            if (range.RuleId is null)
            {
                // Global disable - affects all rules
                return true;
            }

            if (ruleId is not null && range.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase))
            {
                // Rule-specific disable matches this diagnostic's rule
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts total lines in text.
    /// </summary>
    public static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                lines++;
            }
            else if (ch == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static bool IsComment(Token token)
    {
        // Check TokenType if available
        if (token.TokenType is not null &&
            token.TokenType.Contains("Comment", StringComparison.Ordinal))
        {
            return true;
        }

        // Fallback: check text prefix
        var text = token.Text;
        return text.StartsWith("--", StringComparison.Ordinal) ||
               text.StartsWith("/*", StringComparison.Ordinal);
    }

    private static DisableDirective? TryParseDirective(Token token)
    {
        var text = token.Text;

        // Extract comment content
        string content;
        if (text.StartsWith("/*", StringComparison.Ordinal))
        {
            // Block comment: /* ... */
            var end = text.EndsWith("*/", StringComparison.Ordinal) ? text.Length - 2 : text.Length;
            content = text.Substring(2, end - 2).Trim();
        }
        else if (text.StartsWith("--", StringComparison.Ordinal))
        {
            // Line comment: -- ...
            content = text.Substring(2).Trim();
        }
        else
        {
            return null;
        }

        // Check for directive prefix (case-insensitive)
        DisableDirectiveType type;
        string remainder;

        if (TryMatchDirective(content, DisablePrefix, out remainder))
        {
            type = DisableDirectiveType.Disable;
        }
        else if (TryMatchDirective(content, EnablePrefix, out remainder))
        {
            type = DisableDirectiveType.Enable;
        }
        else
        {
            return null;
        }

        // Split on first colon to separate rule IDs from reason
        string? reason = null;
        var colonIndex = remainder.IndexOf(':');
        string ruleIdsPart;
        if (colonIndex >= 0)
        {
            ruleIdsPart = remainder[..colonIndex];
            var reasonText = remainder[(colonIndex + 1)..].Trim();
            reason = string.IsNullOrEmpty(reasonText) ? null : reasonText;
        }
        else
        {
            ruleIdsPart = remainder;
        }

        var ruleIds = ParseRuleIds(ruleIdsPart);

        return new DisableDirective(type, ruleIds, token.Start.Line, reason);
    }

    private static IReadOnlyList<string> ParseRuleIds(string remainder)
    {
        // Remainder could be empty, or contain rule IDs separated by commas
        // Format: "" or " rule-id" or " rule1, rule2, rule3"
        var trimmed = remainder.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Array.Empty<string>();
        }

        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var ruleIds = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            var ruleId = part.Trim();
            if (!string.IsNullOrEmpty(ruleId))
            {
                ruleIds.Add(ruleId);
            }
        }

        return ruleIds;
    }

    private static void ProcessDirective(
        DisableDirective directive,
        string key,
        Dictionary<string, Stack<int>> openDisables,
        List<DisabledRange> ranges)
    {
        if (directive.Type == DisableDirectiveType.Disable)
        {
            // Push onto stack
            if (!openDisables.TryGetValue(key, out var stack))
            {
                stack = new Stack<int>();
                openDisables[key] = stack;
            }

            stack.Push(directive.Line);
        }
        else
        {
            // Enable: pop from stack and create range
            if (openDisables.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                var startLine = stack.Pop();
                var ruleId = string.IsNullOrEmpty(key) ? null : key;
                ranges.Add(new DisabledRange(ruleId, startLine, directive.Line));
            }
            // Ignore enable without matching disable
        }
    }

    private static bool IsLineInRange(int line, DisabledRange range)
    {
        if (line < range.StartLine)
        {
            return false;
        }

        if (range.EndLine is null)
        {
            // Open-ended range (to end of file)
            return true;
        }

        return line < range.EndLine.Value;
    }

    private static bool TryMatchDirective(string content, string prefix, out string remainder)
    {
        remainder = string.Empty;

        if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check that prefix is followed by end-of-string, whitespace, or nothing else
        // This prevents "tsqlrefine-disabled" from matching "tsqlrefine-disable"
        if (content.Length == prefix.Length)
        {
            // Exact match
            remainder = string.Empty;
            return true;
        }

        var nextChar = content[prefix.Length];
        if (char.IsWhiteSpace(nextChar) || nextChar == ':')
        {
            // Followed by whitespace or colon (for rule IDs and/or reason)
            remainder = content[prefix.Length..];
            return true;
        }

        // Not a valid directive (e.g., "tsqlrefine-disabled")
        return false;
    }
}
