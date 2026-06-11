using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;

namespace Custom_keyboard.Repositories;

public interface IComponentRepository
{
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<Brand?> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default);
    Task<Brand> SaveBrandAsync(Brand brand, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default);
    Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<Layout> SaveLayoutAsync(Layout layout, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeyboardCase>> GetCasesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pcb>> GetPcbsForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Plate>> GetPlatesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompatibilityRule>> GetCompatibilityRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(AdminComponentType componentType, CancellationToken cancellationToken = default);
    Task<AdminComponentRecord?> GetAdminComponentByIdAsync(AdminComponentType componentType, string componentId, CancellationToken cancellationToken = default);
    Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default);
    Task<AdminComponentRecord> SaveAdminComponentAsync(AdminComponentRecord component, CancellationToken cancellationToken = default);
    Task SetComponentAvailabilityAsync(AdminComponentType componentType, string componentId, bool isAvailable, CancellationToken cancellationToken = default);
}
