namespace Custom_keyboard.Models.Admin;

// Flat DTO used by the admin catalog editor. Holds the superset of fields across the
// five editable catalog types (Kit, Switch, KeycapSet, Stabilizer, Accessory); only the
// fields relevant to ComponentType are populated/persisted.
public sealed class AdminComponentRecord
{
    public AdminComponentType ComponentType { get; set; }
    public string ComponentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;          // kit/switch/keycap/stab/accessory name
    public int BrandId { get; set; }                          // not used for Accessory (no brand in ERD)
    public decimal PriceUsd { get; set; }
    public bool IsAvailable { get; set; } = true;

    // Kit
    public string LayoutId { get; set; } = string.Empty;
    public string PcbTechnology { get; set; } = string.Empty;
    public string SwitchMount { get; set; } = string.Empty;
    public int RequiredSwitchQuantity { get; set; }
    public string IncludedParts { get; set; } = string.Empty;

    // Switch
    public string SwitchTechnology { get; set; } = string.Empty;
    public string MountType { get; set; } = string.Empty;
    public string SwitchType { get; set; } = string.Empty;
    public int? ActuationForceG { get; set; }

    // KeycapSet
    public string SupportedFormFactor { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;

    // Stabilizer
    public string SupportedLayouts { get; set; } = string.Empty;

    // Accessory
    public string AccessoryType { get; set; } = string.Empty;
    public string TargetComponent { get; set; } = string.Empty;

    public AdminComponentRecord Clone()
    {
        return (AdminComponentRecord)MemberwiseClone();
    }
}
