USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

SELECT
    DB_NAME() AS database_name,
    @@SERVERNAME AS server_name,
    SUSER_SNAME() AS login_name;

SELECT
    (SELECT COUNT(*) FROM users) AS total_users,
    (
        SELECT COUNT(*)
        FROM users AS u
        INNER JOIN roles AS r ON r.role_id = u.role_id
        WHERE r.role_name = 'Seller'
    ) AS total_sellers,
    (
        SELECT COUNT(*) FROM cases
    ) + (
        SELECT COUNT(*) FROM pcbs
    ) + (
        SELECT COUNT(*) FROM plates
    ) + (
        SELECT COUNT(*) FROM switches
    ) + (
        SELECT COUNT(*) FROM keycap_sets
    ) + (
        SELECT COUNT(*) FROM stabilizers
    ) AS total_components,
    (SELECT COUNT(*) FROM build_requests) AS total_requests;

SELECT
    (SELECT COUNT(*) FROM brands) AS total_brands,
    (SELECT COUNT(*) FROM layouts) AS total_layouts,
    (SELECT COUNT(*) FROM case_layouts) AS total_case_layouts,
    (SELECT COUNT(*) FROM pcb_layouts) AS total_pcb_layouts,
    (SELECT COUNT(*) FROM plate_layouts) AS total_plate_layouts;

SELECT
    u.user_id,
    u.username,
    r.role_name,
    u.is_active
FROM users AS u
INNER JOIN roles AS r ON r.role_id = u.role_id
ORDER BY u.user_id;

SELECT
    seller.user_id,
    seller.username,
    seller.is_active,
    sp.seller_profile_id,
    sp.shop_name,
    sp.phone,
    sp.address,
    sp.is_verified,
    sp.verified_by_admin_id,
    sp.verified_at
FROM users AS seller
INNER JOIN roles AS r ON r.role_id = seller.role_id
LEFT JOIN seller_profiles AS sp ON sp.user_id = seller.user_id
WHERE r.role_name = 'Seller'
ORDER BY seller.user_id;

SELECT
    sp.user_id,
    sp.shop_name,
    sp.phone,
    sp.address,
    sp.is_verified,
    seller.is_active,
    r.role_name
FROM seller_profiles AS sp
INNER JOIN users AS seller ON seller.user_id = sp.user_id
INNER JOIN roles AS r ON r.role_id = seller.role_id
WHERE sp.is_verified = 1
  AND seller.is_active = 1
  AND r.role_name = 'Seller'
ORDER BY sp.shop_name;

SELECT 'cases' AS component_table, case_id AS component_id, brand_id, price_usd, is_available FROM cases
UNION ALL
SELECT 'pcbs', pcb_id, brand_id, price_usd, is_available FROM pcbs
UNION ALL
SELECT 'plates', plate_id, brand_id, price_usd, is_available FROM plates
UNION ALL
SELECT 'switches', switch_id, brand_id, price_usd, is_available FROM switches
UNION ALL
SELECT 'keycap_sets', keycap_id, brand_id, price_usd, is_available FROM keycap_sets
UNION ALL
SELECT 'stabilizers', stab_id, brand_id, price_usd, is_available FROM stabilizers
ORDER BY component_table, component_id;

SELECT 'cases' AS component_table, case_id AS component_id, mount_type FROM cases
UNION ALL
SELECT 'pcbs', pcb_id, mount_type FROM pcbs
UNION ALL
SELECT 'plates', plate_id, mount_type FROM plates
ORDER BY component_table, component_id;

SELECT
    'case_layouts' AS mapping_table,
    case_id AS component_id,
    layout_id,
    CAST(is_primary AS varchar(10)) AS mapping_note
FROM case_layouts
UNION ALL
SELECT
    'pcb_layouts',
    pcb_id,
    layout_id,
    COALESCE(variant_name, '')
FROM pcb_layouts
UNION ALL
SELECT
    'plate_layouts',
    plate_id,
    layout_id,
    ''
FROM plate_layouts
ORDER BY mapping_table, component_id, layout_id;

SELECT
    COLUMN_NAME AS column_name,
    IS_NULLABLE AS is_nullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'compatibility_rules'
  AND COLUMN_NAME IN ('case_id', 'pcb_id', 'plate_id')
ORDER BY ORDINAL_POSITION;

SELECT
    COUNT(*) AS compatibility_rules_with_null_component
FROM compatibility_rules
WHERE case_id IS NULL
   OR pcb_id IS NULL
   OR plate_id IS NULL;

SELECT TOP (100)
    log_id,
    user_id,
    table_name,
    record_id,
    action,
    old_value_json,
    new_value_json,
    changed_at
FROM audit_log
ORDER BY changed_at DESC, log_id DESC;
GO
