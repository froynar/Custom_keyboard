using System.Globalization;
using System.Text.RegularExpressions;
using Custom_keyboard.Localization;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

// Kit-based build logic. A build is a kit (builds.kit_id) plus add-on lines (build_items),
// each line carrying exactly one product FK. Total = kit price + Σ(item.qty × unit price).
// Validation follows Documents_Refactor/Keyboard_Build_Validation_Logic.md.
public sealed class BuildService : IBuildService
{
    private const int NotesMaxLength = 500;
    private static readonly Regex ModQuantityRegex = new(
        @"(?:^|;\s*)Quantity:\s*(?<value>\d+)\s*switch(?:es)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IBuildRepository _buildRepository;
    private readonly IComponentCatalogService _catalogService;

    public BuildService(IBuildRepository buildRepository, IComponentCatalogService catalogService)
    {
        _buildRepository = buildRepository;
        _catalogService = catalogService;
    }

    public Task<IReadOnlyList<KeyboardBuild>> GetBuyerBuildsAsync(int buyerId, CancellationToken cancellationToken = default)
        => _buildRepository.GetByBuyerAsync(buyerId, cancellationToken);

    public Task<KeyboardBuild?> GetBuildByIdAsync(string buildId, CancellationToken cancellationToken = default)
        => _buildRepository.GetByIdAsync(buildId, cancellationToken);

    public async Task<KeyboardBuild> SaveBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(build, new BuildValidationResult(), cancellationToken);
        if (!resolution.Result.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, resolution.Result.Errors));
        }

        build.Name = build.Name.Trim();
        build.Notes = NormalizeNullable(build.Notes);
        build.KitId = build.KitId.Trim();
        build.Mods = NormalizeMods(build.Mods);
        ApplySnapshots(build, resolution);

        // A build that the buyer is saving is no longer a transient draft once it is complete.
        if (build.Status == BuildStatus.Draft && resolution.Result.Warnings.Count == 0)
        {
            build.Status = BuildStatus.Saved;
        }

        return await _buildRepository.SaveAsync(build, cancellationToken);
    }

    public async Task ArchiveBuildAsync(string buildId, int buyerId, CancellationToken cancellationToken = default)
    {
        var build = await _buildRepository.GetByIdAsync(buildId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Build_NotExist"]);

        if (build.BuyerId != buyerId)
        {
            throw new InvalidOperationException(Loc.Instance["Build_NotOwned"]);
        }

        await _buildRepository.ArchiveAsync(buildId, cancellationToken);
    }

    public async Task<decimal> CalculateTotalAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(build, new BuildValidationResult(), cancellationToken);
        return resolution.Result.TotalCost;
    }

    public async Task<BuildValidationResult> ValidateBuildAsync(KeyboardBuild build, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(build, new BuildValidationResult(), cancellationToken);
        return resolution.Result;
    }

    // ------------------------------------------------------------------ Resolution
    // Loads the kit + every referenced product from the catalog, validates the build,
    // computes the total and the per-item unit price snapshot in one pass.
    private async Task<BuildResolution> ResolveAsync(
        KeyboardBuild build,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        // 1. Metadata
        if (build.BuyerId <= 0)
        {
            result.AddError(Loc.Instance["Build_InvalidBuyer"]);
        }

        if (string.IsNullOrWhiteSpace(build.Name))
        {
            result.AddError(Loc.Instance["Build_NameRequired"]);
        }

        if (build.Notes is { Length: > NotesMaxLength })
        {
            result.AddError(Loc.Instance.Format("Build_NotesTooLong", NotesMaxLength));
        }

        // 2. Kit
        var kit = await ResolveKitAsync(build.KitId, result, cancellationToken);
        var layout = kit is null
            ? null
            : await _catalogService.GetLayoutByIdAsync(kit.LayoutId, cancellationToken);
        if (kit is not null && layout is null)
        {
            result.AddWarning(Loc.Instance["Build_LayoutMissing"]);
        }

        var requiredSwitchQuantity = kit?.RequiredSwitchQuantity ?? 0;
        result.RequiredSwitchQuantity = requiredSwitchQuantity;

        // 3-6. Build items
        var resolvedItems = new List<ResolvedItem>(build.Items.Count);
        var total = kit?.PriceUsd ?? 0m;
        var switchQuantityTotal = 0;
        var hasKeycap = false;
        var hasStabilizer = false;

        foreach (var item in build.Items)
        {
            if (!item.HasExactlyOneProduct())
            {
                result.AddError(Loc.Instance["Build_ItemOneProduct"]);
                resolvedItems.Add(new ResolvedItem(item, 0m));
                continue;
            }

            if (item.Quantity <= 0)
            {
                result.AddError(Loc.Instance["Build_ItemQuantityPositive"]);
            }

            var unitPrice = await ResolveItemAsync(
                item,
                kit,
                layout,
                result,
                quantity => switchQuantityTotal += quantity,
                () => hasKeycap = true,
                () => hasStabilizer = true,
                cancellationToken);

            resolvedItems.Add(new ResolvedItem(item, unitPrice));
            total += item.Quantity * unitPrice;
        }

        // 3. Switch quantity
        if (kit is not null && requiredSwitchQuantity > 0 && switchQuantityTotal < requiredSwitchQuantity)
        {
            result.AddError(
                Loc.Instance.Format("Build_SwitchQuantityShort", requiredSwitchQuantity, switchQuantityTotal));
        }

        ValidateSwitchModQuantities(build.Mods, switchQuantityTotal, result);

        // 4-5. Completeness advisories
        if (kit is not null && !hasKeycap)
        {
            result.AddWarning(Loc.Instance["Build_NoKeycap"]);
        }

        if (kit is not null && !hasStabilizer)
        {
            result.AddWarning(Loc.Instance["Build_NoStabilizer"]);
        }

        // 8. Price snapshot
        total = Math.Round(total, 2, MidpointRounding.AwayFromZero);
        result.TotalCost = total;
        if (kit is not null)
        {
            result.AddInfo(Loc.Instance.Format("Build_TotalInfo", total));
            result.AddInfo(Loc.Instance.Format("Build_SwitchToBuyInfo", requiredSwitchQuantity));
            if (!string.IsNullOrWhiteSpace(kit.IncludedParts))
            {
                result.AddInfo(Loc.Instance.Format("Build_KitIncludesInfo", kit.IncludedParts));
            }
        }

        return new BuildResolution(result, kit, total, resolvedItems);
    }

    private async Task<KeyboardKit?> ResolveKitAsync(
        string kitId,
        BuildValidationResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(kitId))
        {
            result.AddError(Loc.Instance["Build_KitRequired"]);
            return null;
        }

        var kit = await _catalogService.GetKitByIdAsync(kitId.Trim(), cancellationToken);
        if (kit is null)
        {
            result.AddError(Loc.Instance["Build_KitNotExist"]);
            return null;
        }

        if (!kit.IsAvailable)
        {
            result.AddError(Loc.Instance["Build_KitUnavailable"]);
        }

        if (string.IsNullOrWhiteSpace(kit.PcbTechnology) || string.IsNullOrWhiteSpace(kit.SwitchMount))
        {
            result.AddError(Loc.Instance["Build_KitMissingInfo"]);
        }

        if (kit.PriceUsd < 0)
        {
            result.AddError(Loc.Instance["Build_KitPriceInvalid"]);
        }

        return kit;
    }

    private async Task<decimal> ResolveItemAsync(
        BuildItem item,
        KeyboardKit? kit,
        Layout? layout,
        BuildValidationResult result,
        Action<int> recordSwitchQuantity,
        Action recordKeycap,
        Action recordStabilizer,
        CancellationToken cancellationToken)
    {
        if (item.SwitchId is not null)
        {
            var selectedSwitch = await _catalogService.GetSwitchByIdAsync(item.SwitchId, cancellationToken);
            if (selectedSwitch is null)
            {
                result.AddError(Loc.Instance.Format("Build_SwitchNotExist", item.SwitchId));
                return 0m;
            }

            if (!selectedSwitch.IsAvailable)
            {
                result.AddError(Loc.Instance.Format("Build_SwitchUnavailable", selectedSwitch.SwitchName));
            }

            if (kit is not null)
            {
                if (!Same(selectedSwitch.SwitchTechnology, kit.PcbTechnology))
                {
                    result.AddError(
                        Loc.Instance.Format("Build_SwitchTechMismatch", selectedSwitch.SwitchTechnology, kit.PcbTechnology));
                }

                if (!Same(selectedSwitch.MountType, kit.SwitchMount))
                {
                    result.AddError(
                        Loc.Instance.Format("Build_SwitchMountMismatch", selectedSwitch.MountType, kit.SwitchMount));
                }
            }

            recordSwitchQuantity(item.Quantity);
            return selectedSwitch.PriceUsd;
        }

        if (item.KeycapId is not null)
        {
            var keycap = await _catalogService.GetKeycapSetByIdAsync(item.KeycapId, cancellationToken);
            if (keycap is null)
            {
                result.AddError(Loc.Instance.Format("Build_KeycapNotExist", item.KeycapId));
                return 0m;
            }

            if (!keycap.IsAvailable)
            {
                result.AddError(Loc.Instance.Format("Build_KeycapUnavailable", keycap.KeycapName));
            }

            if (layout is not null && !SupportsFormFactor(keycap.SupportedFormFactor, layout.FormFactor))
            {
                result.AddWarning(
                    Loc.Instance.Format("Build_KeycapFormFactorWarn", keycap.KeycapName, layout.FormFactor));
            }

            recordKeycap();
            return keycap.PriceUsd;
        }

        if (item.StabilizerId is not null)
        {
            var stabilizer = await _catalogService.GetStabilizerByIdAsync(item.StabilizerId, cancellationToken);
            if (stabilizer is null)
            {
                result.AddError(Loc.Instance.Format("Build_StabilizerNotExist", item.StabilizerId));
                return 0m;
            }

            if (!stabilizer.IsAvailable)
            {
                result.AddError(Loc.Instance.Format("Build_StabilizerUnavailable", stabilizer.StabilizerName));
            }

            if (layout is not null && !SupportsFormFactor(stabilizer.SupportedLayouts, layout.FormFactor))
            {
                result.AddWarning(
                    Loc.Instance.Format("Build_StabilizerLayoutWarn", stabilizer.StabilizerName, layout.FormFactor));
            }

            recordStabilizer();
            return stabilizer.PriceUsd;
        }

        // accessory
        var accessory = await _catalogService.GetAccessoryByIdAsync(item.AccessoryId!, cancellationToken);
        if (accessory is null)
        {
            result.AddError(Loc.Instance.Format("Build_AccessoryNotExist", item.AccessoryId));
            return 0m;
        }

        if (!accessory.IsAvailable)
        {
            result.AddError(Loc.Instance.Format("Build_AccessoryUnavailable", accessory.AccessoryName));
        }

        if (!IsValidAccessoryTarget(accessory.TargetComponent))
        {
            result.AddWarning(Loc.Instance.Format("Build_AccessoryTargetUnknown", accessory.AccessoryName));
        }

        return accessory.PriceUsd;
    }

    private static void ApplySnapshots(KeyboardBuild build, BuildResolution resolution)
    {
        foreach (var resolved in resolution.Items)
        {
            resolved.Item.UnitPriceSnapshot = resolved.UnitPrice;
        }

        build.TotalCostSnapshot = resolution.Total;
    }

    private static List<BuildMod> NormalizeMods(IEnumerable<BuildMod> mods)
    {
        return mods
            .Where(mod =>
                !string.IsNullOrWhiteSpace(mod.ModType)
                || !string.IsNullOrWhiteSpace(mod.TargetComponent)
                || !string.IsNullOrWhiteSpace(mod.Notes))
            .Select(mod => new BuildMod
            {
                ModId = mod.ModId,
                BuildId = mod.BuildId,
                ModType = string.IsNullOrWhiteSpace(mod.ModType) ? "General" : mod.ModType.Trim(),
                TargetComponent = string.IsNullOrWhiteSpace(mod.TargetComponent) ? "Build" : mod.TargetComponent.Trim(),
                Notes = NormalizeNullable(mod.Notes)
            })
            .ToList();
    }

    private static void ValidateSwitchModQuantities(
        IEnumerable<BuildMod> mods,
        int selectedSwitchQuantity,
        BuildValidationResult result)
    {
        if (selectedSwitchQuantity <= 0)
        {
            return;
        }

        var switchModQuantityByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods.Where(mod => Same(mod.TargetComponent, "Switch")))
        {
            var modType = string.IsNullOrWhiteSpace(mod.ModType) ? "General" : mod.ModType.Trim();
            var quantity = TryReadSwitchModQuantity(mod.Notes) ?? selectedSwitchQuantity;
            switchModQuantityByType[modType] = switchModQuantityByType.GetValueOrDefault(modType) + quantity;
        }

        foreach (var (modType, quantity) in switchModQuantityByType)
        {
            if (quantity > selectedSwitchQuantity)
            {
                result.AddError(
                    Loc.Instance.Format("Build_ModSwitchExceed", modType, quantity, selectedSwitchQuantity));
            }
        }
    }

    private static int? TryReadSwitchModQuantity(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var match = ModQuantityRegex.Match(notes);
        return match.Success
               && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            ? quantity
            : null;
    }

    private static bool IsValidAccessoryTarget(string? targetComponent)
    {
        return targetComponent is not null
            && (Same(targetComponent, "Switch")
                || Same(targetComponent, "Stabilizer")
                || Same(targetComponent, "Kit")
                || Same(targetComponent, "General"));
    }

    // Keycap/stab compatibility is text-based in the refactor ERD. The supported field lists
    // form-factor tokens like "60/65/75/TKL/100"; a kit's layout form factor is "65%", "TKL"...
    private static bool SupportsFormFactor(string? supported, string layoutFormFactor)
    {
        if (string.IsNullOrWhiteSpace(supported))
        {
            return false;
        }

        var target = NormalizeFormFactor(layoutFormFactor);
        if (target.Length == 0)
        {
            return true;
        }

        foreach (var token in supported.Split(['/', ',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = NormalizeFormFactor(token);
            if (normalized == "UNIVERSAL" || normalized == target)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeFormFactor(string value)
        => value.Trim().Trim('%').ToUpperInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record BuildResolution(
        BuildValidationResult Result,
        KeyboardKit? Kit,
        decimal Total,
        IReadOnlyList<ResolvedItem> Items);

    private sealed record ResolvedItem(BuildItem Item, decimal UnitPrice);
}
