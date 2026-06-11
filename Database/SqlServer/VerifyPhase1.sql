USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

SELECT
    DB_NAME() AS database_name,
    @@SERVERNAME AS server_name,
    SUSER_SNAME() AS login_name;

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
    expected.constraint_name,
    CASE WHEN cc.name IS NULL THEN 'missing' ELSE 'ok' END AS status
FROM (VALUES
    ('CK_builds_status'),
    ('CK_build_requests_status')
) AS expected (constraint_name)
LEFT JOIN sys.check_constraints AS cc
    ON cc.name = expected.constraint_name
ORDER BY expected.constraint_name;

SELECT
    expected.index_name,
    CASE WHEN i.name IS NULL THEN 'missing' ELSE 'ok' END AS status
FROM (VALUES
    ('IX_users_role_id', 'users'),
    ('IX_users_is_active', 'users'),
    ('IX_seller_profiles_is_verified', 'seller_profiles'),
    ('IX_audit_log_user_changed_at', 'audit_log'),
    ('IX_cases_is_available', 'cases'),
    ('IX_pcbs_is_available', 'pcbs'),
    ('IX_plates_is_available', 'plates'),
    ('IX_switches_is_available', 'switches'),
    ('IX_keycap_sets_is_available', 'keycap_sets'),
    ('IX_stabilizers_is_available', 'stabilizers'),
    ('IX_case_layouts_layout_id', 'case_layouts'),
    ('IX_pcb_layouts_layout_id', 'pcb_layouts'),
    ('IX_plate_layouts_layout_id', 'plate_layouts'),
    ('IX_builds_user_id', 'builds'),
    ('IX_builds_status', 'builds'),
    ('IX_build_requests_buyer_status', 'build_requests'),
    ('IX_build_requests_seller_status', 'build_requests'),
    ('IX_build_requests_status', 'build_requests')
) AS expected (index_name, table_name)
LEFT JOIN sys.indexes AS i
    ON i.name = expected.index_name
   AND i.object_id = OBJECT_ID(expected.table_name)
ORDER BY expected.index_name;

SELECT
    role_name,
    permissions
FROM roles
ORDER BY role_name;

SELECT
    layout_id,
    layout_name,
    form_factor,
    standard_key_count
FROM layouts
ORDER BY standard_key_count;

SELECT 'brands' AS seed_area, COUNT(*) AS row_count FROM brands
UNION ALL SELECT 'cases_available', COUNT(*) FROM cases WHERE is_available = 1
UNION ALL SELECT 'pcbs_available', COUNT(*) FROM pcbs WHERE is_available = 1
UNION ALL SELECT 'plates_available', COUNT(*) FROM plates WHERE is_available = 1
UNION ALL SELECT 'switches_available', COUNT(*) FROM switches WHERE is_available = 1
UNION ALL SELECT 'keycaps_available', COUNT(*) FROM keycap_sets WHERE is_available = 1
UNION ALL SELECT 'stabilizers_available', COUNT(*) FROM stabilizers WHERE is_available = 1
UNION ALL SELECT 'compatibility_rules', COUNT(*) FROM compatibility_rules
UNION ALL SELECT 'case_layouts', COUNT(*) FROM case_layouts
UNION ALL SELECT 'pcb_layouts', COUNT(*) FROM pcb_layouts
UNION ALL SELECT 'plate_layouts', COUNT(*) FROM plate_layouts
UNION ALL SELECT 'builds', COUNT(*) FROM builds
UNION ALL SELECT 'build_requests', COUNT(*) FROM build_requests;

SELECT
    u.username,
    u.email,
    r.role_name,
    u.is_active
FROM users AS u
INNER JOIN roles AS r ON r.role_id = u.role_id
WHERE u.username IN ('admin_seed', 'buyer_seed', 'seller_seed')
ORDER BY r.role_name, u.username;

SELECT
    sp.shop_name,
    u.username AS seller_username,
    sp.is_verified
FROM seller_profiles AS sp
INNER JOIN users AS u ON u.user_id = sp.user_id
WHERE u.username = 'seller_seed';

SELECT
    build_id,
    user_id,
    layout_id,
    name,
    status,
    total_cost_snapshot
FROM builds
ORDER BY build_id;

SELECT
    request_id,
    build_id,
    buyer_id,
    seller_user_id,
    status,
    note
FROM build_requests
ORDER BY request_id;

SELECT
    'invalid_build_status' AS check_name,
    COUNT(*) AS invalid_count
FROM builds
WHERE status NOT IN ('Draft', 'Saved', 'Requested')
UNION ALL
SELECT
    'invalid_request_status',
    COUNT(*)
FROM build_requests
WHERE status NOT IN ('Pending', 'Accepted', 'In_progress', 'Completed', 'Cancelled');
GO
