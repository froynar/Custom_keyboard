:ON ERROR EXIT
-- Self-contained, guarded seed for optional analytics demo data.
-- Requires the final schema created by CreateSchema_Refactor.sql.
-- Run in SQLCMD mode (or with sqlcmd -b). Demo DML runs only after the
-- embedded pre-seed validation succeeds, then the same contracts are rechecked.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 51000, 'SeedDemoAnalytics_Refactor.sql requires an explicit application database.', 1;
END;
GO

-- Pre-seed validation for schema version 2026.07.27-simplified-read-views.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52900, 'SeedDemoAnalytics_Refactor.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-simplified-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52901, 'Schema version 2026.07.27-simplified-read-views is not recorded.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns AS column_info
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND column_info.name = N'completed_at'
      AND type_info.name = N'datetime2'
      AND column_info.scale = 7
      AND column_info.is_nullable = 1
)
BEGIN
    THROW 52912, 'device_test_sessions.completed_at does not match nullable datetime2(7).', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND name = N'CK_dts_completed_at'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
BEGIN
    THROW 52913, 'CK_dts_completed_at is missing, disabled, or untrusted.', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE (status = 'Running' AND completed_at IS NOT NULL)
       OR (status <> 'Running' AND completed_at IS NULL)
)
BEGIN
    THROW 52914, 'QC completion timestamps violate the lifecycle contract.', 1;
END;

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52902, 'One or more simplified read views are missing.', 1;
END;
GO

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Last_QC')) <> 19
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Catalog_Comps')) <> 7
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Build_items')) <> 17
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Req_view')) <> 15
BEGIN
    THROW 52903, 'One or more simplified views have an unexpected column count.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (VALUES
        (
            N'Last_QC',
            N'session_id,request_id,device_id,switch_technology,noise_requirement,total_keys,status,completed_at,tested_keys,passed_keys,warning_keys,failed_keys,average_latency_ms,max_latency_ms,average_noise_db,max_noise_db,last_recorded_at,is_complete,is_acceptable'
        ),
        (
            N'Catalog_Comps',
            N'component_type,component_id,name,brand_id,brand_name,price_usd,is_available'
        ),
        (
            N'Build_items',
            N'build_item_id,build_id,switch_id,keycap_id,stab_id,accessory_id,component_type,component_id,component_name,brand_id,brand_name,quantity,unit_price_snapshot,line_total_snapshot,current_price_usd,is_available,notes'
        ),
        (
            N'Req_view',
            N'request_id,build_id,seller_user_id,seller_shop_name,request_payload_json,status,note,requested_at,accepted_at,completed_at,updated_at,buyer_id,total_cost_snapshot,kit_id,kit_name'
        )
    ) AS expected_view(view_name, expected_columns)
    OUTER APPLY (
        SELECT
            STRING_AGG(CONVERT(nvarchar(max), column_info.name), N',')
                WITHIN GROUP (ORDER BY column_info.column_id) AS actual_columns
        FROM sys.views AS view_info
        INNER JOIN sys.schemas AS schema_info
            ON schema_info.schema_id = view_info.schema_id
        INNER JOIN sys.columns AS column_info
            ON column_info.object_id = view_info.object_id
        WHERE schema_info.name = N'views'
          AND view_info.name = expected_view.view_name
    ) AS actual_view
    WHERE actual_view.actual_columns IS NULL
       OR actual_view.actual_columns <> expected_view.expected_columns
)
BEGIN
    THROW 52915, 'One or more simplified view column contracts do not match.', 1;
END;
GO

IF (SELECT COUNT_BIG(*) FROM views.Last_QC) <>
   (SELECT COUNT_BIG(DISTINCT request_id) FROM dbo.device_test_sessions)
BEGIN
    THROW 52904, 'views.Last_QC does not cover every request with QC history.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT
            session_row.request_id,
            session_row.id AS session_id,
            session_row.completed_at,
            ROW_NUMBER() OVER (
                PARTITION BY session_row.request_id
                ORDER BY
                    CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
                    session_row.completed_at DESC,
                    session_row.id DESC
            ) AS latest_rank
        FROM dbo.device_test_sessions AS session_row
    ) AS expected_latest
    WHERE expected_latest.latest_rank = 1
      AND NOT EXISTS (
          SELECT 1
          FROM views.Last_QC AS actual_latest
          WHERE actual_latest.request_id = expected_latest.request_id
            AND actual_latest.session_id = expected_latest.session_id
            AND (
                  actual_latest.completed_at = expected_latest.completed_at
               OR (
                      actual_latest.completed_at IS NULL
                  AND expected_latest.completed_at IS NULL
               )
            )
      )
)
BEGIN
    THROW 52905, 'views.Last_QC does not select the expected latest session.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Catalog_Comps) <>
   (
       (SELECT COUNT_BIG(*) FROM dbo.keyboard_kits)
     + (SELECT COUNT_BIG(*) FROM dbo.switches)
     + (SELECT COUNT_BIG(*) FROM dbo.keycap_sets)
     + (SELECT COUNT_BIG(*) FROM dbo.stabilizers)
     + (SELECT COUNT_BIG(*) FROM dbo.accessories)
   )
BEGIN
    THROW 52906, 'views.Catalog_Comps does not cover the complete catalog.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Build_items) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_items)
BEGIN
    THROW 52907, 'views.Build_items does not resolve every build item.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns AS column_info
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'views.Build_items')
      AND column_info.name = N'line_total_snapshot'
      AND type_info.name = N'decimal'
      AND column_info.precision = 28
      AND column_info.scale = 2
)
BEGIN
    THROW 52908, 'views.Build_items.line_total_snapshot is not decimal(28,2).', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM views.Build_items AS projected_item
    INNER JOIN dbo.build_items AS source_item
        ON source_item.id = projected_item.build_item_id
    WHERE projected_item.line_total_snapshot <>
          CAST(
              CAST(source_item.quantity AS decimal(18,0))
              * CAST(source_item.unit_price_snapshot AS decimal(18,2))
              AS decimal(28,2)
          )
)
BEGIN
    THROW 52909, 'views.Build_items line totals do not match historical quantity and price.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Req_view) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_requests)
BEGIN
    THROW 52910, 'views.Req_view does not cover every build request.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.build_requests AS request_row
    LEFT JOIN views.Last_QC AS latest_qc
        ON latest_qc.request_id = request_row.id
    WHERE request_row.status = 'Completed'
      AND COALESCE(latest_qc.is_acceptable, 0) = 0
)
BEGIN
    THROW 52911, 'A Completed request does not have an acceptable latest QC session.', 1;
END;

PRINT 'Pre-seed schema and read-view validation passed.';
GO
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
BEGIN TRANSACTION;

PRINT 'Seeding demo analytics data (DEMO_ rows)...';

-- ---- Idempotent cleanup of any previous demo rows ([_] escapes the LIKE wildcard) ----
DELETE result_row
FROM device_key_test_results AS result_row
INNER JOIN device_test_sessions AS session_row
    ON session_row.id = result_row.session_id
WHERE session_row.request_id LIKE 'DEMO[_]REQ[_]%';

DELETE FROM device_test_sessions WHERE request_id LIKE 'DEMO[_]REQ[_]%';
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
DECLARE @deviceId varchar(50), @sessionId varchar(50), @switchTechnology varchar(50);

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
        SELECT @switchTechnology = switch_technology
        FROM switches
        WHERE id = @sw;

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
            (
                @reqId,
                @buildId,
                @seller,
                CONCAT(
                    '{"demo":true,"build":{"buildId":"', @buildId,
                    '","name":"Demo build ', @seq,
                    '","noiseRequirement":"Normal","status":"Requested","totalCostSnapshot":',
                    CONVERT(varchar(32), @total),
                    '},"kit":{"kitId":"', @kit,
                    '","pcbTechnology":"', @switchTechnology,
                    '","requiredSwitchQuantity":', @req,
                    '},"items":[],"mods":[]}'
                ),
                @status,
             'Demo analytics seed', @requested, @accepted, @completed, @completed);

        -- A Completed request must carry a complete acceptable QC result. Reuse the
        -- seller's active station or create a deterministic demo station when needed.
        IF @status = 'Completed'
        BEGIN
            SET @deviceId = NULL;
            SELECT TOP (1) @deviceId = id
            FROM devices
            WHERE seller_user_id = @seller
              AND device_type = 'QC_STATION'
              AND is_active = 1
            ORDER BY created_at, id;

            IF @deviceId IS NULL
            BEGIN
                SET @deviceId = CONCAT('DEMO_DEV_', @seller);

                IF EXISTS (SELECT 1 FROM devices WHERE id = @deviceId)
                BEGIN
                    UPDATE devices
                    SET
                        seller_user_id = @seller,
                        device_name = 'Demo QC Station',
                        device_type = 'QC_STATION',
                        is_active = 1,
                        last_seen_at = @completed
                    WHERE id = @deviceId;
                END
                ELSE
                BEGIN
                    INSERT INTO devices (
                        id, seller_user_id, device_name, device_type,
                        is_active, last_seen_at, created_at
                    )
                    VALUES (
                        @deviceId, @seller, 'Demo QC Station', 'QC_STATION',
                        1, @completed, @requested
                    );
                END;
            END;

            SET @sessionId = CONCAT('DEMO_QC_', @seq);
            INSERT INTO device_test_sessions (
                id, request_id, device_id, switch_technology,
                noise_requirement, total_keys, status, completed_at
            )
            VALUES (
                @sessionId, @reqId, @deviceId, @switchTechnology,
                'Normal', @req, 'Passed', @completed
            );

            ;WITH key_numbers AS (
                SELECT TOP (@req)
                    ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS key_number
                FROM sys.all_objects
            )
            INSERT INTO device_key_test_results (
                session_id, key_code, received_key, press_signal_detected,
                latency, press_count, release_signal, hold_duration,
                noise, result, recorded_at
            )
            SELECT
                @sessionId,
                CONCAT('Key', key_number),
                CONCAT('Key', key_number),
                1,
                CAST(2.00 AS decimal(8,2)),
                1,
                1,
                80,
                CAST(40.00 AS decimal(8,2)),
                'Pass',
                DATEADD(MILLISECOND, key_number - @req, @completed)
            FROM key_numbers;
        END;

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
GO
-- Post-seed validation for schema version 2026.07.27-simplified-read-views.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 52900, 'SeedDemoAnalytics_Refactor.sql requires an explicit application database.', 1;
END;

IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM dbo.schema_migrations
       WHERE version = '2026.07.27-simplified-read-views'
         AND succeeded = 1
   )
BEGIN
    THROW 52901, 'Schema version 2026.07.27-simplified-read-views is not recorded.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns AS column_info
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND column_info.name = N'completed_at'
      AND type_info.name = N'datetime2'
      AND column_info.scale = 7
      AND column_info.is_nullable = 1
)
BEGIN
    THROW 52912, 'device_test_sessions.completed_at does not match nullable datetime2(7).', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.device_test_sessions')
      AND name = N'CK_dts_completed_at'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
BEGIN
    THROW 52913, 'CK_dts_completed_at is missing, disabled, or untrusted.', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.device_test_sessions
    WHERE (status = 'Running' AND completed_at IS NOT NULL)
       OR (status <> 'Running' AND completed_at IS NULL)
)
BEGIN
    THROW 52914, 'QC completion timestamps violate the lifecycle contract.', 1;
END;

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 52902, 'One or more simplified read views are missing.', 1;
END;
GO

IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Last_QC')) <> 19
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Catalog_Comps')) <> 7
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Build_items')) <> 17
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'views.Req_view')) <> 15
BEGIN
    THROW 52903, 'One or more simplified views have an unexpected column count.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (VALUES
        (
            N'Last_QC',
            N'session_id,request_id,device_id,switch_technology,noise_requirement,total_keys,status,completed_at,tested_keys,passed_keys,warning_keys,failed_keys,average_latency_ms,max_latency_ms,average_noise_db,max_noise_db,last_recorded_at,is_complete,is_acceptable'
        ),
        (
            N'Catalog_Comps',
            N'component_type,component_id,name,brand_id,brand_name,price_usd,is_available'
        ),
        (
            N'Build_items',
            N'build_item_id,build_id,switch_id,keycap_id,stab_id,accessory_id,component_type,component_id,component_name,brand_id,brand_name,quantity,unit_price_snapshot,line_total_snapshot,current_price_usd,is_available,notes'
        ),
        (
            N'Req_view',
            N'request_id,build_id,seller_user_id,seller_shop_name,request_payload_json,status,note,requested_at,accepted_at,completed_at,updated_at,buyer_id,total_cost_snapshot,kit_id,kit_name'
        )
    ) AS expected_view(view_name, expected_columns)
    OUTER APPLY (
        SELECT
            STRING_AGG(CONVERT(nvarchar(max), column_info.name), N',')
                WITHIN GROUP (ORDER BY column_info.column_id) AS actual_columns
        FROM sys.views AS view_info
        INNER JOIN sys.schemas AS schema_info
            ON schema_info.schema_id = view_info.schema_id
        INNER JOIN sys.columns AS column_info
            ON column_info.object_id = view_info.object_id
        WHERE schema_info.name = N'views'
          AND view_info.name = expected_view.view_name
    ) AS actual_view
    WHERE actual_view.actual_columns IS NULL
       OR actual_view.actual_columns <> expected_view.expected_columns
)
BEGIN
    THROW 52915, 'One or more simplified view column contracts do not match.', 1;
END;
GO

IF (SELECT COUNT_BIG(*) FROM views.Last_QC) <>
   (SELECT COUNT_BIG(DISTINCT request_id) FROM dbo.device_test_sessions)
BEGIN
    THROW 52904, 'views.Last_QC does not cover every request with QC history.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT
            session_row.request_id,
            session_row.id AS session_id,
            session_row.completed_at,
            ROW_NUMBER() OVER (
                PARTITION BY session_row.request_id
                ORDER BY
                    CASE WHEN session_row.status = 'Running' THEN 0 ELSE 1 END,
                    session_row.completed_at DESC,
                    session_row.id DESC
            ) AS latest_rank
        FROM dbo.device_test_sessions AS session_row
    ) AS expected_latest
    WHERE expected_latest.latest_rank = 1
      AND NOT EXISTS (
          SELECT 1
          FROM views.Last_QC AS actual_latest
          WHERE actual_latest.request_id = expected_latest.request_id
            AND actual_latest.session_id = expected_latest.session_id
            AND (
                  actual_latest.completed_at = expected_latest.completed_at
               OR (
                      actual_latest.completed_at IS NULL
                  AND expected_latest.completed_at IS NULL
               )
            )
      )
)
BEGIN
    THROW 52905, 'views.Last_QC does not select the expected latest session.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Catalog_Comps) <>
   (
       (SELECT COUNT_BIG(*) FROM dbo.keyboard_kits)
     + (SELECT COUNT_BIG(*) FROM dbo.switches)
     + (SELECT COUNT_BIG(*) FROM dbo.keycap_sets)
     + (SELECT COUNT_BIG(*) FROM dbo.stabilizers)
     + (SELECT COUNT_BIG(*) FROM dbo.accessories)
   )
BEGIN
    THROW 52906, 'views.Catalog_Comps does not cover the complete catalog.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Build_items) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_items)
BEGIN
    THROW 52907, 'views.Build_items does not resolve every build item.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns AS column_info
    INNER JOIN sys.types AS type_info
        ON type_info.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'views.Build_items')
      AND column_info.name = N'line_total_snapshot'
      AND type_info.name = N'decimal'
      AND column_info.precision = 28
      AND column_info.scale = 2
)
BEGIN
    THROW 52908, 'views.Build_items.line_total_snapshot is not decimal(28,2).', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM views.Build_items AS projected_item
    INNER JOIN dbo.build_items AS source_item
        ON source_item.id = projected_item.build_item_id
    WHERE projected_item.line_total_snapshot <>
          CAST(
              CAST(source_item.quantity AS decimal(18,0))
              * CAST(source_item.unit_price_snapshot AS decimal(18,2))
              AS decimal(28,2)
          )
)
BEGIN
    THROW 52909, 'views.Build_items line totals do not match historical quantity and price.', 1;
END;

IF (SELECT COUNT_BIG(*) FROM views.Req_view) <>
   (SELECT COUNT_BIG(*) FROM dbo.build_requests)
BEGIN
    THROW 52910, 'views.Req_view does not cover every build request.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.build_requests AS request_row
    LEFT JOIN views.Last_QC AS latest_qc
        ON latest_qc.request_id = request_row.id
    WHERE request_row.status = 'Completed'
      AND COALESCE(latest_qc.is_acceptable, 0) = 0
)
BEGIN
    THROW 52911, 'A Completed request does not have an acceptable latest QC session.', 1;
END;

PRINT 'Post-seed schema and read-view validation passed.';
