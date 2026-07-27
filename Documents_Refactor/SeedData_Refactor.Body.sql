-- Internal SQLCMD include. Run SeedData_Refactor.sql, not this body directly.
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
    SELECT r.id AS role_id, v.username, v.email, v.phone, v.password_hash, v.is_active
    -- password_hash below = real PBKDF2-SHA256 hash of "Password123" (Pbkdf2PasswordHasher format).
    -- Every seed account logs in with Password123. Re-running this MERGE also fixes any DB
    -- still holding the old placeholder hashes. (buyer_inactive is banned -> login blocked by is_active.)
    FROM (VALUES
        ('admin_refactor', 'admin.refactor@example.com', '0900000101', 'PBKDF2-SHA256$100000$u+czwNOU+6VTwbiTxJYeeg==$fnxOYROvgpC00LNeZY2XURIMki6uL6jjpYTJDcpoUnw=', 1, 'Admin'),
        ('buyer_refactor', 'buyer.refactor@example.com', '0900000102', 'PBKDF2-SHA256$100000$vfuE4QJ6283VigZl+Cz5Ig==$7oAEqZ/9kGX7IUU23cn7bTo50EbQp+5CyIBejDOednI=', 1, 'Buyer'),
        ('buyer_second', 'buyer.second@example.com', '0900000103', 'PBKDF2-SHA256$100000$BTIVbbCl+QBUgu7G7imKKA==$S5Nq0X9eQT/kmdqkpXilfMCQ+/hBudRu8kcG0gZe/4I=', 1, 'Buyer'),
        ('seller_soigear', 'seller.soigear@example.com', '0900000201', 'PBKDF2-SHA256$100000$RZl15AGpcvDRRqubiU9THg==$8Ca835Cu2F4HFUMpEF8woJ5rXla0jrmJCUscqsyeZ5M=', 1, 'Seller'),
        ('seller_keyboardlab', 'seller.keyboardlab@example.com', '0900000202', 'PBKDF2-SHA256$100000$8mNAeYM+GhS6HOHwB+GE3w==$dmYsF3B4U2kvK6Vb+GeFGYmCrBOC0QXjBH+v26B9CAA=', 1, 'Seller'),
        ('seller_unverified', 'seller.unverified@example.com', '0900000203', 'PBKDF2-SHA256$100000$jb+ng4S4IKGmFPkwxVRJiA==$nF/0FCEmRuOx9+Hlz7VQtpnEoAnl0f3kddEqKG6DIz4=', 1, 'Seller'),
        ('buyer_inactive', 'buyer.inactive@example.com', '0900000104', 'PBKDF2-SHA256$100000$Ckx2r0uNULJoZt4aMBW1ow==$QjnkpSbjOdGtBWK9z6FuTPjDqX703iVQdhUmOu7mSq4=', 0, 'Buyer')
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
    SELECT u.id AS user_id, v.shop_name, v.phone, v.address, v.is_verified, v.verified_at
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
ON target.id = source.layout_id
WHEN MATCHED THEN
    UPDATE SET
        layout_name = source.layout_name,
        form_factor = source.form_factor,
        key_count = source.key_count
WHEN NOT MATCHED THEN
    INSERT (id, layout_name, form_factor, key_count)
    VALUES (source.layout_id, source.layout_name, source.form_factor, source.key_count);

MERGE keyboard_kits AS target
USING (
    SELECT v.kit_id, b.id AS brand_id, v.layout_id, v.kit_name, v.pcb_technology, v.switch_mount,
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
ON target.id = source.kit_id
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
    INSERT (id, brand_id, layout_id, kit_name, pcb_technology, switch_mount, required_switch_quantity, included_parts, price_usd, is_available)
    VALUES (source.kit_id, source.brand_id, source.layout_id, source.kit_name, source.pcb_technology, source.switch_mount, source.required_switch_quantity, source.included_parts, source.price_usd, source.is_available);

MERGE switches AS target
USING (
    SELECT v.switch_id, b.id AS brand_id, v.switch_name, v.switch_technology, v.mount_type,
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
ON target.id = source.switch_id
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
    INSERT (id, brand_id, switch_name, switch_technology, mount_type, switch_type, actuation_force_g, price_usd, is_available)
    VALUES (source.switch_id, source.brand_id, source.switch_name, source.switch_technology, source.mount_type, source.switch_type, source.actuation_force_g, source.price_usd, source.is_available);

MERGE keycap_sets AS target
USING (
    SELECT v.keycap_id, b.id AS brand_id, v.keycap_name, v.supported_form_factor,
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
ON target.id = source.keycap_id
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
    INSERT (id, brand_id, keycap_name, supported_form_factor, profile, material, price_usd, is_available)
    VALUES (source.keycap_id, source.brand_id, source.keycap_name, source.supported_form_factor, source.profile, source.material, source.price_usd, source.is_available);

MERGE stabilizers AS target
USING (
    SELECT v.stab_id, b.id AS brand_id, v.stab_name, v.supported_layouts, v.price_usd, v.is_available
    FROM (VALUES
        ('ST_DUROCK_V2_65_75', 'Durock', 'Durock V2 Stabilizer Package', '60/65/75/TKL', 18.00, 1),
        ('ST_EVERGLIDE_PANDA_UNIVERSAL', 'Everglide', 'Everglide Panda Stabilizer Package', '60/65/75/TKL/100', 16.00, 1),
        ('ST_GENERIC_FULLSIZE', 'Generic', 'Generic Full-size Stabilizer Package', '100', 12.00, 1),
        ('ST_ARCHIVE_BASIC_65', 'Generic', 'Archive Basic 65 Stabilizer Package', '60/65', 8.00, 0)
    ) AS v (stab_id, brand_name, stab_name, supported_layouts, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.id = source.stab_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        stab_name = source.stab_name,
        supported_layouts = source.supported_layouts,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (id, brand_id, stab_name, supported_layouts, price_usd, is_available)
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
ON target.id = source.accessory_id
WHEN MATCHED THEN
    UPDATE SET
        accessory_type = source.accessory_type,
        accessory_name = source.accessory_name,
        target_component = source.target_component,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (id, accessory_type, accessory_name, target_component, price_usd, is_available)
    VALUES (source.accessory_id, source.accessory_type, source.accessory_name, source.target_component, source.price_usd, source.is_available);

MERGE builds AS target
USING (
    SELECT buyer.id AS buyer_id, v.build_id, v.kit_id, v.name, v.notes, v.noise_requirement, v.status, v.total_cost_snapshot, v.created_at, v.updated_at
    FROM (VALUES
        ('BUILD_REF_NEO65_MECH', 'KIT_NEO65', 'Neo65 Cream Linear Build', 'Neo65 with Neo Azure switches, PBT keycaps, Durock stabs and switch lube.', 'Quiet', 'Saved', 225.30, CAST('2026-06-03T08:00:00' AS datetime), CAST('2026-06-03T08:30:00' AS datetime)),
        ('BUILD_REF_BOOG75_HE', 'KIT_BOOG75_HE', 'BOOG75 HE Gaming Build', 'HE kit with Magnetic Jade switches and coiled cable.', 'Silent', 'Requested', 384.00, CAST('2026-06-04T09:00:00' AS datetime), CAST('2026-06-04T09:20:00' AS datetime)),
        ('BUILD_REF_QK65_THOCK', 'KIT_QK65', 'QK65 Thock Build', 'QK65 3-pin build with Oil King switches and switch films.', 'Normal', 'Requested', 368.60, CAST('2026-06-05T10:00:00' AS datetime), CAST('2026-06-05T10:15:00' AS datetime)),
        ('BUILD_REF_AULA_DRAFT', 'KIT_AULA_S75_PRO', 'AULA Budget Draft', 'Draft build missing keycap and stabilizer choices for UI warning tests.', 'Normal', 'Draft', 89.30, CAST('2026-06-06T11:00:00' AS datetime), NULL),
        ('BUILD_REF_ARCHIVED_NEO80', 'KIT_NEO80', 'Archived Neo80 Office Build', 'Archived sample build for build list filtering.', 'Quiet', 'Archived', 349.40, CAST('2026-06-01T07:00:00' AS datetime), CAST('2026-06-07T12:00:00' AS datetime))
    ) AS v (build_id, kit_id, name, notes, noise_requirement, status, total_cost_snapshot, created_at, updated_at)
    CROSS JOIN users AS buyer
    WHERE buyer.username = 'buyer_refactor'
) AS source
ON target.id = source.build_id
WHEN MATCHED THEN
    UPDATE SET
        buyer_id = source.buyer_id,
        kit_id = source.kit_id,
        name = source.name,
        notes = source.notes,
        noise_requirement = source.noise_requirement,
        status = source.status,
        total_cost_snapshot = source.total_cost_snapshot,
        created_at = source.created_at,
        updated_at = source.updated_at
WHEN NOT MATCHED THEN
    INSERT (id, buyer_id, kit_id, name, notes, noise_requirement, status, total_cost_snapshot, created_at, updated_at)
    VALUES (source.build_id, source.buyer_id, source.kit_id, source.name, source.notes, source.noise_requirement, source.status, source.total_cost_snapshot, source.created_at, source.updated_at);

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
    SELECT
        v.request_id,
        v.build_id,
        seller.id AS seller_user_id,
        REPLACE(
            REPLACE(v.request_payload_json_template, N'__SELLER_USER_ID__', CONVERT(nvarchar(20), seller.id)),
            N'__KIT_BRAND_ID__',
            CONVERT(nvarchar(20), kit.brand_id)
        ) AS request_payload_json,
        v.status,
        v.note,
           v.requested_at, v.accepted_at, v.completed_at, v.updated_at
    FROM (VALUES
        (
            'REQ_REF_BOOG75_PENDING',
            'BUILD_REF_BOOG75_HE',
            'seller_soigear',
            N'{"build":{"buildId":"BUILD_REF_BOOG75_HE","name":"BOOG75 HE Gaming Build","notes":"HE kit with Magnetic Jade switches and coiled cable.","noiseRequirement":"Silent","status":"Requested","totalCostSnapshot":384.00,"createdAt":"2026-06-04T09:00:00","updatedAt":"2026-06-04T09:20:00"},"seller":{"sellerUserId":__SELLER_USER_ID__,"shopName":"Soigear Refactor Shop","phone":"0900000201","address":"District 1, Ho Chi Minh City"},"kit":{"kitId":"KIT_BOOG75_HE","kitName":"BOOG75 Hall Effect Kit","brandId":__KIT_BRAND_ID__,"layoutId":"LAYOUT_75","pcbTechnology":"HE","switchMount":"HE","requiredSwitchQuantity":85,"includedParts":"Case, HE PCB, plate, foam, cable","priceUsd":220.00},"items":[{"productType":"Switch","productId":"SW_GATERON_MAGNETIC_JADE_PRO","productName":"Gateron Magnetic Jade Pro HE","quantity":85,"unitPriceSnapshot":1.00,"lineTotal":85.00,"notes":"85 HE switches for BOOG75"},{"productType":"Keycap","productId":"KC_AKKO_MDA_75PLUS","productName":"AKKO MDA 75/TKL Keycap Set","quantity":1,"unitPriceSnapshot":45.00,"lineTotal":45.00,"notes":"75/TKL compatible keycaps"},{"productType":"Stabilizer","productId":"ST_EVERGLIDE_PANDA_UNIVERSAL","productName":"Everglide Panda Stabilizer Package","quantity":1,"unitPriceSnapshot":16.00,"lineTotal":16.00,"notes":"Universal stabilizer package"},{"productType":"Accessory","productId":"ACC_USB_C_COIL","productName":"USB-C Coiled Cable","quantity":1,"unitPriceSnapshot":18.00,"lineTotal":18.00,"notes":"Cable accessory"}],"mods":[{"modType":"Calibration","targetComponent":"Build","notes":"Run HE switch calibration after assembly."}]}',
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
            N'{"build":{"buildId":"BUILD_REF_QK65_THOCK","name":"QK65 Thock Build","notes":"QK65 3-pin build with Oil King switches and switch films.","noiseRequirement":"Normal","status":"Requested","totalCostSnapshot":368.60,"createdAt":"2026-06-05T10:00:00","updatedAt":"2026-06-05T10:15:00"},"seller":{"sellerUserId":__SELLER_USER_ID__,"shopName":"Keyboard Lab VN","phone":"0900000202","address":"Cau Giay, Ha Noi"},"kit":{"kitId":"KIT_QK65","kitName":"QK65 Barebone Kit","brandId":__KIT_BRAND_ID__,"layoutId":"LAYOUT_65","pcbTechnology":"Mechanical","switchMount":"MX 3-pin","requiredSwitchQuantity":70,"includedParts":"Case, PCB, plate, foam, carrying case","priceUsd":274.40},"items":[{"productType":"Switch","productId":"SW_GATERON_OIL_KING","productName":"Gateron Oil King","quantity":70,"unitPriceSnapshot":0.66,"lineTotal":46.20,"notes":"70 switches for 65% kit"},{"productType":"Keycap","productId":"KC_GENERIC_ABS_OEM_DARK","productName":"Generic ABS OEM Dark","quantity":1,"unitPriceSnapshot":25.00,"lineTotal":25.00,"notes":"OEM keycap set"},{"productType":"Stabilizer","productId":"ST_DUROCK_V2_65_75","productName":"Durock V2 Stabilizer Package","quantity":1,"unitPriceSnapshot":18.00,"lineTotal":18.00,"notes":"Stabilizer package for 65% layout"},{"productType":"Accessory","productId":"ACC_SWITCH_FILMS","productName":"HTV Switch Films Pack","quantity":1,"unitPriceSnapshot":5.00,"lineTotal":5.00,"notes":"Switch film pack"}],"mods":[{"modType":"Film","targetComponent":"Switch","notes":"Install switch films before lubing."}]}',
            'Completed',
            'Completed seed request for seller dashboard history.',
            CAST('2026-06-05T10:30:00' AS datetime),
            CAST('2026-06-05T11:00:00' AS datetime),
            CAST('2026-06-06T15:00:00' AS datetime),
            CAST('2026-06-06T15:00:00' AS datetime)
        )
    ) AS v (request_id, build_id, seller_username, request_payload_json_template, status, note, requested_at, accepted_at, completed_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
    INNER JOIN builds AS seed_build ON seed_build.id = v.build_id
    INNER JOIN keyboard_kits AS kit ON kit.id = seed_build.kit_id
) AS source
ON target.id = source.request_id
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
    INSERT (id, build_id, seller_user_id, request_payload_json, status, note, requested_at, accepted_at, completed_at, updated_at)
    VALUES (source.request_id, source.build_id, source.seller_user_id, source.request_payload_json, source.status, source.note, source.requested_at, source.accepted_at, source.completed_at, source.updated_at);

MERGE devices AS target
USING (
    SELECT
        'DEV_REF_QC_01' AS device_id,
        seller.id AS seller_user_id,
        'Keyboard Lab QC Station' AS device_name,
        'QC_STATION' AS device_type,
        CAST(1 AS bit) AS is_active,
        CAST('2026-06-06T14:33:00' AS datetime2) AS last_seen_at,
        CAST('2026-06-02T10:30:00' AS datetime2) AS created_at
    FROM users AS seller
    WHERE seller.username = 'seller_keyboardlab'
) AS source
ON target.id = source.device_id
WHEN MATCHED THEN
    UPDATE SET
        seller_user_id = source.seller_user_id,
        device_name = source.device_name,
        device_type = source.device_type,
        is_active = source.is_active,
        last_seen_at = source.last_seen_at,
        created_at = source.created_at
WHEN NOT MATCHED THEN
    INSERT (id, seller_user_id, device_name, device_type, is_active, last_seen_at, created_at)
    VALUES (source.device_id, source.seller_user_id, source.device_name, source.device_type, source.is_active, source.last_seen_at, source.created_at);

MERGE device_test_sessions AS target
USING (VALUES
    (
        'QCSESS_REF_QK65', 'REQ_REF_QK65_COMPLETED', 'DEV_REF_QC_01',
        'Mechanical', 'Normal', 3, 'Warning',
        CAST('2026-06-06T14:32:00' AS datetime2)
    )
) AS source (
    session_id, request_id, device_id, switch_technology,
    noise_requirement, total_keys, status, completed_at
)
ON target.id = source.session_id
WHEN MATCHED THEN
    UPDATE SET
        request_id = source.request_id,
        device_id = source.device_id,
        switch_technology = source.switch_technology,
        noise_requirement = source.noise_requirement,
        total_keys = source.total_keys,
        status = source.status,
        completed_at = source.completed_at
WHEN NOT MATCHED THEN
    INSERT (
        id, request_id, device_id, switch_technology,
        noise_requirement, total_keys, status, completed_at
    )
    VALUES (
        source.session_id, source.request_id, source.device_id, source.switch_technology,
        source.noise_requirement, source.total_keys, source.status, source.completed_at
    );

MERGE device_key_test_results AS target
USING (VALUES
    ('QCSESS_REF_QK65', 'KeyA', 'A', 1, 2.10, 1, 1, 75, 42.50, 'Pass', CAST('2026-06-06T14:31:00' AS datetime2)),
    ('QCSESS_REF_QK65', 'KeyB', 'B', 1, 12.50, 1, 1, 82, 48.00, 'Warning', CAST('2026-06-06T14:31:30' AS datetime2)),
    ('QCSESS_REF_QK65', 'KeyC', 'C', 1, 2.10, 1, 1, 78, 44.00, 'Pass', CAST('2026-06-06T14:32:00' AS datetime2))
) AS source (
    session_id, key_code, received_key, press_signal_detected, latency,
    press_count, release_signal, hold_duration, noise, result, recorded_at
)
ON target.session_id = source.session_id
AND target.key_code = source.key_code
WHEN MATCHED THEN
    UPDATE SET
        received_key = source.received_key,
        press_signal_detected = source.press_signal_detected,
        latency = source.latency,
        press_count = source.press_count,
        release_signal = source.release_signal,
        hold_duration = source.hold_duration,
        noise = source.noise,
        result = source.result,
        recorded_at = source.recorded_at
WHEN NOT MATCHED THEN
    INSERT (
        session_id, key_code, received_key, press_signal_detected, latency,
        press_count, release_signal, hold_duration, noise, result, recorded_at
    )
    VALUES (
        source.session_id, source.key_code, source.received_key, source.press_signal_detected,
        source.latency, source.press_count, source.release_signal, source.hold_duration,
        source.noise, source.result, source.recorded_at
    );

DELETE existing
FROM audit_log AS existing
INNER JOIN users AS admin_user
    ON admin_user.id = existing.user_id
WHERE admin_user.username = 'admin_refactor'
  AND existing.table_name = 'seller_profiles'
  AND existing.record_id = 'seller_soigear'
  AND existing.action = 'VerifySeller'
  AND existing.changed_at = CAST('2026-06-01T09:00:00' AS datetime2);

INSERT INTO audit_log (user_id, table_name, record_id, action, old_value_json, new_value_json, changed_at)
SELECT admin_user.id, source.table_name, resolved.record_id, source.action, source.old_value_json, source.new_value_json, source.changed_at
FROM (VALUES
    ('seller_profiles', '__SELLER_PROFILE_ID__', 'VerifySeller', NULL, N'{"is_verified":true}', CAST('2026-06-01T09:00:00' AS datetime)),
    ('keyboard_kits', 'KIT_NEO65', 'SeedCatalogItem', NULL, N'{"is_available":true}', CAST('2026-06-03T07:00:00' AS datetime)),
    ('build_requests', 'REQ_REF_QK65_COMPLETED', 'UpdateRequestStatus', N'{"status":"In_progress"}', N'{"status":"Completed"}', CAST('2026-06-06T15:00:00' AS datetime))
) AS source (table_name, record_id, action, old_value_json, new_value_json, changed_at)
CROSS JOIN users AS admin_user
CROSS JOIN (
    SELECT sp.id
    FROM seller_profiles AS sp
    INNER JOIN users AS seller_user ON seller_user.id = sp.user_id
    WHERE seller_user.username = 'seller_soigear'
) AS seller_profile
CROSS APPLY (VALUES (
    CASE
        WHEN source.record_id = '__SELLER_PROFILE_ID__'
            THEN CONVERT(varchar(100), seller_profile.id)
        ELSE source.record_id
    END
)) AS resolved (record_id)
WHERE admin_user.username = 'admin_refactor'
  AND NOT EXISTS (
      SELECT 1
      FROM audit_log AS existing
      WHERE existing.table_name = source.table_name
        AND existing.record_id = resolved.record_id
        AND existing.action = source.action
        AND existing.changed_at = source.changed_at
  );

MERGE chat_conversations AS target
USING (
    SELECT v.conversation_id, seller.id AS seller_user_id, buyer.id AS buyer_id,
           CAST(NULL AS int) AS admin_user_id, v.build_request_id, v.created_at, v.updated_at
    FROM (VALUES
        ('CONV_REF_BOOG75_BUYER_SELLER', 'seller_soigear', 'buyer_refactor', 'REQ_REF_BOOG75_PENDING', CAST('2026-06-04T09:35:00' AS datetime), CAST('2026-06-04T09:38:00' AS datetime))
    ) AS v (conversation_id, seller_username, buyer_username, build_request_id, created_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
    INNER JOIN users AS buyer ON buyer.username = v.buyer_username

    UNION ALL

    SELECT v.conversation_id, seller.id AS seller_user_id, CAST(NULL AS int) AS buyer_id,
           admin_user.id AS admin_user_id, CAST(NULL AS varchar(50)) AS build_request_id, v.created_at, v.updated_at
    FROM (VALUES
        ('CONV_REF_ADMIN_SELLER', 'seller_keyboardlab', 'admin_refactor', CAST('2026-06-02T13:00:00' AS datetime), CAST('2026-06-02T13:05:00' AS datetime))
    ) AS v (conversation_id, seller_username, admin_username, created_at, updated_at)
    INNER JOIN users AS seller ON seller.username = v.seller_username
    INNER JOIN users AS admin_user ON admin_user.username = v.admin_username
) AS source
ON target.id = source.conversation_id
WHEN MATCHED THEN
    UPDATE SET
        seller_user_id = source.seller_user_id,
        buyer_id = source.buyer_id,
        admin_user_id = source.admin_user_id,
        build_request_id = source.build_request_id,
        created_at = source.created_at,
        updated_at = source.updated_at
WHEN NOT MATCHED THEN
    INSERT (id, seller_user_id, buyer_id, admin_user_id, build_request_id, created_at, updated_at)
    VALUES (source.conversation_id, source.seller_user_id, source.buyer_id, source.admin_user_id, source.build_request_id, source.created_at, source.updated_at);

MERGE chat_messages AS target
USING (
    SELECT v.message_id, v.conversation_id, sender.id AS sender_user_id, v.message_text, v.sent_at
    FROM (VALUES
        ('MSG_REF_BOOG75_001', 'CONV_REF_BOOG75_BUYER_SELLER', 'buyer_refactor', 'Please calibrate the HE switches after assembly.', CAST('2026-06-04T09:36:00' AS datetime)),
        ('MSG_REF_BOOG75_002', 'CONV_REF_BOOG75_BUYER_SELLER', 'seller_soigear', 'Confirmed. I will run calibration before marking it completed.', CAST('2026-06-04T09:38:00' AS datetime)),
        ('MSG_REF_ADMIN_SELLER_001', 'CONV_REF_ADMIN_SELLER', 'seller_keyboardlab', 'Can you confirm my seller profile is visible to buyers?', CAST('2026-06-02T13:02:00' AS datetime)),
        ('MSG_REF_ADMIN_SELLER_002', 'CONV_REF_ADMIN_SELLER', 'admin_refactor', 'Yes, your profile is verified and active.', CAST('2026-06-02T13:05:00' AS datetime))
    ) AS v (message_id, conversation_id, sender_username, message_text, sent_at)
    INNER JOIN users AS sender ON sender.username = v.sender_username
) AS source
ON target.id = source.message_id
WHEN MATCHED THEN
    UPDATE SET
        conversation_id = source.conversation_id,
        sender_user_id = source.sender_user_id,
        message_text = source.message_text,
        sent_at = source.sent_at
WHEN NOT MATCHED THEN
    INSERT (id, conversation_id, sender_user_id, message_text, sent_at)
    VALUES (source.message_id, source.conversation_id, source.sender_user_id, source.message_text, source.sent_at);

-- ---------------------------------------------------------------------------
-- Seller applications (buyer -> seller upgrade): one Pending demo for the admin queue.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM seller_applications sa
    INNER JOIN users u ON u.id = sa.buyer_user_id
    WHERE u.username = 'buyer_second'
)
INSERT INTO seller_applications (buyer_user_id, shop_name, phone, address, note, status, created_at)
SELECT u.id, 'My Custom Keeb Shop', '0900000103', '123 Demo Street, HCMC', 'Xin duoc tro thanh seller.', 'Pending', SYSUTCDATETIME()
FROM users AS u WHERE u.username = 'buyer_second';

COMMIT TRANSACTION;
