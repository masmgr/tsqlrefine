-- require-drop-if-exists: Requires IF EXISTS on DROP statements

-- Bad: DROP without IF EXISTS can fail if object doesn't exist
DROP TABLE dbo.Users;
DROP PROCEDURE dbo.MyProc;
DROP VIEW dbo.MyView;
DROP FUNCTION dbo.MyFunc;

-- Good: DROP IF EXISTS is idempotent
DROP TABLE IF EXISTS dbo.Users;
DROP PROCEDURE IF EXISTS dbo.MyProc;
DROP VIEW IF EXISTS dbo.MyView;
DROP FUNCTION IF EXISTS dbo.MyFunc;
