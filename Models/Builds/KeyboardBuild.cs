using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Builds;

public sealed class KeyboardBuild
{
    public string BuildId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string LayoutId { get; set; } = string.Empty;
    public string? CaseId { get; set; }
    public string? PcbId { get; set; }
    public string? PlateId { get; set; }
    public string? SwitchId { get; set; }
    public string? KeycapId { get; set; }
    public string? StabilizerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public BuildStatus Status { get; set; } = BuildStatus.Draft;
    public decimal TotalCostSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<BuildMod> Mods { get; set; } = [];
}
