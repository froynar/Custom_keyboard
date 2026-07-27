using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlAuditLogRepository : IAuditLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlAuditLogRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        var results = new List<AuditLogEntry>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                id AS log_id,
                user_id,
                table_name,
                record_id,
                action,
                old_value_json,
                new_value_json,
                changed_at
            FROM audit_log
            ORDER BY changed_at DESC, id DESC;
            """;
        command.AddParameter("@take", SqlDbType.Int, take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapAuditLogEntry(reader));
        }

        return results;
    }

    public async Task<AuditLogEntry> AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_log (
                user_id,
                table_name,
                record_id,
                action,
                old_value_json,
                new_value_json
            )
            VALUES (
                @user_id,
                @table_name,
                @record_id,
                @action,
                @old_value_json,
                @new_value_json
            );

            DECLARE @new_log_id INT = CAST(SCOPE_IDENTITY() AS INT);

            SELECT
                id AS log_id,
                user_id,
                table_name,
                record_id,
                action,
                old_value_json,
                new_value_json,
                changed_at
            FROM audit_log
            WHERE id = @new_log_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, entry.UserId);
        command.AddParameter("@table_name", SqlDbType.VarChar, entry.TableName, 100);
        command.AddParameter("@record_id", SqlDbType.VarChar, entry.RecordId, 100);
        command.AddParameter("@action", SqlDbType.VarChar, entry.Action, 100);
        command.AddParameter("@old_value_json", SqlDbType.NVarChar, entry.OldValueJson, -1);
        command.AddParameter("@new_value_json", SqlDbType.NVarChar, entry.NewValueJson, -1);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapAuditLogEntry(reader);
        }

        throw new InvalidOperationException("Could not insert audit log entry.");
    }

    private static AuditLogEntry MapAuditLogEntry(SqlDataReader reader)
    {
        return new AuditLogEntry
        {
            LogId = reader.GetIntValue("log_id"),
            UserId = reader.GetIntValue("user_id"),
            TableName = reader.GetStringValue("table_name"),
            RecordId = reader.GetStringValue("record_id"),
            Action = reader.GetStringValue("action"),
            OldValueJson = reader.GetNullableStringValue("old_value_json"),
            NewValueJson = reader.GetNullableStringValue("new_value_json"),
            ChangedAt = reader.GetDateTimeValue("changed_at")
        };
    }
}
