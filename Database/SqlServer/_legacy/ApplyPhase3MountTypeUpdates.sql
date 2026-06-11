USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

IF COL_LENGTH('pcbs', 'mount_type') IS NULL
BEGIN
    ALTER TABLE pcbs
    ADD mount_type VARCHAR(100) NOT NULL
        CONSTRAINT DF_pcbs_mount_type DEFAULT 'Gasket Mount';

    ALTER TABLE pcbs
    DROP CONSTRAINT DF_pcbs_mount_type;
END;

IF COL_LENGTH('plates', 'mount_type') IS NULL
BEGIN
    ALTER TABLE plates
    ADD mount_type VARCHAR(100) NOT NULL
        CONSTRAINT DF_plates_mount_type DEFAULT 'Gasket Mount';

    ALTER TABLE plates
    DROP CONSTRAINT DF_plates_mount_type;
END;
GO
