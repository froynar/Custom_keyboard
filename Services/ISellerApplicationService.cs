using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;

namespace Custom_keyboard.Services;

public interface ISellerApplicationService
{
    /// <summary>Buyer submits an application to become a seller (one Pending per buyer).</summary>
    Task<SellerApplication> SubmitAsync(
        int buyerUserId,
        string shopName,
        string phone,
        string address,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>The buyer's most recent application (to show its status), or null if none.</summary>
    Task<SellerApplication?> GetMyApplicationAsync(int buyerUserId, CancellationToken cancellationToken = default);

    /// <summary>Admin review queue: all applications joined with the applicant account.</summary>
    Task<IReadOnlyList<AdminSellerApplicationRow>> GetApplicationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin approves: buyer becomes a verified Seller (role + seller_profile) and the application is closed.</summary>
    Task ApproveAsync(int applicationId, int adminUserId, string? reviewNote, CancellationToken cancellationToken = default);

    /// <summary>Admin rejects: role unchanged, application marked Rejected.</summary>
    Task RejectAsync(int applicationId, int adminUserId, string? reviewNote, CancellationToken cancellationToken = default);
}
