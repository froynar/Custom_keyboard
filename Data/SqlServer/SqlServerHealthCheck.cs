using System.Data;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Data.SqlServer;

public sealed class SqlServerHealthCheck
{
    public const string ExpectedSchemaVersion = "2026.07.27-simplified-read-views";

    private const int ExpectedPrimaryKeyCount = 21;
    private const int ExpectedForeignKeyCount = 30;
    private const int ExpectedQcColumnCount = 20;
    private const int ExpectedReadViewCount = 4;

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
            throw new InvalidOperationException(
                "Database schema is not compatible with this application build. "
                + "Run the ordered database migrations and postflight verification first. Details: "
                + $"missing schema migration version {ExpectedSchemaVersion}.");
        }

        var readViewState = await ReadReadViewStateAsync(connection, cancellationToken);
        if (readViewState.ViewCount != ExpectedReadViewCount)
        {
            issues.Add($"{readViewState.ViewCount}/{ExpectedReadViewCount} required read views are present");
        }

        if (readViewState.InvalidContractCount > 0)
        {
            issues.Add($"{readViewState.InvalidContractCount} read-view contract check(s) failed");
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

        var qcColumnState = await ReadQcColumnStateAsync(connection, cancellationToken);
        if (qcColumnState.ColumnCount != ExpectedQcColumnCount)
        {
            issues.Add($"{qcColumnState.ColumnCount}/{ExpectedQcColumnCount} concise QC columns are present");
        }

        if (qcColumnState.MissingColumnCount > 0)
        {
            issues.Add($"{qcColumnState.MissingColumnCount} required concise QC column(s) are missing");
        }

        if (qcColumnState.UnexpectedColumnCount > 0)
        {
            issues.Add($"{qcColumnState.UnexpectedColumnCount} legacy or unexpected QC column(s) are present");
        }

        if (qcColumnState.WrongShapeCount > 0)
        {
            issues.Add($"{qcColumnState.WrongShapeCount} concise QC column(s) have an unexpected type or nullability");
        }

        var hardeningState = await ReadBuildDeviceHardeningStateAsync(connection, cancellationToken);
        if (hardeningState.InvalidUnicodeColumnCount > 0)
        {
            issues.Add($"{hardeningState.InvalidUnicodeColumnCount} build/request Unicode column(s) are missing or malformed");
        }

        if (hardeningState.InvalidIndexCount > 0)
        {
            issues.Add($"{hardeningState.InvalidIndexCount} build/device concurrency guard index(es) are missing or disabled");
        }

        if (hardeningState.InvalidCheckConstraintCount > 0)
        {
            issues.Add($"{hardeningState.InvalidCheckConstraintCount} build/device check constraint(s) are missing or untrusted");
        }

        if (hardeningState.InvalidCompletedRequestCount > 0)
        {
            issues.Add(
                $"{hardeningState.InvalidCompletedRequestCount} completed request(s) do not have a complete Passed/Warning QC result");
        }

        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Database schema is not compatible with this application build. "
                + "Run the ordered database migrations and postflight verification first. Details: "
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

    private static async Task<ReadViewState> ReadReadViewStateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH expected_views AS (
                SELECT *
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
                ) AS value_list(view_name, expected_columns)
            ),
            actual_views AS (
                SELECT
                    view_info.name AS view_name,
                    view_info.object_id,
                    STRING_AGG(CONVERT(nvarchar(max), column_info.name), N',')
                        WITHIN GROUP (ORDER BY column_info.column_id) AS actual_columns
                FROM sys.views AS view_info
                INNER JOIN sys.schemas AS schema_info
                    ON schema_info.schema_id = view_info.schema_id
                INNER JOIN sys.columns AS column_info
                    ON column_info.object_id = view_info.object_id
                WHERE schema_info.name = N'views'
                  AND view_info.name IN (N'Last_QC', N'Catalog_Comps', N'Build_items', N'Req_view')
                GROUP BY view_info.name, view_info.object_id
            ),
            expected_critical_columns AS (
                SELECT *
                FROM (VALUES
                    (N'Last_QC', N'completed_at', N'datetime2', 8, 27, 7, 1),
                    (N'Last_QC', N'is_acceptable', N'bit', 1, 1, 0, 1),
                    (N'Catalog_Comps', N'component_id', N'varchar', 50, 0, 0, 0),
                    (N'Catalog_Comps', N'price_usd', N'decimal', 9, 10, 2, 0),
                    (N'Build_items', N'unit_price_snapshot', N'decimal', 9, 10, 2, 0),
                    (N'Build_items', N'line_total_snapshot', N'decimal', 13, 28, 2, 1),
                    (N'Build_items', N'current_price_usd', N'decimal', 9, 10, 2, 0),
                    (N'Req_view', N'request_payload_json', N'nvarchar', -1, 0, 0, 0),
                    (N'Req_view', N'completed_at', N'datetime2', 8, 27, 7, 1)
                ) AS value_list(
                    view_name,
                    column_name,
                    type_name,
                    max_length,
                    precision,
                    scale,
                    is_nullable
                )
            )
            SELECT
                COUNT(actual_views.object_id) AS view_count,
                SUM(
                    CASE
                        WHEN actual_views.object_id IS NULL
                          OR actual_views.actual_columns <> expected_views.expected_columns
                            THEN 1
                        ELSE 0
                    END
                ) + (
                    SELECT COUNT(*)
                    FROM expected_critical_columns AS expected_column
                    LEFT JOIN sys.schemas AS schema_info
                        ON schema_info.name = N'views'
                    LEFT JOIN sys.views AS view_info
                        ON view_info.schema_id = schema_info.schema_id
                       AND view_info.name = expected_column.view_name
                    LEFT JOIN sys.columns AS column_info
                        ON column_info.object_id = view_info.object_id
                       AND column_info.name = expected_column.column_name
                    LEFT JOIN sys.types AS type_info
                        ON type_info.user_type_id = column_info.user_type_id
                    WHERE column_info.object_id IS NULL
                       OR type_info.name <> expected_column.type_name
                       OR column_info.max_length <> expected_column.max_length
                       OR column_info.precision <> expected_column.precision
                       OR column_info.scale <> expected_column.scale
                       OR column_info.is_nullable <> expected_column.is_nullable
                ) AS invalid_contract_count
            FROM expected_views
            LEFT JOIN actual_views
                ON actual_views.view_name = expected_views.view_name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ReadViewState(0, ExpectedReadViewCount);
        }

        return new ReadViewState(reader.GetInt32(0), reader.GetInt32(1));
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
                    (N'device_key_test_results', N'session_id', N'device_test_sessions'),
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

    private static async Task<QcColumnState> ReadQcColumnStateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH expected AS (
                SELECT *
                FROM (VALUES
                    (N'device_test_sessions', N'id', N'varchar', 50, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'request_id', N'varchar', 50, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'device_id', N'varchar', 50, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'switch_technology', N'varchar', 50, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'noise_requirement', N'varchar', 20, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'total_keys', N'int', 4, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'status', N'varchar', 20, NULL, NULL, 0, 0),
                    (N'device_test_sessions', N'completed_at', N'datetime2', 8, NULL, 7, 1, 0),
                    (N'device_key_test_results', N'id', N'bigint', 8, NULL, NULL, 0, 1),
                    (N'device_key_test_results', N'session_id', N'varchar', 50, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'key_code', N'varchar', 30, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'received_key', N'varchar', 30, NULL, NULL, 1, 0),
                    (N'device_key_test_results', N'press_signal_detected', N'bit', 1, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'latency', N'decimal', 5, 8, 2, 1, 0),
                    (N'device_key_test_results', N'press_count', N'int', 4, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'release_signal', N'bit', 1, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'hold_duration', N'int', 4, NULL, NULL, 1, 0),
                    (N'device_key_test_results', N'noise', N'decimal', 5, 8, 2, 1, 0),
                    (N'device_key_test_results', N'result', N'varchar', 20, NULL, NULL, 0, 0),
                    (N'device_key_test_results', N'recorded_at', N'datetime2', 8, NULL, 7, 0, 0)
                ) AS values_list(
                    table_name,
                    column_name,
                    type_name,
                    max_length,
                    expected_precision,
                    expected_scale,
                    is_nullable,
                    is_identity
                )
            ),
            actual AS (
                SELECT
                    table_info.name AS table_name,
                    column_info.name AS column_name,
                    type_info.name AS type_name,
                    column_info.max_length,
                    column_info.precision,
                    column_info.scale,
                    CONVERT(int, column_info.is_nullable) AS is_nullable,
                    CONVERT(int, COLUMNPROPERTY(column_info.object_id, column_info.name, 'IsIdentity')) AS is_identity
                FROM sys.tables AS table_info
                INNER JOIN sys.columns AS column_info
                    ON column_info.object_id = table_info.object_id
                INNER JOIN sys.types AS type_info
                    ON type_info.user_type_id = column_info.user_type_id
                WHERE table_info.schema_id = SCHEMA_ID(N'dbo')
                  AND table_info.name IN (N'device_test_sessions', N'device_key_test_results')
            )
            SELECT
                (SELECT COUNT(*) FROM actual) AS column_count,
                (
                    SELECT COUNT(*)
                    FROM expected
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM actual
                        WHERE actual.table_name = expected.table_name
                          AND actual.column_name = expected.column_name
                    )
                ) AS missing_column_count,
                (
                    SELECT COUNT(*)
                    FROM actual
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM expected
                        WHERE expected.table_name = actual.table_name
                          AND expected.column_name = actual.column_name
                    )
                ) AS unexpected_column_count,
                (
                    SELECT COUNT(*)
                    FROM expected
                    INNER JOIN actual
                        ON actual.table_name = expected.table_name
                       AND actual.column_name = expected.column_name
                    WHERE actual.type_name <> expected.type_name
                       OR actual.max_length <> expected.max_length
                       OR (expected.expected_precision IS NOT NULL AND actual.precision <> expected.expected_precision)
                       OR (expected.expected_scale IS NOT NULL AND actual.scale <> expected.expected_scale)
                       OR actual.is_nullable <> expected.is_nullable
                       OR actual.is_identity <> expected.is_identity
                ) AS wrong_shape_count;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new QcColumnState(0, ExpectedQcColumnCount, 0, 0);
        }

        return new QcColumnState(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }

    private static async Task<BuildDeviceHardeningState> ReadBuildDeviceHardeningStateAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH expected_unicode AS (
                SELECT *
                FROM (VALUES
                    (N'builds', N'name', CONVERT(smallint, 510), CONVERT(bit, 0)),
                    (N'builds', N'notes', CONVERT(smallint, 1000), CONVERT(bit, 1)),
                    (N'build_items', N'notes', CONVERT(smallint, 1000), CONVERT(bit, 1)),
                    (N'build_mods', N'mod_type', CONVERT(smallint, 200), CONVERT(bit, 0)),
                    (N'build_mods', N'target_component', CONVERT(smallint, 200), CONVERT(bit, 0)),
                    (N'build_mods', N'notes', CONVERT(smallint, 1000), CONVERT(bit, 1)),
                    (N'build_requests', N'note', CONVERT(smallint, 1000), CONVERT(bit, 1))
                ) AS values_list(table_name, column_name, max_length, is_nullable)
            ),
            expected_indexes AS (
                SELECT index_name
                FROM (VALUES
                    (N'UX_build_requests_one_active_per_build'),
                    (N'UX_devices_one_active_qc_station_per_seller'),
                    (N'UX_dts_one_running_per_request')
                ) AS values_list(index_name)
            ),
            expected_checks AS (
                SELECT constraint_name
                FROM (VALUES
                    (N'CK_builds_name_not_blank'),
                    (N'CK_devices_name_not_blank'),
                    (N'CK_dts_switch_technology_not_blank'),
                    (N'CK_dts_total_keys'),
                    (N'CK_dts_completed_at'),
                    (N'CK_dktr_key_code_not_blank'),
                    (N'CK_dktr_press_count'),
                    (N'CK_dktr_latency'),
                    (N'CK_dktr_hold_duration'),
                    (N'CK_dktr_noise')
                ) AS values_list(constraint_name)
            )
            SELECT
                (
                    SELECT COUNT(*)
                    FROM expected_unicode AS expected
                    LEFT JOIN sys.tables AS table_info
                        ON table_info.name = expected.table_name
                       AND table_info.schema_id = SCHEMA_ID(N'dbo')
                    LEFT JOIN sys.columns AS column_info
                        ON column_info.object_id = table_info.object_id
                       AND column_info.name = expected.column_name
                    LEFT JOIN sys.types AS type_info
                        ON type_info.user_type_id = column_info.user_type_id
                    WHERE column_info.column_id IS NULL
                       OR type_info.name <> N'nvarchar'
                       OR column_info.max_length <> expected.max_length
                       OR column_info.is_nullable <> expected.is_nullable
                ) AS invalid_unicode_column_count,
                (
                    SELECT COUNT(*)
                    FROM expected_indexes AS expected
                    LEFT JOIN sys.indexes AS index_info
                        ON index_info.name = expected.index_name
                    WHERE index_info.index_id IS NULL
                       OR index_info.is_unique <> 1
                       OR index_info.has_filter <> 1
                       OR index_info.is_disabled <> 0
                ) AS invalid_index_count,
                (
                    SELECT COUNT(*)
                    FROM expected_checks AS expected
                    LEFT JOIN sys.check_constraints AS check_info
                        ON check_info.name = expected.constraint_name
                    WHERE check_info.object_id IS NULL
                       OR check_info.is_disabled <> 0
                       OR check_info.is_not_trusted <> 0
                ) AS invalid_check_constraint_count,
                (
                    SELECT COUNT(*)
                    FROM dbo.build_requests AS request_row
                    OUTER APPLY (
                        SELECT TOP (1)
                            session_row.status,
                            session_row.total_keys,
                            result_summary.tested_keys,
                            result_summary.failed_keys,
                            result_summary.warning_keys
                        FROM dbo.device_test_sessions AS session_row
                        OUTER APPLY (
                            SELECT
                                COUNT(result_row.id) AS tested_keys,
                                SUM(CASE WHEN result_row.result = 'Fail' THEN 1 ELSE 0 END) AS failed_keys,
                                SUM(CASE WHEN result_row.result = 'Warning' THEN 1 ELSE 0 END) AS warning_keys,
                                MAX(result_row.recorded_at) AS last_recorded_at
                            FROM dbo.device_key_test_results AS result_row
                            WHERE result_row.session_id = session_row.id
                        ) AS result_summary
                        WHERE session_row.request_id = request_row.id
                        ORDER BY
                            CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
                            session_row.completed_at DESC,
                            result_summary.last_recorded_at DESC,
                            session_row.id DESC
                    ) AS latest_qc
                    WHERE request_row.status = 'Completed'
                      AND (
                          latest_qc.status NOT IN ('Passed', 'Warning')
                          OR latest_qc.status IS NULL
                          OR latest_qc.tested_keys <> latest_qc.total_keys
                          OR COALESCE(latest_qc.failed_keys, 0) > 0
                          OR (
                              latest_qc.status = 'Passed'
                              AND COALESCE(latest_qc.warning_keys, 0) > 0
                          )
                          OR (
                              latest_qc.status = 'Warning'
                              AND COALESCE(latest_qc.warning_keys, 0) = 0
                          )
                      )
                ) AS invalid_completed_request_count;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new BuildDeviceHardeningState(7, 3, 10, 1);
        }

        return new BuildDeviceHardeningState(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
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

    private sealed record QcColumnState(
        int ColumnCount,
        int MissingColumnCount,
        int UnexpectedColumnCount,
        int WrongShapeCount);

    private sealed record ReadViewState(
        int ViewCount,
        int InvalidContractCount);

    private sealed record BuildDeviceHardeningState(
        int InvalidUnicodeColumnCount,
        int InvalidIndexCount,
        int InvalidCheckConstraintCount,
        int InvalidCompletedRequestCount);
}
