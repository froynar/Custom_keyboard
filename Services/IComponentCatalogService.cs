using Custom_keyboard.Models.Components;

namespace Custom_keyboard.Services;

// Read-only catalog for the kit-based build flow. List methods return only available
// items; Get*ById returns the item regardless of availability (for resolving existing builds).
public interface IComponentCatalogService
{
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default);
    Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default);

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
}
