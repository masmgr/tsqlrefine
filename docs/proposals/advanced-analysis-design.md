# 高度静的解析機能 設計案

> **Status**: Active Proposal — Phase 1 implemented (2026-07-18)
> 複雑な T-SQL ソースコード(ストアドプロシージャ / ビュー / 関数)の品質向上を目的とした
> 新機能群の設計案。本ドキュメントは提案段階であり、実装時に詳細が変わる可能性がある。

## 目次

- [背景と現状](#背景と現状)
- [提案 1: 導入・運用基盤(ベースライン / SARIF / 差分 lint)](#提案-1-導入運用基盤)
- [提案 2: オブジェクトカタログとクロスオブジェクト解析](#提案-2-オブジェクトカタログとクロスオブジェクト解析)
- [提案 3: 制御フロー解析基盤(CFG)とフロー系ルール](#提案-3-制御フロー解析基盤cfgとフロー系ルール)
- [提案 4: テイント解析による動的 SQL 検証の強化](#提案-4-テイント解析による動的-sql-検証の強化)
- [提案 5: メトリクスと report コマンド](#提案-5-メトリクスと-report-コマンド)
- [実装フェーズ](#実装フェーズ)
- [互換性への配慮](#互換性への配慮)

## 背景と現状

tsqlrefine は現在 140 を超えるルール(単一ファイル・AST ベース)を持ち、スキーマスナップショット
(`schema.json`)と JOIN 関係プロファイル(`relations.json`)を `ISchemaContext` 経由で
ルールに供給する仕組みを備えている。

一方、複雑なコードベースで実際に問題を生む領域のうち、以下は未カバーである。

| ギャップ | 例 |
|----------|-----|
| オブジェクト横断の不整合 | EXEC のパラメータ不一致、存在しないプロシージャ呼び出し |
| パス感度のあるフロー解析 | 特定分岐でのみ COMMIT されないトランザクション |
| 変数経由の動的 SQL 追跡 | `SET @sql = @sql + @input` を経由するインジェクション |
| コードベース全体の俯瞰 | 複雑度の高いオブジェクトの特定、影響範囲調査 |
| レガシー環境への段階導入 | 既存違反数千件がある環境で新規違反のみ検出したい |

本設計はいずれも既存アーキテクチャの延長線上にある:

- **事前収集 → JSON → lint 時参照** — `schema collect-relations` と同じパターンを
  オブジェクトカタログに横展開する(提案 2)
- **任意プロバイダーの合成** — スキーマ、関係プロファイル、オブジェクトカタログは
  独立してロードでき、`RuleContext` から型付きで参照する
- **AST ベースのルール実装** — CFG / テイントは AST 上に構築する共有ヘルパーとする

## 提案 1: 導入・運用基盤

### 1.1 ベースライン

**目的**: 既存違反を「凍結」し、新規違反のみをエラーにする。レガシーコードベースへの
段階導入の前提機能。

#### CLI

```powershell
# ベースライン作成(現在の全違反を記録)
tsqlrefine baseline create --output .tsqlrefine/baseline.json [paths...]

# ベースラインを適用して lint(既知の違反は suppressed 扱い)
tsqlrefine lint --baseline .tsqlrefine/baseline.json [paths...]

# ベースラインに残っている違反のうち、解消済みのものを掃除
tsqlrefine baseline trim --baseline .tsqlrefine/baseline.json [paths...]
```

`tsqlrefine.json` にも設定可能とする:

```json
{
  "baseline": ".tsqlrefine/baseline.json"
}
```

#### フィンガープリント設計

行番号は編集で容易にずれるため、行番号非依存の指紋を採用する。

```
fingerprint = SHA-256(LengthPrefixedUtf8(
    fingerprintVersion
  + ruleId
  + normalizedFilePath
  + normalizedDiagnosticText
  + normalizedLeadingContext
  + normalizedTrailingContext
))
```

- `normalizedFilePath` は明示した `--root`、Git worktree root、共通入力ディレクトリの
  優先順で決めたルートからの相対パスとし、`/` 区切りにする。Git 管理下では index 上の
  path casing を正とし、非 Git 入力では casing を保持する
- `normalizedDiagnosticText` は診断 Range が覆う複数行テキストを改行 LF、行末空白除去で
  正規化する。ゼロ幅 Range では診断開始行の非空白テキストを使う
- 前後コンテキストは同一 AST スコープ内の直前・直後の非空白行を使う。これにより行移動に
  耐えつつ、同一テキストの繰り返しを可能な範囲で区別する
- ハッシュ入力は単純連結せず、各フィールドを UTF-8 バイト長付きでエンコードする
- 同一 fingerprint が複数存在する場合、baseline entry と現在の診断を **multiset** として
  1 件ずつ消費して照合する。ファイル内の出現順序は指紋そのものには含めない
- 行や診断対象の内容自体が変わった場合は「新規違反」として再検出される

Git 管理外や複数ルートの入力では自動ルート推定が不安定になり得るため、baseline 作成時の
ルートをメタデータに保存し、適用時に異なるルートが解決された場合は設定エラーとする。

#### baseline.json スキーマ

```json
{
  "version": 1,
  "fingerprintVersion": 1,
  "generatedAt": "2026-07-18T00:00:00Z",
  "toolVersion": "x.y.z",
  "root": "..",
  "entries": [
    { "fingerprint": "sha256hex", "ruleId": "avoid-select-star", "file": "procs/foo.sql" }
  ]
}
```

`root` は baseline.json の親ディレクトリから解析ルートへの相対パスとして保存する。
`ruleId` / `file` は人間によるレビューとデバッグのための冗長情報であり、照合には
`fingerprint` とその件数のみを用いる。

#### 実装ポイント

- 実装場所: `TsqlRefine.Cli/Services/BaselineStore.cs`(読み書き)+
  `CommandExecutor` での分類。**Core / PluginSdk の診断モデルには手を入れない**
  (エンジンは全違反を返し、CLI 層で active / suppressed に分類する)
- CLI 内部に `OutputDiagnostic(Diagnostic Diagnostic, bool Suppressed, string Fingerprint)` と
  `LintOutputResult` を設ける。JSON/SARIF writer はこの CLI 専用 DTO を受け取り、既存の
  Core `LintResult` や PluginSdk `Diagnostic` は変更しない
- 通常の JSON 出力は active のみを従来形で出力する。`--show-suppressed` 時は診断要素に
  `"suppressed": true|false` と `"fingerprint"` を追加する
- Exit code は suppressed を除いた違反数で決定(既存の `ExitCodes.cs` 契約は不変)
- `parse-error` / `parser-exception` は baseline の作成・照合対象にせず、常に表示して
  `ExitCodes.AnalysisError` を返す
- `baseline trim` は今回解析したファイルの entry だけを更新し、入力対象外ファイルの entry は
  保持する。ファイル削除分も掃除する場合だけ `--remove-missing` を明示指定させる

### 1.2 SARIF 出力

**目的**: GitHub Code Scanning / Azure DevOps への直接連携。

- `--output sarif` を追加(既存: `text` / `json`)。SARIF 2.1.0 準拠
- Severity マッピング: `Error`→`error`, `Warning`→`warning`, `Information`/`Hint`→`note`
- `partialFingerprints` にベースラインの指紋を流用(1.1 と同じ計算)
- ルールメタデータ(`RuleMetadata.Description` / `DocumentationUri`)を
  `tool.driver.rules` に展開
- artifact URI は解析ルート相対の `/` 区切り URI とし、SARIF region は内部の 0-based Range
  から SARIF 規定の 1-based line / column へ writer 内で変換する。終端の exclusive semantics を
  保ち、ゼロ幅診断も有効な region として表現する
- `runs[].artifacts`、rule index、result ruleId、`$schema`、`version` を必須出力とし、
  Microsoft SARIF 2.1.0 JSON Schema で検証する
- 実装場所: `TsqlRefine.Cli/Services/OutputWriter.cs` に `SarifWriter` を追加

### 1.3 差分 lint

**目的**: PR チェックで「変更した行に関わる違反」のみを報告する。

```powershell
tsqlrefine lint --changed-only [--base-ref origin/main] [paths...]
```

- Git の起動は shell 文字列連結ではなく引数リスト付き `ProcessStartInfo` を使う
- `git diff --unified=0 <base-ref>...HEAD` と working tree / index の差分を統合し、rename と
  削除を正規化する。未追跡ファイルは入力対象に含まれる場合、その全行を変更行とみなす
- diff の出力から変更行レンジを取得し、違反の
  `Range` と交差するもののみ報告
- git が使えない環境向けに `--changed-lines-from <file>`(JSON 指定)も用意
- 「変更した行に関わる」は診断 Range との交差に限定する。関連箇所や文全体への拡張は初版の
  対象外とし、将来 `--changed-scope statement` として追加を検討する
- parse error / parser exception は変更行フィルターを適用せず、常に報告する
- 実装場所: CLI 層のみ。`GitDiffReader.cs` を `Services/` に追加

## 提案 2: オブジェクトカタログとクロスオブジェクト解析

### 2.1 概要

全 SQL ファイルからプロシージャ / 関数 / ビューの**定義情報(シグネチャ)**と
**参照情報(呼び出し)**を事前収集し、`objects.json` として保存。lint 時に
`RuleContext.ObjectCatalog` 経由でルールに供給する。カタログは `schema.json` と独立して
ロード可能とし、`schema collect-relations` → `relations.json` と同様の事前収集パターンは
踏襲するが、`ISchemaContext` には結合しない。

```
┌─ 収集フェーズ ─────────────────────────────┐   ┌─ 解析フェーズ ──────────────┐
│ *.sql → ObjectCatalogCollector             │   │ lint 時:                    │
│   ├─ 定義: CREATE PROC/FUNC/VIEW           │   │  RuleContext.SchemaContext  │
│   │   → パラメータ名/型/OUTPUT/デフォルト  │──▶│  RuleContext.ObjectCatalog │
│   └─ 参照: EXEC / 関数呼び出し / FROM句    │   │  を各ルールが参照           │
│ → objects.json                             │   └─────────────────────────────┘
└────────────────────────────────────────────┘
```

### 2.2 CLI

```powershell
# 収集(単独)
tsqlrefine schema collect-objects --output objects.json **/*.sql

# schema build に統合(schema.json + relations.json + objects.json を一括生成)
tsqlrefine schema build --connection-string "..." --output-dir .tsqlrefine/schema **/*.sql
```

`SchemaConfig.Path`(ディレクトリ指定)は `objects.json` も自動導出対象に加える。ただし
`SnapshotPath` が未設定でも `ObjectsCatalogPath` または `Path/objects.json` が存在すれば
カタログのみをロードできるよう、`LoadSchemaContext` とは独立した
`LoadObjectCatalog` を設ける。

```json
{
  "schema": {
    "path": ".tsqlrefine/schema",
    "objectsCatalogPath": ".tsqlrefine/schema/objects.json"
  }
}
```

### 2.3 データモデル

新規プロジェクトは作らず `TsqlRefine.Schema` に `Catalog/` サブディレクトリを追加する。

```csharp
// TsqlRefine.Schema/Catalog/CatalogModels.cs
public enum SqlObjectKind { Procedure, ScalarFunction, TableValuedFunction, View }

public sealed record CatalogObjectId(
    string? DatabaseName,    // null は現在 DB
    string SchemaName,
    string Name);

public sealed record CatalogParameter(
    string Name,             // "@UserId"
    string TypeName,         // "int", "nvarchar(50)" — SchemaTypeInfo に正規化して保持
    SchemaTypeInfo Type,
    bool IsOutput,
    bool HasDefault);

public sealed record CatalogObject(
    CatalogObjectId Id,
    SqlObjectKind Kind,
    IReadOnlyList<CatalogParameter> Parameters,
    IReadOnlyList<SchemaColumnInfo>? ResultColumns,  // ビュー/TVF のみ。プロシージャは null
    string DefinedInFile,
    TsqlRefine.PluginSdk.Range DefinedAt);

public sealed record CatalogReference(
    CatalogObjectId? FromObject,             // null はスクリプト直下
    CatalogObjectId ToObject,
    string? ToColumn,
    CatalogReferenceKind Kind,               // Exec / FunctionCall / Table / Column
    CatalogResolutionStatus Resolution,      // Resolved / Unresolved / Ambiguous / OutOfScope
    string ReferencedInFile,
    TsqlRefine.PluginSdk.Range ReferencedAt,
    bool IsDynamic);

public sealed record CatalogScope(
    IReadOnlyList<string> Databases,
    bool IsAuthoritative,                    // 収集範囲内の未解決を診断してよいか
    bool IncludesExternalReferences);

public sealed record ObjectCatalog(
    int Version,
    CatalogScope Scope,
    IReadOnlyList<CatalogObject> Objects,
    IReadOnlyList<CatalogReference> References);
```

識別子の比較は SQL Server の照合順序を完全には再現できないため、初版は
`OrdinalIgnoreCase` とし、その制約を metadata に記録する。3 部名は database を保持し、
4 部名、synonym、linked server、動的に組み立てた名前は `OutOfScope` または `IsDynamic` として
記録する。省略された schema は `SchemaConfig.DefaultSchema` で補完する。列参照を保持することで
`analyze impact --column` を実装可能にする。

### 2.4 PluginSdk 契約(additive)

`ISchemaContext` は既存のまま維持し、`RuleContext` に default null のカタログ引数と
ショートハンドを追加する。これにより `schema.json` なしでカタログを利用でき、既存の
スキーマルールが空のスキーマを「ロード済み」と誤認することもない。公開 API 追加のため
`PluginApi.CurrentVersion` はバンプする。

```csharp
// TsqlRefine.PluginSdk/SchemaContracts.cs に追加
public interface IObjectCatalogProvider
{
    bool HasData { get; }
    CatalogScopeInfo Scope { get; }
    CatalogObjectInfo? ResolveObject(
        string? database, string? schema, string name, SqlObjectKindFilter kind);
    IReadOnlyList<CatalogReferenceInfo> GetReferencesTo(
        string? database, string schema, string name, string? column = null);
    IReadOnlyList<CatalogObjectInfo> GetAllObjects();
}

public sealed record RuleContext(
    string FilePath,
    int CompatLevel,
    ScriptDomAst Ast,
    IReadOnlyList<Token> Tokens,
    RuleSettings Settings,
    ISchemaContext? SchemaContext = null,
    IObjectCatalogProvider? ObjectCatalog = null);
```

DTO(`CatalogObjectInfo` / `CatalogReferenceInfo` / `CatalogScopeInfo` 等)は既存の Relation 系
DTO と同様に PluginSdk 側へ
ミラーし、`TsqlRefine.Schema/Resolution/DtoMappers.cs` で変換する。
ルックアップは database / schema / name / kind を正規化した複合キーの
`FrozenDictionary`(OrdinalIgnoreCase)で構築する。

### 2.5 新規ルール(このカタログの上に構築)

| Rule ID | 検出内容 | Severity | Tier |
|---------|----------|----------|------|
| `exec-parameter-count-mismatch` | EXEC の位置指定引数がパラメータ数と不一致(デフォルト値考慮) | Error | Essential |
| `exec-parameter-name-mismatch` | 名前指定引数(`@p = ...`)が定義に存在しない | Error | Essential |
| `exec-parameter-type-mismatch` | 引数の型がパラメータ型に暗黙変換不能/精度喪失(`TypeCompatibility.CheckComparison` を再利用) | Warning | Recommended |
| `exec-output-not-captured` | OUTPUT パラメータへの `OUTPUT` キーワード指定漏れ | Warning | Recommended |
| `unresolved-procedure-reference` | authoritative なカタログ範囲内に存在しないプロシージャ/関数の呼び出し | Warning | Thorough |
| `unreferenced-object` | どこからも参照されないオブジェクト(エントリポイント指定は設定で除外) | Information | Thorough |
| `deep-view-nesting` | ビューのネスト深度がしきい値(デフォルト 3)超過 | Warning | Thorough |
| `circular-object-reference` | オブジェクト間の循環参照 | Warning | Thorough |

いずれも `context.ObjectCatalog` が null / `HasData == false` の場合は何も報告しない。
`unresolved-procedure-reference` は `Scope.IsAuthoritative == true` かつ参照先が対象 DB 内の
場合だけ報告する。組み込み関数、system object、4 部名、synonym、linked server、動的参照、
収集対象外 DB は `OutOfScope` として報告対象外にする。

型検証には `ImplicitConversionRules` の `internal` メソッドを直接使わず、公開済みの
`TypeCompatibility.CheckComparison` を再利用する。引数式の型が確定できない場合は
`exec-parameter-type-mismatch` を報告しない。

### 2.6 影響分析コマンド

依存グラフを使った読み取り専用の調査コマンドを追加する。

```powershell
# このテーブル/カラムを参照する全オブジェクトの列挙(スキーマ変更前の影響調査)
tsqlrefine analyze impact --table dbo.Users [--column Email] --catalog objects.json

# 依存グラフのエクスポート(可視化ツール連携)
tsqlrefine analyze graph --catalog objects.json --output deps.json [--format json|dot]
```

### 2.7 テスト戦略

- `ObjectCatalogCollector` の単体テスト: パラメータ抽出(型 / OUTPUT / デフォルト)、
  1〜3 部名の正規化、列参照、GO バッチ跨ぎ、`ALTER PROCEDURE`、synonym / linked server /
  動的参照の `OutOfScope` 判定、authoritative / partial scope の扱い
- ルールテストは既存の `RuleTestContext.CreateContext()` に
  `IObjectCatalogProvider` を直接渡せるオーバーロードを追加
- サンプル: `samples/sql/` に呼び出し元 / 呼び出し先のペアを追加

## 提案 3: 制御フロー解析基盤(CFG)とフロー系ルール

### 3.1 概要

ScriptDOM の AST からバッチ / プロシージャ単位の制御フローグラフ(CFG)を構築する
共有ヘルパーを `TsqlRefine.Rules/Helpers/ControlFlow/` に追加し、その上にパス感度の
あるルール群を実装する。

> **配置の判断**: PluginSdk は zero-dependency の契約層なので CFG は置かない。
> Core はエンジン(オーケストレーション)なのでルール用解析は置かない。
> まず Rules の Helpers に置き、プラグインからの需要が出た時点で昇格を検討する。

### 3.2 CFG モデル

```csharp
// Helpers/ControlFlow/ControlFlowGraph.cs
public sealed class CfgNode
{
    public int Id { get; }
    public CfgNodeKind Kind { get; }             // Entry / Statement / Join / Exit
    public TSqlStatement? Statement { get; }     // synthetic node では null
    public IReadOnlyList<CfgEdge> Successors { get; }
    public IReadOnlyList<CfgEdge> Predecessors { get; }
}

public enum CfgEdgeKind
{
    Sequential, TrueBranch, FalseBranch, LoopBack, Exception,
    Return, Break, Continue
}

public sealed class ControlFlowGraph
{
    public CfgNode Entry { get; }
    public CfgNode Exit { get; }
    public IReadOnlyList<CfgNode> Nodes { get; }
}

public static class ControlFlowGraphBuilder
{
    public static CfgBuildResult Build(TSqlBatch batch);
    public static CfgBuildResult Build(StatementList body);
}
```

初版で対応するステートメント: `IF/ELSE`, `WHILE`, `BEGIN...END`, `RETURN`, `THROW`,
`RAISERROR`, `TRY...CATCH`,
`BREAK/CONTINUE`。`GOTO/ラベル` は認識するが、`GOTO` を含むケースは初版では
`CfgBuildResult.UnsupportedReasons` に記録し、当該スコープのフロー系ルールは報告しない
(偽陽性回避を優先)。

TRY 内では `THROW` と severity が静的に 11 以上と分かる `RAISERROR` に明示的な
Exception edge を張る。通常の SQL 文も実行時エラーで CATCH に遷移し得るため、初版では
副作用やエラー可能性を完全にはモデル化せず、TRY 内の一般文から CATCH への
`Exception(Conservative)` edge を張る。severity が式で不明な `RAISERROR` も同様に
保守的な Exception edge とする。

その上に汎用のデータフロー枠組みを 1 つだけ用意する:

```csharp
// Helpers/ControlFlow/DataFlowAnalysis.cs — 前向き may/must 解析の共通実装
public abstract class ForwardDataFlowAnalysis<TState>
{
    protected abstract TState InitialState();
    protected abstract TState Transfer(TState input, CfgNode node);
    protected abstract TState Merge(TState a, TState b);   // join
    protected abstract bool StateEquals(TState a, TState b);
    public IReadOnlyDictionary<CfgNode, TState> Solve(ControlFlowGraph cfg);  // 不動点反復
}
```

実装は worklist 法を使い、ループを含む有限 lattice で収束することを各解析に要求する。
Entry / Exit / Join は synthetic node とし、特定の `TSqlStatement` を持たせない。

### 3.3 新規ルール

| Rule ID | 解析 | 検出内容 | Severity | Tier |
|---------|------|----------|----------|------|
| `transaction-not-closed-on-path` | 可能状態集合の前向き解析 | ある実行パスでこのスコープが開始した TRAN が COMMIT/ROLLBACK されずに RETURN/終端へ到達 | Error | Essential |
| `cursor-not-deallocated-on-path` | must 解析(カーソル状態) | OPEN されたカーソルが CLOSE/DEALLOCATE されないパス(CATCH 経路含む) | Warning | Recommended |
| `variable-used-before-assignment` | must 解析(確定代入) | DECLARE 後、全パスで代入される前に読まれる変数(DECLARE 時初期化なし) | Warning | Recommended |
| `unused-variable` | 参照カウント | 宣言後一度も読まれない変数 / パラメータ | Information | Thorough |
| `unreachable-statement` | 到達可能性 | RETURN/THROW 後や常に偽の分岐内の到達不能文 | Warning | Recommended |
| `inconsistent-result-set` | 分岐ごとの SELECT 形状比較 | 実行パスによって返す結果セットの列数 / 列名が変わるプロシージャ | Warning | Thorough |

既存の `avoid-transaction-without-commit` / `uncommitted-transaction` はトークン /
単純 AST ベースの近似検出として残し、CFG 版はより精密な別ルールとして導入する
(将来、CFG 版が安定したら旧ルールの deprecation を検討)。

### 3.4 偽陽性の抑制方針

- トランザクション状態は絶対的な `@@TRANCOUNT` ではなく、スコープ入口からの相対状態
  `NotStarted / Open(depth range) / Closed / Uncommittable / Unknown` の集合として保持する
- `COMMIT` は深度を 1 減らし、通常の `ROLLBACK TRANSACTION` は全トランザクションを
  終了する。savepoint への ROLLBACK は深度を変えない
- `XACT_STATE()`、`@@TRANCOUNT`、`SET XACT_ABORT ON` を認識して transfer / branch refinement
  に使うが、それらを参照したという理由だけでは診断を抑制しない
- 動的 SQL(`EXEC(@sql)`)や副作用不明のプロシージャ呼び出しは状態を `Unknown` にする。
  スコープ全体を無条件に抑制せず、Unknown を通らずに未終了へ到達するパスがあれば報告する
- 呼び出し元が開始したトランザクションは当該スコープの責任範囲外とし、このスコープ内の
  `BEGIN TRAN` に対応する相対深度だけを検査する
- 呼び出し先の transaction summary をカタログに持たせる interprocedural 解析は初版に
  含めず、別 Phase とする。単なるシグネチャカタログから「呼び出し先が閉じる」とは推定しない

### 3.5 テスト戦略

- CFG 構築自体の単体テスト(`ControlFlowGraphTests`): 各構文の Successor 形状を検証
- ルールテストは通常どおり(違反 SQL / 非違反 SQL のペア)。TRY-CATCH ×
  トランザクションの組み合わせ表に、nested BEGIN/COMMIT、全体 ROLLBACK、savepoint、
  XACT_STATE、XACT_ABORT、呼び出し元トランザクション、動的 SQL、Unknown path を含める

## 提案 4: テイント解析による動的 SQL 検証の強化

### 4.1 概要

現行の `avoid-exec-dynamic-sql` / `require-parameterized-sp-executesql` は
EXEC 箇所の構文パターンのみを見るため、**変数を経由した連結**を追えない:

```sql
DECLARE @sql nvarchar(max) = N'SELECT * FROM Users WHERE Name = ''';
SET @sql = @sql + @userName + N'''';   -- 汚染がここで混入
EXEC sp_executesql @sql;               -- 現行ルールでは検出漏れ(@sql は単独変数)
```

提案 3 の `ForwardDataFlowAnalysis` を使い、「汚染源 → 変数代入の連鎖 → シンク」を
追跡する `dynamic-sql-taint` ルールを新設する。

### 4.2 解析設計

単一の tainted / untainted では escaping の用途を判定できないため、変数ごとに次の
有限 lattice を保持する。

```csharp
public enum SqlTrustKind
{
    Constant,              // 静的リテラルだけから構成
    UntrustedValue,        // 外部値。SQL 文字列へ直接連結してはならない
    EscapedStringLiteral,  // 文字列リテラル内部でのみ使用可能
    QuotedIdentifier,      // 識別子位置でのみ使用可能
    NumericValue,          // 型推論で数値と確定
    SqlFragment,           // 意図的な SQL 断片。安全とはみなさない
    Unknown
}

public sealed record SqlValueState(
    SqlTrustKind Trust,
    IReadOnlyList<SqlSegment>? Segments); // bounded symbolic string。上限超過時は Unknown
```

- **汚染源(source)**: プロシージャ / 関数のパラメータ、`SELECT @var = column` で
  テーブル列から読んだ値(後者は設定で無効化可)
- **伝播(propagation)**: `SET @a = <expr>` / `SELECT @a = <expr>` で代入を追跡し、
  文字列連結(`+`, `CONCAT`)では定数部分と値部分を bounded symbolic segments として保持し、
  trust kind と挿入位置(文字列リテラル / 識別子 / SQL 構造)を合成する。segment 数が設定上限を
  超えた場合や文脈を復元できない場合は `Unknown` に widening して収束させる
- **パラメーター化**: `sp_executesql` の SQL テキストが定数由来で、外部値が宣言済みの
  パラメータ引数として渡される場合は安全とする
- **用途限定 escaping**: `QUOTENAME(x)` は `QuotedIdentifier`、
  `REPLACE(x, '''', '''''')` は引用符で囲まれた文字列リテラル内部に挿入される場合のみ
  `EscapedStringLiteral` とする。いずれも汎用 sanitizer とはみなさない
- **数値変換**: 入力型と変換先が数値型と確定できる `CAST` / `CONVERT` のみ
  `NumericValue` とする。型不明、sql_variant、文字列への再変換は `Unknown` とする
- **制約**: `QUOTENAME` の入力長制限、delimiter、nullability を追跡し、結果が NULL に
  なり得ること自体は注記情報とするが、信頼状態を `Constant` には引き上げない
- **シンク(sink)**: `EXEC(@x)` / `EXEC sp_executesql @x`。最終 SQL テキストに
  `UntrustedValue`、用途不一致の escaped 値、`SqlFragment`、`Unknown` が混入する場合に報告する

解析中の可変状態には変数を整数 ID 化した bitset または通常の `HashSet` / immutable collection
を使う。`FrozenSet` は解析完了後の公開結果やキャッシュにのみ使い、不動点反復の各 transfer で
再構築しない。join は各変数の trust kind の最小上界を取る may 解析とする。

### 4.3 位置づけ

- Rule ID: `dynamic-sql-taint` / Category: Security / Severity: Error / Tier: Critical
- 既存 2 ルールは残す(パターン検出は依然として安価で説明しやすいため)。
  初版ではルール間の実行順序依存やエンジン固有の重複排除を導入せず、同じ EXEC に
  パターン違反と taint 違反が併存し得ることを許容する。将来、診断に共通の
  `FindingFamily` を導入できた場合に表示層での集約を検討する

## 提案 5: メトリクスと report コマンド

### 5.1 メトリクス定義

提案 3 の CFG から安価に算出できるものを対象とする。算出単位は
「オブジェクト(プロシージャ / 関数 / ビュー / トリガー)またはバッチ」。

| メトリクス | 定義 |
|-----------|------|
| Cyclomatic Complexity | CFG の decision point + 1。初版は IF / WHILE / searched CASE WHEN を数え、式中の AND / OR は別指標候補として数えない |
| Nesting Depth | BEGIN-END / IF / WHILE / TRY の最大ネスト深度 |
| Statement Count | 実行文数 |
| JOIN Count | 単一クエリ内の最大 JOIN 数 |
| Parameter Count | パラメータ数 |

### 5.2 しきい値ルール

| Rule ID | デフォルトしきい値 | Severity | Tier |
|---------|-------------------|----------|------|
| `max-cyclomatic-complexity` | 20 | Warning | Thorough |
| `max-nesting-depth` | 5 | Warning | Thorough |
| `max-statement-count` | 200 | Information | Thorough |
| `max-joins-per-query` | 8 | Warning | Thorough |
| `max-parameter-count` | 15 | Information | Thorough |

しきい値は per-rule 設定を要する。JSON や `object?` を PluginSdk の契約へ漏らさず、
型付き読み取り専用インターフェイスを追加する(`PluginApi.CurrentVersion` バンプ対象)。

```csharp
public interface IRuleOptions
{
    bool TryGetBoolean(string name, out bool value);
    bool TryGetInt32(string name, out int value);
    bool TryGetString(string name, out string? value);
}

public sealed record RuleSettings(IRuleOptions? Options = null);

public interface IRuleOptionsDescriptorProvider
{
    IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; }
}
```

設定は既存の severity 文字列形式を維持しつつ、オブジェクト形式を追加する。

```json
{
  "rules": {
    "avoid-select-star": "warning",
    "max-cyclomatic-complexity": {
      "severity": "warning",
      "options": { "max": 20 }
    }
  }
}
```

Core の設定モデルには string / object の両方を読める専用 `RuleConfig` と JSON converter を
設ける。`IReadOnlyDictionary<string, object?>` や `JsonElement` は PluginSdk に公開しない。
エンジンは現状の「ファイルごとに 1 個の RuleContext を全ルールで共有」する実装を変更し、
AST / Token は共有したまま、各ルールの実行時に rule ID に対応する `RuleSettings` を持つ
`RuleContext` を生成する。option を受け取るルールは任意の
`IRuleOptionsDescriptorProvider` を実装し、名前・型・範囲・既定値を登録する。descriptor を
公開しないルールへの options 指定、および未知 option / 型不一致 / 範囲外は設定エラーとする。

### 5.3 report コマンド

```powershell
tsqlrefine report [--output-format json|html] [--output report.html] [paths...]
```

出力内容:

- ルール違反のカテゴリ別 / ルール別 / ファイル別集計
- メトリクス上位オブジェクト(複雑度ランキング)→ リファクタリング優先度付けに使う
- ベースライン適用時: 現時点の新規違反数 / 凍結中違反数 / baseline から解消済みの数
- HTML は自己完結の単一ファイル(CSS/JS インライン)とし、CI 成果物として保存可能にする

初版の report は単一実行時点のスナップショットであり、「推移」は表示しない。時系列表示は
過去の report JSON を複数入力する `report trend --history <dir>` 等の保存契約を別提案で
定義してから追加する。

## 実装フェーズ

依存関係と費用対効果から、次の順序を推奨する。

| Phase | 内容 | 依存 | 規模感 | Status |
|-------|------|------|--------|--------|
| **1** | ベースライン + SARIF 出力(提案 1.1, 1.2) | なし(Core/PluginSdk モデルは不変) | 小〜中 | Implemented |
| **2** | オブジェクトカタログ収集 + EXEC 検証 4 ルール(提案 2.1–2.5 前半) | なし | 中 | Planned |
| **3** | CFG 基盤 + トランザクション / カーソル / 変数ルール(提案 3) | なし | 中〜大 | Planned |
| **4** | テイント解析(提案 4) | Phase 3 の DataFlow 基盤 | 中 | Planned |
| **5** | 型付き per-rule options + メトリクス + report(提案 5) | Phase 3 の CFG | 中 | Planned |
| **6** | 依存グラフ系ルール + analyze impact/graph + 差分 lint(提案 2.5 後半, 2.6, 1.3) | Phase 2 のカタログ、Phase 5 の options | 中 | Planned |

各 Phase は依存を満たした時点で独立してリリース可能。Phase 1 と 2 は並行着手できる。
Phase 2 では既定値だけを使う EXEC 検証を先行し、option を必要とする
`deep-view-nesting` 等は Phase 5 完了後の Phase 6 で導入する。

各 Phase で、実装とテストに加えて次の成果物を同時に更新する。

- CLI コマンド / option: `docs/cli.md`、`README.md`、CLI tests
- 設定: `schemas/tsqlrefine.schema.json`、`docs/configuration.md`、samples
- JSON 出力: 対応する `schemas/*-result.schema.json` と schema validation tests
- 新規ルール: rule tests、`samples/sql/`、`docs/Rules/`、`docs/Rules/REFERENCE.md`、rulesets
- baseline / objects / report: versioned JSON Schema と旧 version の読み込み・エラー tests
- SARIF: SARIF 2.1.0 schema による検証、URI / region / rule index / fingerprint の tests

## 互換性への配慮

プロジェクトの公開契約(`.claude/rules/project-conventions.md` 参照)に対する影響:

1. **Exit codes** — 変更なし。ベースラインは「suppressed を除いた違反数」で既存の
   判定ロジックに載せる
2. **Plugin API** — `RuleContext.ObjectCatalog` / `IObjectCatalogProvider`(提案 2)と
   `IRuleOptions` / `RuleSettings.Options`(提案 5)は additive だが公開 API 追加のため、
   それぞれの Phase で `PluginApi.CurrentVersion` をバンプする
3. **CLI コマンド構造** — 追加のみ(`baseline`, `analyze`, `report` サブコマンド、
   `--baseline` / `--changed-only` / `--output sarif` オプション)。既存コマンドの
   挙動は不変
4. **JSON 出力** — 通常実行の既存フィールドは不変。`suppressed` / `fingerprint` は
   `--show-suppressed` 指定時だけ CLI 専用 DTO から出力する。対応 schema は通常版と
   suppressed 表示版を `oneOf` で表現する
5. **設定 JSON** — `rules.<id>` の既存文字列形式を維持し、object 形式を `oneOf` で追加する。
   既存設定の意味と優先順位は変更しない

パフォーマンス規約(`FrozenDictionary` / `FrozenSet` は構築完了後の読み取り専用 lookup、
`StringBuilder`、計算キャッシュ)は各コンポーネントの実装時に適用する。反復解析中の状態には
bitset や mutable / immutable collection を用途に応じて使い、transfer ごとに Frozen collection
を再構築しない。特にカタログのルックアップと CFG 不動点反復が新たなホットパスになる。
