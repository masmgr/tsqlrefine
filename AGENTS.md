# AGENTS.md (tsqlrefine)

このリポジトリは、T-SQL を対象にした **lint / format / fix** を提供する .NET CLI ツール `tsqlrefine` と、そのルール/フォーマッタ/プラグインホストを含みます。

## まず最初に見る場所

- 入口（CLI）: `src/TsqlRefine.Cli/`
- エンジン/モデル（共通）: `src/TsqlRefine.Core/`
- 組み込みルール: `src/TsqlRefine.Rules/`
- フォーマット: `src/TsqlRefine.Formatting/`
- プラグイン SDK / ホスト: `src/TsqlRefine.PluginSdk/`, `src/TsqlRefine.PluginHost/`
- デバッグ/調査用（sln外）: `src/TsqlRefine.DebugTool/`
- サンプル: `samples/`
- ルールセット（プリセット）: `rulesets/`
- JSON Schema: `schemas/`
- ドキュメント: `docs/`
- ルール個別ドキュメント: `docs/Rules/`
- 開発用スクリプト/補助: `scripts/`（`*.ps1`）と `tools/`

## 開発環境

- `global.json` で .NET SDK を固定しています（`10.0.102`）。`dotnet --info` で一致する SDK が入っていることを確認してください。
- 各プロジェクトは `net10.0` ターゲットです（`src/*/*.csproj`）。
- 依存パッケージは Central Package Management（`Directory.Packages.props`）で管理しています。

## よく使うコマンド

```powershell
# build / test
dotnet build src/TsqlRefine.sln -c Release
dotnet test  src/TsqlRefine.sln -c Release

# CLI をローカル実行（例）
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json

# format（paths 指定は自動でファイルへ書き戻し。stdin は stdout へ出力）
dotnet run --project src/TsqlRefine.Cli -c Release -- format path\to\file.sql
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin

# fix（paths 指定は自動でファイルへ書き戻し。--output json はドライラン用途）
dotnet run --project src/TsqlRefine.Cli -c Release -- fix path\to\dir
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --output json path\to\file.sql

# init / list / config
dotnet run --project src/TsqlRefine.Cli -c Release -- init
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config --output json
dotnet run --project src/TsqlRefine.Cli -c Release -- print-format-config --show-sources
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules
dotnet run --project src/TsqlRefine.Cli -c Release -- list-plugins --verbose

# coverage
.\scripts\run-coverage.ps1
```

## コーディング規約（最低限）

- `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `EnableNETAnalyzers=true`（警告を無視して通す変更は避ける）
- `.editorconfig`:
  - 改行: LF、文字コード: UTF-8
  - C#: 4 spaces、`using` の System 優先ソート
- ソースコード内の文字列（例: メッセージ）とコメントは英語で記載する（例外が必要な場合は PR で理由を明記する）

## 変更時の注意（よく漏れる点）

- **CLI のオプション/出力 JSON を変える場合**: `docs/cli.md`、`README.md` とテスト（`tests/TsqlRefine.Cli.Tests/`）も更新する。
- **設定ファイル（config/ruleset/plugins）を変える場合**: `schemas/`（`tsqlrefine.schema.json` / `ruleset.schema.json`）と `samples/` を更新する。
- **ルールを追加/変更する場合**:
  - ルール実装: `src/TsqlRefine.Rules/Rules/`
  - ルール一覧/プリセット: `rulesets/`（必要なら）
  - テスト: `tests/TsqlRefine.Rules.Tests/` や `tests/TsqlRefine.Core.Tests/`
- **ルール個別ドキュメントを更新する場合**: `docs/Rules/`（と `docs/Rules/README.md`）を更新する。
- **プラグインロード周りを触る場合**: `src/TsqlRefine.PluginHost/` は AssemblyLoadContext 境界があるため、依存 DLL の解決/重複ロードに注意する。

## 仕様と実装の差分メモ

- CLI はサブコマンド必須です（省略時に `lint` にはなりません）。
- `format` / `fix` は paths 指定時に自動でファイルへ書き戻します。stdin の場合は stdout へ出力します。
- `fix --output json` は診断のみを出力し、ファイルへの書き戻しは行いません。
