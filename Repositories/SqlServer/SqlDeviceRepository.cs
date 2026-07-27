using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlDeviceRepository : IDeviceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlDeviceRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Device> SaveAsync(Device device, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(device.DeviceId))
        {
            device.DeviceId = $"DEV_{Guid.NewGuid():N}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlRepositoryHelpers.IndexedDmlSetOptions + """
            IF EXISTS (SELECT 1 FROM devices WHERE id = @device_id)
            BEGIN
                UPDATE devices
                SET
                    seller_user_id = @seller_user_id,
                    device_name = @device_name,
                    device_type = @device_type,
                    is_active = @is_active,
                    last_seen_at = @last_seen_at
                WHERE id = @device_id;
            END
            ELSE
            BEGIN
                INSERT INTO devices (
                    id,
                    seller_user_id,
                    device_name,
                    device_type,
                    is_active,
                    last_seen_at,
                    created_at
                )
                VALUES (
                    @device_id,
                    @seller_user_id,
                    @device_name,
                    @device_type,
                    @is_active,
                    @last_seen_at,
                    COALESCE(@created_at, SYSUTCDATETIME())
                );
            END;

            SELECT
                id AS device_id,
                seller_user_id,
                device_name,
                device_type,
                is_active,
                last_seen_at,
                created_at
            FROM devices
            WHERE id = @device_id;
            """;
        AddDeviceParameters(command, device);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDevice(reader);
        }

        throw new InvalidOperationException("Could not save device.");
    }

    public async Task<Device> GetOrCreateActiveQcStationAsync(
        int sellerUserId,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        var newDeviceId = $"DEV_{Guid.NewGuid():N}";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlRepositoryHelpers.IndexedDmlSetOptions + """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

            DECLARE @device_id VARCHAR(50);
            BEGIN TRANSACTION;

            SELECT TOP (1) @device_id = id
            FROM devices WITH (UPDLOCK, HOLDLOCK)
            WHERE seller_user_id = @seller_user_id
              AND device_type = 'QC_STATION'
              AND is_active = 1
            ORDER BY created_at, id;

            IF @device_id IS NULL
            BEGIN
                SET @device_id = @new_device_id;
                INSERT INTO devices (
                    id,
                    seller_user_id,
                    device_name,
                    device_type,
                    is_active,
                    last_seen_at,
                    created_at
                )
                VALUES (
                    @device_id,
                    @seller_user_id,
                    @device_name,
                    'QC_STATION',
                    1,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            END
            ELSE
            BEGIN
                UPDATE devices
                SET last_seen_at = SYSUTCDATETIME()
                WHERE id = @device_id;
            END;

            COMMIT TRANSACTION;

            SELECT
                id AS device_id,
                seller_user_id,
                device_name,
                device_type,
                is_active,
                last_seen_at,
                created_at
            FROM devices
            WHERE id = @device_id;
            """;
        command.AddParameter("@seller_user_id", SqlDbType.Int, sellerUserId);
        command.AddParameter("@device_name", SqlDbType.NVarChar, deviceName, 100);
        command.AddParameter("@new_device_id", SqlDbType.VarChar, newDeviceId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDevice(reader);
        }

        throw new InvalidOperationException("Could not get or create the seller QC station.");
    }

    public async Task<IReadOnlyList<Device>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        var results = new List<Device>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE seller_user_id = @seller_user_id ORDER BY created_at, id;";
        command.AddParameter("@seller_user_id", SqlDbType.Int, sellerUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapDevice(reader));
        }

        return results;
    }

    public async Task<Device?> GetByIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE id = @device_id;";
        command.AddParameter("@device_id", SqlDbType.VarChar, deviceId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDevice(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT
            id AS device_id,
            seller_user_id,
            device_name,
            device_type,
            is_active,
            last_seen_at,
            created_at
        FROM devices
        """;

    private static void AddDeviceParameters(SqlCommand command, Device device)
    {
        command.AddParameter("@device_id", SqlDbType.VarChar, device.DeviceId, 50);
        command.AddParameter("@seller_user_id", SqlDbType.Int, device.SellerUserId);
        command.AddParameter("@device_name", SqlDbType.NVarChar, device.DeviceName, 100);
        command.AddParameter("@device_type", SqlDbType.VarChar, device.DeviceType.ToString(), 50);
        command.AddParameter("@is_active", SqlDbType.Bit, device.IsActive);
        command.AddParameter("@last_seen_at", SqlDbType.DateTime2, device.LastSeenAt);
        command.AddParameter("@created_at", SqlDbType.DateTime2, device.CreatedAt == default ? null : device.CreatedAt);
    }

    private static Device MapDevice(SqlDataReader reader)
    {
        return new Device
        {
            DeviceId = reader.GetStringValue("device_id"),
            SellerUserId = reader.GetIntValue("seller_user_id"),
            DeviceName = reader.GetStringValue("device_name"),
            DeviceType = reader.GetEnumValue<DeviceType>("device_type"),
            IsActive = reader.GetBoolValue("is_active"),
            LastSeenAt = reader.GetNullableDateTimeValue("last_seen_at"),
            CreatedAt = reader.GetDateTimeValue("created_at")
        };
    }
}
