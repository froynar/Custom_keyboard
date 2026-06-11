namespace Custom_keyboard.Models.Components;

public sealed class KeyboardSwitch : KeyboardComponent
{
    public string SwitchId { get; set; } = string.Empty;
    public string SwitchTechnology { get; set; } = string.Empty;
    public string SwitchType { get; set; } = string.Empty;
    public int ActuationForceG { get; set; }
    public string MountType { get; set; } = string.Empty;
    public string SoundProfile { get; set; } = string.Empty;
}
