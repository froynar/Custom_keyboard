using System.Security.Cryptography;
using System.Text;
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

    public static string SerializeSigned<T>(T payload, string secret)
    {
        var payloadJson = JsonSerializer.Serialize(payload, Json);
        var signature = ComputeSignature(payloadJson, secret);
        return JsonSerializer.Serialize(
            new SignedEnvelope(payloadJson, Convert.ToBase64String(signature)),
            Json);
    }

    public static T DeserializeSigned<T>(string envelopeJson, string secret)
    {
        var envelope = JsonSerializer.Deserialize<SignedEnvelope>(envelopeJson, Json)
            ?? throw new InvalidOperationException("Device telemetry envelope is empty.");

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Device telemetry signature is malformed.", ex);
        }

        var expectedSignature = ComputeSignature(envelope.Payload, secret);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature))
        {
            throw new InvalidOperationException("Device telemetry signature is invalid.");
        }

        return JsonSerializer.Deserialize<T>(envelope.Payload, Json)
            ?? throw new InvalidOperationException("Device telemetry payload is empty.");
    }

    public static bool TryParseKeyTestTopic(
        string topicRoot,
        string topic,
        out string deviceId,
        out string requestId)
        => TryParseTopic(topicRoot, topic, KeyTestSuffix, out deviceId, out requestId);

    public static bool TryParseSessionSummaryTopic(
        string topicRoot,
        string topic,
        out string deviceId,
        out string requestId)
        => TryParseTopic(topicRoot, topic, SessionSummarySuffix, out deviceId, out requestId);

    private static bool TryParseTopic(
        string topicRoot,
        string topic,
        string suffix,
        out string deviceId,
        out string requestId)
    {
        deviceId = string.Empty;
        requestId = string.Empty;

        var prefix = $"{topicRoot.TrimEnd('/')}/device/";
        if (!topic.StartsWith(prefix, StringComparison.Ordinal)
            || !topic.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var route = topic[prefix.Length..^suffix.Length];
        const string requestMarker = "/request/";
        var markerIndex = route.IndexOf(requestMarker, StringComparison.Ordinal);
        if (markerIndex <= 0
            || route.IndexOf(requestMarker, markerIndex + requestMarker.Length, StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        deviceId = route[..markerIndex];
        requestId = route[(markerIndex + requestMarker.Length)..];
        return deviceId.Length is > 0 and <= 50
            && requestId.Length is > 0 and <= 50
            && !deviceId.Contains('/')
            && !requestId.Contains('/');
    }

    private static byte[] ComputeSignature(string payloadJson, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("A device telemetry signing secret is required.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
    }

    private sealed record SignedEnvelope(string Payload, string Signature);
}
