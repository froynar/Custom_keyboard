:ON ERROR EXIT
-- Immutable baseline definitions for schema version 2026.07.27-read-views.
-- Do not add columns introduced by later migrations to this file.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'views') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA views AUTHORIZATION dbo;');
END;
GO

CREATE OR ALTER VIEW views.Last_QC
AS
WITH session_summary AS (
    SELECT
        session_row.id AS session_id,
        session_row.request_id,
        session_row.device_id,
        session_row.switch_technology,
        session_row.noise_requirement,
        session_row.total_keys,
        session_row.status,
        COUNT(result_row.id) AS tested_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Pass' THEN 1 ELSE 0 END), 0) AS passed_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Warning' THEN 1 ELSE 0 END), 0) AS warning_keys,
        COALESCE(SUM(CASE WHEN result_row.result = 'Fail' THEN 1 ELSE 0 END), 0) AS failed_keys,
        CAST(AVG(CAST(result_row.latency AS decimal(18,4))) AS decimal(8,2)) AS average_latency_ms,
        MAX(result_row.latency) AS max_latency_ms,
        CAST(AVG(CAST(result_row.noise AS decimal(18,4))) AS decimal(8,2)) AS average_noise_db,
        MAX(result_row.noise) AS max_noise_db,
        MAX(result_row.recorded_at) AS last_recorded_at
    FROM dbo.device_test_sessions AS session_row
    LEFT JOIN dbo.device_key_test_results AS result_row
        ON result_row.session_id = session_row.id
    GROUP BY
        session_row.id,
        session_row.request_id,
        session_row.device_id,
        session_row.switch_technology,
        session_row.noise_requirement,
        session_row.total_keys,
        session_row.status
),
ranked_sessions AS (
    SELECT
        session_summary.*,
        ROW_NUMBER() OVER (
            PARTITION BY session_summary.request_id
            ORDER BY
                CASE WHEN session_summary.status = 'Running' THEN 0 ELSE 1 END,
                session_summary.last_recorded_at DESC,
                session_summary.session_id DESC
        ) AS latest_rank
    FROM session_summary
)
SELECT
    session_id,
    request_id,
    device_id,
    switch_technology,
    noise_requirement,
    total_keys,
    status,
    tested_keys,
    passed_keys,
    warning_keys,
    failed_keys,
    average_latency_ms,
    max_latency_ms,
    average_noise_db,
    max_noise_db,
    last_recorded_at,
    CAST(
        CASE
            WHEN status <> 'Running' AND tested_keys = total_keys THEN 1
            ELSE 0
        END
        AS bit
    ) AS is_complete,
    CAST(
        CASE
            WHEN status IN ('Passed', 'Warning')
             AND tested_keys = total_keys
             AND failed_keys = 0
             AND (
                    (status = 'Passed' AND warning_keys = 0)
                 OR (status = 'Warning' AND warning_keys > 0)
             )
                THEN 1
            ELSE 0
        END
        AS bit
    ) AS is_acceptable
FROM ranked_sessions
WHERE latest_rank = 1;
GO

CREATE OR ALTER VIEW views.Catalog_Comps
AS
SELECT
    CAST('Kit' AS varchar(20)) AS component_type,
    kit_row.id AS component_id,
    kit_row.kit_name AS name,
    kit_row.brand_id,
    brand_row.brand_name,
    kit_row.price_usd,
    kit_row.is_available,
    kit_row.layout_id,
    kit_row.pcb_technology,
    kit_row.switch_mount,
    kit_row.required_switch_quantity,
    kit_row.included_parts,
    CAST('' AS varchar(50)) AS switch_technology,
    CAST('' AS varchar(100)) AS mount_type,
    CAST('' AS varchar(100)) AS switch_type,
    CAST(NULL AS int) AS actuation_force_g,
    CAST('' AS varchar(255)) AS supported_form_factor,
    CAST('' AS varchar(100)) AS profile,
    CAST('' AS varchar(100)) AS material,
    CAST('' AS varchar(255)) AS supported_layouts,
    CAST('' AS varchar(100)) AS accessory_type,
    CAST('' AS varchar(100)) AS target_component
FROM dbo.keyboard_kits AS kit_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = kit_row.brand_id

UNION ALL

SELECT
    CAST('Switch' AS varchar(20)),
    switch_row.id,
    switch_row.switch_name,
    switch_row.brand_id,
    brand_row.brand_name,
    switch_row.price_usd,
    switch_row.is_available,
    CAST('' AS varchar(50)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    0,
    CAST('' AS varchar(500)),
    switch_row.switch_technology,
    switch_row.mount_type,
    switch_row.switch_type,
    switch_row.actuation_force_g,
    CAST('' AS varchar(255)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(255)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100))
FROM dbo.switches AS switch_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = switch_row.brand_id

UNION ALL

SELECT
    CAST('KeycapSet' AS varchar(20)),
    keycap_row.id,
    keycap_row.keycap_name,
    keycap_row.brand_id,
    brand_row.brand_name,
    keycap_row.price_usd,
    keycap_row.is_available,
    CAST('' AS varchar(50)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    0,
    CAST('' AS varchar(500)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    CAST(NULL AS int),
    keycap_row.supported_form_factor,
    keycap_row.profile,
    keycap_row.material,
    CAST('' AS varchar(255)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100))
FROM dbo.keycap_sets AS keycap_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = keycap_row.brand_id

UNION ALL

SELECT
    CAST('Stabilizer' AS varchar(20)),
    stabilizer_row.id,
    stabilizer_row.stab_name,
    stabilizer_row.brand_id,
    brand_row.brand_name,
    stabilizer_row.price_usd,
    stabilizer_row.is_available,
    CAST('' AS varchar(50)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    0,
    CAST('' AS varchar(500)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    CAST(NULL AS int),
    CAST('' AS varchar(255)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    stabilizer_row.supported_layouts,
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100))
FROM dbo.stabilizers AS stabilizer_row
INNER JOIN dbo.brands AS brand_row ON brand_row.id = stabilizer_row.brand_id

UNION ALL

SELECT
    CAST('Accessory' AS varchar(20)),
    accessory_row.id,
    accessory_row.accessory_name,
    0,
    CAST('' AS varchar(255)),
    accessory_row.price_usd,
    accessory_row.is_available,
    CAST('' AS varchar(50)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    0,
    CAST('' AS varchar(500)),
    CAST('' AS varchar(50)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    CAST(NULL AS int),
    CAST('' AS varchar(255)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(100)),
    CAST('' AS varchar(255)),
    accessory_row.accessory_type,
    accessory_row.target_component
FROM dbo.accessories AS accessory_row;
GO

CREATE OR ALTER VIEW views.Build_items
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
    CAST(item_row.quantity * item_row.unit_price_snapshot AS decimal(18,2)) AS line_total_snapshot,
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

CREATE OR ALTER VIEW views.Req_view
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
    buyer_user.username AS buyer_username,
    build_row.name AS build_name,
    build_row.status AS build_status,
    build_row.noise_requirement,
    build_row.total_cost_snapshot,
    build_row.kit_id,
    kit_row.kit_name,
    latest_qc.session_id AS qc_session_id,
    latest_qc.status AS qc_status,
    latest_qc.total_keys AS qc_total_keys,
    latest_qc.tested_keys AS qc_tested_keys,
    latest_qc.passed_keys AS qc_passed_keys,
    latest_qc.warning_keys AS qc_warning_keys,
    latest_qc.failed_keys AS qc_failed_keys,
    latest_qc.average_latency_ms AS qc_average_latency_ms,
    latest_qc.average_noise_db AS qc_average_noise_db,
    latest_qc.last_recorded_at AS qc_last_recorded_at,
    latest_qc.is_complete AS qc_is_complete,
    latest_qc.is_acceptable AS qc_is_acceptable
FROM dbo.build_requests AS request_row
INNER JOIN dbo.builds AS build_row ON build_row.id = request_row.build_id
INNER JOIN dbo.users AS buyer_user ON buyer_user.id = build_row.buyer_id
INNER JOIN dbo.keyboard_kits AS kit_row ON kit_row.id = build_row.kit_id
LEFT JOIN dbo.users AS seller_user ON seller_user.id = request_row.seller_user_id
LEFT JOIN dbo.seller_profiles AS seller_profile ON seller_profile.user_id = request_row.seller_user_id
LEFT JOIN views.Last_QC AS latest_qc ON latest_qc.request_id = request_row.id;
GO
