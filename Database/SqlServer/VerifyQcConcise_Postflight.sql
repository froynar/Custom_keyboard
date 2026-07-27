:ON ERROR EXIT
-- Read-only postflight for clean installs and migrated concise QC databases.
-- Run with an explicit -d target.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51300, 'VerifyQcConcise_Postflight.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-qc-concise'
         AND succeeded = 1
   )
BEGIN
    THROW 51301, 'Schema version 2026.07.27-qc-concise is not recorded.', 1;
END;

DECLARE @ExpectedSessionColumns TABLE (
    column_name sysname PRIMARY KEY,
    type_name sysname NOT NULL,
    max_length smallint NOT NULL,
    expected_precision tinyint NULL,
    expected_scale tinyint NULL,
    is_nullable bit NOT NULL,
    is_identity bit NOT NULL
);
INSERT INTO @ExpectedSessionColumns (
    column_name, type_name, max_length, expected_precision, expected_scale, is_nullable, is_identity
)
VALUES
    (N'id', N'varchar', 50, NULL, NULL, 0, 0),
    (N'request_id', N'varchar', 50, NULL, NULL, 0, 0),
    (N'device_id', N'varchar', 50, NULL, NULL, 0, 0),
    (N'switch_technology', N'varchar', 50, NULL, NULL, 0, 0),
    (N'noise_requirement', N'varchar', 20, NULL, NULL, 0, 0),
    (N'total_keys', N'int', 4, NULL, NULL, 0, 0),
    (N'status', N'varchar', 20, NULL, NULL, 0, 0),
    (N'completed_at', N'datetime2', 8, NULL, 7, 1, 0);

DECLARE @ExpectedResultColumns TABLE (
    column_name sysname PRIMARY KEY,
    type_name sysname NOT NULL,
    max_length smallint NOT NULL,
    expected_precision tinyint NULL,
    expected_scale tinyint NULL,
    is_nullable bit NOT NULL,
    is_identity bit NOT NULL
);
INSERT INTO @ExpectedResultColumns (
    column_name, type_name, max_length, expected_precision, expected_scale, is_nullable, is_identity
)
VALUES
    (N'id', N'bigint', 8, NULL, NULL, 0, 1),
    (N'session_id', N'varchar', 50, NULL, NULL, 0, 0),
    (N'key_code', N'varchar', 30, NULL, NULL, 0, 0),
    (N'received_key', N'varchar', 30, NULL, NULL, 1, 0),
    (N'press_signal_detected', N'bit', 1, NULL, NULL, 0, 0),
    (N'latency', N'decimal', 5, 8, 2, 1, 0),
    (N'press_count', N'int', 4, NULL, NULL, 0, 0),
    (N'release_signal', N'bit', 1, NULL, NULL, 0, 0),
    (N'hold_duration', N'int', 4, NULL, NULL, 1, 0),
    (N'noise', N'decimal', 5, 8, 2, 1, 0),
    (N'result', N'varchar', 20, NULL, NULL, 0, 0),
    (N'recorded_at', N'datetime2', 8, NULL, 7, 0, 0);

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.device_test_sessions')) <> 8
   OR EXISTS (
       SELECT 1
       FROM @ExpectedSessionColumns AS expected
       LEFT JOIN sys.columns AS column_info
           ON column_info.object_id = OBJECT_ID(N'dbo.device_test_sessions')
          AND column_info.name = expected.column_name
       LEFT JOIN sys.types AS type_info
           ON type_info.user_type_id = column_info.user_type_id
       WHERE column_info.column_id IS NULL
          OR type_info.name <> expected.type_name
          OR column_info.max_length <> expected.max_length
          OR (expected.expected_precision IS NOT NULL AND column_info.precision <> expected.expected_precision)
          OR (expected.expected_scale IS NOT NULL AND column_info.scale <> expected.expected_scale)
          OR column_info.is_nullable <> expected.is_nullable
          OR CONVERT(bit, COLUMNPROPERTY(column_info.object_id, column_info.name, 'IsIdentity')) <> expected.is_identity
   )
BEGIN
    THROW 51302, 'device_test_sessions does not match the exact 8-column concise contract.', 1;
END;

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.device_key_test_results')) <> 12
   OR EXISTS (
       SELECT 1
       FROM @ExpectedResultColumns AS expected
       LEFT JOIN sys.columns AS column_info
           ON column_info.object_id = OBJECT_ID(N'dbo.device_key_test_results')
          AND column_info.name = expected.column_name
       LEFT JOIN sys.types AS type_info
           ON type_info.user_type_id = column_info.user_type_id
       WHERE column_info.column_id IS NULL
          OR type_info.name <> expected.type_name
          OR column_info.max_length <> expected.max_length
          OR (expected.expected_precision IS NOT NULL AND column_info.precision <> expected.expected_precision)
          OR (expected.expected_scale IS NOT NULL AND column_info.scale <> expected.expected_scale)
          OR column_info.is_nullable <> expected.is_nullable
          OR CONVERT(bit, COLUMNPROPERTY(column_info.object_id, column_info.name, 'IsIdentity')) <> expected.is_identity
   )
BEGIN
    THROW 51303, 'device_key_test_results does not match the exact 12-column concise contract.', 1;
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
    (N'device_key_test_results', N'session_id', N'device_test_sessions'),
    (N'audit_log', N'user_id', N'users'),
    (N'chat_conversations', N'seller_user_id', N'users'),
    (N'chat_conversations', N'buyer_id', N'users'),
    (N'chat_conversations', N'admin_user_id', N'users'),
    (N'chat_conversations', N'build_request_id', N'build_requests'),
    (N'chat_messages', N'conversation_id', N'chat_conversations'),
    (N'chat_messages', N'sender_user_id', N'users');

DECLARE @ActualForeignKey TABLE (
    parent_table_name sysname NOT NULL,
    parent_column_name sysname NOT NULL,
    referenced_schema_name sysname NOT NULL,
    referenced_table_name sysname NOT NULL,
    referenced_column_name sysname NOT NULL,
    is_disabled bit NOT NULL,
    is_not_trusted bit NOT NULL
);

INSERT INTO @ActualForeignKey
SELECT
    parent_table.name,
    parent_column.name,
    referenced_schema.name,
    referenced_table.name,
    referenced_column.name,
    foreign_key.is_disabled,
    foreign_key.is_not_trusted
FROM sys.foreign_keys AS foreign_key
INNER JOIN sys.foreign_key_columns AS foreign_key_column
    ON foreign_key_column.constraint_object_id = foreign_key.object_id
INNER JOIN sys.tables AS parent_table
    ON parent_table.object_id = foreign_key.parent_object_id
   AND parent_table.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.columns AS parent_column
    ON parent_column.object_id = foreign_key_column.parent_object_id
   AND parent_column.column_id = foreign_key_column.parent_column_id
INNER JOIN sys.tables AS referenced_table
    ON referenced_table.object_id = foreign_key.referenced_object_id
INNER JOIN sys.schemas AS referenced_schema
    ON referenced_schema.schema_id = referenced_table.schema_id
INNER JOIN sys.columns AS referenced_column
    ON referenced_column.object_id = foreign_key_column.referenced_object_id
   AND referenced_column.column_id = foreign_key_column.referenced_column_id
WHERE parent_table.name IN (
    N'roles', N'users', N'seller_profiles', N'seller_applications',
    N'brands', N'layouts', N'keyboard_kits', N'switches',
    N'keycap_sets', N'stabilizers', N'accessories', N'builds',
    N'build_items', N'build_mods', N'build_requests', N'devices',
    N'device_test_sessions', N'device_key_test_results', N'audit_log',
    N'chat_conversations', N'chat_messages'
);

IF (SELECT COUNT(*) FROM @ExpectedForeignKey) <> 30
   OR (SELECT COUNT(*) FROM @ActualForeignKey) <> 30
   OR EXISTS (
       SELECT 1
       FROM @ExpectedForeignKey AS expected
       LEFT JOIN @ActualForeignKey AS actual
           ON actual.parent_table_name = expected.parent_table_name
          AND actual.parent_column_name = expected.parent_column_name
          AND actual.referenced_table_name = expected.referenced_table_name
       WHERE actual.parent_table_name IS NULL
   )
   OR EXISTS (
       SELECT 1
       FROM @ActualForeignKey AS actual
       LEFT JOIN @ExpectedForeignKey AS expected
           ON expected.parent_table_name = actual.parent_table_name
          AND expected.parent_column_name = actual.parent_column_name
          AND expected.referenced_table_name = actual.referenced_table_name
       WHERE expected.parent_table_name IS NULL
          OR actual.referenced_schema_name <> N'dbo'
          OR actual.referenced_column_name <> N'id'
          OR actual.is_disabled <> 0
          OR actual.is_not_trusted <> 0
   )
BEGIN
    THROW 51304, 'The concise schema requires the exact 30 enabled and trusted business foreign keys.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions AS session_row
    INNER JOIN dbo.build_requests AS request_row
        ON request_row.id = session_row.request_id
    INNER JOIN dbo.devices AS device_row
        ON device_row.id = session_row.device_id
    WHERE request_row.seller_user_id <> device_row.seller_user_id
)
BEGIN
    THROW 51305, 'A concise QC session links a request and device owned by different sellers.', 1;
END;

IF EXISTS (
    SELECT session_id, key_code
    FROM dbo.device_key_test_results
    GROUP BY session_id, key_code
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51306, 'Duplicate per-session key results exist.', 1;
END;

PRINT 'Concise QC postflight passed.';
