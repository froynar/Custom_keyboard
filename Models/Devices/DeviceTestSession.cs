using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Devices;

// One compact QC test session for one build request.
// Only identity/configuration/status properties are persisted. Summary properties
// are calculated from device_key_test_results when the repository reads a session.
public sealed class DeviceTestSession
{
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string SwitchTechnology { get; set; } = string.Empty;
    public NoiseRequirement NoiseRequirement { get; set; } = NoiseRequirement.Normal;
    public int TotalKeys { get; set; }
    public TestSessionStatus Status { get; set; } = TestSessionStatus.Running;

    // Read-only projection values; not columns in device_test_sessions.
    public int TestedKeys { get; set; }
    public int PassedKeys { get; set; }
    public int WarningKeys { get; set; }
    public int FailedKeys { get; set; }
    public decimal? AverageLatencyMs { get; set; }
    public decimal? MaxLatencyMs { get; set; }
    public decimal? AverageNoiseDb { get; set; }
    public decimal? MaxNoiseDb { get; set; }
}
