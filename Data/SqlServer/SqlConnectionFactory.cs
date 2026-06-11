using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Data.SqlServer;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly SqlServerSettings _settings;

    public SqlConnectionFactory(SqlServerSettings? settings = null)
    {
        _settings = settings ?? new SqlServerSettings();
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_settings.BuildConnectionString());
    }
}
