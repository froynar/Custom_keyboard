using System.Text.Json;
using Custom_keyboard.Localization;
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
        => _userRepository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<AdminSellerProfileRow>> GetSellerProfilesAsync(CancellationToken cancellationToken = default)
        => _sellerRepository.GetAdminSellerProfilesAsync(cancellationToken);

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetBrandsAsync(cancellationToken);

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
        => _componentRepository.GetLayoutsAsync(cancellationToken);

    public Task<IReadOnlyList<AdminComponentRecord>> GetComponentsAsync(
        AdminComponentType componentType,
        CancellationToken cancellationToken = default)
        => _componentRepository.GetAdminComponentsAsync(componentType, cancellationToken);

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
            throw new InvalidOperationException(Loc.Instance["Service_AdminCannotLockSelf"]);
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
            throw new InvalidOperationException(Loc.Instance["Service_AdminCannotChangeOwnRole"]);
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
            throw new InvalidOperationException(Loc.Instance["Service_OnlySellerProfile"]);
        }

        var oldProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerProfile.UserId, cancellationToken);

        sellerProfile.ShopName = sellerProfile.ShopName.Trim();
        sellerProfile.Phone = sellerProfile.Phone.Trim();
        sellerProfile.Address = sellerProfile.Address.Trim();
        ValidateSellerProfile(sellerProfile);

        // Verification state is owned by SetSellerVerifiedAsync; preserve it across profile edits.
        if (oldProfile is null)
        {
            sellerProfile.IsVerified = false;
            sellerProfile.VerifiedAt = null;
        }
        else
        {
            sellerProfile.SellerProfileId = oldProfile.SellerProfileId;
            sellerProfile.IsVerified = oldProfile.IsVerified;
            sellerProfile.VerifiedAt = oldProfile.VerifiedAt;
        }

        var saved = await _sellerRepository.SaveAsync(sellerProfile, cancellationToken);
        await AddAuditAsync(
            adminUserId,
            "seller_profiles",
            saved.SellerProfileId.ToString(),
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
            throw new InvalidOperationException(Loc.Instance["Service_OnlySellerVerify"]);
        }

        if (isVerified && !sellerUser.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_CannotVerifyLocked"]);
        }

        var oldProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_CreateProfileBeforeVerify"]);

        await _sellerRepository.SetVerifiedAsync(sellerUserId, isVerified, adminUserId, cancellationToken);
        var newProfile = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_SellerProfileNotFoundAfterUpdate"]);

        await AddAuditAsync(
            adminUserId,
            "seller_profiles",
            newProfile.SellerProfileId.ToString(),
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

    public async Task DeleteBrandAsync(
        int brandId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);

        var oldBrand = await _componentRepository.GetBrandByIdAsync(brandId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_BrandNotExist"]);

        if (await _componentRepository.IsBrandInUseAsync(brandId, cancellationToken))
        {
            throw new InvalidOperationException(Loc.Instance["Service_BrandInUse"]);
        }

        await _componentRepository.DeleteBrandAsync(brandId, cancellationToken);

        await AddAuditAsync(
            adminUserId,
            "brands",
            oldBrand.BrandId.ToString(),
            "BrandDelete",
            oldBrand,
            null,
            cancellationToken);
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
            ?? throw new InvalidOperationException(Loc.Instance["Service_ComponentNotFound"]);

        await _componentRepository.SetComponentAvailabilityAsync(componentType, componentId, isAvailable, cancellationToken);
        var newComponent = await _componentRepository.GetAdminComponentByIdAsync(componentType, componentId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_ComponentNotFoundAfterUpdate"]);

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
        => _auditLogRepository.GetRecentAsync(100, cancellationToken);

    private async Task EnsureAdminAsync(int adminUserId, CancellationToken cancellationToken)
    {
        var adminUser = await RequireUserAsync(adminUserId, cancellationToken);
        if (adminUser.Role != UserRole.Admin || !adminUser.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_OnlyActiveAdmin"]);
        }
    }

    private async Task<User> RequireUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_UserNotFound"]);
    }

    private static void ValidateSellerProfile(SellerProfile sellerProfile)
    {
        if (string.IsNullOrWhiteSpace(sellerProfile.ShopName)
            || string.IsNullOrWhiteSpace(sellerProfile.Phone)
            || string.IsNullOrWhiteSpace(sellerProfile.Address))
        {
            throw new InvalidOperationException(Loc.Instance["Service_SellerProfileFieldsRequired"]);
        }
    }

    private static void ValidateBrand(Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.BrandName))
        {
            throw new InvalidOperationException(Loc.Instance["Service_BrandNameRequired"]);
        }
    }

    private static void ValidateLayout(Layout layout)
    {
        if (string.IsNullOrWhiteSpace(layout.LayoutId)
            || string.IsNullOrWhiteSpace(layout.LayoutName)
            || string.IsNullOrWhiteSpace(layout.FormFactor))
        {
            throw new InvalidOperationException(Loc.Instance["Service_LayoutFieldsRequired"]);
        }

        if (layout.KeyCount <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Service_KeyCountPositive"]);
        }
    }

    private static void ValidateComponent(AdminComponentRecord component)
    {
        if (string.IsNullOrWhiteSpace(component.ComponentId))
        {
            throw new InvalidOperationException(Loc.Instance["Service_ComponentIdRequired"]);
        }

        if (string.IsNullOrWhiteSpace(component.Name))
        {
            throw new InvalidOperationException(Loc.Instance["Service_ComponentNameRequired"]);
        }

        if (component.PriceUsd < 0)
        {
            throw new InvalidOperationException(Loc.Instance["Service_ComponentPriceNegative"]);
        }

        // Accessories have no brand in the refactor ERD; all other catalog types require one.
        if (component.ComponentType != AdminComponentType.Accessory && component.BrandId <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Service_BrandIdPositive"]);
        }

        switch (component.ComponentType)
        {
            case AdminComponentType.Kit:
                RequireText(component.LayoutId, "Layout");
                RequireText(component.PcbTechnology, "PCB technology");
                RequireText(component.SwitchMount, "Switch mount");
                if (component.RequiredSwitchQuantity <= 0)
                {
                    throw new InvalidOperationException(Loc.Instance["Service_RequiredSwitchQtyPositive"]);
                }

                break;
            case AdminComponentType.Switch:
                RequireText(component.SwitchTechnology, "Switch technology");
                RequireText(component.MountType, "Mount type");
                if (component.ActuationForceG is <= 0)
                {
                    throw new InvalidOperationException(Loc.Instance["Service_ActuationForcePositive"]);
                }

                break;
            case AdminComponentType.KeycapSet:
                RequireText(component.SupportedFormFactor, "Supported form factor");
                break;
            case AdminComponentType.Stabilizer:
                RequireText(component.SupportedLayouts, "Supported layouts");
                break;
            case AdminComponentType.Accessory:
                RequireText(component.AccessoryType, "Accessory type");
                RequireText(component.TargetComponent, "Target component");
                if (!IsValidAccessoryTarget(component.TargetComponent))
                {
                    throw new InvalidOperationException(Loc.Instance["Service_TargetComponentInvalid"]);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component.ComponentType), component.ComponentType, null);
        }
    }

    private async Task ValidateComponentCatalogReferencesAsync(
        AdminComponentRecord component,
        CancellationToken cancellationToken)
    {
        if (component.ComponentType != AdminComponentType.Accessory
            && await _componentRepository.GetBrandByIdAsync(component.BrandId, cancellationToken) is null)
        {
            throw new InvalidOperationException(Loc.Instance["Service_BrandNotExist"]);
        }

        if (component.ComponentType == AdminComponentType.Kit
            && await _componentRepository.GetLayoutByIdAsync(component.LayoutId.Trim(), cancellationToken) is null)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_LayoutNotExist", component.LayoutId));
        }
    }

    private static bool IsValidAccessoryTarget(string targetComponent)
    {
        return targetComponent.Trim() is "Switch" or "Stabilizer" or "Kit" or "General";
    }

    private static void RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_FieldRequired", fieldName));
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
            AdminComponentType.Kit => "keyboard_kits",
            AdminComponentType.Switch => "switches",
            AdminComponentType.KeycapSet => "keycap_sets",
            AdminComponentType.Stabilizer => "stabilizers",
            AdminComponentType.Accessory => "accessories",
            _ => throw new ArgumentOutOfRangeException(nameof(componentType), componentType, null)
        };
    }
}
