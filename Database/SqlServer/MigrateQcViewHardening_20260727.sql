:ON ERROR EXIT
-- Adds one authoritative QC completion timestamp and upgrades all read views.
-- The table change, four CREATE OR ALTER batches, constraint, and migration
-- marker are committed atomically. Run from the repository root in sqlcmd mode.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52500, 'MigrateQcViewHardening_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52501, 'Schema version 2026.07.27-read-views must be applied first.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-simplified-read-views'
      AND succeeded = 1
)
BEGIN
    THROW 52507, 'QC/view hardening cannot be reapplied after simplified read views.', 1;
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
    THROW 52506, 'Existing device_test_sessions.completed_at has an incompatible shape.', 1;
END;

BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.device_test_sessions', N'completed_at') IS NULL
BEGIN
    ALTER TABLE dbo.device_test_sessions
        ADD completed_at DATETIME2 NULL;
END;
GO

UPDATE dbo.device_test_sessions
SET completed_at = NULL
WHERE status = 'Running'
  AND completed_at IS NOT NULL;

UPDATE session_row
SET completed_at = COALESCE(
    result_summary.last_recorded_at,
    request_row.completed_at,
    request_row.updated_at,
    request_row.accepted_at,
    request_row.requested_at
)
FROM dbo.device_test_sessions AS session_row
INNER JOIN dbo.build_requests AS request_row
    ON request_row.id = session_row.request_id
OUTER APPLY (
    SELECT MAX(result_row.recorded_at) AS last_recorded_at
    FROM dbo.device_key_test_results AS result_row
    WHERE result_row.session_id = session_row.id
) AS result_summary
WHERE session_row.status <> 'Running'
  AND session_row.completed_at IS NULL;

IF OBJECT_ID(N'dbo.CK_dts_completed_at', N'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.device_test_sessions
        DROP CONSTRAINT CK_dts_completed_at;
END;

ALTER TABLE dbo.device_test_sessions WITH CHECK
    ADD CONSTRAINT CK_dts_completed_at CHECK (
        (status = 'Running' AND completed_at IS NULL)
        OR (status IN ('Passed','Warning','Failed') AND completed_at IS NOT NULL)
    );

ALTER TABLE dbo.device_test_sessions
    CHECK CONSTRAINT CK_dts_completed_at;
GO

:r Database\SqlServer\ReadViews_QcViewHardening_20260727.sql

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52502, 'QC/view hardening did not create all four final views.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE (status = 'Running' AND completed_at IS NOT NULL)
       OR (status <> 'Running' AND completed_at IS NULL)
)
BEGIN
    THROW 52503, 'QC completion timestamps violate the final lifecycle contract.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns AS column_info
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'views.Build_items')
      AND column_info.name = N'line_total_snapshot'
      AND type_info.name = N'decimal'
      AND column_info.precision = 28
      AND column_info.scale = 2
)
BEGIN
    THROW 52504, 'views.Build_items.line_total_snapshot is not decimal(28,2).', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'views.Last_QC')
      AND name = N'completed_at'
)
   OR NOT EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'views.Req_view')
         AND name = N'qc_completed_at'
   )
BEGIN
    THROW 52505, 'Final QC completion-time projections are missing.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-qc-view-hardening'
      AND succeeded = 1
)
BEGIN
    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-qc-view-hardening',
        'Add QC completion time and harden read-view contracts',
        SYSUTCDATETIME(),
        1
    );
END;

COMMIT TRANSACTION;
GO

PRINT 'QC/view hardening migration completed successfully.';
