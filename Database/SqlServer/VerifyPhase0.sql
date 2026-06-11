USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

SELECT
    DB_NAME() AS database_name,
    @@SERVERNAME AS server_name,
    SUSER_SNAME() AS login_name;

SELECT
    COUNT(*) AS user_table_count
FROM sys.tables;

SELECT
    t.name AS table_name,
    SUM(p.rows) AS row_count
FROM sys.tables AS t
INNER JOIN sys.partitions AS p
    ON p.object_id = t.object_id
   AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;

SELECT
    role_name,
    permissions
FROM roles
ORDER BY role_name;
GO
