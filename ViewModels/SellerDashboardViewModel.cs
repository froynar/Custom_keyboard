using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Custom_keyboard.Analytics;
using Custom_keyboard.Commands;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Devices;
using Custom_keyboard.Services.Stats;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Custom_keyboard.ViewModels;

// Seller flow: see assigned build requests, read the build snapshot, move the request
// through its state machine (Accept -> In_progress -> Completed, or Cancel), and view analytics.
public sealed class SellerDashboardViewModel : RoleDashboardViewModel
{
    private readonly IRequestService _requestService;
    private readonly IStatsService _statsService;
    private readonly IDeviceService _deviceService;
    private readonly DeviceSimulator _deviceSimulator;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private bool _hasLoaded;
    private bool _isBusy;
    private int _qcSelectionVersion;
    private string _statusMessage = Tr("Common_Ready");
    private BuildRequest? _selectedRequest;
    private DeviceTestSession? _qcSession;

    private SellerDashboardStats _stats = new();
    private StatsPeriod _selectedPeriod = StatsPeriod.Monthly;
    private ISeries[] _revenueSeries = [];
    private Axis[] _revenueXAxes = [];
    private Axis[] _revenueYAxes = [];
    private ISeries[] _statusSeries = [];

    public SellerDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        IRequestService requestService,
        IStatsService statsService,
        ChatViewModel chat,
        IDeviceService deviceService,
        DeviceSimulator deviceSimulator)
        : base(
            currentUser,
            logoutCommand,
            "Seller_Title",
            "Seller_Subtitle",
            ["Seller_Task1", "Seller_Task2", "Seller_Task3", "Seller_Task4"])
    {
        _requestService = requestService;
        _statsService = statsService;
        _deviceService = deviceService;
        _deviceSimulator = deviceSimulator;
        Chat = chat;

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshSellerAsync, Tr("Seller_RequestsRefreshed")));
        AcceptCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Accepted), _ => CanTransitionTo(RequestStatus.Accepted));
        StartProgressCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.In_progress), _ => CanTransitionTo(RequestStatus.In_progress));
        CompleteCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Completed), _ => CanTransitionTo(RequestStatus.Completed));
        CancelCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Cancelled), _ => CanTransitionTo(RequestStatus.Cancelled));
        StartQcTestCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(StartQcTestAsync, Tr("QcTest_Completed")), _ => CanStartQc());
    }

    public ChatViewModel Chat { get; }

    public ObservableCollection<BuildRequest> Requests { get; } = [];
    public ObservableCollection<KitSales> TopKits { get; } = [];

    public StatsPeriod[] Periods { get; } = Enum.GetValues<StatsPeriod>();

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AcceptCommand { get; }
    public ICommand StartProgressCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StartQcTestCommand { get; }

    public ObservableCollection<DeviceKeyTestResult> KeyResults { get; } = [];

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

    public BuildRequest? SelectedRequest
    {
        get => _selectedRequest;
        set
        {
            if (SetProperty(ref _selectedRequest, value))
            {
                OnPropertyChanged(nameof(SelectedRequestPayload));
                RaiseCommandStatesChanged();
                var loadVersion = Interlocked.Increment(ref _qcSelectionVersion);
                _ = LoadSelectedRequestQcAsync(value?.RequestId, loadVersion);
            }
        }
    }

    public string SelectedRequestPayload => SelectedRequest is null
        ? Tr("Seller_SelectRequestForSnapshot")
        : BuildRequestSnapshotFormatter.Format(SelectedRequest.RequestPayloadJson);

    // --- QC test (per selected request) ---

    public DeviceTestSession? QcSession
    {
        get => _qcSession;
        private set
        {
            if (SetProperty(ref _qcSession, value))
            {
                OnPropertyChanged(nameof(QcSummaryVisibility));
                RaiseCommandStatesChanged();
            }
        }
    }

    public Visibility QcSummaryVisibility => _qcSession is null ? Visibility.Collapsed : Visibility.Visible;

    // --- Analytics (read-only, derived from build_requests + builds) ---

    public decimal TotalRevenue => _stats.TotalRevenue;
    public int ProductsMade => _stats.ProductsMade;
    public int TotalCustomers => _stats.TotalCustomers;
    public int InProgressOrders => _stats.InProgressOrders;
    public string AvgCompletionText => _stats.AvgCompletionDays is { } days ? TrFormat("Common_DaysFormat", days) : "-";

    public StatsPeriod SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value) && _hasLoaded)
            {
                _ = ExecuteSafeAsync(LoadStatsAsync, Tr("Seller_ChartUpdated"));
            }
        }
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

    public ISeries[] StatusSeries
    {
        get => _statusSeries;
        private set => SetProperty(ref _statusSeries, value);
    }

    private async Task LoadAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshRequestsAsync();
        await LoadStatsAsync();
        await Chat.InitializeAsync();
    }

    private async Task RefreshSellerAsync()
    {
        await RefreshRequestsAsync();
        await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        var stats = await _statsService.GetSellerDashboardAsync(CurrentUser.UserId, SelectedPeriod);
        _stats = stats;
        OnPropertyChanged(nameof(TotalRevenue));
        OnPropertyChanged(nameof(ProductsMade));
        OnPropertyChanged(nameof(TotalCustomers));
        OnPropertyChanged(nameof(InProgressOrders));
        OnPropertyChanged(nameof(AvgCompletionText));

        RevenueXAxes = ChartFactory.LabelAxis(stats.TimeSeries.Select(bucket => bucket.Label));
        RebuildCharts();

        TopKits.Clear();
        foreach (var kit in stats.TopKits)
        {
            TopKits.Add(kit);
        }
    }

    // Rebuilds the chart series (whose localized labels are baked in at construction time) from the
    // last-loaded stats. Safe to call any time: _stats defaults to an empty snapshot.
    private void RebuildCharts()
    {
        RevenueSeries = ChartFactory.SellerRevenueOrders(_stats.TimeSeries);
        RevenueYAxes = ChartFactory.RevenueOrdersYAxes();
        StatusSeries = ChartFactory.StatusDonut(_stats.StatusBreakdown);
    }

    protected override void OnLanguageChangedCore()
    {
        OnPropertyChanged(nameof(SelectedRequestPayload));
        RebuildCharts();
    }

    /// <summary>Reload requests in response to a realtime "new request" event.</summary>
    public Task ReloadRequestsAsync()
        => ExecuteSafeAsync(RefreshRequestsAsync, Tr("Seller_NewRequestRealtime"));

    private async Task RefreshRequestsAsync()
    {
        var previousId = SelectedRequest?.RequestId;
        var refreshed = await _requestService.GetSellerRequestsAsync(CurrentUser.UserId);

        Requests.Clear();
        foreach (var request in refreshed)
        {
            Requests.Add(request);
        }

        SelectedRequest = Requests.FirstOrDefault(item =>
            string.Equals(item.RequestId, previousId, StringComparison.OrdinalIgnoreCase));
    }

    private Task UpdateStatusAsync(RequestStatus status)
        => ExecuteSafeAsync(async () =>
        {
            if (SelectedRequest is null)
            {
                throw new InvalidOperationException(Tr("Seller_SelectRequestFirst"));
            }

            await _requestService.UpdateStatusAsync(SelectedRequest.RequestId, CurrentUser.UserId, status);
            await RefreshRequestsAsync();
            await LoadStatsAsync();
        }, TrFormat("Seller_RequestStatusUpdated", Tr("Status_" + status)));

    private bool CanTransitionTo(RequestStatus status)
    {
        if (IsBusy || SelectedRequest is null)
        {
            return false;
        }

        return SelectedRequest.Status switch
        {
            RequestStatus.Pending => status is RequestStatus.Accepted or RequestStatus.Cancelled,
            RequestStatus.Accepted => status is RequestStatus.In_progress or RequestStatus.Cancelled,
            RequestStatus.In_progress => status == RequestStatus.Cancelled
                || (status == RequestStatus.Completed && HasAcceptableQcResult()),
            _ => false
        };
    }

    private bool HasAcceptableQcResult()
        => QcSession?.Status is TestSessionStatus.Passed or TestSessionStatus.Warning;

    private bool CanStartQc()
        => !IsBusy && SelectedRequest is { Status: RequestStatus.In_progress };

    // Runs the QC simulation for the selected request (DeviceSimulator owns Start -> Record* -> Complete,
    // publishing over MQTT with an in-process fallback) and reloads the persisted results into the VM.
    private async Task StartQcTestAsync()
    {
        var request = SelectedRequest ?? throw new InvalidOperationException(Tr("Seller_SelectRequestFirst"));
        await _deviceSimulator.RunQcTestAsync(request);
        await LoadQcAsync(request.RequestId, requireCurrentSelection: true);
        StatusMessage = _deviceSimulator.LastStatusMessage ?? Tr("QcTest_Completed");
    }

    private async Task LoadQcAsync(string requestId, bool requireCurrentSelection = false)
    {
        var session = await _deviceService.GetLatestSessionByRequestAsync(requestId);
        IReadOnlyList<DeviceKeyTestResult> results = [];
        if (session is not null)
        {
            results = await _deviceService.GetKeyResultsAsync(session.SessionId);
        }

        if (requireCurrentSelection
            && !string.Equals(SelectedRequest?.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        QcSession = session;
        KeyResults.Clear();
        foreach (var result in results)
        {
            KeyResults.Add(result);
        }
    }

    // A selection can change while SQL is loading. Only the newest selection may update the QC panel.
    private async Task LoadSelectedRequestQcAsync(string? requestId, int loadVersion)
    {
        try
        {
            if (requestId is null)
            {
                if (loadVersion == Volatile.Read(ref _qcSelectionVersion))
                {
                    QcSession = null;
                    KeyResults.Clear();
                }

                return;
            }

            var session = await _deviceService.GetLatestSessionByRequestAsync(requestId);
            IReadOnlyList<DeviceKeyTestResult> results = session is null
                ? []
                : await _deviceService.GetKeyResultsAsync(session.SessionId);

            if (loadVersion != Volatile.Read(ref _qcSelectionVersion)
                || !string.Equals(SelectedRequest?.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            QcSession = session;
            KeyResults.Clear();
            foreach (var result in results)
            {
                KeyResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("SellerDashboard.LoadQc", ex);
            if (loadVersion == Volatile.Read(ref _qcSelectionVersion)
                && string.Equals(SelectedRequest?.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            {
                QcSession = null;
                KeyResults.Clear();
                StatusMessage = AppLog.ToUserMessage(ex);
            }
        }
    }

    private async Task ExecuteSafeAsync(Func<Task> action, string? successMessage = null)
    {
        await _operationGate.WaitAsync();
        var processingMessage = Tr("Common_Processing");
        IsBusy = true;
        StatusMessage = processingMessage;

        try
        {
            await action();
            if (string.Equals(StatusMessage, processingMessage, StringComparison.Ordinal))
            {
                StatusMessage = successMessage ?? Tr("Common_Done");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("SellerDashboard", ex);
            StatusMessage = AppLog.ToUserMessage(ex);
        }
        finally
        {
            IsBusy = false;
            _operationGate.Release();
        }
    }

    private void RaiseCommandStatesChanged()
    {
        (AcceptCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (StartProgressCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (StartQcTestCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
