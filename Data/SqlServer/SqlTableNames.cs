namespace Custom_keyboard.Data.SqlServer;

public static class SqlTableNames
{
    // Account
    public const string Roles = "roles";
    public const string Users = "users";
    public const string SellerProfiles = "seller_profiles";
    public const string SellerApplications = "seller_applications";

    // Catalog
    public const string Brands = "brands";
    public const string Layouts = "layouts";
    public const string KeyboardKits = "keyboard_kits";
    public const string Switches = "switches";
    public const string KeycapSets = "keycap_sets";
    public const string Stabilizers = "stabilizers";
    public const string Accessories = "accessories";

    // Build
    public const string Builds = "builds";
    public const string BuildItems = "build_items";
    public const string BuildMods = "build_mods";

    // Request / Admin
    public const string BuildRequests = "build_requests";
    public const string AuditLog = "audit_log";

    // Chat
    public const string ChatConversations = "chat_conversations";
    public const string ChatMessages = "chat_messages";
}
