namespace Custom_keyboard.Models.Components;

public sealed class Pcb : KeyboardComponent
{
    public string PcbId { get; set; } = string.Empty;
    public string PcbTechnology { get; set; } = string.Empty;
    public string MountType { get; set; } = string.Empty;
    public bool Hotswap { get; set; }
    public bool Wireless { get; set; }
    public bool Rgb { get; set; }
    public string SwitchMount { get; set; } = string.Empty;
}
