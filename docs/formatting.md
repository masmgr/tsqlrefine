# Formatter 仕様

このドキュメントは `TsqlRefine.Formatting` モジュールの仕様と使用方法を説明します。

---

## 1. 概要

`TsqlRefine.Formatting` は T-SQL コードの最小限フォーマットを提供するモジュールです。

**設計思想**:
- **最小限のフォーマット**: キーワードの大文字小文字変換と空白の正規化のみ
- **構造の保持**: コメント、文字列リテラル、コード構造を変更しない
- **構成可能**: 各要素の大文字小文字を独立して制御可能
- **拡張可能**: 新しいフォーマットパスを追加可能

---

## 2. フォーマットパイプライン

SQL 入力は以下の順序で処理されます:

```
SQL 入力
    ↓
1. 要素別ケーシング (ScriptDomElementCaser)
    - ScriptDom トークンストリームを使用
    - キーワード、関数、データ型、識別子を分類
    - カテゴリごとに大文字/小文字を適用
    ↓
2. 空白の正規化 (WhitespaceNormalizer)
    - 改行の正規化 (CRLF → LF)
    - インデントの正規化 (.editorconfig 対応)
    - 末尾空白の除去 (オプション)
    - 最終改行の挿入 (オプション)
    ↓
3. インラインスペースの正規化 (InlineSpaceNormalizer)
    - コンマ後のスペース追加 (a,b → a, b)
    - 重複スペースの削除 (SELECT  * → SELECT *)
    - 保護領域内は変更しない
    ↓
4. コンマスタイル変換 (CommaStyleTransformer, オプション)
    - 末尾コンマから先頭コンマへの変換
    ↓
フォーマット済み SQL 出力
```

---

## 3. フォーマットオプション

### 3.1 FormattingOptions

| プロパティ | 型 | 既定値 | 説明 |
|-----------|------|--------|------|
| `CompatLevel` | `int` | `150` | SQL Server 互換レベル (100-170) ※170はパッケージ更新が必要 |
| `IndentStyle` | `IndentStyle` | `Spaces` | インデントスタイル (Tabs/Spaces) |
| `IndentSize` | `int` | `4` | インデントサイズ (スペース数またはタブ幅) |
| `KeywordElementCasing` | `ElementCasing` | `Upper` | キーワードの大文字小文字 |
| `BuiltInFunctionCasing` | `ElementCasing` | `Upper` | 組み込み関数の大文字小文字 |
| `DataTypeCasing` | `ElementCasing` | `Lower` | データ型の大文字小文字 |
| `SchemaCasing` | `ElementCasing` | `None` | スキーマ名の大文字小文字 (※CS環境注意) |
| `TableCasing` | `ElementCasing` | `None` | テーブル名の大文字小文字 (※CS環境注意) |
| `ColumnCasing` | `ElementCasing` | `None` | カラム名の大文字小文字 (※CS環境注意) |
| `VariableCasing` | `ElementCasing` | `Lower` | 変数の大文字小文字 |
| `CommaStyle` | `CommaStyle` | `Trailing` | コンマスタイル (Trailing/Leading) |
| `MaxLineLength` | `int` | `0` | 最大行長 (0 = 無制限) |
| `InsertFinalNewline` | `bool` | `true` | ファイル末尾に改行を挿入 |
| `TrimTrailingWhitespace` | `bool` | `true` | 行末の空白を除去 |
| `NormalizeInlineSpacing` | `bool` | `true` | インラインスペースを正規化 |

### 3.2 列挙型

**IndentStyle**:
```csharp
public enum IndentStyle
{
    Tabs,
    Spaces
}
```

**ElementCasing**:
```csharp
public enum ElementCasing
{
    None,   // 変更なし (元のまま)
    Upper,  // 大文字
    Lower   // 小文字
}
```

**CommaStyle**:
```csharp
public enum CommaStyle
{
    Trailing,  // 末尾コンマ: SELECT a, b, c
    Leading    // 先頭コンマ:
               // SELECT a
               //      , b
               //      , c
}
```

> **警告**: Case-Sensitive collation 環境（例: Microsoft Fabric Data Warehouse）では、
> 識別子のケーシング変更によりクエリが壊れる可能性があります。
> `SchemaCasing`, `TableCasing`, `ColumnCasing` を `Upper` または `Lower` に設定する前に、
> ターゲット環境の collation を確認してください。既定は `None`（元のまま保持）です。

---

## 4. 要素カテゴリ

`SqlElementCategorizer` は T-SQL トークンを以下のカテゴリに分類します:

| カテゴリ | 説明 | 例 |
|---------|------|-----|
| `Keyword` | SQL キーワード | `SELECT`, `FROM`, `WHERE`, `JOIN` |
| `BuiltInFunction` | 組み込み関数 | `COUNT`, `SUM`, `GETDATE`, `ISNULL` |
| `DataType` | データ型 | `INT`, `VARCHAR`, `DATETIME`, `DECIMAL` |
| `Schema` | スキーマ名 | `dbo`, `sys`, `staging` |
| `Table` | テーブル名/エイリアス | `users`, `orders`, `u`, `o` |
| `Column` | カラム名/エイリアス | `id`, `name`, `created_at` |
| `Variable` | 変数 | `@id`, `@name`, `@@ROWCOUNT` |
| `Other` | その他 (リテラル、演算子など) | `'text'`, `123`, `+`, `-` |

### 4.1 カテゴリ判定ロジック

1. **変数**: `@` で始まるトークン、または `Variable` を含むトークンタイプ
2. **組み込み関数**: 既知の関数名で、後に `(` が続く場合
3. **データ型**: 既知のデータ型名
4. **スキーマ**: 識別子で、後に `.` が続く場合
5. **テーブル**: FROM/JOIN/INTO/UPDATE の後の識別子、または `.` の後
6. **カラム**: 上記以外の識別子
7. **キーワード**: 上記以外のワードトークン

---

## 5. 保護領域

以下の領域は変換から保護されます:

| 領域 | 開始 | 終了 | 例 |
|------|------|------|-----|
| 文字列リテラル | `'` | `'` | `'Hello World'` |
| ダブルクォート識別子 | `"` | `"` | `"Column Name"` |
| ブラケット識別子 | `[` | `]` | `[Table Name]` |
| ブロックコメント | `/*` | `*/` | `/* comment */` |
| 行コメント | `--` | 行末 | `-- comment` |

**エスケープ処理**:
- `''` (シングルクォート内)
- `]]` (ブラケット内)

---

## 6. ヘルパークラス

### 6.1 SqlFormatter

メインのオーケストレータ。フォーマットパイプラインを調整します。

```csharp
using TsqlRefine.Formatting;

var sql = "select id, name from users where active = 1";
var formatted = SqlFormatter.Format(sql);
// 結果: "SELECT ID, NAME FROM USERS WHERE ACTIVE = 1\n"
```

### 6.2 ScriptDomElementCaser

ScriptDom トークンストリームを使用して要素別のケーシングを適用します。

```csharp
using TsqlRefine.Formatting.Helpers;

var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    ColumnCasing = ElementCasing.Lower
};

var cased = ScriptDomElementCaser.Apply(sql, options, compatLevel: 150);
```

**特徴**:
- 互換レベル対応 (100-160)
- トークンタイプ名のキャッシュによる高速化
- 前後のトークンを考慮したコンテキスト認識

### 6.3 SqlElementCategorizer

T-SQL トークンを要素カテゴリに分類します。

```csharp
using TsqlRefine.Formatting.Helpers;

var category = SqlElementCategorizer.Categorize(token, previousToken, nextToken, context);
```

**組み込み関数一覧** (一部):
- 集約関数: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`
- 文字列関数: `LEN`, `SUBSTRING`, `REPLACE`, `CONCAT`
- 日付関数: `GETDATE`, `DATEADD`, `DATEDIFF`
- 変換関数: `CAST`, `CONVERT`, `TRY_CAST`
- NULL処理: `ISNULL`, `COALESCE`, `NULLIF`
- ランキング関数: `ROW_NUMBER`, `RANK`, `DENSE_RANK`

**データ型一覧** (一部):
- 数値: `INT`, `BIGINT`, `DECIMAL`, `FLOAT`
- 文字列: `CHAR`, `VARCHAR`, `NVARCHAR`, `TEXT`
- 日付: `DATE`, `TIME`, `DATETIME`, `DATETIME2`
- その他: `BIT`, `UNIQUEIDENTIFIER`, `XML`

### 6.4 WhitespaceNormalizer

空白とインデントを正規化します。

```csharp
using TsqlRefine.Formatting.Helpers;

var normalized = WhitespaceNormalizer.Normalize(sql, options);
```

**処理内容**:
- 改行の正規化 (`\r\n` → `\n`)
- インデントの再構築 (タブ/スペース)
- 末尾空白の除去
- 最終改行の挿入

### 6.5 InlineSpaceNormalizer

行内のスペースを正規化します。

```csharp
using TsqlRefine.Formatting.Helpers;

var normalized = InlineSpaceNormalizer.Normalize(sql, options);
```

**処理内容**:
- コンマ後にスペースを追加
- 重複スペースを1つに削減
- 先頭インデントは保持
- 保護領域内は変更しない

### 6.6 CommaStyleTransformer

コンマスタイルを変換します。

```csharp
using TsqlRefine.Formatting.Helpers;

var leading = CommaStyleTransformer.ToLeadingCommas(sql);
```

**制限事項**:
- 行ベースの単純な実装
- 文字列/コメント内のコンマを検出しない
- 複雑なネスト構造には対応しない

### 6.7 CasingHelpers

大文字小文字変換のユーティリティ。

```csharp
using TsqlRefine.Formatting.Helpers;

var upper = CasingHelpers.ApplyCasing("select", ElementCasing.Upper);
// 結果: "SELECT"
```

### 6.8 ProtectedRegionTracker

文字列、コメント、ブラケット内の状態を追跡する内部クラス。

```csharp
var tracker = new ProtectedRegionTracker();
if (tracker.IsInProtectedRegion())
{
    // 保護領域内
}
```

---

## 7. CLI 使用方法

### 7.1 基本的なフォーマット

```powershell
# 標準出力にフォーマット結果を出力
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# ファイルを上書き
dotnet run --project src/TsqlRefine.Cli -c Release -- format --write file.sql

# 標準入力から
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin
```

### 7.2 オプション指定

```powershell
# インデントスタイル指定
dotnet run --project src/TsqlRefine.Cli -c Release -- format --indent-style tabs file.sql

# インデントサイズ指定
dotnet run --project src/TsqlRefine.Cli -c Release -- format --indent-size 2 file.sql

# 複合指定
dotnet run --project src/TsqlRefine.Cli -c Release -- format \
    --indent-style spaces \
    --indent-size 4 \
    --write \
    file.sql
```

---

## 8. EditorConfig サポート

`.editorconfig` の設定を自動的に読み込みます:

```ini
[*.sql]
indent_style = spaces  # または tabs
indent_size = 4        # スペース数
```

**優先順位** (高い順):
1. CLI 引数
2. `.editorconfig`
3. `tsqlrefine.json`
4. `FormattingOptions` 既定値

---

## 9. 制限事項

### 9.1 フォーマット対象

**対象**:
- キーワードの大文字小文字
- 識別子の大文字小文字
- インデント (スペース/タブ)
- 改行の正規化
- 末尾空白の除去
- コンマ配置 (基本的なケース)

**非対象**:
- クエリレイアウトの再フォーマット
- 句の並べ替え
- 式の構造変更
- 改行の追加/削除 (正規化以外)
- 式内のスペース追加/削除

### 9.2 MaxLineLength

現在未実装。トークン認識の行分割が必要なため、将来的な実装予定。

### 9.3 CommaStyleTransformer

- 行ベースの単純な実装
- サブクエリ、CTE などの複雑なケースには対応しない
- AST ベースの実装が将来的に必要

---

## 10. パフォーマンス

- **パース**: Microsoft ScriptDom 使用
- **メモリ**: シングルパス、StringBuilder ベース (最小限のアロケーション)
- **速度**: 一般的なクエリ (<1KB) で ~0.5-2ms、大規模ファイル (>10KB) で ~10-50ms
- **スケーラビリティ**: ファイルサイズに対して線形

---

## 11. アーキテクチャ

### 11.1 プロジェクト構造

```
src/TsqlRefine.Formatting/
├── SqlFormatter.cs              # オーケストレータ (26行)
├── FormattingOptions.cs         # オプション定義
├── TsqlRefine.Formatting.csproj
├── README.md
└── Helpers/
    ├── CasingHelpers.cs         # ケーシングユーティリティ
    ├── ScriptDomElementCaser.cs # 要素別ケーシング
    ├── SqlElementCategorizer.cs # トークン分類
    ├── WhitespaceNormalizer.cs  # 空白正規化
    ├── InlineSpaceNormalizer.cs # インラインスペース正規化
    ├── CommaStyleTransformer.cs # コンマスタイル変換
    └── ProtectedRegionTracker.cs # 保護領域追跡 (内部)
```

### 11.2 依存関係

```
TsqlRefine.Formatting
    └── Microsoft.SqlServer.TransactSql.ScriptDom
```

---

## 12. 拡張方法

### 12.1 新しいフォーマットパスの追加

1. `Helpers/` ディレクトリに新しいヘルパークラスを作成
2. パターンに従う:
   ```csharp
   public static class MyFormattingHelper
   {
       public static string Transform(string input, FormattingOptions options)
       {
           // 実装
       }
   }
   ```
3. `SqlFormatter.Format()` パイプラインに追加
4. `tests/TsqlRefine.Formatting.Tests/Helpers/` にテストを追加

### 12.2 ガイドライン

- **パブリック静的クラス**: 独立してテスト可能、プラグインからアクセス可能
- **単一責任**: 1ヘルパー = 1変換
- **保護領域の尊重**: 必要に応じて `ProtectedRegionTracker` を使用
- **XMLドキュメント**: 制限事項を記載
- **エラー処理**: グレースフルデグラデーション、パースエラーで例外を投げない

---

## 13. 参照

- [CLI 仕様](cli.md) - format コマンドの使用方法
- [設定](configuration.md) - 設定ファイルの書式
- [要素別ケーシング](granular-casing.md) - 詳細なケーシング設定
- [プラグイン API](plugin-api.md) - プラグインからの使用方法
