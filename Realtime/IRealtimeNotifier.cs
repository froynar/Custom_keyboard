using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Realtime;

/// <summary>
/// Publish side of the realtime layer. Implementations MUST be best-effort: they are
/// called <em>after</em> the database write and must never throw or block the caller,
/// so a missing/broken broker can never affect the persisted result (Phase 8 contract).
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>A new build request was persisted for <paramref name="sellerUserId"/>.</summary>
    Task RequestCreatedAsync(int sellerUserId, string requestId, CancellationToken cancellationToken = default);

    /// <summary>A persisted request changed status (seller-driven state machine).</summary>
    Task RequestStatusChangedAsync(string requestId, RequestStatus status, CancellationToken cancellationToken = default);
}
