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
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM devices WHERE device_id = @device_id)
            BEGIN
                UPDATE devices
                SET
                    seller_user_id = @seller_user_id,
                    device_name = @device_name,
                    device_type = @device_type,
                    is_active = @is_active,
                    last_seen_at = @last_seen_at
                WHERE device_id = @device_id;
            END
            ELSE
            BEGIN
                INSERT INTO devices (
                    device_id,
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
                device_id,
                seller_user_id,
                device_name,
                device_type,
                is_active,
                last_seen_at,
                created_at
            FROM devices
            WHERE device_id = @device_id;
            """;
        AddDeviceParameters(command, device);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDevice(reader);
        }

        throw new InvalidOperationException("Could not save device.");
    }

    public async Task<IReadOnlyList<Device>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        var results = new List<Device>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE seller_user_id = @seller_user_id ORDER BY created_at, device_id;";
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
        command.CommandText = $"{BaseSelectSql} WHERE device_id = @device_id;";
        command.AddParameter("@device_id", SqlDbType.VarChar, deviceId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDevice(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT
            device_id,
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
