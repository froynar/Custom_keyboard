namespace Custom_keyboard.Models.Components;

public sealed class KeyboardCase : KeyboardComponent
{
    public string CaseId { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string MountType { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int WeightG { get; set; }
}
