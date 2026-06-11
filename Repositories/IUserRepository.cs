using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default);
    Task<User?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);
    Task SetRoleAsync(int userId, UserRole role, CancellationToken cancellationToken = default);
}
