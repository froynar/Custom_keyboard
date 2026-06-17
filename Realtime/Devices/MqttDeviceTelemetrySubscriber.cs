using System.Text.Json;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services.Devices;
using MQTTnet;
using MQTTnet.Client;

namespace Custom_keyboard.Realtime.Devices;

// Receives QC telemetry from the broker and persists it via DeviceService (the source of truth),
// then raises events so a dashboard VM can reload. Messages are handled one at a time so a
// session-summary cannot complete before its key-test rows are inserted (plan §7.1, FIX #12/#14).
public sealed class MqttDeviceTelemetrySubscriber : IDeviceTelemetrySubscriber, IAsyncDisposable
{
    private const int CompleteRetryAttempts = 10;
    private static readonly TimeSpan CompleteRetryDelay = TimeSpan.FromMilliseconds(150);

    private readonly MqttSettings _settings;
    private readonly IDeviceService _deviceService;
    private readonly MqttFactory _factory = new();
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _processGate = new(1, 1); // serialize message handling per app

    private IMqttClient? _client;
    private Task? _connectLoop;
    private bool _started;
    private bool _disposed;

    public MqttDeviceTelemetrySubscriber(MqttSettings settings, IDeviceService deviceService)
    {
        _settings = settings;
        _deviceService = deviceService;
    }

    public event Func<KeyTelemetry, Task>? KeyTestReceived;
    public event Func<DeviceTestSession, Task>? SessionSummaryReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _started = true;
        if (_settings.Enabled)
        {
            EnsureConnectLoopStarted();
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _started = false;
        await _connectGate.WaitAsync();
        try
        {
            if (_client is { IsConnected: true })
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.SubscriberStop", ex);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private void EnsureConnectLoopStarted()
    {
        if (_disposed || !_started || !_settings.Enabled)
        {
            return;
        }

        if (_connectLoop is { IsCompleted: false })
        {
            return;
        }

        _connectLoop = Task.Run(ConnectLoopAsync, CancellationToken.None);
    }

    private async Task ConnectLoopAsync()
    {
        while (!_disposed && _started && _settings.Enabled)
        {
            if (await ConnectAndSubscribeAsync())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    private async Task<bool> ConnectAndSubscribeAsync()
    {
        try
        {
            var client = await EnsureConnectedAsync();
            if (client is null)
            {
                return false;
            }

            await client.SubscribeAsync(DeviceTelemetryTransport.KeyTestFilter(_settings.TopicRoot));
            await client.SubscribeAsync(DeviceTelemetryTransport.SessionSummaryFilter(_settings.TopicRoot));
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.Subscribe", ex);
            return false;
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic ?? string.Empty;
        var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

        await _processGate.WaitAsync();
        try
        {
            if (topic.EndsWith(DeviceTelemetryTransport.KeyTestSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var telemetry = JsonSerializer.Deserialize<KeyTelemetry>(payload, DeviceTelemetryTransport.Json);
                if (telemetry is not null)
                {
                    await _deviceService.RecordKeyResultAsync(telemetry);
                    await RaiseAsync(KeyTestReceived, telemetry);
                }
            }
            else if (topic.EndsWith(DeviceTelemetryTransport.SessionSummarySuffix, StringComparison.OrdinalIgnoreCase))
            {
                var summary = JsonSerializer.Deserialize<DeviceTestSession>(payload, DeviceTelemetryTransport.Json);
                if (summary is not null)
                {
                    var completed = await CompleteWhenReadyAsync(summary.SessionId);
                    await RaiseAsync(SessionSummaryReceived, completed ?? summary);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.Receive", ex);
        }
        finally
        {
            _processGate.Release();
        }
    }

    // Only finalize once all key-test rows have landed; CompleteSessionAsync returns Running while
    // rows are still missing, so retry briefly to absorb out-of-order MQTT delivery (plan §7.1).
    private async Task<DeviceTestSession?> CompleteWhenReadyAsync(string sessionId)
    {
        for (var attempt = 0; attempt < CompleteRetryAttempts; attempt++)
        {
            var session = await _deviceService.CompleteSessionAsync(sessionId);
            if (session.Status != TestSessionStatus.Running)
            {
                return session;
            }

            await Task.Delay(CompleteRetryDelay);
        }

        return null;
    }

    private async Task<IMqttClient?> EnsureConnectedAsync()
    {
        if (_disposed || !_settings.Enabled)
        {
            return null;
        }

        if (_client is { IsConnected: true })
        {
            return _client;
        }

        await _connectGate.WaitAsync();
        try
        {
            if (_client is { IsConnected: true })
            {
                return _client;
            }

            _client ??= CreateClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Host, _settings.Port)
                .WithClientId($"{_settings.ClientIdPrefix}-devsub-{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            using var timeout = new CancellationTokenSource(_settings.ConnectTimeout);
            await _client.ConnectAsync(options, timeout.Token);
            return _client;
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.SubscriberConnect", ex);
            return null;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private IMqttClient CreateClient()
    {
        var client = _factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        client.DisconnectedAsync += OnDisconnectedAsync;
        return client;
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        if (_disposed || !_started)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (!_disposed && _started)
            {
                EnsureConnectLoopStarted();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("DeviceTelemetry.SubscriberReconnect", ex);
        }
    }

    private static async Task RaiseAsync<T>(Func<T, Task>? handler, T payload)
    {
        if (handler is not null)
        {
            await handler.Invoke(payload);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _started = false;
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
            AppLog.Error("DeviceTelemetry.SubscriberDispose", ex);
        }

        _connectGate.Dispose();
        _processGate.Dispose();
    }
}
