namespace Custom_keyboard.Models.Components;

public abstract class KeyboardComponent
{
    public int BrandId { get; set; }
    public decimal PriceUsd { get; set; }
    public bool IsAvailable { get; set; } = true;
}
