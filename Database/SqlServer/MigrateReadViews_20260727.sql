:ON ERROR EXIT
-- Adds four read-only projections. The migration is atomic across all four
-- CREATE OR ALTER batches; sqlcmd exits and SQL Server rolls back on failure.
-- Run from the repository root so the sqlcmd include path resolves correctly.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52200, 'MigrateReadViews_20260727.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-completed-qc-reconciliation'
         AND succeeded = 1
   )
BEGIN
    THROW 52201, 'Completed/QC reconciliation must be applied before the read views.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-qc-view-hardening'
      AND succeeded = 1
)
BEGIN
    THROW 52203, 'Read-view baseline cannot be reapplied after QC/view hardening.', 1;
END;

BEGIN TRANSACTION;
GO

:r Database\SqlServer\ReadViews_20260727.sql

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52202, 'Read-view migration did not create all four views.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.schema_migrations
    WHERE version = '2026.07.27-read-views'
      AND succeeded = 1
)
BEGIN
    INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
    VALUES (
        '2026.07.27-read-views',
        'Add Last_QC, Catalog_Comps, Build_items, and Req_view read projections',
        SYSUTCDATETIME(),
        1
    );
END;

COMMIT TRANSACTION;
GO

PRINT 'Read-view migration completed successfully.';
