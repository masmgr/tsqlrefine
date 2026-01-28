# リリース手順

このドキュメントでは、tsqlrefineの新しいバージョンをリリースする手順を説明します。

## バージョニング方針

tsqlrefineは[Semantic Versioning 2.0.0](https://semver.org/)に従います。

### バージョン番号の形式

- **MAJOR.MINOR.PATCH** (例: `1.0.0`)
- **MAJOR.MINOR.PATCH-prerelease** (例: `0.1.0-alpha`, `1.0.0-beta.1`)

### バージョンの更新ルール

- **MAJOR**: 後方互換性のない変更
  - PluginSDK APIの破壊的変更
  - CLIの引数や出力形式の破壊的変更
  - 設定ファイル形式の破壊的変更

- **MINOR**: 後方互換性のある機能追加
  - 新しいルールの追加
  - 新しいCLIコマンドやオプションの追加
  - 新しいAPI機能の追加（PluginSDKの拡張）

- **PATCH**: 後方互換性のあるバグ修正
  - ルールの誤検知修正
  - フォーマッタのバグ修正
  - パフォーマンス改善

### プレリリース版

- **alpha**: 開発中の不安定版（破壊的変更の可能性あり）
- **beta**: 機能凍結済み、テスト中（バグ修正のみ）
- **rc**: リリース候補版（重大なバグ修正のみ）

## リリース手順

### 1. バージョン番号の決定

次のリリースのバージョン番号を決定します。

- 現在のバージョンは `Directory.Build.props` の `VersionPrefix` で確認
- 変更内容に基づいて、適切なバージョン番号を決定

### 2. バージョン番号の更新

`Directory.Build.props` を編集してバージョン番号を更新します。

```xml
<VersionPrefix>0.2.0</VersionPrefix>
<VersionSuffix Condition="'$(VersionSuffix)' == ''">alpha</VersionSuffix>
```

プレリリース版の場合:
- `VersionSuffix` を `alpha`, `beta`, `rc.1` などに設定

安定版リリースの場合:
- `VersionSuffix` を削除するか、空文字列に設定
- または、ビルド時に `/p:VersionSuffix=` を指定

### 3. CHANGELOGの更新

`CHANGELOG.md` を作成・更新して、リリース内容を記録します。

```markdown
## [0.2.0] - 2026-01-29

### Added
- 新しいルール: `require-schema-prefix`
- `--parallel` オプションで並列処理をサポート

### Changed
- パフォーマンス改善: 大規模SQLファイルの解析速度が2倍に

### Fixed
- `avoid-select-star` ルールのCTE内での誤検知を修正
```

### 4. コミットとタグの作成

```bash
# 変更をコミット
git add Directory.Build.props CHANGELOG.md
git commit -m "chore: bump version to 0.2.0"

# タグを作成（v プレフィックス必須）
git tag v0.2.0

# リモートにプッシュ
git push origin main
git push origin v0.2.0
```

### 5. GitHub Actionsによる自動リリース

タグをプッシュすると、GitHub Actionsが自動的に以下を実行します:

1. ビルドとテスト
2. NuGetパッケージの作成
3. GitHub Releaseの作成（パッケージファイルを添付）
4. NuGet.orgへの公開（安定版のみ、プレリリース版は除外）

リリースの進行状況は以下で確認できます:
- GitHub Actions: https://github.com/imasa/tsqlrefine/actions

### 6. リリースノートの編集

GitHub Releaseが作成された後、必要に応じてリリースノートを編集します:

1. https://github.com/imasa/tsqlrefine/releases にアクセス
2. 最新のリリースを選択
3. "Edit release" をクリック
4. リリースノートを充実させる（Breaking changes、Migration guide など）

## ローカルでのパッケージ作成

リリースプロセスをテストする場合や、ローカルでパッケージを作成する場合:

```bash
# ビルド（プレリリース版）
dotnet build src/TsqlRefine.sln -c Release

# ビルド（安定版）
dotnet build src/TsqlRefine.sln -c Release /p:VersionSuffix=

# パッケージ作成
dotnet pack src/TsqlRefine.Cli/TsqlRefine.Cli.csproj -c Release /p:VersionSuffix=

# 出力先
# nupkg/TsqlRefine.0.2.0.nupkg
# nupkg/TsqlRefine.0.2.0.snupkg (シンボルパッケージ)
```

## インストール方法

### グローバルツールとしてインストール

```bash
# NuGet.orgから最新版をインストール
dotnet tool install --global TsqlRefine

# 特定のバージョンをインストール
dotnet tool install --global TsqlRefine --version 0.2.0

# プレリリース版をインストール
dotnet tool install --global TsqlRefine --version 0.2.0-alpha --prerelease

# 更新
dotnet tool update --global TsqlRefine

# アンインストール
dotnet tool uninstall --global TsqlRefine
```

### ローカルツールとしてインストール

プロジェクト単位でツールを管理する場合:

```bash
# ツールマニフェストを作成
dotnet new tool-manifest

# ローカルツールとしてインストール
dotnet tool install TsqlRefine

# 実行
dotnet tsqlrefine --help

# 更新
dotnet tool update TsqlRefine
```

### ローカルパッケージからインストール

開発中やテスト用に、ローカルでビルドしたパッケージをインストールする場合:

```bash
# パッケージを作成
dotnet pack src/TsqlRefine.Cli/TsqlRefine.Cli.csproj -c Release /p:VersionSuffix=

# ローカルソースからインストール
dotnet tool install --global TsqlRefine --add-source ./nupkg --version 0.2.0

# または、直接パッケージを指定
dotnet tool install --global --add-source ./nupkg TsqlRefine
```

## トラブルシューティング

### パッケージが見つからない

NuGet.orgへの公開後、インデックスに反映されるまで数分かかる場合があります。

### 古いバージョンがインストールされる

キャッシュをクリアしてから再インストール:

```bash
dotnet nuget locals all --clear
dotnet tool update --global TsqlRefine
```

### GitHub Actionsのワークフローが失敗する

- `NUGET_API_KEY` シークレットが設定されているか確認
  - Settings > Secrets and variables > Actions > Repository secrets
- テストが全てパスしているか確認
- ビルドエラーがないか確認

## 参考資料

- [Semantic Versioning 2.0.0](https://semver.org/)
- [.NET Global Tools のドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/tools/global-tools)
- [NuGet パッケージの作成とパブリッシュ](https://learn.microsoft.com/ja-jp/nuget/quickstart/create-and-publish-a-package-using-the-dotnet-cli)
