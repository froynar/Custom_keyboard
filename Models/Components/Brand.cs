namespace Custom_keyboard.Models.Components;

public sealed class Brand
{
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? Country { get; set; }
}
