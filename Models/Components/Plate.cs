namespace Custom_keyboard.Models.Components;

public sealed class Plate : KeyboardComponent
{
    public string PlateId { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string MountType { get; set; } = string.Empty;
    public string FlexCut { get; set; } = string.Empty;
}
