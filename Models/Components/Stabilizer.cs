namespace Custom_keyboard.Models.Components;

public sealed class Stabilizer : CatalogItem
{
    public string StabilizerId { get; set; } = string.Empty;
    public string StabilizerName { get; set; } = string.Empty;
    public string SupportedLayouts { get; set; } = string.Empty;   // 60/65/75/TKL/100 or universal
}
