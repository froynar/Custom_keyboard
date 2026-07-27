-- Internal SQLCMD include. Run SeedDemoAnalytics_Refactor.sql, not this body directly.
BEGIN TRY
BEGIN TRANSACTION;

PRINT 'Seeding demo analytics data (DEMO_ rows)...';

-- ---- Idempotent cleanup of any previous demo rows ([_] escapes the LIKE wildcard) ----
DELETE FROM build_requests WHERE id LIKE 'DEMO[_]REQ[_]%';
DELETE FROM build_items    WHERE build_id   LIKE 'DEMO[_]BUILD[_]%';
DELETE FROM build_mods     WHERE build_id   LIKE 'DEMO[_]BUILD[_]%';
DELETE FROM builds         WHERE id         LIKE 'DEMO[_]BUILD[_]%';

-- ---- Declarations (T-SQL variables are batch-scoped: declare once, SET in loop) ----
DECLARE @sellerA int, @sellerB int;
DECLARE @buyerA int, @buyerB int, @buyerC int;
DECLARE @kit1 varchar(50), @sw1 varchar(50), @kitPrice1 decimal(10,2), @swPrice1 decimal(10,2), @req1 int;
DECLARE @kit2 varchar(50), @sw2 varchar(50), @kitPrice2 decimal(10,2), @swPrice2 decimal(10,2), @req2 int;
DECLARE @base date = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1);
DECLARE @i int = 0, @j int = 0, @seq int = 0, @ordersThisMonth int;
DECLARE @monthStart datetime2, @requested datetime2, @accepted datetime2, @completed datetime2;
DECLARE @useCombo1 bit, @kit varchar(50), @sw varchar(50), @kitPrice decimal(10,2), @swPrice decimal(10,2), @req int;
DECLARE @seller int, @buyer int, @status varchar(20), @total decimal(10,2);
DECLARE @buildId varchar(50), @reqId varchar(50);

-- ---- Verified, active sellers ----
SELECT TOP 1 @sellerA = sp.user_id
FROM seller_profiles sp INNER JOIN users u ON u.id = sp.user_id
WHERE sp.is_verified = 1 AND u.is_active = 1
ORDER BY sp.user_id;

SELECT TOP 1 @sellerB = sp.user_id
FROM seller_profiles sp INNER JOIN users u ON u.id = sp.user_id
WHERE sp.is_verified = 1 AND u.is_active = 1 AND sp.user_id <> @sellerA
ORDER BY sp.user_id;

IF @sellerA IS NULL
BEGIN
    RAISERROR('No verified active seller found; cannot seed demo analytics.', 16, 1);
    RETURN;
END
IF @sellerB IS NULL SET @sellerB = @sellerA;

-- ---- Active buyers ----
SELECT @buyerA = MIN(u.id) FROM users u INNER JOIN roles r ON r.id = u.role_id
WHERE r.role_name = 'Buyer' AND u.is_active = 1;
SELECT @buyerB = MIN(u.id) FROM users u INNER JOIN roles r ON r.id = u.role_id
WHERE r.role_name = 'Buyer' AND u.is_active = 1 AND u.id <> @buyerA;
SELECT @buyerC = MIN(u.id) FROM users u INNER JOIN roles r ON r.id = u.role_id
WHERE r.role_name = 'Buyer' AND u.is_active = 1 AND u.id NOT IN (@buyerA, ISNULL(@buyerB, -1));

IF @buyerA IS NULL
BEGIN
    RAISERROR('No active buyer found; cannot seed demo analytics.', 16, 1);
    RETURN;
END
IF @buyerB IS NULL SET @buyerB = @buyerA;
IF @buyerC IS NULL SET @buyerC = @buyerB;

-- ---- Two compatible (kit, switch) combos: one pricier, one cheaper, for revenue variety ----
SELECT TOP 1 @kit1 = k.id, @kitPrice1 = k.price_usd, @req1 = k.required_switch_quantity,
             @sw1 = s.id, @swPrice1 = s.price_usd
FROM keyboard_kits k INNER JOIN switches s
    ON s.switch_technology = k.pcb_technology AND s.mount_type = k.switch_mount
WHERE k.is_available = 1 AND s.is_available = 1
ORDER BY k.price_usd DESC, k.id;

SELECT TOP 1 @kit2 = k.id, @kitPrice2 = k.price_usd, @req2 = k.required_switch_quantity,
             @sw2 = s.id, @swPrice2 = s.price_usd
FROM keyboard_kits k INNER JOIN switches s
    ON s.switch_technology = k.pcb_technology AND s.mount_type = k.switch_mount
WHERE k.is_available = 1 AND s.is_available = 1 AND k.id <> @kit1
ORDER BY k.price_usd ASC, k.id;

IF @kit1 IS NULL
BEGIN
    RAISERROR('No compatible kit/switch combo found; cannot seed demo analytics.', 16, 1);
    RETURN;
END
IF @kit2 IS NULL
BEGIN
    SET @kit2 = @kit1; SET @sw2 = @sw1; SET @kitPrice2 = @kitPrice1; SET @swPrice2 = @swPrice1; SET @req2 = @req1;
END

-- ---- Generate orders across 8 months (i=0 is the current month, back to 7 months ago) ----
WHILE @i < 8
BEGIN
    SET @monthStart = DATEADD(MONTH, -@i, CAST(@base AS datetime2));
    SET @ordersThisMonth =
        CASE @i WHEN 0 THEN 3 WHEN 1 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 3
                WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 2 ELSE 1 END;

    SET @j = 0;
    WHILE @j < @ordersThisMonth
    BEGIN
        SET @seq += 1;

        SET @useCombo1 = CASE WHEN (@seq % 2) = 1 THEN 1 ELSE 0 END;
        SET @kit      = CASE WHEN @useCombo1 = 1 THEN @kit1 ELSE @kit2 END;
        SET @sw       = CASE WHEN @useCombo1 = 1 THEN @sw1 ELSE @sw2 END;
        SET @kitPrice = CASE WHEN @useCombo1 = 1 THEN @kitPrice1 ELSE @kitPrice2 END;
        SET @swPrice  = CASE WHEN @useCombo1 = 1 THEN @swPrice1 ELSE @swPrice2 END;
        SET @req      = CASE WHEN @useCombo1 = 1 THEN @req1 ELSE @req2 END;

        SET @seller = CASE WHEN (@seq % 2) = 1 THEN @sellerA ELSE @sellerB END;
        SET @buyer  = CASE (@seq % 3) WHEN 0 THEN @buyerA WHEN 1 THEN @buyerB ELSE @buyerC END;

        -- Mostly Completed; a few non-completed for a colourful status donut.
        SET @status = 'Completed';
        IF (@i = 0 AND @j = 0) SET @status = 'In_progress';
        ELSE IF (@i = 1 AND @j = 0) SET @status = 'Pending';
        ELSE IF (@i = 3 AND @j = 0) SET @status = 'Cancelled';

        SET @total = CAST(@kitPrice + (@swPrice * @req) AS decimal(10,2));
        SET @buildId = CONCAT('DEMO_BUILD_', @seq);
        SET @reqId   = CONCAT('DEMO_REQ_', @seq);

        SET @requested = DATEADD(DAY, 1, @monthStart);
        SET @accepted  = CASE WHEN @status IN ('Completed', 'In_progress') THEN DATEADD(DAY, 2, @monthStart) ELSE NULL END;
        SET @completed = CASE WHEN @status = 'Completed' THEN DATEADD(DAY, 3 + (@seq % 6), @monthStart) ELSE NULL END;

        INSERT INTO builds (id, buyer_id, kit_id, name, notes, status, total_cost_snapshot, created_at)
        VALUES (@buildId, @buyer, @kit, CONCAT('Demo build ', @seq), 'Demo analytics seed', 'Requested', @total, @requested);

        INSERT INTO build_items (build_id, switch_id, quantity, unit_price_snapshot, notes)
        VALUES (@buildId, @sw, @req, @swPrice, 'Demo seed switch');

        INSERT INTO build_requests
            (id, build_id, seller_user_id, request_payload_json, status, note, requested_at, accepted_at, completed_at, updated_at)
        VALUES
            (@reqId, @buildId, @seller, CONCAT('{"demo":true,"buildId":"', @buildId, '"}'), @status,
             'Demo analytics seed', @requested, @accepted, @completed, @completed);

        SET @j += 1;
    END
    SET @i += 1;
END

PRINT CONCAT('Inserted ', @seq, ' demo builds/requests.');

PRINT '--- Completed orders by month (platform, incl. demo) ---';
SELECT CONVERT(char(7), br.completed_at, 126) AS year_month,
       COUNT(*) AS orders,
       SUM(b.total_cost_snapshot) AS revenue
FROM build_requests br INNER JOIN builds b ON b.id = br.build_id
WHERE br.status = 'Completed' AND br.completed_at IS NOT NULL
GROUP BY CONVERT(char(7), br.completed_at, 126)
ORDER BY year_month;

PRINT '--- Status breakdown (platform, incl. demo) ---';
SELECT br.status AS status, COUNT(*) AS cnt
FROM build_requests br
GROUP BY br.status;

PRINT 'Demo analytics seed done.';

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
