:ON ERROR EXIT
-- Reopens historical Completed requests whose latest QC is missing, partial, Failed,
-- or inconsistent with its key-result aggregate. The database remains concise: no
-- redundant QC columns are added.

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
    THROW 51900, 'MigrateCompletedQcReconciliation_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-build-device-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 51901, 'Build/device hardening must be applied before completed-QC reconciliation.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-completed-qc-reconciliation'
      AND succeeded = 1
)
BEGIN
    PRINT 'Completed-QC reconciliation migration already applied.';
    RETURN;
END;

DECLARE @Reconciled TABLE (
    request_id VARCHAR(50) NOT NULL PRIMARY KEY,
    target_status VARCHAR(20) NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH completed_qc AS (
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
    ),
    invalid_completed AS (
        SELECT
            id,
            build_id,
            requested_at,
            ROW_NUMBER() OVER (
                PARTITION BY build_id
                ORDER BY requested_at DESC, id DESC
            ) AS invalid_rank
        FROM completed_qc
        WHERE latest_qc_status NOT IN ('Passed', 'Warning')
           OR latest_qc_status IS NULL
           OR tested_keys <> total_keys
           OR COALESCE(failed_keys, 0) > 0
           OR (latest_qc_status = 'Passed' AND COALESCE(warning_keys, 0) > 0)
           OR (latest_qc_status = 'Warning' AND COALESCE(warning_keys, 0) = 0)
    )
    INSERT INTO @Reconciled (request_id, target_status)
    SELECT
        invalid_row.id,
        CASE
            WHEN invalid_row.invalid_rank > 1
              OR EXISTS (
                  SELECT 1
                  FROM dbo.build_requests AS active_row
                  WHERE active_row.build_id = invalid_row.build_id
                    AND active_row.id <> invalid_row.id
                    AND active_row.status IN ('Pending', 'Accepted', 'In_progress')
              )
                THEN 'Cancelled'
            ELSE 'In_progress'
        END
    FROM invalid_completed AS invalid_row;

    UPDATE request_row
    SET
        status = reconciled.target_status,
        completed_at = NULL,
        updated_at = SYSUTCDATETIME()
    FROM dbo.build_requests AS request_row
    INNER JOIN @Reconciled AS reconciled
        ON reconciled.request_id = request_row.id;

    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-completed-qc-reconciliation',
        'Reconcile historical Completed requests with latest complete QC truth',
        SYSUTCDATETIME(),
        1
    );

    COMMIT TRANSACTION;

    SELECT request_id, target_status
    FROM @Reconciled
    ORDER BY request_id;

    PRINT 'Completed-QC reconciliation migration completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
