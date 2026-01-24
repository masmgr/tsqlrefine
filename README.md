# tsqlrefine

## Docs

- 要件定義: `docs/requirements.md`
- ルール整備（カテゴリ/優先度/プリセット）: `docs/rules.md`
- CLI 仕様（JSON/終了コード）: `docs/cli.md`
- プラグイン API（最小契約: Rule）: `docs/plugin-api.md`
- プロジェクト構成（.NET / CLI・コアのみ）: `docs/project-structure.md`

## Dev

```powershell
dotnet build src/TsqlRefine.sln -c Release
dotnet test src/TsqlRefine.sln -c Release

# stdin から lint（JSON 出力）
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```
