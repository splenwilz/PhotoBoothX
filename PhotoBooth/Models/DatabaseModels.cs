// =============================================
// CONSOLIDATED DATABASE MODELS
// For backwards compatibility - re-exports all domain-based models
// =============================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Photobooth.Models
{
    // =============================================
    // AUTHENTICATION MODELS
    // =============================================
    
    public enum AdminAccessLevel
    {
        None,
        User,
        Master
    }

    public class AdminUser
    {
        public string UserId { get; set; } = Guid.NewGuid().ToString(); // Primary key - UUID for unique identification
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public AdminAccessLevel AccessLevel { get; set; } = AdminAccessLevel.User;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    // =============================================
    // PRODUCT MODELS
    // =============================================

    public enum ProductType
    {
        PhotoStrips,
        Photo4x6,
        SmartphonePrint
    }

    public class ProductCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Product
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public int PhotoCount { get; set; } = 1;
        public int MaxCopies { get; set; } = 10;
        public ProductType ProductType { get; set; } = ProductType.PhotoStrips;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ProductCategory? Category { get; set; }
    }

    // =============================================
    // TEMPLATE MODELS
    // =============================================

    public class TemplateCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        
        // Seasonal functionality
        public bool IsSeasonalCategory { get; set; } = false;
        public string? SeasonStartDate { get; set; } // MM-DD format (e.g., "02-01")
        public string? SeasonEndDate { get; set; }   // MM-DD format (e.g., "02-20")
        public int SeasonalPriority { get; set; } = 0; // Higher numbers appear first during season
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Validation helper methods
        public static bool IsValidSeasonalDate(string? date)
        {
            if (string.IsNullOrEmpty(date))
                return true; // NULL/empty is valid
                
            // Check MM-DD format using regex
            return System.Text.RegularExpressions.Regex.IsMatch(date, @"^(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
        }
        
        public static string? ValidateAndFormatSeasonalDate(string? date)
        {
            if (string.IsNullOrEmpty(date))
                return null;
                
            // Remove any extra spaces
            date = date.Trim();
            
            // If it's already in correct format, return it
            if (IsValidSeasonalDate(date))
                return date;
                
            // Try to parse and reformat common variations
            if (DateTime.TryParse($"2000-{date}", out var parsedDate))
            {
                return $"{parsedDate.Month:D2}-{parsedDate.Day:D2}";
            }
            
            throw new ArgumentException($"Invalid seasonal date format: '{date}'. Expected MM-DD format (e.g., '02-14')");
        }
        
        // Computed properties for seasonal logic
        public bool IsCurrentlyInSeason
        {
            get
            {
                if (!IsSeasonalCategory || string.IsNullOrEmpty(SeasonStartDate) || string.IsNullOrEmpty(SeasonEndDate))
                    return false;
                    
                var today = DateTime.Now;
                var currentDate = $"{today.Month:D2}-{today.Day:D2}";
                
                // Handle cross-year seasons (e.g., Christmas: 12-01 to 01-15)
                if (string.Compare(SeasonStartDate, SeasonEndDate) > 0)
                {
                    return string.Compare(currentDate, SeasonStartDate) >= 0 || string.Compare(currentDate, SeasonEndDate) <= 0;
                }
                else
                {
                    return string.Compare(currentDate, SeasonStartDate) >= 0 && string.Compare(currentDate, SeasonEndDate) <= 0;
                }
            }
        }
    }

    public class TemplateLayout
    {
        public string Id { get; set; } = string.Empty; // UUID e.g., '550e8400-e29b-41d4-a716-446655440001'
        public string LayoutKey { get; set; } = string.Empty; // e.g., 'strip-614x1864' (for backward compatibility)
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PhotoCount { get; set; }
        public int ProductCategoryId { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ProductCategory? ProductCategory { get; set; }
        public List<TemplatePhotoArea> PhotoAreas { get; set; } = new List<TemplatePhotoArea>();
    }

    public class TemplatePhotoArea
    {
        public int Id { get; set; }
        public string LayoutId { get; set; } = string.Empty;
        public int PhotoIndex { get; set; } // 1, 2, 3, 4 for strips
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Rotation { get; set; } = 0;

        // Navigation properties
        public TemplateLayout? Layout { get; set; }
    }

    public class PhotoArea
    {
        public string Id { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Rotation { get; set; } = 0;
    }

    public class TemplateDimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class TemplateConfig
    {
        public TemplateDimensions Dimensions { get; set; } = new TemplateDimensions();
        public List<PhotoArea> PhotoAreas { get; set; } = new List<PhotoArea>();
        public int PhotoCount { get; set; } = 1;
        public decimal Price { get; set; } = 0;
        public string Category { get; set; } = "Classic";
        public string Description { get; set; } = string.Empty;
    }

    public class Template
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string LayoutId { get; set; } = string.Empty; // Links to TemplateLayouts
        public string FolderPath { get; set; } = string.Empty; // Path to template folder
        public string TemplatePath { get; set; } = string.Empty; // Path to template.png
        public string PreviewPath { get; set; } = string.Empty; // Path to preview image
        public bool IsActive { get; set; } = true;
        public decimal Price { get; set; } = 0;
        public int SortOrder { get; set; } = 0;
        public long FileSize { get; set; } = 0;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string? UploadedBy { get; set; }
        public string Description { get; set; } = string.Empty;
        
        // Computed properties from layout
        public int Width => Layout?.Width ?? 0;
        public int Height => Layout?.Height ?? 0;
        public int PhotoCount => Layout?.PhotoCount ?? 1;
        public List<PhotoArea> PhotoAreas 
        { 
            get => Layout?.PhotoAreas?.Select(pa => new PhotoArea
            {
                Id = pa.PhotoIndex.ToString(),
                X = pa.X,
                Y = pa.Y,
                Width = pa.Width,
                Height = pa.Height,
                Rotation = pa.Rotation
            }).ToList() ?? new List<PhotoArea>();
        }
        
        // Legacy properties for backward compatibility
        public string FilePath 
        { 
            get => TemplatePath; 
            set => TemplatePath = value; 
        }
        public string? ThumbnailPath 
        { 
            get => PreviewPath != TemplatePath ? PreviewPath : null; 
            set => PreviewPath = value ?? TemplatePath; 
        }
        public string ConfigPath { get; set; } = string.Empty; // Legacy, unused
        public bool HasValidConfig { get; set; } = true; // Legacy, always true
        public bool HasPreview { get; set; } = true; // Legacy, computed from file existence
        public List<string> ValidationWarnings { get; set; } = new List<string>();

        // Navigation properties
        public TemplateCategory? Category { get; set; }
        public TemplateLayout? Layout { get; set; }
        
        // Legacy navigation property for backward compatibility
        public ProductCategory? ProductCategory => Layout?.ProductCategory;
        public int ProductCategoryId => Layout?.ProductCategoryId ?? 1;
    }

    public class SeasonalSchedule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string StartDate { get; set; } = string.Empty; // MM-DD format
        public string EndDate { get; set; } = string.Empty;   // MM-DD format
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Validation helper methods (reusing from TemplateCategory)
        public static bool IsValidSeasonalDate(string? date) => TemplateCategory.IsValidSeasonalDate(date);
        public static string? ValidateAndFormatSeasonalDate(string? date) => TemplateCategory.ValidateAndFormatSeasonalDate(date);
    }

    public class TemplateUsageStat
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public int UsageCount { get; set; } = 0;
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public Template? Template { get; set; }
    }

    public class TemplateValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string? RecommendedAction { get; set; }
        public Template? Template { get; set; }
    }

    public class TemplateUploadResult
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public List<TemplateValidationResult> Results { get; set; } = new List<TemplateValidationResult>();
        public int SuccessCount { get; set; } = 0;
        public int FailureCount { get; set; } = 0;
    }

    // =============================================
    // SALES & TRANSACTION MODELS
    // =============================================

    public enum PaymentMethod
    {
        Cash,
        Credit,
        Free
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded
    }

    public enum PrintStatus
    {
        Pending,
        Printing,
        Completed,
        Failed
    }

    public class Transaction
    {
        public int Id { get; set; }
        public string TransactionCode { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int? TemplateId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal BasePrice { get; set; }
        public decimal TotalPrice { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Completed;
        public string? CustomerEmail { get; set; }
        public string? SessionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public Product? Product { get; set; }
        public Template? Template { get; set; }
    }

    public class TransactionPhoto
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public int PhotoNumber { get; set; }
        public string OriginalFilePath { get; set; } = string.Empty;
        public string? ProcessedFilePath { get; set; }
        public bool IsRetake { get; set; } = false;
        public DateTime TakenAt { get; set; } = DateTime.Now;

        // Navigation properties
        public Transaction? Transaction { get; set; }
    }

    public class PrintJob
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public int Copies { get; set; } = 1;
        public PrintStatus PrintStatus { get; set; } = PrintStatus.Pending;
        public string? PrinterName { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? FailureReason { get; set; }
        public int PrintsUsed { get; set; } = 1;

        // Navigation properties
        public Transaction? Transaction { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Name { get; set; }
        public bool OptInMarketing { get; set; } = false;
        public DateTime FirstVisit { get; set; } = DateTime.Now;
        public DateTime? LastVisit { get; set; }
        public int TotalTransactions { get; set; } = 0;
        public decimal TotalSpent { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class DailySalesSummary
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD format
        public decimal TotalRevenue { get; set; } = 0;
        public int TotalTransactions { get; set; } = 0;
        public int StripSales { get; set; } = 0;
        public int Photo4x6Sales { get; set; } = 0;
        public int SmartphonePrintSales { get; set; } = 0;
        public decimal CashPayments { get; set; } = 0;
        public decimal CreditPayments { get; set; } = 0;
        public int FreeTransactions { get; set; } = 0;
        public int PrintsUsed { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // =============================================
    // HARDWARE MODELS
    // =============================================

    public enum HardwareStatus
    {
        Online,
        Offline,
        Error,
        Maintenance
    }

    public enum SupplyType
    {
        Paper,
        Ink,
        Ribbon
    }

    public enum UsageType
    {
        Print,
        Test,
        Maintenance,
        Waste
    }

    public class HardwareStatusModel
    {
        public int Id { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public HardwareStatus Status { get; set; }
        public DateTime LastCheckAt { get; set; } = DateTime.Now;
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? LastMaintenanceAt { get; set; }
    }

    public class PrintSupply
    {
        public int Id { get; set; }
        public SupplyType SupplyType { get; set; }
        public int TotalCapacity { get; set; }
        public int CurrentCount { get; set; }
        public int LowThreshold { get; set; } = 100;
        public int CriticalThreshold { get; set; } = 50;
        public DateTime? LastRFIDDetection { get; set; }
        public DateTime InstalledAt { get; set; } = DateTime.Now;
        public DateTime? ReplacedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class SupplyUsageHistory
    {
        public int Id { get; set; }
        public int SupplyId { get; set; }
        public UsageType UsageType { get; set; }
        public int Quantity { get; set; } = 1;
        public int RemainingCount { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.Now;
        public int? TransactionId { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public PrintSupply? Supply { get; set; }
        public Transaction? Transaction { get; set; }
    }

    // =============================================
    // SYSTEM MODELS
    // =============================================

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public enum Severity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class SystemLog
    {
        public int Id { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public AdminUser? User { get; set; }
    }

    public class SystemError
    {
        public int Id { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public Severity Severity { get; set; }
        public bool IsResolved { get; set; } = false;
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public DateTime FirstOccurrence { get; set; } = DateTime.Now;
        public DateTime LastOccurrence { get; set; } = DateTime.Now;
        public int OccurrenceCount { get; set; } = 1;

        // Navigation properties
        public AdminUser? ResolvedByUser { get; set; }
    }

    // =============================================
    // CONFIGURATION MODELS
    // =============================================

    public class Setting
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DataType { get; set; } = "String";
        public string? Description { get; set; }
        public bool IsUserEditable { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
    }

    public class BusinessInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BusinessName { get; set; } = string.Empty;
        public string? LogoPath { get; set; }
        public string? Address { get; set; }
        public bool ShowLogoOnPrints { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
    }

    // =============================================
    // DATA TRANSFER OBJECTS (DTOs)
    // =============================================

    public class SalesOverviewDto
    {
        public string SaleDate { get; set; } = string.Empty;
        public string ProductCategory { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal Revenue { get; set; }
        public int TotalCopies { get; set; }
        public int PrintsUsed { get; set; }
    }

    public class PopularTemplateDto
    {
        public string TemplateName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int TimesUsed { get; set; }
        public decimal Revenue { get; set; }
        public DateTime? LastUsed { get; set; }
    }

    public class HardwareStatusDto
    {
        public string ComponentName { get; set; } = string.Empty;
        public HardwareStatus Status { get; set; }
        public string StatusIcon { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public DateTime LastCheckAt { get; set; }
    }

    // =============================================
    // UTILITY CLASSES
    // =============================================

    public class DatabaseResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static DatabaseResult<T> SuccessResult(T data)
        {
            return new DatabaseResult<T> { Success = true, Data = data };
        }

        public static DatabaseResult<T> ErrorResult(string errorMessage, Exception? exception = null)
        {
            return new DatabaseResult<T> 
            { 
                Success = false, 
                ErrorMessage = errorMessage, 
                Exception = exception 
            };
        }
    }

    public class DatabaseResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static DatabaseResult SuccessResult()
        {
            return new DatabaseResult { Success = true };
        }

        public static DatabaseResult ErrorResult(string errorMessage, Exception? exception = null)
        {
            return new DatabaseResult 
            { 
                Success = false, 
                ErrorMessage = errorMessage, 
                Exception = exception 
            };
        }
    }

    /// <summary>
    /// System date and seasonal status information
    /// </summary>
    public class SystemDateStatus
    {
        public DateTime CurrentSystemDate { get; set; }
        public string CurrentSystemDateString { get; set; } = string.Empty;
        public string CurrentDateForSeason { get; set; } = string.Empty; // MM-DD format
        public string TimeZone { get; set; } = string.Empty;
        public int ActiveSeasonsCount { get; set; } = 0;
        public List<SeasonStatus> SeasonalCategories { get; set; } = new List<SeasonStatus>();
    }

    /// <summary>
    /// Individual season status information
    /// </summary>
    public class SeasonStatus
    {
        public string CategoryName { get; set; } = string.Empty;
        public string SeasonStartDate { get; set; } = string.Empty; // MM-DD format
        public string SeasonEndDate { get; set; } = string.Empty;   // MM-DD format
        public int SeasonalPriority { get; set; } = 0;
        public bool IsCurrentlyActive { get; set; } = false;
        public bool SpansYears { get; set; } = false; // True if start > end (e.g., Christmas)
    }
} 