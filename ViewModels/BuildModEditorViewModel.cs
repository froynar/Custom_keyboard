using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.ViewModels;

public sealed class BuildModEditorViewModel : ViewModelBase
{
    private string _modType = "Lube";
    private string _targetComponent = "Switch";
    private string? _lubeType;
    private bool _isFilmed;
    private int? _springWeightG;
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

    public string? LubeType
    {
        get => _lubeType;
        set => SetProperty(ref _lubeType, value);
    }

    public bool IsFilmed
    {
        get => _isFilmed;
        set => SetProperty(ref _isFilmed, value);
    }

    public int? SpringWeightG
    {
        get => _springWeightG;
        set => SetProperty(ref _springWeightG, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public BuildMod ToBuildMod()
    {
        return new BuildMod
        {
            ModType = ModType,
            TargetComponent = TargetComponent,
            LubeType = LubeType,
            IsFilmed = IsFilmed,
            SpringWeightG = SpringWeightG,
            Notes = Notes
        };
    }
}
