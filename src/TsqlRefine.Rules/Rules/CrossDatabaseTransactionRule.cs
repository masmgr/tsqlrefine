using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class CrossDatabaseTransactionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "cross-database-transaction",
        Description: "Discourage cross-database transactions to avoid distributed transaction issues",
        Category: "Safety",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new CrossDatabaseTransactionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CrossDatabaseTransactionVisitor : DiagnosticVisitorBase
    {
        private int _transactionDepth = 0;
        private readonly HashSet<string> _databasesInTransaction = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TSqlFragment> _problemFragments = new();

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _transactionDepth++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            if (_transactionDepth > 0)
            {
                _transactionDepth--;
                if (_transactionDepth == 0)
                {
                    // End of transaction scope - report if cross-database
                    if (_databasesInTransaction.Count > 1)
                    {
                        foreach (var fragment in _problemFragments)
                        {
                            AddDiagnostic(
                                fragment: fragment,
                                message: "Cross-database transaction detected. This may cause distributed transaction issues and reduced reliability.",
                                code: "cross-database-transaction",
                                category: "Safety",
                                fixable: false
                            );
                        }
                    }

                    // Reset tracking
                    _databasesInTransaction.Clear();
                    _problemFragments.Clear();
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            if (_transactionDepth > 0)
            {
                _transactionDepth--;
                if (_transactionDepth == 0)
                {
                    // End of transaction scope - report if cross-database
                    if (_databasesInTransaction.Count > 1)
                    {
                        foreach (var fragment in _problemFragments)
                        {
                            AddDiagnostic(
                                fragment: fragment,
                                message: "Cross-database transaction detected. This may cause distributed transaction issues and reduced reliability.",
                                code: "cross-database-transaction",
                                category: "Safety",
                                fixable: false
                            );
                        }
                    }

                    // Reset tracking
                    _databasesInTransaction.Clear();
                    _problemFragments.Clear();
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackTableReference(node.InsertSpecification?.Target as NamedTableReference, node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackTableReference(node.UpdateSpecification?.Target as NamedTableReference, node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackTableReference(node.DeleteSpecification?.Target as NamedTableReference, node);
            }

            base.ExplicitVisit(node);
        }

        private void TrackTableReference(NamedTableReference? tableRef, TSqlFragment statementFragment)
        {
            if (tableRef?.SchemaObject != null)
            {
                // Extract database name (3-part name: database.schema.table or 4-part for linked server)
                var identifiers = tableRef.SchemaObject.Identifiers;
                if (identifiers.Count >= 3)
                {
                    // 3-part or 4-part identifier - has explicit database
                    var databaseName = identifiers.Count == 3
                        ? identifiers[0].Value
                        : identifiers[1].Value; // 4-part: skip server name

                    if (!string.IsNullOrEmpty(databaseName))
                    {
                        _databasesInTransaction.Add(databaseName);
                        if (_databasesInTransaction.Count > 1)
                        {
                            _problemFragments.Add(statementFragment);
                        }
                    }
                }
            }
        }
    }
}
