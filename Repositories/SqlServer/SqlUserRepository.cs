using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlUserRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<User>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                u.id AS user_id,
                u.role_id,
                r.role_name,
                u.username,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active
            FROM users AS u
            INNER JOIN roles AS r ON r.id = u.role_id
            ORDER BY u.id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapUser(reader));
        }

        return results;
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return FindSingleAsync("u.username = @value", username, 100, cancellationToken);
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return FindSingleAsync("u.email = @value", email, 255, cancellationToken);
    }

    public async Task<User?> FindByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default)
    {
        return await FindSingleAsync(
            "u.email = @value OR u.username = @value",
            emailOrUsername,
            255,
            cancellationToken);
    }

    public Task<User?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        return FindSingleAsync("u.phone = @value", phone, 30, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                u.id AS user_id,
                u.role_id,
                r.role_name,
                u.username,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active
            FROM users AS u
            INNER JOIN roles AS r ON r.id = u.role_id
            WHERE u.id = @user_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM users;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<User?> FindSingleAsync(
        string predicateSql,
        string value,
        int parameterSize,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                u.id AS user_id,
                u.role_id,
                r.role_name,
                u.username,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active
            FROM users AS u
            INNER JOIN roles AS r ON r.id = u.role_id
            WHERE {predicateSql};
            """;
        command.AddParameter("@value", SqlDbType.VarChar, value, parameterSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @resolved_role_id INT =
                CASE
                    WHEN @role_id > 0 THEN @role_id
                    ELSE (SELECT id FROM roles WHERE role_name = @role_name)
                END;

            INSERT INTO users (role_id, username, email, phone, password_hash, is_active)
            VALUES (@resolved_role_id, @username, @email, @phone, @password_hash, @is_active);

            DECLARE @new_user_id INT = CAST(SCOPE_IDENTITY() AS INT);

            SELECT
                u.id AS user_id,
                u.role_id,
                r.role_name,
                u.username,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active
            FROM users AS u
            INNER JOIN roles AS r ON r.id = u.role_id
            WHERE u.id = @new_user_id;
            """;
        AddUserParameters(command, user);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapUser(reader);
        }

        throw new InvalidOperationException("Could not insert user.");
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users
            SET
                role_id =
                    CASE
                        WHEN @role_id > 0 THEN @role_id
                        ELSE (SELECT id FROM roles WHERE role_name = @role_name)
                    END,
                username = @username,
                email = @email,
                phone = @phone,
                password_hash = @password_hash,
                is_active = @is_active
            WHERE id = @user_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, user.UserId);
        AddUserParameters(command, user);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users
            SET is_active = @is_active
            WHERE id = @user_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, userId);
        command.AddParameter("@is_active", SqlDbType.Bit, isActive);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetRoleAsync(int userId, UserRole role, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users
            SET role_id = (SELECT id FROM roles WHERE role_name = @role_name)
            WHERE id = @user_id;
            """;
        command.AddParameter("@user_id", SqlDbType.Int, userId);
        command.AddParameter("@role_name", SqlDbType.VarChar, role.ToString(), 50);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddUserParameters(SqlCommand command, User user)
    {
        command.AddParameter("@role_id", SqlDbType.Int, user.RoleId);
        command.AddParameter("@role_name", SqlDbType.VarChar, user.Role.ToString(), 50);
        command.AddParameter("@username", SqlDbType.VarChar, user.Username, 100);
        command.AddParameter("@email", SqlDbType.VarChar, user.Email, 255);
        command.AddParameter("@phone", SqlDbType.VarChar, user.Phone, 30);
        command.AddParameter("@password_hash", SqlDbType.VarChar, user.PasswordHash, 255);
        command.AddParameter("@is_active", SqlDbType.Bit, user.IsActive);
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetIntValue("user_id"),
            RoleId = reader.GetIntValue("role_id"),
            Role = reader.GetEnumValue<UserRole>("role_name"),
            Username = reader.GetStringValue("username"),
            Email = reader.GetStringValue("email"),
            Phone = reader.GetStringValue("phone"),
            PasswordHash = reader.GetStringValue("password_hash"),
            IsActive = reader.GetBoolValue("is_active")
        };
    }
}
