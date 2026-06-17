using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Devices;

// One QC test session for one build request. Maps to table `device_test_sessions`.
// Created as Running on StartSession, then aggregated/updated on CompleteSession.
public sealed class DeviceTestSession
{
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public int SellerUserId { get; set; }
    public string SwitchTechnology { get; set; } = string.Empty;   // Mechanical / HE (from kit.pcbTechnology); NOT NULL per DBML
    public NoiseRequirement NoiseRequirement { get; set; } = NoiseRequirement.Normal;
    public int TotalKeys { get; set; }
    public int TestedKeys { get; set; }
    public int PassedKeys { get; set; }
    public int WarningKeys { get; set; }
    public int FailedKeys { get; set; }
    public decimal? AverageLatencyMs { get; set; }
    public decimal? MaxLatencyMs { get; set; }
    public decimal? AverageNoiseDb { get; set; }
    public decimal? MaxNoiseDb { get; set; }
    public TestSessionStatus Status { get; set; } = TestSessionStatus.Running;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
