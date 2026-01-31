# CLI 仕様（入出力 / JSON / 終了コード）

このドキュメントは `tsqlrefine` の CLI 仕様を定義します。

---

## 1. コマンド体系

基本形:

```
tsqlrefine <command> [options] [paths...]
```

- `<command>` を省略した場合は `lint` とみなす
- `paths...` は `file.sql` / `dir` の混在可
- 標準入力は `-` または `--stdin` で受ける（純SQLのみ）

コマンド:

| コマンド | 説明 |
|----------|------|
| `lint` | ルールベースの診断（静的解析）**[default]** |
| `format` | 最小限整形のみ（出力はSQLテキスト） |
| `fix` | 診断 + "安全な範囲" の自動修正 |
| `init` | 既定設定ファイルを生成 |
| `print-config` | 有効な設定をJSON形式で出力 |
| `list-rules` | 利用可能ルール（ロード済）を一覧 |
| `list-plugins` | ロード済プラグイン一覧（Rule プラグインのみ） |

---

## 2. オプション

### 2.1 グローバルオプション

全コマンドで使用可能:

| オプション | 説明 |
|------------|------|
| `-c, --config <path>` | 設定ファイルのパス（既定探索を上書き） |
| `-h, --help` | ヘルプ表示 |
| `-v, --version` | バージョン番号を表示 |

### 2.2 コマンド別オプション

各コマンドで使用可能なオプションは以下の通りです:

#### lint

```
tsqlrefine lint [options] [paths...]
```

| オプション | 説明 |
|------------|------|
| `-g, --ignorelist <path>` | 無視パターンファイル |
| `--detect-encoding` | 入力の文字コードを自動判定 |
| `--stdin` | 標準入力から読み取り |
| `--stdin-filepath <path>` | stdin の仮想パス |
| `--output <text\|json>` | 出力形式（既定: `text`） |
| `--compat-level <100-160>` | SQL Server 互換レベル |
| `--severity <error\|warning\|info\|hint>` | 最低重要度フィルタ |
| `--preset <name>` | プリセット選択 |
| `--ruleset <path>` | カスタムルールセットファイル |

#### format

```
tsqlrefine format [options] [paths...]
```

| オプション | 説明 |
|------------|------|
| `-g, --ignorelist <path>` | 無視パターンファイル |
| `--detect-encoding` | 入力の文字コードを自動判定 |
| `--stdin` | 標準入力から読み取り |
| `--stdin-filepath <path>` | stdin の仮想パス |
| `--output <text\|json>` | 出力形式（既定: `text`） |
| `--compat-level <100-160>` | SQL Server 互換レベル |
| `--write` | ファイルを上書き |
| `--diff` | 差分を表示（`--write` と排他） |
| `--indent-style <tabs\|spaces>` | インデントの種類 |
| `--indent-size <n>` | インデント幅 |

#### fix

```
tsqlrefine fix [options] [paths...]
```

| オプション | 説明 |
|------------|------|
| `-g, --ignorelist <path>` | 無視パターンファイル |
| `--detect-encoding` | 入力の文字コードを自動判定 |
| `--stdin` | 標準入力から読み取り |
| `--stdin-filepath <path>` | stdin の仮想パス |
| `--output <text\|json>` | 出力形式（既定: `text`） |
| `--compat-level <100-160>` | SQL Server 互換レベル |
| `--severity <error\|warning\|info\|hint>` | 最低重要度フィルタ |
| `--preset <name>` | プリセット選択 |
| `--ruleset <path>` | カスタムルールセットファイル |
| `--write` | ファイルを上書き |
| `--diff` | 差分を表示（`--write` と排他） |

#### init

```
tsqlrefine init
```

オプションなし。`tsqlrefine.json` と `tsqlrefine.ignore` を生成します。

#### print-config

```
tsqlrefine print-config [options]
```

| オプション | 説明 |
|------------|------|
| `--output <text\|json>` | 出力形式（既定: `text`） |

#### list-rules

```
tsqlrefine list-rules [options]
```

| オプション | 説明 |
|------------|------|
| `--output <text\|json>` | 出力形式（既定: `text`） |

#### list-plugins

```
tsqlrefine list-plugins [options]
```

| オプション | 説明 |
|------------|------|
| `--output <text\|json>` | 出力形式（既定: `text`） |
| `--verbose` | 詳細情報を表示 |

### 2.3 オプション詳細

#### プリセット (`--preset`)

| プリセット | 説明 |
|------------|------|
| `recommended` | バランスの取れた本番向け（既定） |
| `strict` | 最大限の検査 |
| `pragmatic` | 本番最小限（バグ・データ損失防止） |
| `security-only` | セキュリティと重要な安全性のみ |

#### 文字コード検出 (`--detect-encoding`)

- BOM 優先。BOM が無い場合は `UTF.Unknown` による推定
- `format --write` / `fix --write` では入力ファイルの文字コード（BOM 有無含む）を保持

#### フォーマット

- `.editorconfig` の `indent_style` / `indent_size` を参照（`*.sql` のみ）
- `--indent-style` / `--indent-size` は `.editorconfig` より優先
- stdin の場合は `--stdin-filepath` で拡張子と探索起点を指定可能
- 不変領域: **コメント / 文字列 / 括弧内改行は変更しない**

#### fix コマンド

- `--write`/`--diff` が未指定のときは stdout に修正後 SQL を出力
- 複数入力の場合は `--write` または `--diff` が必要
- `--output json` 指定時は診断結果のみを出力（`--write` と組み合わせ可能）

---

## 3. JSON 出力仕様（Diagnostics）

VSCode の `Diagnostic` 互換形を基本とし、ファイル単位で束ねて出力します。

### 3.1 トップレベル

```ts
interface LintResult {
  tool: "tsqlrefine";
  version: string;
  command: "lint" | "format" | "fix";
  files: FileResult[];
}

interface FileResult {
  filePath: string;              // stdin の場合は --stdin-filepath または "<stdin>"
  diagnostics: Diagnostic[];
}
```

### 3.2 Diagnostic（0-based）

```ts
interface Diagnostic {
  range: Range;
  severity?: DiagnosticSeverity; // 既定はルールの defaultSeverity
  code?: number | string;        // ルールID（例: "semantic/undefined-alias"）
  source?: string;               // "tsqlrefine"
  message: string;
  tags?: DiagnosticTag[];
  data?: {
    ruleId?: string;
    category?: string;
    fixable?: boolean;
  };
}

interface Range {
  start: Position;
  end: Position;
}

interface Position {
  line: number;      // 0-based
  character: number; // 0-based
}

enum DiagnosticSeverity {
  Error = 1,
  Warning = 2,
  Information = 3,
  Hint = 4
}

enum DiagnosticTag {
  Unnecessary = 1,
  Deprecated = 2
}
```

---

## 4. 終了コード

CI で扱いやすいように、結果種別で終了コードを固定します。

- `0`: 成功（診断 0 件、または `--severity` フィルタ後に 0 件）
- `1`: ルール違反あり（診断あり）
- `2`: 解析エラー（パース不能、`GO` 分割失敗など）
- `3`: 設定エラー（config/ignore の読み込み不備、無効な互換レベル等）
- `4`: 実行時例外（内部エラー）

`format`/`fix` で `--write` の場合も、上記ルールに従う（修正できても違反が残るなら `1`）。
