using System.Data;
using System.Text.Json;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlSellerApplicationRepository : ISellerApplicationRepository, ITransactionalSellerApplicationRepository
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new() { WriteIndented = false };

    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlSellerApplicationRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SellerApplication> AddAsync(SellerApplication application, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO seller_applications (buyer_user_id, shop_name, phone, address, note, status, created_at)
            VALUES (@buyer_user_id, @shop_name, @phone, @address, @note, @status, SYSUTCDATETIME());

            {BaseSelectSql} WHERE id = SCOPE_IDENTITY();
            """;
        command.AddParameter("@buyer_user_id", SqlDbType.Int, application.BuyerUserId);
        command.AddParameter("@shop_name", SqlDbType.VarChar, application.ShopName, 255);
        command.AddParameter("@phone", SqlDbType.VarChar, application.Phone, 30);
        command.AddParameter("@address", SqlDbType.VarChar, application.Address, 500);
        command.AddParameter("@note", SqlDbType.VarChar, application.Note, 500);
        command.AddParameter("@status", SqlDbType.VarChar, application.Status.ToString(), 50);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapApplication(reader);
            }
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("Ban da co mot don dang cho duyet.", ex);
        }

        throw new InvalidOperationException("Could not save seller application.");
    }

    public async Task<SellerApplication?> GetByIdAsync(int applicationId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE id = @application_id;";
        command.AddParameter("@application_id", SqlDbType.Int, applicationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapApplication(reader) : null;
    }

    public async Task<SellerApplication?> GetLatestByBuyerAsync(int buyerUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {BaseSelectSql}
            WHERE buyer_user_id = @buyer_user_id
            ORDER BY created_at DESC, id DESC
            OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;
            """;
        command.AddParameter("@buyer_user_id", SqlDbType.Int, buyerUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapApplication(reader) : null;
    }

    public async Task<bool> HasPendingAsync(int buyerUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM seller_applications
                WHERE buyer_user_id = @buyer_user_id AND status = 'Pending'
            ) THEN 1 ELSE 0 END;
            """;
        command.AddParameter("@buyer_user_id", SqlDbType.Int, buyerUserId);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<AdminSellerApplicationRow>> GetAdminRowsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<AdminSellerApplicationRow>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                sa.id AS application_id,
                sa.buyer_user_id,
                u.username,
                u.email,
                sa.shop_name,
                sa.phone,
                sa.address,
                sa.note,
                sa.status,
                sa.review_note,
                sa.created_at,
                sa.reviewed_at
            FROM seller_applications AS sa
            INNER JOIN users AS u ON u.id = sa.buyer_user_id
            ORDER BY CASE WHEN sa.status = 'Pending' THEN 0 ELSE 1 END, sa.created_at DESC, sa.id DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AdminSellerApplicationRow
            {
                ApplicationId = reader.GetIntValue("application_id"),
                BuyerUserId = reader.GetIntValue("buyer_user_id"),
                Username = reader.GetStringValue("username"),
                Email = reader.GetStringValue("email"),
                ShopName = reader.GetStringValue("shop_name"),
                Phone = reader.GetStringValue("phone"),
                Address = reader.GetStringValue("address"),
                Note = reader.GetNullableStringValue("note"),
                Status = reader.GetEnumValue<SellerApplicationStatus>("status"),
                ReviewNote = reader.GetNullableStringValue("review_note"),
                CreatedAt = reader.GetDateTimeValue("created_at"),
                ReviewedAt = reader.GetNullableDateTimeValue("reviewed_at")
            });
        }

        return results;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM seller_applications;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<bool> UpdateStatusAsync(
        int applicationId,
        SellerApplicationStatus status,
        string? reviewNote,
        int reviewedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE seller_applications
            SET status = @status,
                review_note = @review_note,
                reviewed_by = @reviewed_by,
                reviewed_at = SYSUTCDATETIME()
            WHERE id = @application_id
              AND status = 'Pending';
            """;
        command.AddParameter("@application_id", SqlDbType.Int, applicationId);
        command.AddParameter("@status", SqlDbType.VarChar, status.ToString(), 50);
        command.AddParameter("@review_note", SqlDbType.VarChar, reviewNote, 500);
        command.AddParameter("@reviewed_by", SqlDbType.Int, reviewedBy);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<SellerApplicationReviewOutcome> ApprovePendingAsync(
        SellerApplication application,
        int adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        var oldValueJson = JsonSerializer.Serialize(
            new { application.Status, application.BuyerUserId },
            AuditJsonOptions);
        var newValueJson = JsonSerializer.Serialize(
            new { Status = SellerApplicationStatus.Approved.ToString(), Role = UserRole.Seller.ToString(), Verified = true },
            AuditJsonOptions);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @outcome INT = 0; -- 0 success, 1 already processed, 2 invalid applicant

            BEGIN TRY
                BEGIN TRANSACTION;

                IF NOT EXISTS (
                    SELECT 1
                    FROM seller_applications WITH (UPDLOCK, HOLDLOCK)
                    WHERE id = @application_id
                      AND status = 'Pending'
                )
                BEGIN
                    SET @outcome = 1;
                END
                ELSE IF NOT EXISTS (
                    SELECT 1
                    FROM users AS u WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN roles AS r ON r.id = u.role_id
                    WHERE u.id = @buyer_user_id
                      AND u.is_active = 1
                      AND r.role_name = 'Buyer'
                )
                BEGIN
                    SET @outcome = 2;
                END
                ELSE
                BEGIN
                    UPDATE u
                    SET role_id = seller_role.id
                    FROM users AS u
                    CROSS JOIN roles AS seller_role
                    WHERE u.id = @buyer_user_id
                      AND seller_role.role_name = 'Seller';

                    IF EXISTS (SELECT 1 FROM seller_profiles WHERE user_id = @buyer_user_id)
                    BEGIN
                        UPDATE seller_profiles
                        SET shop_name = @shop_name,
                            phone = @phone,
                            address = @address,
                            is_verified = 1,
                            verified_at = SYSUTCDATETIME()
                        WHERE user_id = @buyer_user_id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO seller_profiles (user_id, shop_name, phone, address, is_verified, verified_at)
                        VALUES (@buyer_user_id, @shop_name, @phone, @address, 1, SYSUTCDATETIME());
                    END

                    UPDATE seller_applications
                    SET status = 'Approved',
                        review_note = @review_note,
                        reviewed_by = @reviewed_by,
                        reviewed_at = SYSUTCDATETIME()
                    WHERE id = @application_id
                      AND status = 'Pending';

                    IF @@ROWCOUNT <> 1
                    BEGIN
                        SET @outcome = 1;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO audit_log (user_id, table_name, record_id, action, old_value_json, new_value_json, changed_at)
                        VALUES (@reviewed_by, 'seller_applications', CONVERT(VARCHAR(100), @application_id),
                                'SellerApplicationApprove', @old_value_json, @new_value_json, SYSUTCDATETIME());
                    END
                END

                IF @outcome = 0
                    COMMIT TRANSACTION;
                ELSE
                    ROLLBACK TRANSACTION;

                SELECT @outcome;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0
                    ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """;
        AddReviewParameters(command, application, adminUserId, reviewNote, oldValueJson, newValueJson);

        return ToReviewOutcome(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)));
    }

    public async Task<SellerApplicationReviewOutcome> RejectPendingAsync(
        SellerApplication application,
        int adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        var oldValueJson = JsonSerializer.Serialize(
            new { application.Status, application.BuyerUserId },
            AuditJsonOptions);
        var newValueJson = JsonSerializer.Serialize(
            new { Status = SellerApplicationStatus.Rejected.ToString() },
            AuditJsonOptions);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @outcome INT = 0; -- 0 success, 1 already processed

            BEGIN TRY
                BEGIN TRANSACTION;

                UPDATE sa
                SET status = 'Rejected',
                    review_note = @review_note,
                    reviewed_by = @reviewed_by,
                    reviewed_at = SYSUTCDATETIME()
                FROM seller_applications AS sa WITH (UPDLOCK, HOLDLOCK)
                WHERE sa.id = @application_id
                  AND sa.status = 'Pending';

                IF @@ROWCOUNT <> 1
                BEGIN
                    SET @outcome = 1;
                END
                ELSE
                BEGIN
                    INSERT INTO audit_log (user_id, table_name, record_id, action, old_value_json, new_value_json, changed_at)
                    VALUES (@reviewed_by, 'seller_applications', CONVERT(VARCHAR(100), @application_id),
                            'SellerApplicationReject', @old_value_json, @new_value_json, SYSUTCDATETIME());
                END

                IF @outcome = 0
                    COMMIT TRANSACTION;
                ELSE
                    ROLLBACK TRANSACTION;

                SELECT @outcome;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0
                    ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """;
        AddReviewParameters(command, application, adminUserId, reviewNote, oldValueJson, newValueJson);

        return ToReviewOutcome(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)));
    }

    private const string BaseSelectSql = """
        SELECT
            id AS application_id,
            buyer_user_id,
            shop_name,
            phone,
            address,
            note,
            status,
            review_note,
            created_at,
            reviewed_at,
            reviewed_by
        FROM seller_applications
        """;

    private static SellerApplication MapApplication(SqlDataReader reader)
    {
        return new SellerApplication
        {
            ApplicationId = reader.GetIntValue("application_id"),
            BuyerUserId = reader.GetIntValue("buyer_user_id"),
            ShopName = reader.GetStringValue("shop_name"),
            Phone = reader.GetStringValue("phone"),
            Address = reader.GetStringValue("address"),
            Note = reader.GetNullableStringValue("note"),
            Status = reader.GetEnumValue<SellerApplicationStatus>("status"),
            ReviewNote = reader.GetNullableStringValue("review_note"),
            CreatedAt = reader.GetDateTimeValue("created_at"),
            ReviewedAt = reader.GetNullableDateTimeValue("reviewed_at"),
            ReviewedBy = reader.GetNullableIntValue("reviewed_by")
        };
    }

    private static void AddReviewParameters(
        SqlCommand command,
        SellerApplication application,
        int adminUserId,
        string? reviewNote,
        string oldValueJson,
        string newValueJson)
    {
        command.AddParameter("@application_id", SqlDbType.Int, application.ApplicationId);
        command.AddParameter("@buyer_user_id", SqlDbType.Int, application.BuyerUserId);
        command.AddParameter("@shop_name", SqlDbType.VarChar, application.ShopName, 255);
        command.AddParameter("@phone", SqlDbType.VarChar, application.Phone, 30);
        command.AddParameter("@address", SqlDbType.VarChar, application.Address, 500);
        command.AddParameter("@review_note", SqlDbType.VarChar, reviewNote, 500);
        command.AddParameter("@reviewed_by", SqlDbType.Int, adminUserId);
        command.AddParameter("@old_value_json", SqlDbType.NVarChar, oldValueJson, -1);
        command.AddParameter("@new_value_json", SqlDbType.NVarChar, newValueJson, -1);
    }

    private static SellerApplicationReviewOutcome ToReviewOutcome(int value)
        => value switch
        {
            0 => SellerApplicationReviewOutcome.Success,
            2 => SellerApplicationReviewOutcome.InvalidApplicant,
            _ => SellerApplicationReviewOutcome.AlreadyProcessed
        };

    private static bool IsUniqueViolation(SqlException exception)
        => exception.Number is 2601 or 2627;
}
