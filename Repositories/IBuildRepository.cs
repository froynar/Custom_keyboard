using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Repositories;

public interface IBuildRepository
{
    /// <summary>The buyer's active builds (excludes Archived). Used for the buyer build list.</summary>
    Task<IReadOnlyList<KeyboardBuild>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild?> GetByIdAsync(string buildId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild> SaveAsync(KeyboardBuild build, CancellationToken cancellationToken = default);

    /// <summary>Update only the build status (e.g. Saved -> Requested) without rewriting items/mods.</summary>
    Task SetStatusAsync(string buildId, BuildStatus status, CancellationToken cancellationToken = default);

    Task ArchiveAsync(string buildId, CancellationToken cancellationToken = default);
}
