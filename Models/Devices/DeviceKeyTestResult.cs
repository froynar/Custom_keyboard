using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Devices;

// Per-key QC result (the detail table). Maps to `device_key_test_results`.
// request_id/device_id are copied from the owning session by the repository to keep FKs aligned.
public sealed class DeviceKeyTestResult
{
    public long KeyTestId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string KeyCode { get; set; } = string.Empty;
    public string ExpectedKey { get; set; } = string.Empty;
    public string? ReceivedKey { get; set; }
    public bool PressSignalDetected { get; set; }
    public decimal? LatencyMs { get; set; }
    public int PressEventCount { get; set; }
    public int? BounceCount { get; set; }            // null when no full press-release cycle (e.g. StuckKey)
    public bool ReleaseSignalDetected { get; set; }
    public int? HoldDurationMs { get; set; }
    public bool IsStuck { get; set; }
    public decimal? NoiseDb { get; set; }
    public string SwitchTechnology { get; set; } = string.Empty;   // NOT NULL; denormalized from session
    public KeyTestResult Result { get; set; }
    // Primary failure only: when a key trips several rules, DeviceQcRules picks one
    // by §6.1 priority (StuckKey > Chatter > ...). Null when Result is Pass/Warning.
    public KeyFailureType? FailureType { get; set; }
    public string? FailureReason { get; set; }
    public DateTime RecordedAt { get; set; }
}
