using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services.Devices;

// Inputs to DeviceQcRules. SwitchTechnology is always real (from kit.pcbTechnology);
// NoiseRequirement and StuckThresholdMs carry phase-1 defaults until the buyer
// requirement UI/schema exists (plan §6.1). The Evaluate signature does not change
// when real requirements arrive later — only these values do.
public sealed record QcThresholds(
    string SwitchTechnology,
    NoiseRequirement NoiseRequirement = NoiseRequirement.Normal,
    int StuckThresholdMs = 1000);
