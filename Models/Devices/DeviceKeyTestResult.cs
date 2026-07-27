using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Devices;

// Compact per-key QC result. Failure diagnostics are derived in DeviceService
// when rows are read; they are not persisted by the concise ERD.
public sealed class DeviceKeyTestResult
{
    public long KeyTestId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string KeyCode { get; set; } = string.Empty;
    public string? ReceivedKey { get; set; }
    public bool PressSignalDetected { get; set; }
    public decimal? Latency { get; set; }
    public int PressCount { get; set; }
    public bool ReleaseSignal { get; set; }
    public int? HoldDuration { get; set; }
    public decimal? Noise { get; set; }
    public KeyTestResult Result { get; set; }
    public DateTime RecordedAt { get; set; }

    // Transient dashboard diagnostics reconstructed from the compact row.
    public KeyFailureType? FailureType { get; set; }
    public string? FailureReason { get; set; }
}
