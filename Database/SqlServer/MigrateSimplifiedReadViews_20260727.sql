:ON ERROR EXIT
-- Replaces the four read views with smaller, purpose-focused contracts.
-- All four view changes and the migration marker commit atomically.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52800, 'MigrateSimplifiedReadViews_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-view-hardening'
         AND succeeded = 1
   )
BEGIN
    THROW 52801, 'Schema version 2026.07.27-qc-view-hardening must be applied first.', 1;
END;

BEGIN TRANSACTION;
GO

:r Database\SqlServer\ReadViews_Simplified_20260727.sql

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52802, 'Simplified read-view migration did not create all four views.', 1;
END;

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Last_QC')) <> 19
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Catalog_Comps')) <> 7
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Build_items')) <> 17
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Req_view')) <> 15
BEGIN
    THROW 52803, 'One or more simplified views have an unexpected column count.', 1;
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
    THROW 52804, 'views.Build_items.line_total_snapshot must remain decimal(28,2).', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-simplified-read-views'
      AND succeeded = 1
)
BEGIN
    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-simplified-read-views',
        'Simplify the four read-view contracts without changing application behavior',
        SYSUTCDATETIME(),
        1
    );
END;

COMMIT TRANSACTION;
GO

PRINT 'Simplified read-view migration completed successfully.';
