USE CustomKeyboardBuilder;
GO

SET NOCOUNT ON;

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
