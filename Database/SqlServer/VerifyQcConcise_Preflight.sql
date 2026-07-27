:ON ERROR EXIT
-- Read-only preflight for the concise QC migration.
-- Run with an explicit -d target. This script never changes data or schema.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51100, 'VerifyQcConcise_Preflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.15-pk-id'
         AND succeeded = 1
   )
BEGIN
    THROW 51101, 'Concise QC migration requires completed schema version 2026.07.15-pk-id.', 1;
END;

IF OBJECT_ID(N'dbo.device_test_sessions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.device_key_test_results', N'U') IS NULL
BEGIN
    THROW 51102, 'Required QC tables are missing.', 1;
END;

IF COL_LENGTH(N'dbo.device_key_test_results', N'latency_ms') IS NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'press_event_count') IS NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'release_signal_detected') IS NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'hold_duration_ms') IS NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'noise_db') IS NULL
BEGIN
    THROW 51103, 'Legacy QC columns required by the migration are missing.', 1;
END;

IF COL_LENGTH(N'dbo.device_key_test_results', N'latency') IS NOT NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'press_count') IS NOT NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'release_signal') IS NOT NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'hold_duration') IS NOT NULL
   OR COL_LENGTH(N'dbo.device_key_test_results', N'noise') IS NOT NULL
BEGIN
    THROW 51104, 'Concise QC columns already exist; inspect migration state before retrying.', 1;
END;

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
    THROW 51105, 'Legacy per-key data cannot be represented losslessly by the concise telemetry columns.', 1;
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
    THROW 51106, 'A QC session has inconsistent request/device seller ownership.', 1;
END;

SELECT
    (SELECT COUNT(*) FROM dbo.device_test_sessions) AS session_rows,
    (SELECT COUNT(*) FROM dbo.device_key_test_results) AS key_result_rows,
    (SELECT COUNT(*) FROM dbo.device_key_test_results WHERE failure_type IS NOT NULL) AS archived_failure_type_candidates,
    (SELECT COUNT(*) FROM dbo.device_key_test_results WHERE failure_reason IS NOT NULL) AS archived_failure_reason_candidates,
    (SELECT COUNT(*) FROM dbo.device_test_sessions WHERE completed_at IS NOT NULL) AS archived_completion_time_candidates;

PRINT 'Concise QC preflight passed. Take and restore-test a full backup before running the migration.';
