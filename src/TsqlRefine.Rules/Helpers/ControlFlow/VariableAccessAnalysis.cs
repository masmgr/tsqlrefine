using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

internal sealed record VariableDeclarationInfo(
    string Name,
    Identifier Identifier,
    bool IsInitiallyAssigned,
    bool IsParameter);

internal sealed record VariableAccessInfo(
    IReadOnlyList<VariableReference> Reads,
    IReadOnlySet<string> Writes);

internal static class VariableAccessAnalysis
{
    internal static IReadOnlyDictionary<string, VariableDeclarationInfo> GetDeclarations(ControlFlowScope scope)
    {
        var declarations = new Dictionary<string, VariableDeclarationInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in scope.Parameters)
        {
            if (parameter.VariableName is { } name)
            {
                declarations[name.Value] = new VariableDeclarationInfo(name.Value, name, true, true);
            }
        }

        foreach (var statement in scope.Graph.Nodes.Select(node => node.Statement).OfType<DeclareVariableStatement>())
        {
            foreach (var declaration in statement.Declarations)
            {
                if (declaration.VariableName is { } name)
                {
                    declarations[name.Value] = new VariableDeclarationInfo(
                        name.Value,
                        name,
                        declaration.Value is not null,
                        false);
                }
            }
        }
        return declarations;
    }

    internal static VariableAccessInfo GetAccesses(TSqlStatement statement)
    {
        var visitor = new AccessVisitor();
        statement.Accept(visitor);
        return new VariableAccessInfo(visitor.Reads, visitor.Writes);
    }

    private sealed class AccessVisitor : TSqlFragmentVisitor
    {
        private readonly HashSet<string> _writtenEarlier = new(StringComparer.OrdinalIgnoreCase);

        internal List<VariableReference> Reads { get; } = [];
        internal HashSet<string> Writes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(VariableReference node)
        {
            AddRead(node);
        }

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                declaration.Value?.Accept(this);
                if (declaration.Value is not null)
                {
                    AddWrite(declaration.VariableName?.Value);
                }
            }
        }

        public override void ExplicitVisit(SetVariableStatement node)
        {
            node.Expression?.Accept(this);
            if (node.AssignmentKind != AssignmentKind.Equals && node.Variable is not null)
            {
                AddRead(node.Variable);
            }
            AddWrite(node.Variable?.Name);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            node.Expression?.Accept(this);
            if (node.AssignmentKind != AssignmentKind.Equals && node.Variable is not null)
            {
                AddRead(node.Variable);
            }
            AddWrite(node.Variable?.Name);
        }

        public override void ExplicitVisit(FetchCursorStatement node)
        {
            foreach (var variable in node.IntoVariables)
            {
                AddWrite(variable.Name);
            }
        }

        public override void ExplicitVisit(ExecuteParameter node)
        {
            if (node.IsOutput && node.ParameterValue is VariableReference output)
            {
                AddWrite(output.Name);
                return;
            }
            node.ParameterValue?.Accept(this);
        }

        public override void ExplicitVisit(IfStatement node)
        {
            node.Predicate?.Accept(this);
        }

        public override void ExplicitVisit(WhileStatement node)
        {
            node.Predicate?.Accept(this);
        }

        private void AddRead(VariableReference variable)
        {
            if (!_writtenEarlier.Contains(variable.Name))
            {
                Reads.Add(variable);
            }
        }

        private void AddWrite(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                Writes.Add(name);
                _writtenEarlier.Add(name);
            }
        }
    }
}
