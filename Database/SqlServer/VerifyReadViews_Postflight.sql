:ON ERROR EXIT
-- Read-only verification after MigrateReadViews_20260727.sql.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52300, 'VerifyReadViews_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52301, 'Schema version 2026.07.27-read-views is not recorded.', 1;
END;

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52302, 'One or more required read views are missing.', 1;
END;

IF EXISTS (
    SELECT request_id
    FROM views.Last_QC
    GROUP BY request_id
    HAVING COUNT(*) <> 1
)
BEGIN
    THROW 52303, 'views.Last_QC must return at most one row per request.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Last_QC) <>
   (SELECT COUNT_BIG(DISTINCT request_id) FROM dbo.device_test_sessions)
BEGIN
    THROW 52304, 'views.Last_QC does not cover every request that has a QC session.', 1;
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
    THROW 52305, 'views.Catalog_Comps does not cover the complete component catalog.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Build_items) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_items)
BEGIN
    THROW 52306, 'views.Build_items does not resolve every build item.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Req_view) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_requests)
BEGIN
    THROW 52307, 'views.Req_view does not cover every build request.', 1;
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
    THROW 52308, 'A Completed request does not have an acceptable latest QC result.', 1;
END;

PRINT 'Read-view postflight passed.';
