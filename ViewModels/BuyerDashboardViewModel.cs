using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Devices;
using Custom_keyboard.Services.Stats;

namespace Custom_keyboard.ViewModels;

// Kit-based buyer flow: pick a kit, add switch/keycap/stabilizer/accessory build items + mods,
// watch the live validation (errors/warnings/infos) and total, then save / send request / archive.
public sealed class BuyerDashboardViewModel : RoleDashboardViewModel
{
    private readonly IComponentCatalogService _catalogService;
    private readonly IBuildService _buildService;
    private readonly IRequestService _requestService;
    private readonly IStatsService _statsService;
    private readonly ISellerApplicationService _sellerApplicationService;
    private readonly IDeviceService _deviceService;
    private SellerPublicStats? _sellerStats;
    private SellerApplication? _myApplication;
    private string _sellerAppShopName = string.Empty;
    private string _sellerAppPhone = string.Empty;
    private string _sellerAppAddress = string.Empty;
    private string? _sellerAppNote;

    private bool _hasLoaded;
    private bool _isBusy;
    private bool _suppressValidation;
    private bool _refreshingModQuantityLimits;
    private bool _canSaveBuild;
    private int _validationVersion;
    private string _statusMessage = Tr("Common_Ready");
    private string _buildName = string.Empty;
    private string? _buildNotes;
    private string? _editingBuildId;
    private DateTime _editingCreatedAt;
    private BuildStatus _editingStatus = BuildStatus.Draft;
    private decimal _totalPreview;
    private BuyerDashboardScreen _currentScreen = BuyerDashboardScreen.Home;
    private KeyboardKit? _selectedKit;
    private KeyboardSwitch? _selectedSwitch;
    private int _switchQuantity;
    private KeycapSet? _selectedKeycap;
    private Stabilizer? _selectedStabilizer;
    private NoiseRequirement _selectedNoiseRequirement = NoiseRequirement.Normal;
    private KeyboardBuild? _selectedBuild;
    private SellerProfile? _selectedSeller;
    private BuildModEditorViewModel? _selectedMod;
    private string? _requestNote;
    private BuildRequest? _selectedRequest;
    private DeviceTestSession? _selectedRequestQc;

    public BuyerDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        IComponentCatalogService catalogService,
        IBuildService buildService,
        IRequestService requestService,
        IStatsService statsService,
        ISellerApplicationService sellerApplicationService,
        ChatViewModel chat,
        IDeviceService deviceService)
        : base(
            currentUser,
            logoutCommand,
            "Buyer_Title",
            "Buyer_Subtitle",
            ["Buyer_Task1", "Buyer_Task2", "Buyer_Task3", "Buyer_Task4"])
    {
        _catalogService = catalogService;
        _buildService = buildService;
        _requestService = requestService;
        _statsService = statsService;
        _sellerApplicationService = sellerApplicationService;
        _deviceService = deviceService;
        Chat = chat;

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshAllCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAllAsync, Tr("Buyer_Refreshed")));
        ShowHomeCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.Home);
        ShowBuildListCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.BuildList);
        ShowSentRequestsCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.SentRequests);
        ShowChatCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.Chat);
        NewBuildCommand = new RelayCommand(_ => StartNewBuild());
        OpenBuildCommand = new AsyncRelayCommand(OpenBuildAsync, _ => !IsBusy);
        SaveBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveBuildAsync), _ => CanSaveBuild && !IsBusy);
        ArchiveBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(ArchiveBuildAsync, Tr("Buyer_BuildArchived")), _ => SelectedBuild is not null && !IsBusy);
        SendRequestCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SendRequestAsync), _ => SelectedBuild is not null && SelectedSeller is not null && !IsBusy);
        AddModCommand = new RelayCommand(AddMod);
        RemoveModCommand = new RelayCommand(_ => RemoveSelectedMod(), _ => SelectedMod is not null);
        ApplyRequiredSwitchQuantityCommand = new RelayCommand(_ => ApplyRequiredSwitchQuantity(), _ => SelectedKit is not null);
        ShowRegisterSellerCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.RegisterSeller);
        SubmitSellerApplicationCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SubmitSellerApplicationAsync), _ => CanSubmitSellerApplication && !IsBusy);
    }

    public ChatViewModel Chat { get; }

    public ObservableCollection<KeyboardBuild> Builds { get; } = [];
    public ObservableCollection<BuildRequest> Requests { get; } = [];
    public ObservableCollection<SellerProfile> AvailableSellers { get; } = [];
    public ObservableCollection<KeyboardKit> Kits { get; } = [];
    public ObservableCollection<KeyboardSwitch> Switches { get; } = [];
    public ObservableCollection<KeyboardSwitch> CompatibleSwitches { get; } = [];
    public ObservableCollection<KeycapSet> Keycaps { get; } = [];
    public ObservableCollection<Stabilizer> Stabilizers { get; } = [];
    public ObservableCollection<AccessoryOptionViewModel> Accessories { get; } = [];
    public ObservableCollection<BuildModEditorViewModel> Mods { get; } = [];
    public ObservableCollection<string> Errors { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> Infos { get; } = [];
    public NoiseRequirement[] NoiseRequirements { get; } = Enum.GetValues<NoiseRequirement>();

    public ICommand LoadCommand { get; }
    public ICommand RefreshAllCommand { get; }
    public ICommand ShowHomeCommand { get; }
    public ICommand ShowBuildListCommand { get; }
    public ICommand ShowSentRequestsCommand { get; }
    public ICommand ShowChatCommand { get; }
    public ICommand NewBuildCommand { get; }
    public ICommand OpenBuildCommand { get; }
    public ICommand SaveBuildCommand { get; }
    public ICommand ArchiveBuildCommand { get; }
    public ICommand SendRequestCommand { get; }
    public ICommand AddModCommand { get; }
    public ICommand RemoveModCommand { get; }
    public ICommand ApplyRequiredSwitchQuantityCommand { get; }
    public ICommand ShowRegisterSellerCommand { get; }
    public ICommand SubmitSellerApplicationCommand { get; }

    public string UserMenuHeader => $"{CurrentUser.Username} ({CurrentUser.Role})";

    public string SellerEmptyMessage => AvailableSellers.Count == 0
        ? Tr("Buyer_NoSellers")
        : string.Empty;

    public Visibility HomeVisibility => Visible(BuyerDashboardScreen.Home);
    public Visibility BuildListVisibility => Visible(BuyerDashboardScreen.BuildList);
    public Visibility ConfiguratorVisibility => Visible(BuyerDashboardScreen.Configurator);
    public Visibility SentRequestsVisibility => Visible(BuyerDashboardScreen.SentRequests);
    public Visibility ChatVisibility => Visible(BuyerDashboardScreen.Chat);
    public Visibility RegisterSellerVisibility => Visible(BuyerDashboardScreen.RegisterSeller);

    public BuyerDashboardScreen CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (SetProperty(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(BuildListVisibility));
                OnPropertyChanged(nameof(ConfiguratorVisibility));
                OnPropertyChanged(nameof(SentRequestsVisibility));
                OnPropertyChanged(nameof(ChatVisibility));
                OnPropertyChanged(nameof(RegisterSellerVisibility));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BuildName
    {
        get => _buildName;
        set
        {
            if (SetProperty(ref _buildName, value))
            {
                ScheduleValidation();
            }
        }
    }

    public string? BuildNotes
    {
        get => _buildNotes;
        set => SetProperty(ref _buildNotes, value);
    }

    public decimal TotalPreview
    {
        get => _totalPreview;
        private set => SetProperty(ref _totalPreview, value);
    }

    public bool CanSaveBuild
    {
        get => _canSaveBuild;
        private set
        {
            if (SetProperty(ref _canSaveBuild, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public KeyboardKit? SelectedKit
    {
        get => _selectedKit;
        set
        {
            if (SetProperty(ref _selectedKit, value))
            {
                OnPropertyChanged(nameof(KitSummary));
                RefreshCompatibleSwitches();
                if (!_suppressValidation && value is not null && SwitchQuantity != value.RequiredSwitchQuantity)
                {
                    SwitchQuantity = value.RequiredSwitchQuantity;
                }

                ScheduleValidation();
                RaiseCommandStatesChanged();
            }
        }
    }

    public string KitSummary => SelectedKit is null
        ? Tr("Buyer_NoKitSelected")
        : TrFormat("Buyer_KitSummaryFormat", SelectedKit.PcbTechnology, SelectedKit.SwitchMount, SelectedKit.RequiredSwitchQuantity)
          + (string.IsNullOrWhiteSpace(SelectedKit.IncludedParts) ? string.Empty : TrFormat("Buyer_KitIncludes", SelectedKit.IncludedParts));

    public KeyboardSwitch? SelectedSwitch
    {
        get => _selectedSwitch;
        set
        {
            if (SetProperty(ref _selectedSwitch, value))
            {
                ScheduleValidation();
            }
        }
    }

    public int SwitchQuantity
    {
        get => _switchQuantity;
        set
        {
            if (SetProperty(ref _switchQuantity, value))
            {
                RefreshModQuantityLimits();
                ScheduleValidation();
            }
        }
    }

    public KeycapSet? SelectedKeycap
    {
        get => _selectedKeycap;
        set
        {
            if (SetProperty(ref _selectedKeycap, value))
            {
                ScheduleValidation();
            }
        }
    }

    public Stabilizer? SelectedStabilizer
    {
        get => _selectedStabilizer;
        set
        {
            if (SetProperty(ref _selectedStabilizer, value))
            {
                ScheduleValidation();
            }
        }
    }

    public NoiseRequirement SelectedNoiseRequirement
    {
        get => _selectedNoiseRequirement;
        set
        {
            if (SetProperty(ref _selectedNoiseRequirement, value))
            {
                ScheduleValidation();
            }
        }
    }

    public KeyboardBuild? SelectedBuild
    {
        get => _selectedBuild;
        set
        {
            if (SetProperty(ref _selectedBuild, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public SellerProfile? SelectedSeller
    {
        get => _selectedSeller;
        set
        {
            if (SetProperty(ref _selectedSeller, value))
            {
                RaiseCommandStatesChanged();
                _ = LoadSellerStatsAsync(value);
            }
        }
    }

    // Public (non-financial) stats of the currently selected seller, shown as a small card
    // to help the buyer compare sellers before sending a request.
    public SellerPublicStats? SellerStats
    {
        get => _sellerStats;
        private set
        {
            if (SetProperty(ref _sellerStats, value))
            {
                OnPropertyChanged(nameof(SellerStatsVisibility));
            }
        }
    }

    public Visibility SellerStatsVisibility => SellerStats is not null ? Visibility.Visible : Visibility.Collapsed;

    // Sent-requests selection -> show that request's latest QC summary (read-only, no per-key detail).
    public BuildRequest? SelectedRequest
    {
        get => _selectedRequest;
        set
        {
            if (SetProperty(ref _selectedRequest, value))
            {
                _ = LoadSelectedRequestQcAsync(value);
            }
        }
    }

    public DeviceTestSession? SelectedRequestQc
    {
        get => _selectedRequestQc;
        private set
        {
            if (SetProperty(ref _selectedRequestQc, value))
            {
                OnPropertyChanged(nameof(SelectedRequestQcVisibility));
                OnPropertyChanged(nameof(SelectedRequestQcEmptyVisibility));
            }
        }
    }

    public Visibility SelectedRequestQcVisibility => SelectedRequestQc is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectedRequestQcEmptyVisibility => SelectedRequestQc is null ? Visibility.Visible : Visibility.Collapsed;

    public BuildModEditorViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (SetProperty(ref _selectedMod, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public string? RequestNote
    {
        get => _requestNote;
        set => SetProperty(ref _requestNote, value);
    }

    // --- Seller application (buyer -> seller upgrade) ---

    public string SellerAppShopName
    {
        get => _sellerAppShopName;
        set { if (SetProperty(ref _sellerAppShopName, value)) RaiseCommandStatesChanged(); }
    }

    public string SellerAppPhone
    {
        get => _sellerAppPhone;
        set { if (SetProperty(ref _sellerAppPhone, value)) RaiseCommandStatesChanged(); }
    }

    public string SellerAppAddress
    {
        get => _sellerAppAddress;
        set { if (SetProperty(ref _sellerAppAddress, value)) RaiseCommandStatesChanged(); }
    }

    public string? SellerAppNote
    {
        get => _sellerAppNote;
        set => SetProperty(ref _sellerAppNote, value);
    }

    public SellerApplication? MyApplication
    {
        get => _myApplication;
        private set
        {
            if (SetProperty(ref _myApplication, value))
            {
                OnPropertyChanged(nameof(HasMyApplication));
                OnPropertyChanged(nameof(MyApplicationStatusText));
                RaiseCommandStatesChanged();
            }
        }
    }

    public bool HasMyApplication => MyApplication is not null;

    public string MyApplicationStatusText => MyApplication is null
        ? Tr("Buyer_NoApplication")
        : TrFormat("Buyer_ApplicationStatusFormat", Tr("Status_" + MyApplication.Status))
          + (string.IsNullOrWhiteSpace(MyApplication.ReviewNote) ? string.Empty : $" - {MyApplication.ReviewNote}");

    public bool CanSubmitSellerApplication =>
        !IsBusy
        && (MyApplication is null || MyApplication.Status != SellerApplicationStatus.Pending)
        && !string.IsNullOrWhiteSpace(SellerAppShopName)
        && !string.IsNullOrWhiteSpace(SellerAppPhone)
        && !string.IsNullOrWhiteSpace(SellerAppAddress);

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
        await RefreshCatalogAsync();
        await RefreshBuildsAsync();
        await RefreshSellersAsync();
        await RefreshRequestsAsync();
        await RefreshMyApplicationAsync();
        ScheduleValidation();
    }

    private async Task RefreshMyApplicationAsync()
    {
        MyApplication = await _sellerApplicationService.GetMyApplicationAsync(CurrentUser.UserId);

        // Prefill the form from the latest application so a rejected buyer can edit + resubmit.
        if (MyApplication is not null && string.IsNullOrWhiteSpace(_sellerAppShopName))
        {
            SellerAppShopName = MyApplication.ShopName;
            SellerAppPhone = MyApplication.Phone;
            SellerAppAddress = MyApplication.Address;
            SellerAppNote = MyApplication.Note;
        }
    }

    private async Task SubmitSellerApplicationAsync()
    {
        var application = await _sellerApplicationService.SubmitAsync(
            CurrentUser.UserId,
            SellerAppShopName,
            SellerAppPhone,
            SellerAppAddress,
            SellerAppNote);

        MyApplication = application;
        StatusMessage = Tr("Buyer_ApplicationSubmitted");
    }

    private async Task RefreshCatalogAsync()
    {
        _suppressValidation = true;
        try
        {
            var previousKitId = SelectedKit?.KitId;
            var previousSwitchId = SelectedSwitch?.SwitchId;
            var previousKeycapId = SelectedKeycap?.KeycapId;
            var previousStabilizerId = SelectedStabilizer?.StabilizerId;

            ReplaceItems(Kits, await _catalogService.GetAvailableKitsAsync());
            ReplaceItems(Switches, await _catalogService.GetAvailableSwitchesAsync());
            RefreshCompatibleSwitches();
            ReplaceItems(Keycaps, await _catalogService.GetAvailableKeycapSetsAsync());
            ReplaceItems(Stabilizers, await _catalogService.GetAvailableStabilizersAsync());

            Accessories.Clear();
            foreach (var accessory in await _catalogService.GetAvailableAccessoriesAsync())
            {
                Accessories.Add(new AccessoryOptionViewModel(accessory, ScheduleValidation));
            }

            SelectedKit = FindById(Kits, previousKitId, item => item.KitId);
            SelectedSwitch = FindById(Switches, previousSwitchId, item => item.SwitchId);
            SelectedKeycap = FindById(Keycaps, previousKeycapId, item => item.KeycapId);
            SelectedStabilizer = FindById(Stabilizers, previousStabilizerId, item => item.StabilizerId);
        }
        finally
        {
            _suppressValidation = false;
        }
    }

    private async Task RefreshBuildsAsync()
    {
        var previousId = SelectedBuild?.BuildId;
        Builds.Clear();
        foreach (var build in await _buildService.GetBuyerBuildsAsync(CurrentUser.UserId))
        {
            Builds.Add(build);
        }

        SelectedBuild = Builds.FirstOrDefault(item => Same(item.BuildId, previousId));
    }

    private async Task RefreshRequestsAsync()
    {
        Requests.Clear();
        foreach (var request in await _requestService.GetBuyerRequestsAsync(CurrentUser.UserId))
        {
            Requests.Add(request);
        }
    }

    /// <summary>Reload requests in response to a realtime "status update" event.</summary>
    public Task ReloadRequestsAsync()
        => ExecuteSafeAsync(RefreshRequestsAsync, Tr("Buyer_RealtimeUpdate"));

    private async Task RefreshSellersAsync()
    {
        var previousSellerId = SelectedSeller?.UserId;
        AvailableSellers.Clear();
        foreach (var seller in await _requestService.GetAvailableSellersAsync(CurrentUser.UserId))
        {
            AvailableSellers.Add(seller);
        }

        SelectedSeller = AvailableSellers.FirstOrDefault(item => item.UserId == previousSellerId)
            ?? AvailableSellers.FirstOrDefault();
        OnPropertyChanged(nameof(SellerEmptyMessage));
    }

    private async Task LoadSellerStatsAsync(SellerProfile? seller)
    {
        if (seller is null)
        {
            SellerStats = null;
            return;
        }

        try
        {
            SellerStats = await _statsService.GetSellerPublicAsync(CurrentUser.UserId, seller.UserId);
        }
        catch (Exception ex)
        {
            AppLog.Error("BuyerDashboard", ex);
            SellerStats = null;
        }
    }

    // Best-effort: a request may never have been QC-tested, so a missing session is normal (not an error).
    private async Task LoadSelectedRequestQcAsync(BuildRequest? request)
    {
        if (request is null)
        {
            SelectedRequestQc = null;
            return;
        }

        try
        {
            SelectedRequestQc = await _deviceService.GetLatestSessionByRequestAsync(request.RequestId);
        }
        catch (Exception ex)
        {
            AppLog.Error("BuyerDashboard", ex);
            SelectedRequestQc = null;
        }
    }

    private async Task SaveBuildAsync()
    {
        var build = CreateBuildFromCurrentSelection();
        var saved = await _buildService.SaveBuildAsync(build);
        _editingBuildId = saved.BuildId;
        _editingCreatedAt = saved.CreatedAt;
        _editingStatus = saved.Status;
        await RefreshBuildsAsync();
        SelectedBuild = Builds.FirstOrDefault(item => Same(item.BuildId, saved.BuildId));
        StatusMessage = TrFormat("Buyer_BuildSaved", saved.Name, saved.TotalCostSnapshot);
    }

    private async Task ArchiveBuildAsync()
    {
        if (SelectedBuild is null)
        {
            throw new InvalidOperationException(Tr("Buyer_SelectBuildFirst"));
        }

        await _buildService.ArchiveBuildAsync(SelectedBuild.BuildId, CurrentUser.UserId);
        await RefreshBuildsAsync();
    }

    private async Task SendRequestAsync()
    {
        if (SelectedBuild is null)
        {
            throw new InvalidOperationException(Tr("Buyer_SelectSavedBuildFirst"));
        }

        if (SelectedSeller is null)
        {
            throw new InvalidOperationException(Tr("Buyer_SelectSellerFirst"));
        }

        var seller = SelectedSeller;
        var request = await _requestService.SendRequestAsync(
            SelectedBuild.BuildId,
            CurrentUser.UserId,
            seller.UserId,
            RequestNote);

        RequestNote = null;
        await RefreshRequestsAsync();
        StatusMessage = TrFormat("Buyer_RequestSent", request.RequestId, seller.ShopName);
    }

    private async Task OpenBuildAsync(object? parameter)
    {
        if (parameter is KeyboardBuild build)
        {
            SelectedBuild = build;
        }

        if (SelectedBuild is null)
        {
            StatusMessage = Tr("Buyer_SelectBuildFirst");
            return;
        }

        await ExecuteSafeAsync(() => LoadBuildIntoConfiguratorAsync(SelectedBuild), Tr("Buyer_BuildLoaded"));
    }

    private async Task LoadBuildIntoConfiguratorAsync(KeyboardBuild build)
    {
        // Reload to ensure items + mods are present (list query already loads them, but be safe).
        var full = await _buildService.GetBuildByIdAsync(build.BuildId) ?? build;

        _suppressValidation = true;
        try
        {
            _editingBuildId = full.BuildId;
            _editingCreatedAt = full.CreatedAt;
            _editingStatus = full.Status;
            BuildName = full.Name;
            BuildNotes = full.Notes;
            SelectedNoiseRequirement = full.NoiseRequirement;

            SelectedKit = FindById(Kits, full.KitId, item => item.KitId);
            SelectedSwitch = null;
            SwitchQuantity = SelectedKit?.RequiredSwitchQuantity ?? 0;
            SelectedKeycap = null;
            SelectedStabilizer = null;
            foreach (var accessory in Accessories)
            {
                accessory.SetSelectedSilently(false);
            }

            foreach (var item in full.Items)
            {
                if (item.SwitchId is not null)
                {
                    SelectedSwitch = FindById(Switches, item.SwitchId, sw => sw.SwitchId);
                    SwitchQuantity = item.Quantity;
                }
                else if (item.KeycapId is not null)
                {
                    SelectedKeycap = FindById(Keycaps, item.KeycapId, kc => kc.KeycapId);
                }
                else if (item.StabilizerId is not null)
                {
                    SelectedStabilizer = FindById(Stabilizers, item.StabilizerId, st => st.StabilizerId);
                }
                else if (item.AccessoryId is not null)
                {
                    var option = Accessories.FirstOrDefault(a => Same(a.Accessory.AccessoryId, item.AccessoryId));
                    option?.SetSelectedSilently(true);
                }
            }

            ClearMods();
            foreach (var mod in full.Mods)
            {
                AddMod(BuildModEditorViewModel.FromBuildMod(mod, SwitchQuantity));
            }
        }
        finally
        {
            _suppressValidation = false;
        }

        CurrentScreen = BuyerDashboardScreen.Configurator;
        await RefreshValidationAsync();
    }

    private void StartNewBuild()
    {
        _suppressValidation = true;
        try
        {
            _editingBuildId = null;
            _editingCreatedAt = default;
            _editingStatus = BuildStatus.Draft;
            BuildName = string.Empty;
            BuildNotes = null;
            SelectedNoiseRequirement = NoiseRequirement.Normal;
            SelectedKit = null;
            SelectedSwitch = null;
            SwitchQuantity = 0;
            SelectedKeycap = null;
            SelectedStabilizer = null;
            SelectedMod = null;
            foreach (var accessory in Accessories)
            {
                accessory.SetSelectedSilently(false);
            }

            ClearMods();
        }
        finally
        {
            _suppressValidation = false;
        }

        CurrentScreen = BuyerDashboardScreen.Configurator;
        StatusMessage = Tr("Buyer_NewBuildPrompt");
        ScheduleValidation();
    }

    private void ApplyRequiredSwitchQuantity()
    {
        if (SelectedKit is not null)
        {
            SwitchQuantity = SelectedKit.RequiredSwitchQuantity;
        }
    }

    private void RefreshCompatibleSwitches()
    {
        var selectedKit = SelectedKit;
        var compatible = selectedKit is null
            ? Enumerable.Empty<KeyboardSwitch>()
            : Switches.Where(sw => IsCompatibleSwitch(selectedKit, sw));

        ReplaceItems(CompatibleSwitches, compatible);

        if (SelectedSwitch is null || !CompatibleSwitches.Any(sw => Same(sw.SwitchId, SelectedSwitch.SwitchId)))
        {
            SelectedSwitch = CompatibleSwitches.FirstOrDefault();
        }
    }

    private void AddMod(object? parameter)
    {
        var (targetComponent, modType) = ParseModPreset(parameter);
        var switchQuantityLimit = GetAvailableSwitchModQuantity(targetComponent, modType);
        if (UsesSwitchQuantity(targetComponent) && switchQuantityLimit <= 0)
        {
            StatusMessage = TrFormat("Buyer_ModQuantityFull", modType);
            return;
        }

        AddMod(BuildModEditorViewModel.CreatePreset(targetComponent, modType, switchQuantityLimit));
    }

    private void AddMod(BuildModEditorViewModel mod)
    {
        mod.PropertyChanged += OnModPropertyChanged;
        Mods.Add(mod);
        SelectedMod = mod;
        RefreshModQuantityLimits();
        ScheduleValidation();
    }

    private void RemoveSelectedMod()
    {
        if (SelectedMod is null)
        {
            return;
        }

        SelectedMod.PropertyChanged -= OnModPropertyChanged;
        Mods.Remove(SelectedMod);
        SelectedMod = null;
        RefreshModQuantityLimits();
        ScheduleValidation();
    }

    private void ClearMods()
    {
        foreach (var mod in Mods)
        {
            mod.PropertyChanged -= OnModPropertyChanged;
        }

        Mods.Clear();
        SelectedMod = null;
    }

    private void OnModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshModQuantityLimits();
        ScheduleValidation();
    }

    private void RefreshModQuantityLimits()
    {
        if (_refreshingModQuantityLimits)
        {
            return;
        }

        _refreshingModQuantityLimits = true;
        try
        {
            foreach (var mod in Mods)
            {
                var max = mod.UsesSwitchQuantity
                    ? GetAvailableSwitchModQuantity(mod.TargetComponent, mod.ModType, mod)
                    : SwitchQuantity;
                mod.SetSwitchQuantityLimit(max);
            }
        }
        finally
        {
            _refreshingModQuantityLimits = false;
        }
    }

    private int GetAvailableSwitchModQuantity(
        string targetComponent,
        string modType,
        BuildModEditorViewModel? excluding = null)
    {
        if (!UsesSwitchQuantity(targetComponent))
        {
            return Math.Max(1, SwitchQuantity);
        }

        var usedBySameModType = Mods
            .Where(mod => !ReferenceEquals(mod, excluding)
                          && mod.UsesSwitchQuantity
                          && Same(mod.ModType, modType))
            .Sum(mod => mod.ModQuantity);

        return Math.Max(0, SwitchQuantity - usedBySameModType);
    }

    private static bool UsesSwitchQuantity(string? targetComponent)
        => Same(targetComponent, "Switch");

    private static (string TargetComponent, string ModType) ParseModPreset(object? parameter)
    {
        var preset = parameter as string;
        if (string.IsNullOrWhiteSpace(preset))
        {
            return ("Switch", "Lube");
        }

        var parts = preset.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? (parts[0], parts[1]) : ("Switch", preset.Trim());
    }

    private KeyboardBuild CreateBuildFromCurrentSelection()
    {
        var items = new List<BuildItem>();
        if (SelectedSwitch is not null)
        {
            items.Add(new BuildItem { SwitchId = SelectedSwitch.SwitchId, Quantity = SwitchQuantity });
        }

        if (SelectedKeycap is not null)
        {
            items.Add(new BuildItem { KeycapId = SelectedKeycap.KeycapId, Quantity = 1 });
        }

        if (SelectedStabilizer is not null)
        {
            items.Add(new BuildItem { StabilizerId = SelectedStabilizer.StabilizerId, Quantity = 1 });
        }

        foreach (var accessory in Accessories.Where(item => item.IsSelected))
        {
            items.Add(new BuildItem { AccessoryId = accessory.Accessory.AccessoryId, Quantity = 1 });
        }

        return new KeyboardBuild
        {
            BuildId = _editingBuildId ?? string.Empty,
            CreatedAt = _editingCreatedAt,
            BuyerId = CurrentUser.UserId,
            KitId = SelectedKit?.KitId ?? string.Empty,
            Name = BuildName,
            Notes = BuildNotes,
            NoiseRequirement = SelectedNoiseRequirement,
            Status = _editingStatus,
            Items = items,
            Mods = Mods.Select(mod => mod.ToBuildMod()).ToList()
        };
    }

    private void ScheduleValidation()
    {
        if (!_suppressValidation)
        {
            _ = RefreshValidationAsync();
        }
    }

    private async Task RefreshValidationAsync()
    {
        var version = ++_validationVersion;
        var build = CreateBuildFromCurrentSelection();

        try
        {
            var validation = await _buildService.ValidateBuildAsync(build);
            if (version != _validationVersion)
            {
                return;
            }

            ReplaceItems(Errors, validation.Errors);
            ReplaceItems(Warnings, validation.Warnings);
            ReplaceItems(Infos, validation.Infos);
            TotalPreview = validation.TotalCost;
            CanSaveBuild = validation.IsValid && !string.IsNullOrWhiteSpace(BuildName) && SelectedKit is not null;
        }
        catch (Exception ex)
        {
            if (version != _validationVersion)
            {
                return;
            }

            ReplaceItems(Errors, [ex.Message]);
            Warnings.Clear();
            Infos.Clear();
            TotalPreview = 0m;
            CanSaveBuild = false;
        }
    }

    private async Task ExecuteSafeAsync(Func<Task> action, string? successMessage = null)
    {
        IsBusy = true;
        var processing = Tr("Common_Processing");
        StatusMessage = processing;

        try
        {
            await action();
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                StatusMessage = successMessage;
            }
            else if (StatusMessage == processing)
            {
                StatusMessage = Tr("Common_Done");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("BuyerDashboard", ex);
            StatusMessage = AppLog.ToUserMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStatesChanged()
    {
        (SaveBuildCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ArchiveBuildCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SendRequestCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenBuildCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RemoveModCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyRequiredSwitchQuantityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SubmitSellerApplicationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSubmitSellerApplication));
    }

    private Visibility Visible(BuyerDashboardScreen screen)
        => CurrentScreen == screen ? Visibility.Visible : Visibility.Collapsed;

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static T? FindById<T>(IEnumerable<T> items, string? id, Func<T, string> getId)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return items.FirstOrDefault(item => Same(getId(item), id));
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsCompatibleSwitch(KeyboardKit kit, KeyboardSwitch sw)
        => Same(sw.SwitchTechnology, kit.PcbTechnology)
           && Same(sw.MountType, kit.SwitchMount);
}

public enum BuyerDashboardScreen
{
    Home,
    BuildList,
    Configurator,
    SentRequests,
    Chat,
    RegisterSeller
}

// One accessory row with a checkbox; toggling it re-runs build validation.
public sealed class AccessoryOptionViewModel : ViewModelBase
{
    private readonly Action _onChanged;
    private bool _isSelected;

    public AccessoryOptionViewModel(Accessory accessory, Action onChanged)
    {
        Accessory = accessory;
        _onChanged = onChanged;
    }

    public Accessory Accessory { get; }
    public string DisplayName => $"{Accessory.AccessoryName} ({Accessory.AccessoryType})";
    public string PriceText => $"{Accessory.PriceUsd:F2} USD";
    public string Target => Accessory.TargetComponent ?? Tr("Common_General");
    public string Label => $"{DisplayName} - {PriceText} ({Target})";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _onChanged();
            }
        }
    }

    public void SetSelectedSilently(bool value)
    {
        if (_isSelected != value)
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
}
