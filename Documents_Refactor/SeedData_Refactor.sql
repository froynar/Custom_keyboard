:ON ERROR EXIT
-- Custom Keyboard Builder - guarded clean/demo seed launcher.
-- Requires schema version 2026.07.15-pk-id and the exact postflight contract.
-- Run from the repository root with sqlcmd -b. The DML body is included only
-- after VerifyPkToId_Postflight.sql succeeds.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'CustomKeyboard_Refactor'
BEGIN
    THROW 51000, 'Unexpected database context for SeedData_Refactor.sql.', 1;
END;
GO

:r Database\SqlServer\VerifyPkToId_Postflight.sql
GO
:r Documents_Refactor\SeedData_Refactor.Body.sql
