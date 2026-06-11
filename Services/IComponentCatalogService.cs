using Custom_keyboard.Models.Components;

namespace Custom_keyboard.Services;

public interface IComponentCatalogService
{
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeyboardCase>> GetCasesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pcb>> GetPcbsForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Plate>> GetPlatesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompatibilityRule>> GetCompatibilityRulesAsync(CancellationToken cancellationToken = default);
}
