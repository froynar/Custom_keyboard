using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlBuildRepository : IBuildRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlBuildRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<KeyboardBuild>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default)
    {
        // Archived builds are soft-removed from the buyer's main list (use case: archive hides a build).
        return QueryAsync(
            $"{BuildSelectSql} WHERE buyer_id = @buyer_id AND status <> 'Archived' ORDER BY created_at DESC, id;",
            command => command.AddParameter("@buyer_id", SqlDbType.Int, buyerId),
            cancellationToken);
    }

    public async Task<KeyboardBuild?> GetByIdAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BuildSelectSql} WHERE id = @build_id;";
        command.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var build = MapBuild(reader);
        await reader.DisposeAsync();

        build.Items = await GetBuildItemsAsync(connection, null, build.BuildId, cancellationToken);
        build.Mods = await GetBuildModsAsync(connection, null, build.BuildId, cancellationToken);
        return build;
    }

    public async Task<KeyboardBuild> SaveAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(build.BuildId))
        {
            build.BuildId = $"BUILD_{Guid.NewGuid():N}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM builds WHERE id = @build_id)
            BEGIN
                UPDATE builds
                SET
                    buyer_id = @buyer_id,
                    kit_id = @kit_id,
                    name = @name,
                    notes = @notes,
                    noise_requirement = @noise_requirement,
                    status = @status,
                    total_cost_snapshot = @total_cost_snapshot,
                    updated_at = SYSUTCDATETIME()
                WHERE id = @build_id;
            END
            ELSE
            BEGIN
                INSERT INTO builds (
                    id,
                    buyer_id,
                    kit_id,
                    name,
                    notes,
                    noise_requirement,
                    status,
                    total_cost_snapshot,
                    created_at
                )
                VALUES (
                    @build_id,
                    @buyer_id,
                    @kit_id,
                    @name,
                    @notes,
                    @noise_requirement,
                    @status,
                    @total_cost_snapshot,
                    COALESCE(@created_at, SYSUTCDATETIME())
                );
            END;

            SELECT
                id AS build_id,
                buyer_id,
                kit_id,
                name,
                notes,
                noise_requirement,
                status,
                total_cost_snapshot,
                created_at,
                updated_at
            FROM builds
            WHERE id = @build_id;
            """;
        AddBuildParameters(command, build);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Could not save build.");
        }

        var saved = MapBuild(reader);
        await reader.DisposeAsync();

        await ReplaceBuildItemsAsync(connection, transaction, saved.BuildId, build.Items, cancellationToken);
        await ReplaceBuildModsAsync(connection, transaction, saved.BuildId, build.Mods, cancellationToken);

        saved.Items = await GetBuildItemsAsync(connection, transaction, saved.BuildId, cancellationToken);
        saved.Mods = await GetBuildModsAsync(connection, transaction, saved.BuildId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    public async Task SetStatusAsync(string buildId, BuildStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE builds
            SET status = @status,
                updated_at = SYSUTCDATETIME()
            WHERE id = @build_id;
            """;
        command.AddParameter("@status", SqlDbType.VarChar, status.ToString(), 50);
        command.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task ArchiveAsync(string buildId, CancellationToken cancellationToken = default)
        => SetStatusAsync(buildId, BuildStatus.Archived, cancellationToken);

    private const string BuildSelectSql = """
        SELECT
            id AS build_id,
            buyer_id,
            kit_id,
            name,
            notes,
            noise_requirement,
            status,
            total_cost_snapshot,
            created_at,
            updated_at
        FROM builds
        """;

    private async Task<IReadOnlyList<KeyboardBuild>> QueryAsync(
        string commandText,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
    {
        var results = new List<KeyboardBuild>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = commandText;
            configureCommand?.Invoke(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapBuild(reader));
            }
        }

        foreach (var build in results)
        {
            build.Items = await GetBuildItemsAsync(connection, null, build.BuildId, cancellationToken);
            build.Mods = await GetBuildModsAsync(connection, null, build.BuildId, cancellationToken);
        }

        return results;
    }

    // ------------------------------------------------------------- Build items
    private static async Task<List<BuildItem>> GetBuildItemsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string buildId,
        CancellationToken cancellationToken)
    {
        var items = new List<BuildItem>();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                id AS build_item_id,
                build_id,
                switch_id,
                keycap_id,
                stab_id,
                accessory_id,
                quantity,
                unit_price_snapshot,
                notes
            FROM build_items
            WHERE build_id = @build_id
            ORDER BY id;
            """;
        command.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapBuildItem(reader));
        }

        return items;
    }

    private static async Task ReplaceBuildItemsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string buildId,
        IEnumerable<BuildItem> items,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM build_items WHERE build_id = @build_id;";
            deleteCommand.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO build_items (
                    build_id,
                    switch_id,
                    keycap_id,
                    stab_id,
                    accessory_id,
                    quantity,
                    unit_price_snapshot,
                    notes
                )
                VALUES (
                    @build_id,
                    @switch_id,
                    @keycap_id,
                    @stab_id,
                    @accessory_id,
                    @quantity,
                    @unit_price_snapshot,
                    @notes
                );
                """;
            insertCommand.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
            insertCommand.AddParameter("@switch_id", SqlDbType.VarChar, item.SwitchId, 50);
            insertCommand.AddParameter("@keycap_id", SqlDbType.VarChar, item.KeycapId, 50);
            insertCommand.AddParameter("@stab_id", SqlDbType.VarChar, item.StabilizerId, 50);
            insertCommand.AddParameter("@accessory_id", SqlDbType.VarChar, item.AccessoryId, 50);
            insertCommand.AddParameter("@quantity", SqlDbType.Int, item.Quantity);
            insertCommand.AddDecimalParameter("@unit_price_snapshot", item.UnitPriceSnapshot);
            insertCommand.AddParameter("@notes", SqlDbType.VarChar, item.Notes, 500);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // -------------------------------------------------------------- Build mods
    private static async Task<List<BuildMod>> GetBuildModsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string buildId,
        CancellationToken cancellationToken)
    {
        var mods = new List<BuildMod>();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                id AS mod_id,
                build_id,
                mod_type,
                target_component,
                notes
            FROM build_mods
            WHERE build_id = @build_id
            ORDER BY id;
            """;
        command.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            mods.Add(MapBuildMod(reader));
        }

        return mods;
    }

    private static async Task ReplaceBuildModsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string buildId,
        IEnumerable<BuildMod> mods,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM build_mods WHERE build_id = @build_id;";
            deleteCommand.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var mod in mods)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO build_mods (
                    build_id,
                    mod_type,
                    target_component,
                    notes
                )
                VALUES (
                    @build_id,
                    @mod_type,
                    @target_component,
                    @notes
                );
                """;
            insertCommand.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
            insertCommand.AddParameter("@mod_type", SqlDbType.VarChar, mod.ModType, 100);
            insertCommand.AddParameter("@target_component", SqlDbType.VarChar, mod.TargetComponent, 100);
            insertCommand.AddParameter("@notes", SqlDbType.VarChar, mod.Notes, 500);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // ------------------------------------------------------------------ Mappers
    private static void AddBuildParameters(SqlCommand command, KeyboardBuild build)
    {
        command.AddParameter("@build_id", SqlDbType.VarChar, build.BuildId, 50);
        command.AddParameter("@buyer_id", SqlDbType.Int, build.BuyerId);
        command.AddParameter("@kit_id", SqlDbType.VarChar, build.KitId, 50);
        command.AddParameter("@name", SqlDbType.VarChar, build.Name, 255);
        command.AddParameter("@notes", SqlDbType.VarChar, build.Notes, 500);
        command.AddParameter("@noise_requirement", SqlDbType.VarChar, build.NoiseRequirement.ToString(), 20);
        command.AddParameter("@status", SqlDbType.VarChar, build.Status.ToString(), 50);
        command.AddDecimalParameter("@total_cost_snapshot", build.TotalCostSnapshot);
        command.AddParameter("@created_at", SqlDbType.DateTime2, build.CreatedAt == default ? null : build.CreatedAt);
    }

    private static BuildItem MapBuildItem(SqlDataReader reader)
    {
        return new BuildItem
        {
            BuildItemId = reader.GetIntValue("build_item_id"),
            BuildId = reader.GetStringValue("build_id"),
            SwitchId = reader.GetNullableStringValue("switch_id"),
            KeycapId = reader.GetNullableStringValue("keycap_id"),
            StabilizerId = reader.GetNullableStringValue("stab_id"),
            AccessoryId = reader.GetNullableStringValue("accessory_id"),
            Quantity = reader.GetIntValue("quantity"),
            UnitPriceSnapshot = reader.GetDecimalValue("unit_price_snapshot"),
            Notes = reader.GetNullableStringValue("notes")
        };
    }

    private static BuildMod MapBuildMod(SqlDataReader reader)
    {
        return new BuildMod
        {
            ModId = reader.GetIntValue("mod_id"),
            BuildId = reader.GetStringValue("build_id"),
            ModType = reader.GetStringValue("mod_type"),
            TargetComponent = reader.GetStringValue("target_component"),
            Notes = reader.GetNullableStringValue("notes")
        };
    }

    private static KeyboardBuild MapBuild(SqlDataReader reader)
    {
        return new KeyboardBuild
        {
            BuildId = reader.GetStringValue("build_id"),
            BuyerId = reader.GetIntValue("buyer_id"),
            KitId = reader.GetStringValue("kit_id"),
            Name = reader.GetStringValue("name"),
            Notes = reader.GetNullableStringValue("notes"),
            NoiseRequirement = reader.GetEnumValue<NoiseRequirement>("noise_requirement"),
            Status = reader.GetEnumValue<BuildStatus>("status"),
            TotalCostSnapshot = reader.GetDecimalValue("total_cost_snapshot"),
            CreatedAt = reader.GetDateTimeValue("created_at"),
            UpdatedAt = reader.GetNullableDateTimeValue("updated_at")
        };
    }
}
