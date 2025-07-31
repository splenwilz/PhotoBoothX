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
    -- Legacy extra copy pricing configuration (deprecated - use product-specific pricing below)
    UseCustomExtraCopyPricing BOOLEAN DEFAULT 0, -- If false, extra copies cost same as base price
    ExtraCopy1Price DECIMAL(10,2), -- Price for 1 extra copy (nullable, uses base price if null)
    ExtraCopy2Price DECIMAL(10,2), -- Price for 2 extra copies (nullable, uses base price if null)  
    ExtraCopy4BasePrice DECIMAL(10,2), -- Base price for 4+ extra copies (nullable, uses base price if null)
    ExtraCopyAdditionalPrice DECIMAL(10,2), -- Price per additional copy beyond 4 (nullable, uses base price if null)
    
    -- Simplified product-specific extra copy pricing configuration
    -- Photo Strips extra copy pricing
    StripsExtraCopyPrice DECIMAL(10,2), -- Price per extra strip copy
    StripsMultipleCopyDiscount DECIMAL(5,2) DEFAULT 0.00, -- Discount percentage for 2+ copies (0-100)
    
    -- 4x6 Photos extra copy pricing
    Photo4x6ExtraCopyPrice DECIMAL(10,2), -- Price per extra 4x6 copy
    Photo4x6MultipleCopyDiscount DECIMAL(5,2) DEFAULT 0.00, -- Discount percentage for 2+ copies (0-100)
    
    -- Smartphone Print extra copy pricing
    SmartphoneExtraCopyPrice DECIMAL(10,2), -- Price per extra smartphone print copy
    SmartphoneMultipleCopyDiscount DECIMAL(5,2) DEFAULT 0.00, -- Discount percentage for 2+ copies (0-100)
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CategoryId) REFERENCES ProductCategories(Id),
    UNIQUE(ProductType) -- Ensure only one product per type
);

-- =============================================
-- 3. TEMPLATE MANAGEMENT
-- =============================================

-- Template categories (Fun, Classic, Holiday, Seasonal, etc.)
-- Note: Seasonal dates use TEXT with CHECK constraints for MM-DD format validation
-- This provides good performance for year-agnostic seasonal comparisons while ensuring data integrity
CREATE TABLE TemplateCategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    IsPremium BOOLEAN NOT NULL DEFAULT 0, -- Premium badge determination
    SortOrder INTEGER NOT NULL DEFAULT 0,
    -- Seasonal functionality
    IsSeasonalCategory BOOLEAN NOT NULL DEFAULT 0,
    SeasonStartDate TEXT CHECK (SeasonStartDate IS NULL OR SeasonStartDate GLOB '[0-1][0-9]-[0-3][0-9]'), -- MM-DD format (e.g., "02-01" for Valentine's) with validation
    SeasonEndDate TEXT CHECK (SeasonEndDate IS NULL OR SeasonEndDate GLOB '[0-1][0-9]-[0-3][0-9]'),   -- MM-DD format (e.g., "02-20") with validation
    SeasonalPriority INTEGER NOT NULL DEFAULT 0, -- Higher numbers appear first during season
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Template layout definitions (predefined layouts with photo positions)
CREATE TABLE TemplateLayouts (
    Id TEXT PRIMARY KEY, -- UUID e.g., '550e8400-e29b-41d4-a716-446655440001'
    LayoutKey TEXT NOT NULL UNIQUE, -- e.g., 'strip-614x1864', 'strip-591x1772' (for backward compatibility)
    Name TEXT NOT NULL, -- e.g., 'Classic Photo Strip', 'Compact Strip'
    Description TEXT,
    Width INTEGER NOT NULL,
    Height INTEGER NOT NULL,
    PhotoCount INTEGER NOT NULL,
    ProductCategoryId INTEGER NOT NULL, -- Links to Strips, 4x6, etc.
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ProductCategoryId) REFERENCES ProductCategories(Id)
);

-- Photo area definitions for each layout
CREATE TABLE TemplatePhotoAreas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LayoutId TEXT NOT NULL,
    PhotoIndex INTEGER NOT NULL, -- 1, 2, 3, 4 for strips
    X INTEGER NOT NULL,
    Y INTEGER NOT NULL,
    Width INTEGER NOT NULL,
    Height INTEGER NOT NULL,
    Rotation REAL DEFAULT 0, -- Rotation in degrees
    FOREIGN KEY (LayoutId) REFERENCES TemplateLayouts(Id) ON DELETE CASCADE,
    UNIQUE(LayoutId, PhotoIndex)
);

-- Photo templates with metadata (simplified, layout-based)
CREATE TABLE Templates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    CategoryId INTEGER NOT NULL,
    LayoutId TEXT NOT NULL, -- Links to TemplateLayouts
    FolderPath TEXT NOT NULL UNIQUE, -- Path to template folder
    TemplatePath TEXT NOT NULL, -- Path to template.png
    PreviewPath TEXT NOT NULL, -- Path to preview image
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    IsSeasonal BOOLEAN NOT NULL DEFAULT 0,
    Price DECIMAL(10,2) DEFAULT 0, -- Template pricing: Strip=$5.00, 4x6=$3.00 (set by application based on TemplateType)
    SortOrder INTEGER NOT NULL DEFAULT 0,
    FileSize INTEGER DEFAULT 0, -- In bytes
    Description TEXT DEFAULT '', -- Template description
    TemplateType INTEGER NOT NULL DEFAULT 0 CHECK (TemplateType IN (0, 1)), -- 0 = Strip, 1 = Photo4x6
    UploadedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UploadedBy TEXT,
    FOREIGN KEY (CategoryId) REFERENCES TemplateCategories(Id),
    FOREIGN KEY (LayoutId) REFERENCES TemplateLayouts(Id),
    FOREIGN KEY (UploadedBy) REFERENCES AdminUsers(UserId)
);

-- Seasonal template scheduling
CREATE TABLE SeasonalSchedules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    StartDate TEXT NOT NULL CHECK (StartDate GLOB '[0-1][0-9]-[0-3][0-9]'), -- MM-DD format with validation
    EndDate TEXT NOT NULL CHECK (EndDate GLOB '[0-1][0-9]-[0-3][0-9]'),   -- MM-DD format with validation
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

-- Credit transaction history for tracking credit additions/deductions
CREATE TABLE CreditTransactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Amount DECIMAL(10,2) NOT NULL, -- Positive for additions, negative for deductions
    TransactionType TEXT NOT NULL CHECK (TransactionType IN ('Add', 'Deduct', 'Reset')),
    Description TEXT NOT NULL,
    BalanceAfter DECIMAL(10,2) NOT NULL, -- Credit balance after this transaction
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT, -- Admin user who performed the action (for manual additions)
    RelatedTransactionId INTEGER, -- Link to main transaction if this was from a purchase
    FOREIGN KEY (CreatedBy) REFERENCES AdminUsers(UserId),
    FOREIGN KEY (RelatedTransactionId) REFERENCES Transactions(Id)
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

-- Note: System logging is handled by file-based logging system (Serilog)
-- Log files are stored in AppData/Roaming/PhotoBoothX/Logs/ with automatic rotation
-- Categories: application-*.log, hardware-*.log, transactions-*.log, errors-*.log, performance-*.log

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
-- NOTE: During development, all schema changes are made directly to this file.
-- Migrations will only be implemented after v1.0 release for production updates.

-- Insert default product categories
INSERT INTO ProductCategories (Name, Description, SortOrder) VALUES
    ('Strips', '4-photo strip prints', 1),
    ('4x6', 'Single 4x6 photo prints', 2),
    ('Smartphone', 'Customer phone photo prints', 3);

-- Insert default products only if they don't exist (by default, extra copies cost same as base price)
INSERT OR IGNORE INTO Products (CategoryId, Name, Description, Price, PhotoCount, ProductType, UseCustomExtraCopyPricing, ExtraCopy1Price, ExtraCopy2Price, ExtraCopy4BasePrice, ExtraCopyAdditionalPrice) VALUES
    (1, 'Photo Strip', '4 photos in classic strip format', 5.00, 4, 'PhotoStrips', 0, NULL, NULL, NULL, NULL),
    (2, '4x6 Photo', 'Single high-quality 4x6 print', 3.00, 1, 'Photo4x6', 0, NULL, NULL, NULL, NULL),
    (3, 'Phone Print', 'Print photos from your phone', 2.00, 1, 'SmartphonePrint', 0, NULL, NULL, NULL, NULL);

-- Insert default template categories
INSERT INTO TemplateCategories (Name, Description, SortOrder, IsSeasonalCategory, SeasonStartDate, SeasonEndDate, SeasonalPriority) VALUES
    ('Classic', 'Timeless template designs', 1, 0, NULL, NULL, 0),
    ('Fun', 'Colorful and playful templates', 2, 0, NULL, NULL, 0),
    ('Elegant', 'Sophisticated template designs', 3, 0, NULL, NULL, 0),
    ('Premium', 'High-end template designs', 4, 0, NULL, NULL, 0),
    -- Seasonal Categories (ordered chronologically through the year)
    ('New Year', 'New Year celebration and party templates', 10, 1, '12-26', '01-07', 95),
    ('Valentine''s Day', 'Love and romance themed templates', 11, 1, '02-01', '02-15', 100),
    ('Mardi Gras', 'Carnival and Mardi Gras celebration templates', 12, 1, '02-12', '02-20', 85),
    ('St Patrick''s Day', 'Irish celebration and green themed templates', 13, 1, '03-10', '03-25', 80),
    ('Easter', 'Spring and Easter celebration templates', 14, 1, '03-15', '04-15', 90),
    ('Cinco De Mayo', 'Mexican celebration and fiesta templates', 15, 1, '05-01', '05-10', 75),
    ('Pride Day', 'Pride celebration and rainbow themed templates', 16, 1, '06-20', '06-30', 85),
    ('Fourth of July', 'Independence Day and patriotic templates', 17, 1, '06-25', '07-05', 90),
    ('Summer', 'Bright and sunny summer templates', 18, 1, '06-01', '08-31', 70),
    ('Halloween', 'Spooky and fun Halloween templates', 20, 1, '10-01', '10-31', 85),
    ('Thanksgiving', 'Thanksgiving and autumn harvest templates', 21, 1, '11-01', '11-30', 80),
    ('Christmas', 'Holiday and winter celebration templates', 22, 1, '12-01', '12-26', 95);

-- Insert template layouts (predefined photo area configurations)
INSERT INTO TemplateLayouts (Id, LayoutKey, Name, Description, Width, Height, PhotoCount, ProductCategoryId, SortOrder) VALUES
    ('550e8400-e29b-41d4-a716-446655440001', 'strip-591x1772', 'Compact Photo Strip', 'Compact 4-photo vertical strip layout', 591, 1772, 4, 1, 2),
    ('550e8400-e29b-41d4-a716-446655440002', 'strip-591x1772b', 'Compact Photo Strip', 'Compact 4-photo vertical strip layout', 591, 1772, 4, 1, 2),
    ('550e8400-e29b-41d4-a716-446655440003', '4x6-1864x1228', 'Standard 4x6', 'Single photo 4x6 print layout', 1864, 1228, 1, 2, 1),
    ('550e8400-e29b-41d4-a716-446655440004', 'strip-1080x1920', 'Compact Photo Strip', 'Compact 3-photo vertical strip layout', 1080, 1920, 3, 1, 2),
    ('550e8400-e29b-41d4-a716-446655440005', 'strip-707x2000', 'Compact Photo Strip', 'Compact 4-photo vertical strip layout', 707, 2000, 4, 1, 2);

-- strip-591x1772 layout (4 photos in vertical strip - compact)
INSERT INTO TemplatePhotoAreas (LayoutId, PhotoIndex, X, Y, Width, Height, Rotation) VALUES
    ('550e8400-e29b-41d4-a716-446655440001', 1, 61, 130, 473, 354, 0),
    ('550e8400-e29b-41d4-a716-446655440001', 2, 61, 515, 473, 354, 0),
    ('550e8400-e29b-41d4-a716-446655440001', 3, 61, 903, 473, 354, 0),
    ('550e8400-e29b-41d4-a716-446655440001', 4, 61, 1290, 473, 354, 0);

-- strip-591x1772b layout (4 photos in vertical strip - compact)
INSERT INTO TemplatePhotoAreas (LayoutId, PhotoIndex, X, Y, Width, Height, Rotation) VALUES
    ('550e8400-e29b-41d4-a716-446655440002', 1, 72, 60, 451, 326, 0),
    ('550e8400-e29b-41d4-a716-446655440002', 2, 72, 443, 451, 326, 0),
    ('550e8400-e29b-41d4-a716-446655440002', 3, 72, 824, 451, 326, 0),
    ('550e8400-e29b-41d4-a716-446655440002', 4, 72, 1204, 451, 326, 0);

-- 4x6-1864x1228 layout (single photo)
INSERT INTO TemplatePhotoAreas (LayoutId, PhotoIndex, X, Y, Width, Height, Rotation) VALUES
    ('550e8400-e29b-41d4-a716-446655440003', 1, 433, 200, 952, 715, 0);

-- strip-1080x1920 layout (3 photos in vertical strip)
INSERT INTO TemplatePhotoAreas (LayoutId, PhotoIndex, X, Y, Width, Height, Rotation) VALUES
    ('550e8400-e29b-41d4-a716-446655440004', 1, 249, 5, 582, 459, 0),
    ('550e8400-e29b-41d4-a716-446655440004', 2, 249, 492, 582, 459, 0),
    ('550e8400-e29b-41d4-a716-446655440004', 3, 249, 981, 582, 459, 0);

-- strip-707x2000 layout (4 photos in vertical strip - compact)
INSERT INTO TemplatePhotoAreas (LayoutId, PhotoIndex, X, Y, Width, Height, Rotation) VALUES
    ('550e8400-e29b-41d4-a716-446655440005', 1, 95, 95, 523, 319, 356.945), --3.055
    ('550e8400-e29b-41d4-a716-446655440005', 2, 101, 497, 523, 319, 1.056), --358.948
    ('550e8400-e29b-41d4-a716-446655440005', 3, 104, 895, 523, 319, 356.955), --3.045
    ('550e8400-e29b-41d4-a716-446655440005', 4, 89, 1291, 533, 329, 1.786); --358.214


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