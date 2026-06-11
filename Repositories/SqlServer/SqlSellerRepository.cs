using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlSellerRepository : ISellerRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlSellerRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SellerProfile?> GetBySellerUserIdAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelectSql} WHERE user_id = @user_id;";
        command.AddParameter("@user_id", SqlDbType.Int, sellerUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSellerProfile(reader) : null;
    }

    public Task<IReadOnlyList<AdminSellerProfileRow>> GetAdminSellerProfilesAsync(CancellationToken cancellationToken = default)
    {
        return QuerySellerRowsAsync(
            """
            SELECT
                u.user_id,
                u.username,
                u.email,
                u.phone AS user_phone,
                u.is_active,
                COALESCE(sp.seller_profile_id, 0) AS seller_profile_id,
                sp.assigned_by_admin_id,
                COALESCE(sp.shop_name, '') AS shop_name,
                COALESCE(sp.phone, '') AS profile_phone,
                COALESCE(sp.address, '') AS address,
                CAST(COALESCE(sp.is_verified, 0) AS bit) AS is_verified,
                sp.verified_by_admin_id,
                sp.assigned_at,
                sp.verified_at
            FROM users AS u
            INNER JOIN roles AS r ON r.role_id = u.role_id
            LEFT JOIN seller_profiles AS sp ON sp.user_id = u.user_id
            WHERE r.role_name = 'Seller'
            ORDER BY u.user_id;
            """,
            cancellationToken);
    }

    public Task<IReadOnlyList<SellerProfile>> GetVerifiedSellersAsync(CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            """
            SELECT
                sp.seller_profile_id,
                sp.user_id,
                sp.assigned_by_admin_id,
                sp.shop_name,
                sp.phone,
                sp.address,
                sp.is_verified,
                sp.verified_by_admin_id,
                sp.assigned_at,
                sp.verified_at
            FROM seller_profiles AS sp
            INNER JOIN users AS u ON u.user_id = sp.user_id
            INNER JOIN roles AS r ON r.role_id = u.role_id
            WHERE sp.is_verified = 1
              AND u.is_active = 1
              AND r.role_name = 'Seller'
            ORDER BY sp.shop_name;
            """,
            configureCommand: null,
            cancellationToken);
    }

    public async Task<int> GetSellerCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM users AS u
            INNER JOIN roles AS r ON r.role_id = u.role_id
            WHERE r.role_name = 'Seller';
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<SellerProfile> SaveAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (
                SELECT 1
                FROM seller_profiles
                WHERE seller_profile_id = @seller_profile_id
                   OR user_id = @user_id
            )
            BEGIN
                UPDATE seller_profiles
                SET
                    assigned_by_admin_id = @assigned_by_admin_id,
                    shop_name = @shop_name,
                    phone = @phone,
                    address = @address,
                    is_verified = @is_verified,
                    verified_by_admin_id = @verified_by_admin_id,
                    assigned_at = @assigned_at,
                    verified_at = @verified_at
                WHERE seller_profile_id = @seller_profile_id
                   OR user_id = @user_id;
            END
            ELSE
            BEGIN
                INSERT INTO seller_profiles (
                    user_id,
                    assigned_by_admin_id,
                    shop_name,
                    phone,
                    address,
                    is_verified,
                    verified_by_admin_id,
                    assigned_at,
                    verified_at
                )
                VALUES (
                    @user_id,
                    @assigned_by_admin_id,
                    @shop_name,
                    @phone,
                    @address,
                    @is_verified,
                    @verified_by_admin_id,
                    @assigned_at,
                    @verified_at
                );
            END;

            SELECT
                seller_profile_id,
                user_id,
                assigned_by_admin_id,
                shop_name,
                phone,
                address,
                is_verified,
                verified_by_admin_id,
                assigned_at,
                verified_at
            FROM seller_profiles
            WHERE user_id = @user_id;
            """;
        AddSellerProfileParameters(command, sellerProfile);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapSellerProfile(reader);
        }

        throw new InvalidOperationException("Could not save seller profile.");
    }

    public async Task SetVerifiedAsync(int sellerUserId, bool isVerified, int adminUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE seller_profiles
            SET
                is_verified = @is_verified,
                verified_by_admin_id = CASE WHEN @is_verified = 1 THEN @admin_user_id ELSE NULL END,
                verified_at = CASE WHEN @is_verified = 1 THEN SYSUTCDATETIME() ELSE NULL END
            WHERE user_id = @user_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, sellerUserId);
        command.AddParameter("@is_verified", SqlDbType.Bit, isVerified);
        command.AddParameter("@admin_user_id", SqlDbType.Int, adminUserId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string BaseSelectSql = """
        SELECT
            seller_profile_id,
            user_id,
            assigned_by_admin_id,
            shop_name,
            phone,
            address,
            is_verified,
            verified_by_admin_id,
            assigned_at,
            verified_at
        FROM seller_profiles
        """;

    private async Task<IReadOnlyList<SellerProfile>> QueryAsync(
        string commandText,
        Action<SqlCommand>? configureCommand,
        CancellationToken cancellationToken)
    {
        var results = new List<SellerProfile>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapSellerProfile(reader));
        }

        return results;
    }

    private async Task<IReadOnlyList<AdminSellerProfileRow>> QuerySellerRowsAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminSellerProfileRow>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapSellerRow(reader));
        }

        return results;
    }

    private static void AddSellerProfileParameters(SqlCommand command, SellerProfile sellerProfile)
    {
        command.AddParameter("@seller_profile_id", SqlDbType.Int, sellerProfile.SellerProfileId);
        command.AddParameter("@user_id", SqlDbType.Int, sellerProfile.UserId);
        command.AddParameter("@assigned_by_admin_id", SqlDbType.Int, sellerProfile.AssignedByAdminId);
        command.AddParameter("@shop_name", SqlDbType.VarChar, sellerProfile.ShopName, 255);
        command.AddParameter("@phone", SqlDbType.VarChar, sellerProfile.Phone, 30);
        command.AddParameter("@address", SqlDbType.VarChar, sellerProfile.Address, 500);
        command.AddParameter("@is_verified", SqlDbType.Bit, sellerProfile.IsVerified);
        command.AddParameter("@verified_by_admin_id", SqlDbType.Int, sellerProfile.VerifiedByAdminId);
        command.AddParameter("@assigned_at", SqlDbType.DateTime2, sellerProfile.AssignedAt);
        command.AddParameter("@verified_at", SqlDbType.DateTime2, sellerProfile.VerifiedAt);
    }

    private static SellerProfile MapSellerProfile(SqlDataReader reader)
    {
        return new SellerProfile
        {
            SellerProfileId = reader.GetIntValue("seller_profile_id"),
            UserId = reader.GetIntValue("user_id"),
            AssignedByAdminId = reader.GetNullableIntValue("assigned_by_admin_id"),
            ShopName = reader.GetStringValue("shop_name"),
            Phone = reader.GetStringValue("phone"),
            Address = reader.GetStringValue("address"),
            IsVerified = reader.GetBoolValue("is_verified"),
            VerifiedByAdminId = reader.GetNullableIntValue("verified_by_admin_id"),
            AssignedAt = reader.GetNullableDateTimeValue("assigned_at"),
            VerifiedAt = reader.GetNullableDateTimeValue("verified_at")
        };
    }

    private static AdminSellerProfileRow MapSellerRow(SqlDataReader reader)
    {
        return new AdminSellerProfileRow
        {
            UserId = reader.GetIntValue("user_id"),
            Username = reader.GetStringValue("username"),
            Email = reader.GetStringValue("email"),
            UserPhone = reader.GetStringValue("user_phone"),
            IsActive = reader.GetBoolValue("is_active"),
            SellerProfileId = reader.GetIntValue("seller_profile_id"),
            AssignedByAdminId = reader.GetNullableIntValue("assigned_by_admin_id"),
            ShopName = reader.GetStringValue("shop_name"),
            ProfilePhone = reader.GetStringValue("profile_phone"),
            Address = reader.GetStringValue("address"),
            IsVerified = reader.GetBoolValue("is_verified"),
            VerifiedByAdminId = reader.GetNullableIntValue("verified_by_admin_id"),
            AssignedAt = reader.GetNullableDateTimeValue("assigned_at"),
            VerifiedAt = reader.GetNullableDateTimeValue("verified_at")
        };
    }
}
