using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;

namespace Custom_keyboard.Repositories;

public interface IComponentRepository
{
    // Brands
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<Brand?> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default);
    Task<Brand> SaveBrandAsync(Brand brand, CancellationToken cancellationToken = default);

    // Layouts
    Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default);
    Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<Layout> SaveLayoutAsync(Layout layout, CancellationToken cancellationToken = default);

    // Catalog read (build flow). Get*ById returns the item regardless of availability.
    Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(CancellationToken cancellationToken = default);
    Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default);
    Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default);
    Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default);
    Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default);
    Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default);

    // Admin catalog management (generic over the 5 editable catalog types)
    Task<IReadOnlyList<AdminComponentRecord>> GetAdminComponentsAsync(AdminComponentType componentType, CancellationToken cancellationToken = default);
    Task<AdminComponentRecord?> GetAdminComponentByIdAsync(AdminComponentType componentType, string componentId, CancellationToken cancellationToken = default);
    Task<int> GetComponentCountAsync(CancellationToken cancellationToken = default);
    Task<AdminComponentRecord> SaveAdminComponentAsync(AdminComponentRecord component, CancellationToken cancellationToken = default);
    Task SetComponentAvailabilityAsync(AdminComponentType componentType, string componentId, bool isAvailable, CancellationToken cancellationToken = default);
}
