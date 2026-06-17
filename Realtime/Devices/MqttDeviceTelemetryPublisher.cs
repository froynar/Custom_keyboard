using System.Text.Json;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Devices;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Custom_keyboard.Realtime.Devices;

// MQTT publisher for QC telemetry. Unlike IRealtimeNotifier (fire-and-forget), this awaits the
// publish and reports success/failure so the simulator can fall back to a direct DeviceService
// call when the broker is unreachable (plan §7.1). The DB stays the source of truth either way.
public sealed class MqttDeviceTelemetryPublisher : IDeviceTelemetryPublisher, IAsyncDisposable
{
    private readonly MqttSettings _settings;
    private readonly MqttFactory _factory = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IMqttClient? _client;
    private DateTime _nextConnectAttemptUtc = DateTime.MinValue;
    private bool _disposed;

    public MqttDeviceTelemetryPublisher(MqttSettings? settings = null)
    {
        _settings = settings ?? new MqttSettings();
    }

    public Task<bool> PublishKeyTestAsync(KeyTelemetry telemetry, CancellationToken cancellationToken = default)
        => PublishAsync(
            DeviceTelemetryTransport.KeyTestTopic(_settings.TopicRoot, telemetry.DeviceId, telemetry.RequestId),
            telemetry,
            cancellationToken);

    public Task<bool> PublishSessionSummaryAsync(DeviceTestSession session, CancellationToken cancellationToken = default)
        => PublishAsync(
            DeviceTelemetryTransport.SessionSummaryTopic(_settings.TopicRoot, session.DeviceId, session.RequestId),
            session,
            cancellationToken);

    private async Task<bool> PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || _disposed)
        {
            return false;
        }

        try
        {
            var client = await EnsureConnectedAsync(cancellationToken);
            if (client is null)
            {
                return false;
            }

            var json = JsonSerializer.Serialize(payload, DeviceTelemetryTransport.Json);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await client.PublishAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.Publish", ex);
            return false;
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

            // Back off after a failed attempt so a missing broker doesn't churn on every key.
            if (DateTime.UtcNow < _nextConnectAttemptUtc)
            {
                return null;
            }

            _client ??= _factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Host, _settings.Port)
                .WithClientId($"{_settings.ClientIdPrefix}-devpub-{Guid.NewGuid():N}")
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
            AppLog.Error("DeviceTelemetry.PublisherConnect", ex);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
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
            AppLog.Error("DeviceTelemetry.PublisherDispose", ex);
        }

        _gate.Dispose();
    }
}
