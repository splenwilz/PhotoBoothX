using System;
using System.ComponentModel.DataAnnotations;

namespace Photobooth.Models
{
    // =============================================
    // ENUMS
    // =============================================
    
    public enum AdminAccessLevel
    {
        None,
        User,
        Master
    }

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

    public enum ProductType
    {
        PhotoStrips,
        Photo4x6,
        SmartphonePrint
    }

    public enum PrintStatus
    {
        Pending,
        Printing,
        Completed,
        Failed
    }

    public enum HardwareStatus
    {
        Online,
        Offline,
        Error,
        Maintenance
    }

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

    // =============================================
    // ENTITY MODELS
    // =============================================

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

    public class TemplateCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Template
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int ProductCategoryId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSeasonal { get; set; } = false;
        public decimal Price { get; set; } = 0;
        public int SortOrder { get; set; } = 0;
        public int? FileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string? UploadedBy { get; set; }

        // Navigation properties
        public TemplateCategory? Category { get; set; }
        public ProductCategory? ProductCategory { get; set; }
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

    // =============================================
    // DTO CLASSES FOR COMPLEX QUERIES
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
} 