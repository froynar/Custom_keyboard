using System.Collections.ObjectModel;
using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

// Seller flow: see assigned build requests, read the build snapshot, and move the request
// through its state machine (Accept -> In_progress -> Completed, or Cancel).
public sealed class SellerDashboardViewModel : RoleDashboardViewModel
{
    private readonly IRequestService _requestService;
    private bool _hasLoaded;
    private bool _isBusy;
    private string _statusMessage = "San sang.";
    private BuildRequest? _selectedRequest;

    public SellerDashboardViewModel(
        User currentUser,
        ICommand logoutCommand,
        IRequestService requestService,
        ChatViewModel chat)
        : base(
            currentUser,
            logoutCommand,
            "Seller dashboard",
            "Xu ly request build duoc gan cho seller.",
            [
                "Xem danh sach request moi",
                "Mo chi tiet build snapshot",
                "Cap nhat trang thai request",
                "Chat voi buyer va admin"
            ])
    {
        _requestService = requestService;
        Chat = chat;

        LoadCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(LoadAsync));
        RefreshCommand = new AsyncRelayCommand(_ => ExecuteSafeAsync(RefreshRequestsAsync, "Da refresh request."));
        AcceptCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Accepted), _ => CanTransitionTo(RequestStatus.Accepted));
        StartProgressCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.In_progress), _ => CanTransitionTo(RequestStatus.In_progress));
        CompleteCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Completed), _ => CanTransitionTo(RequestStatus.Completed));
        CancelCommand = new AsyncRelayCommand(_ => UpdateStatusAsync(RequestStatus.Cancelled), _ => CanTransitionTo(RequestStatus.Cancelled));
    }

    public ChatViewModel Chat { get; }

    public ObservableCollection<BuildRequest> Requests { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AcceptCommand { get; }
    public ICommand StartProgressCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand CancelCommand { get; }

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
            }
        }
    }

    public string SelectedRequestPayload => SelectedRequest is null
        ? "Chon request de xem snapshot build."
        : SelectedRequest.RequestPayloadJson;

    private async Task LoadAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshRequestsAsync();
        await Chat.InitializeAsync();
    }

    private async Task RefreshRequestsAsync()
    {
        var previousId = SelectedRequest?.RequestId;
        Requests.Clear();
        foreach (var request in await _requestService.GetSellerRequestsAsync(CurrentUser.UserId))
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
                throw new InvalidOperationException("Chon request truoc.");
            }

            await _requestService.UpdateStatusAsync(SelectedRequest.RequestId, CurrentUser.UserId, status);
            await RefreshRequestsAsync();
        }, $"Da cap nhat request sang {status}.");

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
            RequestStatus.In_progress => status is RequestStatus.Completed or RequestStatus.Cancelled,
            _ => false
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

    private void RaiseCommandStatesChanged()
    {
        (AcceptCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (StartProgressCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
