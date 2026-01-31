# Debug AST

Analyze and visualize the ScriptDom AST structure of SQL queries. Useful for developing new linting rules.

## Usage

```
/debug-ast <sql>

Options:
  --node <type>    Focus on specific node type
  --depth <n>      Maximum depth to display (default: 5)
```

Examples:
- `/debug-ast SELECT * FROM users WHERE id = 1`
- `/debug-ast UPDATE users SET name = 'test' WHERE id = 1`
- `/debug-ast --node BooleanComparisonExpression SELECT * FROM t WHERE x = NULL`

## Instructions

You are an AST debugging assistant for tsqlrefine rule development.

### Workflow

1. **Parse SQL**
   - Use the DebugTool project to parse and analyze SQL
   - Or analyze manually using ScriptDom knowledge

2. **Identify Key AST Nodes**
   - List the main statement type (SelectStatement, UpdateStatement, etc.)
   - Show relevant child nodes for rule development
   - Highlight properties useful for detection logic

3. **Provide Rule Development Guidance**
   - Suggest which visitor methods to override
   - Show relevant properties to check
   - Provide code snippets for detection

### Common AST Node Types

#### Statements
- `SelectStatement` → `QueryExpression` (usually `QuerySpecification`)
- `UpdateStatement` → `UpdateSpecification` with `WhereClause`
- `DeleteStatement` → `DeleteSpecification` with `WhereClause`
- `InsertStatement` → `InsertSpecification` with `InsertSource`

#### Query Components
- `QuerySpecification` → `SelectElements`, `FromClause`, `WhereClause`
- `SelectStarExpression` - SELECT *
- `SelectScalarExpression` - SELECT column
- `FromClause` → `TableReferences`

#### Table References
- `NamedTableReference` → `SchemaObject`, `Alias`, `TableHints`
- `QualifiedJoin` → `FirstTableReference`, `SecondTableReference`, `SearchCondition`
- `TableHint` → `HintKind` (NoLock, etc.)

#### Expressions
- `BooleanComparisonExpression` → `FirstExpression`, `SecondExpression`, `ComparisonType`
- `BooleanBinaryExpression` → `FirstExpression`, `SecondExpression`, `BinaryExpressionType`
- `BooleanIsNullExpression` → `Expression`, `IsNot`
- `NullLiteral` - NULL keyword
- `ColumnReferenceExpression` → `MultiPartIdentifier`

#### Literals
- `StringLiteral`, `IntegerLiteral`, `NumericLiteral`
- `NullLiteral`

### Output Format

```
## AST Analysis

**SQL**: `SELECT * FROM users WHERE id = 1`

### AST Structure
```
TSqlScript
└── TSqlBatch
    └── SelectStatement
        └── QueryExpression: QuerySpecification
            ├── SelectElements[0]: SelectStarExpression ⭐
            ├── FromClause
            │   └── TableReferences[0]: NamedTableReference
            │       └── SchemaObject: "users"
            └── WhereClause
                └── SearchCondition: BooleanComparisonExpression
                    ├── FirstExpression: ColumnReferenceExpression ("id")
                    ├── ComparisonType: Equals
                    └── SecondExpression: IntegerLiteral (1)
```

### For Rule Development

**To detect SELECT *:**
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
    base.ExplicitVisit(node);
}
```

**Key properties:**
- `SelectStarExpression` has no children (it's the * itself)
- `SelectStarExpression.Qualifier` - Table qualifier if present (e.g., `t.*`)
```

### Reference: Visitor Methods

For each AST node type, override `ExplicitVisit`:
```csharp
public override void ExplicitVisit(SelectStatement node) { ... }
public override void ExplicitVisit(UpdateStatement node) { ... }
public override void ExplicitVisit(BooleanComparisonExpression node) { ... }
```

Always call `base.ExplicitVisit(node)` to continue traversal.
