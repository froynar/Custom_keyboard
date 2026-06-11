using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

public sealed class BuildService : IBuildService
{
    private readonly IBuildRepository _buildRepository;
    private readonly IComponentCatalogService _catalogService;

    public BuildService(IBuildRepository buildRepository, IComponentCatalogService catalogService)
    {
        _buildRepository = buildRepository;
        _catalogService = catalogService;
    }

    public Task<IReadOnlyList<KeyboardBuild>> GetBuyerBuildsAsync(int buyerId, CancellationToken cancellationToken = default)
    {
        return _buildRepository.GetByBuyerAsync(buyerId, cancellationToken);
    }

    public async Task<KeyboardBuild> SaveBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateBuildAsync(build, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Messages));
        }

        build.Name = build.Name.Trim();
        build.Notes = NormalizeNullable(build.Notes);
        build.Status = BuildStatus.Saved;
        build.TotalCostSnapshot = validation.TotalCost;
        build.Mods = NormalizeMods(build.Mods);

        return await _buildRepository.SaveAsync(build, cancellationToken);
    }

    public async Task<decimal> CalculateTotalAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateBuildAsync(build, cancellationToken);
        return validation.TotalCost;
    }

    public async Task<BuildValidationResult> ValidateBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var result = new BuildValidationResult();

        if (build.UserId <= 0)
        {
            result.Messages.Add("Buyer khong hop le.");
        }

        if (string.IsNullOrWhiteSpace(build.Name))
        {
            result.Messages.Add("Nhap ten build truoc khi luu.");
        }

        var layout = await FindLayoutAsync(build.LayoutId, result, cancellationToken);
        if (layout is null)
        {
            return result;
        }

        var selectedCase = await FindCaseAsync(build, result, cancellationToken);
        var selectedPcb = await FindPcbAsync(build, result, cancellationToken);
        var selectedPlate = await FindPlateAsync(build, result, cancellationToken);
        var selectedSwitch = await FindSwitchAsync(build, result, cancellationToken);
        var selectedKeycap = await FindKeycapAsync(build, result, cancellationToken);
        var selectedStabilizer = await FindStabilizerAsync(build, result, cancellationToken);

        if (selectedCase is not null
            && selectedPcb is not null
            && selectedPlate is not null)
        {
            await ValidateMainComponentRuleAsync(selectedCase, selectedPcb, selectedPlate, result, cancellationToken);
        }

        if (selectedPcb is not null && selectedSwitch is not null)
        {
            if (!Same(selectedPcb.PcbTechnology, selectedSwitch.SwitchTechnology))
            {
                result.Messages.Add(
                    $"PCB technology '{selectedPcb.PcbTechnology}' khong phu hop voi switch technology '{selectedSwitch.SwitchTechnology}'.");
            }

            if (!Same(selectedPcb.SwitchMount, selectedSwitch.MountType))
            {
                result.Messages.Add(
                    $"Switch mount khong phu hop: PCB can '{selectedPcb.SwitchMount}' nhung switch la '{selectedSwitch.MountType}'.");
            }
        }

        result.TotalCost = CalculateTotal(
            layout,
            selectedCase,
            selectedPcb,
            selectedPlate,
            selectedSwitch,
            selectedKeycap,
            selectedStabilizer);

        return result;
    }

    private async Task<Layout?> FindLayoutAsync(
        string layoutId,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            result.Messages.Add("Chon layout truoc.");
            return null;
        }

        var layouts = await _catalogService.GetLayoutsAsync(cancellationToken);
        var layout = layouts.FirstOrDefault(item => Same(item.LayoutId, layoutId));
        if (layout is null)
        {
            result.Messages.Add("Layout da chon khong ton tai.");
        }

        return layout;
    }

    private async Task<KeyboardCase?> FindCaseAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.CaseId))
        {
            result.Messages.Add("Chon case truoc.");
            return null;
        }

        var cases = await _catalogService.GetCasesForLayoutAsync(build.LayoutId, cancellationToken);
        var selectedCase = cases.FirstOrDefault(item => Same(item.CaseId, build.CaseId));
        if (selectedCase is null)
        {
            result.Messages.Add("Case da chon khong kha dung cho layout nay.");
        }

        return selectedCase;
    }

    private async Task<Pcb?> FindPcbAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.PcbId))
        {
            result.Messages.Add("Chon PCB truoc.");
            return null;
        }

        var pcbs = await _catalogService.GetPcbsForLayoutAsync(build.LayoutId, cancellationToken);
        var selectedPcb = pcbs.FirstOrDefault(item => Same(item.PcbId, build.PcbId));
        if (selectedPcb is null)
        {
            result.Messages.Add("PCB da chon khong kha dung cho layout nay.");
        }

        return selectedPcb;
    }

    private async Task<Plate?> FindPlateAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.PlateId))
        {
            result.Messages.Add("Chon plate truoc.");
            return null;
        }

        var plates = await _catalogService.GetPlatesForLayoutAsync(build.LayoutId, cancellationToken);
        var selectedPlate = plates.FirstOrDefault(item => Same(item.PlateId, build.PlateId));
        if (selectedPlate is null)
        {
            result.Messages.Add("Plate da chon khong kha dung cho layout nay.");
        }

        return selectedPlate;
    }

    private async Task<KeyboardSwitch?> FindSwitchAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.SwitchId))
        {
            result.Messages.Add("Chon switch truoc.");
            return null;
        }

        var switches = await _catalogService.GetAvailableSwitchesAsync(cancellationToken);
        var selectedSwitch = switches.FirstOrDefault(item => Same(item.SwitchId, build.SwitchId));
        if (selectedSwitch is null)
        {
            result.Messages.Add("Switch da chon khong kha dung.");
        }

        return selectedSwitch;
    }

    private async Task<KeycapSet?> FindKeycapAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.KeycapId))
        {
            result.Messages.Add("Chon keycap truoc.");
            return null;
        }

        var keycaps = await _catalogService.GetAvailableKeycapSetsAsync(cancellationToken);
        var selectedKeycap = keycaps.FirstOrDefault(item => Same(item.KeycapId, build.KeycapId));
        if (selectedKeycap is null)
        {
            result.Messages.Add("Keycap da chon khong kha dung.");
        }

        return selectedKeycap;
    }

    private async Task<Stabilizer?> FindStabilizerAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.StabilizerId))
        {
            result.Messages.Add("Chon stabilizer truoc.");
            return null;
        }

        var stabilizers = await _catalogService.GetAvailableStabilizersAsync(cancellationToken);
        var selectedStabilizer = stabilizers.FirstOrDefault(item => Same(item.StabilizerId, build.StabilizerId));
        if (selectedStabilizer is null)
        {
            result.Messages.Add("Stabilizer da chon khong kha dung.");
        }

        return selectedStabilizer;
    }

    private async Task ValidateMainComponentRuleAsync(
        KeyboardCase selectedCase,
        Pcb selectedPcb,
        Plate selectedPlate,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        var rules = await _catalogService.GetCompatibilityRulesAsync(cancellationToken);
        var rule = rules
            .Where(item =>
                MatchesRuleComponent(item.CaseId, selectedCase.CaseId)
                && MatchesRuleComponent(item.PcbId, selectedPcb.PcbId)
                && MatchesRuleComponent(item.PlateId, selectedPlate.PlateId))
            .OrderByDescending(RuleSpecificity)
            .FirstOrDefault();

        if (rule is null)
        {
            result.Messages.Add("Bo case/PCB/plate chua co rule tuong thich.");
            return;
        }

        if (!rule.IsCompatible)
        {
            result.Messages.Add(string.IsNullOrWhiteSpace(rule.Notes)
                ? "Bo case/PCB/plate khong tuong thich."
                : rule.Notes);
        }
    }

    private static decimal CalculateTotal(
        Layout layout,
        KeyboardCase? selectedCase,
        Pcb? selectedPcb,
        Plate? selectedPlate,
        KeyboardSwitch? selectedSwitch,
        KeycapSet? selectedKeycap,
        Stabilizer? selectedStabilizer)
    {
        var switchQuantity = Math.Max(layout.StandardKeyCount, 1);
        var total = 0m;

        total += selectedCase?.PriceUsd ?? 0m;
        total += selectedPcb?.PriceUsd ?? 0m;
        total += selectedPlate?.PriceUsd ?? 0m;
        total += selectedSwitch?.PriceUsd * switchQuantity ?? 0m;
        total += selectedKeycap?.PriceUsd ?? 0m;
        total += selectedStabilizer?.PriceUsd ?? 0m;

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static List<BuildMod> NormalizeMods(IEnumerable<BuildMod> mods)
    {
        return mods
            .Where(mod =>
                !string.IsNullOrWhiteSpace(mod.ModType)
                || !string.IsNullOrWhiteSpace(mod.TargetComponent)
                || !string.IsNullOrWhiteSpace(mod.LubeType)
                || mod.IsFilmed
                || mod.SpringWeightG.HasValue
                || !string.IsNullOrWhiteSpace(mod.Notes))
            .Select(mod => new BuildMod
            {
                ModId = mod.ModId,
                BuildId = mod.BuildId,
                ModType = string.IsNullOrWhiteSpace(mod.ModType) ? "General" : mod.ModType.Trim(),
                TargetComponent = string.IsNullOrWhiteSpace(mod.TargetComponent) ? "Build" : mod.TargetComponent.Trim(),
                LubeType = NormalizeNullable(mod.LubeType),
                IsFilmed = mod.IsFilmed,
                SpringWeightG = mod.SpringWeightG,
                Notes = NormalizeNullable(mod.Notes)
            })
            .ToList();
    }

    private static bool MatchesRuleComponent(string? ruleComponentId, string selectedComponentId)
    {
        return string.IsNullOrWhiteSpace(ruleComponentId) || Same(ruleComponentId, selectedComponentId);
    }

    private static int RuleSpecificity(CompatibilityRule rule)
    {
        var specificity = 0;

        if (!string.IsNullOrWhiteSpace(rule.CaseId))
        {
            specificity++;
        }

        if (!string.IsNullOrWhiteSpace(rule.PcbId))
        {
            specificity++;
        }

        if (!string.IsNullOrWhiteSpace(rule.PlateId))
        {
            specificity++;
        }

        return specificity;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
