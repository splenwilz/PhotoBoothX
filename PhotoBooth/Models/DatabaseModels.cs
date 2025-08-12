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

    public enum TemplateType
    {
        Strip,
        Photo4x6
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
        
        // Legacy extra copy pricing configuration (deprecated - use product-specific pricing below)
        public bool UseCustomExtraCopyPricing { get; set; } = false; // If false, extra copies cost same as base price
        public decimal? ExtraCopy1Price { get; set; } // Price for 1 extra copy (nullable, uses base price if null)
        public decimal? ExtraCopy2Price { get; set; } // Price for 2 extra copies (nullable, uses base price if null)
        public decimal? ExtraCopy4BasePrice { get; set; } // Base price for 4+ extra copies (nullable, uses base price if null)
        public decimal? ExtraCopyAdditionalPrice { get; set; } // Price per additional copy beyond 4 (nullable, uses base price if null)
        
        // Simplified product-specific extra copy pricing configuration
        // Photo Strips extra copy pricing
        public decimal? StripsExtraCopyPrice { get; set; } // Price per extra strip copy
        public decimal? StripsMultipleCopyDiscount { get; set; } // Discount percentage for 2+ copies (0-100)
        
        // 4x6 Photos extra copy pricing
        public decimal? Photo4x6ExtraCopyPrice { get; set; } // Price per extra 4x6 copy
        public decimal? Photo4x6MultipleCopyDiscount { get; set; } // Discount percentage for 2+ copies (0-100)
        
        // Smartphone Print extra copy pricing
        public decimal? SmartphoneExtraCopyPrice { get; set; } // Price per extra smartphone print copy
        public decimal? SmartphoneMultipleCopyDiscount { get; set; } // Discount percentage for 2+ copies (0-100)
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ProductCategory? Category { get; set; }
    }

    /// <summary>
    /// Request object for updating product properties
    /// Encapsulates all optional parameters for the UpdateProductAsync method
    /// </summary>
    public class ProductUpdateRequest
    {
        public bool? IsActive { get; set; }
        public decimal? Price { get; set; }
        public bool? UseCustomExtraCopyPricing { get; set; }
        public decimal? ExtraCopy1Price { get; set; }
        public decimal? ExtraCopy2Price { get; set; }
        public decimal? ExtraCopy4BasePrice { get; set; }
        public decimal? ExtraCopyAdditionalPrice { get; set; }
        public decimal? StripsExtraCopyPrice { get; set; }
        public decimal? StripsMultipleCopyDiscount { get; set; }
        public decimal? Photo4x6ExtraCopyPrice { get; set; }
        public decimal? Photo4x6MultipleCopyDiscount { get; set; }
        public decimal? SmartphoneExtraCopyPrice { get; set; }
        public decimal? SmartphoneMultipleCopyDiscount { get; set; }

        /// <summary>
        /// Validates that at least one property has a value
        /// </summary>
        public bool HasAnyValue()
        {
            return IsActive.HasValue || Price.HasValue || UseCustomExtraCopyPricing.HasValue ||
                   ExtraCopy1Price.HasValue || ExtraCopy2Price.HasValue || ExtraCopy4BasePrice.HasValue ||
                   ExtraCopyAdditionalPrice.HasValue || StripsExtraCopyPrice.HasValue ||
                   StripsMultipleCopyDiscount.HasValue || Photo4x6ExtraCopyPrice.HasValue ||
                   Photo4x6MultipleCopyDiscount.HasValue || SmartphoneExtraCopyPrice.HasValue ||
                   SmartphoneMultipleCopyDiscount.HasValue;
        }
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
        public bool IsPremium { get; set; } = false; // Premium badge determination
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
                
                // Parse start and end dates
                var startParts = SeasonStartDate.Split('-');
                var endParts = SeasonEndDate.Split('-');
                
                if (startParts.Length != 2 || endParts.Length != 2 ||
                    !int.TryParse(startParts[0], out int startMonth) || !int.TryParse(startParts[1], out int startDay) ||
                    !int.TryParse(endParts[0], out int endMonth) || !int.TryParse(endParts[1], out int endDay))
                {
                    return false;
                }
                
                var currentMonth = today.Month;
                var currentDay = today.Day;
                
                // Handle cross-year seasons (e.g., Christmas: 12-01 to 01-15)
                if (startMonth > endMonth || (startMonth == endMonth && startDay > endDay))
                {
                    // Season spans across years
                    return (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) ||
                           (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
                }
                else
                {
                    // Season within same year
                    return (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) &&
                           (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
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
        public int BorderRadius { get; set; } = 0; // Border radius in pixels for rounded corners

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
        public int BorderRadius { get; set; } = 0; // Border radius in pixels for rounded corners
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
        public TemplateType TemplateType { get; set; } = TemplateType.Strip;
        
        // Computed properties from layout
        public int Width => Layout?.Width ?? 0;
        public int Height => Layout?.Height ?? 0;
        public int PhotoCount => Layout?.PhotoCount ?? 1;
        public List<PhotoArea> PhotoAreas 
        { 
            get => Layout?.PhotoAreas?.Where(pa => pa != null).Select(pa => new PhotoArea
            {
                Id = pa.PhotoIndex.ToString(),
                X = pa.X,
                Y = pa.Y,
                Width = pa.Width,
                Height = pa.Height,
                Rotation = pa.Rotation,
                BorderRadius = pa.BorderRadius
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

    /// <summary>
    /// Represents a credit transaction for tracking credit history
    /// </summary>
    public class CreditTransaction
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public CreditTransactionType TransactionType { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal BalanceAfter { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public int? RelatedTransactionId { get; set; }
        
        // Backward compatibility properties
        public CreditTransactionType Type => TransactionType;
        public DateTime Timestamp => CreatedAt;
    }

    /// <summary>
    /// Types of credit transactions
    /// </summary>
    public enum CreditTransactionType
    {
        Add,
        Deduct,
        Reset
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