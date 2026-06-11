namespace Custom_keyboard.Models.Admin;

public sealed class AdminSellerProfileRow
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SellerProfileId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ProfilePhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public int? AssignedByAdminId { get; set; }
    public int? VerifiedByAdminId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
