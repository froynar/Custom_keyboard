using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Builds;

public sealed class KeyboardBuild
{
    public string BuildId { get; set; } = string.Empty;
    public int BuyerId { get; set; }
    public string KitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public NoiseRequirement NoiseRequirement { get; set; } = NoiseRequirement.Normal;
    public BuildStatus Status { get; set; } = BuildStatus.Draft;
    public decimal TotalCostSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<BuildItem> Items { get; set; } = [];
    public List<BuildMod> Mods { get; set; } = [];
}
