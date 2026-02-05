---
name: debug-ast
description: Analyze and visualize ScriptDom AST structure of SQL queries. Use when: developing new linting rules, understanding AST node types, debugging rule detection logic, or exploring T-SQL parsing behavior.
---

# Debug AST

Analyze ScriptDom AST for rule development.

## Workflow

1. Parse SQL using ScriptDom knowledge
2. Identify key AST nodes for the query
3. Show relevant properties for rule detection
4. Suggest visitor methods to override

## Common AST Nodes

### Statements
- `SelectStatement` → `QueryExpression` (usually `QuerySpecification`)
- `UpdateStatement` → `UpdateSpecification` with `WhereClause`
- `DeleteStatement` → `DeleteSpecification` with `WhereClause`
- `InsertStatement` → `InsertSpecification` with `InsertSource`

### Query Components
- `QuerySpecification` → `SelectElements`, `FromClause`, `WhereClause`
- `SelectStarExpression` - SELECT *
- `SelectScalarExpression` - SELECT column
- `FromClause` → `TableReferences`

### Table References
- `NamedTableReference` → `SchemaObject`, `Alias`, `TableHints`
- `QualifiedJoin` → `FirstTableReference`, `SecondTableReference`, `SearchCondition`

### Expressions
- `BooleanComparisonExpression` → `FirstExpression`, `SecondExpression`, `ComparisonType`
- `BooleanBinaryExpression` → AND/OR operations
- `BooleanIsNullExpression` → IS NULL / IS NOT NULL
- `NullLiteral` - NULL keyword

## Example Output

```
TSqlScript
└── TSqlBatch
    └── SelectStatement
        └── QueryExpression: QuerySpecification
            ├── SelectElements[0]: SelectStarExpression
            ├── FromClause
            │   └── TableReferences[0]: NamedTableReference
            │       └── SchemaObject: "users"
            └── WhereClause
                └── SearchCondition: BooleanComparisonExpression
```

## Rule Development Pattern

```csharp
public override void ExplicitVisit(SelectStarExpression node)
{
    AddDiagnostic(
        fragment: node,
        message: "Avoid SELECT *",
        code: "avoid-select-star",
        category: "Performance",
        fixable: false
    );
    base.ExplicitVisit(node);  // Continue traversal
}
```
