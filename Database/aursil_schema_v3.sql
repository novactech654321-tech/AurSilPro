-- AurSil v3: admin role, metal inventory, offers, discounts, activity log
-- Run: sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -i Database\aursil_schema_v3.sql
-- The admin account itself is created by the API on startup from the "Admin" section of appsettings.

USE aursil;
GO

IF COL_LENGTH('dbo.Users', 'Role') IS NULL
    ALTER TABLE dbo.Users ADD Role NVARCHAR(10) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'User'
        CONSTRAINT CK_Users_Role CHECK (Role IN ('User', 'Admin'));
GO

-- Grams of each metal held in the vault and available to sell to customers
IF OBJECT_ID('dbo.Inventory') IS NULL
CREATE TABLE dbo.Inventory
(
    Metal       NVARCHAR(10)  NOT NULL PRIMARY KEY,
    StockGrams  DECIMAL(18,4) NOT NULL DEFAULT 0,
    UpdatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
INSERT INTO dbo.Inventory (Metal, StockGrams)
SELECT m.Metal, 0 FROM (VALUES ('gold'), ('silver'), ('platinum')) AS m(Metal)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Inventory i WHERE i.Metal = m.Metal);
GO

-- Announcements shown on every user's home screen while active
IF OBJECT_ID('dbo.Offers') IS NULL
CREATE TABLE dbo.Offers
(
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    Title      NVARCHAR(100) NOT NULL,
    Message    NVARCHAR(500) NOT NULL,
    IsActive   BIT           NOT NULL DEFAULT 1,
    ExpiresAt  DATETIME2     NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Percent off the live buy rate, per metal
IF OBJECT_ID('dbo.Discounts') IS NULL
CREATE TABLE dbo.Discounts
(
    Metal      NVARCHAR(10) NOT NULL PRIMARY KEY,
    DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsActive   BIT          NOT NULL DEFAULT 0,
    UpdatedAt  DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Discounts_Percent CHECK (DiscountPercent >= 0 AND DiscountPercent <= 50)
);
GO
INSERT INTO dbo.Discounts (Metal, DiscountPercent, IsActive)
SELECT m.Metal, 0, 0 FROM (VALUES ('gold'), ('silver'), ('platinum')) AS m(Metal)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Discounts d WHERE d.Metal = m.Metal);
GO

IF COL_LENGTH('dbo.Orders', 'DiscountPercent') IS NULL
    ALTER TABLE dbo.Orders ADD DiscountPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_Orders_DiscountPercent DEFAULT 0;
GO

-- Every user and admin action; only admins can read it
IF OBJECT_ID('dbo.ActivityLog') IS NULL
CREATE TABLE dbo.ActivityLog
(
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId     NVARCHAR(50)  NULL,                     -- NULL for failed logins with an unknown ID
    Action     NVARCHAR(40)  NOT NULL,                 -- Login | Buy | Sell | PasswordChanged | ...
    Details    NVARCHAR(500) NULL,
    IpAddress  NVARCHAR(45)  NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_Time')
    CREATE INDEX IX_ActivityLog_Time ON dbo.ActivityLog (CreatedAt DESC);
GO
