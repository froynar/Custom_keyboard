using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

public sealed class BuyerDashboardViewModel : RoleDashboardViewModel
{
    private readonly IComponentCatalogService _catalogService;
    private readonly IBuildService _buildService;
    private readonly IRequestService _requestService;
    private bool _hasLoaded;
    private bool _isBusy;
    private bool _canSaveBuild;
    private bool _suppressSelectionRefresh;
    private int _validationVersion;
    private string _statusMessage = "San sang.";
    private string _buildName = string.Empty;
    private string? _buildNotes;
    private string _componentSearchText = string.Empty;
    private string? _editingBuildId;
    private DateTime _editingCreatedAt;
    private decimal _totalPreview;
    private BuyerDashboardScreen _currentScreen = BuyerDashboardScreen.Home;
    private ComponentCategory _selectedComponentCategory = ComponentCategory.All;
    private Layout? _selectedLayout;
    private KeyboardCase? _selectedCase;
    private Pcb? _selectedPcb;
    private Plate? _selectedPlate;
    private KeyboardSwitch? _selectedSwitch;
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
        IRequestService requestService)
        : base(
            currentUser,
            logoutCommand,
            "Buyer dashboard",
            "Quan ly build keyboard va request cua ban.",
            [
                "Xem layout va linh kien kha dung",
                "Tao build keyboard",
                "Luu build va gui request cho seller",
                "Theo doi trang thai request"
            ])
    {
        _catalogService = catalogService;
        _buildService = buildService;
        _requestService = requestService;

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshAllCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshAllAsync, "Da refresh buyer dashboard."));
        ShowHomeCommand = new RelayCommand(_ => SetScreen(BuyerDashboardScreen.Home));
        ShowBuildListCommand = new RelayCommand(_ => SetScreen(BuyerDashboardScreen.BuildList));
        ShowSentRequestsCommand = new RelayCommand(_ => SetScreen(BuyerDashboardScreen.SentRequests));
        NewBuildCommand = new RelayCommand(_ => StartNewBuild());
        OpenBuildDetailCommand = new RelayCommand(OpenBuildDetail, _ => !IsBusy);
        LoadSelectedBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadSelectedBuildAsync, "Da nap build vao configurator."), _ => SelectedBuild is not null && !IsBusy);
        SaveBuildCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SaveBuildAsync), _ => CanSaveBuild && !IsBusy);
        SendRequestCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(SendRequestAsync), _ => SelectedBuild is not null && SelectedSeller is not null && !IsBusy);
        SelectCategoryCommand = new RelayCommand(SelectCategory);
        SelectComponentCommand = new RelayCommand(SelectComponent);
        ClearComponentSearchCommand = new RelayCommand(_ => ComponentSearchText = string.Empty);
        AddModCommand = new RelayCommand(_ => AddMod());
        RemoveModCommand = new RelayCommand(_ => RemoveSelectedMod(), _ => SelectedMod is not null);
        ShowAccountInfoCommand = new RelayCommand(_ => ShowAccountInfo());

        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.All, "All"));
        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.Case, "Case"));
        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.Pcb, "PCB"));
        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.Plate, "Plate"));
        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.Switch, "Switch"));
        ComponentCategories.Add(new ComponentCategoryOptionViewModel(ComponentCategory.Mod, "Mod"));
        UpdateCategoryStates();
    }

    public ObservableCollection<KeyboardBuild> Builds { get; } = [];
    public ObservableCollection<BuildRequest> Requests { get; } = [];
    public ObservableCollection<SellerProfile> AvailableSellers { get; } = [];
    public ObservableCollection<Layout> Layouts { get; } = [];
    public ObservableCollection<KeyboardCase> Cases { get; } = [];
    public ObservableCollection<Pcb> Pcbs { get; } = [];
    public ObservableCollection<Plate> Plates { get; } = [];
    public ObservableCollection<KeyboardSwitch> Switches { get; } = [];
    public ObservableCollection<KeycapSet> Keycaps { get; } = [];
    public ObservableCollection<Stabilizer> Stabilizers { get; } = [];
    public ObservableCollection<ComponentCategoryOptionViewModel> ComponentCategories { get; } = [];
    public ObservableCollection<ComponentOptionViewModel> VisibleComponents { get; } = [];
    public ObservableCollection<string> CompatibilityMessages { get; } = [];
    public ObservableCollection<BuildModEditorViewModel> Mods { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshAllCommand { get; }
    public ICommand ShowHomeCommand { get; }
    public ICommand ShowBuildListCommand { get; }
    public ICommand ShowSentRequestsCommand { get; }
    public ICommand NewBuildCommand { get; }
    public ICommand OpenBuildDetailCommand { get; }
    public ICommand LoadSelectedBuildCommand { get; }
    public ICommand SaveBuildCommand { get; }
    public ICommand SendRequestCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand SelectComponentCommand { get; }
    public ICommand ClearComponentSearchCommand { get; }
    public ICommand AddModCommand { get; }
    public ICommand RemoveModCommand { get; }
    public ICommand ShowAccountInfoCommand { get; }

    public string UserMenuHeader => $"{CurrentUser.Username} ({CurrentUser.Role})";

    public string SellerEmptyMessage => AvailableSellers.Count == 0
        ? "Chua co seller active/verified de nhan request."
        : string.Empty;

    public Visibility HomeVisibility => CurrentScreen == BuyerDashboardScreen.Home ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BuildListVisibility => CurrentScreen == BuyerDashboardScreen.BuildList ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BuildDetailVisibility => CurrentScreen == BuyerDashboardScreen.BuildDetail ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ConfiguratorVisibility => CurrentScreen == BuyerDashboardScreen.Configurator ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SentRequestsVisibility => CurrentScreen == BuyerDashboardScreen.SentRequests ? Visibility.Visible : Visibility.Collapsed;

    public BuyerDashboardScreen CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (SetProperty(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(BuildListVisibility));
                OnPropertyChanged(nameof(BuildDetailVisibility));
                OnPropertyChanged(nameof(ConfiguratorVisibility));
                OnPropertyChanged(nameof(SentRequestsVisibility));
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
                ScheduleValidationRefresh();
            }
        }
    }

    public string? BuildNotes
    {
        get => _buildNotes;
        set => SetProperty(ref _buildNotes, value);
    }

    public string ComponentSearchText
    {
        get => _componentSearchText;
        set
        {
            if (SetProperty(ref _componentSearchText, value))
            {
                RefreshComponentOptions();
            }
        }
    }

    public string ComponentEmptyMessage { get; private set; } = "Chon layout de xem linh kien kha dung.";

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

    public Layout? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value) && !_suppressSelectionRefresh)
            {
                _ = LoadLayoutComponentsAsync();
            }
        }
    }

    public KeyboardCase? SelectedCase
    {
        get => _selectedCase;
        set
        {
            if (SetProperty(ref _selectedCase, value))
            {
                RefreshComponentOptions();
                ScheduleValidationRefresh();
            }
        }
    }

    public Pcb? SelectedPcb
    {
        get => _selectedPcb;
        set
        {
            if (SetProperty(ref _selectedPcb, value))
            {
                RefreshComponentOptions();
                ScheduleValidationRefresh();
            }
        }
    }

    public Plate? SelectedPlate
    {
        get => _selectedPlate;
        set
        {
            if (SetProperty(ref _selectedPlate, value))
            {
                RefreshComponentOptions();
                ScheduleValidationRefresh();
            }
        }
    }

    public KeyboardSwitch? SelectedSwitch
    {
        get => _selectedSwitch;
        set
        {
            if (SetProperty(ref _selectedSwitch, value))
            {
                RefreshComponentOptions();
                ScheduleValidationRefresh();
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
                ScheduleValidationRefresh();
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
                ScheduleValidationRefresh();
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
            }
        }
    }

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
    }

    private async Task RefreshAllAsync()
    {
        await RefreshCatalogAsync();
        await RefreshBuildsAsync();
        await RefreshSellersAsync();
        await RefreshRequestsAsync();
        RefreshComponentOptions();
    }

    private async Task RefreshCatalogAsync()
    {
        var previousLayoutId = SelectedLayout?.LayoutId;
        var previousSwitchId = SelectedSwitch?.SwitchId;
        var previousKeycapId = SelectedKeycap?.KeycapId;
        var previousStabilizerId = SelectedStabilizer?.StabilizerId;

        _suppressSelectionRefresh = true;
        try
        {
            Layouts.Clear();
            foreach (var layout in await _catalogService.GetLayoutsAsync())
            {
                Layouts.Add(layout);
            }

            Switches.Clear();
            foreach (var keyboardSwitch in await _catalogService.GetAvailableSwitchesAsync())
            {
                Switches.Add(keyboardSwitch);
            }

            Keycaps.Clear();
            foreach (var keycap in await _catalogService.GetAvailableKeycapSetsAsync())
            {
                Keycaps.Add(keycap);
            }

            Stabilizers.Clear();
            foreach (var stabilizer in await _catalogService.GetAvailableStabilizersAsync())
            {
                Stabilizers.Add(stabilizer);
            }

            SelectedLayout = FindById(Layouts, previousLayoutId, item => item.LayoutId) ?? Layouts.FirstOrDefault();
            SelectedSwitch = FindById(Switches, previousSwitchId, item => item.SwitchId);
            SelectedKeycap = FindById(Keycaps, previousKeycapId, item => item.KeycapId);
            SelectedStabilizer = FindById(Stabilizers, previousStabilizerId, item => item.StabilizerId);
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        await LoadLayoutComponentsAsync();
    }

    private async Task LoadLayoutComponentsAsync()
    {
        try
        {
            var selectedLayout = SelectedLayout;
            var previousCaseId = SelectedCase?.CaseId;
            var previousPcbId = SelectedPcb?.PcbId;
            var previousPlateId = SelectedPlate?.PlateId;

            _suppressSelectionRefresh = true;
            try
            {
                Cases.Clear();
                Pcbs.Clear();
                Plates.Clear();

                if (selectedLayout is not null)
                {
                    foreach (var keyboardCase in await _catalogService.GetCasesForLayoutAsync(selectedLayout.LayoutId))
                    {
                        Cases.Add(keyboardCase);
                    }

                    foreach (var pcb in await _catalogService.GetPcbsForLayoutAsync(selectedLayout.LayoutId))
                    {
                        Pcbs.Add(pcb);
                    }

                    foreach (var plate in await _catalogService.GetPlatesForLayoutAsync(selectedLayout.LayoutId))
                    {
                        Plates.Add(plate);
                    }
                }

                SelectedCase = FindById(Cases, previousCaseId, item => item.CaseId);
                SelectedPcb = FindById(Pcbs, previousPcbId, item => item.PcbId);
                SelectedPlate = FindById(Plates, previousPlateId, item => item.PlateId);
            }
            finally
            {
                _suppressSelectionRefresh = false;
            }

            RefreshComponentOptions();
            await RefreshValidationPreviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshBuildsAsync()
    {
        Builds.Clear();
        foreach (var build in await _buildService.GetBuyerBuildsAsync(CurrentUser.UserId))
        {
            Builds.Add(build);
        }
    }

    private async Task RefreshRequestsAsync()
    {
        Requests.Clear();
        foreach (var request in await _requestService.GetBuyerRequestsAsync(CurrentUser.UserId))
        {
            Requests.Add(request);
        }
    }

    private async Task RefreshSellersAsync()
    {
        var previousSellerId = SelectedSeller?.UserId;

        AvailableSellers.Clear();
        foreach (var seller in await _requestService.GetAvailableSellersAsync(CurrentUser.UserId))
        {
            AvailableSellers.Add(seller);
        }

        SelectedSeller = AvailableSellers.FirstOrDefault(item => item.UserId == previousSellerId);
        OnPropertyChanged(nameof(SellerEmptyMessage));
    }

    private async Task SaveBuildAsync()
    {
        var build = CreateBuildFromCurrentSelection();
        var saved = await _buildService.SaveBuildAsync(build);
        _editingBuildId = saved.BuildId;
        _editingCreatedAt = saved.CreatedAt;
        await RefreshBuildsAsync();
        SelectedBuild = Builds.FirstOrDefault(item => Same(item.BuildId, saved.BuildId));
        StatusMessage = $"Da luu build '{saved.Name}' voi tong gia {saved.TotalCostSnapshot:F2} USD.";
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

    private async Task LoadSelectedBuildAsync()
    {
        if (SelectedBuild is null)
        {
            throw new InvalidOperationException("Chon build truoc.");
        }

        var build = SelectedBuild;
        _editingBuildId = build.BuildId;
        _editingCreatedAt = build.CreatedAt;

        _suppressSelectionRefresh = true;
        try
        {
            BuildName = build.Name;
            BuildNotes = build.Notes;
            SelectedLayout = FindById(Layouts, build.LayoutId, item => item.LayoutId);
            SelectedSwitch = FindById(Switches, build.SwitchId, item => item.SwitchId);
            SelectedKeycap = FindById(Keycaps, build.KeycapId, item => item.KeycapId);
            SelectedStabilizer = FindById(Stabilizers, build.StabilizerId, item => item.StabilizerId);
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        await LoadLayoutComponentsAsync();

        _suppressSelectionRefresh = true;
        try
        {
            SelectedCase = FindById(Cases, build.CaseId, item => item.CaseId);
            SelectedPcb = FindById(Pcbs, build.PcbId, item => item.PcbId);
            SelectedPlate = FindById(Plates, build.PlateId, item => item.PlateId);

            Mods.Clear();
            foreach (var mod in build.Mods)
            {
                Mods.Add(new BuildModEditorViewModel
                {
                    ModType = mod.ModType,
                    TargetComponent = mod.TargetComponent,
                    LubeType = mod.LubeType,
                    IsFilmed = mod.IsFilmed,
                    SpringWeightG = mod.SpringWeightG,
                    Notes = mod.Notes
                });
            }
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        RefreshComponentOptions();
        SetScreen(BuyerDashboardScreen.Configurator);
        await RefreshValidationPreviewAsync();
    }

    private void StartNewBuild()
    {
        ResetConfigurator();
        SetScreen(BuyerDashboardScreen.Configurator);
    }

    private void ResetConfigurator()
    {
        _suppressSelectionRefresh = true;
        try
        {
            _editingBuildId = null;
            _editingCreatedAt = default;
            BuildName = string.Empty;
            BuildNotes = null;
            SelectedCase = null;
            SelectedPcb = null;
            SelectedPlate = null;
            SelectedSwitch = null;
            SelectedKeycap = null;
            SelectedStabilizer = null;
            SelectedBuild = null;
            SelectedMod = null;
            ComponentSearchText = string.Empty;
            _selectedComponentCategory = ComponentCategory.All;
            Mods.Clear();
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        UpdateCategoryStates();
        RefreshComponentOptions();
        StatusMessage = "Nhap cau hinh build moi.";
        _ = RefreshValidationPreviewAsync();
    }

    private void OpenBuildDetail(object? parameter)
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

        SetScreen(BuyerDashboardScreen.BuildDetail);
    }

    private void SelectCategory(object? parameter)
    {
        if (parameter is ComponentCategoryOptionViewModel option)
        {
            _selectedComponentCategory = option.Category;
        }
        else if (parameter is ComponentCategory category)
        {
            _selectedComponentCategory = category;
        }

        UpdateCategoryStates();
        RefreshComponentOptions();
    }

    private void SelectComponent(object? parameter)
    {
        if (parameter is not ComponentOptionViewModel option)
        {
            return;
        }

        switch (option.Category)
        {
            case ComponentCategory.Case when option.Source is KeyboardCase keyboardCase:
                SelectedCase = keyboardCase;
                break;
            case ComponentCategory.Pcb when option.Source is Pcb pcb:
                SelectedPcb = pcb;
                break;
            case ComponentCategory.Plate when option.Source is Plate plate:
                SelectedPlate = plate;
                break;
            case ComponentCategory.Switch when option.Source is KeyboardSwitch keyboardSwitch:
                SelectedSwitch = keyboardSwitch;
                break;
            case ComponentCategory.Mod:
                AddMod();
                break;
        }
    }

    private void AddMod()
    {
        var mod = new BuildModEditorViewModel();
        Mods.Add(mod);
        SelectedMod = mod;
        ScheduleValidationRefresh();
    }

    private void RemoveSelectedMod()
    {
        if (SelectedMod is null)
        {
            return;
        }

        Mods.Remove(SelectedMod);
        SelectedMod = null;
        ScheduleValidationRefresh();
    }

    private async Task RefreshValidationPreviewAsync()
    {
        if (_suppressSelectionRefresh)
        {
            return;
        }

        var version = ++_validationVersion;
        var build = CreateBuildFromCurrentSelection();

        try
        {
            var validation = await _buildService.ValidateBuildAsync(build);
            if (version != _validationVersion)
            {
                return;
            }

            ApplyValidationResult(validation);
        }
        catch (Exception ex)
        {
            if (version != _validationVersion)
            {
                return;
            }

            CompatibilityMessages.Clear();
            CompatibilityMessages.Add(ex.Message);
            TotalPreview = 0m;
            CanSaveBuild = false;
        }
    }

    private void ApplyValidationResult(BuildValidationResult validation)
    {
        CompatibilityMessages.Clear();
        foreach (var message in validation.Messages)
        {
            CompatibilityMessages.Add(message);
        }

        TotalPreview = validation.TotalCost;
        CanSaveBuild = validation.IsValid;
    }

    private KeyboardBuild CreateBuildFromCurrentSelection()
    {
        return new KeyboardBuild
        {
            BuildId = _editingBuildId ?? string.Empty,
            CreatedAt = _editingCreatedAt,
            UserId = CurrentUser.UserId,
            LayoutId = SelectedLayout?.LayoutId ?? string.Empty,
            CaseId = SelectedCase?.CaseId,
            PcbId = SelectedPcb?.PcbId,
            PlateId = SelectedPlate?.PlateId,
            SwitchId = SelectedSwitch?.SwitchId,
            KeycapId = SelectedKeycap?.KeycapId,
            StabilizerId = SelectedStabilizer?.StabilizerId,
            Name = BuildName,
            Notes = BuildNotes,
            Mods = Mods.Select(mod => mod.ToBuildMod()).ToList()
        };
    }

    private void RefreshComponentOptions()
    {
        VisibleComponents.Clear();

        if (SelectedLayout is null)
        {
            SetComponentEmptyMessage("Chon layout de xem linh kien kha dung.");
            return;
        }

        var options = BuildComponentOptions();
        var searchText = ComponentSearchText.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            options = options
                .Where(option => option.SearchText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var option in options)
        {
            option.IsSelected = IsSelectedComponent(option);
            VisibleComponents.Add(option);
        }

        SetComponentEmptyMessage(VisibleComponents.Count == 0
            ? "Khong co linh kien phu hop voi bo loc hien tai."
            : string.Empty);
    }

    private List<ComponentOptionViewModel> BuildComponentOptions()
    {
        var options = new List<ComponentOptionViewModel>();
        var category = _selectedComponentCategory;

        if (category is ComponentCategory.All or ComponentCategory.Case)
        {
            options.AddRange(Cases.Select(item => new ComponentOptionViewModel(
                ComponentCategory.Case,
                item,
                item.CaseId,
                item.CaseId,
                $"{item.Material} - {item.MountType}",
                $"Mau {item.Color} - {item.WeightG}g",
                item.PriceUsd,
                "Case")));
        }

        if (category is ComponentCategory.All or ComponentCategory.Pcb)
        {
            options.AddRange(Pcbs.Select(item => new ComponentOptionViewModel(
                ComponentCategory.Pcb,
                item,
                item.PcbId,
                item.PcbId,
                $"{item.PcbTechnology} - switch {item.SwitchMount}",
                $"{(item.Hotswap ? "Hotswap" : "Solder")} - {(item.Wireless ? "Wireless" : "Wired")} - {(item.Rgb ? "RGB" : "No RGB")}",
                item.PriceUsd,
                "PCB")));
        }

        if (category is ComponentCategory.All or ComponentCategory.Plate)
        {
            options.AddRange(Plates.Select(item => new ComponentOptionViewModel(
                ComponentCategory.Plate,
                item,
                item.PlateId,
                item.PlateId,
                $"{item.Material} - {item.MountType}",
                item.FlexCut,
                item.PriceUsd,
                "Plate")));
        }

        if (category is ComponentCategory.All or ComponentCategory.Switch)
        {
            options.AddRange(Switches.Select(item => new ComponentOptionViewModel(
                ComponentCategory.Switch,
                item,
                item.SwitchId,
                item.SwitchId,
                $"{item.SwitchTechnology} - {item.MountType}",
                $"{item.SwitchType} - {item.ActuationForceG}g - {item.SoundProfile}",
                item.PriceUsd,
                "Switch")));
        }

        if (category is ComponentCategory.Mod)
        {
            options.Add(new ComponentOptionViewModel(
                ComponentCategory.Mod,
                null,
                "MOD_EDITOR",
                "Them mod co ban",
                "Lube, film, spring hoac ghi chu mod",
                "Bam de them mot dong mod vao build",
                0m,
                "Mod"));
        }

        return options;
    }

    private bool IsSelectedComponent(ComponentOptionViewModel option)
    {
        return option.Category switch
        {
            ComponentCategory.Case => Same(option.Id, SelectedCase?.CaseId),
            ComponentCategory.Pcb => Same(option.Id, SelectedPcb?.PcbId),
            ComponentCategory.Plate => Same(option.Id, SelectedPlate?.PlateId),
            ComponentCategory.Switch => Same(option.Id, SelectedSwitch?.SwitchId),
            _ => false
        };
    }

    private void UpdateCategoryStates()
    {
        foreach (var category in ComponentCategories)
        {
            category.IsSelected = category.Category == _selectedComponentCategory;
        }
    }

    private void SetComponentEmptyMessage(string message)
    {
        if (ComponentEmptyMessage == message)
        {
            return;
        }

        ComponentEmptyMessage = message;
        OnPropertyChanged(nameof(ComponentEmptyMessage));
    }

    private void ShowAccountInfo()
    {
        StatusMessage = $"Tai khoan: {CurrentUser.Username} | {CurrentUser.Email} | {CurrentUser.Phone} | {CurrentUser.Role}";
    }

    private void SetScreen(BuyerDashboardScreen screen)
    {
        CurrentScreen = screen;
    }

    private void ScheduleValidationRefresh()
    {
        if (!_suppressSelectionRefresh)
        {
            _ = RefreshValidationPreviewAsync();
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
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStatesChanged()
    {
        if (SaveBuildCommand is AsyncRelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }

        if (LoadSelectedBuildCommand is AsyncRelayCommand loadBuildCommand)
        {
            loadBuildCommand.RaiseCanExecuteChanged();
        }

        if (SendRequestCommand is AsyncRelayCommand sendRequestCommand)
        {
            sendRequestCommand.RaiseCanExecuteChanged();
        }

        if (RemoveModCommand is RelayCommand removeModCommand)
        {
            removeModCommand.RaiseCanExecuteChanged();
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
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

public enum BuyerDashboardScreen
{
    Home,
    BuildList,
    BuildDetail,
    Configurator,
    SentRequests
}

public enum ComponentCategory
{
    All,
    Case,
    Pcb,
    Plate,
    Switch,
    Mod
}

public sealed class ComponentCategoryOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    public ComponentCategoryOptionViewModel(ComponentCategory category, string label)
    {
        Category = category;
        Label = label;
    }

    public ComponentCategory Category { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ComponentOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    public ComponentOptionViewModel(
        ComponentCategory category,
        object? source,
        string id,
        string title,
        string subtitle,
        string details,
        decimal priceUsd,
        string categoryLabel)
    {
        Category = category;
        Source = source;
        Id = id;
        Title = title;
        Subtitle = subtitle;
        Details = details;
        PriceUsd = priceUsd;
        CategoryLabel = categoryLabel;
        PriceText = priceUsd > 0m ? $"{priceUsd:F2} USD" : string.Empty;
        SearchText = $"{id} {title} {subtitle} {details} {categoryLabel}";
    }

    public ComponentCategory Category { get; }
    public object? Source { get; }
    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Details { get; }
    public decimal PriceUsd { get; }
    public string PriceText { get; }
    public string CategoryLabel { get; }
    public string SearchText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
