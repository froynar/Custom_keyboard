using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;
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
    private ViewModelBase _currentViewModel = null!;
    private User? _currentUser;

    public MainShellViewModel(
        IAccountService accountService,
        IAdminService adminService,
        IComponentCatalogService componentCatalogService,
        IBuildService buildService,
        IRequestService requestService,
        IChatService chatService)
    {
        _accountService = accountService;
        _adminService = adminService;
        _componentCatalogService = componentCatalogService;
        _buildService = buildService;
        _requestService = requestService;
        _chatService = chatService;
        LogoutCommand = new RelayCommand(_ => Logout(), _ => CurrentUser is not null);
        ShowLogin();
    }

    public string CurrentEnvironmentLabel => "WPF + SQL Server + Auth";

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
        ? "Not signed in"
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
        CurrentViewModel = user.Role switch
        {
            UserRole.Seller => new SellerDashboardViewModel(user, LogoutCommand, _requestService, chat),
            UserRole.Admin => new AdminDashboardViewModel(user, LogoutCommand, _adminService, chat),
            _ => new BuyerDashboardViewModel(
                user,
                LogoutCommand,
                _componentCatalogService,
                _buildService,
                _requestService,
                chat)
        };
    }

    private void Logout()
    {
        _accountService.Logout();
        CurrentUser = null;
        ShowLogin("Da dang xuat.");
    }
}
