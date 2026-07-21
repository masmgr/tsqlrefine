# Semantic Unicode String

**Rule ID:** `semantic/unicode-string`
**Category:** Correctness
**Severity:** Error
**Fixable:** Yes

## Description

Detects national (`N'...'`) string literals assigned to non-Unicode (`VARCHAR`/`CHAR`) variables, which may cause data loss.

## Rationale

Assigning a national string literal to a non-Unicode (`VARCHAR`/`CHAR`) variable requires a code-page conversion. Characters that are not representable in the database collation's code page are replaced or otherwise lose information.

Non-national literals such as `'こんにちは'` are not reported by this rule. SQL Server interprets those literals in the database code page before assignment, so this rule cannot determine their representability without collation information. Use `prefer-unicode-string-literals` to require the `N` prefix when Unicode semantics are intended.

**Silent data corruption**:

When Unicode characters are stored in VARCHAR/CHAR variables, they are converted to `?` characters:

```sql
DECLARE @Name VARCHAR(50);
SET @Name = N'こんにちは';  -- Japanese "Hello"
SELECT @Name;               -- Returns '?????' (data corrupted!)
```

**Why this happens**:

1. **VARCHAR/CHAR encoding**: Uses the code page associated with the SQL Server collation
   - Some code pages, including Japanese code page 932, represent multibyte character sets
   - Characters outside the selected code page cannot be represented

2. **NVARCHAR/NCHAR encoding**: Uses UTF-16 (Unicode)
   - Supports 65,536+ characters (all languages, emojis, symbols)
   - Required for international text

3. **Silent conversion**: SQL Server converts unsupported characters to `?` **without error**
   - No compile-time error
   - No runtime error
   - Data is silently corrupted

**Data types comparison**:

| Type | Encoding | Max Characters | Unicode Support | Use Case |
|------|----------|----------------|-----------------|----------|
| VARCHAR | Code page (ASCII) | 256 | No | English-only text |
| CHAR | Code page (ASCII) | 256 | No | Fixed-length codes (US state codes) |
| NVARCHAR | UTF-16 | 65,536+ | Yes | International text, names, emails |
| NCHAR | UTF-16 | 65,536+ | Yes | Fixed-length international codes |

**Affected Unicode characters**:

- **Japanese**: こんにちは, ありがとう → ?????
- **Chinese**: 你好, 谢谢 → ??, ??
- **Arabic**: مرحبا, شكرا → ?????, ????
- **Korean**: 안녕하세요 → ?????
- **Emoji**: 😀, 🎉, ❤️ → ?, ?, ?
- **Accented characters**: café, naïve, Müller → caf?, na?ve, M?ller
- **Mathematical symbols**: ∑, ∞, ≠ → ?, ?, ?

**Business impact**:

1. **Customer data corruption**: Names, addresses, comments stored incorrectly
2. **International users**: Application unusable for non-English users
3. **No error detection**: Silent corruption discovered only when users complain
4. **Irreversible data loss**: Original characters cannot be recovered once corrupted

**Common scenarios**:

1. **International names**: User names with non-ASCII characters
   ```sql
   DECLARE @Name VARCHAR(100);
   SET @Name = N'François Müller';  -- May lose characters during conversion
   ```

2. **User-generated content**: Comments, reviews, messages with emoji
   ```sql
   DECLARE @Comment VARCHAR(500);
   SET @Name = N'Great product! 😀👍';  -- May lose characters during conversion
   ```

3. **Multi-language applications**: Supporting Japanese, Chinese, Arabic, etc.
   ```sql
   DECLARE @Description VARCHAR(1000);
   SET @Description = N'製品の説明';  -- May lose characters during conversion
   ```

**Fix: Use NVARCHAR/NCHAR**:

```sql
DECLARE @Name NVARCHAR(100);  -- Supports Unicode
SET @Name = N'François Müller';  -- Stored correctly
SELECT @Name;  -- Returns 'François Müller'
```

## Examples

### Bad

```sql
-- Japanese text in VARCHAR (corrupted to '?????')
DECLARE @Greeting VARCHAR(50);
SET @Greeting = N'こんにちは';  -- May lose characters during conversion

-- Chinese text in VARCHAR
DECLARE @Name VARCHAR(100);
SET @Name = N'张伟';  -- May lose characters during conversion

-- Arabic text in VARCHAR
DECLARE @Message VARCHAR(200);
SET @Message = N'مرحبا بك';  -- May lose characters during conversion

-- Emoji in VARCHAR
DECLARE @Comment VARCHAR(500);
SET @Comment = N'Great! 😀👍';  -- May lose characters during conversion

-- Accented characters in VARCHAR
DECLARE @CustomerName VARCHAR(100);
SET @CustomerName = N'François Müller';  -- May lose characters during conversion

-- Mathematical symbols in VARCHAR
DECLARE @Formula VARCHAR(100);
SET @Formula = N'Sum: ∑(x) ≠ ∞';  -- May lose characters during conversion

-- Multi-language product description
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Description VARCHAR(1000)  -- Wrong: Cannot store international text
);
INSERT INTO Products (ProductId, Description)
VALUES (1, N'高品質の製品');  -- May lose characters during conversion

-- User comments with emoji
CREATE TABLE Comments (
    CommentId INT PRIMARY KEY,
    CommentText VARCHAR(MAX)  -- Wrong: MAX doesn't fix encoding issue
);
INSERT INTO Comments (CommentId, CommentText)
VALUES (1, N'Amazing product! 🎉❤️');  -- May lose characters during conversion
```

### Good

```sql
-- Japanese text in NVARCHAR (stored correctly)
DECLARE @Greeting NVARCHAR(50);
SET @Greeting = N'こんにちは';  -- Stored as 'こんにちは'

-- Chinese text in NVARCHAR
DECLARE @Name NVARCHAR(100);
SET @Name = N'张伟';  -- Stored as '张伟'

-- Arabic text in NVARCHAR
DECLARE @Message NVARCHAR(200);
SET @Message = N'مرحبا بك';  -- Stored as 'مرحبا بك'

-- Emoji in NVARCHAR
DECLARE @Comment NVARCHAR(500);
SET @Comment = N'Great! 😀👍';  -- Stored as 'Great! 😀👍'

-- Accented characters in NVARCHAR
DECLARE @CustomerName NVARCHAR(100);
SET @CustomerName = N'François Müller';  -- Stored as 'François Müller'

-- Mathematical symbols in NVARCHAR
DECLARE @Formula NVARCHAR(100);
SET @Formula = N'Sum: ∑(x) ≠ ∞';  -- Stored as 'Sum: ∑(x) ≠ ∞'

-- Multi-language product description
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Description NVARCHAR(1000)  -- Correct: Supports all languages
);
INSERT INTO Products (ProductId, Description)
VALUES (1, N'高品質の製品');  -- Stored correctly

-- User comments with emoji
CREATE TABLE Comments (
    CommentId INT PRIMARY KEY,
    CommentText NVARCHAR(MAX)  -- Correct: Supports Unicode
);
INSERT INTO Comments (CommentId, CommentText)
VALUES (1, N'Amazing product! 🎉❤️');  -- Stored correctly

-- Mixed English and international text
DECLARE @FullName NVARCHAR(200);
SET @FullName = N'John Doe (ジョン・ドウ)';  -- Stored correctly

-- ASCII-only text can use VARCHAR (safe)
DECLARE @StateCode VARCHAR(2);  -- OK: Only storing 'CA', 'NY', etc.
SET @StateCode = 'CA';

-- Fixed-length codes (ASCII-only)
DECLARE @CountryCode VARCHAR(3);  -- OK: 'USA', 'GBR', 'JPN' (ISO codes)
SET @CountryCode = 'USA';
```

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": {
    "semantic-unicode-string": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
