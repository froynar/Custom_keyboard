-- Custom Keyboard Builder - Refactor schema (kit-based ERD)
-- Source of truth: Documents/Custom_Keyboard_ERD_Final.dbml
-- Scope: 21 tables / 30 relationships (incl. 3 device QC tables). No cases/pcbs/plates, no compatibility_rules,
--        no seller_inventory, no legacy switch-mod columns (lube_type/is_filmed/spring_weight_g).
-- Target: clean/disposable CKDB_Clean database.
-- WARNING: destructive clean-install script. Re-running it drops the 21 business tables
-- and resets dbo.schema_migrations.
-- CKDB and CustomKeyboard_Refactor are intentionally not targeted by this script.
-- For a database that must preserve runtime data, use the ordered migration scripts instead.
-- Order: roles -> users -> seller_profiles -> seller_applications -> brands -> layouts -> keyboard_kits
--        -> switches -> keycap_sets -> stabilizers -> accessories -> builds -> build_items -> build_mods
--        -> build_requests -> devices -> device_test_sessions -> device_key_test_results
--        -> audit_log -> chat_conversations -> chat_messages
-- This script is idempotent: it drops the 21 tables (reverse FK order), resets schema
-- migration history, and recreates the clean schema.
-- Clean installs record the concise QC, build/device hardening,
-- Completed/QC reconciliation, read-view, QC/view hardening, and simplified
-- read-view schema versions.

IF DB_ID(N'CKDB_Clean') IS NULL
BEGIN
    CREATE DATABASE CKDB_Clean;
END
GO

USE CKDB_Clean;
GO

SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- ---------------------------------------------------------------------------
-- Drop in reverse FK-dependency order so a re-run starts from a clean slate.
-- ---------------------------------------------------------------------------
DROP VIEW IF EXISTS views.Req_view;
DROP VIEW IF EXISTS views.Build_items;
DROP VIEW IF EXISTS views.Catalog_Comps;
DROP VIEW IF EXISTS views.Last_QC;
DROP TABLE IF EXISTS dbo.schema_migrations;
DROP TABLE IF EXISTS device_key_test_results;
DROP TABLE IF EXISTS device_test_sessions;
DROP TABLE IF EXISTS devices;
DROP TABLE IF EXISTS chat_messages;
DROP TABLE IF EXISTS chat_conversations;
DROP TABLE IF EXISTS audit_log;
DROP TABLE IF EXISTS build_requests;
DROP TABLE IF EXISTS build_mods;
DROP TABLE IF EXISTS build_items;
DROP TABLE IF EXISTS builds;
DROP TABLE IF EXISTS accessories;
DROP TABLE IF EXISTS stabilizers;
DROP TABLE IF EXISTS keycap_sets;
DROP TABLE IF EXISTS switches;
DROP TABLE IF EXISTS keyboard_kits;
DROP TABLE IF EXISTS layouts;
DROP TABLE IF EXISTS brands;
DROP TABLE IF EXISTS seller_applications;
DROP TABLE IF EXISTS seller_profiles;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS roles;
GO

-- ===========================================================================
-- 1. roles
-- ===========================================================================
CREATE TABLE roles (
    id INT IDENTITY(1,1) PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE,           -- Buyer, Seller, Admin
    permissions VARCHAR(MAX) NULL
);
GO

-- ===========================================================================
-- 2. users
-- ===========================================================================
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    role_id INT NOT NULL,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    phone VARCHAR(30) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES roles(id)
);
GO

-- ===========================================================================
-- 3. seller_profiles
-- ===========================================================================
CREATE TABLE seller_profiles (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL UNIQUE,
    shop_name VARCHAR(255) NOT NULL,
    phone VARCHAR(30) NOT NULL,
    address VARCHAR(500) NOT NULL,
    is_verified BIT NOT NULL DEFAULT 0,
    verified_at DATETIME2 NULL,
    CONSTRAINT FK_seller_profiles_user FOREIGN KEY (user_id) REFERENCES users(id)
);
GO

-- ===========================================================================
-- 3b. seller_applications  (buyer -> seller upgrade requests, reviewed by admin)
-- ===========================================================================
CREATE TABLE seller_applications (
    id INT IDENTITY(1,1) PRIMARY KEY,
    buyer_user_id INT NOT NULL,
    shop_name VARCHAR(255) NOT NULL,
    phone VARCHAR(30) NOT NULL,
    address VARCHAR(500) NOT NULL,
    note VARCHAR(500) NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',   -- Pending, Approved, Rejected
    review_note VARCHAR(500) NULL,                   -- admin reason on approve/reject
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    reviewed_at DATETIME2 NULL,
    reviewed_by INT NULL,
    CONSTRAINT FK_seller_applications_buyer FOREIGN KEY (buyer_user_id) REFERENCES users(id),
    CONSTRAINT FK_seller_applications_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(id),
    CONSTRAINT CK_seller_applications_status CHECK (status IN ('Pending', 'Approved', 'Rejected'))
);
GO

-- ===========================================================================
-- 4. brands
-- ===========================================================================
CREATE TABLE brands (
    id INT IDENTITY(1,1) PRIMARY KEY,
    brand_name VARCHAR(255) NOT NULL UNIQUE,
    country VARCHAR(100) NULL
);
GO

-- ===========================================================================
-- 5. layouts
-- ===========================================================================
CREATE TABLE layouts (
    id VARCHAR(50) PRIMARY KEY,
    layout_name VARCHAR(100) NOT NULL,
    form_factor VARCHAR(100) NOT NULL,               -- 60, 65, 75, TKL, 100, Alice...
    key_count INT NOT NULL
);
GO

-- ===========================================================================
-- 6. keyboard_kits  (gop case/PCB/plate/foam/cable qua included_parts)
-- ===========================================================================
CREATE TABLE keyboard_kits (
    id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    layout_id VARCHAR(50) NOT NULL,
    kit_name VARCHAR(255) NOT NULL,
    pcb_technology VARCHAR(50) NOT NULL,             -- Mechanical, HE, Topre, Optical
    switch_mount VARCHAR(100) NOT NULL,              -- MX 3-pin, MX 5-pin, HE, Topre, Optical
    required_switch_quantity INT NOT NULL,
    included_parts VARCHAR(500) NULL,                -- case, PCB, plate, foam, cable...
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_keyboard_kits_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT FK_keyboard_kits_layouts FOREIGN KEY (layout_id) REFERENCES layouts(id),
    CONSTRAINT CK_keyboard_kits_price CHECK (price_usd >= 0),
    CONSTRAINT CK_keyboard_kits_switch_qty CHECK (required_switch_quantity > 0)
);
GO

-- ===========================================================================
-- 7. switches  (unit price per switch)
-- ===========================================================================
CREATE TABLE switches (
    id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    switch_name VARCHAR(255) NOT NULL,
    switch_technology VARCHAR(50) NOT NULL,          -- Mechanical, HE, Topre, Optical
    mount_type VARCHAR(100) NOT NULL,                -- MX 3-pin, MX 5-pin, HE, Topre, Optical
    switch_type VARCHAR(100) NULL,
    actuation_force_g INT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_switches_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_switches_price CHECK (price_usd >= 0)
);
GO

-- ===========================================================================
-- 8. keycap_sets
-- ===========================================================================
CREATE TABLE keycap_sets (
    id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    keycap_name VARCHAR(255) NOT NULL,
    supported_form_factor VARCHAR(255) NOT NULL,     -- 60/65/75/TKL/100/universal notes
    profile VARCHAR(100) NULL,
    material VARCHAR(100) NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_keycap_sets_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_keycap_sets_price CHECK (price_usd >= 0)
);
GO

-- ===========================================================================
-- 9. stabilizers  (goi stabilizer co ban theo layout)
-- ===========================================================================
CREATE TABLE stabilizers (
    id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    stab_name VARCHAR(255) NOT NULL,
    supported_layouts VARCHAR(255) NOT NULL,         -- 60/65/75/TKL/100 or universal
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_stabilizers_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_stabilizers_price CHECK (price_usd >= 0)
);
GO

-- ===========================================================================
-- 10. accessories  (lube, film, cable, foam, tool...)
-- ===========================================================================
CREATE TABLE accessories (
    id VARCHAR(50) PRIMARY KEY,
    accessory_type VARCHAR(100) NOT NULL,            -- Lube, Spring, Film, Foam, Cable, Tool
    accessory_name VARCHAR(255) NOT NULL,
    target_component VARCHAR(100) NULL,              -- Switch, Stabilizer, Kit, General
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_accessories_price CHECK (price_usd >= 0)
);
GO

-- ===========================================================================
-- 11. builds  (chi giu kit, buyer, status, tong snapshot)
-- ===========================================================================
CREATE TABLE builds (
    id VARCHAR(50) PRIMARY KEY,
    buyer_id INT NOT NULL,
    kit_id VARCHAR(50) NOT NULL,
    name NVARCHAR(255) NOT NULL,
    notes NVARCHAR(500) NULL,
    noise_requirement VARCHAR(20) NOT NULL DEFAULT 'Normal', -- Normal / Quiet / Silent
    status VARCHAR(50) NOT NULL,                     -- Draft, Saved, Requested, Archived
    total_cost_snapshot DECIMAL(10,2) NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL,
    CONSTRAINT FK_builds_buyer FOREIGN KEY (buyer_id) REFERENCES users(id),
    CONSTRAINT FK_builds_kit FOREIGN KEY (kit_id) REFERENCES keyboard_kits(id),
    CONSTRAINT CK_builds_noise_requirement CHECK (noise_requirement IN ('Normal','Quiet','Silent')),
    CONSTRAINT CK_builds_status CHECK (status IN ('Draft', 'Saved', 'Requested', 'Archived')),
    CONSTRAINT CK_builds_total CHECK (total_cost_snapshot >= 0),
    CONSTRAINT CK_builds_name_not_blank CHECK (LEN(LTRIM(RTRIM(name))) > 0)
);
GO

-- ===========================================================================
-- 12. build_items  (moi dong dung 1 FK san pham: switch/keycap/stab/accessory)
-- ===========================================================================
CREATE TABLE build_items (
    id INT IDENTITY(1,1) PRIMARY KEY,
    build_id VARCHAR(50) NOT NULL,
    switch_id VARCHAR(50) NULL,
    keycap_id VARCHAR(50) NULL,
    stab_id VARCHAR(50) NULL,
    accessory_id VARCHAR(50) NULL,
    quantity INT NOT NULL,
    unit_price_snapshot DECIMAL(10,2) NOT NULL,
    notes NVARCHAR(500) NULL,
    CONSTRAINT FK_build_items_build FOREIGN KEY (build_id) REFERENCES builds(id),
    CONSTRAINT FK_build_items_switch FOREIGN KEY (switch_id) REFERENCES switches(id),
    CONSTRAINT FK_build_items_keycap FOREIGN KEY (keycap_id) REFERENCES keycap_sets(id),
    CONSTRAINT FK_build_items_stab FOREIGN KEY (stab_id) REFERENCES stabilizers(id),
    CONSTRAINT FK_build_items_accessory FOREIGN KEY (accessory_id) REFERENCES accessories(id),
    CONSTRAINT CK_build_items_quantity CHECK (quantity > 0),
    CONSTRAINT CK_build_items_unit_price CHECK (unit_price_snapshot >= 0),
    -- Exactly one product FK must be set.
    CONSTRAINT CK_build_items_exactly_one_fk CHECK (
        (CASE WHEN switch_id    IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN keycap_id    IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN stab_id      IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) = 1
    )
);
GO

-- ===========================================================================
-- 13. build_mods  (Lube, Film, Spring_swap, Tape_mod, Foam_mod...)
-- ===========================================================================
CREATE TABLE build_mods (
    id INT IDENTITY(1,1) PRIMARY KEY,
    build_id VARCHAR(50) NOT NULL,
    mod_type NVARCHAR(100) NOT NULL,                 -- Lube, Film, Spring_swap, Tape_mod, Foam_mod
    target_component NVARCHAR(100) NOT NULL,         -- Switch, Stabilizer, Kit, Build
    notes NVARCHAR(500) NULL,
    CONSTRAINT FK_build_mods_build FOREIGN KEY (build_id) REFERENCES builds(id)
);
GO

-- ===========================================================================
-- 14. build_requests  (buyer gui cho seller; snapshot payload JSON)
-- ===========================================================================
CREATE TABLE build_requests (
    id VARCHAR(50) PRIMARY KEY,
    build_id VARCHAR(50) NOT NULL,
    seller_user_id INT NOT NULL,
    request_payload_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(50) NOT NULL,                     -- Pending, Accepted, In_progress, Completed, Cancelled
    note NVARCHAR(500) NULL,
    requested_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    accepted_at DATETIME2 NULL,
    completed_at DATETIME2 NULL,
    updated_at DATETIME2 NULL,
    CONSTRAINT FK_build_requests_build FOREIGN KEY (build_id) REFERENCES builds(id),
    CONSTRAINT FK_build_requests_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT CK_build_requests_status CHECK (status IN ('Pending', 'Accepted', 'In_progress', 'Completed', 'Cancelled'))
);
GO

-- ===========================================================================
-- 14a. devices  (tram QC cua seller)
-- ===========================================================================
CREATE TABLE devices (
    id               VARCHAR(50)  PRIMARY KEY,        -- vd DEV_{Guid:N} hoac 'QC-STATION-01'
    seller_user_id   INT          NOT NULL,
    device_name      NVARCHAR(100) NOT NULL,
    device_type      VARCHAR(50)  NOT NULL,           -- QC_STATION (gop) / KEY_SIGNAL_TESTER / LATENCY_TESTER / NOISE_SENSOR
    is_active        BIT          NOT NULL DEFAULT 1,
    last_seen_at     DATETIME2    NULL,
    created_at       DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_devices_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT CK_devices_type CHECK (device_type IN ('QC_STATION','KEY_SIGNAL_TESTER','LATENCY_TESTER','NOISE_SENSOR')),
    CONSTRAINT CK_devices_name_not_blank CHECK (LEN(LTRIM(RTRIM(device_name))) > 0)
);
GO

-- ===========================================================================
-- 14b. device_test_sessions  (mot phien QC cho mot request)
-- ===========================================================================
CREATE TABLE device_test_sessions (
    id                 VARCHAR(50) PRIMARY KEY,        -- vd QCSESS_{Guid:N}
    request_id         VARCHAR(50) NOT NULL,
    device_id          VARCHAR(50) NOT NULL,
    switch_technology  VARCHAR(50) NOT NULL,           -- Mechanical / HE (lay tu kit.pcbTechnology)
    noise_requirement  VARCHAR(20) NOT NULL DEFAULT 'Normal', -- Normal / Quiet / Silent (buyer expectation)
    total_keys         INT         NOT NULL,
    status             VARCHAR(20) NOT NULL,           -- Running / Passed / Warning / Failed
    completed_at       DATETIME2   NULL,               -- set once when QC leaves Running
    CONSTRAINT FK_dts_request FOREIGN KEY (request_id) REFERENCES build_requests(id),
    CONSTRAINT FK_dts_device  FOREIGN KEY (device_id)  REFERENCES devices(id),
    CONSTRAINT CK_dts_noise_requirement CHECK (noise_requirement IN ('Normal','Quiet','Silent')),
    CONSTRAINT CK_dts_status CHECK (status IN ('Running','Passed','Warning','Failed')),
    CONSTRAINT CK_dts_completed_at CHECK (
        (status = 'Running' AND completed_at IS NULL)
        OR (status IN ('Passed','Warning','Failed') AND completed_at IS NOT NULL)
    ),
    CONSTRAINT CK_dts_total_keys CHECK (total_keys BETWEEN 1 AND 256),
    CONSTRAINT CK_dts_switch_technology_not_blank CHECK (LEN(LTRIM(RTRIM(switch_technology))) > 0)
);
GO

-- ===========================================================================
-- 14c. device_key_test_results  (ket qua tung phim -- bang chi tiet chinh)
-- ===========================================================================
CREATE TABLE device_key_test_results (
    id                      BIGINT IDENTITY(1,1) PRIMARY KEY,
    session_id              VARCHAR(50) NOT NULL,
    key_code                VARCHAR(30) NOT NULL,
    received_key            VARCHAR(30) NULL,
    press_signal_detected   BIT         NOT NULL,
    latency                 DECIMAL(8,2) NULL,
    press_count             INT         NOT NULL,
    release_signal          BIT         NOT NULL,
    hold_duration           INT         NULL,
    noise                   DECIMAL(8,2) NULL,
    result                  VARCHAR(20) NOT NULL,       -- Pass / Warning / Fail
    recorded_at             DATETIME2   NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_dktr_session FOREIGN KEY (session_id) REFERENCES device_test_sessions(id),
    CONSTRAINT CK_dktr_result CHECK (result IN ('Pass','Warning','Fail')),
    CONSTRAINT CK_dktr_press_count CHECK (press_count BETWEEN 0 AND 100),
    CONSTRAINT CK_dktr_latency CHECK (latency IS NULL OR latency BETWEEN 0 AND 10000),
    CONSTRAINT CK_dktr_hold_duration CHECK (hold_duration IS NULL OR hold_duration BETWEEN 0 AND 3600000),
    CONSTRAINT CK_dktr_noise CHECK (noise IS NULL OR noise BETWEEN 0 AND 200),
    CONSTRAINT CK_dktr_key_code_not_blank CHECK (LEN(LTRIM(RTRIM(key_code))) > 0)
);
GO

-- ===========================================================================
-- 15. audit_log
-- ===========================================================================
CREATE TABLE audit_log (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    record_id VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    old_value_json NVARCHAR(MAX) NULL,
    new_value_json NVARCHAR(MAX) NULL,
    changed_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_audit_log_user FOREIGN KEY (user_id) REFERENCES users(id)
);
GO

-- ===========================================================================
-- 16. chat_conversations  (seller-buyer hoac seller-admin)
-- ===========================================================================
CREATE TABLE chat_conversations (
    id VARCHAR(50) PRIMARY KEY,
    seller_user_id INT NOT NULL,
    buyer_id INT NULL,
    admin_user_id INT NULL,
    build_request_id VARCHAR(50) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL,
    CONSTRAINT FK_chat_conversations_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_buyer FOREIGN KEY (buyer_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_admin FOREIGN KEY (admin_user_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_request FOREIGN KEY (build_request_id) REFERENCES build_requests(id),
    -- Exactly one of buyer_id or admin_user_id must be set (buyer-admin direct chat not allowed).
    CONSTRAINT CK_chat_conversations_participant CHECK (
        (CASE WHEN buyer_id      IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) = 1
    )
);
GO

-- ===========================================================================
-- 17. chat_messages
-- ===========================================================================
CREATE TABLE chat_messages (
    id VARCHAR(50) PRIMARY KEY,
    conversation_id VARCHAR(50) NOT NULL,
    sender_user_id INT NOT NULL,
    message_text NVARCHAR(MAX) NOT NULL,
    sent_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_chat_messages_conversation FOREIGN KEY (conversation_id) REFERENCES chat_conversations(id),
    CONSTRAINT FK_chat_messages_sender FOREIGN KEY (sender_user_id) REFERENCES users(id)
);
GO

-- ===========================================================================
-- Indexes (FK lookups, availability filters, status filters)
-- ===========================================================================
CREATE INDEX IX_users_role_id ON users(role_id);
CREATE INDEX IX_users_is_active ON users(is_active);
CREATE INDEX IX_seller_profiles_is_verified ON seller_profiles(is_verified);

CREATE INDEX IX_keyboard_kits_brand_id ON keyboard_kits(brand_id);
CREATE INDEX IX_keyboard_kits_layout_id ON keyboard_kits(layout_id);
CREATE INDEX IX_keyboard_kits_is_available ON keyboard_kits(is_available);

CREATE INDEX IX_switches_brand_id ON switches(brand_id);
CREATE INDEX IX_switches_is_available ON switches(is_available);
CREATE INDEX IX_keycap_sets_brand_id ON keycap_sets(brand_id);
CREATE INDEX IX_keycap_sets_is_available ON keycap_sets(is_available);
CREATE INDEX IX_stabilizers_brand_id ON stabilizers(brand_id);
CREATE INDEX IX_stabilizers_is_available ON stabilizers(is_available);
CREATE INDEX IX_accessories_is_available ON accessories(is_available);

CREATE INDEX IX_builds_buyer_id ON builds(buyer_id);
CREATE INDEX IX_builds_kit_id ON builds(kit_id);
CREATE INDEX IX_builds_status ON builds(status);

CREATE INDEX IX_build_items_build_id ON build_items(build_id);
CREATE INDEX IX_build_items_switch_id ON build_items(switch_id);
CREATE INDEX IX_build_items_keycap_id ON build_items(keycap_id);
CREATE INDEX IX_build_items_stab_id ON build_items(stab_id);
CREATE INDEX IX_build_items_accessory_id ON build_items(accessory_id);

CREATE INDEX IX_build_mods_build_id ON build_mods(build_id);

CREATE INDEX IX_build_requests_build_id ON build_requests(build_id);
CREATE INDEX IX_build_requests_seller_status ON build_requests(seller_user_id, status);
CREATE INDEX IX_build_requests_status ON build_requests(status);
CREATE UNIQUE INDEX UX_build_requests_one_active_per_build
ON build_requests(build_id)
WHERE status IN ('Pending', 'Accepted', 'In_progress');

CREATE INDEX IX_audit_log_user_changed_at ON audit_log(user_id, changed_at DESC);

CREATE INDEX IX_chat_conversations_seller ON chat_conversations(seller_user_id);
CREATE INDEX IX_chat_conversations_buyer ON chat_conversations(buyer_id);
CREATE INDEX IX_chat_conversations_admin ON chat_conversations(admin_user_id);
CREATE INDEX IX_chat_conversations_request ON chat_conversations(build_request_id);

CREATE INDEX IX_chat_messages_conversation ON chat_messages(conversation_id);
CREATE INDEX IX_chat_messages_sender ON chat_messages(sender_user_id);

CREATE INDEX IX_seller_applications_status ON seller_applications(status);
CREATE INDEX IX_seller_applications_buyer ON seller_applications(buyer_user_id);
CREATE UNIQUE INDEX UX_seller_applications_pending_buyer
ON seller_applications(buyer_user_id)
WHERE status = 'Pending';

-- Device QC layer
CREATE INDEX IX_devices_seller ON devices(seller_user_id);
CREATE UNIQUE INDEX UX_devices_one_active_qc_station_per_seller
ON devices(seller_user_id)
WHERE is_active = 1 AND device_type = 'QC_STATION';
CREATE INDEX IX_dts_request ON device_test_sessions(request_id);
CREATE INDEX IX_dts_device ON device_test_sessions(device_id);
CREATE UNIQUE INDEX UX_dts_one_running_per_request
ON device_test_sessions(request_id)
WHERE status = 'Running';
CREATE INDEX IX_dktr_session ON device_key_test_results(session_id);
CREATE UNIQUE INDEX UX_dktr_session_key_code ON device_key_test_results(session_id, key_code);
GO

-- ===========================================================================
-- Read-only repository projections.
-- Run CreateSchema_Refactor.sql from the repository root in sqlcmd mode.
-- ===========================================================================
:r Database\SqlServer\ReadViews.sql

IF OBJECT_ID(N'views.Last_QC', N'V') IS NULL
   OR OBJECT_ID(N'views.Catalog_Comps', N'V') IS NULL
   OR OBJECT_ID(N'views.Build_items', N'V') IS NULL
   OR OBJECT_ID(N'views.Req_view', N'V') IS NULL
BEGIN
    THROW 51000, 'Clean schema creation did not create all four read views.', 1;
END;
GO

-- ===========================================================================
-- Schema version contract used by application startup guard and migration tools.
-- ===========================================================================
CREATE TABLE dbo.schema_migrations (
    version VARCHAR(64) NOT NULL CONSTRAINT PK_schema_migrations PRIMARY KEY,
    description VARCHAR(255) NOT NULL,
    applied_at DATETIME2 NOT NULL CONSTRAINT DF_schema_migrations_applied_at DEFAULT SYSUTCDATETIME(),
    succeeded BIT NOT NULL CONSTRAINT DF_schema_migrations_succeeded DEFAULT 1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-qc-concise',
    'Concise normalized QC schema with 21 business tables and 30 foreign keys',
    SYSUTCDATETIME(),
    1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-build-device-hardening',
    'Atomic build requests, Unicode text, and hardened device QC invariants',
    SYSUTCDATETIME(),
    1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-completed-qc-reconciliation',
    'Reconcile historical Completed requests with latest complete QC truth',
    SYSUTCDATETIME(),
    1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-read-views',
    'Add Last_QC, Catalog_Comps, Build_items, and Req_view read projections',
    SYSUTCDATETIME(),
    1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-qc-view-hardening',
    'Add QC completion time and harden read-view contracts',
    SYSUTCDATETIME(),
    1
);

INSERT INTO dbo.schema_migrations (version, description, applied_at, succeeded)
VALUES (
    '2026.07.27-simplified-read-views',
    'Simplify the four read-view contracts without changing application behavior',
    SYSUTCDATETIME(),
    1
);
GO
