using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

// Admin flow: manage users + roles, seller verification, the 5 catalog types
// (Kit/Switch/KeycapSet/Stabilizer/Accessory), brands, layouts, and the audit log.
public sealed class AdminDashboardViewModel : RoleDashboardViewModel
{
    private readonly IAdminService _adminService;
    private bool _hasLoaded;
    private bool _isBusy;
    private string _statusMessage = "San sang.";
    private AdminDashboardSummary _summary = new();
    private User? _selectedUser;
    private UserRole _selectedUserRole = UserRole.Buyer;
    private AdminSellerProfileRow? _selectedSellerProfile;
    private Brand? _selectedBrand;
    private Brand _brandEditor = new();
    private Layout? _selectedLayout;
    private Layout _layoutEditor = new();
    private int _sellerEditorUserId;
    private string _sellerShopName = string.Empty;
    private string _sellerPhone = string.Empty;
    private string _sellerAddress = string.Empty;
    private AdminComponentType _selectedComponentType = AdminComponentType.Kit;
    private AdminComponentRecord? _selectedComponent;
    private AdminComponentRecord _componentEditor = new() { ComponentType = AdminComponentType.Kit, IsAvailable = true };

    public AdminDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        IAdminService adminService,
        ChatViewModel chat)
        : base(
            currentUser,
            logoutCommand,
            "Admin dashboard",
            "Quan ly user, seller va danh muc kit-based.",
            [
                "Quan ly user va trang thai active",
                "Quan ly seller profile va verify",
                "Quan ly kit/switch/keycap/stabilizer/accessory",
                "Xem audit log va chat voi seller"
            ])
    {
        _adminService = adminService;
        Chat = chat;

        AvailableRoles = Enum.GetValues<UserRole>();
        ComponentTypes = Enum.GetValues<AdminComponentType>();

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshAllCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAllAsync, "Da refresh admin dashboard."));
        RefreshUsersCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshUsersAsync, "Da refresh users."));
        BanUserCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedUserActiveAsync(false), "Da ban user."));
        UnbanUserCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedUserActiveAsync(true), "Da mo ban user."));
        ChangeUserRoleCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(ChangeSelectedUserRoleAsync, "Da doi role user."));
        RefreshSellersCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshSellersAsync, "Da refresh sellers."));
        SaveSellerProfileCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveSellerProfileAsync, "Da luu seller profile."));
        VerifySellerCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedSellerVerifiedAsync(true), "Da verify seller."));
        UnverifySellerCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedSellerVerifiedAsync(false), "Da unverify seller."));
        RefreshCatalogCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshCatalogAsync, "Da refresh brand/layout."));
        NewBrandCommand = new RelayCommand(_ => NewBrandEditor());
        SaveBrandCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveBrandAsync, "Da luu brand."));
        NewLayoutCommand = new RelayCommand(_ => NewLayoutEditor());
        SaveLayoutCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveLayoutAsync, "Da luu layout."));
        RefreshComponentsCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshComponentsAsync, "Da refresh linh kien."));
        NewComponentCommand = new RelayCommand(_ => NewComponentEditor());
        SaveComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveComponentAsync, "Da luu linh kien."));
        HideComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedComponentAvailabilityAsync(false), "Da an linh kien."));
        RestoreComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedComponentAvailabilityAsync(true), "Da khoi phuc linh kien."));
        RefreshAuditCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAuditAsync, "Da refresh audit log."));
    }

    public ChatViewModel Chat { get; }

    public UserRole[] AvailableRoles { get; }
    public AdminComponentType[] ComponentTypes { get; }

    public ObservableCollection<User> Users { get; } = [];
    public ObservableCollection<AdminSellerProfileRow> SellerProfiles { get; } = [];
    public ObservableCollection<Brand> Brands { get; } = [];
    public ObservableCollection<Layout> Layouts { get; } = [];
    public ObservableCollection<AdminComponentRecord> Components { get; } = [];
    public ObservableCollection<AuditLogEntry> AuditLogs { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshAllCommand { get; }
    public ICommand RefreshUsersCommand { get; }
    public ICommand BanUserCommand { get; }
    public ICommand UnbanUserCommand { get; }
    public ICommand ChangeUserRoleCommand { get; }
    public ICommand RefreshSellersCommand { get; }
    public ICommand SaveSellerProfileCommand { get; }
    public ICommand VerifySellerCommand { get; }
    public ICommand UnverifySellerCommand { get; }
    public ICommand RefreshCatalogCommand { get; }
    public ICommand NewBrandCommand { get; }
    public ICommand SaveBrandCommand { get; }
    public ICommand NewLayoutCommand { get; }
    public ICommand SaveLayoutCommand { get; }
    public ICommand RefreshComponentsCommand { get; }
    public ICommand NewComponentCommand { get; }
    public ICommand SaveComponentCommand { get; }
    public ICommand HideComponentCommand { get; }
    public ICommand RestoreComponentCommand { get; }
    public ICommand RefreshAuditCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AdminDashboardSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value is not null)
            {
                SelectedUserRole = value.Role;
            }
        }
    }

    public UserRole SelectedUserRole
    {
        get => _selectedUserRole;
        set => SetProperty(ref _selectedUserRole, value);
    }

    public AdminSellerProfileRow? SelectedSellerProfile
    {
        get => _selectedSellerProfile;
        set
        {
            if (SetProperty(ref _selectedSellerProfile, value) && value is not null)
            {
                SellerEditorUserId = value.UserId;
                SellerShopName = value.ShopName;
                SellerPhone = string.IsNullOrWhiteSpace(value.ProfilePhone) ? value.UserPhone : value.ProfilePhone;
                SellerAddress = value.Address;
            }
        }
    }

    public Brand? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value) && value is not null)
            {
                BrandEditor = CloneBrand(value);
            }
        }
    }

    public Brand BrandEditor
    {
        get => _brandEditor;
        set => SetProperty(ref _brandEditor, value);
    }

    public Layout? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value) && value is not null)
            {
                LayoutEditor = CloneLayout(value);
            }
        }
    }

    public Layout LayoutEditor
    {
        get => _layoutEditor;
        set => SetProperty(ref _layoutEditor, value);
    }

    public int SellerEditorUserId
    {
        get => _sellerEditorUserId;
        set => SetProperty(ref _sellerEditorUserId, value);
    }

    public string SellerShopName
    {
        get => _sellerShopName;
        set => SetProperty(ref _sellerShopName, value);
    }

    public string SellerPhone
    {
        get => _sellerPhone;
        set => SetProperty(ref _sellerPhone, value);
    }

    public string SellerAddress
    {
        get => _sellerAddress;
        set => SetProperty(ref _sellerAddress, value);
    }

    public AdminComponentType SelectedComponentType
    {
        get => _selectedComponentType;
        set
        {
            if (SetProperty(ref _selectedComponentType, value))
            {
                RaiseComponentFieldVisibility();
                NewComponentEditor();
                if (_hasLoaded)
                {
                    _ = ExecuteSafeAsync(RefreshComponentsAsync);
                }
            }
        }
    }

    public AdminComponentRecord? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (SetProperty(ref _selectedComponent, value) && value is not null)
            {
                ComponentEditor = value.Clone();
            }
        }
    }

    public AdminComponentRecord ComponentEditor
    {
        get => _componentEditor;
        set => SetProperty(ref _componentEditor, value);
    }

    public Visibility KitFieldsVisibility => VisibleWhen(AdminComponentType.Kit);
    public Visibility SwitchFieldsVisibility => VisibleWhen(AdminComponentType.Switch);
    public Visibility KeycapFieldsVisibility => VisibleWhen(AdminComponentType.KeycapSet);
    public Visibility StabilizerFieldsVisibility => VisibleWhen(AdminComponentType.Stabilizer);
    public Visibility AccessoryFieldsVisibility => VisibleWhen(AdminComponentType.Accessory);
    public Visibility BrandFieldVisibility => _selectedComponentType == AdminComponentType.Accessory ? Visibility.Collapsed : Visibility.Visible;

    private async Task LoadAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAllAsync();
        await Chat.InitializeAsync();
    }

    private async Task RefreshAllAsync()
    {
        Summary = await _adminService.GetSummaryAsync();
        await RefreshUsersAsync();
        await RefreshSellersAsync();
        await RefreshCatalogAsync();
        await RefreshComponentsAsync();
        await RefreshAuditAsync();
    }

    private async Task RefreshUsersAsync()
    {
        Users.Clear();
        foreach (var user in await _adminService.GetUsersAsync())
        {
            Users.Add(user);
        }
    }

    private async Task RefreshSellersAsync()
    {
        SellerProfiles.Clear();
        foreach (var seller in await _adminService.GetSellerProfilesAsync())
        {
            SellerProfiles.Add(seller);
        }
    }

    private async Task RefreshCatalogAsync()
    {
        await RefreshBrandsAsync();
        await RefreshLayoutsAsync();
    }

    private async Task RefreshBrandsAsync()
    {
        Brands.Clear();
        foreach (var brand in await _adminService.GetBrandsAsync())
        {
            Brands.Add(brand);
        }
    }

    private async Task RefreshLayoutsAsync()
    {
        Layouts.Clear();
        foreach (var layout in await _adminService.GetLayoutsAsync())
        {
            Layouts.Add(layout);
        }
    }

    private async Task RefreshComponentsAsync()
    {
        Components.Clear();
        foreach (var component in await _adminService.GetComponentsAsync(SelectedComponentType))
        {
            Components.Add(component);
        }
    }

    private async Task RefreshAuditAsync()
    {
        AuditLogs.Clear();
        foreach (var auditLog in await _adminService.GetAuditLogsAsync())
        {
            AuditLogs.Add(auditLog);
        }
    }

    private async Task SetSelectedUserActiveAsync(bool isActive)
    {
        if (SelectedUser is null)
        {
            throw new InvalidOperationException("Chon user truoc.");
        }

        await _adminService.SetUserActiveAsync(SelectedUser.UserId, isActive, CurrentUser.UserId);
        await RefreshUsersAsync();
        await RefreshAuditAsync();
    }

    private async Task ChangeSelectedUserRoleAsync()
    {
        if (SelectedUser is null)
        {
            throw new InvalidOperationException("Chon user truoc.");
        }

        await _adminService.SetUserRoleAsync(SelectedUser.UserId, SelectedUserRole, CurrentUser.UserId);
        Summary = await _adminService.GetSummaryAsync();
        await RefreshUsersAsync();
        await RefreshSellersAsync();
        await RefreshAuditAsync();
    }

    private async Task SaveSellerProfileAsync()
    {
        if (SellerEditorUserId <= 0)
        {
            throw new InvalidOperationException("Chon seller user truoc.");
        }

        await _adminService.SaveSellerProfileAsync(
            new SellerProfile
            {
                UserId = SellerEditorUserId,
                ShopName = SellerShopName,
                Phone = SellerPhone,
                Address = SellerAddress
            },
            CurrentUser.UserId);

        await RefreshSellersAsync();
        await RefreshAuditAsync();
    }

    private async Task SetSelectedSellerVerifiedAsync(bool isVerified)
    {
        if (SelectedSellerProfile is null)
        {
            throw new InvalidOperationException("Chon seller truoc.");
        }

        await _adminService.SetSellerVerifiedAsync(SelectedSellerProfile.UserId, isVerified, CurrentUser.UserId);
        await RefreshSellersAsync();
        await RefreshAuditAsync();
    }

    private void NewBrandEditor()
    {
        SelectedBrand = null;
        BrandEditor = new Brand();
    }

    private async Task SaveBrandAsync()
    {
        BrandEditor = await _adminService.SaveBrandAsync(CloneBrand(BrandEditor), CurrentUser.UserId);
        await RefreshBrandsAsync();
        await RefreshAuditAsync();
    }

    private void NewLayoutEditor()
    {
        SelectedLayout = null;
        LayoutEditor = new Layout();
    }

    private async Task SaveLayoutAsync()
    {
        LayoutEditor = await _adminService.SaveLayoutAsync(CloneLayout(LayoutEditor), CurrentUser.UserId);
        await RefreshLayoutsAsync();
        await RefreshAuditAsync();
    }

    private void NewComponentEditor()
    {
        SelectedComponent = null;
        ComponentEditor = new AdminComponentRecord
        {
            ComponentType = SelectedComponentType,
            IsAvailable = true
        };
    }

    private async Task SaveComponentAsync()
    {
        ComponentEditor.ComponentType = SelectedComponentType;
        var saved = await _adminService.SaveComponentAsync(ComponentEditor.Clone(), CurrentUser.UserId);
        ComponentEditor = saved.Clone();
        Summary = await _adminService.GetSummaryAsync();
        await RefreshComponentsAsync();
        await RefreshAuditAsync();
    }

    private async Task SetSelectedComponentAvailabilityAsync(bool isAvailable)
    {
        if (SelectedComponent is null)
        {
            throw new InvalidOperationException("Chon linh kien truoc.");
        }

        await _adminService.SetComponentAvailabilityAsync(
            SelectedComponent.ComponentType,
            SelectedComponent.ComponentId,
            isAvailable,
            CurrentUser.UserId);

        await RefreshComponentsAsync();
        await RefreshAuditAsync();
    }

    private void RaiseComponentFieldVisibility()
    {
        OnPropertyChanged(nameof(KitFieldsVisibility));
        OnPropertyChanged(nameof(SwitchFieldsVisibility));
        OnPropertyChanged(nameof(KeycapFieldsVisibility));
        OnPropertyChanged(nameof(StabilizerFieldsVisibility));
        OnPropertyChanged(nameof(AccessoryFieldsVisibility));
        OnPropertyChanged(nameof(BrandFieldVisibility));
    }

    private Visibility VisibleWhen(AdminComponentType componentType)
        => _selectedComponentType == componentType ? Visibility.Visible : Visibility.Collapsed;

    private static Brand CloneBrand(Brand brand)
    {
        return new Brand
        {
            BrandId = brand.BrandId,
            BrandName = brand.BrandName,
            Country = brand.Country
        };
    }

    private static Layout CloneLayout(Layout layout)
    {
        return new Layout
        {
            LayoutId = layout.LayoutId,
            LayoutName = layout.LayoutName,
            FormFactor = layout.FormFactor,
            KeyCount = layout.KeyCount
        };
    }

    private async Task ExecuteSafeAsync(Func<Task> action, string? successMessage = null)
    {
        IsBusy = true;
        StatusMessage = "Dang xu ly...";

        try
        {
            await action();
            StatusMessage = successMessage ?? "Hoan tat.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
