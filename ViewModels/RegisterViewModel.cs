using System.Windows.Input;
using Custom_keyboard.Commands;
using Custom_keyboard.Services;

namespace Custom_keyboard.ViewModels;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly Action<string> _showLogin;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;

    public RegisterViewModel(IAccountService accountService, Action<string> showLogin)
    {
        _accountService = accountService;
        _showLogin = showLogin;
        RegisterCommand = new AsyncRelayCommand(_ => RegisterAsync());
        ShowLoginCommand = new RelayCommand(_ => _showLogin(string.Empty));
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand RegisterCommand { get; }
    public ICommand ShowLoginCommand { get; }

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Mat khau xac nhan khong khop.";
            return;
        }

        var result = await _accountService.RegisterBuyerAsync(Username, Email, Phone, Password);
        if (result.Succeeded)
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            _showLogin(result.Message);
            return;
        }

        ErrorMessage = result.Message;
    }
}
