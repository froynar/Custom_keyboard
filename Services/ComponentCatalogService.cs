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
    {
        return _componentRepository.GetBrandsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetLayoutsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<KeyboardCase>> GetCasesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetCasesForLayoutAsync(layoutId, cancellationToken);
    }

    public Task<IReadOnlyList<Pcb>> GetPcbsForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetPcbsForLayoutAsync(layoutId, cancellationToken);
    }

    public Task<IReadOnlyList<Plate>> GetPlatesForLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetPlatesForLayoutAsync(layoutId, cancellationToken);
    }

    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetAvailableSwitchesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetAvailableKeycapSetsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetAvailableStabilizersAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CompatibilityRule>> GetCompatibilityRulesAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetCompatibilityRulesAsync(cancellationToken);
    }
}
