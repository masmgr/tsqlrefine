# プロジェクト構成（.NET / CLI・コアのみ）

このドキュメントは、`tsqlrefine` を **.NET の CLI/コア中心**で開発する前提（VSCode 拡張は別リポジトリ）での、推奨リポジトリ構成をまとめたものです。

目的:

- CLI 仕様（`docs/cli.md`）を実装に落とし込みやすくする
- ルール実装・実行基盤・プラグイン SDK の責務境界を明確にする
- 将来、VSCode 拡張や CI に載せても運用しやすい形にする

---

## 1. ディレクトリ全体像（案）

```
.
├─ README.md
├─ docs/
│  ├─ requirements.md
│  ├─ rules.md
│  ├─ cli.md
│  ├─ plugin-api.md
│  └─ project-structure.md
├─ src/
│  ├─ TsqlRefine.sln
│  ├─ TsqlRefine.Cli/
│  ├─ TsqlRefine.Core/
│  ├─ TsqlRefine.Rules/
│  ├─ TsqlRefine.Formatting/
│  ├─ TsqlRefine.PluginSdk/
│  └─ TsqlRefine.PluginHost/
├─ tests/
│  ├─ TsqlRefine.Core.Tests/
│  ├─ TsqlRefine.Rules.Tests/
│  └─ TsqlRefine.Cli.Tests/
├─ samples/
│  ├─ sql/
│  ├─ config/
│  └─ plugins/
├─ rulesets/
├─ schemas/
└─ tools/
```

---

## 2. ルート（設定・メタ）

ルート直下には「開発体験」と「ビルド再現性」を支える設定を置きます。

- `.editorconfig`: C#/Markdown の基本整形
- `global.json`: .NET SDK バージョン固定
- `Directory.Build.props`: 全プロジェクト共通の設定（nullable, LangVersion など）
- `Directory.Packages.props`: 依存パッケージの一元管理
- `nuget.config`: 追加のフィードやソース制御が必要な場合
- `.github/workflows/`: CI（build/test/pack）

---

## 3. `docs/`（仕様・設計）

実装と独立して参照できる仕様を集約します。

- `docs/requirements.md`: 要件定義とスコープ
- `docs/rules.md`: ルールカテゴリ/優先度/プリセット方針
- `docs/cli.md`: CLI 入出力（JSON/終了コード含む）
- `docs/plugin-api.md`: プラグイン API（最小契約: Rule）

---

## 4. `src/`（製品コード）

`src/TsqlRefine.sln` 配下で、責務と公開境界を分けます。

### 4.1 `TsqlRefine.Cli/`

- `tsqlrefine` 実行ファイル
- パス列挙、stdin、出力形式（text/json）、終了コードの制御
- config/ignore/preset の解決と、コアへの引き渡し

### 4.2 `TsqlRefine.Core/`

- AST/トークン/診断モデルなどの共通モデル
- ルール実行パイプライン（`lint` / `check` の核）
- 設定の読み込みと、ルールへの設定引き回し

### 4.3 `TsqlRefine.Rules/`

- 組み込みルールの実装（カテゴリ/既定 severity/fixable を含む）
- `rulesets/`（プリセット）に従った “既定セット” を提供

### 4.4 `TsqlRefine.Formatting/`

- `format` / `fix` が共通で使う “最小限整形” の実装
- 要件（コメント/文字列/括弧内改行を変えない 等）を守る責務

### 4.5 `TsqlRefine.PluginSdk/`（公開境界）

- `docs/plugin-api.md` の「最小契約」を .NET の型として提供
- コア実装の詳細を露出しない（破壊的変更を最小化するための境界）
- 将来の NuGet 配布対象（想定）

### 4.6 `TsqlRefine.PluginHost/`

- プラグイン探索・ロード（DLL）
- API バージョン互換チェック
- 例外隔離（プラグインが落ちてもコアが落ちない）

---

## 5. `tests/`（品質）

レイヤごとにテスト責務を分けます。

- `TsqlRefine.Core.Tests/`: 解析・設定・診断の単体テスト
- `TsqlRefine.Rules.Tests/`: ルールの入出力テスト（サンプル SQL を共有してもよい）
- `TsqlRefine.Cli.Tests/`: 統合テスト（stdin/paths/json/exit code）

---

## 6. `samples/`（デモ・動作確認）

利用者や開発者が “まず動かす” ための素材を置きます。

- `samples/sql/`: ルールの挙動が分かる SQL 例
- `samples/config/`: 最小 config / ignore / ruleset の例
- `samples/plugins/`: ビルド済み DLL を置いてロード確認できる置き場（サンプル実装は別途でも可）

---

## 7. `rulesets/`（プリセットの実体）

`docs/rules.md` のプリセットを **ファイルとして固定**します。

- `recommended.json`
- `strict.json`
- `security-only.json`

CLI の `--preset` はここを参照して展開する想定です。

---

## 8. `schemas/`（設定補完）

設定ファイルの JSON Schema を置き、エディタ補完やバリデーションに使います。

- `tsqlrefine.schema.json`（例: config 用）
- `ruleset.schema.json`（例: ruleset 用）

---

## 9. `tools/`（開発用）

生成・検証・ベンチマークなど、製品コードに混ぜたくない補助を置きます。

- 例: ルール一覧の生成、ルール/プリセット整合性チェック、リリース補助

