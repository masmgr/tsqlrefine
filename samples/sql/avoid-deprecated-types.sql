-- avoid-deprecated-types rule examples
-- Detects deprecated TEXT, NTEXT, and IMAGE data types

-- BAD: TEXT column
CREATE TABLE dbo.Docs (
    Id INT NOT NULL,
    Content TEXT NOT NULL
);

-- BAD: NTEXT variable
DECLARE @notes NTEXT;

-- BAD: IMAGE parameter
CREATE PROCEDURE dbo.SavePhoto
    @photo IMAGE
AS
    SELECT 1;

-- BAD: CAST to deprecated type
SELECT CAST(Name AS TEXT) FROM dbo.Users;

-- GOOD: Use VARCHAR(MAX) instead of TEXT
CREATE TABLE dbo.DocsModern (
    Id INT NOT NULL,
    Content VARCHAR(MAX) NOT NULL
);

-- GOOD: Use NVARCHAR(MAX) instead of NTEXT
DECLARE @notes2 NVARCHAR(MAX);

-- GOOD: Use VARBINARY(MAX) instead of IMAGE
CREATE PROCEDURE dbo.SavePhotoModern
    @photo VARBINARY(MAX)
AS
    SELECT 1;
