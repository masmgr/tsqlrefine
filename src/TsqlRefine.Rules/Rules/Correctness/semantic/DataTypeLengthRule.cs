using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY).
/// </summary>
public sealed class DataTypeLengthRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/data-type-length",
        Description: "Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY).",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new DataTypeLengthVisitor();
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
            return [];
        }

        if (context.Ast.Fragment is null)
        {
            return [];
        }

        // Find the SqlDataTypeReference at the diagnostic range
        var visitor = new DataTypeFinder(diagnostic.Range);
        context.Ast.Fragment.Accept(visitor);

        if (visitor.FoundDataType is null)
        {
            return [];
        }

        var defaultLength = GetDefaultLength(visitor.FoundDataType.SqlDataTypeOption);
        return [RuleHelpers.CreateInsertFix($"Add ({defaultLength}) length specification", diagnostic.Range.End, $"({defaultLength})")];
    }

    private static string GetDefaultLength(SqlDataTypeOption dataType) => dataType switch
    {
        SqlDataTypeOption.VarChar => "50",
        SqlDataTypeOption.NVarChar => "50",
        SqlDataTypeOption.Char => "1",
        SqlDataTypeOption.NChar => "1",
        SqlDataTypeOption.VarBinary => "50",
        SqlDataTypeOption.Binary => "1",
        _ => "1"
    };

    private sealed class DataTypeFinder : TSqlFragmentVisitor
    {
        private readonly TsqlRefine.PluginSdk.Range _targetRange;

        public DataTypeFinder(TsqlRefine.PluginSdk.Range targetRange)
        {
            _targetRange = targetRange;
        }

        public SqlDataTypeReference? FoundDataType { get; private set; }

        public override void ExplicitVisit(SqlDataTypeReference node)
        {
            var range = ScriptDomHelpers.GetRange(node);
            if (range == _targetRange)
            {
                FoundDataType = node;
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed class DataTypeLengthVisitor : DiagnosticVisitorBase
    {
        private static readonly FrozenSet<SqlDataTypeOption> VariableLengthTypes = FrozenSet.ToFrozenSet(
        [
            SqlDataTypeOption.VarChar,
            SqlDataTypeOption.NVarChar,
            SqlDataTypeOption.Char,
            SqlDataTypeOption.NChar,
            SqlDataTypeOption.VarBinary,
            SqlDataTypeOption.Binary
        ]);

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                CheckDataType(declaration.DataType);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnDefinition node)
        {
            CheckDataType(node.DataType);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            CheckDataType(node.DataType);
            base.ExplicitVisit(node);
        }

        private void CheckDataType(DataTypeReference? dataType)
        {
            if (dataType is not SqlDataTypeReference sqlDataType)
            {
                return;
            }

            // Check if this is a variable-length type
            if (!VariableLengthTypes.Contains(sqlDataType.SqlDataTypeOption))
            {
                return;
            }

            // Check if length is specified
            // Parameters.Count == 0 means no length specified
            // Parameters.Count > 0 means length is specified (could be a number or MAX)
            if (sqlDataType.Parameters.Count == 0)
            {
                var typeName = sqlDataType.SqlDataTypeOption.ToString().ToUpperInvariant();
                AddDiagnostic(
                    fragment: sqlDataType,
                    message: $"Variable-length data type '{typeName}' must have an explicit length specification. Use {typeName}(n) or {typeName}(MAX).",
                    code: "semantic/data-type-length",
                    category: "Correctness",
                    fixable: true
                );
            }
        }
    }
}
