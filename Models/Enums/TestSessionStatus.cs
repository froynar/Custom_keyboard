namespace Custom_keyboard.Models.Enums;

// Lifecycle of a QC test session. Stored as VARCHAR (CK_dts_status).
public enum TestSessionStatus
{
    Running,
    Passed,
    Warning,
    Failed
}
