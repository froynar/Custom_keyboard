namespace Custom_keyboard.Models.Enums;

// Stored as VARCHAR; member names match DB values exactly (CK_devices_type).
// GetEnumValue<T> is underscore-tolerant, but keeping the underscores here means
// ToString() round-trips to the DB value with no manual mapping.
public enum DeviceType
{
    QC_STATION,
    KEY_SIGNAL_TESTER,
    LATENCY_TESTER,
    NOISE_SENSOR
}
