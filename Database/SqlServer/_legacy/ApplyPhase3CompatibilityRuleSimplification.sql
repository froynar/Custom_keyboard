USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

IF COL_LENGTH('compatibility_rules', 'switch_mount_required') IS NOT NULL
BEGIN
    ALTER TABLE compatibility_rules
    DROP COLUMN switch_mount_required;
END;

IF COL_LENGTH('compatibility_rules', 'hotswap_required') IS NOT NULL
BEGIN
    ALTER TABLE compatibility_rules
    DROP COLUMN hotswap_required;
END;
GO
