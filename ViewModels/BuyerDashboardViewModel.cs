using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;
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
    private SellerPublicStats? _sellerStats;

    private bool _hasLoaded;
    private bool _isBusy;
    private bool _suppressValidation;
    private bool _canSaveBuild;
    private int _validationVersion;
    private string _statusMessage = "San sang.";
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
    private KeyboardBuild? _selectedBuild;
    private SellerProfile? _selectedSeller;
    private BuildModEditorViewModel? _selectedMod;
    private string? _requestNote;

    public BuyerDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        IComponentCatalogService catalogService,
        IBuildService buildService,
        IRequestService requestService,
        IStatsService statsService,
        ChatViewModel chat)
        : base(
            currentUser,
            logoutCommand,
            "Buyer dashboard",
            "Chon kit, them linh kien va gui request cho seller.",
            [
                "Chon kit va them switch/keycap/stabilizer/accessory",
                "Xem canh bao tuong thich va tong gia",
                "Luu build va gui request cho seller",
                "Theo doi trang thai request va chat"
            ])
    {
        _catalogService = catalogService;
        _buildService = buildService;
        _requestService = requestService;
        _statsService = statsService;
        Chat = chat;

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshAllCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAllAsync, "Da refresh buyer dashboard."));
        ShowHomeCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.Home);
        ShowBuildListCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.BuildList);
        ShowSentRequestsCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.SentRequests);
        ShowChatCommand = new RelayCommand(_ => CurrentScreen = BuyerDashboardScreen.Chat);
        NewBuildCommand = new RelayCommand(_ => StartNewBuild());
        OpenBuildCommand = new AsyncRelayCommand(OpenBuildAsync, _ => !IsBusy);
        SaveBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveBuildAsync), _ => CanSaveBuild && !IsBusy);
        ArchiveBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(ArchiveBuildAsync, "Da archive build."), _ => SelectedBuild is not null && !IsBusy);
        SendRequestCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SendRequestAsync), _ => SelectedBuild is not null && SelectedSeller is not null && !IsBusy);
        AddModCommand = new RelayCommand(_ => AddMod());
        RemoveModCommand = new RelayCommand(_ => RemoveSelectedMod(), _ => SelectedMod is not null);
        ApplyRequiredSwitchQuantityCommand = new RelayCommand(_ => ApplyRequiredSwitchQuantity(), _ => SelectedKit is not null);
    }

    public ChatViewModel Chat { get; }

    public ObservableCollection<KeyboardBuild> Builds { get; } = [];
    public ObservableCollection<BuildRequest> Requests { get; } = [];
    public ObservableCollection<SellerProfile> AvailableSellers { get; } = [];
    public ObservableCollection<KeyboardKit> Kits { get; } = [];
    public ObservableCollection<KeyboardSwitch> Switches { get; } = [];
    public ObservableCollection<KeycapSet> Keycaps { get; } = [];
    public ObservableCollection<Stabilizer> Stabilizers { get; } = [];
    public ObservableCollection<AccessoryOptionViewModel> Accessories { get; } = [];
    public ObservableCollection<BuildModEditorViewModel> Mods { get; } = [];
    public ObservableCollection<string> Errors { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> Infos { get; } = [];

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

    public string UserMenuHeader => $"{CurrentUser.Username} ({CurrentUser.Role})";

    public string SellerEmptyMessage => AvailableSellers.Count == 0
        ? "Chua co seller active/verified de nhan request."
        : string.Empty;

    public Visibility HomeVisibility => Visible(BuyerDashboardScreen.Home);
    public Visibility BuildListVisibility => Visible(BuyerDashboardScreen.BuildList);
    public Visibility ConfiguratorVisibility => Visible(BuyerDashboardScreen.Configurator);
    public Visibility SentRequestsVisibility => Visible(BuyerDashboardScreen.SentRequests);
    public Visibility ChatVisibility => Visible(BuyerDashboardScreen.Chat);

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
        ? "Chua chon kit."
        : $"{SelectedKit.PcbTechnology} / mount {SelectedKit.SwitchMount} / can {SelectedKit.RequiredSwitchQuantity} switch"
          + (string.IsNullOrWhiteSpace(SelectedKit.IncludedParts) ? string.Empty : $"\nGom: {SelectedKit.IncludedParts}");

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
        ScheduleValidation();
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
        => ExecuteSafeAsync(RefreshRequestsAsync, "Co cap nhat trang thai request (realtime).");

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
            SellerStats = await _statsService.GetSellerPublicAsync(seller.UserId);
        }
        catch (Exception ex)
        {
            AppLog.Error("BuyerDashboard", ex);
            SellerStats = null;
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
        StatusMessage = $"Da luu build '{saved.Name}' - tong {saved.TotalCostSnapshot:F2} USD.";
    }

    private async Task ArchiveBuildAsync()
    {
        if (SelectedBuild is null)
        {
            throw new InvalidOperationException("Chon build truoc.");
        }

        await _buildService.ArchiveBuildAsync(SelectedBuild.BuildId, CurrentUser.UserId);
        await RefreshBuildsAsync();
    }

    private async Task SendRequestAsync()
    {
        if (SelectedBuild is null)
        {
            throw new InvalidOperationException("Chon build da luu truoc khi gui request.");
        }

        if (SelectedSeller is null)
        {
            throw new InvalidOperationException("Chon seller truoc khi gui request.");
        }

        var seller = SelectedSeller;
        var request = await _requestService.SendRequestAsync(
            SelectedBuild.BuildId,
            CurrentUser.UserId,
            seller.UserId,
            RequestNote);

        RequestNote = null;
        await RefreshRequestsAsync();
        StatusMessage = $"Da gui request {request.RequestId} cho {seller.ShopName}.";
    }

    private async Task OpenBuildAsync(object? parameter)
    {
        if (parameter is KeyboardBuild build)
        {
            SelectedBuild = build;
        }

        if (SelectedBuild is null)
        {
            StatusMessage = "Chon build truoc.";
            return;
        }

        await ExecuteSafeAsync(() => LoadBuildIntoConfiguratorAsync(SelectedBuild), "Da nap build vao configurator.");
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

            Mods.Clear();
            foreach (var mod in full.Mods)
            {
                Mods.Add(new BuildModEditorViewModel
                {
                    ModType = mod.ModType,
                    TargetComponent = mod.TargetComponent,
                    Notes = mod.Notes
                });
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

            Mods.Clear();
        }
        finally
        {
            _suppressValidation = false;
        }

        CurrentScreen = BuyerDashboardScreen.Configurator;
        StatusMessage = "Nhap cau hinh build moi.";
        ScheduleValidation();
    }

    private void ApplyRequiredSwitchQuantity()
    {
        if (SelectedKit is not null)
        {
            SwitchQuantity = SelectedKit.RequiredSwitchQuantity;
        }
    }

    private void AddMod()
    {
        var mod = new BuildModEditorViewModel();
        Mods.Add(mod);
        SelectedMod = mod;
        ScheduleValidation();
    }

    private void RemoveSelectedMod()
    {
        if (SelectedMod is null)
        {
            return;
        }

        Mods.Remove(SelectedMod);
        SelectedMod = null;
        ScheduleValidation();
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
        StatusMessage = "Dang xu ly...";

        try
        {
            await action();
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                StatusMessage = successMessage;
            }
            else if (StatusMessage == "Dang xu ly...")
            {
                StatusMessage = "Hoan tat.";
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
}

public enum BuyerDashboardScreen
{
    Home,
    BuildList,
    Configurator,
    SentRequests,
    Chat
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
    public string Target => Accessory.TargetComponent ?? "General";
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
