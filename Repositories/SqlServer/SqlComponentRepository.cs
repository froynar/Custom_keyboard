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
            SELECT id AS brand_id, brand_name, country
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
            SELECT id AS brand_id, brand_name, country
            FROM brands
            WHERE id = @brand_id;
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
            IF @brand_id > 0 AND EXISTS (SELECT 1 FROM brands WHERE id = @brand_id)
            BEGIN
                UPDATE brands
                SET brand_name = @brand_name,
                    country = @country
                WHERE id = @brand_id;
            END
            ELSE
            BEGIN
                INSERT INTO brands (brand_name, country)
                VALUES (@brand_name, @country);

                SET @brand_id = CAST(SCOPE_IDENTITY() AS INT);
            END;

            SELECT id AS brand_id, brand_name, country
            FROM brands
            WHERE id = @brand_id;
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

    public async Task<bool> IsBrandInUseAsync(int brandId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM views.Catalog_Comps
                WHERE brand_id = @brand_id
            ) THEN 1 ELSE 0 END;
            """;
        command.AddParameter("@brand_id", SqlDbType.Int, brandId);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task DeleteBrandAsync(int brandId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM brands
            WHERE id = @brand_id;
            """;
        command.AddParameter("@brand_id", SqlDbType.Int, brandId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- Layouts
    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT id AS layout_id, layout_name, form_factor, key_count
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
            SELECT id AS layout_id, layout_name, form_factor, key_count
            FROM layouts
            WHERE id = @layout_id;
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
            IF EXISTS (SELECT 1 FROM layouts WHERE id = @layout_id)
            BEGIN
                UPDATE layouts
                SET layout_name = @layout_name,
                    form_factor = @form_factor,
                    key_count = @key_count
                WHERE id = @layout_id;
            END
            ELSE
            BEGIN
                INSERT INTO layouts (id, layout_name, form_factor, key_count)
                VALUES (@layout_id, @layout_name, @form_factor, @key_count);
            END;

            SELECT id AS layout_id, layout_name, form_factor, key_count
            FROM layouts
            WHERE id = @layout_id;
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
            $"{KitSelectSql} WHERE catalog.is_available = 1 ORDER BY catalog.name;",
            MapKit,
            cancellationToken: cancellationToken);
    }

    public Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{KitSelectSql} WHERE catalog.component_id = @kit_id;",
            MapKit,
            command => command.AddParameter("@kit_id", SqlDbType.VarChar, kitId, 50),
            cancellationToken);
    }

    // --------------------------------------------------------------- Switches
    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{SwitchSelectSql} WHERE catalog.is_available = 1 ORDER BY catalog.name;",
            MapSwitch,
            cancellationToken: cancellationToken);
    }

    public Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{SwitchSelectSql} WHERE catalog.component_id = @switch_id;",
            MapSwitch,
            command => command.AddParameter("@switch_id", SqlDbType.VarChar, switchId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Keycap sets
    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{KeycapSelectSql} WHERE catalog.is_available = 1 ORDER BY catalog.name;",
            MapKeycapSet,
            cancellationToken: cancellationToken);
    }

    public Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{KeycapSelectSql} WHERE catalog.component_id = @keycap_id;",
            MapKeycapSet,
            command => command.AddParameter("@keycap_id", SqlDbType.VarChar, keycapId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Stabilizers
    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{StabilizerSelectSql} WHERE catalog.is_available = 1 ORDER BY catalog.name;",
            MapStabilizer,
            cancellationToken: cancellationToken);
    }

    public Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{StabilizerSelectSql} WHERE catalog.component_id = @stab_id;",
            MapStabilizer,
            command => command.AddParameter("@stab_id", SqlDbType.VarChar, stabilizerId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------------ Accessories
    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{AccessorySelectSql} WHERE catalog.is_available = 1 ORDER BY catalog.name;",
            MapAccessory,
            cancellationToken: cancellationToken);
    }

    public Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"{AccessorySelectSql} WHERE catalog.component_id = @accessory_id;",
            MapAccessory,
            command => command.AddParameter("@accessory_id", SqlDbType.VarChar, accessoryId, 50),
            cancellationToken);
    }

    // ------------------------------------------------------- Admin catalog CRUD
    public Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(
        AdminComponentType componentType,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"""
            {AdminComponentSelectSql}
            WHERE catalog.component_type = @component_type
            ORDER BY catalog.component_id;
            """,
            reader => MapAdminComponent(reader, componentType),
            command => command.AddParameter(
                "@component_type",
                SqlDbType.VarChar,
                componentType.ToString(),
                20),
            cancellationToken: cancellationToken);
    }

    public Task<AdminComponentRecord?> GetAdminComponentByIdAsync(
        AdminComponentType componentType,
        string componentId,
        CancellationToken cancellationToken = default)
    {
        return QuerySingleAsync(
            $"""
            {AdminComponentSelectSql}
            WHERE catalog.component_type = @component_type
              AND catalog.component_id = @component_id;
            """,
            reader => MapAdminComponent(reader, componentType),
            command =>
            {
                command.AddParameter("@component_type", SqlDbType.VarChar, componentType.ToString(), 20);
                command.AddParameter("@component_id", SqlDbType.VarChar, componentId, 50);
            },
            cancellationToken);
    }

    public async Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM views.Catalog_Comps;";

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
            catalog.component_id AS kit_id,
            catalog.brand_id,
            kit.layout_id,
            catalog.name AS kit_name,
            kit.pcb_technology,
            kit.switch_mount,
            kit.required_switch_quantity,
            kit.included_parts,
            catalog.price_usd,
            catalog.is_available
        FROM views.Catalog_Comps AS catalog
        INNER JOIN keyboard_kits AS kit
            ON catalog.component_type = 'Kit'
           AND kit.id = catalog.component_id
        """;

    private const string SwitchSelectSql = """
        SELECT
            catalog.component_id AS switch_id,
            catalog.brand_id,
            catalog.name AS switch_name,
            switch_row.switch_technology,
            switch_row.mount_type,
            switch_row.switch_type,
            switch_row.actuation_force_g,
            catalog.price_usd,
            catalog.is_available
        FROM views.Catalog_Comps AS catalog
        INNER JOIN switches AS switch_row
            ON catalog.component_type = 'Switch'
           AND switch_row.id = catalog.component_id
        """;

    private const string KeycapSelectSql = """
        SELECT
            catalog.component_id AS keycap_id,
            catalog.brand_id,
            catalog.name AS keycap_name,
            keycap.supported_form_factor,
            keycap.profile,
            keycap.material,
            catalog.price_usd,
            catalog.is_available
        FROM views.Catalog_Comps AS catalog
        INNER JOIN keycap_sets AS keycap
            ON catalog.component_type = 'KeycapSet'
           AND keycap.id = catalog.component_id
        """;

    private const string StabilizerSelectSql = """
        SELECT
            catalog.component_id AS stab_id,
            catalog.brand_id,
            catalog.name AS stab_name,
            stabilizer.supported_layouts,
            catalog.price_usd,
            catalog.is_available
        FROM views.Catalog_Comps AS catalog
        INNER JOIN stabilizers AS stabilizer
            ON catalog.component_type = 'Stabilizer'
           AND stabilizer.id = catalog.component_id
        """;

    private const string AccessorySelectSql = """
        SELECT
            catalog.component_id AS accessory_id,
            accessory.accessory_type,
            catalog.name AS accessory_name,
            accessory.target_component,
            catalog.price_usd,
            catalog.is_available
        FROM views.Catalog_Comps AS catalog
        INNER JOIN accessories AS accessory
            ON catalog.component_type = 'Accessory'
           AND accessory.id = catalog.component_id
        """;

    private const string AdminComponentSelectSql = """
        SELECT
            catalog.component_id,
            catalog.name,
            catalog.brand_id,
            catalog.price_usd,
            catalog.is_available,
            COALESCE(kit.layout_id, '') AS layout_id,
            COALESCE(kit.pcb_technology, '') AS pcb_technology,
            COALESCE(kit.switch_mount, '') AS switch_mount,
            COALESCE(kit.required_switch_quantity, 0) AS required_switch_quantity,
            kit.included_parts,
            COALESCE(switch_row.switch_technology, '') AS switch_technology,
            COALESCE(switch_row.mount_type, '') AS mount_type,
            switch_row.switch_type,
            switch_row.actuation_force_g,
            COALESCE(keycap.supported_form_factor, '') AS supported_form_factor,
            keycap.profile,
            keycap.material,
            COALESCE(stabilizer.supported_layouts, '') AS supported_layouts,
            COALESCE(accessory.accessory_type, '') AS accessory_type,
            accessory.target_component
        FROM views.Catalog_Comps AS catalog
        LEFT JOIN keyboard_kits AS kit
            ON catalog.component_type = 'Kit'
           AND kit.id = catalog.component_id
        LEFT JOIN switches AS switch_row
            ON catalog.component_type = 'Switch'
           AND switch_row.id = catalog.component_id
        LEFT JOIN keycap_sets AS keycap
            ON catalog.component_type = 'KeycapSet'
           AND keycap.id = catalog.component_id
        LEFT JOIN stabilizers AS stabilizer
            ON catalog.component_type = 'Stabilizer'
           AND stabilizer.id = catalog.component_id
        LEFT JOIN accessories AS accessory
            ON catalog.component_type = 'Accessory'
           AND accessory.id = catalog.component_id
        """;

    private static (string TableName, string IdColumn) GetAdminTableInfo(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Kit => ("keyboard_kits", "id"),
            AdminComponentType.Switch => ("switches", "id"),
            AdminComponentType.KeycapSet => ("keycap_sets", "id"),
            AdminComponentType.Stabilizer => ("stabilizers", "id"),
            AdminComponentType.Accessory => ("accessories", "id"),
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static string GetAdminSaveSql(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Kit => """
                IF EXISTS (SELECT 1 FROM keyboard_kits WHERE id = @component_id)
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
                    WHERE id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO keyboard_kits (id, brand_id, layout_id, kit_name, pcb_technology, switch_mount, required_switch_quantity, included_parts, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @layout_id, @name, @pcb_technology, @switch_mount, @required_switch_quantity, @included_parts, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Switch => """
                IF EXISTS (SELECT 1 FROM switches WHERE id = @component_id)
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
                    WHERE id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO switches (id, brand_id, switch_name, switch_technology, mount_type, switch_type, actuation_force_g, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @switch_technology, @mount_type, @switch_type, @actuation_force_g, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.KeycapSet => """
                IF EXISTS (SELECT 1 FROM keycap_sets WHERE id = @component_id)
                BEGIN
                    UPDATE keycap_sets
                    SET brand_id = @brand_id,
                        keycap_name = @name,
                        supported_form_factor = @supported_form_factor,
                        profile = @profile,
                        material = @material,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO keycap_sets (id, brand_id, keycap_name, supported_form_factor, profile, material, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @supported_form_factor, @profile, @material, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Stabilizer => """
                IF EXISTS (SELECT 1 FROM stabilizers WHERE id = @component_id)
                BEGIN
                    UPDATE stabilizers
                    SET brand_id = @brand_id,
                        stab_name = @name,
                        supported_layouts = @supported_layouts,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO stabilizers (id, brand_id, stab_name, supported_layouts, price_usd, is_available)
                    VALUES (@component_id, @brand_id, @name, @supported_layouts, @price_usd, @is_available);
                END;
                """,
            AdminComponentType.Accessory => """
                IF EXISTS (SELECT 1 FROM accessories WHERE id = @component_id)
                BEGIN
                    UPDATE accessories
                    SET accessory_type = @accessory_type,
                        accessory_name = @name,
                        target_component = @target_component,
                        price_usd = @price_usd,
                        is_available = @is_available
                    WHERE id = @component_id;
                END
                ELSE
                BEGIN
                    INSERT INTO accessories (id, accessory_type, accessory_name, target_component, price_usd, is_available)
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
