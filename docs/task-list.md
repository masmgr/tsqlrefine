# タスクリスト（開発ロードマップ）

最終更新: 2026-01-24

このドキュメントは、`docs/requirements.md` / `docs/cli.md` / `docs/rules.md` の内容と、現状の実装状況（2026-01-24 時点）を突き合わせて、**これから進める作業**を一覧化したものです。

---

## 0. 優先度の目安

- **P0**: MVP に必須（動かない/破綻する/誤検知が多い を先に潰す）
- **P1**: 使い勝手・品質を上げる（運用に乗せやすくする）
- **P2**: 拡張・最適化・改善（将来のため）

---

## 1. 現状（完了していること）

- [x] CI で `dotnet build` / `dotnet test` を Windows/Linux で実行（`.github/workflows/ci.yml`）
- [x] CLI の骨格（`lint/check/format/init/print-config/list-rules/list-plugins`）が動く（`src/TsqlRefine.Cli/`）
- [x] エンジンがルールを実行し、診断を JSON で返せる（`src/TsqlRefine.Core/`）
- [x] ビルトインルールの最小例（`avoid-select-star`）＋テスト（`src/TsqlRefine.Rules/`, `tests/`）
- [x] ルールセット（`rulesets/*.json`）の有効/無効を反映できる（`src/TsqlRefine.Core/Config/Ruleset.cs`）
- [x] プラグイン読み込み（RuleProvider の発見）を実装（`src/TsqlRefine.PluginHost/`）

---

## 2. P0（最優先）

### 2.1 フォーマッタを “実装” する（現状は no-op）

- [x] `SqlFormatter.Format` を no-op から脱却（`src/TsqlRefine.Formatting/SqlFormatter.cs`）
- [x] 最小要件をまず満たす（`docs/requirements.md` の「3.3 フォーマット」）
  - [x] キーワードの大文字小文字統一
  - [x] インデント（spaces/tabs + size）の適用ポリシー確定
  - [x] 改行・末尾空白などの安全な正規化（コメント/文字列は壊さない）
- [x] `format` のテストを追加（`tests/TsqlRefine.Cli.Tests/` など）

### 2.2 位置情報（Range）を正しく出す

- [x] `Diagnostic.Range` を常に `(0,0)-(0,0)` から改善（`src/TsqlRefine.Core/Engine/TsqlRefineEngine.cs`, ルール実装）
- [x] `RuleContext.Tokens` を空配列のままにしない（トークナイズ方針を決めて実装）
- [x] ルール実装を Regex 中心から AST/Token 中心へ移行する方針を固める（誤検知と位置精度の改善）

### 2.3 CLI 仕様と実装の “差分” を潰す

- [x] `--ignorelist` を実装（`src/TsqlRefine.Cli/CliApp.cs` で “not implemented”）
- [x] `format --diff` を実装（`src/TsqlRefine.Cli/CliApp.cs` で “not implemented”）
- [x] `--version` を仕様に合わせる（少なくともバージョン文字列を表示）
- [x] 仕様反映時に `docs/cli.md` と `tests/TsqlRefine.Cli.Tests/` を更新（変更漏れ防止）

### 2.4 “MVP ルール” を増やす

`docs/rules.md` の「MVP ルール（最初に入れる 10〜20 個）」を基準に、まずは誤検知が少ないものから追加。

- [ ] P0: `dml-without-where`（Safety）
- [ ] P0: `avoid-null-comparison`（Correctness）
- [ ] P0: `require-parentheses-for-mixed-and-or`（Correctness）
- [ ] P1: `avoid-nolock`（Correctness）
- [ ] P1: `require-column-list-for-insert-*`（Correctness）
- [ ] 追加したルールごとにテスト（`tests/TsqlRefine.Rules.Tests/`）

---

## 3. P1（次にやる）

### 3.1 fix コマンド（安全な範囲の自動修正）

- [ ] `fix` を未実装から MVP 実装へ（`src/TsqlRefine.Cli/CliApp.cs`）
- [ ] ルール側 `Fixable` / `GetFixes` を実際に使う実行基盤を実装（適用順・衝突・複数ファイル）
- [ ] `--write` / `--diff` / “提案のみ” などの UX を確定（`docs/requirements.md` の「3.4 オートフィックス」）

### 3.2 設定・スキーマ・サンプル整備

- [ ] config/ruleset/plugins の変更に合わせて `schemas/` を更新
- [ ] `samples/` に最低限の config / ruleset / plugin 例を追加・同期

### 3.3 プラグインホスト強化（運用向け）

- [ ] 依存 DLL / ネイティブ DLL の解決を強化（必要なら `AssemblyLoadContext.LoadUnmanagedDll` 等）
- [ ] ロード結果の可観測性（エラー詳細、バージョン不整合など）を CLI 出力で分かりやすくする

---

## 4. P2（将来）

- [ ] `check`（semantic）向けのスコープ解析基盤を拡張（`docs/semantic-check-rules.md`）
- [ ] フォーマッタのオプション拡充（キーワード以外のポリシー、最小整形の範囲追加）
- [ ] 配布（.NET tool / release pipeline）とバージョニング（タグ、リリースノート）
- [ ] パフォーマンス最適化（大規模 SQL/大量ファイル時の速度・メモリ）

