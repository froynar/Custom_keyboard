namespace Custom_keyboard.Models.Components;

// Shared base for brand-owned catalog items: kits, switches, keycap sets, stabilizers.
// Accessories are standalone (no brand in the ERD) and do not derive from this type.
public abstract class CatalogItem
{
    public int BrandId { get; set; }
    public decimal PriceUsd { get; set; }
    public bool IsAvailable { get; set; } = true;
}
