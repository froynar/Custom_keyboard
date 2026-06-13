-- ===========================================================================
-- ApplySellerApplications.sql
-- Idempotent add of the seller_applications table to an EXISTING (already-seeded)
-- CustomKeyboard_Refactor database, without dropping/recreating other tables.
-- (CreateSchema_Refactor.sql already contains this table for clean-machine setups;
--  this apply script is for databases created before the table was introduced.)
-- ===========================================================================
SET NOCOUNT ON;

IF OBJECT_ID('seller_applications', 'U') IS NULL
BEGIN
    CREATE TABLE seller_applications (
        application_id INT IDENTITY(1,1) PRIMARY KEY,
        buyer_user_id INT NOT NULL,
        shop_name VARCHAR(255) NOT NULL,
        phone VARCHAR(30) NOT NULL,
        address VARCHAR(500) NOT NULL,
        note VARCHAR(500) NULL,
        status VARCHAR(50) NOT NULL DEFAULT 'Pending',   -- Pending, Approved, Rejected
        review_note VARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        reviewed_at DATETIME2 NULL,
        reviewed_by INT NULL,
        CONSTRAINT FK_seller_applications_buyer FOREIGN KEY (buyer_user_id) REFERENCES users(user_id),
        CONSTRAINT FK_seller_applications_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(user_id),
        CONSTRAINT CK_seller_applications_status CHECK (status IN ('Pending', 'Approved', 'Rejected'))
    );

    CREATE INDEX IX_seller_applications_status ON seller_applications(status);
    CREATE INDEX IX_seller_applications_buyer ON seller_applications(buyer_user_id);

    PRINT 'Created table seller_applications.';
END
ELSE
BEGIN
    PRINT 'Table seller_applications already exists; no change.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_seller_applications_status'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    CREATE INDEX IX_seller_applications_status ON seller_applications(status);
    PRINT 'Created index IX_seller_applications_status.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_seller_applications_buyer'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    CREATE INDEX IX_seller_applications_buyer ON seller_applications(buyer_user_id);
    PRINT 'Created index IX_seller_applications_buyer.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_seller_applications_pending_buyer'
      AND object_id = OBJECT_ID('seller_applications')
)
BEGIN
    IF EXISTS (
        SELECT buyer_user_id
        FROM seller_applications
        WHERE status = 'Pending'
        GROUP BY buyer_user_id
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51000, 'Cannot create UX_seller_applications_pending_buyer: duplicate Pending seller applications exist.', 1;
    END;

    CREATE UNIQUE INDEX UX_seller_applications_pending_buyer
    ON seller_applications(buyer_user_id)
    WHERE status = 'Pending';
    PRINT 'Created unique index UX_seller_applications_pending_buyer.';
END
GO
