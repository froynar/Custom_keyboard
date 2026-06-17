using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Devices;

// A seller's QC station (created on-demand, not seeded). Maps to table `devices`.
public sealed class Device
{
    public string DeviceId { get; set; } = string.Empty;
    public int SellerUserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; } = DeviceType.QC_STATION;
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
