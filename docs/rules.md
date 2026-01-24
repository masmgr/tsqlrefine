# ルール整備（カテゴリ / 優先度 / プリセット）

このドキュメントは、以下を元に **tsqlrefine のルール体系**を整理したものです。

- `docs/tsqllint-basic-rules.md`（TSQLLint 由来の基本ルール）
- `docs/tsqllint-extend-rules.md`（拡張ルール）
- `docs/semantic-check-rules.md`（AST/スコープ中心の静的解析アイデア）

前提:

- 対象 SQL Server: **2012 以降**
- 互換レベル: **設定ファイルで指定可能**
- 入力: **純SQLのみ**（アプリコード内SQL抽出は対象外）
- `GO` / 複数バッチ / `CREATE PROC` 等 DDL: 対応
- 動的SQL文字列（`EXEC(...)` など）・文字列リテラル内は **解析対象外**

---

## 1. ルール種別（lint / semantic）

- **lint**: ルールベース（構文・規約・危険パターン）。パーサ + ルールで判定。
- **semantic**: AST + スコープ（必要なら軽い推論）で「意味が壊れやすい」箇所を検出。

> semantic は「DB接続なし」を前提にするため、**列/型の正確なバインディングは基本やらない**（やるなら別モードで明示）。

---

## 2. カテゴリ（tsqlrefine の正規カテゴリ）

- **Style**: 表記統一・最小整形（キーワード、セミコロン、空行など）
- **Naming**: 別名・識別子の読みやすさ（意味のある alias など）
- **Correctness**: 意味が変わりやすい/誤りやすい（NULL、日付リテラル、AND/OR など）
- **Safety**: 破壊的/広範囲操作の事故防止（WHEREなし DML、危険DDL など）
- **Security**: 注入/危険な実行形態（動的SQL、権限絡みの危険等）
- **Performance**: SARGability、暗黙変換、不要なパターン等
- **Transactions**: TRY/CATCH、XACT_ABORT、分離レベル等
- **Schema**: DDL設計・アンチパターン（SELECT INTO、HEAP、PK 等）
- **Debug**: PRINT 等（出力/デバッグ）

---

## 3. 優先度（P0/P1/P2）と既定 severity

運用しやすさ優先で、まずは **誤検知が少なく費用対効果が高い**ものを P0 に寄せます。

- **P0 (Error)**: 明確に危険/バグになりやすい、誤検知が比較的少ない
- **P1 (Warning)**: 事故が多いが例外もある、チーム方針で変わる
- **P2 (Info/Hint)**: 好み/スタイル、または「匂い」検知

---

## 4. プリセット（初期提案）

- `recommended`（初期既定）: P0 を中心に、P1 のうち高信頼なもの
- `strict`: `recommended` + Style/Naming を厚め（レビュー負荷は上がる）
- `security-only`: Security + Safety のうち破壊的操作/注入系中心

互換レベル/SQL Server バージョン依存ルールは、プリセットに含めても **実行時に自動で無効化/警告**できる設計にします。

---

## 5. MVP ルール（最初に入れる 10〜20 個の叩き台）

### P0 (Error)

- `semantic/undefined-alias`（別名・スコープ）: FROM に無い別名参照
- `semantic/duplicate-alias`（別名・スコープ）: 同一スコープの別名重複
- `semantic/dml-without-where`（Safety）: WHEREなし UPDATE/DELETE（※例外は設定で許容）
- `top-without-order-by`（Performance/Correctness）: TOP に ORDER BY が無い
- `require-parentheses-for-mixed-and-or`（Correctness）: AND/OR 混在に括弧必須
- `avoid-null-comparison`（Correctness）: `= NULL` / `<> NULL` 禁止

### P1 (Warning)

- `avoid-nolock`（Correctness）: `NOLOCK`/`READ UNCOMMITTED` 警告
- `avoid-implicit-conversion-in-predicate`（Performance/Correctness）: 述語の暗黙変換
- `require-column-list-for-insert-values`（Correctness）: INSERT VALUES の列リスト必須
- `require-column-list-for-insert-select`（Correctness）: INSERT SELECT の列リスト必須
- `avoid-exec-dynamic-sql`（Security）: `EXEC(...)` の動的SQL（※解析対象外でも “存在” は検出）
- `avoid-merge`（Safety/Correctness）: `MERGE` 禁止（方針ルール）

### P2 (Info/Hint)

- `keyword-capitalization`（Style）: キーワード表記統一（format と連携）
- `semicolon-termination`（Style）: セミコロン終端（format と連携）
- `require-as-for-table-alias` / `require-as-for-column-alias`（Style/Naming）: AS を強制
- `meaningful-alias`（Naming）: 1文字 alias を警告（多テーブル時）

---

## 6. semantic ルール（最小セット定義）

`docs/semantic-check-rules.md` の内容を **実装可能な “ルールID”** に落とすと以下です（初期案）。

- `semantic/undefined-alias`: 未定義別名参照
- `semantic/duplicate-alias`: 同一スコープで別名重複
- `semantic/alias-scope-violation`: 内側/外側スコープの誤参照（相関の勘違い等）
- `semantic/cte-name-conflict`: CTE 名衝突（CTE同士/テーブル別名との衝突）
- `semantic/join-condition-always-true`: `ON 1=1` / 自己比較など恒真に近い結合
- `semantic/left-join-filtered-by-where`: LEFT JOIN が WHERE で潰れて実質 INNER
- `semantic/insert-column-count-mismatch`: INSERT の列数不一致（列リスト vs SELECT/VALUES）
- `semantic/return-after-statements`: RETURN 後に到達不能文

> **列の存在確認/型推論**は扱わず、AST とスコープで取れる範囲に限定します。

---

## 7. 既存ルールの取り込み方針（basic/extend）

- `docs/tsqllint-basic-rules.md` の 33 ルールは「移植候補の母集団」。
- `docs/tsqllint-extend-rules.md` の 38 ルールは「追加候補の母集団」。
- tsqlrefine 側では **カテゴリ/既定 severity/互換レベル依存**を付与して “製品としてのルールセット” に整備する。

次の作業単位（実装・ドキュメント両方）:

1. ルールごとに `category`, `defaultSeverity(P0/P1/P2)`, `fixable`, `minSqlVersion` を確定
2. `recommended/strict/security-only` の ON/OFF を確定
3. 互換レベルで自動無効化されるルールの扱い（警告 or サイレント）を決める
