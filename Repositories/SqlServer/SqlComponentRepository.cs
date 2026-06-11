using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlComponentRepository : IComponentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlComponentRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT brand_id, brand_name, country
            FROM brands
            ORDER BY brand_name;
            """,
            MapBrand,
            cancellationToken: cancellationToken);
    }

    public async Task<Brand?> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT brand_id, brand_name, country
            FROM brands
            WHERE brand_id = @brand_id;
            """;
        command.AddParameter("@brand_id", SqlDbType.Int, brandId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapBrand(reader) : null;
    }

    public async Task<Brand> SaveBrandAsync(Brand brand, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF @brand_id > 0 AND EXISTS (SELECT 1 FROM brands WHERE brand_id = @brand_id)
            BEGIN
                UPDATE brands
                SET brand_name = @brand_name,
                    country = @country
                WHERE brand_id = @brand_id;
            END
            ELSE
            BEGIN
                INSERT INTO brands (brand_name, country)
                VALUES (@brand_name, @country);

                SET @brand_id = CAST(SCOPE_IDENTITY() AS INT);
            END;

            SELECT brand_id, brand_name, country
            FROM brands
            WHERE brand_id = @brand_id;
            """;
        var idParameter = command.AddParameter("@brand_id", SqlDbType.Int, brand.BrandId);
        idParameter.Direction = ParameterDirection.InputOutput;
        command.AddParameter("@brand_name", SqlDbType.VarChar, brand.BrandName.Trim(), 255);
        command.AddParameter("@country", SqlDbType.VarChar, string.IsNullOrWhiteSpace(brand.Country) ? null : brand.Country.Trim(), 100);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapBrand(reader);
        }

        throw new InvalidOperationException("Could not save brand.");
    }

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT layout_id, layout_name, form_factor, standard_key_count
            FROM layouts
            ORDER BY standard_key_count, layout_name;
            """,
            MapLayout,
            cancellationToken: cancellationToken);
    }

    public async Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT layout_id, layout_name, form_factor, standard_key_count
            FROM layouts
            WHERE layout_id = @layout_id;
            """;
        command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapLayout(reader) : null;
    }

    public async Task<Layout> SaveLayoutAsync(Layout layout, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM layouts WHERE layout_id = @layout_id)
            BEGIN
                UPDATE layouts
                SET layout_name = @layout_name,
                    form_factor = @form_factor,
                    standard_key_count = @standard_key_count
                WHERE layout_id = @layout_id;
            END
            ELSE
            BEGIN
                INSERT INTO layouts (layout_id, layout_name, form_factor, standard_key_count)
                VALUES (@layout_id, @layout_name, @form_factor, @standard_key_count);
            END;

            SELECT layout_id, layout_name, form_factor, standard_key_count
            FROM layouts
            WHERE layout_id = @layout_id;
            """;
        command.AddParameter("@layout_id", SqlDbType.VarChar, layout.LayoutId.Trim(), 50);
        command.AddParameter("@layout_name", SqlDbType.VarChar, layout.LayoutName.Trim(), 100);
        command.AddParameter("@form_factor", SqlDbType.VarChar, layout.FormFactor.Trim(), 100);
        command.AddParameter("@standard_key_count", SqlDbType.Int, layout.StandardKeyCount);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapLayout(reader);
        }

        throw new InvalidOperationException("Could not save layout.");
    }

    public Task<IReadOnlyList<KeyboardCase>> GetCasesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT c.case_id, c.brand_id, c.material, c.mount_type, c.color, c.weight_g, c.price_usd, c.is_available
            FROM cases AS c
            INNER JOIN case_layouts AS cl ON cl.case_id = c.case_id
            WHERE cl.layout_id = @layout_id
              AND c.is_available = 1
            ORDER BY cl.is_primary DESC, c.case_id;
            """,
            MapCase,
            command => command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50),
            cancellationToken);
    }

    public Task<IReadOnlyList<Pcb>> GetPcbsForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT p.pcb_id, p.brand_id, p.pcb_technology, p.mount_type, p.hotswap, p.wireless, p.rgb, p.switch_mount, p.price_usd, p.is_available
            FROM pcbs AS p
            INNER JOIN pcb_layouts AS pl ON pl.pcb_id = p.pcb_id
            WHERE pl.layout_id = @layout_id
              AND p.is_available = 1
            ORDER BY p.pcb_id;
            """,
            MapPcb,
            command => command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50),
            cancellationToken);
    }

    public Task<IReadOnlyList<Plate>> GetPlatesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT p.plate_id, p.brand_id, p.material, p.mount_type, p.flex_cut, p.price_usd, p.is_available
            FROM plates AS p
            INNER JOIN plate_layouts AS pl ON pl.plate_id = p.plate_id
            WHERE pl.layout_id = @layout_id
              AND p.is_available = 1
            ORDER BY p.plate_id;
            """,
            MapPlate,
            command => command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50),
            cancellationToken);
    }

    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT switch_id, brand_id, switch_technology, switch_type, actuation_force_g, mount_type, sound_profile, price_usd, is_available
            FROM switches
            WHERE is_available = 1
            ORDER BY switch_id;
            """,
            MapSwitch,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT keycap_id, brand_id, profile, material, color_primary, legend_type, price_usd, is_available
            FROM keycap_sets
            WHERE is_available = 1
            ORDER BY keycap_id;
            """,
            MapKeycapSet,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT stab_id, brand_id, stab_type, sizes_included, price_usd, is_available
            FROM stabilizers
            WHERE is_available = 1
            ORDER BY stab_id;
            """,
            MapStabilizer,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<CompatibilityRule>> GetCompatibilityRulesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT rule_id, case_id, pcb_id, plate_id, is_compatible, notes
            FROM compatibility_rules
            ORDER BY rule_id;
            """,
            MapCompatibilityRule,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(
        AdminComponentType componentType,
        CancellationToken cancellationToken = default)
    {
        var components = await QueryAsync(
            GetAdminSelectSql(componentType),
            reader => MapAdminComponent(reader, componentType),
            cancellationToken: cancellationToken);

        foreach (var component in components)
        {
            await PopulateLayoutMappingsAsync(component, cancellationToken);
        }

        return components;
    }

    public async Task<AdminComponentRecord?> GetAdminComponentByIdAsync(
        AdminComponentType componentType,
        string componentId,
        CancellationToken cancellationToken = default)
    {
        var (tableName, idColumn) = GetAdminTableInfo(componentType);
        var component = await QuerySingleAdminComponentAsync(
            $"{GetAdminSelectSql(componentType)} WHERE {tableName}.{idColumn} = @component_id;",
            componentType,
            command => command.AddParameter("@component_id", SqlDbType.VarChar, componentId, 50),
            cancellationToken);
        if (component is not null)
        {
            await PopulateLayoutMappingsAsync(component, cancellationToken);
        }

        return component;
    }

    public async Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM cases)
              + (SELECT COUNT(*) FROM pcbs)
              + (SELECT COUNT(*) FROM plates)
              + (SELECT COUNT(*) FROM switches)
              + (SELECT COUNT(*) FROM keycap_sets)
              + (SELECT COUNT(*) FROM stabilizers);
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<AdminComponentRecord> SaveAdminComponentAsync(
        AdminComponentRecord component,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = GetAdminSaveSql(component.ComponentType);
            AddAdminComponentParameters(command, component);
            await command.ExecuteNonQueryAsync(cancellationToken);

            await ReplaceLayoutMappingsAsync(connection, transaction, component, cancellationToken);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        var saved = await GetAdminComponentByIdAsync(component.ComponentType, component.ComponentId, cancellationToken);
        return saved ?? throw new InvalidOperationException("Could not save component.");
    }

    public async Task SetComponentAvailabilityAsync(
        AdminComponentType componentType,
        string componentId,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        var (tableName, idColumn) = GetAdminTableInfo(componentType);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {tableName}
            SET is_available = @is_available
            WHERE {idColumn} = @component_id;
            """;
        command.AddParameter("@component_id", SqlDbType.VarChar, componentId, 50);
        command.AddParameter("@is_available", SqlDbType.Bit, isAvailable);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string commandText,
        Func<SqlDataReader, T> map,
        Action<SqlCommand>? configureCommand = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();

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

        return results;
    }

    private async Task<AdminComponentRecord?> QuerySingleAdminComponentAsync(
        string commandText,
        AdminComponentType componentType,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAdminComponent(reader, componentType) : null;
    }

    private async Task PopulateLayoutMappingsAsync(AdminComponentRecord component, CancellationToken cancellationToken)
    {
        var commandText = component.ComponentType switch
        {
            AdminComponentType.Case => """
                SELECT layout_id, is_primary, CAST(NULL AS varchar(100)) AS variant_name
                FROM case_layouts
                WHERE case_id = @component_id
                ORDER BY is_primary DESC, layout_id;
                """,
            AdminComponentType.Pcb => """
                SELECT layout_id, CAST(0 AS bit) AS is_primary, variant_name
                FROM pcb_layouts
                WHERE pcb_id = @component_id
                ORDER BY layout_id;
                """,
            AdminComponentType.Plate => """
                SELECT layout_id, CAST(0 AS bit) AS is_primary, CAST(NULL AS varchar(100)) AS variant_name
                FROM plate_layouts
                WHERE plate_id = @component_id
                ORDER BY layout_id;
                """,
            _ => null
        };

        if (commandText is null)
        {
            component.SupportedLayoutIds = [];
            component.PrimaryLayoutId = string.Empty;
            component.PcbVariantName = string.Empty;
            return;
        }

        var layoutIds = new List<string>();
        string? primaryLayoutId = null;
        string? pcbVariantName = null;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.AddParameter("@component_id", SqlDbType.VarChar, component.ComponentId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var layoutId = reader.GetStringValue("layout_id");
            layoutIds.Add(layoutId);

            if (reader.GetBoolValue("is_primary"))
            {
                primaryLayoutId = layoutId;
            }

            pcbVariantName ??= reader.GetNullableStringValue("variant_name");
        }

        component.SupportedLayoutIds = layoutIds;
        component.PrimaryLayoutId = primaryLayoutId ?? layoutIds.FirstOrDefault() ?? string.Empty;
        component.PcbVariantName = pcbVariantName ?? string.Empty;
    }

    private static async Task ReplaceLayoutMappingsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AdminComponentRecord component,
        CancellationToken cancellationToken)
    {
        if (component.ComponentType is not (AdminComponentType.Case or AdminComponentType.Pcb or AdminComponentType.Plate))
        {
            return;
        }

        var layoutIds = component.SupportedLayoutIds
            .Where(layoutId => !string.IsNullOrWhiteSpace(layoutId))
            .Select(layoutId => layoutId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await DeleteLayoutMappingsAsync(connection, transaction, component, cancellationToken);

        if (layoutIds.Count == 0)
        {
            return;
        }

        var primaryLayoutId = string.IsNullOrWhiteSpace(component.PrimaryLayoutId)
            ? layoutIds[0]
            : component.PrimaryLayoutId.Trim();

        if (!layoutIds.Contains(primaryLayoutId, StringComparer.OrdinalIgnoreCase))
        {
            primaryLayoutId = layoutIds[0];
        }

        foreach (var layoutId in layoutIds)
        {
            await InsertLayoutMappingAsync(
                connection,
                transaction,
                component,
                layoutId,
                string.Equals(layoutId, primaryLayoutId, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
        }
    }

    private static async Task DeleteLayoutMappingsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AdminComponentRecord component,
        CancellationToken cancellationToken)
    {
        var (tableName, idColumn) = component.ComponentType switch
        {
            AdminComponentType.Case => ("case_layouts", "case_id"),
            AdminComponentType.Pcb => ("pcb_layouts", "pcb_id"),
            AdminComponentType.Plate => ("plate_layouts", "plate_id"),
            _ => throw new ArgumentOutOfRangeException(nameof(component.ComponentType), component.ComponentType, null)
        };

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {tableName} WHERE {idColumn} = @component_id;";
        command.AddParameter("@component_id", SqlDbType.VarChar, component.ComponentId, 50);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLayoutMappingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AdminComponentRecord component,
        string layoutId,
        bool isPrimary,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = component.ComponentType switch
        {
            AdminComponentType.Case => """
                INSERT INTO case_layouts (case_id, layout_id, is_primary)
                VALUES (@component_id, @layout_id, @is_primary);
                """,
            AdminComponentType.Pcb => """
                INSERT INTO pcb_layouts (pcb_id, layout_id, variant_name)
                VALUES (@component_id, @layout_id, @variant_name);
                """,
            AdminComponentType.Plate => """
                INSERT INTO plate_layouts (plate_id, layout_id)
                VALUES (@component_id, @layout_id);
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(component.ComponentType), component.ComponentType, null)
        };
        command.AddParameter("@component_id", SqlDbType.VarChar, component.ComponentId, 50);
        command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50);
        command.AddParameter("@is_primary", SqlDbType.Bit, isPrimary);
        command.AddParameter("@variant_name", SqlDbType.VarChar, string.IsNullOrWhiteSpace(component.PcbVariantName) ? null : component.PcbVariantName.Trim(), 100);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (string TableName, string IdColumn) GetAdminTableInfo(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Case => ("cases", "case_id"),
            AdminComponentType.Pcb => ("pcbs", "pcb_id"),
            AdminComponentType.Plate => ("plates", "plate_id"),
            AdminComponentType.Switch => ("switches", "switch_id"),
            AdminComponentType.KeycapSet => ("keycap_sets", "keycap_id"),
            AdminComponentType.Stabilizer => ("stabilizers", "stab_id"),
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static string GetAdminSelectSql(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Case => """
                SELECT
                    cases.case_id AS component_id,
                    cases.brand_id,
                    cases.price_usd,
                    cases.is_available,
                    cases.material,
                    cases.mount_type,
                    cases.color,
                    cases.weight_g,
                    CAST('' AS varchar(100)) AS technology,
                    CAST(0 AS bit) AS hotswap,
                    CAST(0 AS bit) AS wireless,
                    CAST(0 AS bit) AS rgb,
                    CAST('' AS varchar(100)) AS switch_mount,
                    CAST('' AS varchar(100)) AS flex_cut,
                    CAST('' AS varchar(100)) AS switch_type,
                    0 AS actuation_force_g,
                    CAST('' AS varchar(100)) AS sound_profile,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS color_primary,
                    CAST('' AS varchar(100)) AS legend_type,
                    CAST('' AS varchar(100)) AS stabilizer_type,
                    CAST('' AS varchar(255)) AS sizes_included
                FROM cases
                """,
            AdminComponentType.Pcb => """
                SELECT
                    pcbs.pcb_id AS component_id,
                    pcbs.brand_id,
                    pcbs.price_usd,
                    pcbs.is_available,
                    CAST('' AS varchar(100)) AS material,
                    pcbs.mount_type,
                    CAST('' AS varchar(100)) AS color,
                    0 AS weight_g,
                    pcbs.pcb_technology AS technology,
                    pcbs.hotswap,
                    pcbs.wireless,
                    pcbs.rgb,
                    pcbs.switch_mount,
                    CAST('' AS varchar(100)) AS flex_cut,
                    CAST('' AS varchar(100)) AS switch_type,
                    0 AS actuation_force_g,
                    CAST('' AS varchar(100)) AS sound_profile,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS color_primary,
                    CAST('' AS varchar(100)) AS legend_type,
                    CAST('' AS varchar(100)) AS stabilizer_type,
                    CAST('' AS varchar(255)) AS sizes_included
                FROM pcbs
                """,
            AdminComponentType.Plate => """
                SELECT
                    plates.plate_id AS component_id,
                    plates.brand_id,
                    plates.price_usd,
                    plates.is_available,
                    plates.material,
                    plates.mount_type,
                    CAST('' AS varchar(100)) AS color,
                    0 AS weight_g,
                    CAST('' AS varchar(100)) AS technology,
                    CAST(0 AS bit) AS hotswap,
                    CAST(0 AS bit) AS wireless,
                    CAST(0 AS bit) AS rgb,
                    CAST('' AS varchar(100)) AS switch_mount,
                    plates.flex_cut,
                    CAST('' AS varchar(100)) AS switch_type,
                    0 AS actuation_force_g,
                    CAST('' AS varchar(100)) AS sound_profile,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS color_primary,
                    CAST('' AS varchar(100)) AS legend_type,
                    CAST('' AS varchar(100)) AS stabilizer_type,
                    CAST('' AS varchar(255)) AS sizes_included
                FROM plates
                """,
            AdminComponentType.Switch => """
                SELECT
                    switches.switch_id AS component_id,
                    switches.brand_id,
                    switches.price_usd,
                    switches.is_available,
                    CAST('' AS varchar(100)) AS material,
                    switches.mount_type,
                    CAST('' AS varchar(100)) AS color,
                    0 AS weight_g,
                    switches.switch_technology AS technology,
                    CAST(0 AS bit) AS hotswap,
                    CAST(0 AS bit) AS wireless,
                    CAST(0 AS bit) AS rgb,
                    CAST('' AS varchar(100)) AS switch_mount,
                    CAST('' AS varchar(100)) AS flex_cut,
                    switches.switch_type,
                    switches.actuation_force_g,
                    switches.sound_profile,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS color_primary,
                    CAST('' AS varchar(100)) AS legend_type,
                    CAST('' AS varchar(100)) AS stabilizer_type,
                    CAST('' AS varchar(255)) AS sizes_included
                FROM switches
                """,
            AdminComponentType.KeycapSet => """
                SELECT
                    keycap_sets.keycap_id AS component_id,
                    keycap_sets.brand_id,
                    keycap_sets.price_usd,
                    keycap_sets.is_available,
                    keycap_sets.material,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS color,
                    0 AS weight_g,
                    CAST('' AS varchar(100)) AS technology,
                    CAST(0 AS bit) AS hotswap,
                    CAST(0 AS bit) AS wireless,
                    CAST(0 AS bit) AS rgb,
                    CAST('' AS varchar(100)) AS switch_mount,
                    CAST('' AS varchar(100)) AS flex_cut,
                    CAST('' AS varchar(100)) AS switch_type,
                    0 AS actuation_force_g,
                    CAST('' AS varchar(100)) AS sound_profile,
                    keycap_sets.profile,
                    keycap_sets.color_primary,
                    keycap_sets.legend_type,
                    CAST('' AS varchar(100)) AS stabilizer_type,
                    CAST('' AS varchar(255)) AS sizes_included
                FROM keycap_sets
                """,
            AdminComponentType.Stabilizer => """
                SELECT
                    stabilizers.stab_id AS component_id,
                    stabilizers.brand_id,
                    stabilizers.price_usd,
                    stabilizers.is_available,
                    CAST('' AS varchar(100)) AS material,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS color,
                    0 AS weight_g,
                    CAST('' AS varchar(100)) AS technology,
                    CAST(0 AS bit) AS hotswap,
                    CAST(0 AS bit) AS wireless,
                    CAST(0 AS bit) AS rgb,
                    CAST('' AS varchar(100)) AS switch_mount,
                    CAST('' AS varchar(100)) AS flex_cut,
                    CAST('' AS varchar(100)) AS switch_type,
                    0 AS actuation_force_g,
                    CAST('' AS varchar(100)) AS sound_profile,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS color_primary,
                    CAST('' AS varchar(100)) AS legend_type,
                    stabilizers.stab_type AS stabilizer_type,
                    stabilizers.sizes_included
                FROM stabilizers
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static string GetAdminSaveSql(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Case => """
                IF EXISTS (SELECT 1 FROM cases WHERE case_id = @component_id)
                BEGIN
                    UPDATE cases
                    SET brand_id = @brand_id,
                        material = @material,
                        mount_type = @mount_type,
                        color = @color,
                        weight_g = @weight_g,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE case_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO cases (case_id, brand_id, material, mount_type, color, weight_g, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @material, @mount_type, @color, @weight_g, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Pcb => """
                IF EXISTS (SELECT 1 FROM pcbs WHERE pcb_id = @component_id)
                BEGIN
                    UPDATE pcbs
                    SET brand_id = @brand_id,
                        pcb_technology = @technology,
                        mount_type = @mount_type,
                        hotswap = @hotswap,
                        wireless = @wireless,
                        rgb = @rgb,
                        switch_mount = @switch_mount,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE pcb_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO pcbs (pcb_id, brand_id, pcb_technology, mount_type, hotswap, wireless, rgb, switch_mount, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @technology, @mount_type, @hotswap, @wireless, @rgb, @switch_mount, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Plate => """
                IF EXISTS (SELECT 1 FROM plates WHERE plate_id = @component_id)
                BEGIN
                    UPDATE plates
                    SET brand_id = @brand_id,
                        material = @material,
                        mount_type = @mount_type,
                        flex_cut = @flex_cut,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE plate_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO plates (plate_id, brand_id, material, mount_type, flex_cut, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @material, @mount_type, @flex_cut, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Switch => """
                IF EXISTS (SELECT 1 FROM switches WHERE switch_id = @component_id)
                BEGIN
                    UPDATE switches
                    SET brand_id = @brand_id,
                        switch_technology = @technology,
                        switch_type = @switch_type,
                        actuation_force_g = @actuation_force_g,
                        mount_type = @mount_type,
                        sound_profile = @sound_profile,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE switch_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO switches (switch_id, brand_id, switch_technology, switch_type, actuation_force_g, mount_type, sound_profile, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @technology, @switch_type, @actuation_force_g, @mount_type, @sound_profile, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.KeycapSet => """
                IF EXISTS (SELECT 1 FROM keycap_sets WHERE keycap_id = @component_id)
                BEGIN
                    UPDATE keycap_sets
                    SET brand_id = @brand_id,
                        profile = @profile,
                        material = @material,
                        color_primary = @color_primary,
                        legend_type = @legend_type,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE keycap_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO keycap_sets (keycap_id, brand_id, profile, material, color_primary, legend_type, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @profile, @material, @color_primary, @legend_type, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Stabilizer => """
                IF EXISTS (SELECT 1 FROM stabilizers WHERE stab_id = @component_id)
                BEGIN
                    UPDATE stabilizers
                    SET brand_id = @brand_id,
                        stab_type = @stabilizer_type,
                        sizes_included = @sizes_included,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE stab_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO stabilizers (stab_id, brand_id, stab_type, sizes_included, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @stabilizer_type, @sizes_included, @price_usd, @is_available);
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static void AddAdminComponentParameters(SqlCommand command, AdminComponentRecord component)
    {
        command.AddParameter("@component_id", SqlDbType.VarChar, component.ComponentId.Trim(), 50);
        command.AddParameter("@brand_id", SqlDbType.Int, component.BrandId);
        command.AddDecimalParameter("@price_usd", component.PriceUsd);
        command.AddParameter("@is_available", SqlDbType.Bit, component.IsAvailable);
        command.AddParameter("@material", SqlDbType.VarChar, component.Material.Trim(), 100);
        command.AddParameter("@mount_type", SqlDbType.VarChar, component.MountType.Trim(), 100);
        command.AddParameter("@color", SqlDbType.VarChar, component.Color.Trim(), 100);
        command.AddParameter("@weight_g", SqlDbType.Int, component.WeightG);
        command.AddParameter("@technology", SqlDbType.VarChar, component.Technology.Trim(), 50);
        command.AddParameter("@hotswap", SqlDbType.Bit, component.Hotswap);
        command.AddParameter("@wireless", SqlDbType.Bit, component.Wireless);
        command.AddParameter("@rgb", SqlDbType.Bit, component.Rgb);
        command.AddParameter("@switch_mount", SqlDbType.VarChar, component.SwitchMount.Trim(), 100);
        command.AddParameter("@flex_cut", SqlDbType.VarChar, component.FlexCut.Trim(), 100);
        command.AddParameter("@switch_type", SqlDbType.VarChar, component.SwitchType.Trim(), 100);
        command.AddParameter("@actuation_force_g", SqlDbType.Int, component.ActuationForceG);
        command.AddParameter("@sound_profile", SqlDbType.VarChar, component.SoundProfile.Trim(), 100);
        command.AddParameter("@profile", SqlDbType.VarChar, component.Profile.Trim(), 100);
        command.AddParameter("@color_primary", SqlDbType.VarChar, component.ColorPrimary.Trim(), 100);
        command.AddParameter("@legend_type", SqlDbType.VarChar, component.LegendType.Trim(), 100);
        command.AddParameter("@stabilizer_type", SqlDbType.VarChar, component.StabilizerType.Trim(), 100);
        command.AddParameter("@sizes_included", SqlDbType.VarChar, component.SizesIncluded.Trim(), 255);
    }

    private static Brand MapBrand(SqlDataReader reader)
    {
        return new Brand
        {
            BrandId = reader.GetIntValue("brand_id"),
            BrandName = reader.GetStringValue("brand_name"),
            Country = reader.GetNullableStringValue("country")
        };
    }

    private static Layout MapLayout(SqlDataReader reader)
    {
        return new Layout
        {
            LayoutId = reader.GetStringValue("layout_id"),
            LayoutName = reader.GetStringValue("layout_name"),
            FormFactor = reader.GetStringValue("form_factor"),
            StandardKeyCount = reader.GetIntValue("standard_key_count")
        };
    }

    private static KeyboardCase MapCase(SqlDataReader reader)
    {
        return new KeyboardCase
        {
            CaseId = reader.GetStringValue("case_id"),
            BrandId = reader.GetIntValue("brand_id"),
            Material = reader.GetStringValue("material"),
            MountType = reader.GetStringValue("mount_type"),
            Color = reader.GetStringValue("color"),
            WeightG = reader.GetIntValue("weight_g"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static Pcb MapPcb(SqlDataReader reader)
    {
        return new Pcb
        {
            PcbId = reader.GetStringValue("pcb_id"),
            BrandId = reader.GetIntValue("brand_id"),
            PcbTechnology = reader.GetStringValue("pcb_technology"),
            MountType = reader.GetStringValue("mount_type"),
            Hotswap = reader.GetBoolValue("hotswap"),
            Wireless = reader.GetBoolValue("wireless"),
            Rgb = reader.GetBoolValue("rgb"),
            SwitchMount = reader.GetStringValue("switch_mount"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static Plate MapPlate(SqlDataReader reader)
    {
        return new Plate
        {
            PlateId = reader.GetStringValue("plate_id"),
            BrandId = reader.GetIntValue("brand_id"),
            Material = reader.GetStringValue("material"),
            MountType = reader.GetStringValue("mount_type"),
            FlexCut = reader.GetStringValue("flex_cut"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static KeyboardSwitch MapSwitch(SqlDataReader reader)
    {
        return new KeyboardSwitch
        {
            SwitchId = reader.GetStringValue("switch_id"),
            BrandId = reader.GetIntValue("brand_id"),
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            SwitchType = reader.GetStringValue("switch_type"),
            ActuationForceG = reader.GetIntValue("actuation_force_g"),
            MountType = reader.GetStringValue("mount_type"),
            SoundProfile = reader.GetStringValue("sound_profile"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static KeycapSet MapKeycapSet(SqlDataReader reader)
    {
        return new KeycapSet
        {
            KeycapId = reader.GetStringValue("keycap_id"),
            BrandId = reader.GetIntValue("brand_id"),
            Profile = reader.GetStringValue("profile"),
            Material = reader.GetStringValue("material"),
            ColorPrimary = reader.GetStringValue("color_primary"),
            LegendType = reader.GetStringValue("legend_type"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static Stabilizer MapStabilizer(SqlDataReader reader)
    {
        return new Stabilizer
        {
            StabilizerId = reader.GetStringValue("stab_id"),
            BrandId = reader.GetIntValue("brand_id"),
            StabilizerType = reader.GetStringValue("stab_type"),
            SizesIncluded = reader.GetStringValue("sizes_included"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static CompatibilityRule MapCompatibilityRule(SqlDataReader reader)
    {
        return new CompatibilityRule
        {
            RuleId = reader.GetIntValue("rule_id"),
            CaseId = reader.GetNullableStringValue("case_id"),
            PcbId = reader.GetNullableStringValue("pcb_id"),
            PlateId = reader.GetNullableStringValue("plate_id"),
            IsCompatible = reader.GetBoolValue("is_compatible"),
            Notes = reader.GetNullableStringValue("notes")
        };
    }

    private static AdminComponentRecord MapAdminComponent(SqlDataReader reader, AdminComponentType componentType)
    {
        return new AdminComponentRecord
        {
            ComponentType = componentType,
            ComponentId = reader.GetStringValue("component_id"),
            BrandId = reader.GetIntValue("brand_id"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available"),
            Material = reader.GetStringValue("material"),
            MountType = reader.GetStringValue("mount_type"),
            Color = reader.GetStringValue("color"),
            WeightG = reader.GetIntValue("weight_g"),
            Technology = reader.GetStringValue("technology"),
            Hotswap = reader.GetBoolValue("hotswap"),
            Wireless = reader.GetBoolValue("wireless"),
            Rgb = reader.GetBoolValue("rgb"),
            SwitchMount = reader.GetStringValue("switch_mount"),
            FlexCut = reader.GetStringValue("flex_cut"),
            SwitchType = reader.GetStringValue("switch_type"),
            ActuationForceG = reader.GetIntValue("actuation_force_g"),
            SoundProfile = reader.GetStringValue("sound_profile"),
            Profile = reader.GetStringValue("profile"),
            ColorPrimary = reader.GetStringValue("color_primary"),
            LegendType = reader.GetStringValue("legend_type"),
            StabilizerType = reader.GetStringValue("stabilizer_type"),
            SizesIncluded = reader.GetStringValue("sizes_included")
        };
    }
}
