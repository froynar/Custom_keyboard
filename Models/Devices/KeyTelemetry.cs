namespace Custom_keyboard.Models.Devices;

// Transport DTO the simulator publishes (MQTT or in-process). Not persisted.
// It is the INPUT to DeviceQcRules; Result/FailureType are deliberately absent
// because the rule engine decides those from these raw signals.
public sealed class KeyTelemetry
{
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string KeyCode { get; set; } = string.Empty;
    public string ExpectedKey { get; set; } = string.Empty;
    public string? ReceivedKey { get; set; }
    public bool PressSignalDetected { get; set; }
    public decimal? LatencyMs { get; set; }
    public int PressEventCount { get; set; }
    public int? BounceCount { get; set; }
    public bool ReleaseSignalDetected { get; set; }
    public int? HoldDurationMs { get; set; }
    public bool IsStuck { get; set; }
    public decimal? NoiseDb { get; set; }
    public string SwitchTechnology { get; set; } = string.Empty;   // always set by simulator; feeds NOT NULL per-key column
}
