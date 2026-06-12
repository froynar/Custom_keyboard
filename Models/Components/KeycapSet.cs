namespace Custom_keyboard.Models.Components;

public sealed class KeycapSet : CatalogItem
{
    public string KeycapId { get; set; } = string.Empty;
    public string KeycapName { get; set; } = string.Empty;
    public string SupportedFormFactor { get; set; } = string.Empty;   // 60/65/75/TKL/100/universal notes
    public string? Profile { get; set; }
    public string? Material { get; set; }
}
