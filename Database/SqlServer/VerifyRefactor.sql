-- Custom Keyboard Builder - Refactor verification queries
-- Source: Custom_Keyboard_Project_Refactor_Summary.md section 14.
-- Run AFTER CreateSchema_Refactor.sql + SeedData_Refactor.sql on the test DB.
-- Every CHECK query below must return 0 error rows. Row counts must match Summary section 12.1.

USE CustomKeyboard_Refactor;
GO

SET NOCOUNT ON;
GO

PRINT '================================================================';
PRINT ' 14.1  Row counts per table (compare to Summary 12.1)';
PRINT '       Expected: roles 3, users 7, seller_profiles 3, brands 13,';
PRINT '       layouts 4, keyboard_kits 9, switches 10, keycap_sets 5,';
PRINT '       stabilizers 4, accessories 7, builds 5, build_items 17,';
PRINT '       build_mods 5, build_requests 2, audit_log 3,';
PRINT '       chat_conversations 2, chat_messages 4.';
PRINT '================================================================';
SELECT 'roles' AS table_name, COUNT(*) AS row_count, 3 AS expected_count FROM roles
UNION ALL SELECT 'users', COUNT(*), 7 FROM users
UNION ALL SELECT 'seller_profiles', COUNT(*), 3 FROM seller_profiles
UNION ALL SELECT 'brands', COUNT(*), 13 FROM brands
UNION ALL SELECT 'layouts', COUNT(*), 4 FROM layouts
UNION ALL SELECT 'keyboard_kits', COUNT(*), 9 FROM keyboard_kits
UNION ALL SELECT 'switches', COUNT(*), 10 FROM switches
UNION ALL SELECT 'keycap_sets', COUNT(*), 5 FROM keycap_sets
UNION ALL SELECT 'stabilizers', COUNT(*), 4 FROM stabilizers
UNION ALL SELECT 'accessories', COUNT(*), 7 FROM accessories
UNION ALL SELECT 'builds', COUNT(*), 5 FROM builds
UNION ALL SELECT 'build_items', COUNT(*), 17 FROM build_items
UNION ALL SELECT 'build_mods', COUNT(*), 5 FROM build_mods
UNION ALL SELECT 'build_requests', COUNT(*), 2 FROM build_requests
UNION ALL SELECT 'audit_log', COUNT(*), 3 FROM audit_log
UNION ALL SELECT 'chat_conversations', COUNT(*), 2 FROM chat_conversations
UNION ALL SELECT 'chat_messages', COUNT(*), 4 FROM chat_messages;
GO

PRINT '================================================================';
PRINT ' 14.2  Build total snapshot = kit price + SUM(item qty * unit price)';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT
    b.build_id,
    b.total_cost_snapshot,
    CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2)) AS calculated_total
FROM builds b
INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.build_id
GROUP BY b.build_id, b.total_cost_snapshot, k.price_usd
HAVING b.total_cost_snapshot <> CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2));
GO

PRINT '================================================================';
PRINT ' 14.3  Switch quantity for Saved/Requested builds >= required_switch_quantity';
PRINT '       Rule is at-least (BuildService allows buying spare switches). Expected: 0 rows.';
PRINT '================================================================';
SELECT
    b.build_id,
    k.required_switch_quantity,
    SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) AS selected_switch_quantity
FROM builds b
INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.build_id
WHERE b.status IN ('Saved', 'Requested')
GROUP BY b.build_id, k.required_switch_quantity
HAVING SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) < k.required_switch_quantity;
GO

PRINT '================================================================';
PRINT ' 14.4  Exactly one product FK per build_items row';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT *
FROM build_items
WHERE
    (CASE WHEN switch_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN keycap_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN stab_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) <> 1;
GO

PRINT '================================================================';
PRINT ' 14.5  Seller requests target active + verified sellers only';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT br.request_id, u.username, sp.is_verified, u.is_active
FROM build_requests br
INNER JOIN users u ON u.user_id = br.seller_user_id
LEFT JOIN seller_profiles sp ON sp.user_id = u.user_id
WHERE u.is_active = 0 OR sp.is_verified = 0 OR sp.seller_profile_id IS NULL;
GO

PRINT '================================================================';
PRINT ' 14.6  Conversation has exactly one of buyer_id / admin_user_id';
PRINT '       Expected: 0 rows.';
PRINT '================================================================';
SELECT *
FROM chat_conversations
WHERE
    (CASE WHEN buyer_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) <> 1;
GO

PRINT '================================================================';
PRINT ' Extra A  FK orphan checks across all relationships';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT 'users.role_id' AS fk, COUNT(*) AS orphan_count
    FROM users u LEFT JOIN roles r ON r.role_id = u.role_id WHERE r.role_id IS NULL
UNION ALL SELECT 'seller_profiles.user_id', COUNT(*)
    FROM seller_profiles sp LEFT JOIN users u ON u.user_id = sp.user_id WHERE u.user_id IS NULL
UNION ALL SELECT 'keyboard_kits.brand_id', COUNT(*)
    FROM keyboard_kits k LEFT JOIN brands b ON b.brand_id = k.brand_id WHERE b.brand_id IS NULL
UNION ALL SELECT 'keyboard_kits.layout_id', COUNT(*)
    FROM keyboard_kits k LEFT JOIN layouts l ON l.layout_id = k.layout_id WHERE l.layout_id IS NULL
UNION ALL SELECT 'switches.brand_id', COUNT(*)
    FROM switches s LEFT JOIN brands b ON b.brand_id = s.brand_id WHERE b.brand_id IS NULL
UNION ALL SELECT 'keycap_sets.brand_id', COUNT(*)
    FROM keycap_sets kc LEFT JOIN brands b ON b.brand_id = kc.brand_id WHERE b.brand_id IS NULL
UNION ALL SELECT 'stabilizers.brand_id', COUNT(*)
    FROM stabilizers st LEFT JOIN brands b ON b.brand_id = st.brand_id WHERE b.brand_id IS NULL
UNION ALL SELECT 'builds.buyer_id', COUNT(*)
    FROM builds bd LEFT JOIN users u ON u.user_id = bd.buyer_id WHERE u.user_id IS NULL
UNION ALL SELECT 'builds.kit_id', COUNT(*)
    FROM builds bd LEFT JOIN keyboard_kits k ON k.kit_id = bd.kit_id WHERE k.kit_id IS NULL
UNION ALL SELECT 'build_items.build_id', COUNT(*)
    FROM build_items bi LEFT JOIN builds bd ON bd.build_id = bi.build_id WHERE bd.build_id IS NULL
UNION ALL SELECT 'build_items.switch_id', COUNT(*)
    FROM build_items bi LEFT JOIN switches s ON s.switch_id = bi.switch_id WHERE bi.switch_id IS NOT NULL AND s.switch_id IS NULL
UNION ALL SELECT 'build_items.keycap_id', COUNT(*)
    FROM build_items bi LEFT JOIN keycap_sets kc ON kc.keycap_id = bi.keycap_id WHERE bi.keycap_id IS NOT NULL AND kc.keycap_id IS NULL
UNION ALL SELECT 'build_items.stab_id', COUNT(*)
    FROM build_items bi LEFT JOIN stabilizers st ON st.stab_id = bi.stab_id WHERE bi.stab_id IS NOT NULL AND st.stab_id IS NULL
UNION ALL SELECT 'build_items.accessory_id', COUNT(*)
    FROM build_items bi LEFT JOIN accessories a ON a.accessory_id = bi.accessory_id WHERE bi.accessory_id IS NOT NULL AND a.accessory_id IS NULL
UNION ALL SELECT 'build_mods.build_id', COUNT(*)
    FROM build_mods bm LEFT JOIN builds bd ON bd.build_id = bm.build_id WHERE bd.build_id IS NULL
UNION ALL SELECT 'build_requests.build_id', COUNT(*)
    FROM build_requests br LEFT JOIN builds bd ON bd.build_id = br.build_id WHERE bd.build_id IS NULL
UNION ALL SELECT 'build_requests.seller_user_id', COUNT(*)
    FROM build_requests br LEFT JOIN users u ON u.user_id = br.seller_user_id WHERE u.user_id IS NULL
UNION ALL SELECT 'audit_log.user_id', COUNT(*)
    FROM audit_log al LEFT JOIN users u ON u.user_id = al.user_id WHERE u.user_id IS NULL
UNION ALL SELECT 'chat_conversations.seller_user_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN users u ON u.user_id = c.seller_user_id WHERE u.user_id IS NULL
UNION ALL SELECT 'chat_conversations.build_request_id', COUNT(*)
    FROM chat_conversations c LEFT JOIN build_requests br ON br.request_id = c.build_request_id WHERE c.build_request_id IS NOT NULL AND br.request_id IS NULL
UNION ALL SELECT 'chat_messages.conversation_id', COUNT(*)
    FROM chat_messages m LEFT JOIN chat_conversations c ON c.conversation_id = m.conversation_id WHERE c.conversation_id IS NULL
UNION ALL SELECT 'chat_messages.sender_user_id', COUNT(*)
    FROM chat_messages m LEFT JOIN users u ON u.user_id = m.sender_user_id WHERE u.user_id IS NULL;
GO

PRINT '================================================================';
PRINT ' Extra B  Requested builds have a matching build_request';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT b.build_id, b.status
FROM builds b
WHERE b.status = 'Requested'
  AND NOT EXISTS (SELECT 1 FROM build_requests br WHERE br.build_id = b.build_id);
GO

PRINT '================================================================';
PRINT ' Extra B2 Builds with an active request are Requested';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT b.build_id, b.status
FROM builds b
WHERE b.status NOT IN ('Requested', 'Archived')
  AND EXISTS (
      SELECT 1 FROM build_requests br
      WHERE br.build_id = b.build_id
        AND br.status IN ('Pending', 'Accepted', 'In_progress'));
GO

PRINT '================================================================';
PRINT ' Extra C  Chat message sender belongs to its conversation';
PRINT '          Expected: 0 rows.';
PRINT '================================================================';
SELECT m.message_id, m.conversation_id, m.sender_user_id
FROM chat_messages m
INNER JOIN chat_conversations c ON c.conversation_id = m.conversation_id
WHERE m.sender_user_id NOT IN (
    c.seller_user_id,
    ISNULL(c.buyer_id, -1),
    ISNULL(c.admin_user_id, -1)
);
GO

PRINT 'Verification queries complete.';
GO
