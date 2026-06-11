using System.Text.Json;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

public sealed class AdminService : IAdminService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly IUserRepository _userRepository;
    private readonly ISellerRepository _sellerRepository;
    private readonly IComponentRepository _componentRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminService(
        IUserRepository userRepository,
        ISellerRepository sellerRepository,
        IComponentRepository componentRepository,
        IRequestRepository requestRepository,
        IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _sellerRepository = sellerRepository;
        _componentRepository = componentRepository;
        _requestRepository = requestRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AdminDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return new AdminDashboardSummary
        {
            TotalUsers = await _userRepository.GetCountAsync(cancellationToken),
            TotalSellers = await _sellerRepository.GetSellerCountAsync(cancellationToken),
            TotalComponents = await _componentRepository.GetComponentCountAsync(cancellationToken),
            TotalRequests = await _requestRepository.GetCountAsync(cancellationToken)
        };
    }

    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return _userRepository.GetAllAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AdminSellerProfileRow>> GetSellerProfilesAsync(CancellationToken cancellationToken = default)
    {
        return _sellerRepository.GetAdminSellerProfilesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetBrandsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetLayoutsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AdminComponentRecord>> GetComponentsAsync(
        AdminComponentType componentType,
        CancellationToken cancellationToken = default)
    {
        return _componentRepository.GetAdminComponentsAsync(componentType, cancellationToken);
    }

    public async Task SetUserActiveAsync(
        int userId,
        bool isActive,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var oldUser = await RequireUserAsync(userId, cancellationToken);

        if (userId == adminUserId && !isActive)
        {
            throw new InvalidOperationException("Admin khong the khoa chinh tai khoan dang dung.");
        }

        await _userRepository.SetActiveAsync(userId, isActive, cancellationToken);
        var newUser = await RequireUserAsync(userId, cancellationToken);

        await AddAuditAsync(
            adminUserId,
            "users",
            userId.ToString(),
            isActive ? "UserUnban" : "UserBan",
            ToUserAudit(oldUser),
            ToUserAudit(newUser),
            cancellationToken);
    }

    public async Task SetUserRoleAsync(
        int userId,
        UserRole role,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var oldUser = await RequireUserAsync(userId, cancellationToken);

        if (userId == adminUserId && role != UserRole.Admin)
        {
            throw new InvalidOperationException("Admin khong the doi role cua chinh minh khoi Admin.");
        }

        await _userRepository.SetRoleAsync(userId, role, cancellationToken);
        var newUser = await RequireUserAsync(userId, cancellationToken);

        await AddAuditAsync(
            adminUserId,
            "users",
            userId.ToString(),
            "UserRoleChange",
            ToUserAudit(oldUser),
            ToUserAudit(newUser),
            cancellationToken);
    }

    public async Task<SellerProfile> SaveSellerProfileAsync(
        SellerProfile sellerProfile,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var sellerUser = await RequireUserAsync(sellerProfile.UserId, cancellationToken);
        if (sellerUser.Role != UserRole.Seller)
        {
            throw new InvalidOperationException("Chi user role Seller moi co seller profile.");
        }

        var oldProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerProfile.UserId, cancellationToken);

        sellerProfile.ShopName = sellerProfile.ShopName.Trim();
        sellerProfile.Phone = sellerProfile.Phone.Trim();
        sellerProfile.Address = sellerProfile.Address.Trim();
        ValidateSellerProfile(sellerProfile);

        if (oldProfile is null)
        {
            sellerProfile.AssignedByAdminId = adminUserId;
            sellerProfile.AssignedAt = DateTime.UtcNow;
        }
        else
        {
            sellerProfile.SellerProfileId = oldProfile.SellerProfileId;
            sellerProfile.AssignedByAdminId = oldProfile.AssignedByAdminId;
            sellerProfile.AssignedAt = oldProfile.AssignedAt;
            sellerProfile.IsVerified = oldProfile.IsVerified;
            sellerProfile.VerifiedByAdminId = oldProfile.VerifiedByAdminId;
            sellerProfile.VerifiedAt = oldProfile.VerifiedAt;
        }

        var saved = await _sellerRepository.SaveAsync(sellerProfile, cancellationToken);
        await AddAuditAsync(
            adminUserId,
            "seller_profiles",
            saved.UserId.ToString(),
            oldProfile is null ? "SellerProfileCreate" : "SellerProfileUpdate",
            oldProfile,
            saved,
            cancellationToken);

        return saved;
    }

    public async Task<SellerProfile> SetSellerVerifiedAsync(
        int sellerUserId,
        bool isVerified,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var sellerUser = await RequireUserAsync(sellerUserId, cancellationToken);
        if (sellerUser.Role != UserRole.Seller)
        {
            throw new InvalidOperationException("Chi user role Seller moi duoc verify.");
        }

        if (isVerified && !sellerUser.IsActive)
        {
            throw new InvalidOperationException("Khong the verify seller dang bi khoa.");
        }

        var oldProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Hay tao seller profile truoc khi verify.");

        await _sellerRepository.SetVerifiedAsync(sellerUserId, isVerified, adminUserId, cancellationToken);
        var newProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay seller profile sau khi cap nhat.");

        await AddAuditAsync(
            adminUserId,
            "seller_profiles",
            sellerUserId.ToString(),
            isVerified ? "SellerVerify" : "SellerUnverify",
            oldProfile,
            newProfile,
            cancellationToken);

        return newProfile;
    }

    public async Task<Brand> SaveBrandAsync(
        Brand brand,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);

        brand.BrandName = brand.BrandName.Trim();
        brand.Country = string.IsNullOrWhiteSpace(brand.Country) ? null : brand.Country.Trim();
        ValidateBrand(brand);

        var oldBrand = brand.BrandId > 0
            ? await _componentRepository.GetBrandByIdAsync(brand.BrandId, cancellationToken)
            : null;
        var saved = await _componentRepository.SaveBrandAsync(brand, cancellationToken);

        await AddAuditAsync(
            adminUserId,
            "brands",
            saved.BrandId.ToString(),
            oldBrand is null ? "BrandCreate" : "BrandUpdate",
            oldBrand,
            saved,
            cancellationToken);

        return saved;
    }

    public async Task<Layout> SaveLayoutAsync(
        Layout layout,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);

        layout.LayoutId = layout.LayoutId.Trim();
        layout.LayoutName = layout.LayoutName.Trim();
        layout.FormFactor = layout.FormFactor.Trim();
        ValidateLayout(layout);

        var oldLayout = await _componentRepository.GetLayoutByIdAsync(layout.LayoutId, cancellationToken);
        var saved = await _componentRepository.SaveLayoutAsync(layout, cancellationToken);

        await AddAuditAsync(
            adminUserId,
            "layouts",
            saved.LayoutId,
            oldLayout is null ? "LayoutCreate" : "LayoutUpdate",
            oldLayout,
            saved,
            cancellationToken);

        return saved;
    }

    public async Task<AdminComponentRecord> SaveComponentAsync(
        AdminComponentRecord component,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        component.ComponentId = component.ComponentId.Trim();
        ValidateComponent(component);
        await ValidateComponentCatalogReferencesAsync(component, cancellationToken);

        var oldComponent = await _componentRepository.GetAdminComponentByIdAsync(
            component.ComponentType,
            component.ComponentId,
            cancellationToken);

        var saved = await _componentRepository.SaveAdminComponentAsync(component, cancellationToken);
        await AddAuditAsync(
            adminUserId,
            GetComponentTableName(component.ComponentType),
            saved.ComponentId,
            oldComponent is null ? "ComponentCreate" : "ComponentUpdate",
            oldComponent,
            saved,
            cancellationToken);

        if (ComponentHasLayoutMapping(component.ComponentType) && LayoutMappingChanged(oldComponent, saved))
        {
            await AddAuditAsync(
                adminUserId,
                GetComponentMappingTableName(component.ComponentType),
                saved.ComponentId,
                "ComponentLayoutMappingUpdate",
                oldComponent?.SupportedLayoutIds,
                new
                {
                    saved.SupportedLayoutIds,
                    saved.PrimaryLayoutId,
                    saved.PcbVariantName
                },
                cancellationToken);
        }

        return saved;
    }

    public async Task SetComponentAvailabilityAsync(
        AdminComponentType componentType,
        string componentId,
        bool isAvailable,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        componentId = componentId.Trim();
        var oldComponent = await _componentRepository.GetAdminComponentByIdAsync(componentType, componentId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay linh kien.");

        if (isAvailable
            && ComponentHasLayoutMapping(componentType)
            && !oldComponent.SupportedLayoutIds.Any(layoutId => !string.IsNullOrWhiteSpace(layoutId)))
        {
            throw new InvalidOperationException("Khong the khoi phuc linh kien khi chua co layout mapping.");
        }

        await _componentRepository.SetComponentAvailabilityAsync(componentType, componentId, isAvailable, cancellationToken);
        var newComponent = await _componentRepository.GetAdminComponentByIdAsync(componentType, componentId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay linh kien sau khi cap nhat.");

        await AddAuditAsync(
            adminUserId,
            GetComponentTableName(componentType),
            componentId,
            isAvailable ? "ComponentRestore" : "ComponentHide",
            oldComponent,
            newComponent,
            cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(CancellationToken cancellationToken = default)
    {
        return _auditLogRepository.GetRecentAsync(100, cancellationToken);
    }

    private async Task EnsureAdminAsync(int adminUserId, CancellationToken cancellationToken)
    {
        var adminUser = await RequireUserAsync(adminUserId, cancellationToken);
        if (adminUser.Role != UserRole.Admin || !adminUser.IsActive)
        {
            throw new InvalidOperationException("Chi admin active moi duoc thuc hien thao tac nay.");
        }
    }

    private async Task<User> RequireUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay user.");
    }

    private static void ValidateSellerProfile(SellerProfile sellerProfile)
    {
        if (string.IsNullOrWhiteSpace(sellerProfile.ShopName)
            || string.IsNullOrWhiteSpace(sellerProfile.Phone)
            || string.IsNullOrWhiteSpace(sellerProfile.Address))
        {
            throw new InvalidOperationException("Nhap day du shop name, phone va address.");
        }
    }

    private static void ValidateBrand(Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.BrandName))
        {
            throw new InvalidOperationException("Nhap brand name.");
        }
    }

    private static void ValidateLayout(Layout layout)
    {
        if (string.IsNullOrWhiteSpace(layout.LayoutId)
            || string.IsNullOrWhiteSpace(layout.LayoutName)
            || string.IsNullOrWhiteSpace(layout.FormFactor))
        {
            throw new InvalidOperationException("Nhap day du layout id, name va form factor.");
        }

        if (layout.StandardKeyCount <= 0)
        {
            throw new InvalidOperationException("Standard key count phai lon hon 0.");
        }
    }

    private static void ValidateComponent(AdminComponentRecord component)
    {
        if (string.IsNullOrWhiteSpace(component.ComponentId))
        {
            throw new InvalidOperationException("Nhap component id.");
        }

        if (component.BrandId <= 0)
        {
            throw new InvalidOperationException("BrandId phai lon hon 0.");
        }

        if (component.PriceUsd < 0)
        {
            throw new InvalidOperationException("Gia linh kien khong duoc am.");
        }

        switch (component.ComponentType)
        {
            case AdminComponentType.Case:
                RequireText(component.Material, "Material");
                RequireText(component.MountType, "Mount type");
                RequireText(component.Color, "Color");
                RequireNonNegative(component.WeightG, "Weight");
                RequireLayoutMappingWhenAvailable(component);
                break;
            case AdminComponentType.Pcb:
                RequireText(component.Technology, "PCB technology");
                RequireText(component.MountType, "Mount type");
                RequireText(component.SwitchMount, "Switch mount");
                RequireLayoutMappingWhenAvailable(component);
                break;
            case AdminComponentType.Plate:
                RequireText(component.Material, "Material");
                RequireText(component.MountType, "Mount type");
                RequireText(component.FlexCut, "Flex cut");
                RequireLayoutMappingWhenAvailable(component);
                break;
            case AdminComponentType.Switch:
                RequireText(component.Technology, "Switch technology");
                RequireText(component.SwitchType, "Switch type");
                RequireText(component.MountType, "Mount type");
                RequireText(component.SoundProfile, "Sound profile");
                RequireNonNegative(component.ActuationForceG, "Actuation force");
                break;
            case AdminComponentType.KeycapSet:
                RequireText(component.Profile, "Profile");
                RequireText(component.Material, "Material");
                RequireText(component.ColorPrimary, "Color primary");
                RequireText(component.LegendType, "Legend type");
                break;
            case AdminComponentType.Stabilizer:
                RequireText(component.StabilizerType, "Stabilizer type");
                RequireText(component.SizesIncluded, "Sizes included");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component.ComponentType), component.ComponentType, null);
        }
    }

    private async Task ValidateComponentCatalogReferencesAsync(
        AdminComponentRecord component,
        CancellationToken cancellationToken)
    {
        if (await _componentRepository.GetBrandByIdAsync(component.BrandId, cancellationToken) is null)
        {
            throw new InvalidOperationException("Brand khong ton tai.");
        }

        if (!ComponentHasLayoutMapping(component.ComponentType))
        {
            return;
        }

        var layouts = await _componentRepository.GetLayoutsAsync(cancellationToken);
        var existingLayoutIds = layouts.Select(layout => layout.LayoutId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var layoutId in component.SupportedLayoutIds.Where(layoutId => !string.IsNullOrWhiteSpace(layoutId)))
        {
            if (!existingLayoutIds.Contains(layoutId.Trim()))
            {
                throw new InvalidOperationException($"Layout khong ton tai: {layoutId}");
            }
        }
    }

    private static void RequireLayoutMappingWhenAvailable(AdminComponentRecord component)
    {
        if (component.IsAvailable && !component.SupportedLayoutIds.Any(layoutId => !string.IsNullOrWhiteSpace(layoutId)))
        {
            throw new InvalidOperationException("Linh kien available can it nhat mot layout ho tro.");
        }
    }

    private static void RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} khong duoc rong.");
        }
    }

    private static void RequireNonNegative(int value, string fieldName)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{fieldName} khong duoc am.");
        }
    }

    private async Task AddAuditAsync(
        int adminUserId,
        string tableName,
        string recordId,
        string action,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        await _auditLogRepository.AddAsync(
            new AuditLogEntry
            {
                UserId = adminUserId,
                TableName = tableName,
                RecordId = recordId,
                Action = action,
                OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, AuditJsonOptions),
                NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, AuditJsonOptions)
            },
            cancellationToken);
    }

    private static object ToUserAudit(User user)
    {
        return new
        {
            user.UserId,
            user.RoleId,
            Role = user.Role.ToString(),
            user.Username,
            user.Email,
            user.Phone,
            user.IsActive
        };
    }

    private static string GetComponentTableName(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Case => "cases",
            AdminComponentType.Pcb => "pcbs",
            AdminComponentType.Plate => "plates",
            AdminComponentType.Switch => "switches",
            AdminComponentType.KeycapSet => "keycap_sets",
            AdminComponentType.Stabilizer => "stabilizers",
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static string GetComponentMappingTableName(AdminComponentType componentType)
    {
        return componentType switch
        {
            AdminComponentType.Case => "case_layouts",
            AdminComponentType.Pcb => "pcb_layouts",
            AdminComponentType.Plate => "plate_layouts",
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }

    private static bool ComponentHasLayoutMapping(AdminComponentType componentType)
    {
        return componentType is AdminComponentType.Case or AdminComponentType.Pcb or AdminComponentType.Plate;
    }

    private static bool LayoutMappingChanged(AdminComponentRecord? oldComponent, AdminComponentRecord newComponent)
    {
        if (oldComponent is null)
        {
            return newComponent.SupportedLayoutIds.Count > 0;
        }

        return !oldComponent.SupportedLayoutIds.Order(StringComparer.OrdinalIgnoreCase)
                   .SequenceEqual(newComponent.SupportedLayoutIds.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)
            || !string.Equals(oldComponent.PrimaryLayoutId, newComponent.PrimaryLayoutId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldComponent.PcbVariantName, newComponent.PcbVariantName, StringComparison.Ordinal);
    }
}
