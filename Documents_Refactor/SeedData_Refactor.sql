:ON ERROR EXIT
-- Custom Keyboard Builder - guarded clean/demo seed launcher.
-- Requires schema version 2026.07.27-simplified-read-views and all preceding migration
-- postflight contracts.
-- Run from the repository root with sqlcmd -b. The DML body is included only
-- after every schema postflight succeeds.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51000, 'SeedData_Refactor.sql requires an explicit application database.', 1;
END;
GO

:r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql
GO
:r Documents_Refactor\SeedData_Refactor.Body.sql
GO
:r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql
