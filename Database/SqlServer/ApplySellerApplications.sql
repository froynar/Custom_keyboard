-- ===========================================================================
-- ApplySellerApplications.sql
-- Idempotent add of the seller_applications table to an EXISTING (already-seeded)
-- CKDB database that has already been migrated to PK id,
-- without dropping/recreating other tables.
-- (CreateSchema_Refactor.sql already contains this table for clean-machine setups;
--  this apply script is for databases created before the table was introduced.)
--
-- This is not a PK-to-id bridge migration. Run MigratePkToId_20260715.sql first
-- on legacy databases that still use users.user_id or application_id as PK names.
-- ===========================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'CKDB'
BEGIN
    THROW 51000, 'Unexpected database context for ApplySellerApplications.sql.', 1;
END;

IF COL_LENGTH(N'dbo.users', N'id') IS NULL
   OR COL_LENGTH(N'dbo.users', N'user_id') IS NOT NULL
BEGIN
    THROW 51001, 'ApplySellerApplications.sql requires the PK-to-id schema. Run MigratePkToId_20260715.sql first.', 1;
END;

DECLARE @SchemaMigrationsObjectId int = OBJECT_ID(N'dbo.schema_migrations', N'U');

IF @SchemaMigrationsObjectId IS NULL
BEGIN
    THROW 51002, 'ApplySellerApplications.sql requires schema version 2026.07.15-pk-id.', 1;
END;

IF (
       SELECT COUNT(*)
       FROM sys.columns AS column_info
       INNER JOIN sys.types AS type_info
           ON type_info.user_type_id = column_info.user_type_id
       WHERE column_info.object_id = @SchemaMigrationsObjectId
         AND (
                (column_info.name = N'version' AND type_info.name = N'varchar' AND column_info.max_length = 64 AND column_info.is_nullable = 0)
             OR (column_info.name = N'description' AND type_info.name = N'varchar' AND column_info.max_length = 255 AND column_info.is_nullable = 0)
             OR (column_info.name = N'applied_at' AND type_info.name = N'datetime2' AND column_info.scale = 7 AND column_info.is_nullable = 0)
             OR (column_info.name = N'succeeded' AND type_info.name = N'bit' AND column_info.max_length = 1 AND column_info.is_nullable = 0)
         )
   ) <> 4
   OR NOT EXISTS (
       SELECT 1
       FROM sys.indexes AS pk
       INNER JOIN sys.index_columns AS pk_column
           ON pk_column.object_id = pk.object_id
          AND pk_column.index_id = pk.index_id
          AND pk_column.key_ordinal = 1
       INNER JOIN sys.columns AS column_info
           ON column_info.object_id = pk_column.object_id
          AND column_info.column_id = pk_column.column_id
       WHERE pk.object_id = @SchemaMigrationsObjectId
         AND pk.is_primary_key = 1
         AND pk.is_disabled = 0
         AND column_info.name = N'version'
         AND (
             SELECT COUNT(*)
             FROM sys.index_columns AS all_pk_columns
             WHERE all_pk_columns.object_id = pk.object_id
               AND all_pk_columns.index_id = pk.index_id
               AND all_pk_columns.key_ordinal > 0
         ) = 1
   )
BEGIN
    THROW 51002, 'ApplySellerApplications.sql found an incompatible dbo.schema_migrations table.', 1;
END;

DECLARE @SchemaVersionFound bit = 0;
EXEC sys.sp_executesql
    N'SELECT @is_found = CASE WHEN EXISTS (
          SELECT 1 FROM dbo.schema_migrations
          WHERE version = @version AND succeeded = 1
      ) THEN 1 ELSE 0 END;',
    N'@version varchar(64), @is_found bit OUTPUT',
    @version = '2026.07.15-pk-id',
    @is_found = @SchemaVersionFound OUTPUT;

IF @SchemaVersionFound = 0
BEGIN
    THROW 51002, 'ApplySellerApplications.sql requires schema version 2026.07.15-pk-id.', 1;
END;

IF OBJECT_ID('seller_applications', 'U') IS NULL
BEGIN
    CREATE TABLE seller_applications (
        id INT IDENTITY(1,1) PRIMARY KEY,
        buyer_user_id INT NOT NULL,
        shop_name VARCHAR(255) NOT NULL,
        phone VARCHAR(30) NOT NULL,
        address VARCHAR(500) NOT NULL,
        note VARCHAR(500) NULL,
        status VARCHAR(50) NOT NULL DEFAULT 'Pending',   -- Pending, Approved, Rejected
        review_note VARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        reviewed_at DATETIME2 NULL,
        reviewed_by INT NULL,
        CONSTRAINT FK_seller_applications_buyer FOREIGN KEY (buyer_user_id) REFERENCES users(id),
        CONSTRAINT FK_seller_applications_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(id),
        CONSTRAINT CK_seller_applications_status CHECK (status IN ('Pending', 'Approved', 'Rejected'))
    );

    CREATE INDEX IX_seller_applications_status ON seller_applications(status);
    CREATE INDEX IX_seller_applications_buyer ON seller_applications(buyer_user_id);

    PRINT 'Created table seller_applications.';
END
ELSE IF COL_LENGTH(N'dbo.seller_applications', N'id') IS NULL
     OR COL_LENGTH(N'dbo.seller_applications', N'application_id') IS NOT NULL
     OR NOT EXISTS (
         SELECT 1
         FROM sys.indexes AS pk
         INNER JOIN sys.index_columns AS ic
             ON ic.object_id = pk.object_id
            AND ic.index_id = pk.index_id
            AND ic.key_ordinal = 1
         INNER JOIN sys.columns AS c
             ON c.object_id = ic.object_id
            AND c.column_id = ic.column_id
         WHERE pk.object_id = OBJECT_ID(N'dbo.seller_applications')
           AND pk.is_primary_key = 1
           AND pk.is_disabled = 0
           AND c.name = N'id'
           AND (
               SELECT COUNT(*)
               FROM sys.index_columns AS all_pk_columns
               WHERE all_pk_columns.object_id = pk.object_id
                 AND all_pk_columns.index_id = pk.index_id
                 AND all_pk_columns.key_ordinal > 0
           ) = 1
     )
BEGIN
    THROW 51003, 'Existing seller_applications table is not compatible with the PK-to-id contract.', 1;
END
ELSE
BEGIN
    PRINT 'Table seller_applications already exists with PK id; no table change.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_seller_applications_status'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    CREATE INDEX IX_seller_applications_status ON seller_applications(status);
    PRINT 'Created index IX_seller_applications_status.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_seller_applications_buyer'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    CREATE INDEX IX_seller_applications_buyer ON seller_applications(buyer_user_id);
    PRINT 'Created index IX_seller_applications_buyer.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_seller_applications_pending_buyer'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    IF EXISTS (
        SELECT buyer_user_id
        FROM seller_applications
        WHERE status = 'Pending'
        GROUP BY buyer_user_id
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51000, 'Cannot create UX_seller_applications_pending_buyer: duplicate Pending seller applications exist.', 1;
    END;

    CREATE UNIQUE INDEX UX_seller_applications_pending_buyer
    ON seller_applications(buyer_user_id)
    WHERE status = 'Pending';
    PRINT 'Created unique index UX_seller_applications_pending_buyer.';
END
GO
