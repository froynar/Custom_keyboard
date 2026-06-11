namespace Custom_keyboard.Models.Admin;

public sealed class AdminComponentRecord
{
    public AdminComponentType ComponentType { get; set; }
    public string ComponentId { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public decimal PriceUsd { get; set; }
    public bool IsAvailable { get; set; } = true;

    public string Material { get; set; } = string.Empty;
    public string MountType { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int WeightG { get; set; }

    public string Technology { get; set; } = string.Empty;
    public bool Hotswap { get; set; }
    public bool Wireless { get; set; }
    public bool Rgb { get; set; }
    public string SwitchMount { get; set; } = string.Empty;

    public string FlexCut { get; set; } = string.Empty;

    public string SwitchType { get; set; } = string.Empty;
    public int ActuationForceG { get; set; }
    public string SoundProfile { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;
    public string ColorPrimary { get; set; } = string.Empty;
    public string LegendType { get; set; } = string.Empty;

    public string StabilizerType { get; set; } = string.Empty;
    public string SizesIncluded { get; set; } = string.Empty;

    public List<string> SupportedLayoutIds { get; set; } = [];
    public string PrimaryLayoutId { get; set; } = string.Empty;
    public string PcbVariantName { get; set; } = string.Empty;

    public AdminComponentRecord Clone()
    {
        var clone = (AdminComponentRecord)MemberwiseClone();
        clone.SupportedLayoutIds = [.. SupportedLayoutIds];
        return clone;
    }
}
