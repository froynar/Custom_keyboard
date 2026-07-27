using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlDeviceTestSessionRepository : IDeviceTestSessionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlDeviceTestSessionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DeviceTestSession> SaveAsync(
        DeviceTestSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            session.SessionId = $"QC_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{suffix}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlRepositoryHelpers.IndexedDmlSetOptions + $"""
            IF EXISTS (SELECT 1 FROM device_test_sessions WHERE id = @session_id)
            BEGIN
                UPDATE device_test_sessions
                SET
                    status = @status
                WHERE id = @session_id
                  AND request_id = @request_id
                  AND device_id = @device_id
                  AND switch_technology = @switch_technology
                  AND noise_requirement = @noise_requirement
                  AND total_keys = @total_keys;

                IF @@ROWCOUNT <> 1
                    THROW 51410, 'QC session identity/configuration is immutable.', 1;
            END
            ELSE
            BEGIN
                INSERT INTO device_test_sessions (
                    id,
                    request_id,
                    device_id,
                    switch_technology,
                    noise_requirement,
                    total_keys,
                    status
                )
                SELECT
                    @session_id,
                    @request_id,
                    @device_id,
                    @switch_technology,
                    @noise_requirement,
                    @total_keys,
                    @status
                FROM build_requests AS request_row
                INNER JOIN devices AS device_row
                    ON device_row.id = @device_id
                   AND device_row.seller_user_id = request_row.seller_user_id
                WHERE request_row.id = @request_id
                  AND request_row.status = 'In_progress'
                  AND device_row.device_type = 'QC_STATION'
                  AND device_row.is_active = 1;

                IF @@ROWCOUNT <> 1
                    THROW 51411, 'QC session requires an in-progress request and its seller active QC station.', 1;
            END;

            {BaseSelectSql}
            WHERE s.id = @session_id;
            """;
        AddSessionParameters(command, session);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapSession(reader);
        }

        throw new InvalidOperationException("Could not save device test session.");
    }

    public async Task<DeviceTestSession?> GetLatestByRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {BaseSelectSql}
            WHERE s.request_id = @request_id
            ORDER BY
                CASE WHEN s.status = 'Running' THEN 0 ELSE 1 END,
                summary.last_recorded_at DESC,
                s.id DESC
            OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;
            """;
        command.AddParameter("@request_id", SqlDbType.VarChar, requestId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSession(reader) : null;
    }

    public async Task<DeviceTestSession?> GetByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE s.id = @session_id;";
        command.AddParameter("@session_id", SqlDbType.VarChar, sessionId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSession(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT
            s.id AS session_id,
            s.request_id,
            s.device_id,
            s.switch_technology,
            s.noise_requirement,
            s.total_keys,
            s.status,
            summary.tested_keys,
            summary.passed_keys,
            summary.warning_keys,
            summary.failed_keys,
            summary.average_latency_ms,
            summary.max_latency_ms,
            summary.average_noise_db,
            summary.max_noise_db
        FROM device_test_sessions AS s
        OUTER APPLY (
            SELECT
                COUNT(*) AS tested_keys,
                COALESCE(SUM(CASE WHEN r.result = 'Pass' THEN 1 ELSE 0 END), 0) AS passed_keys,
                COALESCE(SUM(CASE WHEN r.result = 'Warning' THEN 1 ELSE 0 END), 0) AS warning_keys,
                COALESCE(SUM(CASE WHEN r.result = 'Fail' THEN 1 ELSE 0 END), 0) AS failed_keys,
                CAST(AVG(CAST(r.latency AS decimal(18,4))) AS decimal(8,2)) AS average_latency_ms,
                MAX(r.latency) AS max_latency_ms,
                CAST(AVG(CAST(r.noise AS decimal(18,4))) AS decimal(8,2)) AS average_noise_db,
                MAX(r.noise) AS max_noise_db,
                MAX(r.recorded_at) AS last_recorded_at
            FROM device_key_test_results AS r
            WHERE r.session_id = s.id
        ) AS summary
        """;

    private static void AddSessionParameters(SqlCommand command, DeviceTestSession session)
    {
        command.AddParameter("@session_id", SqlDbType.VarChar, session.SessionId, 50);
        command.AddParameter("@request_id", SqlDbType.VarChar, session.RequestId, 50);
        command.AddParameter("@device_id", SqlDbType.VarChar, session.DeviceId, 50);
        command.AddParameter("@switch_technology", SqlDbType.VarChar, session.SwitchTechnology, 50);
        command.AddParameter("@noise_requirement", SqlDbType.VarChar, session.NoiseRequirement.ToString(), 20);
        command.AddParameter("@total_keys", SqlDbType.Int, session.TotalKeys);
        command.AddParameter("@status", SqlDbType.VarChar, session.Status.ToString(), 20);
    }

    private static DeviceTestSession MapSession(SqlDataReader reader)
    {
        return new DeviceTestSession
        {
            SessionId = reader.GetStringValue("session_id"),
            RequestId = reader.GetStringValue("request_id"),
            DeviceId = reader.GetStringValue("device_id"),
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            NoiseRequirement = reader.GetEnumValue<NoiseRequirement>("noise_requirement"),
            TotalKeys = reader.GetIntValue("total_keys"),
            Status = reader.GetEnumValue<TestSessionStatus>("status"),
            TestedKeys = reader.GetIntValue("tested_keys"),
            PassedKeys = reader.GetIntValue("passed_keys"),
            WarningKeys = reader.GetIntValue("warning_keys"),
            FailedKeys = reader.GetIntValue("failed_keys"),
            AverageLatencyMs = reader.GetNullableDecimalValue("average_latency_ms"),
            MaxLatencyMs = reader.GetNullableDecimalValue("max_latency_ms"),
            AverageNoiseDb = reader.GetNullableDecimalValue("average_noise_db"),
            MaxNoiseDb = reader.GetNullableDecimalValue("max_noise_db")
        };
    }
}
