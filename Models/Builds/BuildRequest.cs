using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Builds;

public sealed class BuildRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string BuildId { get; set; } = string.Empty;
    public int SellerUserId { get; set; }
    public string SellerShopName { get; set; } = string.Empty;
    public string RequestPayloadJson { get; set; } = string.Empty;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
