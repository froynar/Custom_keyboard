using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Realtime;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly IAdminService _adminService;
    private readonly IComponentCatalogService _componentCatalogService;
    private readonly IBuildService _buildService;
    private readonly IRequestService _requestService;
    private readonly IChatService _chatService;
    private readonly IStatsService _statsService;
    private readonly ISellerApplicationService _sellerApplicationService;
    private readonly IRealtimeSubscriber _realtime;
    private ViewModelBase _currentViewModel = null!;
    private User? _currentUser;
    private Func<Task>? _realtimeReload;

    public MainShellViewModel(
        IAccountService accountService,
        IAdminService adminService,
        IComponentCatalogService componentCatalogService,
        IBuildService buildService,
        IRequestService requestService,
        IChatService chatService,
        IStatsService statsService,
        ISellerApplicationService sellerApplicationService,
        IRealtimeSubscriber realtime)
    {
        _accountService = accountService;
        _adminService = adminService;
        _componentCatalogService = componentCatalogService;
        _buildService = buildService;
        _requestService = requestService;
        _chatService = chatService;
        _statsService = statsService;
        _sellerApplicationService = sellerApplicationService;
        _realtime = realtime;
        _realtime.SellerRequestsChanged += OnRealtimeReloadAsync;
        _realtime.BuyerRequestsChanged += OnRealtimeReloadAsync;
        LogoutCommand = new RelayCommand(_ => Logout(), _ => CurrentUser is not null);
        ShowLogin();
    }

    public string CurrentEnvironmentLabel => Tr("Common_Environment");

    public User? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(CurrentUserLabel));
                if (LogoutCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string CurrentUserLabel => CurrentUser is null
        ? Tr("Common_NotSignedIn")
        : $"{CurrentUser.Username} - {CurrentUser.Role}";

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public ICommand LogoutCommand { get; }

    private void ShowLogin(string message = "")
    {
        CurrentViewModel = new LoginViewModel(
            _accountService,
            HandleLoginSucceeded,
            ShowRegister,
            message);
    }

    private void ShowRegister()
    {
        CurrentViewModel = new RegisterViewModel(
            _accountService,
            message => ShowLogin(message));
    }

    private void HandleLoginSucceeded(User user)
    {
        CurrentUser = user;
        var chat = new ChatViewModel(_chatService, _requestService, user);
        switch (user.Role)
        {
            case UserRole.Seller:
                var seller = new SellerDashboardViewModel(user, LogoutCommand, _requestService, _statsService, chat);
                _realtimeReload = seller.ReloadRequestsAsync;
                CurrentViewModel = seller;
                break;
            case UserRole.Admin:
                _realtimeReload = null;
                CurrentViewModel = new AdminDashboardViewModel(user, LogoutCommand, _adminService, _statsService, _sellerApplicationService, chat);
                break;
            default:
                var buyer = new BuyerDashboardViewModel(
                    user,
                    LogoutCommand,
                    _componentCatalogService,
                    _buildService,
                    _requestService,
                    _statsService,
                    _sellerApplicationService,
                    chat);
                _realtimeReload = buyer.ReloadRequestsAsync;
                CurrentViewModel = buyer;
                break;
        }

        // Best-effort: connect + subscribe to this user's realtime topics.
        _ = _realtime.StartAsync(user);
    }

    private void Logout()
    {
        _accountService.Logout();
        _realtimeReload = null;
        _ = _realtime.StopAsync();
        CurrentUser = null;
        ShowLogin(Tr("Account_LoggedOut"));
    }

    // Realtime events arrive on a background thread; marshal the dashboard reload to the UI.
    private async Task OnRealtimeReloadAsync()
    {
        var reload = _realtimeReload;
        if (reload is null)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            await reload();
        }
        else
        {
            await dispatcher.InvokeAsync(reload).Task.Unwrap();
        }
    }
}
