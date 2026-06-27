using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly Action<User> _loginSucceeded;
    private string _emailOrUsername = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage;

    public LoginViewModel(
        IAccountService accountService,
        Action<User> loginSucceeded,
        Action showRegister,
        string statusMessage = "")
    {
        _accountService = accountService;
        _loginSucceeded = loginSucceeded;
        _statusMessage = statusMessage;
        LoginCommand = new AsyncRelayCommand(_ => LoginAsync());
        QuickLoginCommand = new AsyncRelayCommand(LoginQuickAsync);
        ShowRegisterCommand = new RelayCommand(_ => showRegister());
    }

    public string EmailOrUsername
    {
        get => _emailOrUsername;
        set => SetProperty(ref _emailOrUsername, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand QuickLoginCommand { get; }
    public ICommand ShowRegisterCommand { get; }

    public IReadOnlyList<QuickLoginAccount> QuickLoginAccounts { get; } =
    [
        new("buyer_refactor", "Buyer"),
        new("seller_soigear", "Seller"),
        new("admin_refactor", "Admin")
    ];

    private async Task LoginQuickAsync(object? parameter)
    {
        if (parameter is not string username || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        EmailOrUsername = username;
        Password = "Password123";
        await LoginAsync();
    }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        var result = await _accountService.LoginAsync(EmailOrUsername, Password);
        if (result.Succeeded && result.User is not null)
        {
            Password = string.Empty;
            _loginSucceeded(result.User);
            return;
        }

        ErrorMessage = result.Message;
    }
}

public sealed record QuickLoginAccount(string Username, string Role)
{
    public string Label => $"{Username} ({Role})";
}
