:ON ERROR EXIT
-- Hardens the concise schema without adding redundant QC columns.
-- Run with an explicit -d target after a verified full backup/restore rehearsal.

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
    THROW 51600, 'MigrateBuildDeviceHardening_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-concise'
         AND succeeded = 1
   )
BEGIN
    THROW 51601, 'Hardening requires completed schema version 2026.07.27-qc-concise.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-build-device-hardening'
      AND succeeded = 1
)
BEGIN
    PRINT 'Build/device hardening migration already applied.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT build_id
        FROM dbo.build_requests
        WHERE status IN ('Pending', 'Accepted', 'In_progress')
        GROUP BY build_id
        HAVING COUNT(*) > 1
    )
        THROW 51602, 'Cannot harden: a build has more than one active request.', 1;

    IF EXISTS (
        SELECT seller_user_id
        FROM dbo.devices
        WHERE is_active = 1
          AND device_type = 'QC_STATION'
        GROUP BY seller_user_id
        HAVING COUNT(*) > 1
    )
        THROW 51603, 'Cannot harden: a seller has more than one active QC station.', 1;

    IF EXISTS (
        SELECT request_id
        FROM dbo.device_test_sessions
        WHERE status = 'Running'
        GROUP BY request_id
        HAVING COUNT(*) > 1
    )
        THROW 51604, 'Cannot harden: a request has more than one running QC session.', 1;

    ALTER TABLE dbo.builds ALTER COLUMN name NVARCHAR(255) NOT NULL;
    ALTER TABLE dbo.builds ALTER COLUMN notes NVARCHAR(500) NULL;
    ALTER TABLE dbo.build_items ALTER COLUMN notes NVARCHAR(500) NULL;
    ALTER TABLE dbo.build_mods ALTER COLUMN mod_type NVARCHAR(100) NOT NULL;
    ALTER TABLE dbo.build_mods ALTER COLUMN target_component NVARCHAR(100) NOT NULL;
    ALTER TABLE dbo.build_mods ALTER COLUMN notes NVARCHAR(500) NULL;
    ALTER TABLE dbo.build_requests ALTER COLUMN note NVARCHAR(500) NULL;

    -- The lost byte cannot be inferred generally. This is the single known damaged demo value
    -- identified by preflight; future Vietnamese input is preserved by NVARCHAR parameters/columns.
    UPDATE dbo.build_requests
    SET note = N'Khoa ' + NCHAR(273) + NCHAR(7865) + N'p trai'
    WHERE note = N'Khoa d?p trai';

    IF OBJECT_ID(N'dbo.CK_dts_total_keys', N'C') IS NOT NULL
        ALTER TABLE dbo.device_test_sessions DROP CONSTRAINT CK_dts_total_keys;
    IF OBJECT_ID(N'dbo.CK_dktr_press_count', N'C') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT CK_dktr_press_count;
    IF OBJECT_ID(N'dbo.CK_dktr_latency', N'C') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT CK_dktr_latency;
    IF OBJECT_ID(N'dbo.CK_dktr_hold_duration', N'C') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT CK_dktr_hold_duration;
    IF OBJECT_ID(N'dbo.CK_dktr_noise', N'C') IS NOT NULL
        ALTER TABLE dbo.device_key_test_results DROP CONSTRAINT CK_dktr_noise;

    ALTER TABLE dbo.builds WITH CHECK
        ADD CONSTRAINT CK_builds_name_not_blank CHECK (LEN(LTRIM(RTRIM(name))) > 0);
    ALTER TABLE dbo.devices WITH CHECK
        ADD CONSTRAINT CK_devices_name_not_blank CHECK (LEN(LTRIM(RTRIM(device_name))) > 0);
    ALTER TABLE dbo.device_test_sessions WITH CHECK
        ADD CONSTRAINT CK_dts_switch_technology_not_blank
            CHECK (LEN(LTRIM(RTRIM(switch_technology))) > 0);
    ALTER TABLE dbo.device_key_test_results WITH CHECK
        ADD CONSTRAINT CK_dktr_key_code_not_blank CHECK (LEN(LTRIM(RTRIM(key_code))) > 0);
    ALTER TABLE dbo.device_test_sessions WITH CHECK
        ADD CONSTRAINT CK_dts_total_keys CHECK (total_keys BETWEEN 1 AND 256);
    ALTER TABLE dbo.device_key_test_results WITH CHECK
        ADD CONSTRAINT CK_dktr_press_count CHECK (press_count BETWEEN 0 AND 100);
    ALTER TABLE dbo.device_key_test_results WITH CHECK
        ADD CONSTRAINT CK_dktr_latency CHECK (latency IS NULL OR latency BETWEEN 0 AND 10000);
    ALTER TABLE dbo.device_key_test_results WITH CHECK
        ADD CONSTRAINT CK_dktr_hold_duration
            CHECK (hold_duration IS NULL OR hold_duration BETWEEN 0 AND 3600000);
    ALTER TABLE dbo.device_key_test_results WITH CHECK
        ADD CONSTRAINT CK_dktr_noise CHECK (noise IS NULL OR noise BETWEEN 0 AND 200);

    CREATE UNIQUE INDEX UX_build_requests_one_active_per_build
        ON dbo.build_requests(build_id)
        WHERE status IN ('Pending', 'Accepted', 'In_progress');

    CREATE UNIQUE INDEX UX_devices_one_active_qc_station_per_seller
        ON dbo.devices(seller_user_id)
        WHERE is_active = 1 AND device_type = 'QC_STATION';

    CREATE UNIQUE INDEX UX_dts_one_running_per_request
        ON dbo.device_test_sessions(request_id)
        WHERE status = 'Running';

    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-build-device-hardening',
        'Atomic build requests, Unicode text, and hardened device QC invariants',
        SYSUTCDATETIME(),
        1
    );

    COMMIT TRANSACTION;
    PRINT 'Build/device hardening migration completed successfully.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
