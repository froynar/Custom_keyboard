USE CustomKeyboardBuilder;
GO

IF COL_LENGTH('builds', 'notes') IS NULL
BEGIN
    ALTER TABLE builds
    ADD notes VARCHAR(500) NULL;
END;
GO
