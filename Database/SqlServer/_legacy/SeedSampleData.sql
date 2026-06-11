USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Sample data generated from Tong_hop_Kit_va_Switch_Soigear_v2.xlsx.
-- Source prices are VND. This seed uses a fixed test conversion rate:
-- 25,000 VND = 1 USD.
-- Kit prices are split for testing as Case 60%, PCB 25%, Plate 15%.
-- This file follows the existing ERD/schema and does not create new tables or columns.

MERGE brands AS target
USING (VALUES
    ('AKKO', 'China'),
    ('AULA', 'China'),
    ('BSUN', 'China'),
    ('Chilkey', 'China'),
    ('Durock', 'China'),
    ('Everglide', 'China'),
    ('Gateron', 'China'),
    ('Generic', 'Unknown'),
    ('KBDfans', 'China'),
    ('Meletrix', 'China'),
    ('MonsGeek', 'China'),
    ('NeoStudio', 'China'),
    ('Owlab', 'China'),
    ('Qwertykeys', 'China'),
    ('SPStar', 'China'),
    ('Sillyworks', 'China'),
    ('TickType', 'China'),
    ('Vertex', 'China'),
    ('Weikav', 'China'),
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
    ('LAYOUT_80', 'TKL / 80%', '80%', 87),
    ('LAYOUT_100', 'Full-size / 100%', '100%', 104)
) AS source (layout_id, layout_name, form_factor, standard_key_count)
ON target.layout_id = source.layout_id
WHEN MATCHED THEN
    UPDATE SET
        layout_name = source.layout_name,
        form_factor = source.form_factor,
        standard_key_count = source.standard_key_count
WHEN NOT MATCHED THEN
    INSERT (layout_id, layout_name, form_factor, standard_key_count)
    VALUES (source.layout_id, source.layout_name, source.form_factor, source.standard_key_count);

MERGE cases AS target
USING (
    SELECT v.case_id, b.brand_id, v.material, v.mount_type, v.color, v.weight_g, v.price_usd, v.is_available
    FROM (VALUES
        ('AE65ProCase', 'Everglide', 'Alu', 'Gasket Mount', 'Silver', 1200, 60.00, 1),
        ('Neo65Case', 'NeoStudio', 'Alu', 'Gasket Mount', 'E-White', 1500, 84.00, 1),
        ('QK65Case', 'Qwertykeys', 'Alu', 'Top Mount / Gasket Mount', 'Navy', 1800, 164.64, 1),
        ('Zoom65Case', 'Meletrix', 'Alu', 'Gasket Mount', 'Burgundy', 1500, 108.00, 1),
        ('Tofu65Case', 'KBDfans', 'Alu', 'Bowl Gasket / Top Mount', 'Cream', 1200, 108.00, 1),
        ('Neo75Case', 'NeoStudio', 'Alu', 'Gasket Mount', 'Graphite', 1800, 136.80, 1),
        ('ND75Case', 'Chilkey', 'Alu', 'Top Mount / Gasket Mount', 'Lilac', 1600, 72.00, 1),
        ('BOOG75Case', 'Meletrix', 'Alu', 'Gasket Mount', 'Milky White', 2000, 132.00, 1),
        ('AULAS75PROCase', 'AULA', 'ABS', 'Gasket Mount', 'Forest Green', 900, 24.00, 1),
        ('Neo80Case', 'NeoStudio', 'Alu', 'Top Mount / Gasket Mount', 'Champagne', 3500, 150.00, 1),
        ('Neo100Case', 'NeoStudio', 'Alu', 'Gasket Mount', 'Pink', 2500, 132.00, 1),
        ('Monsgeek100Case', 'MonsGeek', 'Alu', 'Gasket Mount', 'Blue', 2100, 67.20, 1),
        ('Weikav100Case', 'Weikav', 'Alu', 'Top Mount / Gasket Mount', 'Red', 2000, 48.00, 1),
        ('TICKTYPE100Case', 'TickType', 'Alu', 'Gasket Mount', 'Black', 2500, 126.00, 1)
    ) AS v (case_id, brand_name, material, mount_type, color, weight_g, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.case_id = source.case_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        material = source.material,
        mount_type = source.mount_type,
        color = source.color,
        weight_g = source.weight_g,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (case_id, brand_id, material, mount_type, color, weight_g, price_usd, is_available)
    VALUES (source.case_id, source.brand_id, source.material, source.mount_type, source.color, source.weight_g, source.price_usd, source.is_available);

MERGE pcbs AS target
USING (
    SELECT v.pcb_id, b.brand_id, v.pcb_technology, v.mount_type, v.hotswap, v.wireless, v.rgb, v.switch_mount, v.price_usd, v.is_available
    FROM (VALUES
        ('AE65ProPCB', 'Everglide', 'Magnetic (Hall Effect)', 'Gasket Mount', 1, 0, 0, 'HE Mount', 25.00, 1),
        ('Neo65PCB', 'NeoStudio', 'Mechanical', 'Gasket Mount', 1, 0, 0, '5-pin', 35.00, 1),
        ('QK65PCB', 'Qwertykeys', 'Mechanical', 'Top Mount / Gasket Mount', 1, 0, 1, '3-pin', 68.60, 1),
        ('Zoom65PCB', 'Meletrix', 'Mechanical', 'Gasket Mount', 1, 0, 0, '5-pin', 45.00, 1),
        ('Tofu65PCB', 'KBDfans', 'Mechanical', 'Bowl Gasket / Top Mount', 1, 0, 0, '3-pin', 45.00, 1),
        ('Neo75PCB', 'NeoStudio', 'Mechanical', 'Gasket Mount', 1, 0, 1, '5-pin', 57.00, 1),
        ('ND75PCB', 'Chilkey', 'Mechanical', 'Top Mount / Gasket Mount', 1, 0, 0, '3-pin', 30.00, 1),
        ('BOOG75PCB', 'Meletrix', 'Magnetic (Hall Effect)', 'Gasket Mount', 1, 0, 0, 'HE Mount', 55.00, 1),
        ('AULAS75PROPCB', 'AULA', 'Mechanical', 'Gasket Mount', 1, 0, 1, '3-pin', 10.00, 1),
        ('Neo80PCB', 'NeoStudio', 'Mechanical', 'Top Mount / Gasket Mount', 1, 0, 0, '5-pin', 62.50, 1),
        ('Neo100PCB', 'NeoStudio', 'Mechanical', 'Gasket Mount', 1, 0, 0, '3-pin', 55.00, 1),
        ('Monsgeek100PCB', 'MonsGeek', 'Mechanical', 'Gasket Mount', 1, 0, 1, '5-pin', 28.00, 1),
        ('Weikav100PCB', 'Weikav', 'Mechanical', 'Top Mount / Gasket Mount', 1, 0, 0, '3-pin', 20.00, 1),
        ('TICKTYPE100PCB', 'TickType', 'Mechanical', 'Gasket Mount', 1, 0, 0, '5-pin', 52.50, 1)
    ) AS v (pcb_id, brand_name, pcb_technology, mount_type, hotswap, wireless, rgb, switch_mount, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.pcb_id = source.pcb_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        pcb_technology = source.pcb_technology,
        mount_type = source.mount_type,
        hotswap = source.hotswap,
        wireless = source.wireless,
        rgb = source.rgb,
        switch_mount = source.switch_mount,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (pcb_id, brand_id, pcb_technology, mount_type, hotswap, wireless, rgb, switch_mount, price_usd, is_available)
    VALUES (source.pcb_id, source.brand_id, source.pcb_technology, source.mount_type, source.hotswap, source.wireless, source.rgb, source.switch_mount, source.price_usd, source.is_available);

MERGE plates AS target
USING (
    SELECT v.plate_id, b.brand_id, v.material, v.mount_type, v.flex_cut, v.price_usd, v.is_available
    FROM (VALUES
        ('AE65ProPlate', 'Everglide', 'FR4', 'Gasket Mount', 'No Flex Cut', 15.00, 1),
        ('Neo65Plate', 'NeoStudio', 'Polycarbonate', 'Gasket Mount', 'Flex Cut', 21.00, 1),
        ('QK65Plate', 'Qwertykeys', 'POM', 'Top Mount / Gasket Mount', 'No Flex Cut', 41.16, 1),
        ('Zoom65Plate', 'Meletrix', 'Carbon Fiber', 'Gasket Mount', 'Flex Cut', 27.00, 1),
        ('Tofu65Plate', 'KBDfans', 'Brass', 'Bowl Gasket / Top Mount', 'No Flex Cut', 27.00, 1),
        ('Neo75Plate', 'NeoStudio', 'Aluminum', 'Gasket Mount', 'Flex Cut', 34.20, 1),
        ('ND75Plate', 'Chilkey', 'FR4', 'Top Mount / Gasket Mount', 'No Flex Cut', 18.00, 1),
        ('BOOG75Plate', 'Meletrix', 'Polycarbonate', 'Gasket Mount', 'Flex Cut', 33.00, 1),
        ('AULAS75PROPlate', 'AULA', 'POM', 'Gasket Mount', 'No Flex Cut', 6.00, 1),
        ('Neo80Plate', 'NeoStudio', 'Carbon Fiber', 'Top Mount / Gasket Mount', 'Flex Cut', 37.50, 1),
        ('Neo100Plate', 'NeoStudio', 'Brass', 'Gasket Mount', 'No Flex Cut', 33.00, 1),
        ('Monsgeek100Plate', 'MonsGeek', 'Aluminum', 'Gasket Mount', 'Flex Cut', 16.80, 1),
        ('Weikav100Plate', 'Weikav', 'FR4', 'Top Mount / Gasket Mount', 'No Flex Cut', 12.00, 1),
        ('TICKTYPE100Plate', 'TickType', 'Polycarbonate', 'Gasket Mount', 'Flex Cut', 31.50, 1)
    ) AS v (plate_id, brand_name, material, mount_type, flex_cut, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.plate_id = source.plate_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        material = source.material,
        mount_type = source.mount_type,
        flex_cut = source.flex_cut,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (plate_id, brand_id, material, mount_type, flex_cut, price_usd, is_available)
    VALUES (source.plate_id, source.brand_id, source.material, source.mount_type, source.flex_cut, source.price_usd, source.is_available);

MERGE switches AS target
USING (
    SELECT v.switch_id, b.brand_id, v.switch_technology, v.switch_type, v.actuation_force_g, v.mount_type, v.sound_profile, v.price_usd, v.is_available
    FROM (VALUES
        ('GateronOilKingSwitch', 'Gateron', 'Mechanical', 'Linear', 55, '3-pin', 'Thocky', 0.66, 1),
        ('GateronBoxInkInkBlackV2Switch', 'Gateron', 'Mechanical', 'Linear', 60, '5-pin', 'Thocky', 0.72, 1),
        ('OwlabLondonFogV2Switch', 'Owlab', 'Mechanical', 'Linear', 55, '5-pin', 'Thocky', 0.74, 1),
        ('WSMorandiSwitch', 'WuqueStudio', 'Mechanical', 'Linear', 50, '5-pin', 'Thocky', 0.46, 1),
        ('SillyworksHyacinthHMXV2Switch', 'Sillyworks', 'Mechanical', 'Linear', 45, '3-pin', 'Clacky', 0.38, 1),
        ('VertexV1Switch', 'Vertex', 'Mechanical', 'Linear', 50, '5-pin', 'Balanced', 0.40, 1),
        ('SPStarMeteorWhiteSwitch', 'SPStar', 'Mechanical', 'Linear', 43, '5-pin', 'Clacky', 0.48, 1),
        ('GateronStrawberrySmoothieSwitch', 'Gateron', 'Mechanical', 'Linear', 40, '3-pin', 'Clacky', 0.58, 1),
        ('WSJadeGreenSwitch', 'WuqueStudio', 'Mechanical', 'Linear', 50, '5-pin', 'Thocky', 0.56, 1),
        ('NeoAzureOatAmberRyeSwitch', 'NeoStudio', 'Mechanical', 'Linear', 45, '5-pin', 'Balanced', 0.34, 1),
        ('GateronBananaSmoothieSwitch', 'Gateron', 'Mechanical', 'Tactile', 50, '5-pin', 'Clacky', 0.58, 1),
        ('GateronQuinnSwitch', 'Gateron', 'Mechanical', 'Tactile', 59, '3-pin', 'Clacky', 0.58, 1),
        ('WSHeavyTactileSwitch', 'WuqueStudio', 'Mechanical', 'Tactile', 45, '5-pin', 'Clacky', 0.48, 1),
        ('HuttSwitch', 'BSUN', 'Mechanical', 'Tactile', 40, '5-pin', 'Clacky', 0.60, 1),
        ('WSLightTactileSwitch', 'WuqueStudio', 'Mechanical', 'Tactile', 45, '5-pin', 'Thocky', 0.48, 1),
        ('HMXCilantroSwitch', 'AKKO', 'Mechanical', 'Tactile', 45, '3-pin', 'Clacky', 0.36, 1),
        ('GateronMagneticJadeProHESwitch', 'Gateron', 'Magnetic (Hall Effect)', 'Linear', 38, 'HE Mount', 'Thocky', 1.00, 1),
        ('WSFluxMagneticHESwitch', 'WuqueStudio', 'Magnetic (Hall Effect)', 'Linear', 42, 'HE Mount', 'Balanced', 0.74, 1),
        ('GateronMelodicSwitch', 'Gateron', 'Mechanical', 'Clicky', 50, '5-pin', 'Clicky', 0.60, 1)
    ) AS v (switch_id, brand_name, switch_technology, switch_type, actuation_force_g, mount_type, sound_profile, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.switch_id = source.switch_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        switch_technology = source.switch_technology,
        switch_type = source.switch_type,
        actuation_force_g = source.actuation_force_g,
        mount_type = source.mount_type,
        sound_profile = source.sound_profile,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (switch_id, brand_id, switch_technology, switch_type, actuation_force_g, mount_type, sound_profile, price_usd, is_available)
    VALUES (source.switch_id, source.brand_id, source.switch_technology, source.switch_type, source.actuation_force_g, source.mount_type, source.sound_profile, source.price_usd, source.is_available);

MERGE keycap_sets AS target
USING (
    SELECT v.keycap_id, b.brand_id, v.profile, v.material, v.color_primary, v.legend_type, v.price_usd, v.is_available
    FROM (VALUES
        ('GenericPBTCherryKeycapSet', 'Generic', 'Cherry', 'PBT', 'White / Blue', 'Dye-sub', 35.00, 1),
        ('GenericABSOEMKeycapSet', 'Generic', 'OEM', 'ABS', 'Black / White', 'Double-shot', 25.00, 1)
    ) AS v (keycap_id, brand_name, profile, material, color_primary, legend_type, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.keycap_id = source.keycap_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        profile = source.profile,
        material = source.material,
        color_primary = source.color_primary,
        legend_type = source.legend_type,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (keycap_id, brand_id, profile, material, color_primary, legend_type, price_usd, is_available)
    VALUES (source.keycap_id, source.brand_id, source.profile, source.material, source.color_primary, source.legend_type, source.price_usd, source.is_available);

MERGE stabilizers AS target
USING (
    SELECT v.stab_id, b.brand_id, v.stab_type, v.sizes_included, v.price_usd, v.is_available
    FROM (VALUES
        ('DurockV2StabilizerSet', 'Durock', 'Screw-in', '1x 6.25u, 4x 2u', 18.00, 1)
    ) AS v (stab_id, brand_name, stab_type, sizes_included, price_usd, is_available)
    INNER JOIN brands AS b ON b.brand_name = v.brand_name
) AS source
ON target.stab_id = source.stab_id
WHEN MATCHED THEN
    UPDATE SET
        brand_id = source.brand_id,
        stab_type = source.stab_type,
        sizes_included = source.sizes_included,
        price_usd = source.price_usd,
        is_available = source.is_available
WHEN NOT MATCHED THEN
    INSERT (stab_id, brand_id, stab_type, sizes_included, price_usd, is_available)
    VALUES (source.stab_id, source.brand_id, source.stab_type, source.sizes_included, source.price_usd, source.is_available);

MERGE case_layouts AS target
USING (VALUES
    ('AE65ProCase', 'LAYOUT_65', 1),
    ('Neo65Case', 'LAYOUT_65', 1),
    ('QK65Case', 'LAYOUT_65', 1),
    ('Zoom65Case', 'LAYOUT_65', 1),
    ('Tofu65Case', 'LAYOUT_65', 1),
    ('Neo75Case', 'LAYOUT_75', 1),
    ('ND75Case', 'LAYOUT_75', 1),
    ('BOOG75Case', 'LAYOUT_75', 1),
    ('AULAS75PROCase', 'LAYOUT_75', 1),
    ('Neo80Case', 'LAYOUT_80', 1),
    ('Neo100Case', 'LAYOUT_100', 1),
    ('Monsgeek100Case', 'LAYOUT_100', 1),
    ('Weikav100Case', 'LAYOUT_100', 1),
    ('TICKTYPE100Case', 'LAYOUT_100', 1)
) AS source (case_id, layout_id, is_primary)
ON target.case_id = source.case_id AND target.layout_id = source.layout_id
WHEN MATCHED THEN
    UPDATE SET is_primary = source.is_primary
WHEN NOT MATCHED THEN
    INSERT (case_id, layout_id, is_primary)
    VALUES (source.case_id, source.layout_id, source.is_primary);

MERGE pcb_layouts AS target
USING (VALUES
    ('AE65ProPCB', 'LAYOUT_65', 'HE hotswap'),
    ('Neo65PCB', 'LAYOUT_65', '5-pin hotswap'),
    ('QK65PCB', 'LAYOUT_65', '3-pin hotswap RGB'),
    ('Zoom65PCB', 'LAYOUT_65', '5-pin hotswap'),
    ('Tofu65PCB', 'LAYOUT_65', '3-pin hotswap'),
    ('Neo75PCB', 'LAYOUT_75', '5-pin hotswap RGB'),
    ('ND75PCB', 'LAYOUT_75', '3-pin hotswap'),
    ('BOOG75PCB', 'LAYOUT_75', 'HE hotswap'),
    ('AULAS75PROPCB', 'LAYOUT_75', '3-pin hotswap RGB'),
    ('Neo80PCB', 'LAYOUT_80', '5-pin hotswap'),
    ('Neo100PCB', 'LAYOUT_100', '3-pin hotswap'),
    ('Monsgeek100PCB', 'LAYOUT_100', '5-pin hotswap RGB'),
    ('Weikav100PCB', 'LAYOUT_100', '3-pin hotswap'),
    ('TICKTYPE100PCB', 'LAYOUT_100', '5-pin hotswap')
) AS source (pcb_id, layout_id, variant_name)
ON target.pcb_id = source.pcb_id AND target.layout_id = source.layout_id
WHEN MATCHED THEN
    UPDATE SET variant_name = source.variant_name
WHEN NOT MATCHED THEN
    INSERT (pcb_id, layout_id, variant_name)
    VALUES (source.pcb_id, source.layout_id, source.variant_name);

MERGE plate_layouts AS target
USING (VALUES
    ('AE65ProPlate', 'LAYOUT_65'),
    ('Neo65Plate', 'LAYOUT_65'),
    ('QK65Plate', 'LAYOUT_65'),
    ('Zoom65Plate', 'LAYOUT_65'),
    ('Tofu65Plate', 'LAYOUT_65'),
    ('Neo75Plate', 'LAYOUT_75'),
    ('ND75Plate', 'LAYOUT_75'),
    ('BOOG75Plate', 'LAYOUT_75'),
    ('AULAS75PROPlate', 'LAYOUT_75'),
    ('Neo80Plate', 'LAYOUT_80'),
    ('Neo100Plate', 'LAYOUT_100'),
    ('Monsgeek100Plate', 'LAYOUT_100'),
    ('Weikav100Plate', 'LAYOUT_100'),
    ('TICKTYPE100Plate', 'LAYOUT_100')
) AS source (plate_id, layout_id)
ON target.plate_id = source.plate_id AND target.layout_id = source.layout_id
WHEN NOT MATCHED THEN
    INSERT (plate_id, layout_id)
    VALUES (source.plate_id, source.layout_id);

MERGE compatibility_rules AS target
USING (VALUES
    ('AE65ProCase', 'AE65ProPCB', 'AE65ProPlate', 1, 'HE kit-split compatible set from AE65Pro.'),
    ('Neo65Case', 'Neo65PCB', 'Neo65Plate', 1, 'Mechanical kit-split compatible set from Neo65.'),
    ('QK65Case', 'QK65PCB', 'QK65Plate', 1, 'Mechanical kit-split compatible set from QK65.'),
    ('Zoom65Case', 'Zoom65PCB', 'Zoom65Plate', 1, 'Mechanical kit-split compatible set from Zoom65.'),
    ('Tofu65Case', 'Tofu65PCB', 'Tofu65Plate', 1, 'Mechanical kit-split compatible set from Tofu65.'),
    ('Neo75Case', 'Neo75PCB', 'Neo75Plate', 1, 'Mechanical kit-split compatible set from Neo75.'),
    ('ND75Case', 'ND75PCB', 'ND75Plate', 1, 'Mechanical kit-split compatible set from ND75.'),
    ('BOOG75Case', 'BOOG75PCB', 'BOOG75Plate', 1, 'HE kit-split compatible set from BOOG75.'),
    ('AULAS75PROCase', 'AULAS75PROPCB', 'AULAS75PROPlate', 1, 'Mechanical kit-split compatible set from AULAS75PRO.'),
    ('Neo80Case', 'Neo80PCB', 'Neo80Plate', 1, 'Mechanical kit-split compatible set from Neo80.'),
    ('Neo100Case', 'Neo100PCB', 'Neo100Plate', 1, 'Mechanical kit-split compatible set from Neo100.'),
    ('Monsgeek100Case', 'Monsgeek100PCB', 'Monsgeek100Plate', 1, 'Mechanical kit-split compatible set from Monsgeek100.'),
    ('Weikav100Case', 'Weikav100PCB', 'Weikav100Plate', 1, 'Mechanical kit-split compatible set from Weikav100.'),
    ('TICKTYPE100Case', 'TICKTYPE100PCB', 'TICKTYPE100Plate', 1, 'Mechanical kit-split compatible set from TICKTYPE100.')
) AS source (case_id, pcb_id, plate_id, is_compatible, notes)
ON target.case_id = source.case_id
   AND target.pcb_id = source.pcb_id
   AND target.plate_id = source.plate_id
WHEN MATCHED THEN
    UPDATE SET
        is_compatible = source.is_compatible,
        notes = source.notes
WHEN NOT MATCHED THEN
    INSERT (case_id, pcb_id, plate_id, is_compatible, notes)
    VALUES (source.case_id, source.pcb_id, source.plate_id, source.is_compatible, source.notes);

MERGE users AS target
USING (
    SELECT r.role_id, v.username, v.email, v.phone, v.password_hash, v.is_active
    FROM (VALUES
        ('admin_seed', 'admin.seed@example.com', '0900000001', 'PBKDF2-SHA256$100000$Q3VzdG9tS2V5Ym9hcmQ6QWRtaW5TZWVkOnYx$Wny5LY1TW15VxZeK7Bw019m+F9JJLQJSRsvdnPydXbs=', 1, 'Admin'),
        ('buyer_seed', 'buyer.seed@example.com', '0900000002', 'PBKDF2-SHA256$100000$Q3VzdG9tS2V5Ym9hcmQ6QnV5ZXJTZWVkOnYx$ZmoKZQMNI6TxlQ1IyyIQaVPomlHRc0iSfskWXqAhOa0=', 1, 'Buyer'),
        ('seller_seed', 'seller.seed@example.com', '0900000003', 'PBKDF2-SHA256$100000$Q3VzdG9tS2V5Ym9hcmQ6U2VsbGVyU2VlZDp2MQ==$NdttyizOyl4eRf9/gUs2xPjlSeg0ost8MGHqd6KNNt0=', 1, 'Seller')
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
    SELECT
        seller.user_id,
        admin_user.user_id AS admin_id,
        'Soigear Seed Shop' AS shop_name,
        '0900000003' AS phone,
        'Seed address for local testing' AS address,
        CAST(1 AS BIT) AS is_verified
    FROM users AS seller
    CROSS JOIN users AS admin_user
    WHERE seller.username = 'seller_seed'
      AND admin_user.username = 'admin_seed'
) AS source
ON target.user_id = source.user_id
WHEN MATCHED THEN
    UPDATE SET
        assigned_by_admin_id = source.admin_id,
        shop_name = source.shop_name,
        phone = source.phone,
        address = source.address,
        is_verified = source.is_verified,
        verified_by_admin_id = source.admin_id,
        assigned_at = COALESCE(target.assigned_at, SYSUTCDATETIME()),
        verified_at = COALESCE(target.verified_at, SYSUTCDATETIME())
WHEN NOT MATCHED THEN
    INSERT (user_id, assigned_by_admin_id, shop_name, phone, address, is_verified, verified_by_admin_id, assigned_at, verified_at)
    VALUES (source.user_id, source.admin_id, source.shop_name, source.phone, source.address, source.is_verified, source.admin_id, SYSUTCDATETIME(), SYSUTCDATETIME());

MERGE builds AS target
USING (
    SELECT buyer.user_id, v.build_id, v.layout_id, v.case_id, v.pcb_id, v.plate_id, v.switch_id, v.keycap_id, v.stab_id, v.name, v.status, v.total_cost_snapshot
    FROM (VALUES
        ('BUILD_SAMPLE_NEO65_CORK', 'LAYOUT_65', 'Neo65Case', 'Neo65PCB', 'Neo65Plate', 'NeoAzureOatAmberRyeSwitch', 'GenericPBTCherryKeycapSet', 'DurockV2StabilizerSet', 'Neo65 test build - Cork foam', 'Saved', 195.10),
        ('BUILD_SAMPLE_QK65_PORON', 'LAYOUT_65', 'QK65Case', 'QK65PCB', 'QK65Plate', 'GateronOilKingSwitch', 'GenericABSOEMKeycapSet', 'DurockV2StabilizerSet', 'QK65 test build - Poron foam', 'Saved', 351.50),
        ('BUILD_SAMPLE_BOOG75_EVA', 'LAYOUT_75', 'BOOG75Case', 'BOOG75PCB', 'BOOG75Plate', 'GateronMagneticJadeProHESwitch', 'GenericPBTCherryKeycapSet', 'DurockV2StabilizerSet', 'BOOG75 HE test build - EVA foam', 'Saved', 354.00)
    ) AS v (build_id, layout_id, case_id, pcb_id, plate_id, switch_id, keycap_id, stab_id, name, status, total_cost_snapshot)
    CROSS JOIN users AS buyer
    WHERE buyer.username = 'buyer_seed'
) AS source
ON target.build_id = source.build_id
WHEN MATCHED THEN
    UPDATE SET
        user_id = source.user_id,
        layout_id = source.layout_id,
        case_id = source.case_id,
        pcb_id = source.pcb_id,
        plate_id = source.plate_id,
        switch_id = source.switch_id,
        keycap_id = source.keycap_id,
        stab_id = source.stab_id,
        name = source.name,
        status = source.status,
        total_cost_snapshot = source.total_cost_snapshot,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (build_id, user_id, layout_id, case_id, pcb_id, plate_id, switch_id, keycap_id, stab_id, name, status, total_cost_snapshot)
    VALUES (source.build_id, source.user_id, source.layout_id, source.case_id, source.pcb_id, source.plate_id, source.switch_id, source.keycap_id, source.stab_id, source.name, source.status, source.total_cost_snapshot);

MERGE build_requests AS target
USING (
    SELECT
        v.request_id,
        v.build_id,
        buyer.user_id AS buyer_id,
        seller.user_id AS seller_user_id,
        v.request_payload_json,
        v.status,
        v.note
    FROM (VALUES
        (
            'REQ_SAMPLE_NEO65_CORK',
            'BUILD_SAMPLE_NEO65_CORK',
            N'{"build_id":"BUILD_SAMPLE_NEO65_CORK","layout_id":"LAYOUT_65","case_id":"Neo65Case","pcb_id":"Neo65PCB","plate_id":"Neo65Plate","switch_id":"NeoAzureOatAmberRyeSwitch","keycap_id":"GenericPBTCherryKeycapSet","stab_id":"DurockV2StabilizerSet","total_cost_snapshot":195.10}',
            'Pending',
            'Seed request for seller dashboard testing'
        )
    ) AS v (request_id, build_id, request_payload_json, status, note)
    CROSS JOIN users AS buyer
    CROSS JOIN users AS seller
    WHERE buyer.username = 'buyer_seed'
      AND seller.username = 'seller_seed'
) AS source
ON target.request_id = source.request_id
WHEN MATCHED THEN
    UPDATE SET
        build_id = source.build_id,
        buyer_id = source.buyer_id,
        seller_user_id = source.seller_user_id,
        request_payload_json = source.request_payload_json,
        status = source.status,
        note = source.note,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (request_id, build_id, buyer_id, seller_user_id, request_payload_json, status, note)
    VALUES (source.request_id, source.build_id, source.buyer_id, source.seller_user_id, source.request_payload_json, source.status, source.note);

INSERT INTO build_mods (build_id, mod_type, target_component, lube_type, is_filmed, spring_weight_g, notes)
SELECT source.build_id, source.mod_type, source.target_component, source.lube_type, source.is_filmed, source.spring_weight_g, source.notes
FROM (VALUES
    ('BUILD_SAMPLE_NEO65_CORK', 'Foam', 'Case', NULL, 0, NULL, 'Foam type: Cork'),
    ('BUILD_SAMPLE_QK65_PORON', 'Foam', 'Case', NULL, 0, NULL, 'Foam type: Poron'),
    ('BUILD_SAMPLE_BOOG75_EVA', 'Foam', 'Case', NULL, 0, NULL, 'Foam type: EVA'),
    ('BUILD_SAMPLE_NEO65_CORK', 'Lube', 'Switch', 'Krytox 205g0', 0, NULL, 'Switch lube sample'),
    ('BUILD_SAMPLE_QK65_PORON', 'Lube', 'Stabilizer', 'Tribosys 3204', 0, NULL, 'Stabilizer lube sample')
) AS source (build_id, mod_type, target_component, lube_type, is_filmed, spring_weight_g, notes)
WHERE NOT EXISTS (
    SELECT 1
    FROM build_mods AS existing
    WHERE existing.build_id = source.build_id
      AND existing.mod_type = source.mod_type
      AND existing.target_component = source.target_component
      AND ISNULL(existing.lube_type, '') = ISNULL(source.lube_type, '')
      AND existing.notes = source.notes
);

COMMIT TRANSACTION;
GO
