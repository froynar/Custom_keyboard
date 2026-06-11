using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Accounts;

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public UserRole Role { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
