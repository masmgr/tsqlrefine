using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Metrics;

/// <summary>Metrics calculated for one SQL object or standalone batch.</summary>
public sealed record SqlObjectMetrics(
    string Name,
    string Kind,
    TSqlFragment Location,
    int CyclomaticComplexity,
    int NestingDepth,
    int StatementCount,
    int MaxJoinsPerQuery,
    int ParameterCount);

/// <summary>Collects inexpensive structural metrics from ScriptDOM nodes.</summary>
public static class SqlMetricsCollector
{
    private static readonly ConditionalWeakTable<TSqlFragment, SqlObjectMetrics[]> s_cache = new();

    public static IReadOnlyList<SqlObjectMetrics> Collect(TSqlFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return s_cache.GetValue(fragment, static value => CollectCore(value));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "Existing metrics aggregation logic; tracked as complexity baseline debt.")]
    private static SqlObjectMetrics[] CollectCore(TSqlFragment fragment)
    {
        if (fragment is not TSqlScript script)
        {
            return [];
        }

        var results = new List<SqlObjectMetrics>();
        for (var batchIndex = 0; batchIndex < script.Batches.Count; batchIndex++)
        {
            var batch = script.Batches[batchIndex];
            var objectCount = 0;
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case ProcedureStatementBody procedure:
                        objectCount++;
                        if (procedure.StatementList is not null)
                        {
                            results.Add(Measure(
                                GetName(procedure.ProcedureReference?.Name),
                                "Procedure",
                                (TSqlFragment?)procedure.ProcedureReference?.Name.BaseIdentifier ?? procedure,
                                procedure.StatementList,
                                procedure.Parameters.Count));
                        }
                        break;
                    case FunctionStatementBody function:
                        objectCount++;
                        if (function.StatementList is not null)
                        {
                            results.Add(Measure(
                                GetName(function.Name),
                                "Function",
                                (TSqlFragment?)function.Name?.BaseIdentifier ?? function,
                                function.StatementList,
                                function.Parameters.Count));
                        }
                        break;
                    case TriggerStatementBody trigger:
                        objectCount++;
                        if (trigger.StatementList is not null)
                        {
                            results.Add(Measure(
                                GetName(trigger.Name),
                                "Trigger",
                                (TSqlFragment?)trigger.Name?.BaseIdentifier ?? trigger,
                                trigger.StatementList,
                                0));
                        }
                        break;
                    case ViewStatementBody view:
                        results.Add(Measure(
                            GetName(view.SchemaObjectName),
                            "View",
                            (TSqlFragment?)view.SchemaObjectName?.BaseIdentifier ?? view,
                            view.SelectStatement,
                            0));
                        objectCount++;
                        break;
                }
            }

            if (objectCount == 0)
            {
                results.Add(Measure(
                    $"batch-{batchIndex + 1}",
                    "Batch",
                    batch,
                    batch,
                    0));
            }
        }
        return results.ToArray();
    }

    private static SqlObjectMetrics Measure(
        string name,
        string kind,
        TSqlFragment location,
        TSqlFragment body,
        int parameterCount)
    {
        var visitor = new MetricsVisitor();
        body.Accept(visitor);
        return new SqlObjectMetrics(
            name,
            kind,
            location,
            1 + visitor.DecisionCount,
            visitor.MaxNestingDepth,
            visitor.StatementCount,
            visitor.MaxJoinsPerQuery,
            parameterCount);
    }

    private static string GetName(SchemaObjectName? name) =>
        name is null ? "<anonymous>" : string.Join(".", name.Identifiers.Select(identifier => identifier.Value));

    private sealed class MetricsVisitor : TSqlFragmentVisitor
    {
        private int _nestingDepth;

        internal int DecisionCount { get; private set; }
        internal int MaxNestingDepth { get; private set; }
        internal int StatementCount { get; private set; }
        internal int MaxJoinsPerQuery { get; private set; }

        public override void Visit(TSqlFragment node)
        {
            if (node is TSqlStatement)
            {
                StatementCount++;
            }
            base.Visit(node);
        }

        public override void ExplicitVisit(IfStatement node)
        {
            DecisionCount++;
            VisitNested(node, () => base.ExplicitVisit(node));
        }

        public override void ExplicitVisit(WhileStatement node)
        {
            DecisionCount++;
            VisitNested(node, () => base.ExplicitVisit(node));
        }

        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            DecisionCount += node.WhenClauses.Count;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BeginEndBlockStatement node) =>
            VisitNested(node, () => base.ExplicitVisit(node));

        public override void ExplicitVisit(TryCatchStatement node) =>
            VisitNested(node, () => base.ExplicitVisit(node));

        public override void ExplicitVisit(QuerySpecification node)
        {
            var joins = node.FromClause?.TableReferences.Sum(CountJoins) ?? 0;
            MaxJoinsPerQuery = Math.Max(MaxJoinsPerQuery, joins);
            base.ExplicitVisit(node);
        }

        private void VisitNested(TSqlFragment _, Action visitChildren)
        {
            _nestingDepth++;
            MaxNestingDepth = Math.Max(MaxNestingDepth, _nestingDepth);
            visitChildren();
            _nestingDepth--;
        }

        private static int CountJoins(TableReference tableReference) => tableReference switch
        {
            JoinTableReference join =>
                1 + CountJoins(join.FirstTableReference) + CountJoins(join.SecondTableReference),
            JoinParenthesisTableReference { Join: not null } parenthesis => CountJoins(parenthesis.Join),
            _ => 0
        };
    }
}
