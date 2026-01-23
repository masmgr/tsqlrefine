# TSQLLint ルール要約

このドキュメントは、TSQLLintで利用可能な全33個のルールの要約です。

## 目次

- [コードスタイルとフォーマット](#コードスタイルとフォーマット)
- [データベース構成](#データベース構成)
- [データ型と宣言](#データ型と宣言)
- [クエリ設計](#クエリ設計)
- [データ変更](#データ変更)
- [制御フロー](#制御フロー)
- [パフォーマンス](#パフォーマンス)
- [データベースアクセス](#データベースアクセス)
- [制約と分離](#制約と分離)
- [出力とデバッグ](#出力とデバッグ)

---

## コードスタイルとフォーマット

### keyword-capitalization
**目的**: SQLキーワードを大文字で記述することを要求

**理由**: キーワードを大文字にすることで可読性が向上し、キーワードとオブジェクトの明確な区別が可能

**不正な例**:
```sql
select * from foo;
```

**正しい例**:
```sql
SELECT * FROM foo;
```

---

### semicolon-termination
**目的**: クエリをセミコロンで終了することを強制

**理由**: MicrosoftのTransact-SQL構文規則に従う

**不正な例**:
```sql
SELECT user_name FROM dbo.MyTable
```

**正しい例**:
```sql
SELECT user_name FROM dbo.MyTable;
```

---

### case-sensitive-variables
**目的**: 変数参照で共通の大文字小文字を強制

**理由**: 変数が複数回参照される場合の一貫性を維持

**不正な例**:
```sql
DECLARE @ProductID INT;
SELECT @PRODUCTID;
```

**正しい例**:
```sql
DECLARE @ProductID INT;
SELECT @ProductID;
```

---

### duplicate-empty-line
**目的**: 連続する空行を禁止

**理由**: コードフォーマットの一貫性を維持

**検出**: 連続する空行の2行目以降にフラグを立てる

---

### duplicate-go
**目的**: 連続するGOバッチ区切り文字を禁止

**理由**: コードの整理を改善し、不要なバッチ区切り文字を防止

**検出**: 空白、コメント、セミコロンを無視して検出

---

### nested-block-comments
**目的**: ネストされたブロックコメントを回避

**理由**: 一部のSQLツールはネストされたコメントをサポートせず、スクリプトの解析に失敗する可能性がある

**不正な例**:
```sql
/* outer /* inner */ more text */
```

**正しい例**:
```sql
/* outer -- inner comment */
```

---

### upper-lower
**目的**: 比較でUPPERまたはLOWER関数を使用することを禁止

**理由**: 大文字小文字を区別しないデータベースでは不要で、クエリのSARGabilityに影響を与える可能性がある

**不正な例**:
```sql
WHERE UPPER(first_name) = 'NATHAN';
```

**正しい例**:
```sql
WHERE first_name = 'NATHAN';
```

---

## データベース構成

### set-ansi
**目的**: ファイルの先頭近くで `SET ANSI_NULLS ON` の設定を強制

**理由**: 重要なデータベース構成設定

**要件**: スクリプトの冒頭近くに記述する必要がある

---

### set-nocount
**目的**: ファイルの先頭近くで `SET NOCOUNT ON` の設定を強制

**理由**: 結果カウントからの不要なネットワークトラフィックを削減

**要件**: スクリプトの冒頭近くに記述する必要がある

---

### set-quoted-identifier
**目的**: ファイルの先頭近くで `SET QUOTED_IDENTIFIER ON` の設定を強制

**理由**: 適切なSQL識別子の処理に不可欠

**要件**: スクリプトの冒頭近くに記述する必要がある

---

### set-transaction-isolation-level
**目的**: ファイルの先頭近くで `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` の設定を強制

**理由**: トランザクション分離動作を明示的に設定

**要件**: スクリプトの冒頭近くに記述する必要がある

---

## データ型と宣言

### data-type-length
**目的**: 可変長データ型を宣言する際に長さの使用を要求

**理由**: データベースサイズの制御とI/Oパフォーマンスの向上

**不正な例**:
```sql
CREATE TABLE MyTable (Name nvarchar);
```

**正しい例**:
```sql
CREATE TABLE MyTable (Name nvarchar(50));
```

---

### set-variable
**目的**: SELECT文を使用して変数値を設定することを強制（SETではなく）

**理由**: SELECTは変数割り当ての推奨方法

**不正な例**:
```sql
DECLARE @MyVar VARCHAR(30);
SET @MyVar = 'value';
```

**正しい例**:
```sql
DECLARE @MyVar VARCHAR(30);
SELECT @MyVar = 'value';
```

---

### unicode-string
**目的**: 非Unicode文字列でのUnicode文字を禁止

**理由**: データ損失とエンコーディングの問題を防止

**不正な例**:
```sql
DECLARE @MyVar VARCHAR(30) = 'Ⱦhis is incorrect.';
```

**正しい例**:
```sql
DECLARE @MyVar NVARCHAR(30) = N'Ⱦhis is correct.';
```

---

## クエリ設計

### select-star
**目的**: アスタリスクで全列を選択することを禁止

**理由**: 列を明示的に指定することで明確性とパフォーマンスが向上

**不正な例**:
```sql
SELECT * FROM dbo.MyTable;
```

**正しい例**:
```sql
SELECT user_id, user_name, created_on FROM dbo.MyTable;
```

---

### count-star
**目的**: COUNT(*)の使用を禁止

**理由**: より明確にするためCOUNT(1)またはCOUNT(column_name)を使用

**不正な例**:
```sql
SELECT COUNT(*) FROM Production.Product;
```

**正しい例**:
```sql
SELECT COUNT(1) FROM Production.Product;
-- または
SELECT COUNT([ProductID]) FROM Production.Product;
```

---

### schema-qualify
**目的**: スキーマ名でオブジェクトを修飾することを強制

**理由**: クエリの明確性を向上し、暗黙的なスキーマ選択を防止

**不正な例**:
```sql
SELECT user_name FROM MyTable;
```

**正しい例**:
```sql
SELECT user_name FROM dbo.MyTable;
```

---

### multi-table-alias
**目的**: 複数テーブルの結合でエイリアスを使用することを強制

**理由**: クエリの可読性が向上し、曖昧さを防止

**不正な例**:
```sql
SELECT user_name, country_name
FROM dbo.MyTable
INNER JOIN dbo.Country ON id = country_id;
```

**正しい例**:
```sql
SELECT mt.user_name, c.country_name
FROM dbo.MyTable AS mt
INNER JOIN dbo.Country AS c ON c.id = mt.country_id;
```

---

### join-keyword
**目的**: 暗黙的結合（カンマ結合）の使用を禁止

**理由**: カンマ結合は古いスタイル。明示的なJOIN...ON構文の方が読みやすい

**不正な例**:
```sql
SELECT Name, ListPrice
FROM Production.Product p, Production.ProductCategory pc
WHERE p.CategoryID = pc.CategoryID;
```

**正しい例**:
```sql
SELECT Name, ListPrice
FROM Production.Product p
INNER JOIN Production.ProductCategory pc ON p.CategoryID = pc.CategoryID;
```

---

## データ変更

### delete-where
**目的**: DELETEを使用する際にWHERE条件の使用を強制

**理由**: すべてのレコードの誤削除を防止

**不正な例**:
```sql
DELETE FROM dbo.MyTable;
```

**正しい例**:
```sql
DELETE FROM dbo.MyTable WHERE CreatedOn = GETDATE();
```

---

### update-where
**目的**: UPDATEを使用する際にWHERE条件の使用を強制

**理由**: すべてのレコードの誤更新を防止

**不正な例**:
```sql
UPDATE mytable
SET CreatedOn = GETDATE()
FROM dbo.MyTable AS mytable;
```

**正しい例**:
```sql
UPDATE mytable
SET CreatedOn = GETDATE()
FROM dbo.MyTable AS mytable
WHERE CreatedOn IS NULL;
```

---

## 制御フロー

### conditional-begin-end
**目的**: 条件文内でBEGINとENDシンボルの使用を強制

**理由**: 条件文を単一のブロックとしてバインドし、明確性を向上させエラーを防止

**不正な例**:
```sql
IF (@parm = 1)
    SELECT @output = 'foo'
```

**正しい例**:
```sql
IF (@parm = 1)
BEGIN
    SELECT @output = 'foo'
END
```

---

## パフォーマンス

### non-sargable
**目的**: フィルター句または結合述語での関数を禁止

**理由**: WHERE/JOIN句での関数はインデックス使用を妨げ、パフォーマンスを低下させる

**不正な例**:
```sql
SELECT user_name
FROM dbo.MyTable
WHERE DATEDIFF(day, created_on, GETDATE()) <= 3;
```

**正しい例**:
```sql
SELECT user_name
FROM dbo.MyTable
WHERE created_on >= DATEADD(day, -3, GETDATE());
```

---

### disallow-cursors
**目的**: カーソルの使用を禁止

**理由**: カーソルは行ベースの操作を導入。セットベースの操作が推奨される

**代替**: JOINまたはセットベースの操作を使用

---

### full-text
**目的**: フルテキストの使用を禁止

**理由**: 不適切に調整されたフルテキストクエリはパフォーマンス問題を引き起こす可能性がある

**不正な例**:
```sql
WHERE ListPrice = 80.99 AND CONTAINS(Name, 'Mountain')
```

**正しい例**:
```sql
WHERE ListPrice = 80.99 AND Name like '%Mountain%'
```

---

### data-compression
**目的**: テーブル作成時に圧縮オプションの使用を要求

**理由**: データ圧縮はデータベースサイズを削減し、I/O集約型ワークロードのパフォーマンスを向上

**不正な例**:
```sql
CREATE TABLE MyTable (ID INT, Name nvarchar(50))
```

**正しい例**:
```sql
CREATE TABLE MyTable (ID INT, Name nvarchar(50))
WITH (DATA_COMPRESSION = ROW);
```

---

### utc-datetime
**目的**: ホストローカルの日付/時刻関数を禁止

**理由**: ローカル関数はOSタイムゾーン設定に依存。UTCの方が信頼性が高い

**不正な例**:
```sql
SELECT GETDATE();
SELECT SYSDATETIMEOFFSET();
```

**正しい例**:
```sql
SELECT SYSUTCDATETIME();
SELECT GETUTCDATE();
```

---

## データベースアクセス

### information-schema
**目的**: INFORMATION_SCHEMAビューの使用を禁止

**理由**: SYS.OBJECTSはオブジェクトメタデータのより良いソース

**不正な例**:
```sql
SELECT table_name
FROM INFORMATION_SCHEMA.TABLES
WHERE table_schema = 'MyTable'
```

**正しい例**:
```sql
SELECT name
FROM sys.objects
WHERE OBJECTPROPERTY(object_id, N'SchemaId') = SCHEMA_ID(N'Production')
```

---

### object-property
**目的**: sysビューよりObjectProperty関数の使用を禁止

**理由**: sysビューはOBJECTPROPERTY関数より効率的

**不正な例**:
```sql
IF OBJECTPROPERTY(OBJECT_ID(N'dbo.MyTable'),'ISTABLE') = 1
```

**正しい例**:
```sql
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MyTable')
```

---

### linked-server
**目的**: リンクサーバー呼び出しの使用を禁止

**理由**: リンクサーバークエリはテーブルロックを引き起こす可能性があり推奨されない

**不正な例**:
```sql
SELECT Foo FROM MyServer.MyDatabase.MySchema.MyTable;
```

**正しい例**:
```sql
SELECT Foo FROM MyDatabase.MySchema.MyTable;
```

---

## 制約と分離

### named-constraint
**目的**: 一時テーブルでの名前付き制約を禁止

**理由**: 一時テーブルの名前付き制約は並列使用時に衝突を引き起こす可能性がある

**不正な例**:
```sql
CREATE TABLE #temporary (
    ID INT IDENTITY (1,1),
    CreatedOn DATETIME2 NOT NULL CONSTRAINT [df_CreatedOn] DEFAULT GETDATE()
);
```

**正しい例**:
```sql
CREATE TABLE #temporary (
    ID INT IDENTITY (1,1),
    CreatedOn DATETIME2 NOT NULL DEFAULT GETDATE()
);
```

---

### cross-database-transaction
**目的**: 複数のデータベースでトランザクションを作成する挿入または更新を非推奨

**理由**: データベース間トランザクションはデータ破損につながる可能性がある

**不正な例**:
```sql
BEGIN TRANSACTION;
UPDATE DB1.dbo.Table1 SET Value = 1;
UPDATE DB2.dbo.Table2 SET Value = 1;
COMMIT TRANSACTION;
```

**正しい例**: 各データベース更新を別々のトランザクションでラップ

---

## 出力とデバッグ

### print-statement
**目的**: PRINT文の使用を禁止

**理由**: エラーメッセージとデバッグにはRAISERRORが推奨される

**不正な例**:
```sql
PRINT 'This is a debug message.'
```

**正しい例**:
```sql
RAISERROR('This is a debug message.', 160, 1);
```

---

## ルール構成

全33個のルールは、`.tsqllintrc`ファイルで以下の重要度レベルで構成できます:

- `"off"` - ルールを無効化
- `"warning"` - 警告として報告
- `"error"` - エラーとして報告

また、インラインコメント `/* tsqllint-disable rule-name */` を使用して、ファイル単位でルールを無効化することもできます。

---

## カテゴリー別ルール数

| カテゴリー | ルール数 |
|-----------|---------|
| コードスタイルとフォーマット | 7 |
| データベース構成 | 4 |
| データ型と宣言 | 3 |
| クエリ設計 | 5 |
| データ変更 | 2 |
| 制御フロー | 1 |
| パフォーマンス | 5 |
| データベースアクセス | 3 |
| 制約と分離 | 2 |
| 出力とデバッグ | 1 |
| **合計** | **33** |
