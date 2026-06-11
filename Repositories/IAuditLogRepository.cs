using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.Repositories;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<AuditLogEntry> AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
