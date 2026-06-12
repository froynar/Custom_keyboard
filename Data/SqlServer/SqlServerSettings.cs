namespace Custom_keyboard.Data.SqlServer;

public sealed class SqlServerSettings
{
    public string Server { get; set; } = @"KHOADZS1VN\SQLEXPRESS";
    public string Database { get; set; } = "CustomKeyboard_Refactor";
    public string ApplicationName { get; set; } = "Custom Keyboard Builder";
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public bool Pooling { get; set; }
    public bool MultipleActiveResultSets { get; set; }
    public bool IntegratedSecurity { get; set; } = true;
    public string? UserId { get; set; }
    public string? Password { get; set; }

    public string BuildConnectionString()
    {
        var authentication = IntegratedSecurity
            ? "Integrated Security=True"
            : $"User ID={UserId};Password={Password}";

        return string.Join(
            ";",
            $"Server={Server}",
            $"Database={Database}",
            authentication,
            $"Encrypt={Encrypt}",
            $"TrustServerCertificate={TrustServerCertificate}",
            $"Pooling={Pooling}",
            $"MultipleActiveResultSets={MultipleActiveResultSets}",
            $"Application Name={ApplicationName}");
    }
}
