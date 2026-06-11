IF DB_ID(N'CustomKeyboardBuilder') IS NULL
BEGIN
    CREATE DATABASE CustomKeyboardBuilder;
END
GO

USE CustomKeyboardBuilder;
GO

CREATE TABLE roles (
    role_id INT IDENTITY(1,1) PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE,
    permissions VARCHAR(MAX) NULL
);

CREATE TABLE users (
    user_id INT IDENTITY(1,1) PRIMARY KEY,
    role_id INT NOT NULL,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    phone VARCHAR(30) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES roles(role_id)
);

CREATE TABLE seller_profiles (
    seller_profile_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL UNIQUE,
    assigned_by_admin_id INT NULL,
    shop_name VARCHAR(255) NOT NULL,
    phone VARCHAR(30) NOT NULL,
    address VARCHAR(500) NOT NULL,
    is_verified BIT NOT NULL DEFAULT 0,
    verified_by_admin_id INT NULL,
    assigned_at DATETIME2 NULL,
    verified_at DATETIME2 NULL,
    CONSTRAINT FK_seller_profiles_user FOREIGN KEY (user_id) REFERENCES users(user_id),
    CONSTRAINT FK_seller_profiles_assigned_by FOREIGN KEY (assigned_by_admin_id) REFERENCES users(user_id),
    CONSTRAINT FK_seller_profiles_verified_by FOREIGN KEY (verified_by_admin_id) REFERENCES users(user_id)
);

CREATE TABLE audit_log (
    log_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    record_id VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    old_value_json NVARCHAR(MAX) NULL,
    new_value_json NVARCHAR(MAX) NULL,
    changed_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_audit_log_user FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE brands (
    brand_id INT IDENTITY(1,1) PRIMARY KEY,
    brand_name VARCHAR(255) NOT NULL UNIQUE,
    country VARCHAR(100) NULL
);

CREATE TABLE layouts (
    layout_id VARCHAR(50) PRIMARY KEY,
    layout_name VARCHAR(100) NOT NULL,
    form_factor VARCHAR(100) NOT NULL,
    standard_key_count INT NOT NULL
);

CREATE TABLE switches (
    switch_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    switch_technology VARCHAR(50) NOT NULL,
    switch_type VARCHAR(100) NOT NULL,
    actuation_force_g INT NOT NULL,
    mount_type VARCHAR(100) NOT NULL,
    sound_profile VARCHAR(100) NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_switches_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE keycap_sets (
    keycap_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    profile VARCHAR(100) NOT NULL,
    material VARCHAR(100) NOT NULL,
    color_primary VARCHAR(100) NOT NULL,
    legend_type VARCHAR(100) NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_keycap_sets_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE cases (
    case_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    material VARCHAR(100) NOT NULL,
    mount_type VARCHAR(100) NOT NULL,
    color VARCHAR(100) NOT NULL,
    weight_g INT NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_cases_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE pcbs (
    pcb_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    pcb_technology VARCHAR(50) NOT NULL,
    mount_type VARCHAR(100) NOT NULL,
    hotswap BIT NOT NULL,
    wireless BIT NOT NULL,
    rgb BIT NOT NULL,
    switch_mount VARCHAR(100) NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_pcbs_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE plates (
    plate_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    material VARCHAR(100) NOT NULL,
    mount_type VARCHAR(100) NOT NULL,
    flex_cut VARCHAR(100) NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_plates_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE stabilizers (
    stab_id VARCHAR(50) PRIMARY KEY,
    brand_id INT NOT NULL,
    stab_type VARCHAR(100) NOT NULL,
    sizes_included VARCHAR(255) NOT NULL,
    price_usd DECIMAL(10,2) NOT NULL,
    is_available BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_stabilizers_brands FOREIGN KEY (brand_id) REFERENCES brands(brand_id)
);

CREATE TABLE case_layouts (
    case_id VARCHAR(50) NOT NULL,
    layout_id VARCHAR(50) NOT NULL,
    is_primary BIT NOT NULL DEFAULT 0,
    PRIMARY KEY (case_id, layout_id),
    CONSTRAINT FK_case_layouts_cases FOREIGN KEY (case_id) REFERENCES cases(case_id),
    CONSTRAINT FK_case_layouts_layouts FOREIGN KEY (layout_id) REFERENCES layouts(layout_id)
);

CREATE TABLE pcb_layouts (
    pcb_id VARCHAR(50) NOT NULL,
    layout_id VARCHAR(50) NOT NULL,
    variant_name VARCHAR(100) NULL,
    PRIMARY KEY (pcb_id, layout_id),
    CONSTRAINT FK_pcb_layouts_pcbs FOREIGN KEY (pcb_id) REFERENCES pcbs(pcb_id),
    CONSTRAINT FK_pcb_layouts_layouts FOREIGN KEY (layout_id) REFERENCES layouts(layout_id)
);

CREATE TABLE plate_layouts (
    plate_id VARCHAR(50) NOT NULL,
    layout_id VARCHAR(50) NOT NULL,
    PRIMARY KEY (plate_id, layout_id),
    CONSTRAINT FK_plate_layouts_plates FOREIGN KEY (plate_id) REFERENCES plates(plate_id),
    CONSTRAINT FK_plate_layouts_layouts FOREIGN KEY (layout_id) REFERENCES layouts(layout_id)
);

CREATE TABLE builds (
    build_id VARCHAR(50) PRIMARY KEY,
    user_id INT NOT NULL,
    layout_id VARCHAR(50) NOT NULL,
    case_id VARCHAR(50) NULL,
    pcb_id VARCHAR(50) NULL,
    plate_id VARCHAR(50) NULL,
    switch_id VARCHAR(50) NULL,
    keycap_id VARCHAR(50) NULL,
    stab_id VARCHAR(50) NULL,
    name VARCHAR(255) NOT NULL,
    notes VARCHAR(500) NULL,
    status VARCHAR(50) NOT NULL,
    total_cost_snapshot DECIMAL(10,2) NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NULL,
    CONSTRAINT FK_builds_users FOREIGN KEY (user_id) REFERENCES users(user_id),
    CONSTRAINT FK_builds_layouts FOREIGN KEY (layout_id) REFERENCES layouts(layout_id),
    CONSTRAINT FK_builds_cases FOREIGN KEY (case_id) REFERENCES cases(case_id),
    CONSTRAINT FK_builds_pcbs FOREIGN KEY (pcb_id) REFERENCES pcbs(pcb_id),
    CONSTRAINT FK_builds_plates FOREIGN KEY (plate_id) REFERENCES plates(plate_id),
    CONSTRAINT FK_builds_switches FOREIGN KEY (switch_id) REFERENCES switches(switch_id),
    CONSTRAINT FK_builds_keycaps FOREIGN KEY (keycap_id) REFERENCES keycap_sets(keycap_id),
    CONSTRAINT FK_builds_stabilizers FOREIGN KEY (stab_id) REFERENCES stabilizers(stab_id)
);

CREATE TABLE build_mods (
    mod_id INT IDENTITY(1,1) PRIMARY KEY,
    build_id VARCHAR(50) NOT NULL,
    mod_type VARCHAR(100) NOT NULL,
    target_component VARCHAR(100) NOT NULL,
    lube_type VARCHAR(100) NULL,
    is_filmed BIT NOT NULL DEFAULT 0,
    spring_weight_g INT NULL,
    notes VARCHAR(500) NULL,
    CONSTRAINT FK_build_mods_builds FOREIGN KEY (build_id) REFERENCES builds(build_id)
);

CREATE TABLE compatibility_rules (
    rule_id INT IDENTITY(1,1) PRIMARY KEY,
    case_id VARCHAR(50) NOT NULL,
    pcb_id VARCHAR(50) NOT NULL,
    plate_id VARCHAR(50) NOT NULL,
    is_compatible BIT NOT NULL,
    notes VARCHAR(500) NULL,
    CONSTRAINT FK_compatibility_cases FOREIGN KEY (case_id) REFERENCES cases(case_id),
    CONSTRAINT FK_compatibility_pcbs FOREIGN KEY (pcb_id) REFERENCES pcbs(pcb_id),
    CONSTRAINT FK_compatibility_plates FOREIGN KEY (plate_id) REFERENCES plates(plate_id)
);

CREATE TABLE build_requests (
    request_id VARCHAR(50) PRIMARY KEY,
    build_id VARCHAR(50) NOT NULL,
    buyer_id INT NOT NULL,
    seller_user_id INT NOT NULL,
    request_payload_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(50) NOT NULL,
    note VARCHAR(500) NULL,
    requested_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    accepted_at DATETIME2 NULL,
    completed_at DATETIME2 NULL,
    updated_at DATETIME2 NULL,
    CONSTRAINT FK_build_requests_builds FOREIGN KEY (build_id) REFERENCES builds(build_id),
    CONSTRAINT FK_build_requests_buyer FOREIGN KEY (buyer_id) REFERENCES users(user_id),
    CONSTRAINT FK_build_requests_seller FOREIGN KEY (seller_user_id) REFERENCES users(user_id)
);

INSERT INTO roles (role_name, permissions)
SELECT 'Buyer', 'build:create,request:create'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Buyer');

INSERT INTO roles (role_name, permissions)
SELECT 'Seller', 'request:read,request:update'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Seller');

INSERT INTO roles (role_name, permissions)
SELECT 'Admin', 'user:manage,seller:manage,component:manage,audit:read'
WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Admin');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_builds_status')
BEGIN
    ALTER TABLE builds WITH CHECK
    ADD CONSTRAINT CK_builds_status
    CHECK (status IN ('Draft', 'Saved', 'Requested'));
END;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_build_requests_status')
BEGIN
    ALTER TABLE build_requests WITH CHECK
    ADD CONSTRAINT CK_build_requests_status
    CHECK (status IN ('Pending', 'Accepted', 'In_progress', 'Completed', 'Cancelled'));
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_users_role_id' AND object_id = OBJECT_ID('users'))
    CREATE INDEX IX_users_role_id ON users(role_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_users_is_active' AND object_id = OBJECT_ID('users'))
    CREATE INDEX IX_users_is_active ON users(is_active);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_seller_profiles_is_verified' AND object_id = OBJECT_ID('seller_profiles'))
    CREATE INDEX IX_seller_profiles_is_verified ON seller_profiles(is_verified);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_audit_log_user_changed_at' AND object_id = OBJECT_ID('audit_log'))
    CREATE INDEX IX_audit_log_user_changed_at ON audit_log(user_id, changed_at DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cases_is_available' AND object_id = OBJECT_ID('cases'))
    CREATE INDEX IX_cases_is_available ON cases(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pcbs_is_available' AND object_id = OBJECT_ID('pcbs'))
    CREATE INDEX IX_pcbs_is_available ON pcbs(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_plates_is_available' AND object_id = OBJECT_ID('plates'))
    CREATE INDEX IX_plates_is_available ON plates(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_switches_is_available' AND object_id = OBJECT_ID('switches'))
    CREATE INDEX IX_switches_is_available ON switches(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_keycap_sets_is_available' AND object_id = OBJECT_ID('keycap_sets'))
    CREATE INDEX IX_keycap_sets_is_available ON keycap_sets(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_stabilizers_is_available' AND object_id = OBJECT_ID('stabilizers'))
    CREATE INDEX IX_stabilizers_is_available ON stabilizers(is_available);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_case_layouts_layout_id' AND object_id = OBJECT_ID('case_layouts'))
    CREATE INDEX IX_case_layouts_layout_id ON case_layouts(layout_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pcb_layouts_layout_id' AND object_id = OBJECT_ID('pcb_layouts'))
    CREATE INDEX IX_pcb_layouts_layout_id ON pcb_layouts(layout_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_plate_layouts_layout_id' AND object_id = OBJECT_ID('plate_layouts'))
    CREATE INDEX IX_plate_layouts_layout_id ON plate_layouts(layout_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_builds_user_id' AND object_id = OBJECT_ID('builds'))
    CREATE INDEX IX_builds_user_id ON builds(user_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_builds_status' AND object_id = OBJECT_ID('builds'))
    CREATE INDEX IX_builds_status ON builds(status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_build_requests_buyer_status' AND object_id = OBJECT_ID('build_requests'))
    CREATE INDEX IX_build_requests_buyer_status ON build_requests(buyer_id, status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_build_requests_seller_status' AND object_id = OBJECT_ID('build_requests'))
    CREATE INDEX IX_build_requests_seller_status ON build_requests(seller_user_id, status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_build_requests_status' AND object_id = OBJECT_ID('build_requests'))
    CREATE INDEX IX_build_requests_status ON build_requests(status);
GO
