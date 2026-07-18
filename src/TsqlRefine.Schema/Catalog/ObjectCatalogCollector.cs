using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Catalog;

/// <summary>Collects SQL object definitions and references from ScriptDOM ASTs.</summary>
public static class ObjectCatalogCollector
{
    /// <summary>Collects an object catalog from SQL text and file path pairs.</summary>
    public static ObjectCatalog Collect(
        IEnumerable<(string Sql, string FilePath)> inputs,
        int compatLevel,
        string defaultSchema = "dbo",
        bool isAuthoritative = true)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSchema);

        var inputList = inputs.ToArray();
        var objects = new List<CatalogObject>();
        var references = new List<CatalogReference>();
        foreach (var (sql, filePath) in inputList)
        {
            var fragment = SqlParser.Parse(sql, compatLevel);
            if (fragment is null)
            {
                continue;
            }

            var visitor = new CollectorVisitor(filePath, defaultSchema, objects, references);
            fragment.Accept(visitor);
        }

        var resolvedReferences = ResolveReferences(objects, references);
        var databases = objects.Select(obj => obj.Id.DatabaseName)
            .Concat(references.Select(reference => reference.ToObject.DatabaseName))
            .Where(database => !string.IsNullOrWhiteSpace(database))
            .Select(database => database!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ObjectCatalog(
            ObjectCatalogSerializer.CurrentVersion,
            DateTimeOffset.UtcNow,
            compatLevel,
            new CatalogScope(
                databases,
                isAuthoritative,
                references.Any(r => r.Resolution == CatalogResolutionStatus.OutOfScope),
                defaultSchema),
            objects,
            resolvedReferences);
    }

    private static CatalogReference[] ResolveReferences(
        IReadOnlyList<CatalogObject> objects,
        List<CatalogReference> references)
    {
        var output = new CatalogReference[references.Count];
        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            if (reference.IsDynamic || reference.Resolution == CatalogResolutionStatus.OutOfScope)
            {
                output[i] = reference;
                continue;
            }

            var matches = objects.Count(obj => IdentityEquals(obj.Id, reference.ToObject) && KindMatches(obj.Kind, reference.Kind));
            var status = matches switch
            {
                0 when reference.Kind is CatalogReferenceKind.Table or CatalogReferenceKind.Column =>
                    CatalogResolutionStatus.OutOfScope,
                0 => CatalogResolutionStatus.Unresolved,
                1 => CatalogResolutionStatus.Resolved,
                _ => CatalogResolutionStatus.Ambiguous
            };
            output[i] = reference with { Resolution = status };
        }
        return output;
    }

    private static bool IdentityEquals(CatalogObjectId left, CatalogObjectId right) =>
        string.Equals(left.DatabaseName ?? string.Empty, right.DatabaseName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.SchemaName, right.SchemaName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

    private static bool KindMatches(CatalogObjectKind kind, CatalogReferenceKind referenceKind) => referenceKind switch
    {
        CatalogReferenceKind.Execute => kind == CatalogObjectKind.Procedure,
        CatalogReferenceKind.FunctionCall => kind is CatalogObjectKind.ScalarFunction or CatalogObjectKind.TableValuedFunction,
        CatalogReferenceKind.Table or CatalogReferenceKind.Column => kind == CatalogObjectKind.View,
        _ => false
    };

    private sealed class CollectorVisitor(
        string filePath,
        string defaultSchema,
        List<CatalogObject> objects,
        List<CatalogReference> references) : TSqlFragmentVisitor
    {
        private CatalogObjectId? _currentObject;

        public override void ExplicitVisit(CreateProcedureStatement node) => VisitProcedure(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(AlterProcedureStatement node) => VisitProcedure(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(CreateOrAlterProcedureStatement node) => VisitProcedure(node, () => base.ExplicitVisit(node));

        public override void ExplicitVisit(CreateFunctionStatement node) => VisitFunction(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(AlterFunctionStatement node) => VisitFunction(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(CreateOrAlterFunctionStatement node) => VisitFunction(node, () => base.ExplicitVisit(node));

        public override void ExplicitVisit(CreateViewStatement node) => VisitView(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(AlterViewStatement node) => VisitView(node, () => base.ExplicitVisit(node));
        public override void ExplicitVisit(CreateOrAlterViewStatement node) => VisitView(node, () => base.ExplicitVisit(node));

        public override void ExplicitVisit(ExecuteStatement node)
        {
            var executable = node.ExecuteSpecification?.ExecutableEntity;
            if (executable is ExecutableProcedureReference procedure)
            {
                var referenceName = procedure.ProcedureReference;
                if (referenceName?.ProcedureVariable is not null)
                {
                    references.Add(new CatalogReference(
                        _currentObject,
                        new CatalogObjectId(null, defaultSchema, referenceName.ProcedureVariable.Name),
                        null,
                        CatalogReferenceKind.Execute,
                        CatalogResolutionStatus.OutOfScope,
                        filePath,
                        GetRange(referenceName.ProcedureVariable),
                        true));
                }
                else if (referenceName?.ProcedureReference?.Name is { } name)
                {
                    AddReference(name, null, CatalogReferenceKind.Execute);
                }
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.CallTarget is MultiPartIdentifierCallTarget target &&
                node.FunctionName?.Value is { Length: > 0 } functionName)
            {
                AddFunctionReference(target.MultiPartIdentifier, functionName, node.FunctionName);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
        {
            if (node.SchemaObject is not null)
            {
                AddReference(node.SchemaObject, null, CatalogReferenceKind.FunctionCall);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            if (node.SchemaObject is not null)
            {
                AddReference(node.SchemaObject, null, CatalogReferenceKind.Table);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            var identifiers = node.MultiPartIdentifier?.Identifiers;
            if (identifiers is { Count: 3 })
            {
                var target = new CatalogObjectId(null, identifiers[0].Value, identifiers[1].Value);
                references.Add(CreateReference(target, identifiers[2].Value, CatalogReferenceKind.Column, node, false));
            }
            else if (identifiers is { Count: 4 })
            {
                var target = new CatalogObjectId(identifiers[0].Value, identifiers[1].Value, identifiers[2].Value);
                references.Add(CreateReference(target, identifiers[3].Value, CatalogReferenceKind.Column, node, false));
            }
            base.ExplicitVisit(node);
        }

        private void VisitProcedure(ProcedureStatementBody node, Action visitChildren)
        {
            var name = node.ProcedureReference?.Name;
            if (name is null)
            {
                visitChildren();
                return;
            }
            VisitDefinition(
                ToId(name),
                CatalogObjectKind.Procedure,
                node.Parameters.Select(CatalogTypeMapper.FromParameter).ToArray(),
                null,
                name,
                visitChildren);
        }

        private void VisitFunction(FunctionStatementBody node, Action visitChildren)
        {
            if (node.Name is null)
            {
                visitChildren();
                return;
            }
            var kind = node.ReturnType is ScalarFunctionReturnType
                ? CatalogObjectKind.ScalarFunction
                : CatalogObjectKind.TableValuedFunction;
            VisitDefinition(
                ToId(node.Name),
                kind,
                node.Parameters.Select(CatalogTypeMapper.FromParameter).ToArray(),
                GetFunctionResultColumns(node.ReturnType),
                node.Name,
                visitChildren);
        }

        private void VisitView(ViewStatementBody node, Action visitChildren)
        {
            if (node.SchemaObjectName is null)
            {
                visitChildren();
                return;
            }
            var resultColumns = node.Columns.Count == 0
                ? null
                : node.Columns.Select(identifier => new SchemaColumnInfo(
                    identifier.Value,
                    new SchemaTypeInfo("unknown", SchemaTypeCategory.Other),
                    true)).ToArray();
            VisitDefinition(
                ToId(node.SchemaObjectName),
                CatalogObjectKind.View,
                [],
                resultColumns,
                node.SchemaObjectName,
                visitChildren);
        }

        private void VisitDefinition(
            CatalogObjectId id,
            CatalogObjectKind kind,
            IReadOnlyList<CatalogParameter> parameters,
            IReadOnlyList<SchemaColumnInfo>? resultColumns,
            TSqlFragment nameFragment,
            Action visitChildren)
        {
            objects.Add(new CatalogObject(id, kind, parameters, resultColumns, filePath, GetRange(nameFragment)));
            var previous = _currentObject;
            _currentObject = id;
            visitChildren();
            _currentObject = previous;
        }

        private static SchemaColumnInfo[]? GetFunctionResultColumns(FunctionReturnType returnType)
        {
            if (returnType is not TableValuedFunctionReturnType tableReturn)
            {
                return null;
            }
            return tableReturn.DeclareTableVariableBody.Definition.ColumnDefinitions
                .Select(column =>
                {
                    var (_, type) = CatalogTypeMapper.FromDataType(column.DataType);
                    return new SchemaColumnInfo(column.ColumnIdentifier.Value, type, true);
                })
                .ToArray();
        }

        private void AddReference(SchemaObjectName name, string? column, CatalogReferenceKind kind)
        {
            var id = ToId(name);
            var external = name.ServerIdentifier is not null;
            references.Add(CreateReference(id, column, kind, name, external));
        }

        private void AddFunctionReference(
            MultiPartIdentifier qualifier,
            string functionName,
            TSqlFragment rangeFragment)
        {
            var identifiers = qualifier.Identifiers;
            CatalogObjectId id;
            var external = false;
            if (identifiers.Count == 1)
            {
                id = new CatalogObjectId(null, identifiers[0].Value, functionName);
            }
            else if (identifiers.Count == 2)
            {
                id = new CatalogObjectId(identifiers[0].Value, identifiers[1].Value, functionName);
            }
            else
            {
                id = new CatalogObjectId(
                    identifiers.Count > 1 ? identifiers[^2].Value : null,
                    identifiers.Count > 0 ? identifiers[^1].Value : defaultSchema,
                    functionName);
                external = true;
            }
            references.Add(CreateReference(id, null, CatalogReferenceKind.FunctionCall, rangeFragment, external));
        }

        private CatalogReference CreateReference(
            CatalogObjectId target,
            string? column,
            CatalogReferenceKind kind,
            TSqlFragment fragment,
            bool external) => new(
                _currentObject,
                target,
                column,
                kind,
                external ? CatalogResolutionStatus.OutOfScope : CatalogResolutionStatus.Unresolved,
                filePath,
                GetRange(fragment),
                false);

        private CatalogObjectId ToId(SchemaObjectName name) => new(
            name.DatabaseIdentifier?.Value,
            name.SchemaIdentifier?.Value ?? defaultSchema,
            name.BaseIdentifier?.Value ?? string.Empty);

        private static TsqlRefine.PluginSdk.Range GetRange(TSqlFragment fragment)
        {
            var startLine = Math.Max(0, fragment.StartLine - 1);
            var startColumn = Math.Max(0, fragment.StartColumn - 1);
            var endColumn = startColumn + Math.Max(1, fragment.FragmentLength);
            return new TsqlRefine.PluginSdk.Range(
                new Position(startLine, startColumn),
                new Position(startLine, endColumn));
        }
    }
}
