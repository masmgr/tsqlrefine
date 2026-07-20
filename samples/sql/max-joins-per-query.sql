SELECT a.Id
FROM dbo.A AS a
JOIN dbo.B AS b ON b.Id = a.Id
JOIN dbo.C AS c ON c.Id = b.Id;
