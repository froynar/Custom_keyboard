:ON ERROR EXIT
-- Simplified read-view contract for schema version 2026.07.27-simplified-read-views.
-- Keep the views focused on reusable data; type-specific catalog details stay in
-- their source tables and are joined by the repository only when needed.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'views') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA views AUTHORIZATION dbo;');
END;
GO

BEGIN TRANSACTION;
GO

-- Xóa theo thứ tự phụ thuộc để có thể chạy lại script bằng CREATE VIEW.
DROP VIEW IF EXISTS views.Req_view;
DROP VIEW IF EXISTS views.Build_items;
DROP VIEW IF EXISTS views.Catalog_Comps;
DROP VIEW IF EXISTS views.Last_QC;
GO

-- Last_QC: Lấy phiên QC mới nhất và tổng hợp kết quả kiểm tra của từng yêu cầu build.
CREATE VIEW views.Last_QC
AS
WITH latest_session AS (
    SELECT
        session_row.*,
        ROW_NUMBER() OVER (
            PARTITION BY session_row.request_id
            ORDER BY
                CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
                session_row.completed_at DESC,
                session_row.id DESC
        ) AS latest_rank
    FROM dbo.device_test_sessions AS session_row
)
SELECT
    session_row.id AS session_id,
    session_row.request_id,
    session_row.device_id,
    session_row.switch_technology,
    session_row.noise_requirement,
    session_row.total_keys,
    session_row.status,
    session_row.completed_at,
    result_summary.tested_keys,
    result_summary.passed_keys,
    result_summary.warning_keys,
    result_summary.failed_keys,
    result_summary.average_latency_ms,
    result_summary.max_latency_ms,
    result_summary.average_noise_db,
    result_summary.max_noise_db,
    result_summary.last_recorded_at,
    CAST(
        CASE
            WHEN session_row.status <> 'Running'
             AND result_summary.tested_keys = session_row.total_keys
                THEN 1
            ELSE 0
        END
        AS bit
    ) AS is_complete,
    CAST(
        CASE
            WHEN session_row.status IN ('Passed', 'Warning')
             AND result_summary.tested_keys = session_row.total_keys
             AND result_summary.failed_keys = 0
             AND (
                    (session_row.status = 'Passed' AND result_summary.warning_keys = 0)
                 OR (session_row.status = 'Warning' AND result_summary.warning_keys > 0)
             )
                THEN 1
            ELSE 0
        END
        AS bit
    ) AS is_acceptable
FROM latest_session AS session_row
OUTER APPLY (
    SELECT
        COUNT(result_row.id) AS tested_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Pass' THEN 1 ELSE 0 END), 0) AS passed_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Warning' THEN 1 ELSE 0 END), 0) AS warning_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Fail' THEN 1 ELSE 0 END), 0) AS failed_keys,
        CAST(AVG(CAST(result_row.latency AS decimal(18,4))) AS decimal(8,2)) AS average_latency_ms,
        MAX(result_row.latency) AS max_latency_ms,
        CAST(AVG(CAST(result_row.noise AS decimal(18,4))) AS decimal(8,2)) AS average_noise_db,
        MAX(result_row.noise) AS max_noise_db,
        MAX(result_row.recorded_at) AS last_recorded_at
    FROM dbo.device_key_test_results AS result_row
    WHERE result_row.session_id = session_row.id
) AS result_summary
WHERE session_row.latest_rank = 1;
GO

-- Catalog_Comps: Gom thông tin chung của tất cả linh kiện vào một danh mục để đọc.
CREATE VIEW views.Catalog_Comps
AS
SELECT
    CAST('Kit' AS varchar(20)) AS component_type,
    kit_row.id AS component_id,
    kit_row.kit_name AS name,
    kit_row.brand_id,
    brand_row.brand_name,
    kit_row.price_usd,
    kit_row.is_available
FROM dbo.keyboard_kits AS kit_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = kit_row.brand_id

UNION ALL

SELECT
    'Switch',
    switch_row.id,
    switch_row.switch_name,
    switch_row.brand_id,
    brand_row.brand_name,
    switch_row.price_usd,
    switch_row.is_available
FROM dbo.switches AS switch_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = switch_row.brand_id

UNION ALL

SELECT
    'KeycapSet',
    keycap_row.id,
    keycap_row.keycap_name,
    keycap_row.brand_id,
    brand_row.brand_name,
    keycap_row.price_usd,
    keycap_row.is_available
FROM dbo.keycap_sets AS keycap_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = keycap_row.brand_id

UNION ALL

SELECT
    'Stabilizer',
    stabilizer_row.id,
    stabilizer_row.stab_name,
    stabilizer_row.brand_id,
    brand_row.brand_name,
    stabilizer_row.price_usd,
    stabilizer_row.is_available
FROM dbo.stabilizers AS stabilizer_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = stabilizer_row.brand_id

UNION ALL

SELECT
    'Accessory',
    accessory_row.id,
    accessory_row.accessory_name,
    CAST(0 AS int),
    CAST('' AS varchar(255)),
    accessory_row.price_usd,
    accessory_row.is_available
FROM dbo.accessories AS accessory_row;
GO

-- Build_items: Hiển thị chi tiết linh kiện trong build kèm giá lịch sử và giá hiện tại.
CREATE VIEW views.Build_items
AS
SELECT
    item_row.id AS build_item_id,
    item_row.build_id,
    item_row.switch_id,
    item_row.keycap_id,
    item_row.stab_id,
    item_row.accessory_id,
    catalog_row.component_type,
    catalog_row.component_id,
    catalog_row.name AS component_name,
    catalog_row.brand_id,
    catalog_row.brand_name,
    item_row.quantity,
    item_row.unit_price_snapshot,
    CAST(
        CAST(item_row.quantity AS decimal(18,0))
        * CAST(item_row.unit_price_snapshot AS decimal(18,2))
        AS decimal(28,2)
    ) AS line_total_snapshot,
    catalog_row.price_usd AS current_price_usd,
    catalog_row.is_available,
    item_row.notes
FROM dbo.build_items AS item_row
INNER JOIN views.Catalog_Comps AS catalog_row
    ON catalog_row.component_type =
        CASE
            WHEN item_row.switch_id IS NOT NULL THEN 'Switch'
            WHEN item_row.keycap_id IS NOT NULL THEN 'KeycapSet'
            WHEN item_row.stab_id IS NOT NULL THEN 'Stabilizer'
            ELSE 'Accessory'
        END
   AND catalog_row.component_id =
        COALESCE(item_row.switch_id, item_row.keycap_id, item_row.stab_id, item_row.accessory_id);
GO

-- Req_view: Gom thông tin yêu cầu build, người bán, tổng tiền và kit liên quan.
CREATE VIEW views.Req_view
AS
SELECT
    request_row.id AS request_id,
    request_row.build_id,
    request_row.seller_user_id,
    COALESCE(
        NULLIF(LTRIM(RTRIM(seller_profile.shop_name)), ''),
        seller_user.username,
        CONVERT(varchar(20), request_row.seller_user_id)
    ) AS seller_shop_name,
    request_row.request_payload_json,
    request_row.status,
    request_row.note,
    request_row.requested_at,
    request_row.accepted_at,
    request_row.completed_at,
    request_row.updated_at,
    build_row.buyer_id,
    build_row.total_cost_snapshot,
    build_row.kit_id,
    kit_row.kit_name
FROM dbo.build_requests AS request_row
INNER JOIN dbo.builds AS build_row ON build_row.id = request_row.build_id
INNER JOIN dbo.keyboard_kits AS kit_row ON kit_row.id = build_row.kit_id
LEFT JOIN dbo.users AS seller_user ON seller_user.id = request_row.seller_user_id
LEFT JOIN dbo.seller_profiles AS seller_profile ON seller_profile.user_id = request_row.seller_user_id;
GO

COMMIT TRANSACTION;
GO
