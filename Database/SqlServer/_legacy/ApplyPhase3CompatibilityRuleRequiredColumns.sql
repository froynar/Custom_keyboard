USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

IF EXISTS (
    SELECT 1
    FROM compatibility_rules
    WHERE case_id IS NULL
       OR pcb_id IS NULL
       OR plate_id IS NULL
)
BEGIN
    THROW 51000, 'Cannot make compatibility_rules case_id/pcb_id/plate_id NOT NULL while null rows exist.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('compatibility_rules')
      AND name = 'case_id'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE compatibility_rules ALTER COLUMN case_id VARCHAR(50) NOT NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('compatibility_rules')
      AND name = 'pcb_id'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE compatibility_rules ALTER COLUMN pcb_id VARCHAR(50) NOT NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('compatibility_rules')
      AND name = 'plate_id'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE compatibility_rules ALTER COLUMN plate_id VARCHAR(50) NOT NULL;
END;

SELECT
    COLUMN_NAME AS column_name,
    IS_NULLABLE AS is_nullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'compatibility_rules'
  AND COLUMN_NAME IN ('case_id', 'pcb_id', 'plate_id')
ORDER BY ORDINAL_POSITION;
GO
