:ON ERROR EXIT
-- Read-only preflight for build/request and device/QC hardening.
-- Run with an explicit -d target after 2026.07.27-qc-concise.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51500, 'VerifyBuildDeviceHardening_Preflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-concise'
         AND succeeded = 1
   )
BEGIN
    THROW 51501, 'Hardening requires completed schema version 2026.07.27-qc-concise.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.builds'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.build_items'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.build_mods'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.build_requests'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.devices'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.device_test_sessions'))
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.device_key_test_results'))
BEGIN
    THROW 51502, 'One or more build/device tables are missing.', 1;
END;

IF EXISTS (
    SELECT build_id
    FROM dbo.build_requests
    WHERE status IN ('Pending', 'Accepted', 'In_progress')
    GROUP BY build_id
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51503, 'A build has more than one active request.', 1;
END;

IF EXISTS (
    SELECT seller_user_id
    FROM dbo.devices
    WHERE is_active = 1
      AND device_type = 'QC_STATION'
    GROUP BY seller_user_id
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51504, 'A seller has more than one active QC station.', 1;
END;

IF EXISTS (
    SELECT request_id
    FROM dbo.device_test_sessions
    WHERE status = 'Running'
    GROUP BY request_id
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51505, 'A request has more than one running QC session.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE total_keys NOT BETWEEN 1 AND 256
)
   OR EXISTS (
       SELECT 1
       FROM dbo.device_key_test_results
       WHERE press_count NOT BETWEEN 0 AND 100
          OR latency NOT BETWEEN 0 AND 10000
          OR noise NOT BETWEEN 0 AND 200
          OR hold_duration NOT BETWEEN 0 AND 3600000
   )
BEGIN
    THROW 51506, 'Existing QC measurements are outside the hardened accepted range.', 1;
END;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.builds) AS build_rows,
    (SELECT COUNT_BIG(*) FROM dbo.build_requests) AS request_rows,
    (SELECT COUNT_BIG(*) FROM dbo.devices) AS device_rows,
    (SELECT COUNT_BIG(*) FROM dbo.device_test_sessions) AS session_rows,
    (SELECT COUNT_BIG(*) FROM dbo.device_key_test_results) AS key_result_rows,
    (SELECT COUNT_BIG(*) FROM dbo.build_requests WHERE note = 'Khoa d?p trai') AS known_unicode_repair_rows;

PRINT 'Build/device hardening preflight passed.';
