:ON ERROR EXIT
-- Read-only postflight for build/request and device/QC hardening.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51700, 'VerifyBuildDeviceHardening_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-build-device-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 51701, 'Schema version 2026.07.27-build-device-hardening is not recorded.', 1;
END;

DECLARE @ExpectedUnicodeColumns TABLE (
    table_name sysname NOT NULL,
    column_name sysname NOT NULL,
    max_length smallint NOT NULL,
    is_nullable bit NOT NULL,
    PRIMARY KEY (table_name, column_name)
);

INSERT INTO @ExpectedUnicodeColumns (table_name, column_name, max_length, is_nullable)
VALUES
    (N'builds', N'name', 510, 0),
    (N'builds', N'notes', 1000, 1),
    (N'build_items', N'notes', 1000, 1),
    (N'build_mods', N'mod_type', 200, 0),
    (N'build_mods', N'target_component', 200, 0),
    (N'build_mods', N'notes', 1000, 1),
    (N'build_requests', N'note', 1000, 1);

IF EXISTS (
    SELECT 1
    FROM @ExpectedUnicodeColumns AS expected
    LEFT JOIN sys.tables AS table_info
        ON table_info.name = expected.table_name
       AND table_info.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.columns AS column_info
        ON column_info.object_id = table_info.object_id
       AND column_info.name = expected.column_name
    LEFT JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.column_id IS NULL
       OR type_info.name <> N'nvarchar'
       OR column_info.max_length <> expected.max_length
       OR column_info.is_nullable <> expected.is_nullable
)
BEGIN
    THROW 51702, 'One or more build/request text columns are not the expected NVARCHAR shape.', 1;
END;

IF EXISTS (
    SELECT expected.index_name
    FROM (VALUES
        (N'UX_build_requests_one_active_per_build'),
        (N'UX_devices_one_active_qc_station_per_seller'),
        (N'UX_dts_one_running_per_request')
    ) AS expected(index_name)
    LEFT JOIN sys.indexes AS index_info
        ON index_info.name = expected.index_name
    WHERE index_info.index_id IS NULL
       OR index_info.is_unique <> 1
       OR index_info.has_filter <> 1
       OR index_info.is_disabled <> 0
)
BEGIN
    THROW 51703, 'One or more build/device concurrency guard indexes are missing or disabled.', 1;
END;

IF EXISTS (
    SELECT expected.constraint_name
    FROM (VALUES
        (N'CK_builds_name_not_blank'),
        (N'CK_devices_name_not_blank'),
        (N'CK_dts_switch_technology_not_blank'),
        (N'CK_dts_total_keys'),
        (N'CK_dktr_key_code_not_blank'),
        (N'CK_dktr_press_count'),
        (N'CK_dktr_latency'),
        (N'CK_dktr_hold_duration'),
        (N'CK_dktr_noise')
    ) AS expected(constraint_name)
    LEFT JOIN sys.check_constraints AS check_info
        ON check_info.name = expected.constraint_name
    WHERE check_info.object_id IS NULL
       OR check_info.is_disabled <> 0
       OR check_info.is_not_trusted <> 0
)
BEGIN
    THROW 51704, 'One or more build/device check constraints are missing, disabled, or untrusted.', 1;
END;

IF EXISTS (
    SELECT build_id
    FROM dbo.build_requests
    WHERE status IN ('Pending', 'Accepted', 'In_progress')
    GROUP BY build_id
    HAVING COUNT(*) > 1
)
   OR EXISTS (
       SELECT seller_user_id
       FROM dbo.devices
       WHERE is_active = 1
         AND device_type = 'QC_STATION'
       GROUP BY seller_user_id
       HAVING COUNT(*) > 1
   )
   OR EXISTS (
       SELECT request_id
       FROM dbo.device_test_sessions
       WHERE status = 'Running'
       GROUP BY request_id
       HAVING COUNT(*) > 1
   )
BEGIN
    THROW 51705, 'A protected build/device uniqueness invariant is violated.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.build_requests
    WHERE id = 'REQ_8afe9c8f50b84c7f9e477b2999bb598b'
      AND (
          note IS NULL
          OR note <> N'Khoa ' + NCHAR(273) + NCHAR(7865) + N'p trai'
      )
)
BEGIN
    THROW 51706, 'The known damaged Vietnamese request note was not repaired.', 1;
END;

PRINT 'Build/device hardening postflight passed.';
