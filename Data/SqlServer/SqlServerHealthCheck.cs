using System.Data;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Data.SqlServer;

public sealed class SqlServerHealthCheck
{
    public const string ExpectedSchemaVersion = "2026.07.15-pk-id";

    private const int ExpectedPrimaryKeyCount = 21;
    private const int ExpectedForeignKeyCount = 33;

    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlServerHealthCheck(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    public async Task EnsureCompatibleSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var issues = new List<string>();

        if (!await HasExpectedSchemaVersionAsync(connection, cancellationToken))
        {
            issues.Add($"missing schema migration version {ExpectedSchemaVersion}");
        }

        var primaryKeyState = await ReadPrimaryKeyStateAsync(connection, cancellationToken);
        if (primaryKeyState.MissingTableCount > 0)
        {
            issues.Add($"{primaryKeyState.MissingTableCount} required business table(s) are missing");
        }

        if (primaryKeyState.IdPrimaryKeyCount != ExpectedPrimaryKeyCount)
        {
            issues.Add($"{primaryKeyState.IdPrimaryKeyCount}/{ExpectedPrimaryKeyCount} primary keys are named id");
        }

        if (primaryKeyState.LegacyPrimaryKeyCount > 0)
        {
            issues.Add($"{primaryKeyState.LegacyPrimaryKeyCount} legacy primary-key column(s) are still present");
        }

        if (primaryKeyState.WrongShapeCount > 0)
        {
            issues.Add($"{primaryKeyState.WrongShapeCount} primary key(s) have an unexpected type, identity flag, or column count");
        }

        var foreignKeyState = await ReadForeignKeyStateAsync(connection, cancellationToken);
        if (foreignKeyState.ForeignKeyCount != ExpectedForeignKeyCount)
        {
            issues.Add($"{foreignKeyState.ForeignKeyCount}/{ExpectedForeignKeyCount} expected foreign keys are present");
        }

        if (foreignKeyState.InvalidStateCount > 0)
        {
            issues.Add($"{foreignKeyState.InvalidStateCount} foreign key(s) are disabled or untrusted");
        }

        if (foreignKeyState.NonIdTargetCount > 0)
        {
            issues.Add($"{foreignKeyState.NonIdTargetCount} foreign key(s) still target a non-id primary key column");
        }

        if (foreignKeyState.MissingRelationshipCount > 0)
        {
            issues.Add($"{foreignKeyState.MissingRelationshipCount} required foreign-key relationship(s) are missing");
        }

        if (foreignKeyState.UnexpectedRelationshipCount > 0)
        {
            issues.Add($"{foreignKeyState.UnexpectedRelationshipCount} unexpected foreign-key relationship(s) are present");
        }

        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Database schema is not compatible with this application build. "
                + "Run the PK-to-id migration and postflight verification first. Details: "
                + string.Join("; ", issues)
                + ".");
        }
    }

    private static async Task<bool> HasExpectedSchemaVersionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.columns AS c
                    INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'dbo.schema_migrations')
                      AND c.name = N'version'
                      AND t.name = N'varchar'
                      AND c.max_length = 64
                      AND c.is_nullable = 0
                ) THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.columns AS c
                    INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'dbo.schema_migrations')
                      AND c.name = N'description'
                      AND t.name = N'varchar'
                      AND c.max_length = 255
                      AND c.is_nullable = 0
                ) THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.columns AS c
                    INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'dbo.schema_migrations')
                      AND c.name = N'applied_at'
                      AND t.name = N'datetime2'
                      AND c.scale = 7
                      AND c.is_nullable = 0
                ) THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.columns AS c
                    INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'dbo.schema_migrations')
                      AND c.name = N'succeeded'
                      AND t.name = N'bit'
                      AND c.max_length = 1
                      AND c.is_nullable = 0
                ) THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes AS pk
                    INNER JOIN sys.index_columns AS ic
                        ON ic.object_id = pk.object_id
                       AND ic.index_id = pk.index_id
                       AND ic.key_ordinal = 1
                    INNER JOIN sys.columns AS c
                        ON c.object_id = ic.object_id
                       AND c.column_id = ic.column_id
                    WHERE pk.object_id = OBJECT_ID(N'dbo.schema_migrations')
                      AND pk.is_primary_key = 1
                      AND pk.is_disabled = 0
                      AND c.name = N'version'
                      AND (
                          SELECT COUNT(*)
                          FROM sys.index_columns AS all_pk_columns
                          WHERE all_pk_columns.object_id = pk.object_id
                            AND all_pk_columns.index_id = pk.index_id
                            AND all_pk_columns.key_ordinal > 0
                      ) = 1
                ) THEN 0
                ELSE 1
            END;
            """;

        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            return false;
        }

        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.schema_migrations
                WHERE version = @version
                  AND succeeded = 1
            ) THEN 1 ELSE 0 END;
            """;
        command.Parameters.Add("@version", SqlDbType.VarChar, 64).Value = ExpectedSchemaVersion;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<PrimaryKeyState> ReadPrimaryKeyStateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH expected AS (
                SELECT *
                FROM (VALUES
                    (N'roles', N'role_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'users', N'user_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'seller_profiles', N'seller_profile_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'seller_applications', N'application_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'brands', N'brand_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'layouts', N'layout_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'keyboard_kits', N'kit_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'switches', N'switch_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'keycap_sets', N'keycap_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'stabilizers', N'stab_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'accessories', N'accessory_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'builds', N'build_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'build_items', N'build_item_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'build_mods', N'mod_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'build_requests', N'request_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'devices', N'device_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'device_test_sessions', N'session_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'device_key_test_results', N'key_test_id', N'bigint', CONVERT(smallint, 8), CONVERT(bit, 1)),
                    (N'audit_log', N'log_id', N'int', CONVERT(smallint, 4), CONVERT(bit, 1)),
                    (N'chat_conversations', N'conversation_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0)),
                    (N'chat_messages', N'message_id', N'varchar', CONVERT(smallint, 50), CONVERT(bit, 0))
                ) AS v(table_name, legacy_pk_name, expected_type, expected_max_length, expected_identity)
            ),
            actual AS (
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
                FROM expected AS e
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
                SUM(CASE WHEN object_id IS NULL THEN 1 ELSE 0 END) AS missing_table_count,
                SUM(CASE WHEN pk_column_name = N'id' THEN 1 ELSE 0 END) AS id_pk_count,
                SUM(CASE WHEN has_legacy_column = 1 THEN 1 ELSE 0 END) AS legacy_pk_count,
                SUM(CASE
                    WHEN object_id IS NOT NULL
                     AND (
                            pk_column_name <> N'id'
                         OR pk_type_name <> expected_type
                         OR pk_max_length <> expected_max_length
                         OR pk_is_identity <> expected_identity
                         OR pk_is_disabled <> 0
                         OR pk_key_column_count <> 1
                     )
                    THEN 1
                    ELSE 0
                END) AS wrong_shape_count
            FROM actual;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PrimaryKeyState(ExpectedPrimaryKeyCount, 0, 0, ExpectedPrimaryKeyCount);
        }

        return new PrimaryKeyState(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }

    private static async Task<ForeignKeyState> ReadForeignKeyStateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH expected_tables AS (
                SELECT *
                FROM (VALUES
                    (N'roles'), (N'users'), (N'seller_profiles'), (N'seller_applications'),
                    (N'brands'), (N'layouts'), (N'keyboard_kits'), (N'switches'),
                    (N'keycap_sets'), (N'stabilizers'), (N'accessories'), (N'builds'),
                    (N'build_items'), (N'build_mods'), (N'build_requests'), (N'devices'),
                    (N'device_test_sessions'), (N'device_key_test_results'), (N'audit_log'),
                    (N'chat_conversations'), (N'chat_messages')
                ) AS v(table_name)
            ),
            expected_relationships AS (
                SELECT *
                FROM (VALUES
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
                    (N'chat_messages', N'sender_user_id', N'users')
                ) AS v(parent_table_name, parent_column_name, referenced_table_name)
            ),
            actual_relationships AS (
                SELECT
                    parent_table.name AS parent_table_name,
                    parent_column.name AS parent_column_name,
                    referenced_schema.name AS referenced_schema_name,
                    referenced_table.name AS referenced_table_name,
                    referenced_column.name AS referenced_column_name,
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
                WHERE parent_table.name IN (SELECT table_name FROM expected_tables)
            )
            SELECT
                (SELECT COUNT(*) FROM actual_relationships) AS fk_count,
                (
                    SELECT COUNT(*)
                    FROM actual_relationships
                    WHERE is_disabled = 1 OR is_not_trusted = 1
                ) AS invalid_state_count,
                (
                    SELECT COUNT(*)
                    FROM actual_relationships
                    WHERE referenced_schema_name <> N'dbo'
                       OR referenced_column_name <> N'id'
                ) AS non_id_target_count,
                (
                    SELECT COUNT(*)
                    FROM expected_relationships AS expected
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM actual_relationships AS actual
                        WHERE actual.parent_table_name = expected.parent_table_name
                          AND actual.parent_column_name = expected.parent_column_name
                          AND actual.referenced_schema_name = N'dbo'
                          AND actual.referenced_table_name = expected.referenced_table_name
                          AND actual.referenced_column_name = N'id'
                    )
                ) AS missing_relationship_count,
                (
                    SELECT COUNT(*)
                    FROM actual_relationships AS actual
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM expected_relationships AS expected
                        WHERE expected.parent_table_name = actual.parent_table_name
                          AND expected.parent_column_name = actual.parent_column_name
                          AND actual.referenced_schema_name = N'dbo'
                          AND expected.referenced_table_name = actual.referenced_table_name
                          AND actual.referenced_column_name = N'id'
                    )
                ) AS unexpected_relationship_count;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ForeignKeyState(0, 0, ExpectedForeignKeyCount, ExpectedForeignKeyCount, 0);
        }

        return new ForeignKeyState(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    private sealed record PrimaryKeyState(
        int MissingTableCount,
        int IdPrimaryKeyCount,
        int LegacyPrimaryKeyCount,
        int WrongShapeCount);

    private sealed record ForeignKeyState(
        int ForeignKeyCount,
        int InvalidStateCount,
        int NonIdTargetCount,
        int MissingRelationshipCount,
        int UnexpectedRelationshipCount);
}
