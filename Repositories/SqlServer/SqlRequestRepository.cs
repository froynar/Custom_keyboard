using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlRequestRepository : IRequestRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlRequestRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BuildRequest?> GetByIdAsync(string requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE br.id = @request_id;";
        command.AddParameter("@request_id", SqlDbType.VarChar, requestId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRequest(reader) : null;
    }

    public Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default)
    {
        // build_requests no longer stores buyer_id; the buyer is derived from the linked build.
        return QueryAsync(
            $"""
            {BaseSelectSql}
            INNER JOIN builds AS b ON b.id = br.build_id
            WHERE b.buyer_id = @buyer_id
            ORDER BY br.requested_at DESC, br.id;
            """,
            command => command.AddParameter("@buyer_id", SqlDbType.Int, buyerId),
            cancellationToken);
    }

    public Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"{BaseSelectSql} WHERE br.seller_user_id = @seller_user_id ORDER BY br.requested_at DESC, br.id;",
            command => command.AddParameter("@seller_user_id", SqlDbType.Int, sellerUserId),
            cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM build_requests;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<BuildRequest> SaveAsync(BuildRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            request.RequestId = $"REQ_{Guid.NewGuid():N}";
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM build_requests WHERE id = @request_id)
            BEGIN
                UPDATE build_requests
                SET
                    build_id = @build_id,
                    seller_user_id = @seller_user_id,
                    request_payload_json = @request_payload_json,
                    status = @status,
                    note = @note,
                    accepted_at = @accepted_at,
                    completed_at = @completed_at,
                    updated_at = SYSUTCDATETIME()
                WHERE id = @request_id;
            END
            ELSE
            BEGIN
                INSERT INTO build_requests (
                    id,
                    build_id,
                    seller_user_id,
                    request_payload_json,
                    status,
                    note,
                    requested_at,
                    accepted_at,
                    completed_at
                )
                VALUES (
                    @request_id,
                    @build_id,
                    @seller_user_id,
                    @request_payload_json,
                    @status,
                    @note,
                    COALESCE(@requested_at, SYSUTCDATETIME()),
                    @accepted_at,
                    @completed_at
                );
            END;

            SELECT
                br.id AS request_id,
                br.build_id,
                br.seller_user_id,
                br.request_payload_json,
                br.status,
                br.note,
                br.requested_at,
                br.accepted_at,
                br.completed_at,
                br.updated_at
            FROM build_requests AS br
            WHERE br.id = @request_id;
            """;
        AddRequestParameters(command, request);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapRequest(reader);
        }

        throw new InvalidOperationException("Could not save build request.");
    }

    private const string BaseSelectSql = """
        SELECT
            br.id AS request_id,
            br.build_id,
            br.seller_user_id,
            br.request_payload_json,
            br.status,
            br.note,
            br.requested_at,
            br.accepted_at,
            br.completed_at,
            br.updated_at
        FROM build_requests AS br
        """;

    private async Task<IReadOnlyList<BuildRequest>> QueryAsync(
        string commandText,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
    {
        var results = new List<BuildRequest>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapRequest(reader));
        }

        return results;
    }

    private static void AddRequestParameters(SqlCommand command, BuildRequest request)
    {
        command.AddParameter("@request_id", SqlDbType.VarChar, request.RequestId, 50);
        command.AddParameter("@build_id", SqlDbType.VarChar, request.BuildId, 50);
        command.AddParameter("@seller_user_id", SqlDbType.Int, request.SellerUserId);
        command.AddParameter("@request_payload_json", SqlDbType.NVarChar, request.RequestPayloadJson, -1);
        command.AddParameter("@status", SqlDbType.VarChar, request.Status.ToString(), 50);
        command.AddParameter("@note", SqlDbType.VarChar, request.Note, 500);
        command.AddParameter("@requested_at", SqlDbType.DateTime2, request.RequestedAt == default ? null : request.RequestedAt);
        command.AddParameter("@accepted_at", SqlDbType.DateTime2, request.AcceptedAt);
        command.AddParameter("@completed_at", SqlDbType.DateTime2, request.CompletedAt);
    }

    private static BuildRequest MapRequest(SqlDataReader reader)
    {
        return new BuildRequest
        {
            RequestId = reader.GetStringValue("request_id"),
            BuildId = reader.GetStringValue("build_id"),
            SellerUserId = reader.GetIntValue("seller_user_id"),
            RequestPayloadJson = reader.GetStringValue("request_payload_json"),
            Status = reader.GetEnumValue<RequestStatus>("status"),
            Note = reader.GetNullableStringValue("note"),
            RequestedAt = reader.GetDateTimeValue("requested_at"),
            AcceptedAt = reader.GetNullableDateTimeValue("accepted_at"),
            CompletedAt = reader.GetNullableDateTimeValue("completed_at"),
            UpdatedAt = reader.GetNullableDateTimeValue("updated_at")
        };
    }
}
