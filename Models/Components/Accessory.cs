namespace Custom_keyboard.Models.Components;

// Lube, spring, film, foam, cable, tool... Stored as a build item, not on the build itself.
public sealed class Accessory
{
    public string AccessoryId { get; set; } = string.Empty;
    public string AccessoryType { get; set; } = string.Empty;   // Lube, Spring, Film, Foam, Cable, Tool
    public string AccessoryName { get; set; } = string.Empty;
    public string? TargetComponent { get; set; }                // Switch, Stabilizer, Kit, General
    public decimal PriceUsd { get; set; }
    public bool IsAvailable { get; set; } = true;
}
