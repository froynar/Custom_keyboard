using System.Text.Json;
using System.Text.Json.Serialization;

namespace Custom_keyboard.Realtime.Devices;

// Shared topic builders + JSON options for the device telemetry pair (publisher/subscriber).
// Topics follow guide §9.1/§9.2: keyboard/device/{deviceId}/request/{requestId}/(key-test|session-summary).
internal static class DeviceTelemetryTransport
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public const string KeyTestSuffix = "/key-test";
    public const string SessionSummarySuffix = "/session-summary";

    public static string KeyTestTopic(string topicRoot, string deviceId, string requestId)
        => $"{topicRoot}/device/{deviceId}/request/{requestId}{KeyTestSuffix}";

    public static string SessionSummaryTopic(string topicRoot, string deviceId, string requestId)
        => $"{topicRoot}/device/{deviceId}/request/{requestId}{SessionSummarySuffix}";

    // Wildcard subscriptions across all devices/requests for this app.
    public static string KeyTestFilter(string topicRoot) => $"{topicRoot}/device/+/request/+{KeyTestSuffix}";

    public static string SessionSummaryFilter(string topicRoot) => $"{topicRoot}/device/+/request/+{SessionSummarySuffix}";
}
