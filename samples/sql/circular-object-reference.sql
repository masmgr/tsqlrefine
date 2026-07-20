CREATE VIEW dbo.CycleFirst AS SELECT Id FROM dbo.CycleSecond;
GO
CREATE VIEW dbo.CycleSecond AS SELECT Id FROM dbo.CycleFirst;
