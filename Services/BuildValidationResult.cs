namespace Custom_keyboard.Services;

public sealed class BuildValidationResult
{
    public bool IsValid => Messages.Count == 0;
    public decimal TotalCost { get; set; }
    public List<string> Messages { get; } = [];
}
