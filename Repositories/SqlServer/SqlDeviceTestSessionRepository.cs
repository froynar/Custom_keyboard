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

    public async Task<DeviceTestSession> SaveAsync(DeviceTestSession session, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            session.SessionId = $"QCSESS_{Guid.NewGuid():N}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM device_test_sessions WHERE id = @session_id)
            BEGIN
                UPDATE device_test_sessions
                SET
                    request_id = @request_id,
                    device_id = @device_id,
                    seller_user_id = @seller_user_id,
                    switch_technology = @switch_technology,
                    noise_requirement = @noise_requirement,
                    total_keys = @total_keys,
                    tested_keys = @tested_keys,
                    passed_keys = @passed_keys,
                    warning_keys = @warning_keys,
                    failed_keys = @failed_keys,
                    average_latency_ms = @average_latency_ms,
                    max_latency_ms = @max_latency_ms,
                    average_noise_db = @average_noise_db,
                    max_noise_db = @max_noise_db,
                    status = @status,
                    completed_at = @completed_at
                WHERE id = @session_id;
            END
            ELSE
            BEGIN
                INSERT INTO device_test_sessions (
                    id,
                    request_id,
                    device_id,
                    seller_user_id,
                    switch_technology,
                    noise_requirement,
                    total_keys,
                    tested_keys,
                    passed_keys,
                    warning_keys,
                    failed_keys,
                    average_latency_ms,
                    max_latency_ms,
                    average_noise_db,
                    max_noise_db,
                    status,
                    started_at,
                    completed_at
                )
                VALUES (
                    @session_id,
                    @request_id,
                    @device_id,
                    @seller_user_id,
                    @switch_technology,
                    @noise_requirement,
                    @total_keys,
                    @tested_keys,
                    @passed_keys,
                    @warning_keys,
                    @failed_keys,
                    @average_latency_ms,
                    @max_latency_ms,
                    @average_noise_db,
                    @max_noise_db,
                    @status,
                    COALESCE(@started_at, SYSUTCDATETIME()),
                    @completed_at
                );
            END;

            SELECT
                id AS session_id,
                request_id,
                device_id,
                seller_user_id,
                switch_technology,
                noise_requirement,
                total_keys,
                tested_keys,
                passed_keys,
                warning_keys,
                failed_keys,
                average_latency_ms,
                max_latency_ms,
                average_noise_db,
                max_noise_db,
                status,
                started_at,
                completed_at
            FROM device_test_sessions
            WHERE id = @session_id;
            """;
        AddSessionParameters(command, session);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapSession(reader);
        }

        throw new InvalidOperationException("Could not save device test session.");
    }

    public async Task<DeviceTestSession?> GetLatestByRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {BaseSelectSql}
            WHERE request_id = @request_id
            ORDER BY started_at DESC, id DESC
            OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;
            """;
        command.AddParameter("@request_id", SqlDbType.VarChar, requestId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSession(reader) : null;
    }

    public async Task<DeviceTestSession?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE id = @session_id;";
        command.AddParameter("@session_id", SqlDbType.VarChar, sessionId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSession(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT
            id AS session_id,
            request_id,
            device_id,
            seller_user_id,
            switch_technology,
            noise_requirement,
            total_keys,
            tested_keys,
            passed_keys,
            warning_keys,
            failed_keys,
            average_latency_ms,
            max_latency_ms,
            average_noise_db,
            max_noise_db,
            status,
            started_at,
            completed_at
        FROM device_test_sessions
        """;

    private static void AddSessionParameters(SqlCommand command, DeviceTestSession session)
    {
        command.AddParameter("@session_id", SqlDbType.VarChar, session.SessionId, 50);
        command.AddParameter("@request_id", SqlDbType.VarChar, session.RequestId, 50);
        command.AddParameter("@device_id", SqlDbType.VarChar, session.DeviceId, 50);
        command.AddParameter("@seller_user_id", SqlDbType.Int, session.SellerUserId);
        command.AddParameter("@switch_technology", SqlDbType.VarChar, session.SwitchTechnology, 50);
        command.AddParameter("@noise_requirement", SqlDbType.VarChar, session.NoiseRequirement.ToString(), 20);
        command.AddParameter("@total_keys", SqlDbType.Int, session.TotalKeys);
        command.AddParameter("@tested_keys", SqlDbType.Int, session.TestedKeys);
        command.AddParameter("@passed_keys", SqlDbType.Int, session.PassedKeys);
        command.AddParameter("@warning_keys", SqlDbType.Int, session.WarningKeys);
        command.AddParameter("@failed_keys", SqlDbType.Int, session.FailedKeys);
        command.AddNullableDecimalParameter("@average_latency_ms", session.AverageLatencyMs);
        command.AddNullableDecimalParameter("@max_latency_ms", session.MaxLatencyMs);
        command.AddNullableDecimalParameter("@average_noise_db", session.AverageNoiseDb);
        command.AddNullableDecimalParameter("@max_noise_db", session.MaxNoiseDb);
        command.AddParameter("@status", SqlDbType.VarChar, session.Status.ToString(), 20);
        command.AddParameter("@started_at", SqlDbType.DateTime2, session.StartedAt == default ? null : session.StartedAt);
        command.AddParameter("@completed_at", SqlDbType.DateTime2, session.CompletedAt);
    }

    private static DeviceTestSession MapSession(SqlDataReader reader)
    {
        return new DeviceTestSession
        {
            SessionId = reader.GetStringValue("session_id"),
            RequestId = reader.GetStringValue("request_id"),
            DeviceId = reader.GetStringValue("device_id"),
            SellerUserId = reader.GetIntValue("seller_user_id"),
            SwitchTechnology = reader.GetStringValue("switch_technology"),
            NoiseRequirement = reader.GetEnumValue<NoiseRequirement>("noise_requirement"),
            TotalKeys = reader.GetIntValue("total_keys"),
            TestedKeys = reader.GetIntValue("tested_keys"),
            PassedKeys = reader.GetIntValue("passed_keys"),
            WarningKeys = reader.GetIntValue("warning_keys"),
            FailedKeys = reader.GetIntValue("failed_keys"),
            AverageLatencyMs = reader.GetNullableDecimalValue("average_latency_ms"),
            MaxLatencyMs = reader.GetNullableDecimalValue("max_latency_ms"),
            AverageNoiseDb = reader.GetNullableDecimalValue("average_noise_db"),
            MaxNoiseDb = reader.GetNullableDecimalValue("max_noise_db"),
            Status = reader.GetEnumValue<TestSessionStatus>("status"),
            StartedAt = reader.GetDateTimeValue("started_at"),
            CompletedAt = reader.GetNullableDateTimeValue("completed_at")
        };
    }
}
