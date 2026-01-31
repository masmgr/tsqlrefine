# CLI 仕様（入出力 / JSON / 終了コード）

このドキュメントは `tsqlrefine` の CLI 仕様を定義します。

---

## 1. コマンド体系

基本形:

```
tsqlrefine [global options] [paths...]
tsqlrefine <command> [options] [paths...]
```

- `<command>` を省略した場合は `lint` とみなす
- `paths...` は `file.sql` / `dir` の混在可
- 標準入力は `-` または `--stdin` で受ける（純SQLのみ）

コマンド:

- `lint`: ルールベースの診断（静的解析）
- `format`: 最小限整形のみ（出力はSQLテキスト）
- `fix`: 診断 + "安全な範囲" の自動修正
- `init`: 既定設定ファイルを生成
- `print-config`: 探索された設定ファイルのパスを出力
- `list-rules`: 利用可能ルール（ロード済）を一覧
- `list-plugins`: ロード済プラグイン一覧（Rule プラグインのみ）

---

## 2. 主なオプション（tsqllint 互換を意識）

グローバル:

- `-c, --config <path>`: 設定ファイルのパス（既定探索を上書き）
- `-g, --ignorelist <path>`: 無視するファイルのパターンリスト（glob形式、1行1パターン、#でコメント）
- `-v, --version`: バージョン番号を表示
- `-h, --help`: ヘルプ表示

入出力:

- `--stdin`: 標準入力から 1 ファイル相当の SQL を読む（`-` でも可）
- `--stdin-filepath <path>`: stdin の “仮想パス” を指定（診断出力の filePath 用）
- `--output <text|json>`: 出力形式（既定: `text`）
- `--severity <error|warning|info|hint>`: これ未満を出力しない（既定: すべて）
- `--detect-encoding`: 入力の文字コードを自動判定する（BOM 優先。BOM が無い場合は `UTF.Unknown` による推定）
- `--detect-encoding` を指定した場合、`format --write` / `fix --write` の上書きは入力ファイルの文字コード（および BOM 有無）を保持します

実行モード:

- `--preset <recommended|strict|pragmatic|security-only>`: プリセット選択（既定: `recommended`）
- `--compat-level <110|120|130|140|150|160>`: SQL Server 互換レベル（設定ファイルでの指定を上書き）
- `--ruleset <path>`: ルールセット設定を別ファイルで指定（将来）

format / fix:

- `--write`: ファイルを上書き（指定しない場合は stdout に出力）
- `--diff`: 差分を表示（`--write` と排他）
- `--indent-style <tabs|spaces>`: インデントの種類（`.editorconfig` より優先）
- `--indent-size <n>`: spaces の場合の幅（`.editorconfig` より優先）

`format` は `.editorconfig` の `indent_style` / `indent_size` を参照します（対象: `*.sql` のみ）。
stdin の場合は `--stdin-filepath` で拡張子と探索起点を指定できます。

`fix` は `format` と同様に、`--write`/`--diff` が未指定のときは stdout に修正後 SQL を出力します
（複数入力の場合は `--write` または `--diff` が必要）。`--output json` を指定した場合は
診断結果のみを出力し、`--write` と組み合わせて利用できます。

> format の不変領域: **コメント / 文字列 / 括弧内改行は変更しない**（要件より）。

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
