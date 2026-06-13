using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.ViewModels;

// UI model for buyer mod presets. Persistence still uses build_mods(mod_type, target_component, notes).
public sealed partial class BuildModEditorViewModel : ViewModelBase
{
    public const int MinSpringWeightG = 30;
    public const int MaxSpringWeightG = 76;

    private const string SwitchTarget = "Switch";
    private const string StabilizerTarget = "Stabilizer";
    private const string BuildTarget = "Build";
    private const string KitTarget = "Kit";
    private const string SpringSwapType = "Spring_swap";

    private static readonly string[] SwitchModTypes = ["Lube", "Film", SpringSwapType];
    private static readonly string[] StabilizerModTypes = ["Lube", "Tune", "Holee_mod"];
    private static readonly string[] BuildModTypes = ["Tape_mod", "Foam_mod"];

    private string _modType = "Lube";
    private string _targetComponent = SwitchTarget;
    private string? _notes;
    private int _springWeightG = 63;
    private int _modQuantity = 1;
    private int _maxModQuantity = 1;

    public static string[] TargetComponents { get; } = [SwitchTarget, StabilizerTarget, BuildTarget];
    public static int[] SpringWeightOptions { get; } = Enumerable.Range(MinSpringWeightG, MaxSpringWeightG - MinSpringWeightG + 1).ToArray();

    public string ModType
    {
        get => _modType;
        set
        {
            if (SetProperty(ref _modType, Normalize(value, "Lube")))
            {
                if (IsSpringSwap && TargetComponent != SwitchTarget)
                {
                    TargetComponent = SwitchTarget;
                }

                RaiseDerivedStateChanged();
            }
        }
    }

    public string TargetComponent
    {
        get => _targetComponent;
        set
        {
            if (SetProperty(ref _targetComponent, Normalize(value, SwitchTarget)))
            {
                EnsureModTypeIsAllowed();
                RaiseDerivedStateChanged();
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        set
        {
            if (SetProperty(ref _notes, value))
            {
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public int SpringWeightG
    {
        get => _springWeightG;
        set
        {
            var clamped = Math.Clamp(value, MinSpringWeightG, MaxSpringWeightG);
            if (SetProperty(ref _springWeightG, clamped))
            {
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public int ModQuantity
    {
        get => _modQuantity;
        set
        {
            var min = MaxModQuantity == 0 ? 0 : 1;
            var clamped = Math.Clamp(value, min, MaxModQuantity);
            if (SetProperty(ref _modQuantity, clamped))
            {
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public int MaxModQuantity
    {
        get => _maxModQuantity;
        private set
        {
            var normalized = Math.Max(0, value);
            if (SetProperty(ref _maxModQuantity, normalized))
            {
                OnPropertyChanged(nameof(QuantityOptions));
                ModQuantity = Math.Min(ModQuantity, normalized);
            }
        }
    }

    public IReadOnlyList<string> AvailableModTypes => TargetComponent switch
    {
        SwitchTarget => SwitchModTypes,
        StabilizerTarget => StabilizerModTypes,
        BuildTarget or KitTarget => BuildModTypes,
        _ => SwitchModTypes
    };

    public int[] QuantityOptions => MaxModQuantity == 0
        ? [0]
        : Enumerable.Range(1, MaxModQuantity).ToArray();

    public bool IsSpringSwap => string.Equals(ModType, SpringSwapType, StringComparison.OrdinalIgnoreCase);
    public bool UsesSwitchQuantity => string.Equals(TargetComponent, SwitchTarget, StringComparison.OrdinalIgnoreCase);
    public Visibility SpringWeightVisibility => IsSpringSwap ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SwitchQuantityVisibility => UsesSwitchQuantity ? Visibility.Visible : Visibility.Collapsed;

    public string DisplayTitle => $"{LocalizeToken(TargetComponent)} - {LocalizeToken(ModType)}";

    public string DisplaySummary
    {
        get
        {
            var parts = new List<string>();
            if (UsesSwitchQuantity)
            {
                parts.Add(TrFormat("Buyer_ModSwitchQuantitySummary", ModQuantity));
            }

            if (IsSpringSwap)
            {
                parts.Add($"{SpringWeightG}g");
            }

            if (!string.IsNullOrWhiteSpace(Notes))
            {
                parts.Add(Notes.Trim());
            }

            return parts.Count == 0 ? Tr("Buyer_ModNoNotes") : string.Join(" - ", parts);
        }
    }

    public static BuildModEditorViewModel CreatePreset(string targetComponent, string modType, int switchQuantity)
    {
        var mod = new BuildModEditorViewModel();
        mod.SetSwitchQuantityLimit(switchQuantity);
        mod.TargetComponent = targetComponent;
        mod.ModType = modType;
        if (mod.UsesSwitchQuantity)
        {
            mod.ModQuantity = Math.Max(1, switchQuantity);
        }

        return mod;
    }

    public static BuildModEditorViewModel FromBuildMod(BuildMod source, int switchQuantity)
    {
        var mod = CreatePreset(
            string.IsNullOrWhiteSpace(source.TargetComponent) ? SwitchTarget : source.TargetComponent,
            string.IsNullOrWhiteSpace(source.ModType) ? "Lube" : source.ModType,
            switchQuantity);

        mod.ApplyPersistedNotes(source.Notes);
        return mod;
    }

    public void SetSwitchQuantityLimit(int switchQuantity)
    {
        MaxModQuantity = Math.Max(0, switchQuantity);
        if (!UsesSwitchQuantity)
        {
            return;
        }

        if (MaxModQuantity > 0 && ModQuantity == 0)
        {
            ModQuantity = 1;
        }
        else if (ModQuantity > MaxModQuantity)
        {
            ModQuantity = MaxModQuantity;
        }
    }

    public BuildMod ToBuildMod()
    {
        return new BuildMod
        {
            ModType = ModType,
            TargetComponent = TargetComponent,
            Notes = ComposePersistedNotes()
        };
    }

    private void EnsureModTypeIsAllowed()
    {
        if (!AvailableModTypes.Contains(ModType, StringComparer.OrdinalIgnoreCase))
        {
            ModType = AvailableModTypes[0];
        }
    }

    private string? ComposePersistedNotes()
    {
        var parts = new List<string>();
        if (UsesSwitchQuantity)
        {
            parts.Add($"Quantity: {ModQuantity} switches");
        }

        if (IsSpringSwap)
        {
            parts.Add($"Spring weight: {SpringWeightG}g");
        }

        if (!string.IsNullOrWhiteSpace(Notes))
        {
            parts.Add(Notes.Trim());
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private void ApplyPersistedNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            Notes = null;
            return;
        }

        var remaining = notes.Trim();
        var quantityMatch = QuantityRegex().Match(remaining);
        if (quantityMatch.Success && int.TryParse(quantityMatch.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            ModQuantity = quantity;
            remaining = RemoveMatch(remaining, quantityMatch);
        }

        var springMatch = SpringWeightRegex().Match(remaining);
        if (springMatch.Success && int.TryParse(springMatch.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var springWeight))
        {
            SpringWeightG = springWeight;
            remaining = RemoveMatch(remaining, springMatch);
        }

        Notes = string.IsNullOrWhiteSpace(remaining) ? null : remaining.Trim(' ', ';', '-');
    }

    private void RaiseDerivedStateChanged()
    {
        OnPropertyChanged(nameof(AvailableModTypes));
        OnPropertyChanged(nameof(IsSpringSwap));
        OnPropertyChanged(nameof(UsesSwitchQuantity));
        OnPropertyChanged(nameof(SpringWeightVisibility));
        OnPropertyChanged(nameof(SwitchQuantityVisibility));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplaySummary));
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string LocalizeToken(string value)
    {
        var key = value.Trim() switch
        {
            SwitchTarget => "Buyer_ModTargetSwitch",
            StabilizerTarget => "Buyer_ModTargetStabilizer",
            BuildTarget => "Buyer_ModTargetBuild",
            KitTarget => "Buyer_ModTargetKit",
            "Lube" => "Buyer_ModTypeLube",
            "Film" => "Buyer_ModTypeFilm",
            SpringSwapType => "Buyer_ModTypeSpring_swap",
            "Tune" => "Buyer_ModTypeTune",
            "Holee_mod" => "Buyer_ModTypeHolee_mod",
            "Tape_mod" => "Buyer_ModTypeTape_mod",
            "Foam_mod" => "Buyer_ModTypeFoam_mod",
            _ => null
        };

        return key is null ? value.Replace('_', ' ') : Tr(key);
    }

    private static string RemoveMatch(string value, Match match)
    {
        var updated = value.Remove(match.Index, match.Length);
        return Regex.Replace(updated, @"\s*;\s*;\s*", "; ").Trim(' ', ';');
    }

    [GeneratedRegex(@"(?:^|;\s*)Quantity:\s*(?<value>\d+)\s*switch(?:es)?", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityRegex();

    [GeneratedRegex(@"(?:^|;\s*)Spring\s*weight:\s*(?<value>\d+)\s*g", RegexOptions.IgnoreCase)]
    private static partial Regex SpringWeightRegex();
}
