namespace Custom_keyboard.Models.Components;

public sealed class KeyboardSwitch : CatalogItem
{
    public string SwitchId { get; set; } = string.Empty;
    public string SwitchName { get; set; } = string.Empty;
    public string SwitchTechnology { get; set; } = string.Empty;   // Mechanical, HE, Topre, Optical
    public string MountType { get; set; } = string.Empty;          // MX 3-pin, MX 5-pin, HE, Topre, Optical
    public string? SwitchType { get; set; }                        // Linear, Tactile, Clicky...
    public int? ActuationForceG { get; set; }
}
