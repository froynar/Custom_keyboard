using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.ViewModels;

// A single mod line on a build. The refactor ERD keeps only mod_type, target_component, notes.
public sealed class BuildModEditorViewModel : ViewModelBase
{
    private string _modType = "Lube";
    private string _targetComponent = "Switch";
    private string? _notes;

    public string ModType
    {
        get => _modType;
        set => SetProperty(ref _modType, value);
    }

    public string TargetComponent
    {
        get => _targetComponent;
        set => SetProperty(ref _targetComponent, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public static string[] ModTypes { get; } = ["Lube", "Film", "Spring_swap", "Tape_mod", "Foam_mod"];
    public static string[] TargetComponents { get; } = ["Switch", "Stabilizer", "Kit", "Build"];

    public BuildMod ToBuildMod()
    {
        return new BuildMod
        {
            ModType = ModType,
            TargetComponent = TargetComponent,
            Notes = Notes
        };
    }
}
