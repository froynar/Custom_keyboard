namespace Custom_keyboard.Models.Builds;

public sealed class BuildMod
{
    public int ModId { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string ModType { get; set; } = string.Empty;          // Lube, Film, Spring_swap, Tape_mod, Foam_mod
    public string TargetComponent { get; set; } = string.Empty;  // Switch, Stabilizer, Kit, Build
    public string? Notes { get; set; }
}
