using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Enums;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Custom_keyboard.Realtime;

/// <summary>
/// MQTT-backed realtime layer (Phase 8). Buyers publish "new request" to a per-seller
/// topic; sellers publish "status update" to a per-request topic. The DB stays the
/// source of truth — every network call here is best-effort and runs in the background,
/// so a missing broker never blocks the UI or loses data (receivers just reload from DB).
/// </summary>
public sealed class MqttRealtimeService : IRealtimeNotifier, IRealtimeSubscriber, IAsyncDisposable
{
    private readonly MqttSettings _settings;
    private readonly MqttFactory _factory = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IMqttClient? _client;
    private User? _user;
    private DateTime _nextConnectAttemptUtc = DateTime.MinValue;
    private bool _disposed;

    public MqttRealtimeService(MqttSettings? settings = null)
    {
        _settings = settings ?? new MqttSettings();
    }

    public event Func<Task>? SellerRequestsChanged;
    public event Func<Task>? BuyerRequestsChanged;

    // ----- IRealtimeNotifier (publish side; returns immediately, work runs in background) -----

    public Task RequestCreatedAsync(int sellerUserId, string requestId, CancellationToken cancellationToken = default)
    {
        PublishInBackground($"{_settings.TopicRoot}/seller/{sellerUserId}/build-request/new", requestId);
        return Task.CompletedTask;
    }

    public Task RequestStatusChangedAsync(string requestId, RequestStatus status, CancellationToken cancellationToken = default)
    {
        PublishInBackground($"{_settings.TopicRoot}/build-request/{requestId}/status/update", status.ToString());
        return Task.CompletedTask;
    }

    // ----- IRealtimeSubscriber (subscribe side) -----

    public Task StartAsync(User user, CancellationToken cancellationToken = default)
    {
        _user = user;
        if (_settings.Enabled)
        {
            _ = Task.Run(() => ConnectAndSubscribeAsync(user), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _user = null;
        await _gate.WaitAsync();
        try
        {
            if (_client is { IsConnected: true })
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Mqtt.Stop", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ----- internals -----

    private void PublishInBackground(string topic, string payload)
    {
        if (!_settings.Enabled || _disposed)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var client = await EnsureConnectedAsync(CancellationToken.None);
                if (client is null)
                {
                    return;
                }

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await client.PublishAsync(message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                AppLog.Error("Mqtt.Publish", ex);
            }
        }, CancellationToken.None);
    }

    private async Task ConnectAndSubscribeAsync(User user)
    {
        try
        {
            var client = await EnsureConnectedAsync(CancellationToken.None);
            if (client is null)
            {
                return;
            }

            switch (user.Role)
            {
                case UserRole.Seller:
                    await client.SubscribeAsync($"{_settings.TopicRoot}/seller/{user.UserId}/build-request/new");
                    break;
                case UserRole.Buyer:
                    await client.SubscribeAsync($"{_settings.TopicRoot}/build-request/+/status/update");
                    break;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Mqtt.Subscribe", ex);
        }
    }

    private async Task<IMqttClient?> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_disposed || !_settings.Enabled)
        {
            return null;
        }

        if (_client is { IsConnected: true })
        {
            return _client;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_client is { IsConnected: true })
            {
                return _client;
            }

            // Back off after a failed attempt so a missing broker doesn't churn on every action.
            if (DateTime.UtcNow < _nextConnectAttemptUtc)
            {
                return null;
            }

            _client ??= CreateClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Host, _settings.Port)
                .WithClientId($"{_settings.ClientIdPrefix}-{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_settings.ConnectTimeout);
            await _client.ConnectAsync(options, timeout.Token);
            return _client;
        }
        catch (Exception ex)
        {
            _nextConnectAttemptUtc = DateTime.UtcNow.AddSeconds(30);
            AppLog.Error("Mqtt.Connect", ex);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IMqttClient CreateClient()
    {
        var client = _factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        client.DisconnectedAsync += OnDisconnectedAsync;
        return client;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic ?? string.Empty;
            if (topic.EndsWith("/build-request/new", StringComparison.OrdinalIgnoreCase))
            {
                await RaiseAsync(SellerRequestsChanged);
            }
            else if (topic.EndsWith("/status/update", StringComparison.OrdinalIgnoreCase))
            {
                await RaiseAsync(BuyerRequestsChanged);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Mqtt.Receive", ex);
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        // Only auto-reconnect for an active session; StopAsync clears _user first.
        var user = _user;
        if (_disposed || user is null)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (!_disposed && _user is not null)
            {
                await ConnectAndSubscribeAsync(_user);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Mqtt.Reconnect", ex);
        }
    }

    private static async Task RaiseAsync(Func<Task>? handler)
    {
        if (handler is not null)
        {
            await handler.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _user = null;
        try
        {
            if (_client is not null)
            {
                if (_client.IsConnected)
                {
                    await _client.DisconnectAsync();
                }

                _client.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Mqtt.Dispose", ex);
        }

        _gate.Dispose();
    }
}
