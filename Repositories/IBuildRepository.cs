using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.Repositories;

public interface IBuildRepository
{
    Task<IReadOnlyList<KeyboardBuild>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild?> GetByIdAsync(string buildId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild> SaveAsync(KeyboardBuild build, CancellationToken cancellationToken = default);
    Task ArchiveAsync(string buildId, CancellationToken cancellationToken = default);
}
