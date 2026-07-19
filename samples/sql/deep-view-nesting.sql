CREATE VIEW dbo.LevelFour AS SELECT Id FROM dbo.BaseTable;
GO
CREATE VIEW dbo.LevelThree AS SELECT Id FROM dbo.LevelFour;
GO
CREATE VIEW dbo.LevelTwo AS SELECT Id FROM dbo.LevelThree;
GO
CREATE VIEW dbo.LevelOne AS SELECT Id FROM dbo.LevelTwo;
GO
CREATE VIEW dbo.TooDeep AS SELECT Id FROM dbo.LevelOne;
