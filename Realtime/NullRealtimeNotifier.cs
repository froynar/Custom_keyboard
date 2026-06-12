using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Realtime;

/// <summary>No-op notifier used when realtime is disabled or in tests.</summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public static readonly NullRealtimeNotifier Instance = new();

    public Task RequestCreatedAsync(int sellerUserId, string requestId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RequestStatusChangedAsync(string requestId, RequestStatus status, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
