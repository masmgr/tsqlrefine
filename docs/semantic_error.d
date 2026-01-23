あります。ScriptDom みたいな「構文は通るけど意味が壊れてる」系は、マージや機械編集で特に刺さります。列の存在確認みたいな“本当のバインディング”抜きでも、**AST とスコープだけで検出できて実用的**なものを挙げます。

## 1) 別名・スコープ系（あなたの話の拡張）

* **未定義別名参照**（今のテーマ）
* **同一スコープで別名重複**（`FROM A t JOIN B t` みたいな衝突）
* **外側スコープの別名を内側で誤参照**（相関サブクエリのつもりが違う、または逆）
* **CTE 名の衝突**（CTE名とテーブル別名、CTE同士など）
  → これ全部、マージで“名前がずれて”死にやすいです。

## 2) GROUP BY / 集計の意味崩れ（DB無しでもかなり検出できる）

* **SELECT に非集計列があるのに GROUP BY に無い**

  * これは SQL Server なら普通はエラーになりますが、クエリ断片の合成時に「条件付きで列が入る」などで壊れやすい
* **HAVING で集計してない列を参照してる**（意図ミスの匂い）
* **GROUP BY の列が増減して意図が変わる**（これは警告向け）

※列の存在確認はできなくても「式が集計関数の内側か外側か」は AST で取れます。

## 3) JOIN の意味ミス（静的に嫌な匂いを取れる）

* **JOIN してるのに ON が無い/常に true に近い**

  * `JOIN T2 ON 1=1`、`ON t1.col = t1.col` みたいな事故（マージで条件が消えた系）
* **LEFT JOIN してるのに WHERE で右側を絞って実質 INNER JOIN になってる**

  * 例：`LEFT JOIN B ... WHERE B.id = ...`
  * “意図的”もあるけど、事故も多いので警告として強い
* **JOIN 条件の別名取り違え**（`a.id = a.id` みたいな自己比較、または左右逆参照の欠落）

## 4) DISTINCT / TOP / ORDER BY の事故（結果が静かに変わる）

* **TOP があるのに ORDER BY が無い**（結果が不安定）
* **DISTINCT を足した/消した**（重複排除が意図か事故か。差分検知で警告すると強い）
* **ORDER BY が外側と内側で意味が違う**（サブクエリ内 ORDER BY の無効化パターンなど）

  * SQL Server の「ORDER BY は TOP / OFFSET がないと意味がない」系を検知できる

## 5) UPDATE/DELETE の“やらかし”検知（超役に立つ）

* **WHERE が無い UPDATE/DELETE**（意図的でも要レビュー）
* **FROM 付き UPDATE で結合が多対多になりそう**（行が増える/結果不定）

  * 厳密判定は無理でも「JOIN してる」「キーっぽい等価条件が無い」などで警告は可能
* **INSERT INTO (列リスト) と SELECT の列数不一致**

  * 型は見れないけど、列数は見れるので強い

## 6) パラメータ・変数の意味ミス（DB不要で取れる）

* **宣言されてない変数参照**（`@x` を使ってるのに `DECLARE` や引数が無い）
* **同名変数の再宣言**（スコープ内で `DECLARE @x` が重複）
* **OUTPUT 変数を使ってない**（ストアド呼び出し側のミスは別だけど、内部でも類似あり）

## 7) 制御フローで「絶対に実行されない/される」系

* **RETURN の後に続く文**（死んでるコード）
* **IF の条件が定数**（`IF 1=1` 的なもの。テンプレ事故で残りがち）
* **TRY/CATCH で THROW/RAISERROR が無いのに握りつぶし**（レビュー警告向け）

---

# 実務での優先度（おすすめ順）

マージ事故検知の「費用対効果」だけで並べると：

1. **未定義/重複別名・スコープ違反**
2. **WHEREなし UPDATE/DELETE**
3. **TOP without ORDER BY**
4. **LEFT JOIN が WHERE で潰れてる**
5. **JOIN 条件が消えてる/恒真**
6. **INSERT 列数不一致**
7. **GROUP BY/集計の不整合（可能な範囲で）**

この辺は「DBに繋がない静的解析」でもかなり防げます。

---

もし「ストアドのマージで壊れる」を主目的にするなら、上の 1〜5 を **エラー/警告の2段階**で運用するのが一番ラクです（全部をエラーにすると誤検知で嫌われがち）。


結論から言うと、**ScriptDom 単体では「FROM に存在しないテーブル／エイリアス参照」を直接検知する API はありませんが、AST を辿って自前で解析すれば十分に検知可能です。**
ただし「列解決（バインディング）」は行われないため、**構文木＋スコープ解析を実装する必要があります。**

---

## なぜ ScriptDom だけでは不十分か

ScriptDom は **構文解析（Syntax）専用**であり、

* テーブルや列の**意味解決（Semantic Binding）**
* エイリアスの**スコープ検証**
  は行いません。

つまり、

```sql
SELECT a.col FROM Table1 t;
```

で `a` が未定義でも **パースは成功します。**

---

## 検知の基本戦略（実用パターン）

### ① FROM 句から「有効なテーブル／エイリアス一覧」を収集

対象ノード例:

* `NamedTableReference`
* `QueryDerivedTable`
* `SchemaObjectFunctionTableReference`
* `JoinTableReference`

収集対象:

* 実テーブル名
* エイリアス名 (`Alias?.Value`)
* サブクエリの別名

```csharp
var declaredAliases = new HashSet<string>();

void CollectFromTable(TableReference table)
{
    switch (table)
    {
        case NamedTableReference t:
            declaredAliases.Add(t.Alias?.Value ?? t.SchemaObject.BaseIdentifier.Value);
            break;

        case QueryDerivedTable q:
            declaredAliases.Add(q.Alias?.Value);
            break;

        case JoinTableReference j:
            CollectFromTable(j.FirstTableReference);
            CollectFromTable(j.SecondTableReference);
            break;
    }
}
```

---

## ② SELECT / WHERE / GROUP BY / HAVING 内の参照を収集

対象ノード:

* `ColumnReferenceExpression`
* `MultiPartIdentifier`

```csharp
var usedAliases = new HashSet<string>();

void CollectColumnRef(ColumnReferenceExpression col)
{
    if (col.MultiPartIdentifier.Identifiers.Count > 1)
    {
        var alias = col.MultiPartIdentifier.Identifiers[0].Value;
        usedAliases.Add(alias);
    }
}
```

---

## ③ FROM に存在しない参照を検知

```csharp
var invalid = usedAliases.Except(declaredAliases);
```

---

## 検知できる例

```sql
SELECT x.col FROM Table1 t WHERE y.col = 1;
```

検知結果:

```
x → 未定義
y → 未定義
```

---

## 注意点（実運用で必須）

| ケース                               | 対応                   |
| --------------------------------- | -------------------- |
| `SELECT col FROM Table1`（エイリアス省略） | OK とする               |
| `SELECT *`                        | 列検証不可                |
| サブクエリ（スコープ分離）                     | **スコープスタック管理が必要**    |
| CTE (`WITH`)                      | CTE 名を FROM と同様に登録   |
| `CROSS APPLY`, TVF                | TableReference として処理 |
| `INSERT/UPDATE/DELETE`            | 別途対応                 |
| `EXEC()` 動的 SQL                   | 静的解析不可               |

---

## スコープ管理が重要（サブクエリ対応）

### 悪い例（誤検知）

```sql
SELECT * FROM (
   SELECT a.col FROM Table1 a
) t WHERE a.col = 1; -- a は外側では無効
```

→ **スコープスタック**（ネストごとに alias set を分離）が必要

---

## より強力な代替手段（参考）

### Semantic Binding が必要なら

| 方法                                                    | 特徴       |
| ----------------------------------------------------- | -------- |
| SQL Server の `sys.dm_exec_describe_first_result_set`  | 実 DB 必要  |
| SMO + バインディング                                         | 部分可能     |
| Microsoft.SqlServer.TransactSql.ScriptDom + 自作 Binder | 実務で現実的   |
| SQL Server Query Optimizer（SHOWPLAN）                  | 最強・DB 必須 |

---

## もし望むなら提供可能

必要なら **完全対応サンプルコード**を出せます：

* C# + ScriptDom
* スコープ対応
* CTE / サブクエリ / JOIN / APPLY 対応
* SELECT / WHERE / GROUP BY / HAVING / ORDER BY 対応
* **「未定義テーブル／エイリアス検知クラス」完全版**

欲しければ言ってください。
