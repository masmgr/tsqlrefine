using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

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
    private static readonly FrozenSet<string> SoftKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Common T-SQL keywords that parse successfully as identifiers
        "VALUE",
        "TYPE",
        "DATE",
        "TIME",
        "NAME",
        "STATUS",
        "COUNT",
        "SUM",
        "FIRST",
        "LAST",
        "MIN",
        "MAX",
        "AVG",
        "YEAR",
        "MONTH",
        "DAY",
        "HOUR",
        "MINUTE",
        "SECOND",
        "LEVEL",
        "STATE",
        "DATA",
        "ROLE",
        "LANGUAGE",
        "OUTPUT",
        "INPUT",
        "ABSOLUTE",
        "RELATIVE",
        "PATH",
        "CONTENT",
        "DOCUMENT",
        "ROWCOUNT",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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
    /// Uses ScriptDom's parsing to correctly identify identifier context.
    /// </summary>
    private sealed class KeywordIdentifierVisitor : DiagnosticVisitorBase
    {
        /// <summary>
        /// Checks table names in FROM, JOIN, UPDATE, etc.
        /// </summary>
        public override void ExplicitVisit(NamedTableReference node)
        {
            // Check the base table name (last part of schema.table)
            var identifier = node.SchemaObject?.BaseIdentifier;
            if (identifier != null && NeedsEscaping(identifier))
            {
                AddIdentifierDiagnostic(identifier);
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks column references like t.order, SELECT order FROM ...
        /// </summary>
        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier?.Identifiers == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Check each part of the multi-part identifier
            // e.g., for "t.order", check both "t" and "order"
            foreach (var identifier in node.MultiPartIdentifier.Identifiers)
            {
                if (NeedsEscaping(identifier))
                {
                    AddIdentifierDiagnostic(identifier);
                }
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks column definitions in CREATE TABLE.
        /// </summary>
        public override void ExplicitVisit(ColumnDefinition node)
        {
            var identifier = node.ColumnIdentifier;
            if (identifier != null && NeedsEscaping(identifier))
            {
                AddIdentifierDiagnostic(identifier);
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks if an identifier needs escaping.
        /// Returns true if the identifier is not quoted and is a T-SQL keyword.
        /// </summary>
        private static bool NeedsEscaping(Identifier? identifier)
        {
            if (identifier == null)
            {
                return false;
            }

            // Already escaped with brackets or double quotes
            if (identifier.QuoteType != QuoteType.NotQuoted)
            {
                return false;
            }

            // Check if it's a soft keyword that should be escaped
            return SoftKeywords.Contains(identifier.Value);
        }

        private void AddIdentifierDiagnostic(Identifier identifier)
        {
            var escaped = $"[{identifier.Value}]";

            AddDiagnostic(
                fragment: identifier,
                message: $"Identifier '{identifier.Value}' is a T-SQL soft keyword. Escape it as {escaped} for clarity.",
                code: RuleId,
                category: Category,
                fixable: true
            );
        }
    }
}
