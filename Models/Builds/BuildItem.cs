namespace Custom_keyboard.Models.Builds;

// One add-on line in a build. Exactly one product FK must be set
// (switch, keycap, stabilizer, or accessory). The kit itself lives on KeyboardBuild.KitId.
public sealed class BuildItem
{
    public int BuildItemId { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string? SwitchId { get; set; }
    public string? KeycapId { get; set; }
    public string? StabilizerId { get; set; }
    public string? AccessoryId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public string? Notes { get; set; }

    public bool HasExactlyOneProduct()
    {
        var count = 0;
        if (SwitchId is not null) count++;
        if (KeycapId is not null) count++;
        if (StabilizerId is not null) count++;
        if (AccessoryId is not null) count++;
        return count == 1;
    }
}
