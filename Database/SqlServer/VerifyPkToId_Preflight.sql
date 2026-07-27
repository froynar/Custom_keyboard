-- ===========================================================================
-- VerifyPkToId_Preflight.sql
-- Read-only guard to run before MigratePkToId_20260715.sql.
--
-- This script expects the legacy runtime schema: all 21 business-table primary
-- keys still use their old names, and no owner table already has an id PK.
-- It does not write data and must be run after a verified backup/restore drill.
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
    THROW 51000, 'Select the intended business database explicitly before running PK-to-id preflight.', 1;
END;

PRINT N'PK-to-id preflight target: ' + QUOTENAME(@CurrentDatabase);

DECLARE @SchemaMigrationsObjectId int = OBJECT_ID(N'dbo.schema_migrations', N'U');

IF @SchemaMigrationsObjectId IS NOT NULL
BEGIN
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
        THROW 51001, 'Preflight failed: dbo.schema_migrations has an incompatible shape.', 1;
    END;

    DECLARE @MigrationAlreadyApplied bit = 0;
    EXEC sys.sp_executesql
        N'SELECT @is_applied = CASE WHEN EXISTS (
              SELECT 1 FROM dbo.schema_migrations
              WHERE version = @version AND succeeded = 1
          ) THEN 1 ELSE 0 END;',
        N'@version varchar(64), @is_applied bit OUTPUT',
        @version = @TargetSchemaVersion,
        @is_applied = @MigrationAlreadyApplied OUTPUT;

    IF @MigrationAlreadyApplied = 1
    BEGIN
        THROW 51001, 'PK-to-id migration version is already recorded; do not run legacy preflight.', 1;
    END;
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
    THROW 51002, 'Internal preflight mapping does not contain 21 PK entries.', 1;
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
    THROW 51002, 'Internal preflight mapping does not contain 33 FK entries.', 1;
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
        CASE WHEN id_column.column_id IS NULL THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS has_id_column,
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
    LEFT JOIN sys.columns AS id_column
        ON id_column.object_id = t.object_id
       AND id_column.name = N'id'
)
SELECT
    table_name,
    legacy_pk_name,
    pk_column_name AS actual_pk_name,
    pk_type_name,
    pk_max_length,
    pk_is_identity,
    pk_is_disabled,
    has_id_column,
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
    LEFT JOIN sys.columns AS id_column
        ON id_column.object_id = t.object_id
       AND id_column.name = N'id'
    WHERE t.object_id IS NULL
       OR pk_column.name <> e.legacy_pk_name
       OR id_column.column_id IS NOT NULL
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
    THROW 51003, 'Preflight failed: PK metadata is not the expected legacy state.', 1;
END;

DECLARE @ForeignKeyCount int;
DECLARE @InvalidForeignKeyStateCount int;
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
    @InvalidForeignKeyStateCount = ISNULL(SUM(CASE WHEN is_disabled = 1 OR is_not_trusted = 1 THEN 1 ELSE 0 END), 0)
FROM @ActualForeignKey;

SELECT @MissingForeignKeyCount = COUNT(*)
FROM @ExpectedForeignKey AS expected
INNER JOIN @ExpectedPk AS referenced_pk
    ON referenced_pk.table_name = expected.referenced_table_name
WHERE NOT EXISTS (
    SELECT 1
    FROM @ActualForeignKey AS actual
    WHERE actual.parent_table_name = expected.parent_table_name
      AND actual.parent_column_name = expected.parent_column_name
      AND actual.referenced_schema_name = N'dbo'
      AND actual.referenced_table_name = expected.referenced_table_name
      AND actual.referenced_column_name = referenced_pk.legacy_pk_name
);

SELECT @UnexpectedForeignKeyCount = COUNT(*)
FROM @ActualForeignKey AS actual
WHERE NOT EXISTS (
    SELECT 1
    FROM @ExpectedForeignKey AS expected
    INNER JOIN @ExpectedPk AS referenced_pk
        ON referenced_pk.table_name = expected.referenced_table_name
    WHERE actual.parent_table_name = expected.parent_table_name
      AND actual.parent_column_name = expected.parent_column_name
      AND actual.referenced_schema_name = N'dbo'
      AND actual.referenced_table_name = expected.referenced_table_name
      AND actual.referenced_column_name = referenced_pk.legacy_pk_name
);

IF @ForeignKeyCount <> @ExpectedForeignKeyCount
   OR @MissingForeignKeyCount <> 0
   OR @UnexpectedForeignKeyCount <> 0
BEGIN
    THROW 51004, 'Preflight failed: the 33 foreign-key relationships do not match the legacy contract.', 1;
END;

IF @InvalidForeignKeyStateCount <> 0
BEGIN
    THROW 51005, 'Preflight failed: at least one foreign key is disabled or untrusted.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM @ExpectedPk
    WHERE ISNULL(HAS_PERMS_BY_NAME(N'dbo.' + table_name, N'OBJECT', N'ALTER'), 0) <> 1
)
BEGIN
    THROW 51006, 'Preflight failed: the current principal lacks ALTER permission on at least one business table.', 1;
END;

IF ISNULL(HAS_PERMS_BY_NAME(N'dbo.audit_log', N'OBJECT', N'UPDATE'), 0) <> 1
BEGIN
    THROW 51009, 'Preflight failed: the current principal lacks UPDATE permission needed to normalize audit record IDs.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM sys.sql_expression_dependencies AS dependency
    INNER JOIN sys.tables AS referenced_table
        ON referenced_table.object_id = dependency.referenced_id
       AND referenced_table.schema_id = SCHEMA_ID(N'dbo')
    INNER JOIN sys.columns AS referenced_column
        ON referenced_column.object_id = dependency.referenced_id
       AND referenced_column.column_id = dependency.referenced_minor_id
    INNER JOIN @ExpectedPk AS expected
        ON expected.table_name = referenced_table.name
       AND expected.legacy_pk_name = referenced_column.name
)
BEGIN
    THROW 51007, 'Preflight failed: a SQL module or expression depends on a legacy PK column.', 1;
END;

DECLARE @SellerProfileAuditNormalization TABLE (
    log_id int NOT NULL PRIMARY KEY,
    current_record_id varchar(100) NOT NULL,
    resolved_profile_id int NULL,
    resolution_source varchar(30) NOT NULL
);

INSERT INTO @SellerProfileAuditNormalization (
    log_id,
    current_record_id,
    resolved_profile_id,
    resolution_source
)
SELECT
    audit_entry.log_id,
    audit_entry.record_id,
    COALESCE(
        json_ids.new_profile_id,
        json_ids.old_profile_id,
        profile_by_username.seller_profile_id,
        TRY_CONVERT(int, audit_entry.record_id)
    ),
    CASE
        WHEN json_ids.new_profile_id IS NOT NULL THEN 'new_value_json'
        WHEN json_ids.old_profile_id IS NOT NULL THEN 'old_value_json'
        WHEN profile_by_username.seller_profile_id IS NOT NULL THEN 'username'
        WHEN TRY_CONVERT(int, audit_entry.record_id) IS NOT NULL THEN 'already_numeric'
        ELSE 'unresolved'
    END
FROM dbo.audit_log AS audit_entry
LEFT JOIN dbo.users AS user_by_username
    ON user_by_username.username = audit_entry.record_id
LEFT JOIN dbo.seller_profiles AS profile_by_username
    ON profile_by_username.user_id = user_by_username.user_id
OUTER APPLY (VALUES (
    TRY_CONVERT(int, CASE WHEN ISJSON(audit_entry.new_value_json) = 1 THEN COALESCE(
        JSON_VALUE(audit_entry.new_value_json, '$.SellerProfileId'),
        JSON_VALUE(audit_entry.new_value_json, '$.sellerProfileId')
    ) END),
    TRY_CONVERT(int, CASE WHEN ISJSON(audit_entry.old_value_json) = 1 THEN COALESCE(
        JSON_VALUE(audit_entry.old_value_json, '$.SellerProfileId'),
        JSON_VALUE(audit_entry.old_value_json, '$.sellerProfileId')
    ) END)
)) AS json_ids (new_profile_id, old_profile_id)
WHERE audit_entry.table_name = 'seller_profiles';

IF EXISTS (
    SELECT 1
    FROM @SellerProfileAuditNormalization
    WHERE resolved_profile_id IS NULL OR resolved_profile_id <= 0
)
BEGIN
    THROW 51008, 'Preflight failed: a seller_profiles audit record_id cannot be normalized to its integer PK.', 1;
END;

PRINT 'seller_profiles audit record IDs that migration will normalize.';
SELECT
    log_id,
    current_record_id,
    resolved_profile_id,
    resolution_source
FROM @SellerProfileAuditNormalization
WHERE current_record_id <> CONVERT(varchar(100), resolved_profile_id)
ORDER BY log_id;

PRINT 'Row counts at preflight time. Save this output for postflight comparison.';
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

PRINT 'Identity counters at preflight time. Save this output for postflight comparison.';
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

PRINT 'PK-to-id preflight passed. The database is in the expected legacy state.';
