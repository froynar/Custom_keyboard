:ON ERROR EXIT
-- Read-only guard before MigrateQcViewHardening_20260727.sql.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52400, 'VerifyQcViewHardening_Preflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52401, 'Schema version 2026.07.27-read-views must be applied first.', 1;
END;

IF OBJECT_ID(N'dbo.device_test_sessions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.device_key_test_results', N'U') IS NULL
   OR OBJECT_ID(N'dbo.build_requests', N'U') IS NULL
   OR OBJECT_ID(N'dbo.builds', N'U') IS NULL
   OR OBJECT_ID(N'dbo.build_items', N'U') IS NULL
   OR OBJECT_ID(N'dbo.brands', N'U') IS NULL
   OR OBJECT_ID(N'dbo.keyboard_kits', N'U') IS NULL
   OR OBJECT_ID(N'dbo.switches', N'U') IS NULL
   OR OBJECT_ID(N'dbo.keycap_sets', N'U') IS NULL
   OR OBJECT_ID(N'dbo.stabilizers', N'U') IS NULL
   OR OBJECT_ID(N'dbo.accessories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.seller_profiles', N'U') IS NULL
BEGIN
    THROW 52402, 'One or more tables required by QC/view hardening are missing.', 1;
END;

IF COL_LENGTH(N'dbo.device_test_sessions', N'completed_at') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns AS column_info
       INNER JOIN sys.types AS type_info
           ON type_info.user_type_id = column_info.user_type_id
       WHERE column_info.object_id = OBJECT_ID(N'dbo.device_test_sessions')
         AND column_info.name = N'completed_at'
         AND type_info.name = N'datetime2'
         AND column_info.scale = 7
         AND column_info.is_nullable = 1
   )
BEGIN
    THROW 52403, 'Existing device_test_sessions.completed_at has an incompatible shape.', 1;
END;

PRINT 'QC/view hardening preflight passed.';
