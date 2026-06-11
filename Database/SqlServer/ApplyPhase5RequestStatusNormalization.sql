USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_build_requests_status')
BEGIN
    ALTER TABLE build_requests DROP CONSTRAINT CK_build_requests_status;
END;

UPDATE build_requests
SET status = 'In_progress',
    updated_at = SYSUTCDATETIME()
WHERE status = 'InProgress';

ALTER TABLE build_requests WITH CHECK
ADD CONSTRAINT CK_build_requests_status
CHECK (status IN ('Pending', 'Accepted', 'In_progress', 'Completed', 'Cancelled'));

SELECT
    status,
    COUNT(*) AS request_count
FROM build_requests
GROUP BY status
ORDER BY status;
GO
