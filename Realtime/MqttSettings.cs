using System.Security.Cryptography;

namespace Custom_keyboard.Realtime;

/// <summary>
/// Connection settings for the optional MQTT realtime layer (Phase 8). Realtime is a
/// bonus on top of the DB source-of-truth: when <see cref="Enabled"/> is false or no
/// broker is reachable, the app keeps working DB-only and publishing/subscribing no-ops.
/// </summary>
public sealed class MqttSettings
{
    /// <summary>Turn the realtime layer on/off. When off, nothing connects.</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public string ClientIdPrefix { get; set; } = "custom-keyboard";

    /// <summary>Root segment for all topics, e.g. <c>keyboard/seller/{id}/build-request/new</c>.</summary>
    public string TopicRoot { get; set; } = "keyboard";

    /// <summary>How long to wait for a broker connection before giving up (best-effort).</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// HMAC secret for device telemetry envelopes. Set CUSTOM_KEYBOARD_DEVICE_TELEMETRY_SECRET
    /// when an external device/process must publish trusted QC messages. A per-process secret is
    /// generated otherwise, which is sufficient for the in-app simulator/publisher/subscriber.
    /// </summary>
    public string DeviceTelemetrySecret { get; set; } =
        Environment.GetEnvironmentVariable("CUSTOM_KEYBOARD_DEVICE_TELEMETRY_SECRET")
        ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
