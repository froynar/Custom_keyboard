namespace Custom_keyboard.Models.Enums;

// Buyer's noise expectation. Phase 1 always uses Normal (there is no buyer requirement UI yet);
// Silent/Quiet activate when that UI/schema lands (plan §3.5/§6.1). Not persisted in this phase.
public enum NoiseRequirement
{
    Silent,
    Quiet,
    Normal
}
