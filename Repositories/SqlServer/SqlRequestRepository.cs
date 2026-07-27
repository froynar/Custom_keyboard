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
        command.CommandText = $"{BaseSelectSql} WHERE request_view.request_id = @request_id;";
        command.AddParameter("@request_id", SqlDbType.VarChar, requestId, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRequest(reader) : null;
    }

    public Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"""
            {BaseSelectSql}
            WHERE request_view.buyer_id = @buyer_id
            ORDER BY request_view.requested_at DESC, request_view.request_id;
            """,
            command => command.AddParameter("@buyer_id", SqlDbType.Int, buyerId),
            cancellationToken);
    }

    public Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            $"""
            {BaseSelectSql}
            WHERE request_view.seller_user_id = @seller_user_id
            ORDER BY request_view.requested_at DESC, request_view.request_id;
            """,
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
        EnsureRequestId(request);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await SaveCoreAsync(connection, null, request, cancellationToken);
    }

    public async Task<BuildRequest> SaveAndSetBuildStatusAsync(
        BuildRequest request,
        BuildStatus buildStatus,
        CancellationToken cancellationToken = default)
    {
        EnsureRequestId(request);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var saved = await SaveCoreAsync(connection, transaction, request, cancellationToken);

            await using var buildCommand = connection.CreateCommand();
            buildCommand.Transaction = transaction;
            buildCommand.CommandText = """
                UPDATE builds
                SET status = @status,
                    updated_at = SYSUTCDATETIME()
                WHERE id = @build_id;
                """;
            buildCommand.AddParameter("@status", SqlDbType.VarChar, buildStatus.ToString(), 50);
            buildCommand.AddParameter("@build_id", SqlDbType.VarChar, saved.BuildId, 50);

            if (await buildCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("Could not update the owning build status.");
            }

            await transaction.CommitAsync(cancellationToken);
            return saved;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<BuildRequest> SaveCoreAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SqlRepositoryHelpers.IndexedDmlSetOptions + $"""
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

            {BaseSelectSql}
            WHERE request_view.request_id = @request_id;
            """;
        AddRequestParameters(command, request);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapRequest(reader);
        }

        throw new InvalidOperationException("Could not save build request.");
    }

    private static void EnsureRequestId(BuildRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            request.RequestId = $"REQ_{Guid.NewGuid():N}";
        }
    }

    public async Task<BuildRequest?> TryUpdateStatusAsync(
        BuildRequest request,
        RequestStatus expectedStatus,
        bool requireAcceptableQc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlRepositoryHelpers.IndexedDmlSetOptions + $"""
            DECLARE @updated BIT = 0;

            UPDATE request_row WITH (UPDLOCK, ROWLOCK)
            SET
                status = @status,
                accepted_at = CASE
                    WHEN @status = 'Accepted' THEN COALESCE(accepted_at, @accepted_at)
                    ELSE accepted_at
                END,
                completed_at = CASE
                    WHEN @status = 'Completed' THEN COALESCE(completed_at, @completed_at)
                    ELSE completed_at
                END,
                updated_at = SYSUTCDATETIME()
            FROM build_requests AS request_row
            WHERE request_row.id = @request_id
              AND request_row.seller_user_id = @seller_user_id
              AND request_row.status = @expected_status
              AND (
                  @require_acceptable_qc = 0
                  OR EXISTS (
                      SELECT 1
                      FROM views.Last_QC AS latest_qc
                      WHERE latest_qc.request_id = request_row.id
                        AND latest_qc.is_acceptable = 1
                  )
              );

            IF @@ROWCOUNT = 1
                SET @updated = 1;

            {BaseSelectSql}
            WHERE request_view.request_id = @request_id
              AND @updated = 1;
            """;
        command.AddParameter("@request_id", SqlDbType.VarChar, request.RequestId, 50);
        command.AddParameter("@seller_user_id", SqlDbType.Int, request.SellerUserId);
        command.AddParameter("@expected_status", SqlDbType.VarChar, expectedStatus.ToString(), 50);
        command.AddParameter("@status", SqlDbType.VarChar, request.Status.ToString(), 50);
        command.AddParameter("@accepted_at", SqlDbType.DateTime2, request.AcceptedAt);
        command.AddParameter("@completed_at", SqlDbType.DateTime2, request.CompletedAt);
        command.AddParameter("@require_acceptable_qc", SqlDbType.Bit, requireAcceptableQc);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRequest(reader) : null;
    }

    private const string BaseSelectSql = """
        SELECT
            request_view.request_id,
            request_view.build_id,
            request_view.seller_user_id,
            request_view.seller_shop_name,
            request_view.request_payload_json,
            request_view.status,
            request_view.note,
            request_view.requested_at,
            request_view.accepted_at,
            request_view.completed_at,
            request_view.updated_at
        FROM views.Req_view AS request_view
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
        command.AddParameter("@note", SqlDbType.NVarChar, request.Note, 500);
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
            SellerShopName = reader.GetStringValue("seller_shop_name"),
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
