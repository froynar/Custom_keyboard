namespace Custom_keyboard.Models.Enums;

// Reason a key failed/warned. Stored as VARCHAR (CK_dktr_failure_type), nullable.
public enum KeyFailureType
{
    NoSignal,
    WrongKey,
    Chatter,
    StuckKey,
    HighLatency,
    TooNoisy
}
