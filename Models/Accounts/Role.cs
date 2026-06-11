using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Models.Accounts;

public sealed class Role
{
    public int RoleId { get; set; }
    public UserRole RoleName { get; set; }
    public string? Permissions { get; set; }
}
