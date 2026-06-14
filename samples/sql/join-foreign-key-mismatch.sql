-- Rule: join-foreign-key-mismatch
-- Detects JOINs where the ON columns match a foreign key relationship but the joined table differs from the FK target.

-- Bad: A.ID_B has FK to B.Id, but query joins to C instead
SELECT a.Name, c.Value
FROM dbo.TableA AS a
INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;

-- Bad: Same issue with reversed ON clause order
SELECT a.Name, c.Value
FROM dbo.TableA AS a
INNER JOIN dbo.TableC AS c ON a.ID_B = c.Id;

-- Good: Correct FK target table
SELECT a.Name, b.Value
FROM dbo.TableA AS a
INNER JOIN dbo.TableB AS b ON b.Id = a.ID_B;

-- Good: Columns not part of any FK relationship
SELECT a.Name, c.Value
FROM dbo.TableA AS a
INNER JOIN dbo.TableC AS c ON c.Value = a.Name;
