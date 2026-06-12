using Custom_keyboard.Models.Components;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

public sealed class ComponentCatalogService : IComponentCatalogService
{
    private readonly IComponentRepository _componentRepository;

    public ComponentCatalogService(IComponentRepository componentRepository)
    {
        _componentRepository = componentRepository;
    }

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetBrandsAsync(cancellationToken);

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetLayoutsAsync(cancellationToken);

    public Task<Layout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
        => _componentRepository.GetLayoutByIdAsync(layoutId, cancellationToken);

    public Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetAvailableKitsAsync(cancellationToken);

    public Task<KeyboardKit?> GetKitByIdAsync(string kitId, CancellationToken cancellationToken = default)
        => _componentRepository.GetKitByIdAsync(kitId, cancellationToken);

    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetAvailableSwitchesAsync(cancellationToken);

    public Task<KeyboardSwitch?> GetSwitchByIdAsync(string switchId, CancellationToken cancellationToken = default)
        => _componentRepository.GetSwitchByIdAsync(switchId, cancellationToken);

    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetAvailableKeycapSetsAsync(cancellationToken);

    public Task<KeycapSet?> GetKeycapSetByIdAsync(string keycapId, CancellationToken cancellationToken = default)
        => _componentRepository.GetKeycapSetByIdAsync(keycapId, cancellationToken);

    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetAvailableStabilizersAsync(cancellationToken);

    public Task<Stabilizer?> GetStabilizerByIdAsync(string stabilizerId, CancellationToken cancellationToken = default)
        => _componentRepository.GetStabilizerByIdAsync(stabilizerId, cancellationToken);

    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetAvailableAccessoriesAsync(cancellationToken);

    public Task<Accessory?> GetAccessoryByIdAsync(string accessoryId, CancellationToken cancellationToken = default)
        => _componentRepository.GetAccessoryByIdAsync(accessoryId, cancellationToken);
}
