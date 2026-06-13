using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Admin;

// A seller application joined with the applicant's account fields, for the admin review queue.
public sealed class AdminSellerApplicationRow
{
    public int ApplicationId { get; set; }
    public int BuyerUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Note { get; set; }
    public SellerApplicationStatus Status { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
