namespace Custom_keyboard.Models.Components;

public sealed class Layout
{
    public string LayoutId { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty;
    public string FormFactor { get; set; } = string.Empty;
    public int KeyCount { get; set; }
}
