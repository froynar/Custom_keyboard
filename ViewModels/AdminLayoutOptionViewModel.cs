using Custom_keyboard.Models.Components;

namespace Custom_keyboard.ViewModels;

public sealed class AdminLayoutOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    public AdminLayoutOptionViewModel(Layout layout, bool isSelected)
    {
        LayoutId = layout.LayoutId;
        LayoutName = layout.LayoutName;
        FormFactor = layout.FormFactor;
        IsSelected = isSelected;
    }

    public string LayoutId { get; }
    public string LayoutName { get; }
    public string FormFactor { get; }
    public string DisplayName => $"{LayoutName} ({LayoutId})";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
