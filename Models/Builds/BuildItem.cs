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

    // Historical price captured when the build was saved. Financial totals must use
    // this value rather than CurrentPriceUsd, which can change with the catalog.
    public decimal UnitPriceSnapshot { get; set; }

    // Read-only projection fields populated by views.Build_items after persistence.
    public string ComponentType { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public decimal LineTotalSnapshot { get; set; }
    public decimal CurrentPriceUsd { get; set; }
    public bool IsAvailable { get; set; }

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
