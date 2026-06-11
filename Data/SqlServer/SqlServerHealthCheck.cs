namespace Custom_keyboard.Data.SqlServer;

public sealed class SqlServerHealthCheck
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlServerHealthCheck(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return true;
    }
}
