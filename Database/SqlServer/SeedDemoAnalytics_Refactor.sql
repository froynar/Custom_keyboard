:ON ERROR EXIT
-- Guarded launcher for optional analytics demo data.
-- Requires schema version 2026.07.27-simplified-read-views and all preceding migration
-- postflight contracts.
-- Run from the repository root with sqlcmd -b. The DML body is included only
-- after all schema postflights succeed.

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
    THROW 51000, 'SeedDemoAnalytics_Refactor.sql requires an explicit application database.', 1;
END;
GO

:r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql
GO
:r Database\SqlServer\SeedDemoAnalytics_Refactor.Body.sql
GO
:r Database\SqlServer\VerifySimplifiedReadViews_Postflight.sql
