namespace Custom_keyboard.Services;

// Result of validating a kit-based build.
// Errors block save/request; warnings are advisory; infos are calculated facts (total, qty).
public sealed class BuildValidationResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Infos { get; } = [];

    public bool IsValid => Errors.Count == 0;
    public decimal TotalCost { get; set; }
    public int RequiredSwitchQuantity { get; set; }

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddInfo(string message) => Infos.Add(message);

    // Flattened view for callers that just need the blocking reasons (e.g. request flow).
    public IEnumerable<string> Messages => Errors;
}
