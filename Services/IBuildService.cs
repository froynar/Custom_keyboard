using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.Services;

public interface IBuildService
{
    Task<IReadOnlyList<KeyboardBuild>> GetBuyerBuildsAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild?> GetBuildByIdAsync(string buildId, CancellationToken cancellationToken = default);
    Task<KeyboardBuild> SaveBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default);
    Task ArchiveBuildAsync(string buildId, int buyerId, CancellationToken cancellationToken = default);
    Task<decimal> CalculateTotalAsync(KeyboardBuild build, CancellationToken cancellationToken = default);
    Task<BuildValidationResult> ValidateBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default);
}
