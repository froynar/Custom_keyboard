namespace Custom_keyboard.Models.Components;

public sealed class CompatibilityRule
{
    public int RuleId { get; set; }
    public string? CaseId { get; set; }
    public string? PcbId { get; set; }
    public string? PlateId { get; set; }
    public bool IsCompatible { get; set; }
    public string? Notes { get; set; }
}
