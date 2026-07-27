:ON ERROR EXIT
-- Preserving migration from the legacy QC schema to Documents/Custom_Keyboard_ERD_Final.dbml.
-- Run VerifyQcConcise_Preflight.sql first against a restored clone.
-- This migration removes redundant summary/start fields but preserves the authoritative
-- completed_at timestamp for every historical QC session.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51200, 'MigrateQcConcise_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
BEGIN
    THROW 51201, 'dbo.schema_migrations is missing.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-qc-concise'
      AND succeeded = 1
)
BEGIN
    PRINT 'Concise QC migration is already recorded; no changes were made.';
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.15-pk-id'
      AND succeeded = 1
)
BEGIN
    THROW 51202, 'Migration requires completed schema version 2026.07.15-pk-id.', 1;
END;

IF NOT EXISTS (
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
    THROW 51206, 'Legacy device_test_sessions.completed_at is missing or incompatible; refusing a lossy migration.', 1;
END;

DECLARE @SessionRowsBefore bigint = (SELECT COUNT_BIG(*) FROM dbo.device_test_sessions);
DECLARE @KeyRowsBefore bigint = (SELECT COUNT_BIG(*) FROM dbo.device_key_test_results);

IF EXISTS (
    SELECT 1
    FROM dbo.device_key_test_results AS result
    INNER JOIN dbo.device_test_sessions AS session_row
        ON session_row.id = result.session_id
    WHERE result.request_id <> session_row.request_id
       OR result.device_id <> session_row.device_id
       OR result.switch_technology <> session_row.switch_technology
       OR result.expected_key <> result.key_code
       OR ISNULL(result.bounce_count, -2147483648) <>
            ISNULL(
                CASE
                    WHEN result.release_signal_detected = 1
                        THEN CASE WHEN result.press_event_count > 0 THEN result.press_event_count - 1 ELSE 0 END
                    ELSE NULL
                END,
                -2147483648
            )
       OR result.is_stuck <>
            CASE
                WHEN result.press_signal_detected = 1
                 AND result.release_signal_detected = 0
                    THEN 1
                ELSE 0
            END
)
BEGIN
    THROW 51203, 'QC detail data is inconsistent with its owning session or concise representation.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions AS session_row
    INNER JOIN dbo.build_requests AS request_row
        ON request_row.id = session_row.request_id
    INNER JOIN dbo.devices AS device_row
        ON device_row.id = session_row.device_id
    WHERE session_row.seller_user_id <> request_row.seller_user_id
       OR session_row.seller_user_id <> device_row.seller_user_id
)
BEGIN
    THROW 51204, 'QC request/device seller ownership is inconsistent.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DROP INDEX IF EXISTS IX_dktr_request ON dbo.device_key_test_results;
    DROP INDEX IF EXISTS IX_dts_seller_status ON dbo.device_test_sessions;

    IF OBJECT_ID(N'dbo.FK_dktr_request', N'F') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT FK_dktr_request;
    IF OBJECT_ID(N'dbo.FK_dktr_device', N'F') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT FK_dktr_device;
    IF OBJECT_ID(N'dbo.FK_dts_seller', N'F') IS NOT NULL
        ALTER TABLE dbo.device_test_sessions DROP CONSTRAINT FK_dts_seller;
    IF OBJECT_ID(N'dbo.CK_dktr_failure_type', N'C') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT CK_dktr_failure_type;

    EXEC sys.sp_rename N'dbo.device_key_test_results.latency_ms', N'latency', N'COLUMN';
    EXEC sys.sp_rename N'dbo.device_key_test_results.press_event_count', N'press_count', N'COLUMN';
    EXEC sys.sp_rename N'dbo.device_key_test_results.release_signal_detected', N'release_signal', N'COLUMN';
    EXEC sys.sp_rename N'dbo.device_key_test_results.hold_duration_ms', N'hold_duration', N'COLUMN';
    EXEC sys.sp_rename N'dbo.device_key_test_results.noise_db', N'noise', N'COLUMN';

    DECLARE @DropDefaults nvarchar(max) = N'';
    SELECT @DropDefaults = @DropDefaults
        + N'ALTER TABLE '
        + QUOTENAME(OBJECT_SCHEMA_NAME(default_constraint.parent_object_id))
        + N'.'
        + QUOTENAME(OBJECT_NAME(default_constraint.parent_object_id))
        + N' DROP CONSTRAINT '
        + QUOTENAME(default_constraint.name)
        + N';'
    FROM sys.default_constraints AS default_constraint
    INNER JOIN sys.columns AS column_info
        ON column_info.object_id = default_constraint.parent_object_id
       AND column_info.column_id = default_constraint.parent_column_id
    WHERE default_constraint.parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND column_info.name IN (
          N'tested_keys', N'passed_keys', N'warning_keys', N'failed_keys', N'started_at'
      );

    IF @DropDefaults <> N''
        EXEC sys.sp_executesql @DropDefaults;

    ALTER TABLE dbo.device_key_test_results DROP COLUMN
        request_id,
        device_id,
        expected_key,
        bounce_count,
        is_stuck,
        switch_technology,
        failure_type,
        failure_reason;

    ALTER TABLE dbo.device_test_sessions DROP COLUMN
        seller_user_id,
        tested_keys,
        passed_keys,
        warning_keys,
        failed_keys,
        average_latency_ms,
        max_latency_ms,
        average_noise_db,
        max_noise_db,
        started_at;

    -- The renamed columns are resolved at execution time. Static ALTER statements
    -- in this batch would be compiled against their legacy names before sp_rename runs.
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.device_test_sessions
            ADD CONSTRAINT CK_dts_total_keys CHECK (total_keys > 0);
        ALTER TABLE dbo.device_key_test_results
            ADD CONSTRAINT CK_dktr_press_count CHECK (press_count >= 0);
        ALTER TABLE dbo.device_key_test_results
            ADD CONSTRAINT CK_dktr_latency CHECK (latency IS NULL OR latency >= 0);
        ALTER TABLE dbo.device_key_test_results
            ADD CONSTRAINT CK_dktr_hold_duration CHECK (hold_duration IS NULL OR hold_duration >= 0);
        ALTER TABLE dbo.device_key_test_results
            ADD CONSTRAINT CK_dktr_noise CHECK (noise IS NULL OR noise >= 0);
    ';

    IF (SELECT COUNT_BIG(*) FROM dbo.device_test_sessions) <> @SessionRowsBefore
       OR (SELECT COUNT_BIG(*) FROM dbo.device_key_test_results) <> @KeyRowsBefore
    BEGIN
        THROW 51205, 'QC row counts changed during migration.', 1;
    END;

    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-qc-concise',
        'Normalize QC storage to 8 session columns, 12 per-key columns, and 30 foreign keys while preserving completed_at',
        SYSUTCDATETIME(),
        1
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT 'Concise QC migration completed successfully.';
