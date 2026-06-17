using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Realtime.Devices;

// Subscribe side: receives telemetry from the broker, persists it via DeviceService, then raises
// these events so a dashboard VM can reload (plan §6.3/§7.1). Events fire on a background thread;
// consumers marshal to the UI.
public interface IDeviceTelemetrySubscriber
{
    event Func<KeyTelemetry, Task>? KeyTestReceived;
    event Func<DeviceTestSession, Task>? SessionSummaryReceived;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
