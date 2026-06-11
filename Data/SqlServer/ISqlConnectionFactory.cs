using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Data.SqlServer;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
