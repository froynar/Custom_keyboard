:ON ERROR EXIT
-- Read-only preflight for reconciling historical Completed requests with QC truth.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51800, 'VerifyCompletedQcReconciliation_Preflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-build-device-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 51801, 'Build/device hardening must be applied before completed-QC reconciliation.', 1;
END;

IF OBJECT_ID(N'dbo.build_requests', N'U') IS NULL
   OR OBJECT_ID(N'dbo.device_test_sessions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.device_key_test_results', N'U') IS NULL
BEGIN
    THROW 51802, 'Required request/QC tables are missing.', 1;
END;

WITH request_qc AS (
    SELECT
        request_row.id,
        request_row.build_id,
        request_row.requested_at,
        latest_qc.status AS latest_qc_status,
        latest_qc.total_keys,
        latest_qc.tested_keys,
        latest_qc.failed_keys,
        latest_qc.warning_keys
    FROM dbo.build_requests AS request_row
    OUTER APPLY (
        SELECT TOP (1)
            session_row.status,
            session_row.total_keys,
            result_summary.tested_keys,
            result_summary.failed_keys,
            result_summary.warning_keys
        FROM dbo.device_test_sessions AS session_row
        OUTER APPLY (
            SELECT
                COUNT(result_row.id) AS tested_keys,
                SUM(CASE WHEN result_row.result = 'Fail' THEN 1 ELSE 0 END) AS failed_keys,
                SUM(CASE WHEN result_row.result = 'Warning' THEN 1 ELSE 0 END) AS warning_keys,
                MAX(result_row.recorded_at) AS last_recorded_at
            FROM dbo.device_key_test_results AS result_row
            WHERE result_row.session_id = session_row.id
        ) AS result_summary
        WHERE session_row.request_id = request_row.id
        ORDER BY
            CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
            result_summary.last_recorded_at DESC,
            session_row.id DESC
    ) AS latest_qc
    WHERE request_row.status = 'Completed'
)
SELECT
    id AS invalid_completed_request_id,
    build_id,
    latest_qc_status,
    tested_keys,
    total_keys,
    failed_keys,
    warning_keys
FROM request_qc
WHERE latest_qc_status NOT IN ('Passed', 'Warning')
   OR latest_qc_status IS NULL
   OR tested_keys <> total_keys
   OR COALESCE(failed_keys, 0) > 0
   OR (latest_qc_status = 'Passed' AND COALESCE(warning_keys, 0) > 0)
   OR (latest_qc_status = 'Warning' AND COALESCE(warning_keys, 0) = 0)
ORDER BY requested_at, id;

PRINT 'Completed-QC reconciliation preflight passed.';
