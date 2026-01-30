# Semantic Unicode String

**Rule ID:** `semantic/unicode-string`
**Category:** Correctness
**Severity:** Error
**Fixable:** Yes

## Description

Detects Unicode characters in string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss.

## Rationale

Unicode characters in non-Unicode (VARCHAR/CHAR) variables cause **silent data corruption** with no error or warning.

**Silent data corruption**:

When Unicode characters are stored in VARCHAR/CHAR variables, they are converted to `?` characters:

```sql
DECLARE @Name VARCHAR(50);
SET @Name = 'こんにちは';  -- Japanese "Hello"
SELECT @Name;               -- Returns '?????' (data corrupted!)
```

**Why this happens**:

1. **VARCHAR/CHAR encoding**: Uses single-byte or code page encoding (ASCII, Windows-1252, etc.)
   - Supports only 256 different characters (0-255)
   - Cannot represent most international characters

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
   SET @Name = 'François Müller';  -- Becomes 'Fran?ois M?ller'
   ```

2. **User-generated content**: Comments, reviews, messages with emoji
   ```sql
   DECLARE @Comment VARCHAR(500);
   SET @Name = 'Great product! 😀👍';  -- Becomes 'Great product! ??'
   ```

3. **Multi-language applications**: Supporting Japanese, Chinese, Arabic, etc.
   ```sql
   DECLARE @Description VARCHAR(1000);
   SET @Description = '製品の説明';  -- Becomes '?????'
   ```

**Fix: Use NVARCHAR/NCHAR**:

```sql
DECLARE @Name NVARCHAR(100);  -- Supports Unicode
SET @Name = 'François Müller';  -- Stored correctly
SELECT @Name;  -- Returns 'François Müller'
```

## Examples

### Bad

```sql
-- Japanese text in VARCHAR (corrupted to '?????')
DECLARE @Greeting VARCHAR(50);
SET @Greeting = 'こんにちは';  -- Stored as '?????'

-- Chinese text in VARCHAR
DECLARE @Name VARCHAR(100);
SET @Name = '张伟';  -- Stored as '??'

-- Arabic text in VARCHAR
DECLARE @Message VARCHAR(200);
SET @Message = 'مرحبا بك';  -- Stored as '???? ??'

-- Emoji in VARCHAR
DECLARE @Comment VARCHAR(500);
SET @Comment = 'Great! 😀👍';  -- Stored as 'Great! ??'

-- Accented characters in VARCHAR
DECLARE @CustomerName VARCHAR(100);
SET @CustomerName = 'François Müller';  -- Stored as 'Fran?ois M?ller'

-- Mathematical symbols in VARCHAR
DECLARE @Formula VARCHAR(100);
SET @Formula = 'Sum: ∑(x) ≠ ∞';  -- Stored as 'Sum: ?(x) ? ?'

-- Multi-language product description
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Description VARCHAR(1000)  -- Wrong: Cannot store international text
);
INSERT INTO Products (ProductId, Description)
VALUES (1, '高品質の製品');  -- Stored as '??????'

-- User comments with emoji
CREATE TABLE Comments (
    CommentId INT PRIMARY KEY,
    CommentText VARCHAR(MAX)  -- Wrong: MAX doesn't fix encoding issue
);
INSERT INTO Comments (CommentId, CommentText)
VALUES (1, 'Amazing product! 🎉❤️');  -- Stored as 'Amazing product! ??'
```

### Good

```sql
-- Japanese text in NVARCHAR (stored correctly)
DECLARE @Greeting NVARCHAR(50);
SET @Greeting = 'こんにちは';  -- Stored as 'こんにちは'

-- Chinese text in NVARCHAR
DECLARE @Name NVARCHAR(100);
SET @Name = '张伟';  -- Stored as '张伟'

-- Arabic text in NVARCHAR
DECLARE @Message NVARCHAR(200);
SET @Message = 'مرحبا بك';  -- Stored as 'مرحبا بك'

-- Emoji in NVARCHAR
DECLARE @Comment NVARCHAR(500);
SET @Comment = 'Great! 😀👍';  -- Stored as 'Great! 😀👍'

-- Accented characters in NVARCHAR
DECLARE @CustomerName NVARCHAR(100);
SET @CustomerName = 'François Müller';  -- Stored as 'François Müller'

-- Mathematical symbols in NVARCHAR
DECLARE @Formula NVARCHAR(100);
SET @Formula = 'Sum: ∑(x) ≠ ∞';  -- Stored as 'Sum: ∑(x) ≠ ∞'

-- Multi-language product description
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Description NVARCHAR(1000)  -- Correct: Supports all languages
);
INSERT INTO Products (ProductId, Description)
VALUES (1, '高品質の製品');  -- Stored correctly

-- User comments with emoji
CREATE TABLE Comments (
    CommentId INT PRIMARY KEY,
    CommentText NVARCHAR(MAX)  -- Correct: Supports Unicode
);
INSERT INTO Comments (CommentId, CommentText)
VALUES (1, 'Amazing product! 🎉❤️');  -- Stored correctly

-- Mixed English and international text
DECLARE @FullName NVARCHAR(200);
SET @FullName = 'John Doe (ジョン・ドウ)';  -- Stored correctly

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
  "rules": [
    { "id": "semantic/unicode-string", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
