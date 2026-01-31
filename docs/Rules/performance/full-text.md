# Full Text

**Rule ID:** `full-text`
**Category:** Performance
**Severity:** Warning
**Fixable:** No

## Description

Prohibit full-text search predicates; use alternative search strategies for better performance

## Rationale

This rule **flags** full-text search predicates (`CONTAINS`, `FREETEXT`, `CONTAINSTABLE`, `FREETEXTTABLE`) to encourage review of whether full-text indexes are truly beneficial:

**Important**: This is an **informational** rule - full-text search is a legitimate SQL Server feature, but it's often **misused or unnecessary**:

1. **Maintenance overhead**:
   - Full-text indexes require separate maintenance and population schedules
   - Can become out-of-sync with base tables
   - Population can be CPU and I/O intensive
   - Requires additional disk space

2. **Complexity**:
   - Requires creating full-text catalogs and indexes
   - Different query syntax than regular T-SQL
   - More difficult to troubleshoot performance issues
   - Requires understanding of word breakers, stoplists, and thesaurus

3. **Limited use cases**: Full-text is beneficial only for:
   - Large text documents (>1KB per row)
   - Natural language search with stemming/inflections
   - Proximity searches and thesaurus lookups

**When full-text is NOT needed:**
- Simple prefix searches: `LIKE 'abc%'` (SARGable with index)
- Exact matches: `WHERE column = 'value'`
- Small text columns (<100 characters)
- Structured data with known patterns

**Better alternatives for common scenarios:**
- **Prefix search**: `LIKE 'term%'` with regular index
- **Contains substring**: `LIKE '%term%'` (use only if necessary, not SARGable)
- **Pattern matching**: Regular expressions via CLR function
- **External search**: Elasticsearch, Azure Cognitive Search for advanced scenarios

## Examples

### Bad

```sql
-- Full-text for simple prefix search (overkill)
SELECT * FROM products
WHERE CONTAINS(product_name, '"laptop*"');  -- Just use LIKE 'laptop%'

-- Full-text on short columns (unnecessary complexity)
SELECT * FROM users
WHERE CONTAINS(username, 'john');  -- username is 50 chars, use LIKE

-- Multiple full-text predicates (performance issues)
SELECT *
FROM documents d
WHERE CONTAINS(d.title, 'report')
  AND CONTAINS(d.content, 'financial')
  AND CONTAINS(d.tags, 'Q1');  -- Consider consolidating

-- Full-text on frequently updated tables (index churn)
SELECT * FROM chat_messages
WHERE FREETEXT(message_text, 'hello world');  -- Messages inserted constantly
```

### Good

```sql
-- Simple prefix search with regular index
CREATE INDEX IX_products_name ON products(product_name);

SELECT * FROM products
WHERE product_name LIKE 'laptop%';  -- SARGable with index

-- Exact match with regular index
SELECT * FROM users
WHERE username = 'john123';

-- If full-text is truly needed (large documents, natural language)
-- Create full-text catalog and index first
CREATE FULLTEXT CATALOG DocumentCatalog;
GO

CREATE FULLTEXT INDEX ON documents(content)
KEY INDEX PK_documents
ON DocumentCatalog
WITH CHANGE_TRACKING AUTO;
GO

-- Then use full-text search for complex natural language queries
SELECT d.title, d.author, FT_TBL.RANK
FROM documents d
INNER JOIN CONTAINSTABLE(documents, content,
    'NEAR((legal, contract), 5)') AS FT_TBL
    ON d.document_id = FT_TBL.[KEY]
WHERE FT_TBL.RANK > 50
ORDER BY FT_TBL.RANK DESC;

-- For simple substring search (if truly needed)
SELECT * FROM products
WHERE product_description LIKE '%waterproof%';  -- Not SARGable but simpler

-- External search service for advanced scenarios
-- (pseudo-code - query Elasticsearch/Azure Search via application code)
-- results = searchService.Search("waterproof laptops")
-- SELECT * FROM products WHERE product_id IN (@results)
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
    { "id": "full-text", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
