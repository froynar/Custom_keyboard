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

    // ----------------------------------------------------------------- Brands
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

    public Task<Brand?> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            """
            SELECT brand_id, brand_name, country
            FROM brands
            WHERE brand_id = @brand_id;
            """,
            MapBrand,
            command => command.AddParameter("@brand_id", SqlDbType.Int, brandId),
            cancellationToken);
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

    // ---------------------------------------------------------------- Layouts
    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT layout_id, layout_name, form_factor, key_count
            FROM layouts
            ORDER BY key_count, layout_name;
            """,
            MapLayout,
            cancellationToken: cancellationToken);
    }

    public Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            """
            SELECT layout_id, layout_name, form_factor, key_count
            FROM layouts
            WHERE layout_id = @layout_id;
            """,
            MapLayout,
            command => command.AddParameter("@layout_id", SqlDbType.VarChar, layoutId, 50),
            cancellationToken);
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
                    key_count = @key_count
                WHERE layout_id = @layout_id;
            END
            ELSE
            BEGIN
                INSERT INTO layouts (layout_id, layout_name, form_factor, key_count)
                VALUES (@layout_id, @layout_name, @form_factor, @key_count);
            END;

            SELECT layout_id, layout_name, form_factor, key_count
            FROM layouts
            WHERE layout_id = @layout_id;
            """;
        command.AddParameter("@layout_id", SqlDbType.VarChar, layout.LayoutId.Trim(), 50);
        command.AddParameter("@layout_name", SqlDbType.VarChar, layout.LayoutName.Trim(), 100);
        command.AddParameter("@form_factor", SqlDbType.VarChar, layout.FormFactor.Trim(), 100);
        command.AddParameter("@key_count", SqlDbType.Int, layout.KeyCount);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapLayout(reader);
        }

        throw new InvalidOperationException("Could not save layout.");
    }

    // ------------------------------------------------------------------- Kits
    public Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{KitSelectSql} WHERE is_available = 1 ORDER BY kit_name;",
            MapKit,
            cancellationToken: cancellationToken);
    }

    public Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{KitSelectSql} WHERE kit_id = @kit_id;",
            MapKit,
            command => command.AddParameter("@kit_id", SqlDbType.VarChar, kitId, 50),
            cancellationToken);
    }

    // --------------------------------------------------------------- Switches
    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{SwitchSelectSql} WHERE is_available = 1 ORDER BY switch_name;",
            MapSwitch,
            cancellationToken: cancellationToken);
    }

    public Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{SwitchSelectSql} WHERE switch_id = @switch_id;",
            MapSwitch,
            command => command.AddParameter("@switch_id", SqlDbType.VarChar, switchId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Keycap sets
    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{KeycapSelectSql} WHERE is_available = 1 ORDER BY keycap_name;",
            MapKeycapSet,
            cancellationToken: cancellationToken);
    }

    public Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{KeycapSelectSql} WHERE keycap_id = @keycap_id;",
            MapKeycapSet,
            command => command.AddParameter("@keycap_id", SqlDbType.VarChar, keycapId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Stabilizers
    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{StabilizerSelectSql} WHERE is_available = 1 ORDER BY stab_name;",
            MapStabilizer,
            cancellationToken: cancellationToken);
    }

    public Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{StabilizerSelectSql} WHERE stab_id = @stab_id;",
            MapStabilizer,
            command => command.AddParameter("@stab_id", SqlDbType.VarChar, stabilizerId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Accessories
    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{AccessorySelectSql} WHERE is_available = 1 ORDER BY accessory_name;",
            MapAccessory,
            cancellationToken: cancellationToken);
    }

    public Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{AccessorySelectSql} WHERE accessory_id = @accessory_id;",
            MapAccessory,
            command => command.AddParameter("@accessory_id", SqlDbType.VarChar, accessoryId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------- Admin catalog CRUD
    public Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(
        AdminComponentType componentType,
        CancellationToken cancellationToken = default)
    {
        var (_, idColumn) = GetAdminTableInfo(componentType);
        return QueryAsync(
            $"{GetAdminSelectSql(componentType)} ORDER BY {idColumn};",
            reader => MapAdminComponent(reader, componentType),
            cancellationToken: cancellationToken);
    }

    public Task<AdminComponentRecord?> GetAdminComponentByIdAsync(
        AdminComponentType componentType,
        string componentId,
        CancellationToken cancellationToken = default)
    {
        var (tableName, idColumn) = GetAdminTableInfo(componentType);
        return QuerySingleAsync(
            $"{GetAdminSelectSql(componentType)} WHERE {tableName}.{idColumn} = @component_id;",
            reader => MapAdminComponent(reader, componentType),
            command => command.AddParameter("@component_id", SqlDbType.VarChar, componentId, 50),
            cancellationToken);
    }

    public async Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM keyboard_kits)
              + (SELECT COUNT(*) FROM switches)
              + (SELECT COUNT(*) FROM keycap_sets)
              + (SELECT COUNT(*) FROM stabilizers)
              + (SELECT COUNT(*) FROM accessories);
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<AdminComponentRecord> SaveAdminComponentAsync(
        AdminComponentRecord component,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = GetAdminSaveSql(component.ComponentType);
        AddAdminComponentParameters(command, component);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var saved = await GetAdminComponentByIdAsync(component.ComponentType, component.ComponentId.Trim(), cancellationToken);
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

    // ------------------------------------------------------------- Query helpers
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

    private async Task<T?> QuerySingleAsync<T>(
        string commandText,
        Func<SqlDataReader, T> map,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
        where T : class
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? map(reader) : null;
    }

    // ------------------------------------------------------------- Catalog SQL
    private const string KitSelectSql = """
        SELECT
            kit_id, brand_id, layout_id, kit_name, pcb_technology, switch_mount,
            required_switch_quantity, included_parts, price_usd, is_available
        FROM keyboard_kits
        """;

    private const string SwitchSelectSql = """
        SELECT
            switch_id, brand_id, switch_name, switch_technology, mount_type,
            switch_type, actuation_force_g, price_usd, is_available
        FROM switches
        """;

    private const string KeycapSelectSql = """
        SELECT
            keycap_id, brand_id, keycap_name, supported_form_factor,
            profile, material, price_usd, is_available
        FROM keycap_sets
        """;

    private const string StabilizerSelectSql = """
        SELECT
            stab_id, brand_id, stab_name, supported_layouts, price_usd, is_available
        FROM stabilizers
        """;

    private const string AccessorySelectSql = """
        SELECT
            accessory_id, accessory_type, accessory_name, target_component, price_usd, is_available
        FROM accessories
        """;

    private static (string TableName, string IdColumn) GetAdminTableInfo(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Kit => ("keyboard_kits", "kit_id"),
            AdminComponentType.Switch => ("switches", "switch_id"),
            AdminComponentType.KeycapSet => ("keycap_sets", "keycap_id"),
            AdminComponentType.Stabilizer => ("stabilizers", "stab_id"),
            AdminComponentType.Accessory => ("accessories", "accessory_id"),
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    // Projects each catalog type onto the AdminComponentRecord superset columns.
    private static string GetAdminSelectSql(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Kit => """
                SELECT
                    keyboard_kits.kit_id AS component_id,
                    keyboard_kits.kit_name AS name,
                    keyboard_kits.brand_id,
                    keyboard_kits.price_usd,
                    keyboard_kits.is_available,
                    keyboard_kits.layout_id,
                    keyboard_kits.pcb_technology,
                    keyboard_kits.switch_mount,
                    keyboard_kits.required_switch_quantity,
                    keyboard_kits.included_parts,
                    CAST('' AS varchar(50)) AS switch_technology,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS switch_type,
                    CAST(NULL AS int) AS actuation_force_g,
                    CAST('' AS varchar(255)) AS supported_form_factor,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS material,
                    CAST('' AS varchar(255)) AS supported_layouts,
                    CAST('' AS varchar(100)) AS accessory_type,
                    CAST('' AS varchar(100)) AS target_component
                FROM keyboard_kits
                """,
            AdminComponentType.Switch => """
                SELECT
                    switches.switch_id AS component_id,
                    switches.switch_name AS name,
                    switches.brand_id,
                    switches.price_usd,
                    switches.is_available,
                    CAST('' AS varchar(50)) AS layout_id,
                    CAST('' AS varchar(50)) AS pcb_technology,
                    CAST('' AS varchar(100)) AS switch_mount,
                    0 AS required_switch_quantity,
                    CAST('' AS varchar(500)) AS included_parts,
                    switches.switch_technology,
                    switches.mount_type,
                    switches.switch_type,
                    switches.actuation_force_g,
                    CAST('' AS varchar(255)) AS supported_form_factor,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS material,
                    CAST('' AS varchar(255)) AS supported_layouts,
                    CAST('' AS varchar(100)) AS accessory_type,
                    CAST('' AS varchar(100)) AS target_component
                FROM switches
                """,
            AdminComponentType.KeycapSet => """
                SELECT
                    keycap_sets.keycap_id AS component_id,
                    keycap_sets.keycap_name AS name,
                    keycap_sets.brand_id,
                    keycap_sets.price_usd,
                    keycap_sets.is_available,
                    CAST('' AS varchar(50)) AS layout_id,
                    CAST('' AS varchar(50)) AS pcb_technology,
                    CAST('' AS varchar(100)) AS switch_mount,
                    0 AS required_switch_quantity,
                    CAST('' AS varchar(500)) AS included_parts,
                    CAST('' AS varchar(50)) AS switch_technology,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS switch_type,
                    CAST(NULL AS int) AS actuation_force_g,
                    keycap_sets.supported_form_factor,
                    keycap_sets.profile,
                    keycap_sets.material,
                    CAST('' AS varchar(255)) AS supported_layouts,
                    CAST('' AS varchar(100)) AS accessory_type,
                    CAST('' AS varchar(100)) AS target_component
                FROM keycap_sets
                """,
            AdminComponentType.Stabilizer => """
                SELECT
                    stabilizers.stab_id AS component_id,
                    stabilizers.stab_name AS name,
                    stabilizers.brand_id,
                    stabilizers.price_usd,
                    stabilizers.is_available,
                    CAST('' AS varchar(50)) AS layout_id,
                    CAST('' AS varchar(50)) AS pcb_technology,
                    CAST('' AS varchar(100)) AS switch_mount,
                    0 AS required_switch_quantity,
                    CAST('' AS varchar(500)) AS included_parts,
                    CAST('' AS varchar(50)) AS switch_technology,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS switch_type,
                    CAST(NULL AS int) AS actuation_force_g,
                    CAST('' AS varchar(255)) AS supported_form_factor,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS material,
                    stabilizers.supported_layouts,
                    CAST('' AS varchar(100)) AS accessory_type,
                    CAST('' AS varchar(100)) AS target_component
                FROM stabilizers
                """,
            AdminComponentType.Accessory => """
                SELECT
                    accessories.accessory_id AS component_id,
                    accessories.accessory_name AS name,
                    CAST(0 AS int) AS brand_id,
                    accessories.price_usd,
                    accessories.is_available,
                    CAST('' AS varchar(50)) AS layout_id,
                    CAST('' AS varchar(50)) AS pcb_technology,
                    CAST('' AS varchar(100)) AS switch_mount,
                    0 AS required_switch_quantity,
                    CAST('' AS varchar(500)) AS included_parts,
                    CAST('' AS varchar(50)) AS switch_technology,
                    CAST('' AS varchar(100)) AS mount_type,
                    CAST('' AS varchar(100)) AS switch_type,
                    CAST(NULL AS int) AS actuation_force_g,
                    CAST('' AS varchar(255)) AS supported_form_factor,
                    CAST('' AS varchar(100)) AS profile,
                    CAST('' AS varchar(100)) AS material,
                    CAST('' AS varchar(255)) AS supported_layouts,
                    accessories.accessory_type,
                    accessories.target_component
                FROM accessories
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static string GetAdminSaveSql(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Kit => """
                IF EXISTS (SELECT 1 FROM keyboard_kits WHERE kit_id = @component_id)
                BEGIN
                    UPDATE keyboard_kits
                    SET brand_id = @brand_id,
                        layout_id = @layout_id,
                        kit_name = @name,
                        pcb_technology = @pcb_technology,
                        switch_mount = @switch_mount,
                        required_switch_quantity = @required_switch_quantity,
                        included_parts = @included_parts,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE kit_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO keyboard_kits (kit_id, brand_id, layout_id, kit_name, pcb_technology, switch_mount, required_switch_quantity, included_parts, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @layout_id, @name, @pcb_technology, @switch_mount, @required_switch_quantity, @included_parts, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Switch => """
                IF EXISTS (SELECT 1 FROM switches WHERE switch_id = @component_id)
                BEGIN
                    UPDATE switches
                    SET brand_id = @brand_id,
                        switch_name = @name,
                        switch_technology = @switch_technology,
                        mount_type = @mount_type,
                        switch_type = @switch_type,
                        actuation_force_g = @actuation_force_g,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE switch_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO switches (switch_id, brand_id, switch_name, switch_technology, mount_type, switch_type, actuation_force_g, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @switch_technology, @mount_type, @switch_type, @actuation_force_g, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.KeycapSet => """
                IF EXISTS (SELECT 1 FROM keycap_sets WHERE keycap_id = @component_id)
                BEGIN
                    UPDATE keycap_sets
                    SET brand_id = @brand_id,
                        keycap_name = @name,
                        supported_form_factor = @supported_form_factor,
                        profile = @profile,
                        material = @material,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE keycap_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO keycap_sets (keycap_id, brand_id, keycap_name, supported_form_factor, profile, material, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @supported_form_factor, @profile, @material, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Stabilizer => """
                IF EXISTS (SELECT 1 FROM stabilizers WHERE stab_id = @component_id)
                BEGIN
                    UPDATE stabilizers
                    SET brand_id = @brand_id,
                        stab_name = @name,
                        supported_layouts = @supported_layouts,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE stab_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO stabilizers (stab_id, brand_id, stab_name, supported_layouts, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @supported_layouts, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Accessory => """
                IF EXISTS (SELECT 1 FROM accessories WHERE accessory_id = @component_id)
                BEGIN
                    UPDATE accessories
                    SET accessory_type = @accessory_type,
                        accessory_name = @name,
                        target_component = @target_component,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE accessory_id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO accessories (accessory_id, accessory_type, accessory_name, target_component, price_usd, is_available)
                    VALUES (@component_id, @accessory_type, @name, @target_component, @price_usd, @is_available);
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    // Adds every parameter; each save batch references only the subset it needs.
    private static void AddAdminComponentParameters(SqlCommand command, AdminComponentRecord component)
    {
        command.AddParameter("@component_id", SqlDbType.VarChar, component.ComponentId.Trim(), 50);
        command.AddParameter("@name", SqlDbType.VarChar, component.Name.Trim(), 255);
        command.AddParameter("@brand_id", SqlDbType.Int, component.BrandId);
        command.AddDecimalParameter("@price_usd", component.PriceUsd);
        command.AddParameter("@is_available", SqlDbType.Bit, component.IsAvailable);
        command.AddParameter("@layout_id", SqlDbType.VarChar, component.LayoutId.Trim(), 50);
        command.AddParameter("@pcb_technology", SqlDbType.VarChar, component.PcbTechnology.Trim(), 50);
        command.AddParameter("@switch_mount", SqlDbType.VarChar, component.SwitchMount.Trim(), 100);
        command.AddParameter("@required_switch_quantity", SqlDbType.Int, component.RequiredSwitchQuantity);
        command.AddParameter("@included_parts", SqlDbType.VarChar, NullIfEmpty(component.IncludedParts), 500);
        command.AddParameter("@switch_technology", SqlDbType.VarChar, component.SwitchTechnology.Trim(), 50);
        command.AddParameter("@mount_type", SqlDbType.VarChar, component.MountType.Trim(), 100);
        command.AddParameter("@switch_type", SqlDbType.VarChar, NullIfEmpty(component.SwitchType), 100);
        command.AddParameter("@actuation_force_g", SqlDbType.Int, component.ActuationForceG);
        command.AddParameter("@supported_form_factor", SqlDbType.VarChar, component.SupportedFormFactor.Trim(), 255);
        command.AddParameter("@profile", SqlDbType.VarChar, NullIfEmpty(component.Profile), 100);
        command.AddParameter("@material", SqlDbType.VarChar, NullIfEmpty(component.Material), 100);
        command.AddParameter("@supported_layouts", SqlDbType.VarChar, component.SupportedLayouts.Trim(), 255);
        command.AddParameter("@accessory_type", SqlDbType.VarChar, component.AccessoryType.Trim(), 100);
        command.AddParameter("@target_component", SqlDbType.VarChar, NullIfEmpty(component.TargetComponent), 100);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ------------------------------------------------------------------ Mappers
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
            KeyCount = reader.GetIntValue("key_count")
        };
    }

    private static KeyboardKit MapKit(SqlDataReader reader)
    {
        return new KeyboardKit
        {
            KitId = reader.GetStringValue("kit_id"),
            BrandId = reader.GetIntValue("brand_id"),
            LayoutId = reader.GetStringValue("layout_id"),
            KitName = reader.GetStringValue("kit_name"),
            PcbTechnology = reader.GetStringValue("pcb_technology"),
            SwitchMount = reader.GetStringValue("switch_mount"),
            RequiredSwitchQuantity = reader.GetIntValue("required_switch_quantity"),
            IncludedParts = reader.GetNullableStringValue("included_parts"),
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
            SwitchName = reader.GetStringValue("switch_name"),
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            MountType = reader.GetStringValue("mount_type"),
            SwitchType = reader.GetNullableStringValue("switch_type"),
            ActuationForceG = reader.GetNullableIntValue("actuation_force_g"),
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
            KeycapName = reader.GetStringValue("keycap_name"),
            SupportedFormFactor = reader.GetStringValue("supported_form_factor"),
            Profile = reader.GetNullableStringValue("profile"),
            Material = reader.GetNullableStringValue("material"),
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
            StabilizerName = reader.GetStringValue("stab_name"),
            SupportedLayouts = reader.GetStringValue("supported_layouts"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static Accessory MapAccessory(SqlDataReader reader)
    {
        return new Accessory
        {
            AccessoryId = reader.GetStringValue("accessory_id"),
            AccessoryType = reader.GetStringValue("accessory_type"),
            AccessoryName = reader.GetStringValue("accessory_name"),
            TargetComponent = reader.GetNullableStringValue("target_component"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available")
        };
    }

    private static AdminComponentRecord MapAdminComponent(SqlDataReader reader, AdminComponentType componentType)
    {
        return new AdminComponentRecord
        {
            ComponentType = componentType,
            ComponentId = reader.GetStringValue("component_id"),
            Name = reader.GetStringValue("name"),
            BrandId = reader.GetIntValue("brand_id"),
            PriceUsd = reader.GetDecimalValue("price_usd"),
            IsAvailable = reader.GetBoolValue("is_available"),
            LayoutId = reader.GetStringValue("layout_id"),
            PcbTechnology = reader.GetStringValue("pcb_technology"),
            SwitchMount = reader.GetStringValue("switch_mount"),
            RequiredSwitchQuantity = reader.GetIntValue("required_switch_quantity"),
            IncludedParts = reader.GetNullableStringValue("included_parts") ?? string.Empty,
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            MountType = reader.GetStringValue("mount_type"),
            SwitchType = reader.GetNullableStringValue("switch_type") ?? string.Empty,
            ActuationForceG = reader.GetNullableIntValue("actuation_force_g"),
            SupportedFormFactor = reader.GetStringValue("supported_form_factor"),
            Profile = reader.GetNullableStringValue("profile") ?? string.Empty,
            Material = reader.GetNullableStringValue("material") ?? string.Empty,
            SupportedLayouts = reader.GetStringValue("supported_layouts"),
            AccessoryType = reader.GetStringValue("accessory_type"),
            TargetComponent = reader.GetNullableStringValue("target_component") ?? string.Empty
        };
    }
}
