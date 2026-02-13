using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

/// <summary>
/// Discourage cross-database transactions to avoid distributed transaction issues
/// </summary>
public sealed class CrossDatabaseTransactionRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "cross-database-transaction",
        Description: "Discourage cross-database transactions to avoid distributed transaction issues",
        Category: "Safety",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new CrossDatabaseTransactionVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CrossDatabaseTransactionVisitor : DiagnosticVisitorBase
    {
        private int _transactionDepth;
        private readonly HashSet<string> _databasesInTransaction = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TSqlFragment> _problemFragments = new();
        private const string DiagnosticMessage =
            "Cross-database transaction detected. This may cause distributed transaction issues and reduced reliability.";

        public override void ExplicitVisit(TSqlScript node)
        {
            base.ExplicitVisit(node);

            // Report cross-database usage even when an explicit transaction is left open.
            if (_transactionDepth > 0)
            {
                ReportIfCrossDatabaseTransaction();
                ResetTransactionTracking();
                _transactionDepth = 0;
            }
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            if (_transactionDepth == 0)
            {
                // Start a fresh scope when entering a new explicit transaction chain.
                ResetTransactionTracking();
            }

            _transactionDepth++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            if (_transactionDepth <= 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            _transactionDepth--;
            if (_transactionDepth == 0)
            {
                ReportIfCrossDatabaseTransaction();
                ResetTransactionTracking();
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            if (_transactionDepth <= 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            _transactionDepth--;
            if (_transactionDepth == 0)
            {
                ReportIfCrossDatabaseTransaction();
                ResetTransactionTracking();
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackStatementDatabaseReferences(node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackStatementDatabaseReferences(node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (_transactionDepth > 0)
            {
                TrackStatementDatabaseReferences(node);
            }

            base.ExplicitVisit(node);
        }

        private void TrackStatementDatabaseReferences(TSqlFragment statementFragment)
        {
            var databaseNamesInStatement = NamedTableReferenceCollector.CollectDatabaseNames(statementFragment);
            if (databaseNamesInStatement.Count == 0)
            {
                return;
            }

            foreach (var databaseName in databaseNamesInStatement)
            {
                _databasesInTransaction.Add(databaseName);
            }

            if (_databasesInTransaction.Count > 1 && !_problemFragments.Contains(statementFragment))
            {
                _problemFragments.Add(statementFragment);
            }
        }

        private void ReportIfCrossDatabaseTransaction()
        {
            if (_databasesInTransaction.Count <= 1)
            {
                return;
            }

            foreach (var fragment in _problemFragments)
            {
                AddDiagnostic(
                    fragment: fragment,
                    message: DiagnosticMessage,
                    code: "cross-database-transaction",
                    category: "Safety",
                    fixable: false
                );
            }
        }

        private void ResetTransactionTracking()
        {
            _databasesInTransaction.Clear();
            _problemFragments.Clear();
        }

        private sealed class NamedTableReferenceCollector : TSqlFragmentVisitor
        {
            private readonly HashSet<string> _databaseNames = new(StringComparer.OrdinalIgnoreCase);

            public static HashSet<string> CollectDatabaseNames(TSqlFragment fragment)
            {
                var collector = new NamedTableReferenceCollector();
                fragment.Accept(collector);
                return collector._databaseNames;
            }

            public override void ExplicitVisit(NamedTableReference node)
            {
                var databaseName = TryGetDatabaseName(node.SchemaObject);
                if (!string.IsNullOrEmpty(databaseName))
                {
                    _databaseNames.Add(databaseName);
                }

                base.ExplicitVisit(node);
            }

            private static string? TryGetDatabaseName(SchemaObjectName? schemaObject)
            {
                if (schemaObject is null)
                {
                    return null;
                }

                // 3-part: database.schema.object
                // 4-part: server.database.schema.object
                var identifiers = schemaObject.Identifiers;
                if (identifiers.Count < 3)
                {
                    return null;
                }

                return identifiers.Count == 3
                    ? identifiers[0].Value
                    : identifiers[1].Value;
            }
        }
    }
}
