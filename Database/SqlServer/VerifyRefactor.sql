-- Custom Keyboard Builder - Refactor verification queries
-- Source: Custom_Keyboard_Project_Refactor_Summary.md section 14.
-- Run AFTER CreateSchema_Refactor.sql + SeedData_Refactor.sql on the test DB.
-- This is the clean-seed fixture verifier. For preserving an existing runtime
-- database, use VerifyPkToId_Preflight.sql and VerifyPkToId_Postflight.sql.
-- Every CHECK query below must return 0 error rows. Row counts are clean-seed
-- expectations, not a runtime-data migration baseline.

USE CustomKeyboard_Refactor;
GO

SET NOCOUNT ON;
GO

DECLARE @ExpectedSchemaVersion varchar(64) = '2026.07.15-pk-id';

DECLARE @SchemaMigrationsObjectId int = OBJECT_ID(N'dbo.schema_migrations', N'U');

IF @SchemaMigrationsObjectId IS NULL
BEGIN
    THROW 51000, 'VerifyRefactor.sql requires clean schema version 2026.07.15-pk-id.', 1;
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
    THROW 51000, 'VerifyRefactor.sql found an incompatible dbo.schema_migrations table.', 1;
END;

DECLARE @SchemaVersionFound bit = 0;
EXEC sys.sp_executesql
    N'SELECT @is_found = CASE WHEN EXISTS (
          SELECT 1 FROM dbo.schema_migrations
          WHERE version = @version AND succeeded = 1
      ) THEN 1 ELSE 0 END;',
    N'@version varchar(64), @is_found bit OUTPUT',
    @version = @ExpectedSchemaVersion,
    @is_found = @SchemaVersionFound OUTPUT;

IF @SchemaVersionFound = 0
BEGIN
    THROW 51000, 'VerifyRefactor.sql requires clean schema version 2026.07.15-pk-id.', 1;
END;

DECLARE @ExpectedPrimaryKey TABLE (
    table_name sysname NOT NULL PRIMARY KEY,
    legacy_pk_name sysname NOT NULL,
    expected_type sysname NOT NULL,
    expected_max_length smallint NOT NULL,
    expected_identity bit NOT NULL
);

INSERT INTO @ExpectedPrimaryKey (table_name, legacy_pk_name, expected_type, expected_max_length, expected_identity)
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

IF (SELECT COUNT(*) FROM @ExpectedPrimaryKey) <> 21
BEGIN
    THROW 51001, 'VerifyRefactor.sql internal PK mapping does not contain 21 entries.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM @ExpectedPrimaryKey AS expected
    LEFT JOIN sys.tables AS t
        ON t.name = expected.table_name
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
       AND legacy_column.name = expected.legacy_pk_name
    WHERE t.object_id IS NULL
       OR pk_column.name <> N'id'
       OR legacy_column.column_id IS NOT NULL
       OR type_info.name <> expected.expected_type
       OR pk_column.max_length <> expected.expected_max_length
       OR CONVERT(bit, COLUMNPROPERTY(t.object_id, pk_column.name, 'IsIdentity')) <> expected.expected_identity
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
    THROW 51001, 'VerifyRefactor.sql requires the exact 21-PK id/type/identity contract.', 1;
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
WHERE parent_table.name IN (SELECT table_name FROM @ExpectedPrimaryKey);

IF (SELECT COUNT(*) FROM @ExpectedForeignKey) <> 33
   OR (SELECT COUNT(*) FROM @ActualForeignKey) <> 33
   OR EXISTS (
        SELECT parent_table_name, parent_column_name, N'dbo', referenced_table_name, N'id'
        FROM @ExpectedForeignKey
        EXCEPT
        SELECT parent_table_name, parent_column_name, referenced_schema_name, referenced_table_name, referenced_column_name
        FROM @ActualForeignKey
   )
   OR EXISTS (
        SELECT parent_table_name, parent_column_name, referenced_schema_name, referenced_table_name, referenced_column_name
        FROM @ActualForeignKey
        EXCEPT
        SELECT parent_table_name, parent_column_name, N'dbo', referenced_table_name, N'id'
        FROM @ExpectedForeignKey
   )
BEGIN
    THROW 51002, 'VerifyRefactor.sql requires the exact 33-FK id-target contract.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM @ActualForeignKey
    WHERE is_disabled = 1 OR is_not_trusted = 1
)
BEGIN
    THROW 51003, 'VerifyRefactor.sql found a disabled or untrusted foreign key.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.audit_log
    WHERE table_name = 'seller_profiles'
      AND (TRY_CONVERT(int, record_id) IS NULL OR TRY_CONVERT(int, record_id) <= 0)
)
BEGIN
    THROW 51010, 'VerifyRefactor.sql requires seller_profiles audit record_id to use the integer profile PK.', 1;
END;
GO

PRINT '================================================================';
PRINT ' 14.1  Row counts per table (compare to Summary 12.1)';
PRINT '       Expected: roles 3, users 7, seller_profiles 3, brands 13,';
PRINT '       layouts 4, keyboard_kits 9, switches 10, keycap_sets 5,';
PRINT '       stabilizers 4, accessories 7, builds 5, build_items 17,';
PRINT '       build_mods 5, build_requests 2, audit_log 3,';
PRINT '       chat_conversations 2, chat_messages 4, seller_applications 1,';
PRINT '       devices 1, device_test_sessions 1, device_key_test_results 3.';
PRINT '================================================================';
DECLARE @RowCountCheck TABLE (
    table_name sysname NOT NULL PRIMARY KEY,
    row_count int NOT NULL,
    expected_count int NOT NULL
);

INSERT INTO @RowCountCheck (table_name, row_count, expected_count)
SELECT 'roles' AS table_name, COUNT(*) AS row_count, 3 AS expected_count FROM roles
UNION ALL SELECT 'users', COUNT(*), 7 FROM users
UNION ALL SELECT 'seller_profiles', COUNT(*), 3 FROM seller_profiles
UNION ALL SELECT 'brands', COUNT(*), 13 FROM brands
UNION ALL SELECT 'layouts', COUNT(*), 4 FROM layouts
UNION ALL SELECT 'keyboard_kits', COUNT(*), 9 FROM keyboard_kits
UNION ALL SELECT 'switches', COUNT(*), 10 FROM switches
UNION ALL SELECT 'keycap_sets', COUNT(*), 5 FROM keycap_sets
UNION ALL SELECT 'stabilizers', COUNT(*), 4 FROM stabilizers
UNION ALL SELECT 'accessories', COUNT(*), 7 FROM accessories
UNION ALL SELECT 'builds', COUNT(*), 5 FROM builds
UNION ALL SELECT 'build_items', COUNT(*), 17 FROM build_items
UNION ALL SELECT 'build_mods', COUNT(*), 5 FROM build_mods
UNION ALL SELECT 'build_requests', COUNT(*), 2 FROM build_requests
UNION ALL SELECT 'audit_log', COUNT(*), 3 FROM audit_log
UNION ALL SELECT 'chat_conversations', COUNT(*), 2 FROM chat_conversations
UNION ALL SELECT 'chat_messages', COUNT(*), 4 FROM chat_messages
UNION ALL SELECT 'seller_applications', COUNT(*), 1 FROM seller_applications
UNION ALL SELECT 'devices', COUNT(*), 1 FROM devices
UNION ALL SELECT 'device_test_sessions', COUNT(*), 1 FROM device_test_sessions
UNION ALL SELECT 'device_key_test_results', COUNT(*), 3 FROM device_key_test_results;

SELECT table_name, row_count, expected_count
FROM @RowCountCheck
ORDER BY table_name;

IF EXISTS (SELECT 1 FROM @RowCountCheck WHERE row_count <> expected_count)
BEGIN
    THROW 51004, 'VerifyRefactor.sql found an unexpected clean-seed row count.', 1;
END;
GO

PRINT '================================================================';
PRINT ' 14.2  Build total snapshot = kit price + SUM(item qty * unit price)';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT
    b.id AS build_id,
    b.total_cost_snapshot,
    CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2)) AS calculated_total
FROM builds b
INNER JOIN keyboard_kits k ON k.id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.id
GROUP BY b.id, b.total_cost_snapshot, k.price_usd
HAVING b.total_cost_snapshot <> CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2));
GO

PRINT '================================================================';
PRINT ' 14.3  Switch quantity for Saved/Requested builds >= required_switch_quantity';
PRINT '       Rule is at-least (BuildService allows buying spare switches). Expected: 0 rows.';
PRINT '================================================================';
SELECT
    b.id AS build_id,
    k.required_switch_quantity,
    SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) AS selected_switch_quantity
FROM builds b
INNER JOIN keyboard_kits k ON k.id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.id
WHERE b.status IN ('Saved', 'Requested')
GROUP BY b.id, k.required_switch_quantity
HAVING SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) < k.required_switch_quantity;
GO

PRINT '================================================================';
PRINT ' 14.4  Exactly one product FK per build_items row';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT *
FROM build_items
WHERE
    (CASE WHEN switch_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN keycap_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN stab_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) <> 1;
GO

PRINT '================================================================';
PRINT ' 14.5  Seller requests target active + verified sellers only';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT br.id AS request_id, u.username, sp.is_verified, u.is_active
FROM build_requests br
INNER JOIN users u ON u.id = br.seller_user_id
LEFT JOIN seller_profiles sp ON sp.user_id = u.id
WHERE u.is_active = 0 OR sp.is_verified = 0 OR sp.id IS NULL;
GO

PRINT '================================================================';
PRINT ' 14.6  Conversation has exactly one of buyer_id / admin_user_id';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT *
FROM chat_conversations
WHERE
    (CASE WHEN buyer_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) <> 1;
GO

PRINT '================================================================';
PRINT ' Extra A  FK orphan checks across all relationships';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT 'users.role_id' AS fk, COUNT(*) AS orphan_count
    FROM users u LEFT JOIN roles r ON r.id = u.role_id WHERE r.id IS NULL
UNION ALL SELECT 'seller_profiles.user_id', COUNT(*)
    FROM seller_profiles sp LEFT JOIN users u ON u.id = sp.user_id WHERE u.id IS NULL
UNION ALL SELECT 'seller_applications.buyer_user_id', COUNT(*)
    FROM seller_applications sa LEFT JOIN users u ON u.id = sa.buyer_user_id WHERE u.id IS NULL
UNION ALL SELECT 'seller_applications.reviewed_by', COUNT(*)
    FROM seller_applications sa LEFT JOIN users u ON u.id = sa.reviewed_by WHERE sa.reviewed_by IS NOT NULL AND u.id IS NULL
UNION ALL SELECT 'keyboard_kits.brand_id', COUNT(*)
    FROM keyboard_kits k LEFT JOIN brands b ON b.id = k.brand_id WHERE b.id IS NULL
UNION ALL SELECT 'keyboard_kits.layout_id', COUNT(*)
    FROM keyboard_kits k LEFT JOIN layouts l ON l.id = k.layout_id WHERE l.id IS NULL
UNION ALL SELECT 'switches.brand_id', COUNT(*)
    FROM switches s LEFT JOIN brands b ON b.id = s.brand_id WHERE b.id IS NULL
UNION ALL SELECT 'keycap_sets.brand_id', COUNT(*)
    FROM keycap_sets kc LEFT JOIN brands b ON b.id = kc.brand_id WHERE b.id IS NULL
UNION ALL SELECT 'stabilizers.brand_id', COUNT(*)
    FROM stabilizers st LEFT JOIN brands b ON b.id = st.brand_id WHERE b.id IS NULL
UNION ALL SELECT 'builds.buyer_id', COUNT(*)
    FROM builds bd LEFT JOIN users u ON u.id = bd.buyer_id WHERE u.id IS NULL
UNION ALL SELECT 'builds.kit_id', COUNT(*)
    FROM builds bd LEFT JOIN keyboard_kits k ON k.id = bd.kit_id WHERE k.id IS NULL
UNION ALL SELECT 'build_items.build_id', COUNT(*)
    FROM build_items bi LEFT JOIN builds bd ON bd.id = bi.build_id WHERE bd.id IS NULL
UNION ALL SELECT 'build_items.switch_id', COUNT(*)
    FROM build_items bi LEFT JOIN switches s ON s.id = bi.switch_id WHERE bi.switch_id IS NOT NULL AND s.id IS NULL
UNION ALL SELECT 'build_items.keycap_id', COUNT(*)
    FROM build_items bi LEFT JOIN keycap_sets kc ON kc.id = bi.keycap_id WHERE bi.keycap_id IS NOT NULL AND kc.id IS NULL
UNION ALL SELECT 'build_items.stab_id', COUNT(*)
    FROM build_items bi LEFT JOIN stabilizers st ON st.id = bi.stab_id WHERE bi.stab_id IS NOT NULL AND st.id IS NULL
UNION ALL SELECT 'build_items.accessory_id', COUNT(*)
    FROM build_items bi LEFT JOIN accessories a ON a.id = bi.accessory_id WHERE bi.accessory_id IS NOT NULL AND a.id IS NULL
UNION ALL SELECT 'build_mods.build_id', COUNT(*)
    FROM build_mods bm LEFT JOIN builds bd ON bd.id = bm.build_id WHERE bd.id IS NULL
UNION ALL SELECT 'build_requests.build_id', COUNT(*)
    FROM build_requests br LEFT JOIN builds bd ON bd.id = br.build_id WHERE bd.id IS NULL
UNION ALL SELECT 'build_requests.seller_user_id', COUNT(*)
    FROM build_requests br LEFT JOIN users u ON u.id = br.seller_user_id WHERE u.id IS NULL
UNION ALL SELECT 'devices.seller_user_id', COUNT(*)
    FROM devices d LEFT JOIN users u ON u.id = d.seller_user_id WHERE u.id IS NULL
UNION ALL SELECT 'device_test_sessions.request_id', COUNT(*)
    FROM device_test_sessions dts LEFT JOIN build_requests br ON br.id = dts.request_id WHERE br.id IS NULL
UNION ALL SELECT 'device_test_sessions.device_id', COUNT(*)
    FROM device_test_sessions dts LEFT JOIN devices d ON d.id = dts.device_id WHERE d.id IS NULL
UNION ALL SELECT 'device_test_sessions.seller_user_id', COUNT(*)
    FROM device_test_sessions dts LEFT JOIN users u ON u.id = dts.seller_user_id WHERE u.id IS NULL
UNION ALL SELECT 'device_key_test_results.session_id', COUNT(*)
    FROM device_key_test_results dktr LEFT JOIN device_test_sessions dts ON dts.id = dktr.session_id WHERE dts.id IS NULL
UNION ALL SELECT 'device_key_test_results.request_id', COUNT(*)
    FROM device_key_test_results dktr LEFT JOIN build_requests br ON br.id = dktr.request_id WHERE br.id IS NULL
UNION ALL SELECT 'device_key_test_results.device_id', COUNT(*)
    FROM device_key_test_results dktr LEFT JOIN devices d ON d.id = dktr.device_id WHERE d.id IS NULL
UNION ALL SELECT 'audit_log.user_id', COUNT(*)
    FROM audit_log al LEFT JOIN users u ON u.id = al.user_id WHERE u.id IS NULL
UNION ALL SELECT 'chat_conversations.seller_user_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN users u ON u.id = c.seller_user_id WHERE u.id IS NULL
UNION ALL SELECT 'chat_conversations.buyer_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN users u ON u.id = c.buyer_id WHERE c.buyer_id IS NOT NULL AND u.id IS NULL
UNION ALL SELECT 'chat_conversations.admin_user_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN users u ON u.id = c.admin_user_id WHERE c.admin_user_id IS NOT NULL AND u.id IS NULL
UNION ALL SELECT 'chat_conversations.build_request_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN build_requests br ON br.id = c.build_request_id WHERE c.build_request_id IS NOT NULL AND br.id IS NULL
UNION ALL SELECT 'chat_messages.conversation_id', COUNT(*)
    FROM chat_messages m LEFT JOIN chat_conversations c ON c.id = m.conversation_id WHERE c.id IS NULL
UNION ALL SELECT 'chat_messages.sender_user_id', COUNT(*)
    FROM chat_messages m LEFT JOIN users u ON u.id = m.sender_user_id WHERE u.id IS NULL;
GO

PRINT '================================================================';
PRINT ' Extra B  Requested builds have a matching build_request';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT b.id AS build_id, b.status
FROM builds b
WHERE b.status = 'Requested'
  AND NOT EXISTS (SELECT 1 FROM build_requests br WHERE br.build_id = b.id);
GO

PRINT '================================================================';
PRINT ' Extra B2 Builds with an active request are Requested';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT b.id AS build_id, b.status
FROM builds b
WHERE b.status NOT IN ('Requested', 'Archived')
  AND EXISTS (
      SELECT 1 FROM build_requests br
      WHERE br.build_id = b.id
        AND br.status IN ('Pending', 'Accepted', 'In_progress'));
GO

PRINT '================================================================';
PRINT ' Extra B3 Device QC session/key consistency';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT 'duplicate_key_result' AS problem, session_id, key_code
FROM device_key_test_results
GROUP BY session_id, key_code
HAVING COUNT(*) > 1

UNION ALL

SELECT 'summary_count_mismatch', dts.id, NULL
FROM device_test_sessions dts
OUTER APPLY (
    SELECT
        COUNT(*) AS tested_keys,
        SUM(CASE WHEN dktr.result = 'Pass' THEN 1 ELSE 0 END) AS passed_keys,
        SUM(CASE WHEN dktr.result = 'Warning' THEN 1 ELSE 0 END) AS warning_keys,
        SUM(CASE WHEN dktr.result = 'Fail' THEN 1 ELSE 0 END) AS failed_keys
    FROM device_key_test_results dktr
    WHERE dktr.session_id = dts.id
) actual
WHERE dts.status <> 'Running'
  AND (
        dts.tested_keys <> actual.tested_keys
     OR dts.passed_keys <> ISNULL(actual.passed_keys, 0)
     OR dts.warning_keys <> ISNULL(actual.warning_keys, 0)
     OR dts.failed_keys <> ISNULL(actual.failed_keys, 0)
  )

UNION ALL

SELECT 'stale_empty_running_session', dts.id, NULL
FROM device_test_sessions dts
WHERE dts.status = 'Running'
  AND dts.started_at < DATEADD(minute, -5, SYSUTCDATETIME())
  AND NOT EXISTS (
      SELECT 1
      FROM device_key_test_results dktr
      WHERE dktr.session_id = dts.id);
GO

PRINT '================================================================';
PRINT ' Extra C  Chat message sender belongs to its conversation';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT m.id AS message_id, m.conversation_id, m.sender_user_id
FROM chat_messages m
INNER JOIN chat_conversations c ON c.id = m.conversation_id
WHERE m.sender_user_id NOT IN (
    c.seller_user_id,
    ISNULL(c.buyer_id, -1),
    ISNULL(c.admin_user_id, -1)
);
GO

PRINT '================================================================';
PRINT ' Extra D  Seller applications: valid status + applicant/reviewer exist';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT problem, application_id, status
FROM (
    SELECT 'invalid_status_or_fk' AS problem, sa.id AS application_id, sa.status
    FROM seller_applications sa
    LEFT JOIN users b ON b.id = sa.buyer_user_id
    LEFT JOIN users r ON r.id = sa.reviewed_by
    WHERE sa.status NOT IN ('Pending', 'Approved', 'Rejected')
       OR b.id IS NULL
       OR (sa.reviewed_by IS NOT NULL AND r.id IS NULL)

    UNION ALL
    SELECT 'pending_applicant_not_active_buyer', sa.id AS application_id, sa.status
    FROM seller_applications sa
    INNER JOIN users b ON b.id = sa.buyer_user_id
    INNER JOIN roles br ON br.id = b.role_id
    WHERE sa.status = 'Pending'
      AND (b.is_active = 0 OR br.role_name <> 'Buyer')

    UNION ALL
    SELECT 'duplicate_pending_for_buyer', MIN(sa.id), 'Pending'
    FROM seller_applications sa
    WHERE sa.status = 'Pending'
    GROUP BY sa.buyer_user_id
    HAVING COUNT(*) > 1
) problems;
GO

PRINT 'Verification queries complete.';
GO
