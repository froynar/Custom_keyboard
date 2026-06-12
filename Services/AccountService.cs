using System.Text.RegularExpressions;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;
using Custom_keyboard.Services.Security;

namespace Custom_keyboard.Services;

public sealed class AccountService : IAccountService
{
    // Pragmatic format checks: one "@", a dotted domain, and 8-15 phone digits (optional +).
    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PhonePattern = new(
        @"^\+?\d{8,15}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AccountService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public User? CurrentUser { get; private set; }

    public async Task<AccountResult> LoginAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default)
    {
        emailOrUsername = emailOrUsername.Trim();
        if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
        {
            return AccountResult.Failure(
                AccountOperationStatus.ValidationError,
                "Nhap email/username va mat khau.");
        }

        var user = await _userRepository.FindByEmailOrUsernameAsync(emailOrUsername, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return AccountResult.Failure(
                AccountOperationStatus.InvalidCredentials,
                "Email/username hoac mat khau khong dung.");
        }

        if (!user.IsActive)
        {
            return AccountResult.Failure(
                AccountOperationStatus.InactiveUser,
                "Tai khoan dang bi khoa.");
        }

        CurrentUser = user;
        return AccountResult.Success(user);
    }

    public async Task<AccountResult> RegisterBuyerAsync(
        string username,
        string email,
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = username.Trim();
        email = email.Trim();
        // Normalize away common separators so "090 123 4567" and "090-123-4567" validate and
        // de-duplicate consistently with what we store.
        phone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(password))
        {
            return AccountResult.Failure(
                AccountOperationStatus.ValidationError,
                "Nhap day du username, email, phone va mat khau.");
        }

        if (password.Length < 6)
        {
            return AccountResult.Failure(
                AccountOperationStatus.ValidationError,
                "Mat khau can it nhat 6 ky tu.");
        }

        if (!EmailPattern.IsMatch(email))
        {
            return AccountResult.Failure(
                AccountOperationStatus.ValidationError,
                "Email khong dung dinh dang (vd: ten@domain.com).");
        }

        if (!PhonePattern.IsMatch(phone))
        {
            return AccountResult.Failure(
                AccountOperationStatus.ValidationError,
                "So dien thoai khong hop le (8-15 chu so, co the bat dau bang +).");
        }

        if (await _userRepository.FindByUsernameAsync(username, cancellationToken) is not null)
        {
            return AccountResult.Failure(AccountOperationStatus.DuplicateUsername, "Username da ton tai.");
        }

        if (await _userRepository.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return AccountResult.Failure(AccountOperationStatus.DuplicateEmail, "Email da ton tai.");
        }

        if (await _userRepository.FindByPhoneAsync(phone, cancellationToken) is not null)
        {
            return AccountResult.Failure(AccountOperationStatus.DuplicatePhone, "Phone da ton tai.");
        }

        var user = new User
        {
            Role = UserRole.Buyer,
            Username = username,
            Email = email,
            Phone = phone,
            PasswordHash = _passwordHasher.HashPassword(password),
            IsActive = true
        };

        var created = await _userRepository.AddAsync(user, cancellationToken);
        return AccountResult.Success(created, "Dang ky thanh cong. Hay dang nhap.");
    }

    public Task<User?> GetAccountInfoAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetByIdAsync(userId, cancellationToken);
    }

    public void Logout()
    {
        CurrentUser = null;
    }

    private static string NormalizePhone(string phone)
    {
        // Keep a leading '+' and digits; drop spaces, dashes, dots, and parentheses.
        return string.Concat(phone.Where(c => char.IsDigit(c) || c == '+')).Trim();
    }
}
