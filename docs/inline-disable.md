# インライン無効化ディレクティブ

tsqlrefine では、SQL ファイル内のコメントを使用してルールを一時的に無効化できます。

---

## 基本構文

### すべてのルールを無効化

```sql
/* tsqlrefine-disable */
SELECT * FROM Users;
/* tsqlrefine-enable */
```

`tsqlrefine-disable` から `tsqlrefine-enable` までの範囲内では、すべてのルール違反が抑制されます。

### 特定のルールを無効化

```sql
/* tsqlrefine-disable avoid-select-star */
SELECT * FROM Users;
/* tsqlrefine-enable avoid-select-star */
```

指定したルール ID のみが無効化されます。他のルールは引き続き適用されます。

### 複数のルールを無効化

```sql
/* tsqlrefine-disable avoid-select-star, dml-without-where */
SELECT * FROM Users;
UPDATE Users SET Status = 1;
/* tsqlrefine-enable avoid-select-star, dml-without-where */
```

カンマ区切りで複数のルール ID を指定できます。

---

## スクリプト全体を無効化

ファイルの先頭に `tsqlrefine-disable` を配置し、対応する `tsqlrefine-enable` を省略すると、スクリプト全体でルールが無効化されます。

### すべてのルールを無効化（スクリプト全体）

```sql
/* tsqlrefine-disable */

SELECT * FROM Users;
UPDATE Users SET Status = 1;
DELETE FROM TempData;
```

### 特定のルールを無効化（スクリプト全体）

```sql
/* tsqlrefine-disable avoid-select-star */

SELECT * FROM Users;
SELECT * FROM Orders;
```

---

## サポートされるコメント形式

### ブロックコメント

```sql
/* tsqlrefine-disable */
/* tsqlrefine-enable */
```

### ラインコメント

```sql
-- tsqlrefine-disable
-- tsqlrefine-enable
```

---

## ディレクティブの特性

### 大文字・小文字を区別しない

ディレクティブ名は大文字・小文字を区別しません。

```sql
/* TSQLREFINE-DISABLE */
/* TsqlRefine-Disable */
/* tsqlrefine-disable */
```

上記はすべて同じ動作をします。

### ルール ID の大文字・小文字

ルール ID も大文字・小文字を区別しません。

```sql
/* tsqlrefine-disable AVOID-SELECT-STAR */
/* tsqlrefine-disable Avoid-Select-Star */
/* tsqlrefine-disable avoid-select-star */
```

### 空白の扱い

ディレクティブ前後の空白は無視されます。

```sql
/*tsqlrefine-disable*/
/*  tsqlrefine-disable  */
/* 	tsqlrefine-disable 	*/
```

---

## ネストされた無効化

無効化ディレクティブはネストできます。

```sql
/* tsqlrefine-disable */
SELECT * FROM t1;           -- 外側の disable で抑制

/* tsqlrefine-disable */
SELECT * FROM t2;           -- 両方の disable で抑制
/* tsqlrefine-enable */

SELECT * FROM t3;           -- 外側の disable でまだ抑制
/* tsqlrefine-enable */

SELECT * FROM t4;           -- 抑制されない
```

内側の `enable` は内側の `disable` を閉じ、外側の `enable` が外側の `disable` を閉じます。

---

## 注意事項

### 行ベースの抑制

無効化は行ベースで適用されます。診断の開始行が無効化範囲内にある場合、その診断は抑制されます。

### パースエラーは抑制されない

構文エラー（`parse-error`）は `tsqlrefine-disable` の影響を受けません。これは構文エラーが根本的な問題を示すためです。

```sql
/* tsqlrefine-disable */
SELECT * FROM           -- パースエラーは報告される
```

### 無効化範囲の開始位置

無効化ディレクティブは、そのディレクティブが存在する行から有効になります。ディレクティブより前の行には影響しません。

```sql
SELECT * FROM t1;       -- 抑制されない（ディレクティブより前）
/* tsqlrefine-disable */
SELECT * FROM t2;       -- 抑制される
```

### 対応する enable がない場合

`tsqlrefine-enable` がない場合、無効化はファイル末尾まで継続します。

---

## サンプルファイル

`samples/sql/inline-disable/` ディレクトリに使用例があります：

- `disable-all.sql` - すべてのルールを無効化
- `disable-specific.sql` - 特定のルールを無効化
- `disable-region.sql` - 特定の範囲のみ無効化
- `disable-multiple.sql` - 複数のルールを無効化

---

## ルール ID の確認

利用可能なルール ID は `list-rules` コマンドで確認できます：

```bash
tsqlrefine list-rules
```

出力例：

```
avoid-select-star       Performance     Warning     fixable=False
dml-without-where       Safety          Error       fixable=False
...
```
