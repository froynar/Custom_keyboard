using Custom_keyboard.Models.Accounts;

namespace Custom_keyboard.Realtime;

/// <summary>
/// Subscribe side of the realtime layer, consumed by the shell to refresh a dashboard
/// when an event arrives. Events fire on a background thread; consumers marshal to the UI.
/// </summary>
public interface IRealtimeSubscriber
{
    /// <summary>Raised when a new build request arrives for the subscribed seller.</summary>
    event Func<Task>? SellerRequestsChanged;

    /// <summary>Raised when one of the subscribed buyer's requests changes status.</summary>
    event Func<Task>? BuyerRequestsChanged;

    /// <summary>Connect and subscribe to the topics relevant to <paramref name="user"/>'s role. Best-effort.</summary>
    Task StartAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Unsubscribe/disconnect (e.g. on logout). Best-effort.</summary>
    Task StopAsync();
}
