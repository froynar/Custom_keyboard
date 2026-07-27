using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlDeviceKeyTestResultRepository : IDeviceKeyTestResultRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlDeviceKeyTestResultRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DeviceKeyTestResult> InsertAsync(
        DeviceKeyTestResult result,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

            DECLARE @key_test_id BIGINT;

            BEGIN TRANSACTION;

            IF NOT EXISTS (
                SELECT 1
                FROM device_test_sessions AS session_row WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN build_requests AS request_row
                    ON request_row.id = session_row.request_id
                INNER JOIN devices AS device_row
                    ON device_row.id = session_row.device_id
                   AND device_row.seller_user_id = request_row.seller_user_id
                WHERE session_row.id = @session_id
                  AND session_row.status = 'Running'
                  AND request_row.status = 'In_progress'
                  AND device_row.device_type = 'QC_STATION'
                  AND device_row.is_active = 1
            )
                THROW 51420, 'Key telemetry requires an active QC session, request, and seller device.', 1;

            SELECT TOP (1) @key_test_id = id
            FROM device_key_test_results WITH (UPDLOCK, HOLDLOCK)
            WHERE session_id = @session_id
              AND key_code = @key_code
            ORDER BY id;

            IF @key_test_id IS NULL
            BEGIN
                INSERT INTO device_key_test_results (
                    session_id,
                    key_code,
                    received_key,
                    press_signal_detected,
                    latency,
                    press_count,
                    release_signal,
                    hold_duration,
                    noise,
                    result,
                    recorded_at
                )
                VALUES (
                    @session_id,
                    @key_code,
                    @received_key,
                    @press_signal_detected,
                    @latency,
                    @press_count,
                    @release_signal,
                    @hold_duration,
                    @noise,
                    @result,
                    COALESCE(@recorded_at, SYSUTCDATETIME())
                );

                SET @key_test_id = CAST(SCOPE_IDENTITY() AS BIGINT);
            END;

            COMMIT TRANSACTION;

            SELECT
                id AS key_test_id,
                session_id,
                key_code,
                received_key,
                press_signal_detected,
                latency,
                press_count,
                release_signal,
                hold_duration,
                noise,
                result,
                recorded_at
            FROM device_key_test_results
            WHERE id = @key_test_id;
            """;
        AddKeyResultParameters(command, result);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapKeyResult(reader);
        }

        throw new InvalidOperationException("Could not insert device key test result.");
    }

    public async Task<IReadOnlyList<DeviceKeyTestResult>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DeviceKeyTestResult>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE session_id = @session_id ORDER BY id;";
        command.AddParameter("@session_id", SqlDbType.VarChar, sessionId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapKeyResult(reader));
        }

        return results;
    }

    private const string BaseSelectSql = """
        SELECT
            id AS key_test_id,
            session_id,
            key_code,
            received_key,
            press_signal_detected,
            latency,
            press_count,
            release_signal,
            hold_duration,
            noise,
            result,
            recorded_at
        FROM device_key_test_results
        """;

    private static void AddKeyResultParameters(SqlCommand command, DeviceKeyTestResult result)
    {
        command.AddParameter("@session_id", SqlDbType.VarChar, result.SessionId, 50);
        command.AddParameter("@key_code", SqlDbType.VarChar, result.KeyCode, 30);
        command.AddParameter("@received_key", SqlDbType.VarChar, result.ReceivedKey, 30);
        command.AddParameter("@press_signal_detected", SqlDbType.Bit, result.PressSignalDetected);
        command.AddNullableDecimalParameter("@latency", result.Latency);
        command.AddParameter("@press_count", SqlDbType.Int, result.PressCount);
        command.AddParameter("@release_signal", SqlDbType.Bit, result.ReleaseSignal);
        command.AddParameter("@hold_duration", SqlDbType.Int, result.HoldDuration);
        command.AddNullableDecimalParameter("@noise", result.Noise);
        command.AddParameter("@result", SqlDbType.VarChar, result.Result.ToString(), 20);
        command.AddParameter("@recorded_at", SqlDbType.DateTime2, result.RecordedAt == default ? null : result.RecordedAt);
    }

    private static DeviceKeyTestResult MapKeyResult(SqlDataReader reader)
    {
        return new DeviceKeyTestResult
        {
            KeyTestId = reader.GetLongValue("key_test_id"),
            SessionId = reader.GetStringValue("session_id"),
            KeyCode = reader.GetStringValue("key_code"),
            ReceivedKey = reader.GetNullableStringValue("received_key"),
            PressSignalDetected = reader.GetBoolValue("press_signal_detected"),
            Latency = reader.GetNullableDecimalValue("latency"),
            PressCount = reader.GetIntValue("press_count"),
            ReleaseSignal = reader.GetBoolValue("release_signal"),
            HoldDuration = reader.GetNullableIntValue("hold_duration"),
            Noise = reader.GetNullableDecimalValue("noise"),
            Result = reader.GetEnumValue<KeyTestResult>("result"),
            RecordedAt = reader.GetDateTimeValue("recorded_at")
        };
    }
}
