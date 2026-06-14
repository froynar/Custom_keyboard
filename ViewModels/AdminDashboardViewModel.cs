using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Analytics;
using Custom_keyboard.Commands;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Stats;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Custom_keyboard.ViewModels;

// Admin flow: manage users + roles, seller verification, the 5 catalog types
// (Kit/Switch/KeycapSet/Stabilizer/Accessory), brands, layouts, and the audit log.
public sealed class AdminDashboardViewModel : RoleDashboardViewModel
{
    private readonly IAdminService _adminService;
    private readonly IStatsService _statsService;
    private readonly ISellerApplicationService _sellerApplicationService;
    private AdminSellerApplicationRow? _selectedApplication;
    private string? _applicationReviewNote;
    private bool _hasLoaded;
    private bool _isBusy;
    private string _statusMessage = Tr("Common_Ready");
    private AdminDashboardSummary _summary = new();
    private AdminOverviewStats _overview = new();
    private StatsPeriod _selectedPeriod = StatsPeriod.Monthly;
    private ISeries[] _statusSeries = [];
    private ISeries[] _revenueSeries = [];
    private Axis[] _revenueXAxes = [];
    private Axis[] _revenueYAxes = [];
    private ISeries[] _topSellersSeries = [];
    private Axis[] _topSellersXAxes = [];
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
        IStatsService statsService,
        ISellerApplicationService sellerApplicationService,
        ChatViewModel chat)
        : base(
            currentUser,
            logoutCommand,
            "Admin_Title",
            "Admin_Subtitle",
            ["Admin_Task1", "Admin_Task2", "Admin_Task3", "Admin_Task4"])
    {
        _adminService = adminService;
        _statsService = statsService;
        _sellerApplicationService = sellerApplicationService;
        Chat = chat;

        AvailableRoles = Enum.GetValues<UserRole>();
        ComponentTypes = Enum.GetValues<AdminComponentType>();

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshAllCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAllAsync, Tr("Admin_RefreshedDashboard")));
        RefreshUsersCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshUsersAsync, Tr("Admin_RefreshedUsers")));
        BanUserCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedUserActiveAsync(false), Tr("Admin_UserBanned")));
        UnbanUserCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedUserActiveAsync(true), Tr("Admin_UserUnbanned")));
        ChangeUserRoleCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(ChangeSelectedUserRoleAsync, Tr("Admin_RoleChanged")));
        RefreshSellersCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshSellersAsync, Tr("Admin_RefreshedSellers")));
        SaveSellerProfileCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveSellerProfileAsync, Tr("Admin_SellerProfileSaved")));
        VerifySellerCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedSellerVerifiedAsync(true), Tr("Admin_SellerVerified")));
        UnverifySellerCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedSellerVerifiedAsync(false), Tr("Admin_SellerUnverified")));
        RefreshCatalogCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshCatalogAsync, Tr("Admin_RefreshedCatalog")));
        NewBrandCommand = new RelayCommand(_ => NewBrandEditor());
        SaveBrandCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveBrandAsync, Tr("Admin_BrandSaved")));
        NewLayoutCommand = new RelayCommand(_ => NewLayoutEditor());
        SaveLayoutCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveLayoutAsync, Tr("Admin_LayoutSaved")));
        RefreshComponentsCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshComponentsAsync, Tr("Admin_RefreshedComponents")));
        NewComponentCommand = new RelayCommand(_ => NewComponentEditor());
        SaveComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveComponentAsync, Tr("Admin_ComponentSaved")));
        HideComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedComponentAvailabilityAsync(false), Tr("Admin_ComponentHidden")));
        RestoreComponentCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(() => SetSelectedComponentAvailabilityAsync(true), Tr("Admin_ComponentRestored")));
        RefreshAuditCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAuditAsync, Tr("Admin_RefreshedAudit")));
        RefreshApplicationsCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshApplicationsAsync, Tr("Admin_RefreshedApplications")));
        ApproveApplicationCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(ApproveSelectedApplicationAsync, Tr("Admin_ApplicationApproved")), _ => CanReviewSelectedApplication);
        RejectApplicationCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RejectSelectedApplicationAsync, Tr("Admin_ApplicationRejected")), _ => CanReviewSelectedApplication);
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
    public ObservableCollection<AdminSellerApplicationRow> Applications { get; } = [];
    public ObservableCollection<SellerRank> TopSellers { get; } = [];

    public StatsPeriod[] Periods { get; } = Enum.GetValues<StatsPeriod>();

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
    public ICommand RefreshApplicationsCommand { get; }
    public ICommand ApproveApplicationCommand { get; }
    public ICommand RejectApplicationCommand { get; }

    public AdminSellerApplicationRow? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (SetProperty(ref _selectedApplication, value))
            {
                OnPropertyChanged(nameof(CanReviewSelectedApplication));
                (ApproveApplicationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (RejectApplicationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ApplicationReviewNote
    {
        get => _applicationReviewNote;
        set => SetProperty(ref _applicationReviewNote, value);
    }

    public bool CanReviewSelectedApplication =>
        !IsBusy && SelectedApplication is { Status: SellerApplicationStatus.Pending };

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanReviewSelectedApplication));
                (ApproveApplicationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (RejectApplicationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
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

    // --- Analytics overview (read-only, whole platform) ---

    public decimal TotalRevenue => _overview.TotalRevenue;
    public int CompletedOrders => _overview.CompletedOrders;
    public int AnalyticsTotalUsers => _overview.TotalUsers;
    public string UsersByRoleText => _overview.UsersByRole.Count == 0
        ? "-"
        : string.Join(" / ", _overview.UsersByRole.Select(item => $"{item.Role}: {item.Count}"));
    public int VerifiedSellers => _overview.VerifiedSellers;
    public int TotalBuilds => _overview.TotalBuilds;
    public int TotalRequests => _overview.TotalRequests;

    public StatsPeriod SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value) && _hasLoaded)
            {
                _ = ExecuteSafeAsync(LoadOverviewAsync, Tr("Admin_ChartUpdated"));
            }
        }
    }

    public ISeries[] StatusSeries
    {
        get => _statusSeries;
        private set => SetProperty(ref _statusSeries, value);
    }

    public ISeries[] RevenueSeries
    {
        get => _revenueSeries;
        private set => SetProperty(ref _revenueSeries, value);
    }

    public Axis[] RevenueXAxes
    {
        get => _revenueXAxes;
        private set => SetProperty(ref _revenueXAxes, value);
    }

    public Axis[] RevenueYAxes
    {
        get => _revenueYAxes;
        private set => SetProperty(ref _revenueYAxes, value);
    }

    public ISeries[] TopSellersSeries
    {
        get => _topSellersSeries;
        private set => SetProperty(ref _topSellersSeries, value);
    }

    public Axis[] TopSellersXAxes
    {
        get => _topSellersXAxes;
        private set => SetProperty(ref _topSellersXAxes, value);
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
        await LoadOverviewAsync();
        await RefreshUsersAsync();
        await RefreshSellersAsync();
        await RefreshCatalogAsync();
        await RefreshComponentsAsync();
        await RefreshAuditAsync();
        await RefreshApplicationsAsync();
    }

    private async Task LoadOverviewAsync()
    {
        var overview = await _statsService.GetAdminOverviewAsync(CurrentUser.UserId, SelectedPeriod);
        _overview = overview;
        OnPropertyChanged(nameof(AnalyticsTotalUsers));
        OnPropertyChanged(nameof(UsersByRoleText));
        OnPropertyChanged(nameof(VerifiedSellers));
        OnPropertyChanged(nameof(TotalBuilds));
        OnPropertyChanged(nameof(TotalRequests));
        OnPropertyChanged(nameof(TotalRevenue));
        OnPropertyChanged(nameof(CompletedOrders));

        RevenueXAxes = ChartFactory.LabelAxis(overview.RevenueSeries.Select(bucket => bucket.Label));
        TopSellersXAxes = ChartFactory.LabelAxis(overview.TopSellers.Select(seller => seller.ShopName));
        RebuildCharts();

        TopSellers.Clear();
        foreach (var seller in overview.TopSellers)
        {
            TopSellers.Add(seller);
        }
    }

    // Rebuilds the chart series (localized labels are baked in at construction) from the last-loaded
    // overview. Safe to call any time: _overview defaults to an empty snapshot.
    private void RebuildCharts()
    {
        StatusSeries = ChartFactory.StatusDonut(_overview.StatusBreakdown);
        RevenueSeries = ChartFactory.RevenueLine(_overview.RevenueSeries);
        RevenueYAxes = ChartFactory.RevenueYAxis();
        TopSellersSeries = ChartFactory.TopSellersColumns(_overview.TopSellers);
    }

    protected override void OnLanguageChangedCore() => RebuildCharts();

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

    private async Task RefreshApplicationsAsync()
    {
        var previousId = SelectedApplication?.ApplicationId;
        Applications.Clear();
        foreach (var application in await _sellerApplicationService.GetApplicationsAsync())
        {
            Applications.Add(application);
        }

        SelectedApplication = Applications.FirstOrDefault(item => item.ApplicationId == previousId)
            ?? Applications.FirstOrDefault();
    }

    private async Task ApproveSelectedApplicationAsync()
    {
        if (SelectedApplication is null)
        {
            throw new InvalidOperationException(Tr("Admin_SelectApplicationFirst"));
        }

        await _sellerApplicationService.ApproveAsync(SelectedApplication.ApplicationId, CurrentUser.UserId, ApplicationReviewNote);
        ApplicationReviewNote = null;
        Summary = await _adminService.GetSummaryAsync();
        await RefreshApplicationsAsync();
        await RefreshUsersAsync();
        await RefreshSellersAsync();
        await RefreshAuditAsync();
    }

    private async Task RejectSelectedApplicationAsync()
    {
        if (SelectedApplication is null)
        {
            throw new InvalidOperationException(Tr("Admin_SelectApplicationFirst"));
        }

        await _sellerApplicationService.RejectAsync(SelectedApplication.ApplicationId, CurrentUser.UserId, ApplicationReviewNote);
        ApplicationReviewNote = null;
        await RefreshApplicationsAsync();
        await RefreshAuditAsync();
    }

    private async Task SetSelectedUserActiveAsync(bool isActive)
    {
        if (SelectedUser is null)
        {
            throw new InvalidOperationException(Tr("Admin_SelectUserFirst"));
        }

        await _adminService.SetUserActiveAsync(SelectedUser.UserId, isActive, CurrentUser.UserId);
        await RefreshUsersAsync();
        await RefreshAuditAsync();
    }

    private async Task ChangeSelectedUserRoleAsync()
    {
        if (SelectedUser is null)
        {
            throw new InvalidOperationException(Tr("Admin_SelectUserFirst"));
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
            throw new InvalidOperationException(Tr("Admin_SelectSellerUserFirst"));
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
            throw new InvalidOperationException(Tr("Admin_SelectSellerFirst"));
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
            throw new InvalidOperationException(Tr("Admin_SelectComponentFirst"));
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
        StatusMessage = Tr("Common_Processing");

        try
        {
            await action();
            StatusMessage = successMessage ?? Tr("Common_Done");
        }
        catch (Exception ex)
        {
            AppLog.Error("AdminDashboard", ex);
            StatusMessage = AppLog.ToUserMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
