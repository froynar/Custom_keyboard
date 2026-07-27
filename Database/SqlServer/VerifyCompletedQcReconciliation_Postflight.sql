:ON ERROR EXIT
-- Read-only postflight for the Completed request / latest QC business invariant.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52000, 'VerifyCompletedQcReconciliation_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-completed-qc-reconciliation'
         AND succeeded = 1
   )
BEGIN
    THROW 52001, 'Schema version 2026.07.27-completed-qc-reconciliation is not recorded.', 1;
END;

IF EXISTS (
    SELECT 1
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
      AND (
          latest_qc.status NOT IN ('Passed', 'Warning')
          OR latest_qc.status IS NULL
          OR latest_qc.tested_keys <> latest_qc.total_keys
          OR COALESCE(latest_qc.failed_keys, 0) > 0
          OR (latest_qc.status = 'Passed' AND COALESCE(latest_qc.warning_keys, 0) > 0)
          OR (latest_qc.status = 'Warning' AND COALESCE(latest_qc.warning_keys, 0) = 0)
      )
)
BEGIN
    THROW 52002, 'A Completed request lacks a complete Passed/Warning latest QC result.', 1;
END;

PRINT 'Completed-QC reconciliation postflight passed.';
