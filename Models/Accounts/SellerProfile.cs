namespace Custom_keyboard.Models.Accounts;

public sealed class SellerProfile
{
    public int SellerProfileId { get; set; }
    public int UserId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
