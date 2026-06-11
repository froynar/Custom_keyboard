using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.Services;

public sealed class AccountResult
{
    private AccountResult(AccountOperationStatus status, User? user, string message)
    {
        Status = status;
        User = user;
        Message = message;
    }

    public AccountOperationStatus Status { get; }
    public User? User { get; }
    public string Message { get; }
    public bool Succeeded => Status == AccountOperationStatus.Success;

    public static AccountResult Success(User user, string message = "")
    {
        return new AccountResult(AccountOperationStatus.Success, user, message);
    }

    public static AccountResult Failure(AccountOperationStatus status, string message)
    {
        return new AccountResult(status, null, message);
    }
}
