:ON ERROR EXIT
-- Final read-only verification for schema version 2026.07.27-simplified-read-views.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52900, 'VerifySimplifiedReadViews_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-simplified-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52901, 'Schema version 2026.07.27-simplified-read-views is not recorded.', 1;
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
    THROW 52912, 'device_test_sessions.completed_at does not match nullable datetime2(7).', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND name = N'CK_dts_completed_at'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
BEGIN
    THROW 52913, 'CK_dts_completed_at is missing, disabled, or untrusted.', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE (status = 'Running' AND completed_at IS NOT NULL)
       OR (status <> 'Running' AND completed_at IS NULL)
)
BEGIN
    THROW 52914, 'QC completion timestamps violate the lifecycle contract.', 1;
END;

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52902, 'One or more simplified read views are missing.', 1;
END;
GO

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Last_QC')) <> 19
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Catalog_Comps')) <> 7
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Build_items')) <> 17
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Req_view')) <> 15
BEGIN
    THROW 52903, 'One or more simplified views have an unexpected column count.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (VALUES
        (
            N'Last_QC',
            N'session_id,request_id,device_id,switch_technology,noise_requirement,total_keys,status,completed_at,tested_keys,passed_keys,warning_keys,failed_keys,average_latency_ms,max_latency_ms,average_noise_db,max_noise_db,last_recorded_at,is_complete,is_acceptable'
        ),
        (
            N'Catalog_Comps',
            N'component_type,component_id,name,brand_id,brand_name,price_usd,is_available'
        ),
        (
            N'Build_items',
            N'build_item_id,build_id,switch_id,keycap_id,stab_id,accessory_id,component_type,component_id,component_name,brand_id,brand_name,quantity,unit_price_snapshot,line_total_snapshot,current_price_usd,is_available,notes'
        ),
        (
            N'Req_view',
            N'request_id,build_id,seller_user_id,seller_shop_name,request_payload_json,status,note,requested_at,accepted_at,completed_at,updated_at,buyer_id,total_cost_snapshot,kit_id,kit_name'
        )
    ) AS expected_view(view_name, expected_columns)
    OUTER APPLY (
        SELECT
            STRING_AGG(CONVERT(nvarchar(max), column_info.name), N',')
                WITHIN GROUP (ORDER BY column_info.column_id) AS actual_columns
        FROM sys.views AS view_info
        INNER JOIN sys.schemas AS schema_info
            ON schema_info.schema_id = view_info.schema_id
        INNER JOIN sys.columns AS column_info
            ON column_info.object_id = view_info.object_id
        WHERE schema_info.name = N'views'
          AND view_info.name = expected_view.view_name
    ) AS actual_view
    WHERE actual_view.actual_columns IS NULL
       OR actual_view.actual_columns <> expected_view.expected_columns
)
BEGIN
    THROW 52915, 'One or more simplified view column contracts do not match.', 1;
END;
GO

IF (SELECT COUNT_BIG(*) FROM views.Last_QC) <>
   (SELECT COUNT_BIG(DISTINCT request_id) FROM dbo.device_test_sessions)
BEGIN
    THROW 52904, 'views.Last_QC does not cover every request with QC history.', 1;
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
                    session_row.id DESC
            ) AS latest_rank
        FROM dbo.device_test_sessions AS session_row
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
    THROW 52905, 'views.Last_QC does not select the expected latest session.', 1;
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
    THROW 52906, 'views.Catalog_Comps does not cover the complete catalog.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Build_items) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_items)
BEGIN
    THROW 52907, 'views.Build_items does not resolve every build item.', 1;
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
    THROW 52908, 'views.Build_items.line_total_snapshot is not decimal(28,2).', 1;
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
    THROW 52909, 'views.Build_items line totals do not match historical quantity and price.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Req_view) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_requests)
BEGIN
    THROW 52910, 'views.Req_view does not cover every build request.', 1;
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
    THROW 52911, 'A Completed request does not have an acceptable latest QC session.', 1;
END;

PRINT 'Simplified read-view postflight passed.';
