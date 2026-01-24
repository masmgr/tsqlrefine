# AGENTS.md (tsqlrefine)

このリポジトリは、T-SQL を対象にした **lint / check / format / fix** を提供する .NET CLI ツール `tsqlrefine` と、そのルール/フォーマッタ/プラグインホストを含みます。

## まず最初に見る場所

- 入口（CLI）: `src/TsqlRefine.Cli/`
- エンジン/モデル（共通）: `src/TsqlRefine.Core/`
- 組み込みルール: `src/TsqlRefine.Rules/`
- フォーマット: `src/TsqlRefine.Formatting/`
- プラグイン SDK / ホスト: `src/TsqlRefine.PluginSdk/`, `src/TsqlRefine.PluginHost/`
- サンプル: `samples/`
- ルールセット（プリセット）: `rulesets/`
- JSON Schema: `schemas/`
- ドキュメント: `docs/`

## 開発環境

- `global.json` で .NET SDK を固定しています（例: `10.0.102`）。`dotnet --info` で一致する SDK が入っていることを確認してください。
- 各プロジェクトは `net10.0` ターゲットです（`src/*/*.csproj`）。
- 依存パッケージは Central Package Management（`Directory.Packages.props`）で管理しています。

## よく使うコマンド

```powershell
# build / test
dotnet build src/TsqlRefine.sln -c Release
dotnet test  src/TsqlRefine.sln -c Release

# CLI をローカル実行（例）
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

## コーディング規約（最低限）

- `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `EnableNETAnalyzers=true`（警告を無視して通す変更は避ける）
- `.editorconfig`:
  - 改行: LF、文字コード: UTF-8
  - C#: 4 spaces、`using` の System 優先ソート
- ソースコード内の文字列（例: メッセージ）とコメントは英語で記載する（例外が必要な場合は PR で理由を明記する）

## 変更時の注意（よく漏れる点）

- **CLI のオプション/出力 JSON を変える場合**: `docs/cli.md` とテスト（`tests/TsqlRefine.Cli.Tests/`）も更新する。
- **設定ファイル（config/ruleset/plugins）を変える場合**: `schemas/` の JSON Schema と `samples/` を更新する。
- **ルールを追加/変更する場合**:
  - ルール実装: `src/TsqlRefine.Rules/Rules/`
  - ルール一覧/プリセット: `rulesets/`（必要なら）
  - テスト: `tests/TsqlRefine.Rules.Tests/` や `tests/TsqlRefine.Core.Tests/`
- **プラグインロード周りを触る場合**: `src/TsqlRefine.PluginHost/` は AssemblyLoadContext 境界があるため、依存 DLL の解決/重複ロードに注意する。
