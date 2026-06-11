namespace Custom_keyboard.Models.Components;

public sealed class KeycapSet : KeyboardComponent
{
    public string KeycapId { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string ColorPrimary { get; set; } = string.Empty;
    public string LegendType { get; set; } = string.Empty;
}
