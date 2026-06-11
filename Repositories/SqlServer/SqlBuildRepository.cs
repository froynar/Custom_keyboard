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
        return QueryAsync(
            """
            SELECT
                build_id,
                user_id,
                layout_id,
                case_id,
                pcb_id,
                plate_id,
                switch_id,
                keycap_id,
                stab_id,
                name,
                notes,
                status,
                total_cost_snapshot,
                created_at,
                updated_at
            FROM builds
            WHERE user_id = @buyer_id
            ORDER BY created_at DESC, build_id;
            """,
            MapBuild,
            command => command.AddParameter("@buyer_id", SqlDbType.Int, buyerId),
            cancellationToken);
    }

    public async Task<KeyboardBuild?> GetByIdAsync(string buildId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                build_id,
                user_id,
                layout_id,
                case_id,
                pcb_id,
                plate_id,
                switch_id,
                keycap_id,
                stab_id,
                name,
                notes,
                status,
                total_cost_snapshot,
                created_at,
                updated_at
            FROM builds
            WHERE build_id = @build_id;
            """;
        command.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var build = MapBuild(reader);
        build.Mods = await GetBuildModsAsync(build.BuildId, cancellationToken);
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
            IF EXISTS (SELECT 1 FROM builds WHERE build_id = @build_id)
            BEGIN
                UPDATE builds
                SET
                    user_id = @user_id,
                    layout_id = @layout_id,
                    case_id = @case_id,
                    pcb_id = @pcb_id,
                    plate_id = @plate_id,
                    switch_id = @switch_id,
                    keycap_id = @keycap_id,
                    stab_id = @stab_id,
                    name = @name,
                    notes = @notes,
                    status = @status,
                    total_cost_snapshot = @total_cost_snapshot,
                    updated_at = SYSUTCDATETIME()
                WHERE build_id = @build_id;
            END
            ELSE
            BEGIN
                INSERT INTO builds (
                    build_id,
                    user_id,
                    layout_id,
                    case_id,
                    pcb_id,
                    plate_id,
                    switch_id,
                    keycap_id,
                    stab_id,
                    name,
                    notes,
                    status,
                    total_cost_snapshot,
                    created_at
                )
                VALUES (
                    @build_id,
                    @user_id,
                    @layout_id,
                    @case_id,
                    @pcb_id,
                    @plate_id,
                    @switch_id,
                    @keycap_id,
                    @stab_id,
                    @name,
                    @notes,
                    @status,
                    @total_cost_snapshot,
                    COALESCE(@created_at, SYSUTCDATETIME())
                );
            END;

            SELECT
                build_id,
                user_id,
                layout_id,
                case_id,
                pcb_id,
                plate_id,
                switch_id,
                keycap_id,
                stab_id,
                name,
                notes,
                status,
                total_cost_snapshot,
                created_at,
                updated_at
            FROM builds
            WHERE build_id = @build_id;
            """;
        AddBuildParameters(command, build);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var saved = MapBuild(reader);
            await reader.DisposeAsync();

            await ReplaceBuildModsAsync(connection, transaction, saved.BuildId, build.Mods, cancellationToken);
            saved.Mods = await GetBuildModsAsync(connection, transaction, saved.BuildId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return saved;
        }

        throw new InvalidOperationException("Could not save build.");
    }

    private async Task<IReadOnlyList<KeyboardBuild>> QueryAsync(
        string commandText,
        Func<SqlDataReader, KeyboardBuild> map,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
    {
        var results = new List<KeyboardBuild>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        foreach (var build in results)
        {
            build.Mods = await GetBuildModsAsync(build.BuildId, cancellationToken);
        }

        return results;
    }

    private async Task<List<BuildMod>> GetBuildModsAsync(string buildId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await GetBuildModsAsync(connection, null, buildId, cancellationToken);
    }

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
                mod_id,
                build_id,
                mod_type,
                target_component,
                lube_type,
                is_filmed,
                spring_weight_g,
                notes
            FROM build_mods
            WHERE build_id = @build_id
            ORDER BY mod_id;
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
                    lube_type,
                    is_filmed,
                    spring_weight_g,
                    notes
                )
                VALUES (
                    @build_id,
                    @mod_type,
                    @target_component,
                    @lube_type,
                    @is_filmed,
                    @spring_weight_g,
                    @notes
                );
                """;
            insertCommand.AddParameter("@build_id", SqlDbType.VarChar, buildId, 50);
            insertCommand.AddParameter("@mod_type", SqlDbType.VarChar, mod.ModType, 100);
            insertCommand.AddParameter("@target_component", SqlDbType.VarChar, mod.TargetComponent, 100);
            insertCommand.AddParameter("@lube_type", SqlDbType.VarChar, mod.LubeType, 100);
            insertCommand.AddParameter("@is_filmed", SqlDbType.Bit, mod.IsFilmed);
            insertCommand.AddParameter("@spring_weight_g", SqlDbType.Int, mod.SpringWeightG);
            insertCommand.AddParameter("@notes", SqlDbType.VarChar, mod.Notes, 500);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddBuildParameters(SqlCommand command, KeyboardBuild build)
    {
        command.AddParameter("@build_id", SqlDbType.VarChar, build.BuildId, 50);
        command.AddParameter("@user_id", SqlDbType.Int, build.UserId);
        command.AddParameter("@layout_id", SqlDbType.VarChar, build.LayoutId, 50);
        command.AddParameter("@case_id", SqlDbType.VarChar, build.CaseId, 50);
        command.AddParameter("@pcb_id", SqlDbType.VarChar, build.PcbId, 50);
        command.AddParameter("@plate_id", SqlDbType.VarChar, build.PlateId, 50);
        command.AddParameter("@switch_id", SqlDbType.VarChar, build.SwitchId, 50);
        command.AddParameter("@keycap_id", SqlDbType.VarChar, build.KeycapId, 50);
        command.AddParameter("@stab_id", SqlDbType.VarChar, build.StabilizerId, 50);
        command.AddParameter("@name", SqlDbType.VarChar, build.Name, 255);
        command.AddParameter("@notes", SqlDbType.VarChar, build.Notes, 500);
        command.AddParameter("@status", SqlDbType.VarChar, build.Status.ToString(), 50);
        command.AddDecimalParameter("@total_cost_snapshot", build.TotalCostSnapshot);
        command.AddParameter("@created_at", SqlDbType.DateTime2, build.CreatedAt == default ? null : build.CreatedAt);
    }

    private static BuildMod MapBuildMod(SqlDataReader reader)
    {
        return new BuildMod
        {
            ModId = reader.GetIntValue("mod_id"),
            BuildId = reader.GetStringValue("build_id"),
            ModType = reader.GetStringValue("mod_type"),
            TargetComponent = reader.GetStringValue("target_component"),
            LubeType = reader.GetNullableStringValue("lube_type"),
            IsFilmed = reader.GetBoolValue("is_filmed"),
            SpringWeightG = reader.GetNullableIntValue("spring_weight_g"),
            Notes = reader.GetNullableStringValue("notes")
        };
    }

    private static KeyboardBuild MapBuild(SqlDataReader reader)
    {
        return new KeyboardBuild
        {
            BuildId = reader.GetStringValue("build_id"),
            UserId = reader.GetIntValue("user_id"),
            LayoutId = reader.GetStringValue("layout_id"),
            CaseId = reader.GetNullableStringValue("case_id"),
            PcbId = reader.GetNullableStringValue("pcb_id"),
            PlateId = reader.GetNullableStringValue("plate_id"),
            SwitchId = reader.GetNullableStringValue("switch_id"),
            KeycapId = reader.GetNullableStringValue("keycap_id"),
            StabilizerId = reader.GetNullableStringValue("stab_id"),
            Name = reader.GetStringValue("name"),
            Notes = reader.GetNullableStringValue("notes"),
            Status = reader.GetEnumValue<BuildStatus>("status"),
            TotalCostSnapshot = reader.GetDecimalValue("total_cost_snapshot"),
            CreatedAt = reader.GetDateTimeValue("created_at"),
            UpdatedAt = reader.GetNullableDateTimeValue("updated_at")
        };
    }
}
