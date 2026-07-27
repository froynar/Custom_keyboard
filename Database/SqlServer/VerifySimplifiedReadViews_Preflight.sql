:ON ERROR EXIT
-- Read-only guard before MigrateSimplifiedReadViews_20260727.sql.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52700, 'VerifySimplifiedReadViews_Preflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-view-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 52701, 'Schema version 2026.07.27-qc-view-hardening must be applied first.', 1;
END;

IF OBJECT_ID(N'dbo.device_test_sessions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.device_key_test_results', N'U') IS NULL
   OR OBJECT_ID(N'dbo.keyboard_kits', N'U') IS NULL
   OR OBJECT_ID(N'dbo.switches', N'U') IS NULL
   OR OBJECT_ID(N'dbo.keycap_sets', N'U') IS NULL
   OR OBJECT_ID(N'dbo.stabilizers', N'U') IS NULL
   OR OBJECT_ID(N'dbo.accessories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.brands', N'U') IS NULL
   OR OBJECT_ID(N'dbo.build_items', N'U') IS NULL
   OR OBJECT_ID(N'dbo.build_requests', N'U') IS NULL
   OR OBJECT_ID(N'dbo.builds', N'U') IS NULL
   OR OBJECT_ID(N'dbo.users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.seller_profiles', N'U') IS NULL
BEGIN
    THROW 52702, 'One or more tables required by the simplified views are missing.', 1;
END;

PRINT 'Simplified read-view preflight passed.';
