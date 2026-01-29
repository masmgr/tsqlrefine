# TsqlRefine ルールドキュメント レビュー結果レポート

## エグゼクティブサマリー

- **レビュー対象**: 84件（README除く85件中）
- **レビュー完了日**: 2026-01-30
- **レビュアー**: Claude Sonnet 4.5

### 統計サマリー

| 項目 | 件数 | 割合 |
|------|------|------|
| **総ルール数** | 84 | 100% |
| **Tier 1（Error, 高優先度）** | 11 | 13% |
| **Tier 2（Warning, 中優先度）** | 34 | 40% |
| **Tier 3（Information, 低優先度）** | 25 | 30% |
| **Good Quality（既存高品質）** | 14 | 17% |

### 品質評価サマリー

| 評価 | 件数 | 説明 |
|------|------|------|
| ✅ 高品質（改善不要） | 14 | 80行以上の詳細ドキュメント |
| ✅ 良好（軽微な改善） | 6 | 内容は良好、小さな改善で完璧 |
| ⚠️ 要改善（中程度） | 54 | Rationale拡充、例の追加が必要 |
| ❌ 緊急修正（重大問題） | 10 | 空の例、矛盾した内容、誤った情報 |

### Severity変更推奨

| ルールID | 現在 | 推奨 | 理由 |
|----------|------|------|------|
| semantic/set-variable | Error | Warning/Info | コーディングスタイルの問題、機能的影響なし |
| top-without-order-by | Error | Warning | 非決定的だがエラーではない、正常に実行される |
| order-by-in-subquery | Error | Warning | 無駄だがエラーではない、最適化で削除される可能性 |
| utc-datetime | Warning | Information | 正確性の推奨だが必須ではない、状況依存 |

---

## Tier 1: Error Severity（11件）

### 品質分布

| 評価 | 件数 |
|------|------|
| ✅ 高品質 | 2 |
| ✅ 良好 | 0 |
| ⚠️ 要改善 | 9 |
| ❌ 緊急修正 | 0 |

### レビュー詳細

#### 1. dml-without-where (safety)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート「prevents destructive or dangerous operations」
- ビジネス影響の深刻さ（全レコード削除/更新）が明記されていない
- 意図的な全件更新のケース（システムメンテナンス等）への言及なし

**推奨改善**:
```markdown
## Rationale

UPDATE/DELETE without WHERE clause affects ALL rows in the table, which can cause:

- **Catastrophic data loss**: Entire tables can be wiped in milliseconds
- **Business impact**: Loss of customer data, financial records, audit trails
- **Recovery costs**: Restore from backup may lose hours/days of work
- **Compliance violations**: May breach GDPR, HIPAA, SOX regulations

Common mistakes:
- Forgot WHERE clause during development
- Copy-pasted incomplete query
- Wrong connection/database context

If you truly need to modify all rows:
1. Add explicit WHERE 1=1 to signal intent
2. Use TRUNCATE TABLE for deletions (faster, logged differently)
3. Document the reason in comments
```

**優先度**: 🔴 高（Error Severityルール）

---

#### 2. named-constraint (correctness)

**現状**: 57行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート「prevents incorrect results or runtime errors」
- 問題の本質（tempdb内での名前衝突）が不明
- なぜ名前衝突が発生するか（同一セッションでの再作成）の説明なし

**推奨改善**:
```markdown
## Rationale

Named constraints in temporary tables cause conflicts because:

1. **Constraint names are global in tempdb**: Even though #TempUsers is session-specific, constraint names like `PK_TempUsers` are shared across all sessions in tempdb
2. **Concurrent execution fails**: If two sessions run the same script simultaneously, the second one fails with "constraint name already exists"
3. **Stored procedure re-execution**: Calling a procedure twice in the same session can fail if it drops and recreates temp tables

**Error example**:
```
Msg 2714, Level 16, State 6
There is already an object named 'PK_TempUsers' in the database.
```

**Solutions**:
- Use unnamed constraints (recommended for temp tables)
- Use unique constraint names with @@SPID or NEWID() (complex, avoid)
- Use table variables instead of temp tables (different scope)

**When is this rule too strict?**
- Single-use temp tables in ad-hoc scripts
- Dev/test environments with controlled execution
```

**優先度**: 🟡 高（Error Severityルール、頻出パターン）

---

#### 3. semantic/cte-name-conflict (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート
- CTE名前衝突の具体的エラーメッセージなし
- ネストCTEと複数CTEの違いが不明

**推奨改善**:
```markdown
## Rationale

CTE names must be unique within a single WITH clause. Duplicate names cause:

**Compile-time error**:
```
Msg 462, Level 16, State 1
Duplicate common table expression name 'UserCTE' was specified.
```

Common scenarios:
1. **Copy-paste errors**: Duplicating CTE definitions when building complex queries
2. **Nested CTEs**: Accidentally reusing outer CTE name in inner scope
3. **Refactoring mistakes**: Merging multiple queries without renaming CTEs

**Valid**: Multiple CTEs with different names
```sql
WITH FirstCTE AS (...),
     SecondCTE AS (...)
SELECT * FROM FirstCTE JOIN SecondCTE;
```

**Invalid**: Same CTE name twice
```sql
WITH UserCTE AS (...),
     UserCTE AS (...)  -- Error!
SELECT * FROM UserCTE;
```
```

**優先度**: 🟡 高（Error Severityルール）

---

#### 4. semantic/data-type-length (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- デフォルト長（VARCHAR→1バイト）の説明なし
- なぜ1バイトが問題か（トランケーション）の説明不足
- CHAR/VARCHAR/NVARCHAR全ての挙動の違いが不明

**推奨改善**:
```markdown
## Rationale

Omitting length for character types causes silent data truncation:

| Type | Default Length | Impact |
|------|----------------|---------|
| VARCHAR | 1 byte | Truncates to first character |
| NVARCHAR | 1 character | Truncates to first character |
| CHAR | 1 byte | Pads/truncates to 1 character |

**Danger**: No error or warning when data is truncated!

**Bad example**:
```sql
DECLARE @Name VARCHAR;  -- Defaults to VARCHAR(1)
SET @Name = 'Alice';    -- Silently truncates to 'A'
SELECT @Name;           -- Returns 'A', data loss!
```

**Good example**:
```sql
DECLARE @Name VARCHAR(100);  -- Explicit length
SET @Name = 'Alice';         -- Stores full value
SELECT @Name;                -- Returns 'Alice'
```

**Modern best practice**:
- VARCHAR(MAX) for unpredictable lengths (use sparingly, impacts performance)
- VARCHAR(50), VARCHAR(100), VARCHAR(500) for typical text fields
- NVARCHAR(n) for Unicode support (emails, names, international text)

**Note**: This rule applies to:
- Variable declarations (`DECLARE @x VARCHAR`)
- Table columns (`CREATE TABLE t (c VARCHAR)`)
- Function parameters (`@Param VARCHAR`)
```

**優先度**: 🔴 高（Error Severity、データ損失リスク）

---

#### 5. semantic/duplicate-alias (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート
- どのスコープで重複が問題か（SELECT句内、サブクエリ、CTE）が不明
- SQL Serverの挙動（エラーになるか、警告か）が不明

**推奨改善**:
```markdown
## Rationale

Duplicate column aliases in SELECT clause cause ambiguity and errors:

**Compile-time error** (in some contexts):
```
Msg 8155, Level 16, State 2
Ambiguous column name 'UserName'.
```

**Common scenarios**:
1. **JOIN with same column names**: Selecting columns from both tables without unique aliases
2. **Copy-paste errors**: Duplicating column expressions with same alias
3. **Computed columns**: Multiple calculations aliased with generic names like 'Total'

**Example - Ambiguous reference**:
```sql
-- Bad: Duplicate alias 'Name'
SELECT
    u.FirstName AS Name,
    u.LastName AS Name,  -- Duplicate!
    o.OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId;

-- Error when referencing: Which Name?
```

**Example - Implicit column resolution**:
```sql
-- Bad: Both tables have 'Id' column
SELECT Id, Id  -- Which Id? Ambiguous
FROM Users u
JOIN Orders o ON u.Id = o.UserId;

-- Good: Explicit aliases
SELECT u.Id AS UserId, o.Id AS OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId;
```

**Best practices**:
- Use descriptive, unique aliases
- Prefix with table/entity name for clarity (UserId, OrderId)
- Avoid generic names (Id, Name, Value)
```

**優先度**: 🟡 高（Error Severity）

---

#### 6. semantic/insert-column-count-mismatch (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート
- エラーメッセージの具体例なし
- VALUES句とSELECT句の両方のケースが不明

**推奨改善**:
```markdown
## Rationale

Column count mismatch between INSERT target and source causes runtime errors:

**Runtime error**:
```
Msg 213, Level 16, State 1
Column name or number of supplied values does not match table definition.
```

**Common causes**:
1. **Schema changes**: Table altered after INSERT statement was written
2. **Copy-paste errors**: VALUES list incomplete or has extra values
3. **Dynamic SQL**: Incorrectly generated INSERT statements
4. **Missing columns**: Forgot required columns (even if they have defaults)

**Example - VALUES mismatch**:
```sql
-- Table has 3 columns
CREATE TABLE Users (Id INT, Name VARCHAR(50), Email VARCHAR(100));

-- Bad: Only 2 values
INSERT INTO Users (Id, Name, Email)
VALUES (1, 'Alice');  -- Error: Expected 3 values, got 2

-- Good: All 3 values
INSERT INTO Users (Id, Name, Email)
VALUES (1, 'Alice', 'alice@example.com');
```

**Example - INSERT SELECT mismatch**:
```sql
-- Bad: SELECT returns 2 columns, INSERT expects 3
INSERT INTO Users (Id, Name, Email)
SELECT UserId, UserName FROM OtherTable;  -- Error!

-- Good: SELECT returns 3 columns
INSERT INTO Users (Id, Name, Email)
SELECT UserId, UserName, UserEmail FROM OtherTable;
```

**Prevention**:
- Always specify column list in INSERT
- Match SELECT column count exactly
- Use tools/tests to validate INSERT statements after schema changes
```

**優先度**: 🟡 高（Error Severity、頻出エラー）

---

#### 7. semantic/undefined-alias (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート
- どのクエリ句で未定義エイリアスが問題か（WHERE、HAVING、SELECT）が不明
- エイリアスのスコープルール（SQLの評価順序）の説明なし

**推奨改善**:
```markdown
## Rationale

Referencing undefined table/column aliases causes compile-time errors:

**Compile error examples**:
```
Msg 4104, Level 16, State 1
The multi-part identifier "x.Name" could not be bound.

Msg 207, Level 16, State 1
Invalid column name 'UserCount'.
```

**Common scenarios**:

1. **Typo in alias reference**:
```sql
-- Bad: Typo in alias
SELECT u.Name
FROM Users AS usr  -- Alias is 'usr'
WHERE u.Id = 1;    -- References 'u' (undefined!)

-- Good: Correct alias
SELECT usr.Name
FROM Users AS usr
WHERE usr.Id = 1;
```

2. **Column alias in WHERE clause** (logical error):
```sql
-- Bad: Column aliases not available in WHERE
SELECT FirstName + ' ' + LastName AS FullName
FROM Users
WHERE FullName LIKE '%Smith%';  -- Error: FullName undefined

-- Good: Repeat expression or use subquery
SELECT FullName
FROM (
    SELECT FirstName + ' ' + LastName AS FullName
    FROM Users
) AS Derived
WHERE FullName LIKE '%Smith%';
```

3. **JOIN with missing alias**:
```sql
-- Bad: No alias for Orders table
SELECT o.OrderId
FROM Users u
JOIN Orders ON u.Id = Orders.UserId  -- No alias 'o'
WHERE o.Total > 100;                  -- Error: 'o' undefined

-- Good: Define alias
SELECT o.OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE o.Total > 100;
```

**SQL evaluation order** (why WHERE can't see column aliases):
1. FROM
2. JOIN
3. WHERE
4. GROUP BY
5. HAVING
6. SELECT (aliases defined here)
7. ORDER BY
```

**優先度**: 🟡 高（Error Severity、頻出エラー）

---

#### 8. semantic/set-variable (correctness)

**現状**: 55行、Error
**評価**: ⚠️ 要改善 + ❌ Severity過剰

**問題点**:
- **Severity過剰**: これはスタイル/一貫性の問題であり、Error（データ損失・実行時エラー）ではない
- SET/SELECTの挙動の違い（複数行代入時）が不明確
- 「Note: SET and SELECT do not behave identically」とあるが詳細なし

**推奨改善**:
```markdown
## Severity Recommendation

**Current**: Error
**Recommended**: Warning or Information

**Reason**: This is a style/consistency issue, not a correctness error. Both SET and SELECT are valid T-SQL syntax with different trade-offs.

## Rationale

SET and SELECT have different behaviors for variable assignment:

| Aspect | SET | SELECT |
|--------|-----|--------|
| Multiple variables | One at a time | Multiple at once |
| Query returns 0 rows | Variable unchanged | Variable set to NULL |
| Query returns 2+ rows | Error | Uses last row (unpredictable) |
| Clarity | Explicit assignment | Can mix with query logic |

**Behavior difference - Multi-row query**:
```sql
-- SET: Error if query returns multiple rows
DECLARE @Name VARCHAR(50);
SET @Name = (SELECT Name FROM Users);  -- Error if >1 row

-- SELECT: Uses last row (non-deterministic!)
DECLARE @Name VARCHAR(50);
SELECT @Name = Name FROM Users;  -- No error, uses last row
```

**When to use SET**:
- Simple scalar assignments: `SET @Counter = 0`
- Calculations: `SET @Total = @Price * @Quantity`
- When you want errors on multi-row results

**When to use SELECT**:
- Assigning multiple variables: `SELECT @Id = Id, @Name = Name FROM Users WHERE ...`
- Assigning from queries with TOP 1: `SELECT @Name = Name FROM Users ORDER BY Id DESC`
- Consistency in codebases that prefer SELECT style

**This rule's purpose**: Enforce consistent style across codebase, not prevent errors.

## Examples

### Bad (according to rule)

```sql
DECLARE @Count INT;
SET @Count = 10;  -- Uses SET
```

### Good (rule prefers SELECT)

```sql
DECLARE @Count INT;
SELECT @Count = COUNT(*) FROM Users;  -- Uses SELECT
```

**Note**: Both examples are functionally correct. This is a style preference.
```

**優先度**: 🔴 最高（Severity変更が必要）

---

#### 9. semantic/unicode-string (correctness)

**現状**: 53行、Error
**評価**: ⚠️ 要改善

**問題点**:
- Rationaleが汎用テンプレート
- コードページ依存によるデータ損失の説明なし
- NVARCHAR列へのVARCHAR文字列代入の暗黙変換問題が不明
- Unicode文字（日本語、絵文字等）の具体例なし

**推奨改善**:
```markdown
## Rationale

Comparing NVARCHAR columns with non-Unicode string literals causes:

1. **Implicit conversion**: String literal converted to NVARCHAR, can cause index scan instead of seek
2. **Data corruption risk**: Non-ASCII characters may be lost or corrupted
3. **Code page dependency**: Behavior varies based on server collation

**Performance impact**:
```sql
-- Bad: Implicit conversion may prevent index usage
SELECT * FROM Users
WHERE Name = 'Alice';  -- 'Alice' is VARCHAR, Name is NVARCHAR

-- Good: Explicit Unicode literal
SELECT * FROM Users
WHERE Name = N'Alice';  -- N'Alice' is NVARCHAR, no conversion
```

**Data corruption example**:
```sql
-- Table with NVARCHAR column
CREATE TABLE Users (Name NVARCHAR(50));

-- Bad: Non-Unicode literal with Japanese characters
INSERT INTO Users (Name)
VALUES ('田中太郎');  -- May corrupt to '????' depending on code page

-- Good: Unicode literal
INSERT INTO Users (Name)
VALUES (N'田中太郎');  -- Correctly stores Unicode
```

**When to use N prefix**:
- Comparing with NVARCHAR/NCHAR columns
- String contains non-ASCII characters (Japanese, Chinese, emoji, etc.)
- International applications
- Consistent behavior across different server collations

**Compatibility**:
- SQL Server 2008+ (all compat levels 100-160)
- No performance overhead for N prefix itself
```

**優先度**: 🔴 高（Error Severity、国際化対応必須）

---

#### 10. top-without-order-by (performance)

**現状**: 53行、Error
**評価**: ⚠️ 要改善 + ❌ Severity過剰

**問題点**:
- **Severity過剰**: 非決定的だがエラーではない、クエリは正常に実行される
- 非決定性の具体例（複数実行で異なる結果）が不明
- クラスタ化インデックスの影響（物理順序）の説明なし

**推奨改善**:
```markdown
## Severity Recommendation

**Current**: Error
**Recommended**: Warning

**Reason**: Query executes successfully, but results are non-deterministic. This is a code quality warning, not a runtime error.

## Rationale

TOP without ORDER BY returns unpredictable rows:

**Problem**: SQL Server chooses rows based on physical storage order, which can change due to:
- Index reorganization/rebuild
- Page splits
- Data modifications
- Parallel execution plans

**Example - Non-deterministic results**:
```sql
-- Bad: Which 10 users? Changes between executions!
SELECT TOP 10 * FROM Users;  -- May return different users each time

-- Execution 1: Returns users with Id 1, 5, 7, 12, 15, 20, 23, 28, 31, 40
-- Execution 2: Returns users with Id 2, 6, 8, 13, 16, 21, 24, 29, 32, 41 (different!)
```

**Clustered index impact**:
```sql
-- Even with clustered index, no guarantee without ORDER BY
CREATE CLUSTERED INDEX IX_Users_Id ON Users(Id);
SELECT TOP 10 * FROM Users;  -- Still non-deterministic in spec
-- (May follow index order, but not guaranteed by SQL standard)
```

**Good - Deterministic**:
```sql
SELECT TOP 10 * FROM Users ORDER BY Id;  -- Always same 10 users
SELECT TOP 10 * FROM Users ORDER BY CreatedDate DESC;  -- Newest 10 users
```

**When is TOP without ORDER BY acceptable?**
- Ad-hoc data inspection: `SELECT TOP 10 * FROM BigTable` (just need any sample)
- Performance testing: Getting sample rows for testing
- COUNT verification: `SELECT TOP 1 1 FROM Table WHERE ...` (existence check)

**Best practice**: Always use ORDER BY with TOP for production code.
```

**優先度**: 🔴 最高（Severity変更が必要）

---

#### 11. order-by-in-subquery (correctness)

**現状**: 79行、Error
**評価**: ✅ 良好 + ❌ Severity過剰

**問題点**:
- **Severity過剰**: 無駄だがエラーではない、クエリは実行される
- ただしドキュメント自体は良好（79行、詳細な説明あり）

**推奨改善**:
```markdown
## Severity Recommendation

**Current**: Error
**Recommended**: Warning

**Reason**:
- Subquery ORDER BY is ignored by optimizer (wasteful but not erroneous)
- Query executes successfully
- No data corruption or runtime errors
- This is an optimization/code quality issue, not correctness error
```

**優先度**: 🟡 中（Severity変更推奨、ドキュメント自体は良好）

---

### Tier 1 まとめ

**緊急対応が必要な項目**:
1. ❌ **semantic/set-variable**: Severity を Error → Warning/Information に変更
2. ❌ **top-without-order-by**: Severity を Error → Warning に変更
3. ❌ **order-by-in-subquery**: Severity を Error → Warning に変更

**高優先度改善項目**:
- すべて11件でRationaleの拡充が必要（汎用テンプレートからの脱却）
- 具体的エラーメッセージ、ビジネス影響、データ損失リスクの明記
- エッジケース・例外状況の文書化

---

## Tier 2: Warning Severity（34件）

### 品質分布

| 評価 | 件数 |
|------|------|
| ✅ 高品質 | 6 |
| ✅ 良好 | 0 |
| ⚠️ 要改善 | 24 |
| ❌ 緊急修正 | 4 |

### 緊急修正が必要なルール（4件）

#### 1. cross-database-transaction (safety)

**現状**: 53行、Warning
**評価**: ❌ 緊急修正（Bad/Good例が同一内容）

**問題点**:
- **Bad例とGood例が完全に同一**: `SELECT * FROM DB1.dbo.Table1`
- Rationaleが汎用テンプレート
- 問題の本質（分散トランザクション、ロック、デッドロック）が不明

**修正内容**:
```markdown
## Rationale

Cross-database transactions introduce significant risks:

1. **Distributed transaction escalation**: May escalate to MS DTC, causing performance degradation
2. **Deadlock complexity**: Cross-database locks harder to diagnose and prevent
3. **Recovery challenges**: Restore operations become complex with cross-DB dependencies
4. **Availability**: One database offline affects all dependent databases

**When does this happen?**
- Explicit BEGIN TRANSACTION with operations on multiple databases
- Triggers that modify other databases
- Linked server queries within transactions

## Examples

### Bad

```sql
-- Cross-database transaction
BEGIN TRANSACTION;
    UPDATE DB1.dbo.Customers SET Status = 'Active';
    UPDATE DB2.dbo.Orders SET Processed = 1;  -- Different database!
COMMIT;

-- Trigger causing cross-database transaction
CREATE TRIGGER trg_UpdateLog ON DB1.dbo.Customers
AFTER UPDATE AS
BEGIN
    INSERT INTO DB2.dbo.AuditLog (Message)  -- Cross-database!
    VALUES ('Customer updated');
END;
```

### Good

```sql
-- Single database transaction
BEGIN TRANSACTION;
    UPDATE Customers SET Status = 'Active';
    UPDATE Orders SET Processed = 1;  -- Same database
COMMIT;

-- Alternative: Use message queue for cross-database operations
BEGIN TRANSACTION;
    UPDATE Customers SET Status = 'Active';
    INSERT INTO OutboxQueue (TargetDB, Operation, Payload)
    VALUES ('DB2', 'UpdateOrders', '{"Processed": 1}');
COMMIT;
-- Separate process handles OutboxQueue
```

**Alternatives**:
- Consolidate related tables into single database
- Use Service Broker for asynchronous cross-database operations
- Application-level coordination instead of DB transactions
```

**優先度**: 🔴 緊急（例が無意味）

---

#### 2. set-nocount (transactions)

**現状**: 53行、Warning
**評価**: ❌ 緊急修正（両方の例が空）

**問題点**:
- Bad例: `-- Example showing rule violation`（空）
- Good例: `-- Example showing compliant code`（空）
- Rationaleが汎用テンプレート
- なぜNOCOUNT ONが必要か（ネットワーク帯域、パフォーマンス）が不明

**修正内容**:
```markdown
## Rationale

SET NOCOUNT ON prevents sending row count messages to client:

**Benefits**:
1. **Reduced network traffic**: Eliminates "n rows affected" messages
2. **Slight performance improvement**: Less overhead in stored procedures
3. **Cleaner output**: Application logs/traces less cluttered
4. **Best practice**: Microsoft recommendation for stored procedures

**Impact of omitting**:
- Each INSERT/UPDATE/DELETE sends separate row count message
- Stored procedures with 50+ statements generate 50+ messages
- Negligible in single queries, noticeable in high-volume procedures

## Examples

### Bad

```sql
-- No SET NOCOUNT ON at the beginning
CREATE PROCEDURE uspGetActiveUsers
AS
BEGIN
    -- Missing SET NOCOUNT ON
    SELECT * FROM Users WHERE Active = 1;
    UPDATE Users SET LastAccessed = GETDATE();  -- Sends "(5 rows affected)"
END;
```

### Good

```sql
CREATE PROCEDURE uspGetActiveUsers
AS
BEGIN
    SET NOCOUNT ON;  -- First statement in procedure

    SELECT * FROM Users WHERE Active = 1;
    UPDATE Users SET LastAccessed = GETDATE();  -- No row count message
END;
```

**Where to use**:
- First statement in stored procedures
- First statement in triggers
- Not necessary in ad-hoc queries
```

**優先度**: 🔴 緊急（例が空）

---

#### 3. set-quoted-identifier (transactions)

**現状**: 53行、Warning
**評価**: ❌ 緊急修正（両方の例が空）

**問題点**:
- Bad例: `-- Example showing rule violation`（空）
- Good例: `-- Example showing compliant code`（空）
- Rationaleが汎用テンプレート
- QUOTED_IDENTIFIERの影響（インデックス付きビュー、計算列）が不明

**修正内容**:
```markdown
## Rationale

SET QUOTED_IDENTIFIER ON is required for:

1. **Indexed views**: Cannot create indexed views with QUOTED_IDENTIFIER OFF
2. **Computed columns**: Persisted computed columns require QUOTED_IDENTIFIER ON
3. **Filtered indexes**: Must be created with QUOTED_IDENTIFIER ON
4. **Standard SQL compliance**: ON is SQL standard behavior

**Behavior difference**:

| QUOTED_IDENTIFIER | Double quotes (") | Behavior |
|-------------------|-------------------|----------|
| ON (default) | Identifier delimiter | "Order" is column/table name |
| OFF | String delimiter | "Order" is string literal |

**Errors when OFF**:
```
Msg 1935, Level 16, State 1
Cannot create index on view 'dbo.vw_Sales' because the view was created with QUOTED_IDENTIFIER OFF.
```

## Examples

### Bad

```sql
-- File without SET QUOTED_IDENTIFIER ON
CREATE VIEW vw_ActiveUsers AS
SELECT "Order", "User" FROM Users WHERE Active = 1;
-- Ambiguous: Are these column names or string literals?

-- Creating indexed view will fail
CREATE UNIQUE CLUSTERED INDEX IX_ActiveUsers ON vw_ActiveUsers("Order");
-- Error if QUOTED_IDENTIFIER was OFF when view created
```

### Good

```sql
SET QUOTED_IDENTIFIER ON;  -- At file start

CREATE VIEW vw_ActiveUsers AS
SELECT [Order], [User] FROM Users WHERE Active = 1;  -- Unambiguous

-- Indexed view creation succeeds
CREATE UNIQUE CLUSTERED INDEX IX_ActiveUsers ON vw_ActiveUsers([Order]);
```

**Best practice**: Always use SET QUOTED_IDENTIFIER ON at the beginning of SQL files, especially for:
- Views
- Stored procedures
- Functions
- DDL scripts
```

**優先度**: 🔴 緊急（例が空）

---

#### 4. set-transaction-isolation-level (transactions)

**現状**: 53行、Warning
**評価**: ❌ 緊急修正（両方の例が空）

**問題点**:
- Bad例: `-- Example showing rule violation`（空）
- Good例: `-- Example showing compliant code`（空）
- Rationaleが汎用テンプレート
- 各分離レベルの影響（ロック、ファントムリード、パフォーマンス）が不明

**修正内容**:
```markdown
## Rationale

Explicit transaction isolation level setting ensures predictable behavior:

**Why explicit is better**:
1. **Default varies**: Server default can be changed, causing unexpected behavior
2. **Clarity**: Readers understand concurrency requirements
3. **Prevents bugs**: Implicit READ COMMITTED may cause issues in high-concurrency scenarios

**Isolation levels** (performance vs. consistency trade-off):

| Level | Dirty Read | Non-Repeatable | Phantom | Performance |
|-------|------------|----------------|---------|-------------|
| READ UNCOMMITTED | Yes | Yes | Yes | Fastest (no locks) |
| READ COMMITTED | No | Yes | Yes | Good (default) |
| REPEATABLE READ | No | No | Yes | Slower (more locks) |
| SERIALIZABLE | No | No | No | Slowest (range locks) |
| SNAPSHOT | No | No | No | Good (row versioning) |

## Examples

### Bad

```sql
-- No explicit isolation level
BEGIN TRANSACTION;
    SELECT COUNT(*) FROM Orders;  -- Uses server default (uncertain)
    INSERT INTO OrderLog VALUES ('Processed');
COMMIT;
```

### Good

```sql
-- Explicit isolation level
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRANSACTION;
    SELECT COUNT(*) FROM Orders;
    INSERT INTO OrderLog VALUES ('Processed');
COMMIT;

-- Or SNAPSHOT for high-concurrency reads
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRANSACTION;
    SELECT * FROM Orders WHERE Status = 'Pending';  -- No locks, consistent view
COMMIT;
```

**When to use each level**:
- **READ UNCOMMITTED**: Reporting queries where dirty reads acceptable
- **READ COMMITTED**: Default for most OLTP operations
- **REPEATABLE READ**: Financial transactions, audit operations
- **SERIALIZABLE**: Critical operations requiring absolute consistency
- **SNAPSHOT**: High-concurrency reads without blocking writers
```

**優先度**: 🔴 緊急（例が空）

---

### 高優先度改善項目（10件）

#### 5. require-xact-abort-on (transactions)

**現状**: 53行、Warning
**評価**: ⚠️ 要改善 + ❌ Good例が空、Rationale誤り

**問題点**:
- Good例が空: `-- Example showing compliant code`
- Rationaleが完全に誤り: 「maintains code formatting and consistency」（トランザクション安全性の問題）
- XACT_ABORTの役割（部分コミット防止）の説明なし

**推奨改善**:
```markdown
## Rationale

SET XACT_ABORT ON ensures runtime errors automatically roll back entire transaction:

**Without XACT_ABORT ON** (dangerous):
- Some errors continue transaction (partial commit risk)
- Explicit error checking needed after every statement
- Easy to miss errors and commit partial work

**With XACT_ABORT ON** (safe):
- Any runtime error aborts and rolls back transaction
- Connection also terminates on error
- Guaranteed all-or-nothing behavior

**Example of danger**:
```sql
-- Bad: XACT_ABORT OFF (default)
BEGIN TRANSACTION;
    INSERT INTO Orders (Id, Total) VALUES (1, 100);  -- Succeeds
    INSERT INTO Orders (Id, Total) VALUES (1, 200);  -- Error: Duplicate key
    -- Transaction STILL OPEN! First insert committed if no error check
COMMIT;  -- Commits first insert (partial commit!)
```

## Examples

### Bad

```sql
BEGIN TRANSACTION;
    UPDATE Users SET Active = 1;
    -- If error occurs, transaction may be left open
COMMIT;
```

### Good

```sql
SET XACT_ABORT ON;  -- Must be before BEGIN TRANSACTION

BEGIN TRANSACTION;
    UPDATE Users SET Active = 1;
    UPDATE Orders SET Processed = 1;
    -- Any error automatically rolls back entire transaction
COMMIT;
```

**Best practice**: Always use SET XACT_ABORT ON with explicit transactions in stored procedures.
```

**優先度**: 🟡 高（トランザクション安全性）

---

#### 6. require-qualified-columns-everywhere (style)

**現状**: 53行、Warning
**評価**: ⚠️ 要改善 + ❌ Bad/Good例が矛盾

**問題点**:
- Bad例は複数テーブルなのに、Good例は単一テーブル（ルールと矛盾）
- ルールは「when multiple tables are referenced」と言っているが例が合わない
- Rationaleが汎用テンプレート

**修正内容**:
```markdown
## Rationale

Qualifying columns in multi-table queries prevents:

1. **Ambiguity**: Which table does column belong to?
2. **Future errors**: Schema changes may add same column name to another table
3. **Readability**: Explicit qualification makes query logic clear
4. **Maintenance**: Easier to refactor queries

**Stricter than `qualified-select-columns`**: This rule requires qualification in WHERE/JOIN/ORDER BY, not just SELECT.

## Examples

### Bad

```sql
-- Multi-table query without qualification
SELECT u.Name, OrderId  -- OrderId not qualified
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE Active = 1  -- Active not qualified (which table?)
ORDER BY CreatedDate;  -- CreatedDate not qualified

-- Potential error if both tables have Active column
-- Ambiguous which CreatedDate to use
```

### Good

```sql
-- All columns qualified
SELECT u.Name, o.OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE u.Active = 1  -- Clearly Users.Active
ORDER BY o.CreatedDate;  -- Clearly Orders.CreatedDate

-- Even with single table in FROM (but multi-table overall)
SELECT u.Name, o.OrderId
FROM Users u
JOIN Orders o ON u.Id = o.UserId
WHERE u.Active = 1 AND o.Status = 'Pending'  -- All qualified
```

**Single table queries**: No qualification needed
```sql
SELECT Name, Email FROM Users WHERE Active = 1;  -- OK, single table
```
```

**優先度**: 🟡 高（例の矛盾）

---

#### 7. semantic/multi-table-alias (style)

**現状**: 53行、Warning
**評価**: ⚠️ 要改善 + ❌ Good例が不適切

**問題点**:
- Bad例は複数テーブル、Good例は単一テーブル（ルールと矛盾）
- ルールは「multi-table queries (with JOINs)」と言っているのにGood例にJOINなし

**修正内容**:
```markdown
## Examples

### Bad

```sql
-- Column not qualified in multi-table query
SELECT Id, Name  -- Which Id? Users.Id or Orders.Id?
FROM Users u
JOIN Orders o ON u.Id = o.UserId;
```

### Good

```sql
-- All columns qualified with table alias
SELECT u.Id, u.Name, o.OrderId, o.Total
FROM Users u
JOIN Orders o ON u.Id = o.UserId;
```

**Single table**: No alias needed
```sql
SELECT Id, Name FROM Users;  -- OK, single table
```
```

**優先度**: 🟡 高（例の矛盾）

---

#### 8-17. その他のRationale拡充が必要なルール（10件）

以下のルールはすべて汎用テンプレートRationaleを使用しており、具体的な説明が必要:

8. **avoid-select-star** (performance, 53行): 具体的パフォーマンス問題（余分な列の転送、インデックス効率）の説明なし
9. **disallow-cursors** (performance, 53行): カーソルとセットベース操作の定量的比較なし
10. **escape-keyword-identifier** (correctness, 53行): 予約語エスケープの具体的理由・リスクなし
11. **require-column-list-for-insert-select** (correctness, 53行): スキーマ変更の具体例なし
12. **require-column-list-for-insert-values** (correctness, 53行): 同上
13. **require-parentheses-for-mixed-and-or** (correctness, 53行): AND/OR優先順位の誤解釈例なし
14. **avoid-ambiguous-datetime-literal** (correctness, 72行): リージョン依存の日付解釈例なし
15. **avoid-atat-identity** (correctness, 78行): @@IDENTITYとSCOPE_IDENTITY()の違いが不明確
16. **semantic/join-condition-always-true** (correctness, 53行): なぜ`ON 1=1`が問題か（カルテシアン積）の説明不足
17. **semantic/left-join-filtered-by-where** (correctness, 53行): LEFT JOINがINNER JOINになる理由の説明不足

**優先度**: 🟠 中（Rationale拡充）

---

### 良好なドキュメント（6件）

以下のルールは既に高品質のため、改善の優先度は低い:

1. **avoid-exec-dynamic-sql** (security, 92行): SQLインジェクション、詳細な例
2. **avoid-merge** (safety, 163行): MERGEのバグ、代替案の詳細
3. **avoid-nolock** (correctness, 155行): NOLOCKの危険性、dirty read例
4. **avoid-null-comparison** (correctness, 160行): NULL比較の罠、ANSI_NULLS影響
5. **avoid-implicit-conversion-in-predicate** (performance, 94行): 暗黙変換のインデックス影響
6. **non-sargable** (performance, 99行): SARGable述語の詳細

---

### 中優先度改善項目（14件）

以下のルールは内容は悪くないが、Rationaleの拡充や例の追加で改善可能:

18. **avoid-top-in-dml** (performance): TOP in UPDATE/DELETEの非決定性
19. **forbid-top-100-percent-order-by** (performance): 最適化で削除される可能性
20. **object-property** (performance): OBJECTPROPERTY廃止、代替関数
21. **upper-lower** (performance): 関数インデックスの推奨
22. **utc-datetime** (performance): タイムゾーン問題、Severity過剰の可能性
23. **join-keyword** (style): INNER JOIN明示の推奨
24. **nested-block-comments** (style): ネストコメントの問題
25. **require-begin-end-for-if-with-controlflow-exception** (style): カテゴリ不一致（Control Flow Safety → Style）
26. **require-begin-end-for-while** (style): カテゴリ不一致（Control Flow Safety → Style）
27. **require-explicit-join-type** (style): カテゴリ不一致（Query Structure → Style）
28. **semantic/case-sensitive-variables** (style): 変数名の大文字小文字一貫性
29. **semantic/schema-qualify** (style): スキーマ明示の推奨
30. **require-try-catch-for-transaction** (transactions): TRY-CATCHの必要性
31. **set-ansi** (transactions): ANSI_NULLS等の推奨設定

**優先度**: 🟢 低-中

---

## Tier 3: Information Severity（25件）

### 品質分布

| 評価 | 件数 |
|------|------|
| ✅ 高品質 | 3 |
| ✅ 良好 | 0 |
| ⚠️ 要改善 | 20 |
| ❌ 緊急修正 | 2 |

### 緊急修正が必要なルール（2件）

#### 1. conditional-begin-end (style)

**現状**: 53行、Information
**評価**: ❌ 緊急修正（Good例が空）

**問題点**:
- Good例が空: `-- Example showing compliant code`
- Rationaleが汎用テンプレート

**修正内容**:
```markdown
## Rationale

BEGIN/END blocks in conditional statements improve:

1. **Maintainability**: Easy to add more statements later
2. **Clarity**: Explicit block boundaries
3. **Error prevention**: Avoid single-statement assumption bugs

**Common bug without BEGIN/END**:
```sql
IF @x = 1
    SELECT 1;
    SELECT 2;  -- Always executes! Not part of IF
```

## Examples

### Bad

```sql
IF @x = 1 SELECT 1;  -- Single-line, no BEGIN/END

IF @Status = 'Active'
    UPDATE Users SET LastSeen = GETDATE();  -- Only this in IF
    SELECT @@ROWCOUNT;  -- Always executes!
```

### Good

```sql
IF @x = 1
BEGIN
    SELECT 1;
END;

IF @Status = 'Active'
BEGIN
    UPDATE Users SET LastSeen = GETDATE();
    SELECT @@ROWCOUNT;  -- Both in IF block
END;
```
```

**優先度**: 🔴 緊急（例が空）

---

#### 2. prefer-concat-ws (style)

**現状**: 53行、Information
**評価**: ❌ 緊急修正（Bad例が空）

**問題点**:
- Bad例が空: `-- Example showing rule violation`
- Good例のみあり

**修正内容**:
```markdown
## Examples

### Bad

```sql
-- Repetitive separator in CONCAT
SELECT CONCAT(FirstName, ',', LastName, ',', Email) FROM Users;

-- Or with + operator
SELECT FirstName + ',' + LastName + ',' + Email FROM Users;
```

### Good

```sql
-- CONCAT_WS eliminates repetition
SELECT CONCAT_WS(',', FirstName, LastName, Email) FROM Users;
```

**Compatibility**: SQL Server 2017+ (compat level 140+)
```

**優先度**: 🔴 緊急（例が不完全）

---

### 高品質ドキュメント（3件）

以下のルールは既に詳細なドキュメントを持つため、改善の優先度は低い:

1. **insert-select-column-name-mismatch** (correctness, 173行): 列名不一致の詳細
2. **disallow-select-distinct** (performance, 181行): DISTINCTの問題、代替案
3. **avoid-magic-convert-style-for-datetime** (style, 106行): CONVERT style番号の問題

---

### 中優先度改善項目（20件）

以下のルールはRationaleの拡充が必要だが、Information Severityのため優先度は低い:

3. **data-compression** (performance): Good例が無関係（`SELECT * FROM users`）
4. **full-text** (performance): フルテキストインデックスの推奨
5. **information-schema** (performance): INFORMATION_SCHEMAのパフォーマンス問題
6. **linked-server** (performance): リンクサーバーのパフォーマンス問題
7. **duplicate-empty-line** (style): 連続空行の禁止
8. **duplicate-go** (style): 連続GOの禁止
9. **prefer-coalesce-over-nested-isnull** (style): Good例がネストなし（矛盾）
10. **prefer-concat-over-plus** (style): CONCAT推奨
11. **prefer-concat-over-plus-when-nullable-or-convert** (style): NULL処理
12. **prefer-json-functions** (style): JSON関数の推奨
13. **prefer-string-agg-over-stuff** (style): STRING_AGG推奨（SQL 2017+）
14. **prefer-trim-over-ltrim-rtrim** (style): TRIM推奨（SQL 2017+）
15. **prefer-try-convert-patterns** (style): TRY_CONVERT推奨
16. **prefer-unicode-string-literals** (style): Unicode文字列推奨
17. **qualified-select-columns** (style): SELECT句での列修飾
18. **require-as-for-column-alias** (style): AS明示
19. **require-as-for-table-alias** (style): AS明示
20. **semicolon-termination** (style): セミコロン必須
21. **print-statement** (debug): Good例が不適切（`SELECT 'Hello World'`）
22. **require-ms-description-for-table-definition-file** (schema): MS_Description推奨

**優先度**: 🟢 低（Information Severity）

---

## Good Quality: 既存高品質ドキュメント（14件）

これらのルールは80行以上の詳細なドキュメントを持ち、改善の優先度は最低:

1. **avoid-exec-dynamic-sql** (security, 92行): SQLインジェクション、sp_executesql推奨
2. **avoid-merge** (safety, 163行): MERGEのバグ・非決定性、代替案
3. **avoid-nolock** (correctness, 155行): NOLOCKのdirty read・data corruption
4. **avoid-null-comparison** (correctness, 160行): NULL比較の罠、ANSI_NULLS
5. **ban-legacy-join-syntax** (correctness, 95行): カンマJOINの問題
6. **no-top-without-order-by-in-select-into** (correctness, 124行): SELECT INTOでのTOP問題
7. **dangerous-ddl** (safety, 133行): DROP/TRUNCATE等の危険操作
8. **avoid-implicit-conversion-in-predicate** (performance, 94行): 暗黙変換のインデックス無効化
9. **non-sargable** (performance, 99行): SARGable述語の最適化
10. **ban-query-hints** (performance, 221行): クエリヒントの問題・代替案
11. **catch-swallowing** (transactions, 241行): エラー抑制の危険性、パターン・反パターン
12. **transaction-without-commit-or-rollback** (transactions, 287行): トランザクション完結性
13. **uncommitted-transaction** (transactions, 159行): 未コミットトランザクション検出
14. **avoid-heap-table** (schema, 132行): ヒープテーブルの問題、クラスタ化インデックス推奨

**優先度**: なし（改善不要）

---

## 改善優先度リスト（Top 20）

### 🔴 緊急修正（空の例、矛盾、重大な問題）- 10件

| # | ルールID | カテゴリ | Severity | 問題 | 優先度 |
|---|----------|----------|----------|------|--------|
| 1 | **semantic/set-variable** | correctness | Error | ❌ Severity過剰（Error→Warning/Info）、SET/SELECT違い不明 | 🔴🔴🔴 |
| 2 | **top-without-order-by** | performance | Error | ❌ Severity過剰（Error→Warning）、非決定性の説明不足 | 🔴🔴🔴 |
| 3 | **order-by-in-subquery** | correctness | Error | ❌ Severity過剰（Error→Warning）、ドキュメント自体は良好 | 🔴🔴 |
| 4 | **set-nocount** | transactions | Warning | ❌ Bad/Good例が両方とも空 | 🔴🔴 |
| 5 | **set-quoted-identifier** | transactions | Warning | ❌ Bad/Good例が両方とも空 | 🔴🔴 |
| 6 | **set-transaction-isolation-level** | transactions | Warning | ❌ Bad/Good例が両方とも空 | 🔴🔴 |
| 7 | **cross-database-transaction** | safety | Warning | ❌ Bad/Good例が同一内容 | 🔴🔴 |
| 8 | **require-qualified-columns-everywhere** | style | Warning | ❌ Bad/Good例がルールと矛盾（単一テーブル） | 🔴 |
| 9 | **semantic/multi-table-alias** | style | Warning | ❌ Good例が不適切（単一テーブル） | 🔴 |
| 10 | **conditional-begin-end** | style | Information | ❌ Good例が空 | 🔴 |
| 11 | **prefer-concat-ws** | style | Information | ❌ Bad例が空 | 🔴 |

### 🟡 高優先度（Rationale不足、Error Severityルール）- 10件

| # | ルールID | カテゴリ | Severity | 問題 | 優先度 |
|---|----------|----------|----------|------|--------|
| 12 | **dml-without-where** | safety | Error | ビジネス影響（全レコード削除）の深刻さ不明 | 🟡🟡 |
| 13 | **named-constraint** | correctness | Error | tempdb名前衝突の本質不明 | 🟡🟡 |
| 14 | **semantic/data-type-length** | correctness | Error | デフォルト長（1バイト）→トランケーション説明なし | 🟡🟡 |
| 15 | **semantic/unicode-string** | correctness | Error | コードページ依存・データ損失説明なし | 🟡🟡 |
| 16 | **require-xact-abort-on** | transactions | Warning | Good例が空、Rationale誤り（部分コミット防止） | 🟡🟡 |
| 17 | **semantic/cte-name-conflict** | correctness | Error | 具体的エラーメッセージなし | 🟡 |
| 18 | **semantic/duplicate-alias** | correctness | Error | 曖昧さのスコープ不明 | 🟡 |
| 19 | **semantic/insert-column-count-mismatch** | correctness | Error | エラーメッセージ例なし | 🟡 |
| 20 | **semantic/undefined-alias** | correctness | Error | SQL評価順序（WHEREでalias使えない理由）なし | 🟡 |

### 🟠 中優先度（Rationaleテンプレート化、例不足）- 残り全て

Tier 2 Warning（残り24件）、Tier 3 Information（残り20件）は全て汎用Rationaleテンプレートを使用しており、以下の改善が必要:

- 具体的な問題の説明（パフォーマンス影響、セキュリティリスク等）
- 複数シナリオの例（Bad 2つ以上、Good対応する修正例）
- エッジケースの明記（例外的なケース、SQL Serverバージョン依存性）

**優先度**: 🟠 中-低（Severityが低いため）

---

## 共通問題パターン

### 1. Rationaleの汎用テンプレート使用（約70件）

以下のフレーズが多用されている:

| テンプレート | 使用箇所 | 件数 |
|-------------|---------|------|
| "This rule maintains code formatting and consistency" | Style系 | ~30件 |
| "This rule identifies patterns that can cause performance issues" | Performance系 | ~15件 |
| "This rule prevents destructive or dangerous operations" | Safety系 | ~5件 |
| "This rule prevents code that may produce incorrect results or runtime errors" | Correctness系 | ~20件 |

**推奨**: 各ルール固有の具体的な問題を記載する。

**良い例（catch-swallowing）**:
```markdown
Error suppression makes debugging impossible. When a CATCH block silently swallows errors:
- **Production incidents** become impossible to diagnose
- **Data corruption** may go unnoticed
- **Transaction state** becomes unpredictable
```

**悪い例（大半のルール）**:
```markdown
This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.
```

---

### 2. 空のGood/Bad例（7件）

| ルールID | Bad例 | Good例 |
|----------|-------|--------|
| set-nocount | ❌ 空 | ❌ 空 |
| set-quoted-identifier | ❌ 空 | ❌ 空 |
| set-transaction-isolation-level | ❌ 空 | ❌ 空 |
| require-xact-abort-on | ✅ あり | ❌ 空 |
| conditional-begin-end | ✅ あり | ❌ 空 |
| prefer-concat-ws | ❌ 空 | ✅ あり |

**推奨**: すべての例を埋める。テンプレートコメント（`-- Example showing rule violation`）を削除。

---

### 3. Bad/Good例の内容矛盾（5件）

| ルールID | 問題 |
|----------|------|
| cross-database-transaction | Bad/Good例が完全に同一（`SELECT * FROM DB1.dbo.Table1`） |
| require-qualified-columns-everywhere | Badは複数テーブル、Goodは単一テーブル（ルールと矛盾） |
| semantic/multi-table-alias | 同上（Goodに複数テーブル例なし） |
| data-compression | Good例が無関係（`SELECT * FROM users`、CREATE TABLE例が必要） |
| prefer-coalesce-over-nested-isnull | Good例がネストなし（`ISNULL(@value, 'default')`） |
| print-statement | Good例が不適切（`SELECT 'Hello World'`、RAISERRORが必要） |

**推奨**: 例を修正し、ルールの意図を正確に反映させる。

---

### 4. カテゴリ不一致（3件）

| ルールID | 現在のカテゴリ | ドキュメント記載 | 推奨 |
|----------|---------------|----------------|------|
| require-begin-end-for-if-with-controlflow-exception | style | Control Flow Safety | Style |
| require-begin-end-for-while | style | Control Flow Safety | Style |
| require-explicit-join-type | style | Query Structure | Style |

**推奨**: すべてStyleカテゴリに統一（機能的影響なし、可読性・保守性の問題）。

---

### 5. Severity過剰（4件）

| ルールID | 現在 | 推奨 | 理由 |
|----------|------|------|------|
| semantic/set-variable | Error | Warning/Information | スタイル問題、SET/SELECT両方とも正しい |
| top-without-order-by | Error | Warning | 非決定的だが実行される、エラーではない |
| order-by-in-subquery | Error | Warning | 無駄だが実行される、エラーではない |
| utc-datetime | Warning | Information | 状況依存、タイムゾーン問題は必須ではない |

**基準**:
- **Error**: データ損失、実行時エラー、セマンティックエラー（実行不可）
- **Warning**: 重要だが状況依存、パフォーマンス、セキュリティリスク
- **Information**: スタイル、一貫性、機能的影響なし

---

## レビュー方法論

このレビューは以下の基準で実施された:

### 説明内容の充実度

#### Description（1行）
- ルールの目的を簡潔に説明
- 検出する問題パターンを明示

#### Rationale（段落）
必須要素:
- **ビジネス影響**: なぜこのルールが重要か（データ損失、パフォーマンス、保守性等）
- **具体的問題**: 違反した場合の具体的な結果（エラーメッセージ、バグ例、パフォーマンス劣化）
- **解決方法**: 推奨アプローチ、ベストプラクティス

NG（汎用テンプレート）:
```markdown
This rule maintains code formatting and consistency. Following this rule improves code readability and makes it easier to maintain.
```

OK（具体的説明）:
```markdown
TOP without ORDER BY returns unpredictable rows based on physical storage order, which changes with index maintenance, page splits, and parallel execution. Results vary between executions, breaking reproducibility requirements.
```

#### Examples（複数シナリオ）
- **Bad例**: 2つ以上の違反パターン（典型例、エッジケース）
- **Good例**: Bad例に対応する修正方法
- **コメント**: なぜBadか、Goodで何が改善されるかの説明

---

### エッジケースの考慮

#### SQL Serverバージョン依存性
- compat level 100-160での挙動の違い
- 新機能（CONCAT_WS, STRING_AGG等）の対象バージョン明記

#### SET設定依存性
- ANSI_NULLS, QUOTED_IDENTIFIER等の影響
- デフォルト値と推奨値

#### 例外的なケース
- ルールを無効化すべきシナリオ
- 意図的な違反が許容される状況

#### 検出ロジックの制限事項
- 動的SQLでの検出不可
- ネストしたクエリでの制限
- 偽陽性・偽陰性の可能性

---

### Severity妥当性

#### Error（11件）
**基準**: データ損失、実行時エラー、セマンティックエラー（実行不可）

適切な例:
- `dml-without-where`: 全レコード削除/更新（データ損失）
- `semantic/data-type-length`: 暗黙のトランケーション（データ損失）
- `semantic/duplicate-alias`: 曖昧さ（実行時エラー）

**不適切な例（Severity変更推奨）**:
- `semantic/set-variable`: スタイル問題（SET/SELECT両方とも正しい）
- `top-without-order-by`: 非決定的だがエラーではない
- `order-by-in-subquery`: 無駄だがエラーではない

#### Warning（34件）
**基準**: 重要だが状況依存、パフォーマンス問題、セキュリティリスク

適切な例:
- `avoid-select-star`: パフォーマンス劣化
- `avoid-exec-dynamic-sql`: SQLインジェクションリスク
- `cross-database-transaction`: 分散トランザクション問題

#### Information（25件）
**基準**: スタイル、一貫性、機能的影響なし

適切な例:
- `semicolon-termination`: スタイル
- `prefer-concat-ws`: 可読性（機能的には同等）
- `duplicate-empty-line`: フォーマット

**現状維持優先**: 明らかに不適切な場合のみ変更を提案。

---

## 推奨アクション

### 即座に対応すべき項目（1-2週間）- 14件

#### 1. Severity変更（3件）
- ❌ **semantic/set-variable**: Error → Warning/Information
- ❌ **top-without-order-by**: Error → Warning
- ❌ **order-by-in-subquery**: Error → Warning

#### 2. 空の例を埋める（7件）
- set-nocount（両方）
- set-quoted-identifier（両方）
- set-transaction-isolation-level（両方）
- require-xact-abort-on（Good）
- conditional-begin-end（Good）
- prefer-concat-ws（Bad）

#### 3. 矛盾した例を修正（5件）
- cross-database-transaction（同一内容）
- require-qualified-columns-everywhere（単一テーブル）
- semantic/multi-table-alias（単一テーブル）
- data-compression（無関係な例）
- prefer-coalesce-over-nested-isnull（ネストなし）
- print-statement（RAISERROR例なし）

#### 4. カテゴリ不一致を修正（3件）
- require-begin-end-for-if-with-controlflow-exception → Style
- require-begin-end-for-while → Style
- require-explicit-join-type → Style

---

### 中期的に対応すべき項目（1-2ヶ月）- 20件

#### 5. Tier 1ルール（11件）のRationale拡充
全て汎用テンプレートから脱却し、具体的な説明へ:
- dml-without-where: 全件削除の影響
- named-constraint: tempdb衝突の詳細
- semantic/cte-name-conflict: エラーメッセージ
- semantic/data-type-length: トランケーション詳細
- semantic/duplicate-alias: 曖昧さのスコープ
- semantic/insert-column-count-mismatch: エラー例
- semantic/undefined-alias: SQL評価順序
- semantic/unicode-string: コードページ依存
- （semantic/set-variable, top-without-order-by, order-by-in-subqueryは上記でSeverity変更）

#### 6. Tier 2の緊急修正・高優先度（10件）のRationale拡充
- require-xact-abort-on: 部分コミット防止
- require-qualified-columns-everywhere: 曖昧さ防止
- semantic/multi-table-alias: 同上
- avoid-select-star: 具体的パフォーマンス影響
- disallow-cursors: カーソル vs セットベース定量比較
- escape-keyword-identifier: 予約語衝突
- require-column-list-for-insert-select: スキーマ変更影響
- require-column-list-for-insert-values: 同上
- require-parentheses-for-mixed-and-or: 優先順位誤解釈
- avoid-ambiguous-datetime-literal: リージョン依存

---

### 長期的に対応すべき項目（3ヶ月以上）- 50件

#### 7. Tier 2中優先度（14件）の例追加・Rationale拡充
- avoid-atat-identity, semantic/join-condition-always-true, semantic/left-join-filtered-by-where
- avoid-top-in-dml, forbid-top-100-percent-order-by, object-property, upper-lower, utc-datetime
- join-keyword, nested-block-comments, semantic/case-sensitive-variables, semantic/schema-qualify
- require-try-catch-for-transaction, set-ansi

#### 8. Tier 3ルール（20件）の段階的改善
Information Severityのため優先度は低いが、以下を改善:
- data-compression, full-text, information-schema, linked-server
- duplicate-empty-line, duplicate-go, prefer-* シリーズ（8件）
- qualified-select-columns, require-as-for-*, semicolon-termination
- print-statement, require-ms-description-for-table-definition-file

---

## まとめ

TsqlRefineのルールドキュメントは全体的に一貫した構造を持つ一方、多くのルールで以下の改善機会がある:

### 主要な発見

#### 1. Rationaleの個別化（約70件）
- **現状**: 汎用テンプレート（「maintains code formatting and consistency」等）を多用
- **推奨**: 各ルール固有の具体的な問題を記載
- **例**: catch-swallowingの詳細なRationaleを参考に、ビジネス影響・具体的問題・解決方法を明記

#### 2. 例の充実（約15件）
- **空の例**: 7件（両方空が3件、片方空が4件）
- **矛盾した例**: 5件（Bad/Good例がルールと不一致）
- **推奨**: 複数シナリオ（Bad 2つ以上、Good対応する修正例）、コメントで説明

#### 3. エッジケースの明記（約60件）
- **現状**: 制限事項や例外状況の記載が少ない
- **推奨**: SQL Serverバージョン依存性、SET設定影響、検出ロジックの制限を文書化

#### 4. Severity の見直し（4件）
- **semantic/set-variable**: Error → Warning/Information（スタイル問題）
- **top-without-order-by**: Error → Warning（非決定的だがエラーではない）
- **order-by-in-subquery**: Error → Warning（無駄だがエラーではない）
- **utc-datetime**: Warning → Information（状況依存、必須ではない）

---

### 改善の優先順位

| 優先度 | 件数 | 内容 | 期限 |
|--------|------|------|------|
| 🔴 緊急 | 14 | 空の例（7）、矛盾（5）、Severity変更（3）、カテゴリ（3） | 1-2週間 |
| 🟡 高 | 20 | Tier 1 Rationale拡充（11）、Tier 2緊急修正（10） | 1-2ヶ月 |
| 🟠 中 | 50 | Tier 2中優先度（14）、Tier 3全般（20）、汎用テンプレート（残り） | 3ヶ月以上 |
| 🟢 低 | 14 | Good Quality（既に高品質、改善不要） | なし |

---

### 品質メトリクス（最終）

| メトリクス | 現状 | 目標 |
|----------|------|------|
| **高品質（80行以上）** | 14件（17%） | 30件（36%） |
| **良好（60行以上）** | 20件（24%） | 40件（48%） |
| **要改善（60行未満）** | 50件（59%） | 14件（17%） |
| **緊急修正（空・矛盾）** | 14件 | 0件 |
| **Severity適切** | 80件（95%） | 84件（100%） |
| **カテゴリ適切** | 81件（96%） | 84件（100%） |

---

### 次のステップ

#### Phase 1（即座、1-2週間）
1. Severity変更: semantic/set-variable, top-without-order-by, order-by-in-subquery
2. 空の例を埋める: 7件
3. 矛盾した例を修正: 5件
4. カテゴリ不一致を修正: 3件

#### Phase 2（中期、1-2ヶ月）
5. Tier 1全件のRationale拡充（11件、Error Severity優先）
6. Tier 2緊急・高優先度のRationale拡充（10件）

#### Phase 3（長期、3ヶ月以上）
7. Tier 2中優先度（14件）
8. Tier 3全般（20件）
9. 汎用テンプレートの個別化（残り全て）

---

**全85件中、約70件が何らかの改善の恩恵を受けると評価される。**
特に緊急修正14件と高優先度20件（計34件、40%）は、ユーザー体験向上のため早期対応を推奨する。
