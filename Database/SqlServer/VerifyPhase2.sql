USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

SELECT
    DB_NAME() AS database_name,
    @@SERVERNAME AS server_name,
    SUSER_SNAME() AS login_name;

SELECT
    u.username,
    u.email,
    r.role_name,
    u.is_active,
    CASE
        WHEN u.password_hash LIKE 'PBKDF2-SHA256$100000$%' THEN 'pbkdf2'
        ELSE 'invalid'
    END AS password_hash_status
FROM users AS u
INNER JOIN roles AS r ON r.role_id = u.role_id
WHERE u.username IN ('admin_seed', 'buyer_seed', 'seller_seed')
ORDER BY r.role_name, u.username;

SELECT
    COUNT(*) AS invalid_seed_password_hash_count
FROM users
WHERE username IN ('admin_seed', 'buyer_seed', 'seller_seed')
  AND password_hash NOT LIKE 'PBKDF2-SHA256$100000$%';

SELECT
    COUNT(*) AS placeholder_password_hash_count
FROM users
WHERE password_hash LIKE 'seed-password-hash-%';
GO
