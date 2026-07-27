:ON ERROR EXIT
-- Final read-only verification for schema version 2026.07.27-qc-view-hardening.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52600, 'VerifyQcViewHardening_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-view-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 52601, 'Schema version 2026.07.27-qc-view-hardening is not recorded.', 1;
END;
GO

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
    THROW 52602, 'device_test_sessions.completed_at does not match nullable datetime2(7).', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND name = N'CK_dts_completed_at'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
BEGIN
    THROW 52603, 'CK_dts_completed_at is missing, disabled, or untrusted.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints AS check_info
    CROSS APPLY (
        VALUES (
            LOWER(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(check_info.definition, N' ', N''),
                                N'[', N''
                            ),
                            N']', N''
                        ),
                        CHAR(13), N''
                    ),
                    CHAR(10), N''
                )
            )
        )
    ) AS normalized(definition)
    WHERE check_info.parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND check_info.name = N'CK_dts_completed_at'
      AND (
             normalized.definition NOT LIKE N'%status%running%'
          OR normalized.definition NOT LIKE N'%status%passed%'
          OR normalized.definition NOT LIKE N'%status%warning%'
          OR normalized.definition NOT LIKE N'%status%failed%'
          OR normalized.definition NOT LIKE N'%completed_atisnull%'
          OR normalized.definition NOT LIKE N'%completed_atisnotnull%'
      )
)
BEGIN
    THROW 52612, 'CK_dts_completed_at definition does not enforce the final lifecycle states.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE (status = 'Running' AND completed_at IS NOT NULL)
       OR (status <> 'Running' AND completed_at IS NULL)
)
BEGIN
    THROW 52604, 'QC completion timestamps violate the lifecycle contract.', 1;
END;

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52605, 'One or more final read views are missing.', 1;
END;
GO

IF (
    SELECT COUNT(*)
    FROM (
        VALUES
            (OBJECT_ID(N'views.Last_QC'), N'completed_at'),
            (OBJECT_ID(N'views.Req_view'), N'qc_completed_at')
    ) AS expected_column(object_id, column_name)
    INNER JOIN sys.columns AS column_info
        ON column_info.object_id = expected_column.object_id
       AND column_info.name = expected_column.column_name
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE type_info.name = N'datetime2'
      AND column_info.scale = 7
      AND column_info.is_nullable = 1
) <> 2
BEGIN
    THROW 52613, 'QC completion-time view columns do not match datetime2(7).', 1;
END;
GO

IF (SELECT COUNT_BIG(*) FROM views.Last_QC) <>
   (SELECT COUNT_BIG(DISTINCT request_id) FROM dbo.device_test_sessions)
BEGIN
    THROW 52606, 'views.Last_QC does not cover every request with QC history.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT
            session_row.request_id,
            session_row.id AS session_id,
            session_row.completed_at,
            ROW_NUMBER() OVER (
                PARTITION BY session_row.request_id
                ORDER BY
                    CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
                    session_row.completed_at DESC,
                    result_summary.last_recorded_at DESC,
                    session_row.id DESC
            ) AS latest_rank
        FROM dbo.device_test_sessions AS session_row
        OUTER APPLY (
            SELECT MAX(result_row.recorded_at) AS last_recorded_at
            FROM dbo.device_key_test_results AS result_row
            WHERE result_row.session_id = session_row.id
        ) AS result_summary
    ) AS expected_latest
    WHERE expected_latest.latest_rank = 1
      AND NOT EXISTS (
          SELECT 1
          FROM views.Last_QC AS actual_latest
          WHERE actual_latest.request_id = expected_latest.request_id
            AND actual_latest.session_id = expected_latest.session_id
            AND (
                  actual_latest.completed_at = expected_latest.completed_at
               OR (
                      actual_latest.completed_at IS NULL
                  AND expected_latest.completed_at IS NULL
               )
            )
      )
)
BEGIN
    THROW 52607, 'views.Last_QC does not select the expected latest session.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Catalog_Comps) <>
   (
       (SELECT COUNT_BIG(*) FROM dbo.keyboard_kits)
     + (SELECT COUNT_BIG(*) FROM dbo.switches)
     + (SELECT COUNT_BIG(*) FROM dbo.keycap_sets)
     + (SELECT COUNT_BIG(*) FROM dbo.stabilizers)
     + (SELECT COUNT_BIG(*) FROM dbo.accessories)
   )
BEGIN
    THROW 52608, 'views.Catalog_Comps does not cover the complete catalog.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Build_items) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_items)
BEGIN
    THROW 52609, 'views.Build_items does not resolve every build item.', 1;
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
    THROW 52610, 'views.Build_items.line_total_snapshot is not decimal(28,2).', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM views.Build_items AS projected_item
    INNER JOIN dbo.build_items AS source_item
        ON source_item.id = projected_item.build_item_id
    WHERE projected_item.line_total_snapshot <>
          CAST(
              CAST(source_item.quantity AS decimal(18,0))
              * CAST(source_item.unit_price_snapshot AS decimal(18,2))
              AS decimal(28,2)
          )
)
BEGIN
    THROW 52614, 'views.Build_items line totals do not match historical quantity and price.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Req_view) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_requests)
BEGIN
    THROW 52611, 'views.Req_view does not cover every build request.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM views.Req_view AS request_view
    LEFT JOIN views.Last_QC AS latest_qc
        ON latest_qc.request_id = request_view.request_id
    WHERE request_view.qc_completed_at <> latest_qc.completed_at
       OR (
              request_view.qc_completed_at IS NULL
          AND latest_qc.completed_at IS NOT NULL
       )
       OR (
              request_view.qc_completed_at IS NOT NULL
          AND latest_qc.completed_at IS NULL
       )
)
BEGIN
    THROW 52615, 'views.Req_view does not preserve the latest QC completion time.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.build_requests AS request_row
    LEFT JOIN views.Last_QC AS latest_qc
        ON latest_qc.request_id = request_row.id
    WHERE request_row.status = 'Completed'
      AND COALESCE(latest_qc.is_acceptable, 0) = 0
)
BEGIN
    THROW 52616, 'A Completed request does not have an acceptable latest QC session.', 1;
END;

PRINT 'QC/view hardening postflight passed.';
