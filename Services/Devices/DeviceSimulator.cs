using System.Text.Json;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Realtime.Devices;

namespace Custom_keyboard.Services.Devices;

// Simulated QC station. Parses the build request snapshot for key count + switch technology,
// starts a session, generates per-key telemetry (guide §10), and emits it over MQTT — falling
// back to a direct DeviceService call when the broker is unavailable so QC data is never lost
// (plan §7.2). It produces RAW signals only; DeviceQcRules (inside DeviceService) decides Pass/Fail.
public sealed class DeviceSimulator
{
    private const int FallbackKeyCount = 68;
    private const string DefaultSwitchTechnology = "Mechanical";
    private const int CompleteRetryAttempts = 10;
    private const double NoSignalFaultRate = 0.004;
    private const double WrongKeyFaultRate = 0.0015;
    private const double StuckKeyFaultRate = 0.0005;
    private const double ChatterFaultRate = 0.007;
    private const double LatencyOutlierRate = 0.015;
    private static readonly TimeSpan CompleteRetryDelay = TimeSpan.FromMilliseconds(150);

    private static readonly string[] BaseKeyLayout =
    [
        "Esc", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "Minus", "Equal", "Backspace",
        "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "LBracket", "RBracket", "Backslash",
        "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Semicolon", "Quote", "Enter",
        "LShift", "Z", "X", "C", "V", "B", "N", "M", "Comma", "Period", "Slash", "RShift",
        "LCtrl", "LWin", "LAlt", "Space", "RAlt", "Fn", "Menu", "RCtrl",
        "Up", "Down", "Left", "Right", "Delete", "Home", "End"
    ];

    private readonly IDeviceService _deviceService;
    private readonly IDeviceTelemetryPublisher _publisher;
    private readonly Random _random = new();

    public DeviceSimulator(IDeviceService deviceService, IDeviceTelemetryPublisher publisher)
    {
        _deviceService = deviceService;
        _publisher = publisher;
    }

    // Optional pacing so a demo streams keys instead of dumping them instantly. Default: no delay.
    public TimeSpan PerKeyDelay { get; set; } = TimeSpan.Zero;

    // Set when the payload could not be parsed and defaults were used (plan §7.2). UI may surface it.
    public string? LastStatusMessage { get; private set; }

    public async Task<DeviceTestSession> RunQcTestAsync(BuildRequest request, CancellationToken cancellationToken = default)
    {
        LastStatusMessage = null;
        var (keyCount, switchTechnology, noiseRequirement) = ParsePayload(request.RequestPayloadJson);

        // Order that satisfies every FK: device row -> session row -> per-key results (plan §6.2).
        var device = await _deviceService.GetOrCreateQcStationAsync(request.SellerUserId, cancellationToken);
        var session = await _deviceService.StartSessionAsync(
            request.RequestId,
            request.SellerUserId,
            device.DeviceId,
            switchTechnology,
            noiseRequirement,
            keyCount,
            cancellationToken);

        var keys = BuildKeyLayout(keyCount);
        var sessionNoiseDb = NextSessionNoiseDb(noiseRequirement);
        var emittedTelemetries = new List<KeyTelemetry>(keys.Count);
        var allViaMqtt = true;

        foreach (var keyCode in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var telemetry = GenerateKeyTelemetry(session, device.DeviceId, keyCode, switchTechnology, sessionNoiseDb);
            emittedTelemetries.Add(telemetry);

            // Main path: publish to broker (subscriber persists). Fallback: write directly (FIX #11/#12).
            var published = await _publisher.PublishKeyTestAsync(telemetry, cancellationToken);
            if (!published)
            {
                allViaMqtt = false;
                await _deviceService.RecordKeyResultAsync(telemetry, cancellationToken);
            }

            if (PerKeyDelay > TimeSpan.Zero)
            {
                await Task.Delay(PerKeyDelay, cancellationToken);
            }
        }

        // Completion: summary publish is best-effort notification only. Always reconcile against
        // emitted telemetry so a half-alive broker cannot leave an empty/partial Running session.
        if (allViaMqtt && keyCount > 0)
        {
            _ = await _publisher.PublishSessionSummaryAsync(session, cancellationToken);
        }

        return await CompleteWhenPersistedOrFallbackAsync(session.SessionId, emittedTelemetries, cancellationToken);
    }

    private async Task<DeviceTestSession> CompleteWhenPersistedOrFallbackAsync(
        string sessionId,
        IReadOnlyList<KeyTelemetry> telemetries,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < CompleteRetryAttempts; attempt++)
        {
            var existing = await _deviceService.GetKeyResultsAsync(sessionId, cancellationToken);
            if (existing.Count >= telemetries.Count)
            {
                var completed = await _deviceService.CompleteSessionAsync(sessionId, cancellationToken);
                if (completed.Status != TestSessionStatus.Running)
                {
                    return completed;
                }
            }

            await Task.Delay(CompleteRetryDelay, cancellationToken);
        }

        // Publish success only proves the broker accepted the message; it does not prove the app
        // subscriber persisted it. Fill any missing rows directly so a QC session never stays empty.
        var persisted = await _deviceService.GetKeyResultsAsync(sessionId, cancellationToken);
        var persistedKeys = persisted.Select(result => result.KeyCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var telemetry in telemetries)
        {
            if (!persistedKeys.Contains(telemetry.KeyCode))
            {
                await _deviceService.RecordKeyResultAsync(telemetry, cancellationToken);
            }
        }

        return await CompleteDirectAsync(sessionId, cancellationToken);
    }

    private async Task<DeviceTestSession> CompleteDirectAsync(string sessionId, CancellationToken cancellationToken)
    {
        DeviceTestSession session = await _deviceService.CompleteSessionAsync(sessionId, cancellationToken);
        for (var attempt = 1; attempt < CompleteRetryAttempts && session.Status == TestSessionStatus.Running; attempt++)
        {
            await Task.Delay(CompleteRetryDelay, cancellationToken);
            session = await _deviceService.CompleteSessionAsync(sessionId, cancellationToken);
        }

        return session;
    }

    private (int KeyCount, string SwitchTechnology, NoiseRequirement NoiseRequirement) ParsePayload(string? payloadJson)
    {
        var noiseRequirement = NoiseRequirement.Normal;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && TryGetProperty(root, "build", out var build)
                    && build.ValueKind == JsonValueKind.Object)
                {
                    noiseRequirement = ParseNoiseRequirement(GetString(build, "noiseRequirement"));
                }

                if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "kit", out var kit) && kit.ValueKind == JsonValueKind.Object)
                {
                    var keyCount = GetInt(kit, "requiredSwitchQuantity");
                    var switchTechnology = GetString(kit, "pcbTechnology");
                    if (keyCount is > 0 && !string.IsNullOrWhiteSpace(switchTechnology))
                    {
                        return (keyCount.Value, switchTechnology!, noiseRequirement);
                    }
                }
            }
            catch (JsonException ex)
            {
                AppLog.Error("DeviceSimulator.ParsePayload", ex);
            }
        }

        // Old/empty payloads (e.g. demo requests) must not crash QC — use safe defaults (plan §7.2).
        LastStatusMessage = $"Build payload missing kit info; using defaults ({FallbackKeyCount} keys, {DefaultSwitchTechnology}).";
        return (FallbackKeyCount, DefaultSwitchTechnology, noiseRequirement);
    }

    private static NoiseRequirement ParseNoiseRequirement(string? value)
        => Enum.TryParse<NoiseRequirement>(value, ignoreCase: true, out var requirement)
            ? requirement
            : NoiseRequirement.Normal;

    private static IReadOnlyList<string> BuildKeyLayout(int keyCount)
    {
        var keys = new List<string>(keyCount);
        for (var index = 0; index < keyCount; index++)
        {
            keys.Add(index < BaseKeyLayout.Length ? BaseKeyLayout[index] : $"Key{index + 1}");
        }

        return keys;
    }

    private KeyTelemetry GenerateKeyTelemetry(
        DeviceTestSession session,
        string deviceId,
        string keyCode,
        string switchTechnology,
        decimal sessionNoiseDb)
    {
        var telemetry = new KeyTelemetry
        {
            SessionId = session.SessionId,
            RequestId = session.RequestId,
            DeviceId = deviceId,
            KeyCode = keyCode,
            ExpectedKey = keyCode,
            SwitchTechnology = switchTechnology,
            NoiseDb = NextMeasuredNoiseDb(sessionNoiseDb)
        };

        // Fault injection uses low, QC-like rates: most keys are clean; chatter is
        // more likely than stuck, but both remain uncommon.
        var roll = _random.NextDouble();
        var noSignalCutoff = NoSignalFaultRate;
        var wrongKeyCutoff = noSignalCutoff + WrongKeyFaultRate;
        var stuckCutoff = wrongKeyCutoff + StuckKeyFaultRate;
        var chatterCutoff = stuckCutoff + ChatterFaultRate;

        if (roll < noSignalCutoff) // NoSignal
        {
            telemetry.PressSignalDetected = false;
            telemetry.ReceivedKey = null;
            telemetry.LatencyMs = null;
            telemetry.PressEventCount = 0;
            telemetry.BounceCount = null;
            telemetry.ReleaseSignalDetected = false;
            telemetry.HoldDurationMs = null;
            telemetry.IsStuck = false;
            return telemetry;
        }

        if (roll < wrongKeyCutoff) // WrongKey
        {
            ApplyCleanPress(telemetry, switchTechnology);
            telemetry.ReceivedKey = DifferentKey(keyCode);
            return telemetry;
        }

        if (roll < stuckCutoff) // StuckKey: press lands, release never does
        {
            telemetry.PressSignalDetected = true;
            telemetry.ReceivedKey = keyCode;
            telemetry.LatencyMs = NextLatencyMs(switchTechnology, allowOutlier: false);
            telemetry.PressEventCount = 1;
            telemetry.BounceCount = null; // no complete press-release cycle (guide §6.5)
            telemetry.ReleaseSignalDetected = false;
            telemetry.HoldDurationMs = _random.Next(1500, 3000);
            telemetry.IsStuck = true;
            return telemetry;
        }

        if (roll < chatterCutoff) // Chatter: one press registered several times
        {
            var pressEvents = _random.Next(2, 6);
            telemetry.PressSignalDetected = true;
            telemetry.ReceivedKey = keyCode;
            telemetry.LatencyMs = NextLatencyMs(switchTechnology, allowOutlier: false);
            telemetry.PressEventCount = pressEvents;
            telemetry.BounceCount = pressEvents - 1;
            telemetry.ReleaseSignalDetected = true;
            telemetry.HoldDurationMs = _random.Next(70, 120);
            telemetry.IsStuck = false;
            return telemetry;
        }

        // Clean press (latency/noise may still land in a warning/fail band via the outlier roll).
        ApplyCleanPress(telemetry, switchTechnology);
        return telemetry;
    }

    private void ApplyCleanPress(KeyTelemetry telemetry, string switchTechnology)
    {
        telemetry.PressSignalDetected = true;
        telemetry.ReceivedKey = telemetry.ExpectedKey;
        telemetry.LatencyMs = NextLatencyMs(switchTechnology, allowOutlier: true);
        telemetry.PressEventCount = 1;
        telemetry.BounceCount = 0;
        telemetry.ReleaseSignalDetected = true;
        telemetry.HoldDurationMs = _random.Next(70, 120);
        telemetry.IsStuck = false;
    }

    private decimal NextLatencyMs(string switchTechnology, bool allowOutlier)
    {
        var isHe = string.Equals(switchTechnology, "HE", StringComparison.OrdinalIgnoreCase);
        double value;
        if (allowOutlier && _random.NextDouble() < LatencyOutlierRate)
        {
            value = isHe ? NextDouble(3.5, 7.0) : NextDouble(18.0, 28.0);
        }
        else
        {
            value = isHe ? NextDouble(1.0, 3.2) : NextDouble(4.5, 14.0);
        }

        return Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
    }

    private decimal NextSessionNoiseDb(NoiseRequirement requirement)
    {
        // One build using one switch profile has a shared acoustic baseline. A noisy
        // build should fail consistently instead of randomly spiking per key.
        var roll = _random.NextDouble();
        var value = requirement switch
        {
            NoiseRequirement.Silent when roll < 0.75 => NextDouble(41.0, 45.0),
            NoiseRequirement.Silent when roll < 0.95 => NextDouble(45.0, 55.0),
            NoiseRequirement.Silent => NextDouble(55.5, 59.0),

            NoiseRequirement.Quiet when roll < 0.75 => NextDouble(49.0, 55.0),
            NoiseRequirement.Quiet when roll < 0.95 => NextDouble(55.0, 65.0),
            NoiseRequirement.Quiet => NextDouble(65.5, 68.0),

            _ when roll < 0.85 => NextDouble(54.0, 62.0),
            _ when roll < 0.97 => NextDouble(62.0, 65.0),
            _ => NextDouble(65.5, 68.0)
        };

        return Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
    }

    private decimal NextMeasuredNoiseDb(decimal sessionNoiseDb)
    {
        const double SensorToleranceDb = 0.8;
        var measured = (double)sessionNoiseDb + NextDouble(-SensorToleranceDb, SensorToleranceDb);
        return Math.Round((decimal)Math.Max(0.0, measured), 2, MidpointRounding.AwayFromZero);
    }

    private string DifferentKey(string keyCode)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = BaseKeyLayout[_random.Next(BaseKeyLayout.Length)];
            if (!string.Equals(candidate, keyCode, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return keyCode == "A" ? "S" : "A";
    }

    private double NextDouble(double min, double max) => min + (_random.NextDouble() * (max - min));

    private static bool TryGetProperty(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return parent.TryGetProperty(pascalName, out value);
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? GetInt(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }
}
