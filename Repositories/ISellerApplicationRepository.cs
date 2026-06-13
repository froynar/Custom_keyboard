using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Repositories;

public interface ISellerApplicationRepository
{
    Task<SellerApplication> AddAsync(SellerApplication application, CancellationToken cancellationToken = default);
    Task<SellerApplication?> GetByIdAsync(int applicationId, CancellationToken cancellationToken = default);

    /// <summary>The buyer's most recent application (any status), for showing their current state.</summary>
    Task<SellerApplication?> GetLatestByBuyerAsync(int buyerUserId, CancellationToken cancellationToken = default);

    Task<bool> HasPendingAsync(int buyerUserId, CancellationToken cancellationToken = default);

    /// <summary>All applications joined with the applicant account, pending first, for the admin queue.</summary>
    Task<IReadOnlyList<AdminSellerApplicationRow>> GetAdminRowsAsync(CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(
        int applicationId,
        SellerApplicationStatus status,
        string? reviewNote,
        int reviewedBy,
        CancellationToken cancellationToken = default);
}

public enum SellerApplicationReviewOutcome
{
    Success,
    AlreadyProcessed,
    InvalidApplicant
}

public interface ITransactionalSellerApplicationRepository
{
    Task<SellerApplicationReviewOutcome> ApprovePendingAsync(
        SellerApplication application,
        int adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default);

    Task<SellerApplicationReviewOutcome> RejectPendingAsync(
        SellerApplication application,
        int adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default);
}
