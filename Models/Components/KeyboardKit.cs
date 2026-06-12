namespace Custom_keyboard.Models.Components;

// Base product of a build: a kit already bundles case/PCB/plate/foam/cable via IncludedParts.
public sealed class KeyboardKit : CatalogItem
{
    public string KitId { get; set; } = string.Empty;
    public string LayoutId { get; set; } = string.Empty;
    public string KitName { get; set; } = string.Empty;
    public string PcbTechnology { get; set; } = string.Empty;   // Mechanical, HE, Topre, Optical
    public string SwitchMount { get; set; } = string.Empty;     // MX 3-pin, MX 5-pin, HE, Topre, Optical
    public int RequiredSwitchQuantity { get; set; }
    public string? IncludedParts { get; set; }
}
