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
    public ICommand ShowRegisterCommand { get; }

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
