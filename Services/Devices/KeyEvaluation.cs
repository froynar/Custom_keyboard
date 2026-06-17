using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services.Devices;

// Output of DeviceQcRules for one key: verdict + (optional) primary failure + human-readable reason.
// FailureType/Reason are null on a clean Pass.
public readonly record struct KeyEvaluation(KeyTestResult Result, KeyFailureType? FailureType, string? Reason)
{
    public static KeyEvaluation Pass { get; } = new(KeyTestResult.Pass, null, null);
}
