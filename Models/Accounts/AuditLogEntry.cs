namespace Custom_keyboard.Models.Accounts;

public sealed class AuditLogEntry
{
    public int LogId { get; set; }
    public int UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public DateTime ChangedAt { get; set; }
}
