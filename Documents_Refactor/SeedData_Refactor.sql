-- Custom Keyboard Builder - Refactor seed dataset
-- Target: Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml
-- Scope: dataset for the refactored ERD only. This does not target the current runtime schema.
-- Prices are sample snapshots for testing/refactor flows, not live market prices.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

MERGE roles AS target
USING (VALUES
    ('Buyer', 'build:create,request:create,chat:seller'),
    ('Seller', 'request:read,request:update,chat:buyer,chat:admin'),
    ('Admin', 'user:manage,seller:manage,catalog:manage,audit:read,chat:seller')
) AS source (role_name, permissions)
ON target.role_name = source.role_name
WHEN MATCHED THEN
    UPDATE SET permissions = source.permissions
WHEN NOT MATCHED THEN
    INSERT (role_name, permissions)
    VALUES (source.role_name, source.permissions);

MERGE users AS target
USING (
    SELECT r.role_id, v.username, v.email, v.phone, v.password_hash, v.is_active
    FROM (VALUES
        ('admin_refactor', 'admin.refactor@example.com', '0900000101', 'PBKDF2-DEMO-HASH-admin-refactor', 1, 'Admin'),
        ('buyer_refactor', 'buyer.refactor@example.com', '0900000102', 'PBKDF2-DEMO-HASH-buyer-refactor', 1, 'Buyer'),
        ('buyer_second', 'buyer.second@example.com', '0900000103', 'PBKDF2-DEMO-HASH-buyer-second', 1, 'Buyer'),
        ('seller_soigear', 'seller.soigear@example.com', '0900000201', 'PBKDF2-DEMO-HASH-seller-soigear', 1, 'Seller'),
        ('seller_keyboardlab', 'seller.keyboardlab@example.com', '0900000202', 'PBKDF2-DEMO-HASH-seller-keyboardlab', 1, 'Seller'),
        ('seller_unverified', 'seller.unverified@example.com', '0900000203', 'PBKDF2-DEMO-HASH-seller-unverified', 1, 'Seller'),
        ('buyer_inactive', 'buyer.inactive@example.com', '0900000104', 'PBKDF2-DEMO-HASH-buyer-inactive', 0, 'Buyer')
    ) AS v (username, email, phone, password_hash, is_active, role_name)
    INNER JOIN roles AS r ON r.role_name = v.role_name
) AS source
ON target.username = source.username
WHEN MATCHED THEN
    UPDATE SET
        role_id = source.role_id,
        email = source.email,
        phone = source.phone,
        password_hash = source.password_hash,
        is_active = source.is_active
WHEN NOT MATCHED THEN
    INSERT (role_id, username, email, phone, password_hash, is_active)
    VALUES (source.role_id, source.username, source.email, source.phone, source.password_hash, source.is_active);

MERGE seller_profiles AS target
USING (
    SELECT u.user_id, v.shop_name, v.phone, v.address, v.is_verified, v.verified_at
    FROM (VALUES
        ('seller_soigear', 'Soigear Refactor Shop', '0900000201', 'District 1, Ho Chi Minh City', 1, CAST('2026-06-01T09:00:00' AS datetime)),
        ('seller_keyboardlab', 'Keyboard Lab VN', '0900000202', 'Cau Giay, Ha Noi', 1, CAST('2026-06-02T10:30:00' AS datetime)),
        ('seller_unverified', 'Pending Keyboard Studio', '0900000203', 'Thu Duc, Ho Chi Minh City', 0, NULL)
    ) AS v (username, shop_name, phone, address, is_verified, verified_at)
    INNER JOIN users AS u ON u.username = v.username
) AS source
ON target.user_id = source.user_id
WHEN MATCHED THEN
    UPDATE SET
        shop_name = source.shop_name,
        phone = source.phone,
        address = source.address,
        is_verified = source.is_verified,
        verified_at = source.verified_at
WHEN NOT MATCHED THEN
    INSERT (user_id, shop_name, phone, address, is_verified, verified_at)
    VALUES (source.user_id, source.shop_name, source.phone, source.address, source.is_verified, source.verified_at);

MERGE brands AS target
USING (VALUES
    ('AKKO', 'China'),
    ('AULA', 'China'),
    ('BSUN', 'China'),
    ('Durock', 'China'),
    ('Everglide', 'China'),
    ('Gateron', 'China'),
    ('Generic', 'Unknown'),
    ('KBDfans', 'China'),
    ('Meletrix', 'China'),
    ('NeoStudio', 'China'),
    ('Owlab', 'China'),
    ('Qwertykeys', 'China'),
    ('WuqueStudio', 'China')
) AS source (brand_name, country)
ON target.brand_name = source.brand_name
WHEN MATCHED THEN
    UPDATE SET country = source.country
WHEN NOT MATCHED THEN
    INSERT (brand_name, country)
    VALUES (source.brand_name, source.country);

MERGE layouts AS target
USING (VALUES
    ('LAYOUT_65', '65%', '65%', 65),
    ('LAYOUT_75', '75%', '75%', 81),
    ('LAYOUT_TKL', 'TKL / 80%', 'TKL', 87),
    ('LAYOUT_100', 'Full-size / 100%', '100%', 104)
) AS source (layout_id, layout_name, form_factor, key_count)
ON target.layout_id = source.layout_id
WHEN MATCHED THEN
    UPDATE SET
        layout_name = source.layout_name,
        form_factor = source.form_factor,
        key_count = source.key_count
WHEN NOT MATCHED THEN
    INSERT (layout_id, layout_name, form_factor, key_count)
    VALUES (source.layout_id, source.layout_name, source.form_factor, source.key_count);

MERGE keyboard_kits AS target
USING (
    SELECT v.kit_id, b.brand_id, v.layout_id, v.kit_name, v.pcb_technology, v.switch_mount,
           v.required_switch_quantity, v.included_parts, v.price_usd, v.is_available
    FROM (VALUES
        ('KIT_NEO65', 'NeoStudio', 'LAYOUT_65', 'Neo65 Barebone Kit', 'Mechanical', 'MX 5-pin', 70, 'Case, PCB, plate, foam, daughterboard, cable', 140.00, 1),
        ('KIT_QK65', 'Qwertykeys', 'LAYOUT_65', 'QK65 Barebone Kit', 'Mechanical', 'MX 3-pin', 70, 'Case, PCB, plate, foam, carrying case', 274.40, 1),
        ('KIT_ZOOM65', 'Meletrix', 'LAYOUT_65', 'Zoom65 Essential Kit', 'Mechanical', 'MX 5-pin', 70, 'Case, PCB, plate, foam, cable', 180.00, 1),
        ('KIT_NEO75', 'NeoStudio', 'LAYOUT_75', 'Neo75 Barebone Kit', 'Mechanical', 'MX 5-pin', 85, 'Case, PCB, plate, foam, cable', 228.00, 1),
        ('KIT_BOOG75_HE', 'Meletrix', 'LAYOUT_75', 'BOOG75 Hall Effect Kit', 'HE', 'HE', 85, 'Case, HE PCB, plate, foam, cable', 220.00, 1),
        ('KIT_AULA_S75_PRO', 'AULA', 'LAYOUT_75', 'AULA S75 Pro Kit', 'Mechanical', 'MX 3-pin', 85, 'Case, PCB, plate, foam', 40.00, 1),
        ('KIT_NEO80', 'NeoStudio', 'LAYOUT_TKL', 'Neo80 Barebone Kit', 'Mechanical', 'MX 5-pin', 90, 'Case, PCB, plate, foam, cable', 250.00, 1),
        ('KIT_NEO100', 'NeoStudio', 'LAYOUT_100', 'Neo100 Barebone Kit', 'Mechanical', 'MX 3-pin', 108, 'Case, PCB, plate, foam, cable', 220.00, 1),
        ('KIT_TOFU65_ARCHIVE', 'KBDfans', 'LAYOUT_65', 'Tofu65 Archive Kit', 'Mechanical', 'MX 3-pin', 70, 'Case, PCB, plate', 180.00, 0)
    ) AS v (kit_id, brand_name, layout_id, kit_name, pcb_technology, switch_mount, required_switch_quantity, included_parts, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.kit_id = source.kit_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        layout_id = source.layout_id,
        kit_name = source.kit_name,
        pcb_technology = source.pcb_technology,
        switch_mount = source.switch_mount,
        required_switch_quantity = source.required_switch_quantity,
        included_parts = source.included_parts,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (kit_id, brand_id, layout_id, kit_name, pcb_technology, switch_mount, required_switch_quantity, included_parts, price_usd, is_available)
    VALUES (source.kit_id, source.brand_id, source.layout_id, source.kit_name, source.pcb_technology, source.switch_mount, source.required_switch_quantity, source.included_parts, source.price_usd, source.is_available);

MERGE switches AS target
USING (
    SELECT v.switch_id, b.brand_id, v.switch_name, v.switch_technology, v.mount_type,
           v.switch_type, v.actuation_force_g, v.price_usd, v.is_available
    FROM (VALUES
        ('SW_GATERON_OIL_KING', 'Gateron', 'Gateron Oil King', 'Mechanical', 'MX 3-pin', 'Linear', 55, 0.66, 1),
        ('SW_GATERON_BOX_INK_V2', 'Gateron', 'Gateron Box Ink Black V2', 'Mechanical', 'MX 5-pin', 'Linear', 60, 0.72, 1),
        ('SW_OWLAB_LONDON_FOG_V2', 'Owlab', 'Owlab London Fog V2', 'Mechanical', 'MX 5-pin', 'Linear', 55, 0.74, 1),
        ('SW_WS_MORANDI', 'WuqueStudio', 'WS Morandi', 'Mechanical', 'MX 5-pin', 'Linear', 50, 0.46, 1),
        ('SW_NEO_AZURE', 'NeoStudio', 'Neo Azure/Oat/Amber/Rye', 'Mechanical', 'MX 5-pin', 'Linear', 45, 0.34, 1),
        ('SW_GATERON_QUINN', 'Gateron', 'Gateron Quinn', 'Mechanical', 'MX 3-pin', 'Tactile', 59, 0.58, 1),
        ('SW_WS_LIGHT_TACTILE', 'WuqueStudio', 'WS Light Tactile', 'Mechanical', 'MX 5-pin', 'Tactile', 45, 0.48, 1),
        ('SW_GATERON_MAGNETIC_JADE_PRO', 'Gateron', 'Gateron Magnetic Jade Pro HE', 'HE', 'HE', 'Linear', 38, 1.00, 1),
        ('SW_WS_FLUX_HE', 'WuqueStudio', 'WS Flux Magnetic HE', 'HE', 'HE', 'Linear', 42, 0.74, 1),
        ('SW_BSUN_HUTT', 'BSUN', 'BSUN Hutt', 'Mechanical', 'MX 5-pin', 'Tactile', 40, 0.60, 0)
    ) AS v (switch_id, brand_name, switch_name, switch_technology, mount_type, switch_type, actuation_force_g, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.switch_id = source.switch_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        switch_name = source.switch_name,
        switch_technology = source.switch_technology,
        mount_type = source.mount_type,
        switch_type = source.switch_type,
        actuation_force_g = source.actuation_force_g,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (switch_id, brand_id, switch_name, switch_technology, mount_type, switch_type, actuation_force_g, price_usd, is_available)
    VALUES (source.switch_id, source.brand_id, source.switch_name, source.switch_technology, source.mount_type, source.switch_type, source.actuation_force_g, source.price_usd, source.is_available);

MERGE keycap_sets AS target
USING (
    SELECT v.keycap_id, b.brand_id, v.keycap_name, v.supported_form_factor,
           v.profile, v.material, v.price_usd, v.is_available
    FROM (VALUES
        ('KC_GENERIC_PBT_CHERRY_UNIVERSAL', 'Generic', 'Generic PBT Cherry Universal', '60/65/75/TKL/100', 'Cherry', 'PBT', 35.00, 1),
        ('KC_GENERIC_ABS_OEM_DARK', 'Generic', 'Generic ABS OEM Dark', '60/65/75/TKL/100', 'OEM', 'ABS', 25.00, 1),
        ('KC_KBDFANS_CHERRY_65', 'KBDfans', 'KBDfans Cherry 65 Keycap Set', '60/65', 'Cherry', 'PBT', 55.00, 1),
        ('KC_AKKO_MDA_75PLUS', 'AKKO', 'AKKO MDA 75/TKL Keycap Set', '75/TKL/100', 'MDA', 'PBT', 45.00, 1),
        ('KC_ARCHIVE_DYE_SUB', 'Generic', 'Archive Dye-sub Keycap Set', '60/65', 'Cherry', 'PBT', 20.00, 0)
    ) AS v (keycap_id, brand_name, keycap_name, supported_form_factor, profile, material, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.keycap_id = source.keycap_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        keycap_name = source.keycap_name,
        supported_form_factor = source.supported_form_factor,
        profile = source.profile,
        material = source.material,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (keycap_id, brand_id, keycap_name, supported_form_factor, profile, material, price_usd, is_available)
    VALUES (source.keycap_id, source.brand_id, source.keycap_name, source.supported_form_factor, source.profile, source.material, source.price_usd, source.is_available);

MERGE stabilizers AS target
USING (
    SELECT v.stab_id, b.brand_id, v.stab_name, v.supported_layouts, v.price_usd, v.is_available
    FROM (VALUES
        ('ST_DUROCK_V2_65_75', 'Durock', 'Durock V2 Stabilizer Package', '60/65/75/TKL', 18.00, 1),
        ('ST_EVERGLIDE_PANDA_UNIVERSAL', 'Everglide', 'Everglide Panda Stabilizer Package', '60/65/75/TKL/100', 16.00, 1),
        ('ST_GENERIC_FULLSIZE', 'Generic', 'Generic Full-size Stabilizer Package', '100', 12.00, 1),
        ('ST_ARCHIVE_BASIC_65', 'Generic', 'Archive Basic 65 Stabilizer Package', '60/65', 8.00, 0)
    ) AS v (stab_id, brand_name, stab_name, supported_layouts, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.stab_id = source.stab_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        stab_name = source.stab_name,
        supported_layouts = source.supported_layouts,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (stab_id, brand_id, stab_name, supported_layouts, price_usd, is_available)
    VALUES (source.stab_id, source.brand_id, source.stab_name, source.supported_layouts, source.price_usd, source.is_available);

MERGE accessories AS target
USING (VALUES
    ('ACC_KRYTOX_205G0', 'Lube', 'Krytox 205g0 5ml', 'Switch', 8.50, 1),
    ('ACC_TRIBOSYS_3204', 'Lube', 'Tribosys 3204 5ml', 'Stabilizer', 7.00, 1),
    ('ACC_SWITCH_FILMS', 'Film', 'HTV Switch Films Pack', 'Switch', 5.00, 1),
    ('ACC_USB_C_COIL', 'Cable', 'USB-C Coiled Cable', 'General', 18.00, 1),
    ('ACC_PORON_FOAM', 'Foam', 'Poron Case Foam Sheet', 'Kit', 12.00, 1),
    ('ACC_SWITCH_PULLER', 'Tool', 'Switch Puller', 'General', 4.00, 1),
    ('ACC_ARCHIVE_CABLE', 'Cable', 'Archive USB Cable', 'General', 6.00, 0)
) AS source (accessory_id, accessory_type, accessory_name, target_component, price_usd, is_available)
ON target.accessory_id = source.accessory_id
WHEN MATCHED THEN
    UPDATE SET
        accessory_type = source.accessory_type,
        accessory_name = source.accessory_name,
        target_component = source.target_component,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (accessory_id, accessory_type, accessory_name, target_component, price_usd, is_available)
    VALUES (source.accessory_id, source.accessory_type, source.accessory_name, source.target_component, source.price_usd, source.is_available);

MERGE builds AS target
USING (
    SELECT buyer.user_id AS buyer_id, v.build_id, v.kit_id, v.name, v.notes, v.status, v.total_cost_snapshot, v.created_at, v.updated_at
    FROM (VALUES
        ('BUILD_REF_NEO65_MECH', 'KIT_NEO65', 'Neo65 Cream Linear Build', 'Neo65 with Neo Azure switches, PBT keycaps, Durock stabs and switch lube.', 'Saved', 225.30, CAST('2026-06-03T08:00:00' AS datetime), CAST('2026-06-03T08:30:00' AS datetime)),
        ('BUILD_REF_BOOG75_HE', 'KIT_BOOG75_HE', 'BOOG75 HE Gaming Build', 'HE kit with Magnetic Jade switches and coiled cable.', 'Requested', 384.00, CAST('2026-06-04T09:00:00' AS datetime), CAST('2026-06-04T09:20:00' AS datetime)),
        ('BUILD_REF_QK65_THOCK', 'KIT_QK65', 'QK65 Thock Build', 'QK65 3-pin build with Oil King switches and switch films.', 'Requested', 368.60, CAST('2026-06-05T10:00:00' AS datetime), CAST('2026-06-05T10:15:00' AS datetime)),
        ('BUILD_REF_AULA_DRAFT', 'KIT_AULA_S75_PRO', 'AULA Budget Draft', 'Draft build missing keycap and stabilizer choices for UI warning tests.', 'Draft', 89.30, CAST('2026-06-06T11:00:00' AS datetime), NULL),
        ('BUILD_REF_ARCHIVED_NEO80', 'KIT_NEO80', 'Archived Neo80 Office Build', 'Archived sample build for build list filtering.', 'Archived', 349.40, CAST('2026-06-01T07:00:00' AS datetime), CAST('2026-06-07T12:00:00' AS datetime))
    ) AS v (build_id, kit_id, name, notes, status, total_cost_snapshot, created_at, updated_at)
    CROSS JOIN users AS buyer
    WHERE buyer.username = 'buyer_refactor'
) AS source
ON target.build_id = source.build_id
WHEN MATCHED THEN
    UPDATE SET
        buyer_id = source.buyer_id,
        kit_id = source.kit_id,
        name = source.name,
        notes = source.notes,
        status = source.status,
        total_cost_snapshot = source.total_cost_snapshot,
        created_at = source.created_at,
        updated_at = source.updated_at
WHEN NOT MATCHED THEN
    INSERT (build_id, buyer_id, kit_id, name, notes, status, total_cost_snapshot, created_at, updated_at)
    VALUES (source.build_id, source.buyer_id, source.kit_id, source.name, source.notes, source.status, source.total_cost_snapshot, source.created_at, source.updated_at);

INSERT INTO build_items (build_id, switch_id, keycap_id, stab_id, accessory_id, quantity, unit_price_snapshot, notes)
SELECT source.build_id, source.switch_id, source.keycap_id, source.stab_id, source.accessory_id, source.quantity, source.unit_price_snapshot, source.notes
FROM (VALUES
    ('BUILD_REF_NEO65_MECH', 'SW_NEO_AZURE', NULL, NULL, NULL, 70, 0.34, '70 switches for 65% kit'),
    ('BUILD_REF_NEO65_MECH', NULL, 'KC_GENERIC_PBT_CHERRY_UNIVERSAL', NULL, NULL, 1, 35.00, 'Universal PBT keycap set'),
    ('BUILD_REF_NEO65_MECH', NULL, NULL, 'ST_DUROCK_V2_65_75', NULL, 1, 18.00, 'Stabilizer package for 65% layout'),
    ('BUILD_REF_NEO65_MECH', NULL, NULL, NULL, 'ACC_KRYTOX_205G0', 1, 8.50, 'Switch lube add-on'),

    ('BUILD_REF_BOOG75_HE', 'SW_GATERON_MAGNETIC_JADE_PRO', NULL, NULL, NULL, 85, 1.00, '85 HE switches for BOOG75'),
    ('BUILD_REF_BOOG75_HE', NULL, 'KC_AKKO_MDA_75PLUS', NULL, NULL, 1, 45.00, '75/TKL compatible keycaps'),
    ('BUILD_REF_BOOG75_HE', NULL, NULL, 'ST_EVERGLIDE_PANDA_UNIVERSAL', NULL, 1, 16.00, 'Universal stabilizer package'),
    ('BUILD_REF_BOOG75_HE', NULL, NULL, NULL, 'ACC_USB_C_COIL', 1, 18.00, 'Cable accessory'),

    ('BUILD_REF_QK65_THOCK', 'SW_GATERON_OIL_KING', NULL, NULL, NULL, 70, 0.66, '70 switches for 65% kit'),
    ('BUILD_REF_QK65_THOCK', NULL, 'KC_GENERIC_ABS_OEM_DARK', NULL, NULL, 1, 25.00, 'OEM keycap set'),
    ('BUILD_REF_QK65_THOCK', NULL, NULL, 'ST_DUROCK_V2_65_75', NULL, 1, 18.00, 'Stabilizer package for 65% layout'),
    ('BUILD_REF_QK65_THOCK', NULL, NULL, NULL, 'ACC_SWITCH_FILMS', 1, 5.00, 'Switch film pack'),

    ('BUILD_REF_AULA_DRAFT', 'SW_GATERON_QUINN', NULL, NULL, NULL, 85, 0.58, 'Draft switch choice only'),

    ('BUILD_REF_ARCHIVED_NEO80', 'SW_WS_MORANDI', NULL, NULL, NULL, 90, 0.46, '90 switches for TKL kit'),
    ('BUILD_REF_ARCHIVED_NEO80', NULL, 'KC_GENERIC_PBT_CHERRY_UNIVERSAL', NULL, NULL, 1, 35.00, 'Universal keycap set'),
    ('BUILD_REF_ARCHIVED_NEO80', NULL, NULL, 'ST_EVERGLIDE_PANDA_UNIVERSAL', NULL, 1, 16.00, 'Universal stabilizer package'),
    ('BUILD_REF_ARCHIVED_NEO80', NULL, NULL, NULL, 'ACC_TRIBOSYS_3204', 1, 7.00, 'Stabilizer lube add-on')
) AS source (build_id, switch_id, keycap_id, stab_id, accessory_id, quantity, unit_price_snapshot, notes)
WHERE NOT EXISTS (
    SELECT 1
    FROM build_items AS existing
    WHERE existing.build_id = source.build_id
      AND ISNULL(existing.switch_id, '') = ISNULL(source.switch_id, '')
      AND ISNULL(existing.keycap_id, '') = ISNULL(source.keycap_id, '')
      AND ISNULL(existing.stab_id, '') = ISNULL(source.stab_id, '')
      AND ISNULL(existing.accessory_id, '') = ISNULL(source.accessory_id, '')
);

INSERT INTO build_mods (build_id, mod_type, target_component, notes)
SELECT source.build_id, source.mod_type, source.target_component, source.notes
FROM (VALUES
    ('BUILD_REF_NEO65_MECH', 'Lube', 'Switch', 'Apply Krytox 205g0 lightly to switch rails.'),
    ('BUILD_REF_NEO65_MECH', 'Tune', 'Stabilizer', 'Balance wire and tune stabilizers before assembly.'),
    ('BUILD_REF_BOOG75_HE', 'Calibration', 'Build', 'Run HE switch calibration after assembly.'),
    ('BUILD_REF_QK65_THOCK', 'Film', 'Switch', 'Install switch films before lubing.'),
    ('BUILD_REF_ARCHIVED_NEO80', 'Lube', 'Stabilizer', 'Stabilizer lube requested for archived sample.')
) AS source (build_id, mod_type, target_component, notes)
WHERE NOT EXISTS (
    SELECT 1
    FROM build_mods AS existing
    WHERE existing.build_id = source.build_id
      AND existing.mod_type = source.mod_type
      AND existing.target_component = source.target_component
      AND ISNULL(existing.notes, '') = ISNULL(source.notes, '')
);

MERGE build_requests AS target
USING (
    SELECT v.request_id, v.build_id, seller.user_id AS seller_user_id, v.request_payload_json, v.status, v.note,
           v.requested_at, v.accepted_at, v.completed_at, v.updated_at
    FROM (VALUES
        (
            'REQ_REF_BOOG75_PENDING',
            'BUILD_REF_BOOG75_HE',
            'seller_soigear',
            N'{"build_id":"BUILD_REF_BOOG75_HE","kit_id":"KIT_BOOG75_HE","seller":"seller_soigear","items":[{"switch_id":"SW_GATERON_MAGNETIC_JADE_PRO","quantity":85},{"keycap_id":"KC_AKKO_MDA_75PLUS","quantity":1},{"stab_id":"ST_EVERGLIDE_PANDA_UNIVERSAL","quantity":1},{"accessory_id":"ACC_USB_C_COIL","quantity":1}],"total_cost_snapshot":384.00}',
            'Pending',
            'Please confirm HE calibration after assembly.',
            CAST('2026-06-04T09:30:00' AS datetime),
            NULL,
            NULL,
            NULL
        ),
        (
            'REQ_REF_QK65_COMPLETED',
            'BUILD_REF_QK65_THOCK',
            'seller_keyboardlab',
            N'{"build_id":"BUILD_REF_QK65_THOCK","kit_id":"KIT_QK65","seller":"seller_keyboardlab","items":[{"switch_id":"SW_GATERON_OIL_KING","quantity":70},{"keycap_id":"KC_GENERIC_ABS_OEM_DARK","quantity":1},{"stab_id":"ST_DUROCK_V2_65_75","quantity":1},{"accessory_id":"ACC_SWITCH_FILMS","quantity":1}],"total_cost_snapshot":368.60}',
            'Completed',
            'Completed seed request for seller dashboard history.',
            CAST('2026-06-05T10:30:00' AS datetime),
            CAST('2026-06-05T11:00:00' AS datetime),
            CAST('2026-06-06T15:00:00' AS datetime),
            CAST('2026-06-06T15:00:00' AS datetime)
        )
    ) AS v (request_id, build_id, seller_username, request_payload_json, status, note, requested_at, accepted_at, completed_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
) AS source
ON target.request_id = source.request_id
WHEN MATCHED THEN
    UPDATE SET
        build_id = source.build_id,
        seller_user_id = source.seller_user_id,
        request_payload_json = source.request_payload_json,
        status = source.status,
        note = source.note,
        requested_at = source.requested_at,
        accepted_at = source.accepted_at,
        completed_at = source.completed_at,
        updated_at = source.updated_at
WHEN NOT MATCHED THEN
    INSERT (request_id, build_id, seller_user_id, request_payload_json, status, note, requested_at, accepted_at, completed_at, updated_at)
    VALUES (source.request_id, source.build_id, source.seller_user_id, source.request_payload_json, source.status, source.note, source.requested_at, source.accepted_at, source.completed_at, source.updated_at);

INSERT INTO audit_log (user_id, table_name, record_id, action, old_value_json, new_value_json, changed_at)
SELECT admin_user.user_id, source.table_name, source.record_id, source.action, source.old_value_json, source.new_value_json, source.changed_at
FROM (VALUES
    ('seller_profiles', 'seller_soigear', 'VerifySeller', NULL, N'{"is_verified":true}', CAST('2026-06-01T09:00:00' AS datetime)),
    ('keyboard_kits', 'KIT_NEO65', 'SeedCatalogItem', NULL, N'{"is_available":true}', CAST('2026-06-03T07:00:00' AS datetime)),
    ('build_requests', 'REQ_REF_QK65_COMPLETED', 'UpdateRequestStatus', N'{"status":"In_progress"}', N'{"status":"Completed"}', CAST('2026-06-06T15:00:00' AS datetime))
) AS source (table_name, record_id, action, old_value_json, new_value_json, changed_at)
CROSS JOIN users AS admin_user
WHERE admin_user.username = 'admin_refactor'
  AND NOT EXISTS (
      SELECT 1
      FROM audit_log AS existing
      WHERE existing.table_name = source.table_name
        AND existing.record_id = source.record_id
        AND existing.action = source.action
        AND existing.changed_at = source.changed_at
  );

MERGE chat_conversations AS target
USING (
    SELECT v.conversation_id, seller.user_id AS seller_user_id, buyer.user_id AS buyer_id,
           CAST(NULL AS int) AS admin_user_id, v.build_request_id, v.created_at, v.updated_at
    FROM (VALUES
        ('CONV_REF_BOOG75_BUYER_SELLER', 'seller_soigear', 'buyer_refactor', 'REQ_REF_BOOG75_PENDING', CAST('2026-06-04T09:35:00' AS datetime), CAST('2026-06-04T09:38:00' AS datetime))
    ) AS v (conversation_id, seller_username, buyer_username, build_request_id, created_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
    INNER JOIN users AS buyer ON buyer.username = v.buyer_username

    UNION ALL

    SELECT v.conversation_id, seller.user_id AS seller_user_id, CAST(NULL AS int) AS buyer_id,
           admin_user.user_id AS admin_user_id, CAST(NULL AS varchar(50)) AS build_request_id, v.created_at, v.updated_at
    FROM (VALUES
        ('CONV_REF_ADMIN_SELLER', 'seller_keyboardlab', 'admin_refactor', CAST('2026-06-02T13:00:00' AS datetime), CAST('2026-06-02T13:05:00' AS datetime))
    ) AS v (conversation_id, seller_username, admin_username, created_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
    INNER JOIN users AS admin_user ON admin_user.username = v.admin_username
) AS source
ON target.conversation_id = source.conversation_id
WHEN MATCHED THEN
    UPDATE SET
        seller_user_id = source.seller_user_id,
        buyer_id = source.buyer_id,
        admin_user_id = source.admin_user_id,
        build_request_id = source.build_request_id,
        created_at = source.created_at,
        updated_at = source.updated_at
WHEN NOT MATCHED THEN
    INSERT (conversation_id, seller_user_id, buyer_id, admin_user_id, build_request_id, created_at, updated_at)
    VALUES (source.conversation_id, source.seller_user_id, source.buyer_id, source.admin_user_id, source.build_request_id, source.created_at, source.updated_at);

MERGE chat_messages AS target
USING (
    SELECT v.message_id, v.conversation_id, sender.user_id AS sender_user_id, v.message_text, v.sent_at
    FROM (VALUES
        ('MSG_REF_BOOG75_001', 'CONV_REF_BOOG75_BUYER_SELLER', 'buyer_refactor', 'Please calibrate the HE switches after assembly.', CAST('2026-06-04T09:36:00' AS datetime)),
        ('MSG_REF_BOOG75_002', 'CONV_REF_BOOG75_BUYER_SELLER', 'seller_soigear', 'Confirmed. I will run calibration before marking it completed.', CAST('2026-06-04T09:38:00' AS datetime)),
        ('MSG_REF_ADMIN_SELLER_001', 'CONV_REF_ADMIN_SELLER', 'seller_keyboardlab', 'Can you confirm my seller profile is visible to buyers?', CAST('2026-06-02T13:02:00' AS datetime)),
        ('MSG_REF_ADMIN_SELLER_002', 'CONV_REF_ADMIN_SELLER', 'admin_refactor', 'Yes, your profile is verified and active.', CAST('2026-06-02T13:05:00' AS datetime))
    ) AS v (message_id, conversation_id, sender_username, message_text, sent_at)
    INNER JOIN users AS sender ON sender.username = v.sender_username
) AS source
ON target.message_id = source.message_id
WHEN MATCHED THEN
    UPDATE SET
        conversation_id = source.conversation_id,
        sender_user_id = source.sender_user_id,
        message_text = source.message_text,
        sent_at = source.sent_at
WHEN NOT MATCHED THEN
    INSERT (message_id, conversation_id, sender_user_id, message_text, sent_at)
    VALUES (source.message_id, source.conversation_id, source.sender_user_id, source.message_text, source.sent_at);

COMMIT TRANSACTION;
