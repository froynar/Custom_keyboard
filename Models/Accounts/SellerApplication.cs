using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Accounts;

// A buyer's request to be upgraded to a Seller. Admin approves (-> role Seller + verified
// seller_profile) or rejects. Shop name / phone / address are captured up front so an approval
// can create the seller_profile without extra input.
public sealed class SellerApplication
{
    public int ApplicationId { get; set; }
    public int BuyerUserId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Note { get; set; }
    public SellerApplicationStatus Status { get; set; } = SellerApplicationStatus.Pending;
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedBy { get; set; }
}
