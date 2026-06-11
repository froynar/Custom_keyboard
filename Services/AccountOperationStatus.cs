namespace Custom_keyboard.Services;

public enum AccountOperationStatus
{
    Success,
    ValidationError,
    InvalidCredentials,
    InactiveUser,
    DuplicateUsername,
    DuplicateEmail,
    DuplicatePhone
}
