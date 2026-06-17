using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services.Devices;

// Pure, IO-free QC rule engine (guide §5). Maps one key's raw telemetry to a verdict.
// Priority: signal failures > latency > noise. Only one (primary) failure is reported per key;
// among signal faults StuckKey is concluded before Chatter (plan §6.1).
public static class DeviceQcRules
{
    // Latency thresholds (ms) — guide §5.2. HE is judged more strictly than mechanical/optical.
    private const decimal HeLatencyGoodMs = 3m;
    private const decimal HeLatencyWarnMs = 6m;
    private const decimal MechLatencyGoodMs = 10m;
    private const decimal MechLatencyWarnMs = 20m;

    // Noise thresholds (dB) — guide §5.3.
    private const decimal SilentNoiseGoodDb = 45m;
    private const decimal SilentNoiseWarnDb = 55m;
    private const decimal QuietNoiseGoodDb = 55m;
    private const decimal QuietNoiseWarnDb = 65m;
    private const decimal NormalNoiseOkDb = 65m;

    public static KeyEvaluation Evaluate(KeyTelemetry telemetry, QcThresholds thresholds)
    {
        // 1. Signal — most fundamental; a failure here makes latency/noise meaningless.
        if (!telemetry.PressSignalDetected)
        {
            return Fail(KeyFailureType.NoSignal, "No signal detected");
        }

        if (!string.Equals(telemetry.ReceivedKey, telemetry.ExpectedKey, StringComparison.Ordinal))
        {
            return Fail(KeyFailureType.WrongKey, "Received key did not match expected key");
        }

        // StuckKey before Chatter: a missing release means there is no complete press-release
        // cycle to judge double-click, so bounce_count is not evaluated here (guide §5.1 / §6.5).
        if (!telemetry.ReleaseSignalDetected
            || telemetry.IsStuck
            || (telemetry.HoldDurationMs is int hold && hold > thresholds.StuckThresholdMs))
        {
            return Fail(KeyFailureType.StuckKey, "Key remained active after release");
        }

        if ((telemetry.BounceCount ?? 0) >= 1 || telemetry.PressEventCount >= 2)
        {
            return Fail(KeyFailureType.Chatter, "Key triggered multiple times in one press");
        }

        // 2. Latency
        var isHe = IsHeSwitch(thresholds.SwitchTechnology);
        var latencyVerdict = EvaluateLatency(telemetry.LatencyMs, isHe);
        if (latencyVerdict == Verdict.Fail)
        {
            return Fail(
                KeyFailureType.HighLatency,
                isHe ? "HE switch latency exceeded 6ms" : "Latency exceeded 20ms");
        }

        // 3. Noise
        var noiseVerdict = EvaluateNoise(telemetry.NoiseDb, thresholds.NoiseRequirement);
        if (noiseVerdict == Verdict.Fail)
        {
            return Fail(KeyFailureType.TooNoisy, "Noise exceeded the buyer's silent requirement");
        }

        // 4. No failure — surface the worst warning, if any (latency takes precedence over noise).
        if (latencyVerdict == Verdict.Warning)
        {
            return Warning(isHe ? "Latency above HE good threshold (3ms)" : "Latency above good threshold (10ms)");
        }

        if (noiseVerdict == Verdict.Warning)
        {
            return Warning("Noise above the expected band");
        }

        return KeyEvaluation.Pass;
    }

    private static Verdict EvaluateLatency(decimal? latencyMs, bool isHe)
    {
        if (latencyMs is not decimal ms)
        {
            return Verdict.Pass;
        }

        var good = isHe ? HeLatencyGoodMs : MechLatencyGoodMs;
        var warn = isHe ? HeLatencyWarnMs : MechLatencyWarnMs;
        if (ms <= good)
        {
            return Verdict.Pass;
        }

        return ms <= warn ? Verdict.Warning : Verdict.Fail;
    }

    private static Verdict EvaluateNoise(decimal? noiseDb, NoiseRequirement requirement)
    {
        if (noiseDb is not decimal db)
        {
            return Verdict.Pass;
        }

        return requirement switch
        {
            NoiseRequirement.Silent => db <= SilentNoiseGoodDb ? Verdict.Pass
                : db <= SilentNoiseWarnDb ? Verdict.Warning : Verdict.Fail,
            NoiseRequirement.Quiet => db <= QuietNoiseGoodDb ? Verdict.Pass
                : db <= QuietNoiseWarnDb ? Verdict.Warning : Verdict.Fail,
            // Normal (phase-1 default): noise is informational and never fails a build (guide §5.3).
            _ => db <= NormalNoiseOkDb ? Verdict.Pass : Verdict.Warning
        };
    }

    private static bool IsHeSwitch(string switchTechnology)
        => string.Equals(switchTechnology, "HE", StringComparison.OrdinalIgnoreCase);

    private static KeyEvaluation Fail(KeyFailureType failureType, string reason)
        => new(KeyTestResult.Fail, failureType, reason);

    private static KeyEvaluation Warning(string reason)
        => new(KeyTestResult.Warning, null, reason);

    private enum Verdict
    {
        Pass,
        Warning,
        Fail
    }
}
