-- ===========================================================================
-- VerifyPkToId_Postflight.sql
-- Read-only verification to run immediately after MigratePkToId_20260715.sql.
--
-- Save this output and compare row counts / identity counters with the
-- VerifyPkToId_Preflight.sql output from the same maintenance window.
-- This file intentionally has no USE statement. Select the target explicitly
-- with sqlcmd -d (or the SSMS database selector), including for a restore clone.
-- ===========================================================================

SET NOCOUNT ON;

DECLARE @CurrentDatabase sysname = DB_NAME();
DECLARE @ExpectedForeignKeyCount int = 33;
DECLARE @ExpectedPkCount int = 21;
DECLARE @TargetSchemaVersion varchar(64) = '2026.07.15-pk-id';

IF @CurrentDatabase IS NULL
   OR @CurrentDatabase IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51000, 'Select the intended business database explicitly before running PK-to-id postflight.', 1;
END;

PRINT N'PK-to-id postflight target: ' + QUOTENAME(@CurrentDatabase);

DECLARE @SchemaMigrationsObjectId int = OBJECT_ID(N'dbo.schema_migrations', N'U');

IF @SchemaMigrationsObjectId IS NULL
BEGIN
    THROW 51001, 'Postflight failed: target schema migration version is not recorded.', 1;
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
    THROW 51001, 'Postflight failed: dbo.schema_migrations has an incompatible shape.', 1;
END;

DECLARE @MigrationRecorded bit = 0;
EXEC sys.sp_executesql
    N'SELECT @is_recorded = CASE WHEN EXISTS (
          SELECT 1 FROM dbo.schema_migrations
          WHERE version = @version AND succeeded = 1
      ) THEN 1 ELSE 0 END;',
    N'@version varchar(64), @is_recorded bit OUTPUT',
    @version = @TargetSchemaVersion,
    @is_recorded = @MigrationRecorded OUTPUT;

IF @MigrationRecorded = 0
BEGIN
    THROW 51001, 'Postflight failed: target schema migration version is not recorded.', 1;
END;

DECLARE @ExpectedPk TABLE (
    table_name sysname NOT NULL PRIMARY KEY,
    legacy_pk_name sysname NOT NULL,
    expected_type sysname NOT NULL,
    expected_max_length smallint NOT NULL,
    expected_identity bit NOT NULL
);

INSERT INTO @ExpectedPk (table_name, legacy_pk_name, expected_type, expected_max_length, expected_identity)
VALUES
    (N'roles', N'role_id', N'int', 4, 1),
    (N'users', N'user_id', N'int', 4, 1),
    (N'seller_profiles', N'seller_profile_id', N'int', 4, 1),
    (N'seller_applications', N'application_id', N'int', 4, 1),
    (N'brands', N'brand_id', N'int', 4, 1),
    (N'layouts', N'layout_id', N'varchar', 50, 0),
    (N'keyboard_kits', N'kit_id', N'varchar', 50, 0),
    (N'switches', N'switch_id', N'varchar', 50, 0),
    (N'keycap_sets', N'keycap_id', N'varchar', 50, 0),
    (N'stabilizers', N'stab_id', N'varchar', 50, 0),
    (N'accessories', N'accessory_id', N'varchar', 50, 0),
    (N'builds', N'build_id', N'varchar', 50, 0),
    (N'build_items', N'build_item_id', N'int', 4, 1),
    (N'build_mods', N'mod_id', N'int', 4, 1),
    (N'build_requests', N'request_id', N'varchar', 50, 0),
    (N'devices', N'device_id', N'varchar', 50, 0),
    (N'device_test_sessions', N'session_id', N'varchar', 50, 0),
    (N'device_key_test_results', N'key_test_id', N'bigint', 8, 1),
    (N'audit_log', N'log_id', N'int', 4, 1),
    (N'chat_conversations', N'conversation_id', N'varchar', 50, 0),
    (N'chat_messages', N'message_id', N'varchar', 50, 0);

IF (SELECT COUNT(*) FROM @ExpectedPk) <> @ExpectedPkCount
BEGIN
    THROW 51002, 'Internal postflight mapping does not contain 21 PK entries.', 1;
END;

DECLARE @ExpectedForeignKey TABLE (
    parent_table_name sysname NOT NULL,
    parent_column_name sysname NOT NULL,
    referenced_table_name sysname NOT NULL,
    PRIMARY KEY (parent_table_name, parent_column_name)
);

INSERT INTO @ExpectedForeignKey (parent_table_name, parent_column_name, referenced_table_name)
VALUES
    (N'users', N'role_id', N'roles'),
    (N'seller_profiles', N'user_id', N'users'),
    (N'seller_applications', N'buyer_user_id', N'users'),
    (N'seller_applications', N'reviewed_by', N'users'),
    (N'keyboard_kits', N'brand_id', N'brands'),
    (N'keyboard_kits', N'layout_id', N'layouts'),
    (N'switches', N'brand_id', N'brands'),
    (N'keycap_sets', N'brand_id', N'brands'),
    (N'stabilizers', N'brand_id', N'brands'),
    (N'builds', N'buyer_id', N'users'),
    (N'builds', N'kit_id', N'keyboard_kits'),
    (N'build_items', N'build_id', N'builds'),
    (N'build_items', N'switch_id', N'switches'),
    (N'build_items', N'keycap_id', N'keycap_sets'),
    (N'build_items', N'stab_id', N'stabilizers'),
    (N'build_items', N'accessory_id', N'accessories'),
    (N'build_mods', N'build_id', N'builds'),
    (N'build_requests', N'build_id', N'builds'),
    (N'build_requests', N'seller_user_id', N'users'),
    (N'devices', N'seller_user_id', N'users'),
    (N'device_test_sessions', N'request_id', N'build_requests'),
    (N'device_test_sessions', N'device_id', N'devices'),
    (N'device_test_sessions', N'seller_user_id', N'users'),
    (N'device_key_test_results', N'session_id', N'device_test_sessions'),
    (N'device_key_test_results', N'request_id', N'build_requests'),
    (N'device_key_test_results', N'device_id', N'devices'),
    (N'audit_log', N'user_id', N'users'),
    (N'chat_conversations', N'seller_user_id', N'users'),
    (N'chat_conversations', N'buyer_id', N'users'),
    (N'chat_conversations', N'admin_user_id', N'users'),
    (N'chat_conversations', N'build_request_id', N'build_requests'),
    (N'chat_messages', N'conversation_id', N'chat_conversations'),
    (N'chat_messages', N'sender_user_id', N'users');

IF (SELECT COUNT(*) FROM @ExpectedForeignKey) <> @ExpectedForeignKeyCount
BEGIN
    THROW 51002, 'Internal postflight mapping does not contain 33 FK entries.', 1;
END;

WITH ActualPk AS (
    SELECT
        e.table_name,
        e.legacy_pk_name,
        e.expected_type,
        e.expected_max_length,
        e.expected_identity,
        t.object_id,
        pk_column.name AS pk_column_name,
        type_info.name AS pk_type_name,
        pk_column.max_length AS pk_max_length,
        CONVERT(bit, COLUMNPROPERTY(t.object_id, pk_column.name, 'IsIdentity')) AS pk_is_identity,
        CONVERT(bit, ISNULL(pk.is_disabled, 0)) AS pk_is_disabled,
        CASE WHEN legacy_column.column_id IS NULL THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS has_legacy_column,
        (
            SELECT COUNT(*)
            FROM sys.index_columns AS all_pk_columns
            WHERE all_pk_columns.object_id = t.object_id
              AND all_pk_columns.index_id = pk.index_id
              AND all_pk_columns.key_ordinal > 0
        ) AS pk_key_column_count
    FROM @ExpectedPk AS e
    LEFT JOIN sys.tables AS t
        ON t.name = e.table_name
       AND t.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.indexes AS pk
        ON pk.object_id = t.object_id
       AND pk.is_primary_key = 1
    LEFT JOIN sys.index_columns AS pk_index_column
        ON pk_index_column.object_id = t.object_id
       AND pk_index_column.index_id = pk.index_id
       AND pk_index_column.key_ordinal = 1
    LEFT JOIN sys.columns AS pk_column
        ON pk_column.object_id = t.object_id
       AND pk_column.column_id = pk_index_column.column_id
    LEFT JOIN sys.types AS type_info
        ON type_info.user_type_id = pk_column.user_type_id
    LEFT JOIN sys.columns AS legacy_column
        ON legacy_column.object_id = t.object_id
       AND legacy_column.name = e.legacy_pk_name
)
SELECT
    table_name,
    legacy_pk_name,
    pk_column_name AS actual_pk_name,
    pk_type_name,
    pk_max_length,
    pk_is_identity,
    pk_is_disabled,
    has_legacy_column,
    pk_key_column_count
FROM ActualPk
ORDER BY table_name;

IF EXISTS (
    SELECT 1
    FROM @ExpectedPk AS e
    LEFT JOIN sys.tables AS t
        ON t.name = e.table_name
       AND t.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.indexes AS pk
        ON pk.object_id = t.object_id
       AND pk.is_primary_key = 1
    LEFT JOIN sys.index_columns AS pk_index_column
        ON pk_index_column.object_id = t.object_id
       AND pk_index_column.index_id = pk.index_id
       AND pk_index_column.key_ordinal = 1
    LEFT JOIN sys.columns AS pk_column
        ON pk_column.object_id = t.object_id
       AND pk_column.column_id = pk_index_column.column_id
    LEFT JOIN sys.types AS type_info
        ON type_info.user_type_id = pk_column.user_type_id
    LEFT JOIN sys.columns AS legacy_column
        ON legacy_column.object_id = t.object_id
       AND legacy_column.name = e.legacy_pk_name
    WHERE t.object_id IS NULL
       OR pk_column.name <> N'id'
       OR legacy_column.column_id IS NOT NULL
       OR type_info.name <> e.expected_type
       OR pk_column.max_length <> e.expected_max_length
       OR CONVERT(bit, COLUMNPROPERTY(t.object_id, pk_column.name, 'IsIdentity')) <> e.expected_identity
       OR ISNULL(pk.is_disabled, 0) <> 0
       OR (
            SELECT COUNT(*)
            FROM sys.index_columns AS all_pk_columns
            WHERE all_pk_columns.object_id = t.object_id
              AND all_pk_columns.index_id = pk.index_id
              AND all_pk_columns.key_ordinal > 0
       ) <> 1
)
BEGIN
    THROW 51003, 'Postflight failed: PK metadata is not the expected id state.', 1;
END;

DECLARE @ForeignKeyCount int;
DECLARE @InvalidForeignKeyStateCount int;
DECLARE @NonIdForeignKeyTargetCount int;
DECLARE @MissingForeignKeyCount int;
DECLARE @UnexpectedForeignKeyCount int;

DECLARE @ActualForeignKey TABLE (
    parent_table_name sysname NOT NULL,
    parent_column_name sysname NOT NULL,
    referenced_schema_name sysname NOT NULL,
    referenced_table_name sysname NOT NULL,
    referenced_column_name sysname NOT NULL,
    is_disabled bit NOT NULL,
    is_not_trusted bit NOT NULL
);

INSERT INTO @ActualForeignKey (
    parent_table_name,
    parent_column_name,
    referenced_schema_name,
    referenced_table_name,
    referenced_column_name,
    is_disabled,
    is_not_trusted
)
SELECT
    parent_table.name,
    parent_column.name,
    referenced_schema.name,
    referenced_table.name,
    referenced_column.name,
    fk.is_disabled,
    fk.is_not_trusted
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.tables AS parent_table
    ON parent_table.object_id = fk.parent_object_id
   AND parent_table.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.columns AS parent_column
    ON parent_column.object_id = fkc.parent_object_id
   AND parent_column.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS referenced_table
    ON referenced_table.object_id = fk.referenced_object_id
INNER JOIN sys.schemas AS referenced_schema
    ON referenced_schema.schema_id = referenced_table.schema_id
INNER JOIN sys.columns AS referenced_column
    ON referenced_column.object_id = fkc.referenced_object_id
   AND referenced_column.column_id = fkc.referenced_column_id
WHERE parent_table.name IN (SELECT table_name FROM @ExpectedPk);

SELECT
    @ForeignKeyCount = COUNT(*),
    @InvalidForeignKeyStateCount = ISNULL(SUM(CASE WHEN is_disabled = 1 OR is_not_trusted = 1 THEN 1 ELSE 0 END), 0),
    @NonIdForeignKeyTargetCount = ISNULL(SUM(CASE WHEN referenced_schema_name <> N'dbo' OR referenced_column_name <> N'id' THEN 1 ELSE 0 END), 0)
FROM @ActualForeignKey;

SELECT @MissingForeignKeyCount = COUNT(*)
FROM @ExpectedForeignKey AS expected
WHERE NOT EXISTS (
    SELECT 1
    FROM @ActualForeignKey AS actual
    WHERE actual.parent_table_name = expected.parent_table_name
      AND actual.parent_column_name = expected.parent_column_name
      AND actual.referenced_schema_name = N'dbo'
      AND actual.referenced_table_name = expected.referenced_table_name
      AND actual.referenced_column_name = N'id'
);

SELECT @UnexpectedForeignKeyCount = COUNT(*)
FROM @ActualForeignKey AS actual
WHERE NOT EXISTS (
    SELECT 1
    FROM @ExpectedForeignKey AS expected
    WHERE actual.parent_table_name = expected.parent_table_name
      AND actual.parent_column_name = expected.parent_column_name
      AND actual.referenced_schema_name = N'dbo'
      AND actual.referenced_table_name = expected.referenced_table_name
      AND actual.referenced_column_name = N'id'
);

SELECT
    @ForeignKeyCount AS fk_count,
    @InvalidForeignKeyStateCount AS disabled_or_untrusted_fk_count,
    @NonIdForeignKeyTargetCount AS non_id_target_fk_count,
    @MissingForeignKeyCount AS missing_fk_count,
    @UnexpectedForeignKeyCount AS unexpected_fk_count;

IF @ForeignKeyCount <> @ExpectedForeignKeyCount
   OR @MissingForeignKeyCount <> 0
   OR @UnexpectedForeignKeyCount <> 0
BEGIN
    THROW 51004, 'Postflight failed: the 33 foreign-key relationships do not match the id contract.', 1;
END;

IF @InvalidForeignKeyStateCount <> 0
BEGIN
    THROW 51005, 'Postflight failed: at least one foreign key is disabled or untrusted.', 1;
END;

IF @NonIdForeignKeyTargetCount <> 0
BEGIN
    THROW 51006, 'Postflight failed: at least one foreign key does not target id.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.audit_log
    WHERE table_name = 'seller_profiles'
      AND (TRY_CONVERT(int, record_id) IS NULL OR TRY_CONVERT(int, record_id) <= 0)
)
BEGIN
    THROW 51009, 'Postflight failed: seller_profiles audit record_id must use the integer profile PK.', 1;
END;

PRINT 'Row counts at postflight time. Compare with preflight output.';
SELECT e.table_name, SUM(p.rows) AS row_count
FROM @ExpectedPk AS e
INNER JOIN sys.tables AS t
    ON t.name = e.table_name
   AND t.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.partitions AS p
    ON p.object_id = t.object_id
   AND p.index_id IN (0, 1)
GROUP BY e.table_name
ORDER BY e.table_name;

PRINT 'Identity counters at postflight time. Compare with preflight output.';
SELECT
    e.table_name,
    IDENT_CURRENT(SCHEMA_NAME(t.schema_id) + N'.' + t.name) AS identity_current
FROM @ExpectedPk AS e
INNER JOIN sys.tables AS t
    ON t.name = e.table_name
   AND t.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.columns AS c
    ON c.object_id = t.object_id
WHERE COLUMNPROPERTY(t.object_id, c.name, 'IsIdentity') = 1
ORDER BY e.table_name;

PRINT 'FK orphan checks. Every orphan_count must be 0.';
CREATE TABLE #OrphanCheck (
    fk varchar(200) NOT NULL PRIMARY KEY,
    orphan_count bigint NOT NULL
);

DECLARE @OrphanSql nvarchar(max);

SELECT @OrphanSql = STRING_AGG(
    CONVERT(nvarchar(max),
        N'INSERT INTO #OrphanCheck (fk, orphan_count) '
        + N'SELECT N''' + REPLACE(parent_table_name + N'.' + parent_column_name, N'''', N'''''') + N''', COUNT_BIG(*) '
        + N'FROM dbo.' + QUOTENAME(parent_table_name) + N' AS child '
        + N'LEFT JOIN dbo.' + QUOTENAME(referenced_table_name) + N' AS referenced '
        + N'ON referenced.[id] = child.' + QUOTENAME(parent_column_name) + N' '
        + N'WHERE child.' + QUOTENAME(parent_column_name) + N' IS NOT NULL '
        + N'AND referenced.[id] IS NULL;'),
    NCHAR(10)
)
FROM @ExpectedForeignKey;

EXEC sys.sp_executesql @OrphanSql;

SELECT fk, orphan_count
FROM #OrphanCheck
ORDER BY fk;

IF EXISTS (SELECT 1 FROM #OrphanCheck WHERE orphan_count <> 0)
BEGIN
    THROW 51007, 'Postflight failed: at least one foreign-key orphan was found.', 1;
END;

PRINT 'DBCC CHECKCONSTRAINTS should return no rows.';
DECLARE @ConstraintViolation TABLE (
    table_name nvarchar(776) NULL,
    constraint_name nvarchar(776) NULL,
    where_clause nvarchar(max) NULL
);

INSERT INTO @ConstraintViolation (table_name, constraint_name, where_clause)
EXEC (N'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;');

SELECT table_name, constraint_name, where_clause
FROM @ConstraintViolation;

IF EXISTS (SELECT 1 FROM @ConstraintViolation)
BEGIN
    THROW 51008, 'Postflight failed: DBCC CHECKCONSTRAINTS found at least one violation.', 1;
END;

PRINT 'PK-to-id postflight passed. Compare row counts and identity counters with preflight before reopening the app.';
