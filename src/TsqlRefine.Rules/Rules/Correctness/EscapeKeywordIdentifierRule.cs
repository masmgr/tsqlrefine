using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Warns when a T-SQL soft keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.
/// </summary>
public sealed class EscapeKeywordIdentifierRule : IRule
{
    private const string RuleId = "escape-keyword-identifier";
    private const string Category = "Correctness";

    /// <summary>
    /// T-SQL keywords that can be used as identifiers without causing parse errors,
    /// but should still be escaped for clarity and to avoid potential issues.
    /// These are "soft" keywords that SQL Server allows as unquoted identifiers
    /// in certain contexts.
    /// </summary>
    private static readonly FrozenSet<string> SoftKeywords = FrozenSet.ToFrozenSet(
        [
            "VALUE", "TYPE", "DATE", "TIME", "NAME", "STATUS",
            "COUNT", "SUM", "FIRST", "LAST", "MIN", "MAX", "AVG",
            "YEAR", "MONTH", "DAY", "HOUR", "MINUTE", "SECOND",
            "LEVEL", "STATE", "DATA", "ROLE", "LANGUAGE",
            "OUTPUT", "INPUT", "ABSOLUTE", "RELATIVE",
            "PATH", "CONTENT", "DOCUMENT", "ROWCOUNT",
        ],
        StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Warns when a T-SQL soft keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new KeywordIdentifierVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        var token = TokenHelpers.FindTokenByRange(context.Tokens, diagnostic.Range);
        if (token is null)
        {
            yield break;
        }

        yield return RuleHelpers.CreateReplaceFix("Escape keyword identifier", diagnostic.Range, $"[{token.Text}]");
    }

    /// <summary>
    /// AST visitor that identifies keyword identifiers that need escaping.
    /// </summary>
    private sealed class KeywordIdentifierVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(NamedTableReference node)
        {
            CheckAndReportIdentifier(node.SchemaObject?.BaseIdentifier);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            foreach (var identifier in node.MultiPartIdentifier?.Identifiers ?? [])
            {
                CheckAndReportIdentifier(identifier);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnDefinition node)
        {
            CheckAndReportIdentifier(node.ColumnIdentifier);
            base.ExplicitVisit(node);
        }

        private void CheckAndReportIdentifier(Identifier? identifier)
        {
            if (identifier is null ||
                identifier.QuoteType != QuoteType.NotQuoted ||
                !SoftKeywords.Contains(identifier.Value))
            {
                return;
            }

            AddDiagnostic(
                fragment: identifier,
                message: $"Identifier '{identifier.Value}' is a T-SQL soft keyword. Escape it as [{identifier.Value}] for clarity.",
                code: RuleId,
                category: Category,
                fixable: true);
        }
    }
}
