namespace Custom_keyboard.Models.Builds;

public sealed class BuildMod
{
    public int ModId { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string ModType { get; set; } = string.Empty;
    public string TargetComponent { get; set; } = string.Empty;
    public string? LubeType { get; set; }
    public bool IsFilmed { get; set; }
    public int? SpringWeightG { get; set; }
    public string? Notes { get; set; }
}
