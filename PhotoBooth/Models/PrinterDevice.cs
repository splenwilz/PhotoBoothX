namespace Photobooth.Models
{
    /// <summary>
    /// Represents a printer device available for printing
    /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printersettings
    /// </summary>
    public class PrinterDevice
    {
        /// <summary>
        /// Zero-based index of the printer in the installed printers collection
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Human-readable name of the printer device (e.g., "DNP DS620A")
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the printer is currently online and ready to print
        /// </summary>
        public bool IsOnline { get; set; }
        
        /// <summary>
        /// Whether this is the default printer
        /// </summary>
        public bool IsDefault { get; set; }
        
        /// <summary>
        /// Printer status information (e.g., "Ready", "Offline", "Error")
        /// </summary>
        public string Status { get; set; } = "Unknown";
        
        /// <summary>
        /// Printer model/driver name (e.g., "DNP DS-RX1")
        /// </summary>
        public string? Model { get; set; }
        
        /// <summary>
        /// Printer location (if available)
        /// </summary>
        public string? Location { get; set; }
        
        /// <summary>
        /// Printer comment/description (if available)
        /// </summary>
        public string? Comment { get; set; }
        
        /// <summary>
        /// Whether the printer supports color printing
        /// </summary>
        public bool SupportsColor { get; set; }
        
        /// <summary>
        /// Maximum number of copies supported by the printer
        /// </summary>
        public int MaxCopies { get; set; }
        
        /// <summary>
        /// Whether the printer can print duplex (two-sided)
        /// </summary>
        public bool SupportsDuplex { get; set; }
        
        /// <summary>
        /// Roll capacity information (if available)
        /// </summary>
        public RollCapacityInfo? RollCapacity { get; set; }
    }
    
    /// <summary>
    /// Represents roll capacity/paper level information for roll-fed printers
    /// </summary>
    public class RollCapacityInfo
    {
        /// <summary>
        /// Whether roll capacity information is available for this printer
        /// </summary>
        public bool IsAvailable { get; set; }
        
        /// <summary>
        /// Remaining paper level as a percentage (0-100), or null if not available
        /// </summary>
        public int? RemainingPercentage { get; set; }
        
        /// <summary>
        /// Remaining prints/capacity (if available), or null if not available
        /// </summary>
        public int? RemainingPrints { get; set; }
        
        /// <summary>
        /// Maximum capacity (total prints per roll), or null if not available
        /// </summary>
        public int? MaxCapacity { get; set; }
        
        /// <summary>
        /// Status message about paper level (e.g., "Low", "OK", "Full", "Unknown")
        /// </summary>
        public string Status { get; set; } = "Unknown";
        
        /// <summary>
        /// Method used to retrieve this information (e.g., "WMI", "SNMP", "PrintQueue", "Not Available")
        /// </summary>
        public string Source { get; set; } = "Not Available";
        
        /// <summary>
        /// Additional details or error messages
        /// </summary>
        public string? Details { get; set; }
    }
}

