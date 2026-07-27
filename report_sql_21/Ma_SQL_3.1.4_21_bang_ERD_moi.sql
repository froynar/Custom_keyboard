-- Custom Keyboard Builder
-- Mã SQL 3.1.4: 21 bảng nghiệp vụ theo ERD mới
-- Thứ tự đã bảo đảm phụ thuộc khóa ngoại.

-- Mã SQL 3.1.4.1 - Tạo bảng roles
CREATE TABLE roles (
    id           INT           IDENTITY(1,1) PRIMARY KEY,
    role_name    VARCHAR(50)   NOT NULL UNIQUE,
    permissions  VARCHAR(MAX)  NULL
);

-- Mã SQL 3.1.4.2 - Tạo bảng users
CREATE TABLE users (
    id             INT           IDENTITY(1,1) PRIMARY KEY,
    role_id        INT           NOT NULL,
    username       VARCHAR(100)  NOT NULL UNIQUE,
    email          VARCHAR(255)  NOT NULL UNIQUE,
    phone          VARCHAR(30)   NOT NULL UNIQUE,
    password_hash  VARCHAR(255)  NOT NULL,
    is_active      BIT           NOT NULL DEFAULT 1,
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES roles(id)
);

CREATE INDEX IX_users_role_id
    ON users(role_id);

CREATE INDEX IX_users_is_active
    ON users(is_active);

-- Mã SQL 3.1.4.3 - Tạo bảng seller_profiles
CREATE TABLE seller_profiles (
    id           INT           IDENTITY(1,1) PRIMARY KEY,
    user_id      INT           NOT NULL UNIQUE,
    shop_name    VARCHAR(255)  NOT NULL,
    phone        VARCHAR(30)   NOT NULL,
    address      VARCHAR(500)  NOT NULL,
    is_verified  BIT           NOT NULL DEFAULT 0,
    verified_at  DATETIME2     NULL,
    CONSTRAINT FK_seller_profiles_user FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX IX_seller_profiles_is_verified
    ON seller_profiles(is_verified);

-- Mã SQL 3.1.4.4 - Tạo bảng seller_applications
CREATE TABLE seller_applications (
    id             INT           IDENTITY(1,1) PRIMARY KEY,
    buyer_user_id  INT           NOT NULL,
    shop_name      VARCHAR(255)  NOT NULL,
    phone          VARCHAR(30)   NOT NULL,
    address        VARCHAR(500)  NOT NULL,
    note           VARCHAR(500)  NULL,
    status         VARCHAR(50)   NOT NULL DEFAULT 'Pending',
    review_note    VARCHAR(500)  NULL,
    created_at     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    reviewed_at    DATETIME2     NULL,
    reviewed_by    INT           NULL,
    CONSTRAINT FK_seller_applications_buyer FOREIGN KEY (buyer_user_id) REFERENCES users(id),
    CONSTRAINT FK_seller_applications_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(id),
    CONSTRAINT CK_seller_applications_status CHECK (status IN ('Pending', 'Approved', 'Rejected'))
);

CREATE INDEX IX_seller_applications_status
    ON seller_applications(status);

CREATE INDEX IX_seller_applications_buyer
    ON seller_applications(buyer_user_id);

CREATE UNIQUE INDEX UX_seller_applications_pending_buyer
    ON seller_applications(buyer_user_id)
    WHERE status = 'Pending';

-- Mã SQL 3.1.4.5 - Tạo bảng brands
CREATE TABLE brands (
    id          INT           IDENTITY(1,1) PRIMARY KEY,
    brand_name  VARCHAR(255)  NOT NULL UNIQUE,
    country     VARCHAR(100)  NULL
);

-- Mã SQL 3.1.4.6 - Tạo bảng layouts
CREATE TABLE layouts (
    id           VARCHAR(50)   PRIMARY KEY,
    layout_name  VARCHAR(100)  NOT NULL,
    form_factor  VARCHAR(100)  NOT NULL,
    key_count    INT           NOT NULL
);

-- Mã SQL 3.1.4.7 - Tạo bảng keyboard_kits
CREATE TABLE keyboard_kits (
    id                        VARCHAR(50)    PRIMARY KEY,
    brand_id                  INT            NOT NULL,
    layout_id                 VARCHAR(50)    NOT NULL,
    kit_name                  VARCHAR(255)   NOT NULL,
    pcb_technology            VARCHAR(50)    NOT NULL,
    switch_mount              VARCHAR(100)   NOT NULL,
    required_switch_quantity  INT            NOT NULL,
    included_parts            VARCHAR(500)   NULL,
    price_usd                 DECIMAL(10,2)  NOT NULL,
    is_available              BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_keyboard_kits_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT FK_keyboard_kits_layouts FOREIGN KEY (layout_id) REFERENCES layouts(id),
    CONSTRAINT CK_keyboard_kits_price CHECK (price_usd >= 0),
    CONSTRAINT CK_keyboard_kits_switch_qty CHECK (required_switch_quantity > 0)
);

CREATE INDEX IX_keyboard_kits_brand_id
    ON keyboard_kits(brand_id);

CREATE INDEX IX_keyboard_kits_layout_id
    ON keyboard_kits(layout_id);

CREATE INDEX IX_keyboard_kits_is_available
    ON keyboard_kits(is_available);

-- Mã SQL 3.1.4.8 - Tạo bảng switches
CREATE TABLE switches (
    id                 VARCHAR(50)    PRIMARY KEY,
    brand_id           INT            NOT NULL,
    switch_name        VARCHAR(255)   NOT NULL,
    switch_technology  VARCHAR(50)    NOT NULL,
    mount_type         VARCHAR(100)   NOT NULL,
    switch_type        VARCHAR(100)   NULL,
    actuation_force_g  INT            NULL,
    price_usd          DECIMAL(10,2)  NOT NULL,
    is_available       BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_switches_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_switches_price CHECK (price_usd >= 0)
);

CREATE INDEX IX_switches_brand_id
    ON switches(brand_id);

CREATE INDEX IX_switches_is_available
    ON switches(is_available);

-- Mã SQL 3.1.4.9 - Tạo bảng keycap_sets
CREATE TABLE keycap_sets (
    id                     VARCHAR(50)    PRIMARY KEY,
    brand_id               INT            NOT NULL,
    keycap_name            VARCHAR(255)   NOT NULL,
    supported_form_factor  VARCHAR(255)   NOT NULL,
    profile                VARCHAR(100)   NULL,
    material               VARCHAR(100)   NULL,
    price_usd              DECIMAL(10,2)  NOT NULL,
    is_available           BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_keycap_sets_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_keycap_sets_price CHECK (price_usd >= 0)
);

CREATE INDEX IX_keycap_sets_brand_id
    ON keycap_sets(brand_id);

CREATE INDEX IX_keycap_sets_is_available
    ON keycap_sets(is_available);

-- Mã SQL 3.1.4.10 - Tạo bảng stabilizers
CREATE TABLE stabilizers (
    id                 VARCHAR(50)    PRIMARY KEY,
    brand_id           INT            NOT NULL,
    stab_name          VARCHAR(255)   NOT NULL,
    supported_layouts  VARCHAR(255)   NOT NULL,
    price_usd          DECIMAL(10,2)  NOT NULL,
    is_available       BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_stabilizers_brands FOREIGN KEY (brand_id) REFERENCES brands(id),
    CONSTRAINT CK_stabilizers_price CHECK (price_usd >= 0)
);

CREATE INDEX IX_stabilizers_brand_id
    ON stabilizers(brand_id);

CREATE INDEX IX_stabilizers_is_available
    ON stabilizers(is_available);

-- Mã SQL 3.1.4.11 - Tạo bảng accessories
CREATE TABLE accessories (
    id                VARCHAR(50)    PRIMARY KEY,
    accessory_type    VARCHAR(100)   NOT NULL,
    accessory_name    VARCHAR(255)   NOT NULL,
    target_component  VARCHAR(100)   NULL,
    price_usd         DECIMAL(10,2)  NOT NULL,
    is_available      BIT            NOT NULL DEFAULT 1,
    CONSTRAINT CK_accessories_price CHECK (price_usd >= 0)
);

CREATE INDEX IX_accessories_is_available
    ON accessories(is_available);

-- Mã SQL 3.1.4.12 - Tạo bảng builds
CREATE TABLE builds (
    id                   VARCHAR(50)    PRIMARY KEY,
    buyer_id             INT            NOT NULL,
    kit_id               VARCHAR(50)    NOT NULL,
    name                 NVARCHAR(255)  NOT NULL,
    notes                NVARCHAR(500)  NULL,
    noise_requirement    VARCHAR(20)    NOT NULL DEFAULT 'Normal',
    status               VARCHAR(50)    NOT NULL,
    total_cost_snapshot  DECIMAL(10,2)  NOT NULL,
    created_at           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at           DATETIME2      NULL,
    CONSTRAINT FK_builds_buyer FOREIGN KEY (buyer_id) REFERENCES users(id),
    CONSTRAINT FK_builds_kit FOREIGN KEY (kit_id) REFERENCES keyboard_kits(id),
    CONSTRAINT CK_builds_noise_requirement CHECK (noise_requirement IN ('Normal','Quiet','Silent')),
    CONSTRAINT CK_builds_status CHECK (status IN ('Draft', 'Saved', 'Requested', 'Archived')),
    CONSTRAINT CK_builds_total CHECK (total_cost_snapshot >= 0),
    CONSTRAINT CK_builds_name_not_blank CHECK (LEN(LTRIM(RTRIM(name))) > 0)
);

CREATE INDEX IX_builds_buyer_id
    ON builds(buyer_id);

CREATE INDEX IX_builds_kit_id
    ON builds(kit_id);

CREATE INDEX IX_builds_status
    ON builds(status);

-- Mã SQL 3.1.4.13 - Tạo bảng build_items
CREATE TABLE build_items (
    id                   INT            IDENTITY(1,1) PRIMARY KEY,
    build_id             VARCHAR(50)    NOT NULL,
    switch_id            VARCHAR(50)    NULL,
    keycap_id            VARCHAR(50)    NULL,
    stab_id              VARCHAR(50)    NULL,
    accessory_id         VARCHAR(50)    NULL,
    quantity             INT            NOT NULL,
    unit_price_snapshot  DECIMAL(10,2)  NOT NULL,
    notes                NVARCHAR(500)  NULL,
    CONSTRAINT FK_build_items_build FOREIGN KEY (build_id) REFERENCES builds(id),
    CONSTRAINT FK_build_items_switch FOREIGN KEY (switch_id) REFERENCES switches(id),
    CONSTRAINT FK_build_items_keycap FOREIGN KEY (keycap_id) REFERENCES keycap_sets(id),
    CONSTRAINT FK_build_items_stab FOREIGN KEY (stab_id) REFERENCES stabilizers(id),
    CONSTRAINT FK_build_items_accessory FOREIGN KEY (accessory_id) REFERENCES accessories(id),
    CONSTRAINT CK_build_items_quantity CHECK (quantity > 0),
    CONSTRAINT CK_build_items_unit_price CHECK (unit_price_snapshot >= 0),
    CONSTRAINT CK_build_items_exactly_one_fk CHECK (
        (CASE WHEN switch_id    IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN keycap_id    IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN stab_id      IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) = 1
    )
);

CREATE INDEX IX_build_items_build_id
    ON build_items(build_id);

CREATE INDEX IX_build_items_switch_id
    ON build_items(switch_id);

CREATE INDEX IX_build_items_keycap_id
    ON build_items(keycap_id);

CREATE INDEX IX_build_items_stab_id
    ON build_items(stab_id);

CREATE INDEX IX_build_items_accessory_id
    ON build_items(accessory_id);

-- Mã SQL 3.1.4.14 - Tạo bảng build_mods
CREATE TABLE build_mods (
    id                INT            IDENTITY(1,1) PRIMARY KEY,
    build_id          VARCHAR(50)    NOT NULL,
    mod_type          NVARCHAR(100)  NOT NULL,
    target_component  NVARCHAR(100)  NOT NULL,
    notes             NVARCHAR(500)  NULL,
    CONSTRAINT FK_build_mods_build FOREIGN KEY (build_id) REFERENCES builds(id)
);

CREATE INDEX IX_build_mods_build_id
    ON build_mods(build_id);

-- Mã SQL 3.1.4.15 - Tạo bảng build_requests
CREATE TABLE build_requests (
    id                    VARCHAR(50)    PRIMARY KEY,
    build_id              VARCHAR(50)    NOT NULL,
    seller_user_id        INT            NOT NULL,
    request_payload_json  NVARCHAR(MAX)  NOT NULL,
    status                VARCHAR(50)    NOT NULL,
    note                  NVARCHAR(500)  NULL,
    requested_at          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    accepted_at           DATETIME2      NULL,
    completed_at          DATETIME2      NULL,
    updated_at            DATETIME2      NULL,
    CONSTRAINT FK_build_requests_build FOREIGN KEY (build_id) REFERENCES builds(id),
    CONSTRAINT FK_build_requests_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT CK_build_requests_status CHECK (status IN ('Pending', 'Accepted', 'In_progress', 'Completed', 'Cancelled'))
);

CREATE INDEX IX_build_requests_build_id
    ON build_requests(build_id);

CREATE INDEX IX_build_requests_seller_status
    ON build_requests(seller_user_id, status);

CREATE INDEX IX_build_requests_status
    ON build_requests(status);

CREATE UNIQUE INDEX UX_build_requests_one_active_per_build
    ON build_requests(build_id)
    WHERE status IN ('Pending', 'Accepted', 'In_progress');

-- Mã SQL 3.1.4.16 - Tạo bảng devices
CREATE TABLE devices (
    id              VARCHAR(50)    PRIMARY KEY,
    seller_user_id  INT            NOT NULL,
    device_name     NVARCHAR(100)  NOT NULL,
    device_type     VARCHAR(50)    NOT NULL,
    is_active       BIT            NOT NULL DEFAULT 1,
    last_seen_at    DATETIME2      NULL,
    created_at      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_devices_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT CK_devices_type CHECK (device_type IN ('QC_STATION','KEY_SIGNAL_TESTER','LATENCY_TESTER','NOISE_SENSOR')),
    CONSTRAINT CK_devices_name_not_blank CHECK (LEN(LTRIM(RTRIM(device_name))) > 0)
);

CREATE INDEX IX_devices_seller
    ON devices(seller_user_id);

CREATE UNIQUE INDEX UX_devices_one_active_qc_station_per_seller
    ON devices(seller_user_id)
    WHERE is_active = 1 AND device_type = 'QC_STATION';

-- Mã SQL 3.1.4.17 - Tạo bảng device_test_sessions
CREATE TABLE device_test_sessions (
    id                 VARCHAR(50)  PRIMARY KEY,
    request_id         VARCHAR(50)  NOT NULL,
    device_id          VARCHAR(50)  NOT NULL,
    switch_technology  VARCHAR(50)  NOT NULL,
    noise_requirement  VARCHAR(20)  NOT NULL DEFAULT 'Normal',
    total_keys         INT          NOT NULL,
    status             VARCHAR(20)  NOT NULL,
    completed_at       DATETIME2    NULL,
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

CREATE INDEX IX_dts_request
    ON device_test_sessions(request_id);

CREATE INDEX IX_dts_device
    ON device_test_sessions(device_id);

CREATE UNIQUE INDEX UX_dts_one_running_per_request
    ON device_test_sessions(request_id)
    WHERE status = 'Running';

-- Mã SQL 3.1.4.18 - Tạo bảng device_key_test_results
CREATE TABLE device_key_test_results (
    id                     BIGINT        IDENTITY(1,1) PRIMARY KEY,
    session_id             VARCHAR(50)   NOT NULL,
    key_code               VARCHAR(30)   NOT NULL,
    received_key           VARCHAR(30)   NULL,
    press_signal_detected  BIT           NOT NULL,
    latency                DECIMAL(8,2)  NULL,
    press_count            INT           NOT NULL,
    release_signal         BIT           NOT NULL,
    hold_duration          INT           NULL,
    noise                  DECIMAL(8,2)  NULL,
    result                 VARCHAR(20)   NOT NULL,
    recorded_at            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_dktr_session FOREIGN KEY (session_id) REFERENCES device_test_sessions(id),
    CONSTRAINT CK_dktr_result CHECK (result IN ('Pass','Warning','Fail')),
    CONSTRAINT CK_dktr_press_count CHECK (press_count BETWEEN 0 AND 100),
    CONSTRAINT CK_dktr_latency CHECK (latency IS NULL OR latency BETWEEN 0 AND 10000),
    CONSTRAINT CK_dktr_hold_duration CHECK (hold_duration IS NULL OR hold_duration BETWEEN 0 AND 3600000),
    CONSTRAINT CK_dktr_noise CHECK (noise IS NULL OR noise BETWEEN 0 AND 200),
    CONSTRAINT CK_dktr_key_code_not_blank CHECK (LEN(LTRIM(RTRIM(key_code))) > 0)
);

CREATE INDEX IX_dktr_session
    ON device_key_test_results(session_id);

CREATE UNIQUE INDEX UX_dktr_session_key_code
    ON device_key_test_results(session_id, key_code);

-- Mã SQL 3.1.4.19 - Tạo bảng audit_log
CREATE TABLE audit_log (
    id              INT            IDENTITY(1,1) PRIMARY KEY,
    user_id         INT            NOT NULL,
    table_name      VARCHAR(100)   NOT NULL,
    record_id       VARCHAR(100)   NOT NULL,
    action          VARCHAR(100)   NOT NULL,
    old_value_json  NVARCHAR(MAX)  NULL,
    new_value_json  NVARCHAR(MAX)  NULL,
    changed_at      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_audit_log_user FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX IX_audit_log_user_changed_at
    ON audit_log(user_id, changed_at DESC);

-- Mã SQL 3.1.4.20 - Tạo bảng chat_conversations
CREATE TABLE chat_conversations (
    id                VARCHAR(50)  PRIMARY KEY,
    seller_user_id    INT          NOT NULL,
    buyer_id          INT          NULL,
    admin_user_id     INT          NULL,
    build_request_id  VARCHAR(50)  NULL,
    created_at        DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at        DATETIME2    NULL,
    CONSTRAINT FK_chat_conversations_seller FOREIGN KEY (seller_user_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_buyer FOREIGN KEY (buyer_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_admin FOREIGN KEY (admin_user_id) REFERENCES users(id),
    CONSTRAINT FK_chat_conversations_request FOREIGN KEY (build_request_id) REFERENCES build_requests(id),
    CONSTRAINT CK_chat_conversations_participant CHECK (
        (CASE WHEN buyer_id      IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) = 1
    )
);

CREATE INDEX IX_chat_conversations_seller
    ON chat_conversations(seller_user_id);

CREATE INDEX IX_chat_conversations_buyer
    ON chat_conversations(buyer_id);

CREATE INDEX IX_chat_conversations_admin
    ON chat_conversations(admin_user_id);

CREATE INDEX IX_chat_conversations_request
    ON chat_conversations(build_request_id);

-- Mã SQL 3.1.4.21 - Tạo bảng chat_messages
CREATE TABLE chat_messages (
    id               VARCHAR(50)    PRIMARY KEY,
    conversation_id  VARCHAR(50)    NOT NULL,
    sender_user_id   INT            NOT NULL,
    message_text     NVARCHAR(MAX)  NOT NULL,
    sent_at          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_chat_messages_conversation FOREIGN KEY (conversation_id) REFERENCES chat_conversations(id),
    CONSTRAINT FK_chat_messages_sender FOREIGN KEY (sender_user_id) REFERENCES users(id)
);

CREATE INDEX IX_chat_messages_conversation
    ON chat_messages(conversation_id);

CREATE INDEX IX_chat_messages_sender
    ON chat_messages(sender_user_id);
