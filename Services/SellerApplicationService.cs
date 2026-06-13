using System.Text.Json;
using Custom_keyboard.Localization;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

// Buyer -> Seller upgrade workflow.
//   Submit:  a buyer (active, role Buyer) opens one Pending application.
//   Approve: admin promotes the buyer to a verified Seller (role + seller_profile) and closes the application.
//   Reject:  admin marks the application Rejected; the user's role is untouched.
// Approved buyers must sign in again to land on the seller dashboard (the session role is not
// revalidated mid-flight - same as the rest of the app).
public sealed class SellerApplicationService : ISellerApplicationService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new() { WriteIndented = false };

    private readonly IUserRepository _userRepository;
    private readonly ISellerRepository _sellerRepository;
    private readonly ISellerApplicationRepository _applicationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public SellerApplicationService(
        IUserRepository userRepository,
        ISellerRepository sellerRepository,
        ISellerApplicationRepository applicationRepository,
        IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _sellerRepository = sellerRepository;
        _applicationRepository = applicationRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<SellerApplication> SubmitAsync(
        int buyerUserId,
        string shopName,
        string phone,
        string address,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var buyer = await RequireUserAsync(buyerUserId, cancellationToken);
        if (!buyer.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_AccountLockedNoApply"]);
        }

        if (buyer.Role != UserRole.Buyer)
        {
            throw new InvalidOperationException(Loc.Instance["Service_OnlyBuyerApply"]);
        }

        shopName = TrimRequired(shopName, Loc.Instance["Common_ShopName"], 255);
        phone = TrimRequired(phone, Loc.Instance["Common_Phone"], 30);
        address = TrimRequired(address, Loc.Instance["Common_Address"], 500);
        note = TrimOptional(note, Loc.Instance["Common_Notes"], 500);

        if (await _applicationRepository.HasPendingAsync(buyerUserId, cancellationToken))
        {
            throw new InvalidOperationException(Loc.Instance["Service_AlreadyPendingApplication"]);
        }

        return await _applicationRepository.AddAsync(
            new SellerApplication
            {
                BuyerUserId = buyerUserId,
                ShopName = shopName,
                Phone = phone,
                Address = address,
                Note = note,
                Status = SellerApplicationStatus.Pending
            },
            cancellationToken);
    }

    public Task<SellerApplication?> GetMyApplicationAsync(int buyerUserId, CancellationToken cancellationToken = default)
        => _applicationRepository.GetLatestByBuyerAsync(buyerUserId, cancellationToken);

    public Task<IReadOnlyList<AdminSellerApplicationRow>> GetApplicationsAsync(CancellationToken cancellationToken = default)
        => _applicationRepository.GetAdminRowsAsync(cancellationToken);

    public async Task ApproveAsync(int applicationId, int adminUserId, string? reviewNote, CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);

        var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_ApplicationNotFound"]);
        if (application.Status != SellerApplicationStatus.Pending)
        {
            throw new InvalidOperationException(Loc.Instance["Service_ApplicationProcessed"]);
        }

        var buyer = await RequireUserAsync(application.BuyerUserId, cancellationToken);
        if (!buyer.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_CannotApproveLocked"]);
        }

        if (buyer.Role != UserRole.Buyer)
        {
            throw new InvalidOperationException(Loc.Instance["Service_CannotApproveNotBuyer"]);
        }

        reviewNote = TrimOptional(reviewNote, Loc.Instance["Service_ReviewNoteField"], 500);

        if (_applicationRepository is ITransactionalSellerApplicationRepository transactionalRepository)
        {
            var outcome = await transactionalRepository.ApprovePendingAsync(application, adminUserId, reviewNote, cancellationToken);
            ThrowIfReviewFailed(outcome);
            return;
        }

        // Unit-test fallback for in-memory repositories. The SQL repository performs these steps
        // inside one transaction to avoid a half-promoted user if a later write fails.
        await _userRepository.SetRoleAsync(buyer.UserId, UserRole.Seller, cancellationToken);
        await _sellerRepository.SaveAsync(
            new SellerProfile
            {
                UserId = buyer.UserId,
                ShopName = application.ShopName,
                Phone = application.Phone,
                Address = application.Address,
                IsVerified = true,
                VerifiedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _applicationRepository.UpdateStatusAsync(
            applicationId,
            SellerApplicationStatus.Approved,
            reviewNote,
            adminUserId,
            cancellationToken);

        await AddAuditAsync(
            adminUserId,
            applicationId,
            "SellerApplicationApprove",
            new { application.Status, application.BuyerUserId },
            new { Status = SellerApplicationStatus.Approved.ToString(), Role = UserRole.Seller.ToString(), Verified = true },
            cancellationToken);
    }

    public async Task RejectAsync(int applicationId, int adminUserId, string? reviewNote, CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);

        var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_ApplicationNotFound"]);
        if (application.Status != SellerApplicationStatus.Pending)
        {
            throw new InvalidOperationException(Loc.Instance["Service_ApplicationProcessed"]);
        }

        reviewNote = TrimOptional(reviewNote, Loc.Instance["Service_ReviewNoteField"], 500);

        if (_applicationRepository is ITransactionalSellerApplicationRepository transactionalRepository)
        {
            var outcome = await transactionalRepository.RejectPendingAsync(application, adminUserId, reviewNote, cancellationToken);
            ThrowIfReviewFailed(outcome);
            return;
        }

        var updated = await _applicationRepository.UpdateStatusAsync(
            applicationId,
            SellerApplicationStatus.Rejected,
            reviewNote,
            adminUserId,
            cancellationToken);
        if (!updated)
        {
            throw new InvalidOperationException(Loc.Instance["Service_ApplicationProcessed"]);
        }

        await AddAuditAsync(
            adminUserId,
            applicationId,
            "SellerApplicationReject",
            new { application.Status, application.BuyerUserId },
            new { Status = SellerApplicationStatus.Rejected.ToString() },
            cancellationToken);
    }

    private async Task EnsureAdminAsync(int adminUserId, CancellationToken cancellationToken)
    {
        var admin = await RequireUserAsync(adminUserId, cancellationToken);
        if (admin.Role != UserRole.Admin || !admin.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_OnlyActiveAdminReview"]);
        }
    }

    private async Task<User> RequireUserAsync(int userId, CancellationToken cancellationToken)
        => await _userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new InvalidOperationException(Loc.Instance["Service_UserNotFound"]);

    private static string TrimRequired(string? value, string fieldName, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(Loc.Instance["Service_SellerProfileFieldsRequired"]);
        }

        if (value.Length > maxLength)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_FieldTooLong", fieldName, maxLength));
        }

        return value;
    }

    private static string? TrimOptional(string? value, string fieldName, int maxLength)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (value is not null && value.Length > maxLength)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_FieldTooLong", fieldName, maxLength));
        }

        return value;
    }

    private static void ThrowIfReviewFailed(SellerApplicationReviewOutcome outcome)
    {
        switch (outcome)
        {
            case SellerApplicationReviewOutcome.Success:
                return;
            case SellerApplicationReviewOutcome.InvalidApplicant:
                throw new InvalidOperationException(Loc.Instance["Service_CannotApproveNotActiveBuyer"]);
            default:
                throw new InvalidOperationException(Loc.Instance["Service_ApplicationProcessed"]);
        }
    }

    private Task AddAuditAsync(
        int adminUserId,
        int applicationId,
        string action,
        object oldValue,
        object newValue,
        CancellationToken cancellationToken)
        => _auditLogRepository.AddAsync(
            new AuditLogEntry
            {
                UserId = adminUserId,
                TableName = "seller_applications",
                RecordId = applicationId.ToString(),
                Action = action,
                OldValueJson = JsonSerializer.Serialize(oldValue, AuditJsonOptions),
                NewValueJson = JsonSerializer.Serialize(newValue, AuditJsonOptions)
            },
            cancellationToken);
}
