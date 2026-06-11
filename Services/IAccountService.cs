using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.Services;

public interface IAccountService
{
    User? CurrentUser { get; }

    Task<AccountResult> LoginAsync(string emailOrUsername, string password, CancellationToken cancellationToken = default);
    Task<AccountResult> RegisterBuyerAsync(string username, string email, string phone, string password, CancellationToken cancellationToken = default);
    Task<User?> GetAccountInfoAsync(int userId, CancellationToken cancellationToken = default);
    void Logout();
}
