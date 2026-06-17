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

    public async Task<DeviceKeyTestResult> InsertAsync(DeviceKeyTestResult result, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO device_key_test_results (
                session_id,
                request_id,
                device_id,
                key_code,
                expected_key,
                received_key,
                press_signal_detected,
                latency_ms,
                press_event_count,
                bounce_count,
                release_signal_detected,
                hold_duration_ms,
                is_stuck,
                noise_db,
                switch_technology,
                result,
                failure_type,
                failure_reason,
                recorded_at
            )
            VALUES (
                @session_id,
                @request_id,
                @device_id,
                @key_code,
                @expected_key,
                @received_key,
                @press_signal_detected,
                @latency_ms,
                @press_event_count,
                @bounce_count,
                @release_signal_detected,
                @hold_duration_ms,
                @is_stuck,
                @noise_db,
                @switch_technology,
                @result,
                @failure_type,
                @failure_reason,
                COALESCE(@recorded_at, SYSUTCDATETIME())
            );

            DECLARE @new_key_test_id BIGINT = CAST(SCOPE_IDENTITY() AS BIGINT);

            SELECT
                key_test_id,
                session_id,
                request_id,
                device_id,
                key_code,
                expected_key,
                received_key,
                press_signal_detected,
                latency_ms,
                press_event_count,
                bounce_count,
                release_signal_detected,
                hold_duration_ms,
                is_stuck,
                noise_db,
                switch_technology,
                result,
                failure_type,
                failure_reason,
                recorded_at
            FROM device_key_test_results
            WHERE key_test_id = @new_key_test_id;
            """;
        AddKeyResultParameters(command, result);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapKeyResult(reader);
        }

        throw new InvalidOperationException("Could not insert device key test result.");
    }

    public async Task<IReadOnlyList<DeviceKeyTestResult>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var results = new List<DeviceKeyTestResult>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE session_id = @session_id ORDER BY key_test_id;";
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
            key_test_id,
            session_id,
            request_id,
            device_id,
            key_code,
            expected_key,
            received_key,
            press_signal_detected,
            latency_ms,
            press_event_count,
            bounce_count,
            release_signal_detected,
            hold_duration_ms,
            is_stuck,
            noise_db,
            switch_technology,
            result,
            failure_type,
            failure_reason,
            recorded_at
        FROM device_key_test_results
        """;

    private static void AddKeyResultParameters(SqlCommand command, DeviceKeyTestResult result)
    {
        command.AddParameter("@session_id", SqlDbType.VarChar, result.SessionId, 50);
        command.AddParameter("@request_id", SqlDbType.VarChar, result.RequestId, 50);
        command.AddParameter("@device_id", SqlDbType.VarChar, result.DeviceId, 50);
        command.AddParameter("@key_code", SqlDbType.VarChar, result.KeyCode, 30);
        command.AddParameter("@expected_key", SqlDbType.VarChar, result.ExpectedKey, 30);
        command.AddParameter("@received_key", SqlDbType.VarChar, result.ReceivedKey, 30);
        command.AddParameter("@press_signal_detected", SqlDbType.Bit, result.PressSignalDetected);
        command.AddNullableDecimalParameter("@latency_ms", result.LatencyMs);
        command.AddParameter("@press_event_count", SqlDbType.Int, result.PressEventCount);
        command.AddParameter("@bounce_count", SqlDbType.Int, result.BounceCount);
        command.AddParameter("@release_signal_detected", SqlDbType.Bit, result.ReleaseSignalDetected);
        command.AddParameter("@hold_duration_ms", SqlDbType.Int, result.HoldDurationMs);
        command.AddParameter("@is_stuck", SqlDbType.Bit, result.IsStuck);
        command.AddNullableDecimalParameter("@noise_db", result.NoiseDb);
        command.AddParameter("@switch_technology", SqlDbType.VarChar, result.SwitchTechnology, 50);
        command.AddParameter("@result", SqlDbType.VarChar, result.Result.ToString(), 20);
        command.AddParameter("@failure_type", SqlDbType.VarChar, result.FailureType?.ToString(), 30);
        command.AddParameter("@failure_reason", SqlDbType.NVarChar, result.FailureReason, 255);
        command.AddParameter("@recorded_at", SqlDbType.DateTime2, result.RecordedAt == default ? null : result.RecordedAt);
    }

    private static DeviceKeyTestResult MapKeyResult(SqlDataReader reader)
    {
        return new DeviceKeyTestResult
        {
            KeyTestId = reader.GetLongValue("key_test_id"),
            SessionId = reader.GetStringValue("session_id"),
            RequestId = reader.GetStringValue("request_id"),
            DeviceId = reader.GetStringValue("device_id"),
            KeyCode = reader.GetStringValue("key_code"),
            ExpectedKey = reader.GetStringValue("expected_key"),
            ReceivedKey = reader.GetNullableStringValue("received_key"),
            PressSignalDetected = reader.GetBoolValue("press_signal_detected"),
            LatencyMs = reader.GetNullableDecimalValue("latency_ms"),
            PressEventCount = reader.GetIntValue("press_event_count"),
            BounceCount = reader.GetNullableIntValue("bounce_count"),
            ReleaseSignalDetected = reader.GetBoolValue("release_signal_detected"),
            HoldDurationMs = reader.GetNullableIntValue("hold_duration_ms"),
            IsStuck = reader.GetBoolValue("is_stuck"),
            NoiseDb = reader.GetNullableDecimalValue("noise_db"),
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            Result = reader.GetEnumValue<KeyTestResult>("result"),
            FailureType = reader.GetNullableEnumValue<KeyFailureType>("failure_type"),
            FailureReason = reader.GetNullableStringValue("failure_reason"),
            RecordedAt = reader.GetDateTimeValue("recorded_at")
        };
    }
}
