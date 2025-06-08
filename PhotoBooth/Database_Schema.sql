-- =============================================
-- Photobooth Application Database Schema
-- Comprehensive database design for all features
-- =============================================

-- =============================================
-- 1. USERS & AUTHENTICATION
-- =============================================

-- Admin users with two-level access
CREATE TABLE AdminUsers (
    UserId TEXT PRIMARY KEY, -- UUID for unique user identification
    Username TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL DEFAULT '',
    PasswordHash TEXT NOT NULL,
    AccessLevel TEXT NOT NULL CHECK (AccessLevel IN ('Master', 'User')),
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt DATETIME,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT,
    UpdatedBy TEXT,
    FOREIGN KEY (CreatedBy) REFERENCES AdminUsers(UserId),
    FOREIGN KEY (UpdatedBy) REFERENCES AdminUsers(UserId)
);

-- =============================================
-- 2. PRODUCT MANAGEMENT
-- =============================================

-- Product categories (Strips, 4x6, Smartphone Print)
CREATE TABLE ProductCategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Individual products with pricing
CREATE TABLE Products (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    Price DECIMAL(10,2) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    PhotoCount INTEGER DEFAULT 1, -- For strips: 4 photos, for 4x6: 1 photo
    MaxCopies INTEGER DEFAULT 10,
    ProductType TEXT NOT NULL DEFAULT 'PhotoStrips' CHECK (ProductType IN ('PhotoStrips', 'Photo4x6', 'SmartphonePrint')),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CategoryId) REFERENCES ProductCategories(Id)
);

-- =============================================
-- 3. TEMPLATE MANAGEMENT
-- =============================================

-- Template categories (Fun, Classic, Holiday, Seasonal, etc.)
CREATE TABLE TemplateCategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Photo templates with metadata
CREATE TABLE Templates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    CategoryId INTEGER NOT NULL,
    ProductCategoryId INTEGER NOT NULL, -- Links to Strips, 4x6, etc.
    FilePath TEXT NOT NULL,
    ThumbnailPath TEXT,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    IsSeasonal BOOLEAN NOT NULL DEFAULT 0,
    Price DECIMAL(10,2) DEFAULT 0, -- Premium templates
    SortOrder INTEGER NOT NULL DEFAULT 0,
    FileSize INTEGER, -- In bytes
    Width INTEGER,
    Height INTEGER,
    UploadedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UploadedBy TEXT,
    FOREIGN KEY (CategoryId) REFERENCES TemplateCategories(Id),
    FOREIGN KEY (ProductCategoryId) REFERENCES ProductCategories(Id),
    FOREIGN KEY (UploadedBy) REFERENCES AdminUsers(UserId)
);

-- Seasonal template scheduling
CREATE TABLE SeasonalSchedules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    StartDate TEXT NOT NULL, -- MM-DD format
    EndDate TEXT NOT NULL,   -- MM-DD format
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Link templates to seasonal schedules
CREATE TABLE TemplateSeasonalSchedules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TemplateId INTEGER NOT NULL,
    ScheduleId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TemplateId) REFERENCES Templates(Id) ON DELETE CASCADE,
    FOREIGN KEY (ScheduleId) REFERENCES SeasonalSchedules(Id) ON DELETE CASCADE,
    UNIQUE(TemplateId, ScheduleId)
);

-- =============================================
-- 4. TRANSACTIONS & SALES
-- =============================================

-- Main transaction records
CREATE TABLE Transactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionCode TEXT NOT NULL UNIQUE, -- e.g., "TRX-20241215-1234"
    ProductId INTEGER NOT NULL,
    TemplateId INTEGER, -- NULL for smartphone prints
    Quantity INTEGER NOT NULL DEFAULT 1,
    BasePrice DECIMAL(10,2) NOT NULL,
    TotalPrice DECIMAL(10,2) NOT NULL,
    PaymentMethod TEXT NOT NULL CHECK (PaymentMethod IN ('Cash', 'Credit', 'Free')),
    PaymentStatus TEXT NOT NULL DEFAULT 'Completed' CHECK (PaymentStatus IN ('Pending', 'Completed', 'Failed', 'Refunded')),
    CustomerEmail TEXT, -- For smartphone prints or receipts
    SessionId TEXT, -- Link multiple transactions in same session
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CompletedAt DATETIME,
    Notes TEXT,
    FOREIGN KEY (ProductId) REFERENCES Products(Id),
    FOREIGN KEY (TemplateId) REFERENCES Templates(Id)
);

-- Photos taken in each transaction
CREATE TABLE TransactionPhotos (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionId INTEGER NOT NULL,
    PhotoNumber INTEGER NOT NULL, -- 1, 2, 3, 4 for strips
    OriginalFilePath TEXT NOT NULL,
    ProcessedFilePath TEXT,
    IsRetake BOOLEAN NOT NULL DEFAULT 0,
    TakenAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE CASCADE
);

-- Print records for tracking supplies
CREATE TABLE PrintJobs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionId INTEGER NOT NULL,
    Copies INTEGER NOT NULL DEFAULT 1,
    PrintStatus TEXT NOT NULL DEFAULT 'Pending' CHECK (PrintStatus IN ('Pending', 'Printing', 'Completed', 'Failed')),
    PrinterName TEXT,
    StartedAt DATETIME,
    CompletedAt DATETIME,
    FailureReason TEXT,
    PrintsUsed INTEGER NOT NULL DEFAULT 1, -- For supply tracking
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id)
);

-- =============================================
-- 5. SYSTEM SETTINGS & CONFIGURATION
-- =============================================

-- Application settings storage
CREATE TABLE Settings (
    Id TEXT PRIMARY KEY,
    Category TEXT NOT NULL,
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    DataType TEXT NOT NULL DEFAULT 'String', -- 'String', 'Integer', 'Boolean', 'Decimal'
    Description TEXT,
    IsUserEditable BOOLEAN NOT NULL DEFAULT 1,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedBy TEXT,
    UNIQUE(Category, Key),
    FOREIGN KEY (UpdatedBy) REFERENCES AdminUsers(UserId)
);

-- Business/location information
CREATE TABLE BusinessInfo (
    Id TEXT PRIMARY KEY,
    BusinessName TEXT NOT NULL,
    LogoPath TEXT,
    Address TEXT,
    ShowLogoOnPrints BOOLEAN NOT NULL DEFAULT 1,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedBy TEXT,
    FOREIGN KEY (UpdatedBy) REFERENCES AdminUsers(UserId)
);

-- =============================================
-- 6. HARDWARE & SUPPLIES MANAGEMENT
-- =============================================

-- Hardware status tracking
CREATE TABLE HardwareStatus (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ComponentName TEXT NOT NULL UNIQUE,
    Status TEXT NOT NULL CHECK (Status IN ('Online', 'Offline', 'Error', 'Maintenance')),
    LastCheckAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ErrorCode TEXT,
    ErrorMessage TEXT,
    LastMaintenanceAt DATETIME
);

-- Print supply tracking
CREATE TABLE PrintSupplies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplyType TEXT NOT NULL CHECK (SupplyType IN ('Paper', 'Ink', 'Ribbon')),
    TotalCapacity INTEGER NOT NULL, -- e.g., 700 prints per roll
    CurrentCount INTEGER NOT NULL,
    LowThreshold INTEGER NOT NULL DEFAULT 100,
    CriticalThreshold INTEGER NOT NULL DEFAULT 50,
    LastRFIDDetection DATETIME,
    InstalledAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReplacedAt DATETIME,
    Notes TEXT
);

-- Supply usage history
CREATE TABLE SupplyUsageHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplyId INTEGER NOT NULL,
    UsageType TEXT NOT NULL CHECK (UsageType IN ('Print', 'Test', 'Maintenance', 'Waste')),
    Quantity INTEGER NOT NULL DEFAULT 1,
    RemainingCount INTEGER NOT NULL,
    UsedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    TransactionId INTEGER, -- Link to transaction if it was a print
    Notes TEXT,
    FOREIGN KEY (SupplyId) REFERENCES PrintSupplies(Id),
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id)
);

-- =============================================
-- 7. SYSTEM LOGS & DIAGNOSTICS
-- =============================================

-- System activity logs
CREATE TABLE SystemLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LogLevel TEXT NOT NULL CHECK (LogLevel IN ('Debug', 'Info', 'Warning', 'Error', 'Critical')),
    Category TEXT NOT NULL,
    Message TEXT NOT NULL,
    Details TEXT, -- JSON formatted additional info
    UserId TEXT, -- NULL for system events
    SessionId TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserId) REFERENCES AdminUsers(UserId)
);

-- Error tracking
CREATE TABLE SystemErrors (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ErrorCode TEXT NOT NULL,
    ErrorMessage TEXT NOT NULL,
    Component TEXT NOT NULL,
    Severity TEXT NOT NULL CHECK (Severity IN ('Low', 'Medium', 'High', 'Critical')),
    IsResolved BOOLEAN NOT NULL DEFAULT 0,
    ResolvedAt DATETIME,
    ResolvedBy TEXT,
    FirstOccurrence DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastOccurrence DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    OccurrenceCount INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (ResolvedBy) REFERENCES AdminUsers(UserId)
);

-- =============================================
-- 8. ANALYTICS & REPORTING
-- =============================================

-- Daily sales summaries for fast reporting
CREATE TABLE DailySalesSummary (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL UNIQUE, -- YYYY-MM-DD format
    TotalRevenue DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalTransactions INTEGER NOT NULL DEFAULT 0,
    StripSales INTEGER NOT NULL DEFAULT 0,
    Photo4x6Sales INTEGER NOT NULL DEFAULT 0,
    SmartphonePrintSales INTEGER NOT NULL DEFAULT 0,
    CashPayments DECIMAL(10,2) NOT NULL DEFAULT 0,
    CreditPayments DECIMAL(10,2) NOT NULL DEFAULT 0,
    FreeTransactions INTEGER NOT NULL DEFAULT 0,
    PrintsUsed INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Popular templates tracking
CREATE TABLE TemplateUsageStats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TemplateId INTEGER NOT NULL,
    UsageCount INTEGER NOT NULL DEFAULT 0,
    LastUsedAt DATETIME,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TemplateId) REFERENCES Templates(Id) ON DELETE CASCADE,
    UNIQUE(TemplateId)
);

-- =============================================
-- 9. CUSTOMER DATA (Optional)
-- =============================================

-- Customer information for smartphone prints or marketing
CREATE TABLE Customers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Email TEXT UNIQUE,
    Phone TEXT,
    Name TEXT,
    OptInMarketing BOOLEAN NOT NULL DEFAULT 0,
    FirstVisit DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastVisit DATETIME,
    TotalTransactions INTEGER NOT NULL DEFAULT 0,
    TotalSpent DECIMAL(10,2) NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Link customers to transactions
CREATE TABLE TransactionCustomers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionId INTEGER NOT NULL,
    CustomerId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE CASCADE,
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    UNIQUE(TransactionId, CustomerId)
);

-- =============================================
-- 10. INITIAL DATA SETUP
-- =============================================

-- Insert default product categories
INSERT INTO ProductCategories (Name, Description, SortOrder) VALUES
    ('Strips', '4-photo strip prints', 1),
    ('4x6', 'Single 4x6 photo prints', 2),
    ('Smartphone', 'Customer phone photo prints', 3);

-- Insert default products
INSERT INTO Products (CategoryId, Name, Description, Price, PhotoCount, ProductType) VALUES
    (1, 'Photo Strip', '4 photos in classic strip format', 5.00, 4, 'PhotoStrips'),
    (2, '4x6 Photo', 'Single high-quality 4x6 print', 3.00, 1, 'Photo4x6'),
    (3, 'Phone Print', 'Print photos from your phone', 2.00, 1, 'SmartphonePrint');

-- Insert default template categories
INSERT INTO TemplateCategories (Name, Description, SortOrder) VALUES
    ('Classic', 'Timeless template designs', 1),
    ('Fun', 'Colorful and playful templates', 2),
    ('Holiday', 'Seasonal holiday templates', 3),
    ('Elegant', 'Sophisticated template designs', 4),
    ('Premium', 'High-end template designs', 5);

-- Insert default hardware components
INSERT INTO HardwareStatus (ComponentName, Status) VALUES
    ('Camera', 'Online'),
    ('Printer', 'Offline'),
    ('Arduino', 'Online'),
    ('TouchScreen', 'Online'),
    ('RFID Reader', 'Online');

-- Insert default print supplies
INSERT INTO PrintSupplies (SupplyType, TotalCapacity, CurrentCount, LowThreshold, CriticalThreshold) VALUES
    ('Paper', 700, 650, 100, 50),
    ('Ink', 700, 650, 100, 50);

-- Default admin users are created by DatabaseService.CreateDefaultAdminUserDirect()
-- to ensure proper UUID generation and password hashing

-- Insert default business info
INSERT INTO BusinessInfo (Id, BusinessName, ShowLogoOnPrints, UpdatedAt) VALUES
    ('550e8400-e29b-41d4-a716-446655440001', 'PhotoboothX', 1, CURRENT_TIMESTAMP);

-- Default system settings are created by DatabaseService.CreateDefaultSettingsAsync()
-- to ensure proper UUID generation and avoid complex SQL UUID expressions

-- =============================================
-- USEFUL VIEWS FOR REPORTING
-- =============================================

-- Sales overview view
CREATE VIEW SalesOverview AS
SELECT 
    DATE(t.CreatedAt) as SaleDate,
    pc.Name as ProductCategory,
    COUNT(*) as TransactionCount,
    SUM(t.TotalPrice) as Revenue,
    SUM(pj.Copies) as TotalCopies,
    SUM(pj.PrintsUsed) as PrintsUsed
FROM Transactions t
JOIN Products p ON t.ProductId = p.Id
JOIN ProductCategories pc ON p.CategoryId = pc.Id
LEFT JOIN PrintJobs pj ON t.Id = pj.TransactionId
WHERE t.PaymentStatus = 'Completed'
GROUP BY DATE(t.CreatedAt), pc.Name
ORDER BY SaleDate DESC, pc.Name;

-- Popular templates view
CREATE VIEW PopularTemplates AS
SELECT 
    t.Name as TemplateName,
    tc.Name as Category,
    COUNT(tr.Id) as TimesUsed,
    SUM(tr.TotalPrice) as Revenue,
    MAX(tr.CreatedAt) as LastUsed
FROM Templates t
JOIN TemplateCategories tc ON t.CategoryId = tc.Id
LEFT JOIN Transactions tr ON t.Id = tr.TemplateId
WHERE t.IsActive = 1
GROUP BY t.Id, t.Name, tc.Name
ORDER BY TimesUsed DESC;

-- Hardware status summary
CREATE VIEW HardwareStatusSummary AS
SELECT 
    ComponentName,
    Status,
    CASE 
        WHEN Status = 'Online' THEN '🟢'
        WHEN Status = 'Offline' THEN '🔴'
        WHEN Status = 'Error' THEN '🟠'
        ELSE '🟡'
    END as StatusIcon,
    ErrorCode,
    LastCheckAt
FROM HardwareStatus
ORDER BY ComponentName; 