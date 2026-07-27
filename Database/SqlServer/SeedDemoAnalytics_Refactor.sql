:ON ERROR EXIT
-- Guarded launcher for optional analytics demo data.
-- Requires schema version 2026.07.15-pk-id and the exact postflight contract.
-- Run from the repository root with sqlcmd -b. The DML body is included only
-- after VerifyPkToId_Postflight.sql succeeds.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'CustomKeyboard_Refactor'
BEGIN
    THROW 51000, 'Unexpected database context for SeedDemoAnalytics_Refactor.sql.', 1;
END;
GO

:r Database\SqlServer\VerifyPkToId_Postflight.sql
GO
:r Database\SqlServer\SeedDemoAnalytics_Refactor.Body.sql
