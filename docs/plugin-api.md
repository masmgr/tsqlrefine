# プラグイン API（最小契約: Rule）

このプロジェクトでは **Rule のみ**をプラグイン化対象とします（Formatter/Reporter はコア固定）。

目的:

- チーム固有の規約/危険パターンを追加できる
- コアの更新とプラグインの互換性を管理できる

---

## 1. 基本設計

- 1 プラグイン = “Rule の集合”
- ロード失敗時は **コアが落ちない**（要件: 信頼性）
- 互換性は「API バージョン」で判定し、非互換ならロードしない（警告を出す）

---

## 2. ルール ID 命名

衝突回避のため、プラグインは namespace を必須とします。

- 例: `myteam/avoid-select-star-in-procs`
- コア/標準プラグインは `core/...` / `semantic/...` のように予約

---

## 3. ルール契約（概念）

`tsqlrefine` の内部モデルは実装言語に依存しますが、契約として最低限必要なのは以下です。

### 3.1 メタデータ

- `ruleId`（一意）
- `description` / `messageTemplate`
- `category`（`docs/rules.md` のカテゴリ）
- `defaultSeverity`（Error/Warning/Info/Hint）
- `fixable`（boolean）
- `minSqlVersion` / `maxSqlVersion`（任意）

### 3.2 実行

- 入力: パース済 AST（`GO` 単位のバッチも含む）、トークン、互換レベル、設定
- 出力: `Diagnostic[]`（必要なら Fix/提案も）

---

## 4. C# インターフェース案（.NET 前提の場合）

> 実装言語が確定していない段階でも、外形として “こういう形を要求する” を先に置けます。

```csharp
public enum RuleSeverity
{
    Error,
    Warning,
    Information,
    Hint
}

public sealed record RuleMetadata(
    string RuleId,
    string Description,
    string Category,
    RuleSeverity DefaultSeverity,
    bool Fixable,
    int? MinCompatLevel = null,
    int? MaxCompatLevel = null
);

public interface IRule
{
    RuleMetadata Metadata { get; }

    IEnumerable<Diagnostic> Analyze(RuleContext context);

    IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic);
}

public sealed record RuleContext(
    string FilePath,
    int CompatLevel,
    ScriptDomAst Ast,
    IReadOnlyList<Token> Tokens,
    RuleSettings Settings
);
```

※ `GetFixes` は `Metadata.Fixable == true` の場合のみ呼ばれる。

---

## 5. ロード仕様

設定ファイルでプラグインを指定します。

例:

```jsonc
{
  "plugins": [
    { "path": "./plugins/MyTeam.TsqlRefineRules.dll", "enabled": true }
  ]
}
```

ロード時:

1. アセンブリを読み込み
2. `IRule` 実装（または `IRuleProvider`）を列挙
3. `ruleId` の重複チェック（重複はエラー扱いで、そのプラグインを無効化）

---

## 6. 互換性

- コアは `pluginApiVersion` を持つ（例: `1`）
- プラグインは `supportedApiVersions: [1]` を宣言
- 合わなければロードしない（`list-plugins` に理由を表示）

