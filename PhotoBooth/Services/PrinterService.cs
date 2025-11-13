using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Printing;
using System.Threading;
using System.Management; // For WMI queries
using System.Text.RegularExpressions; // For parsing SNMP/WMI responses
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Service for printer operations using System.Drawing.Printing
    /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing
    /// </summary>
    public class PrinterService : IPrinterService
    {
        #region Private Fields

        private bool _isInitialized = false;
        private string? _selectedPrinterName;
        private readonly object _lockObject = new object();
        
        // Cached printer status to avoid repeated expensive checks
        // Cache expires after 5 seconds to balance performance and accuracy
        private PrinterDevice? _cachedPrinterStatus;
        private DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromSeconds(5);

        #endregion

        #region Constructor

        /// <summary>
        /// Initialize printer service
        /// </summary>
        public PrinterService()
        {
            try
            {
                // Initialize by getting default printer
                _selectedPrinterName = GetDefaultPrinterName();
                _isInitialized = true;

                LoggingService.Hardware.Information("Printer", "PrinterService initialized",
                    ("DefaultPrinter", _selectedPrinterName ?? "None"),
                    ("AvailablePrinters", GetAvailablePrinters().Count));
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Printer", "Failed to initialize PrinterService", ex);
                _isInitialized = false;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Check if printer service is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Get the currently selected/default printer name
        /// </summary>
        public string? SelectedPrinterName => _selectedPrinterName;

        #endregion

        #region Public Methods

        /// <summary>
        /// Get list of all available printers installed on the system
        /// Uses System.Drawing.Printing.PrinterSettings.InstalledPrinters
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printersettings.installedprinters
        /// </summary>
        /// <returns>List of available printer devices</returns>
        public List<PrinterDevice> GetAvailablePrinters()
        {
            var printers = new List<PrinterDevice>();

            try
            {
                lock (_lockObject)
                {
                    // Get all installed printers from Windows
                    // InstalledPrinters is a StringCollection containing printer names
                    var installedPrinters = PrinterSettings.InstalledPrinters;
                    var defaultPrinterName = GetDefaultPrinterName();

                    // Convert to PrinterDevice objects
                    for (int i = 0; i < installedPrinters.Count; i++)
                    {
                        var printerName = installedPrinters[i];
                        
                        // Create PrinterSettings to check printer status
                        var printerSettings = new PrinterSettings
                        {
                            PrinterName = printerName
                        };

                        // Check if printer is valid and get detailed information
                        bool isOnline = false;
                        string status = "Unknown";
                        string? model = null;
                        string? location = null;
                        string? comment = null;
                        bool supportsColor = false;
                        int maxCopies = 1;
                        bool supportsDuplex = false;

                        try
                        {
                            // PrinterSettings.IsValid checks if printer exists and is accessible
                            if (printerSettings.IsValid)
                            {
                                // Check actual printer offline status using comprehensive method
                                // Uses both PrintQueue and WMI WorkOffline flag for accurate detection
                                // Some printers (e.g., DNP) only report offline via WMI WorkOffline flag
                                // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.isoffline
                                // Use the existing IsPrinterOffline() method which checks both PrintQueue and WMI
                                isOnline = !IsPrinterOffline(printerName);
                                status = isOnline ? "Ready" : "Offline";
                                
                                // Log the status for debugging
                                if (!isOnline)
                                {
                                    LoggingService.Hardware.Information("Printer", $"Printer detected as offline: {printerName}",
                                        ("Status", status));
                                }
                                
                                // Get additional printer information from PrinterSettings
                                // Note: Some properties may not be available for all printers
                                try
                                {
                                    // Printer name often contains model information
                                    model = printerName;
                                    
                                    // Try to get printer location (may not be available)
                                    // Location is typically set in Windows printer properties
                                    
                                    // Check if printer supports color
                                    // CanPrint checks if printer can print, but doesn't tell us about color
                                    // We'll assume photo printers support color
                                    supportsColor = true; // Photo printers typically support color
                                    
                                    // Get maximum copies supported
                                    maxCopies = printerSettings.MaximumCopies;
                                    
                                    // Check duplex support
                                    supportsDuplex = printerSettings.CanDuplex;
                                }
                                catch
                                {
                                    // Some properties may not be accessible, use defaults
                                }
                            }
                            else
                            {
                                status = "Invalid";
                            }
                        }
                        catch (Exception ex)
                        {
                            // Printer might be offline or inaccessible
                            LoggingService.Hardware.Warning("Printer", $"Could not check status for printer: {printerName}",
                                ("Exception", ex.Message));
                            status = "Error";
                        }

                        var printerDevice = new PrinterDevice
                        {
                            Index = i,
                            Name = printerName ?? string.Empty,
                            IsOnline = isOnline,
                            IsDefault = string.Equals(printerName, defaultPrinterName, StringComparison.OrdinalIgnoreCase),
                            Status = status,
                            Model = model,
                            Location = location,
                            Comment = comment,
                            SupportsColor = supportsColor,
                            MaxCopies = maxCopies,
                            SupportsDuplex = supportsDuplex
                        };

                        printers.Add(printerDevice);
                    }

                    LoggingService.Hardware.Information("Printer", "Retrieved available printers",
                        ("Count", printers.Count),
                        ("DefaultPrinter", defaultPrinterName ?? "None"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Printer", "Failed to get available printers", ex);
            }

            return printers;
        }

        /// <summary>
        /// Select a printer by name for subsequent print operations
        /// </summary>
        /// <param name="printerName">Name of the printer to select</param>
        /// <returns>True if printer was found and selected, false otherwise</returns>
        public bool SelectPrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                LoggingService.Hardware.Warning("Printer", "Attempted to select printer with null or empty name");
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    // Verify printer exists
                    var availablePrinters = GetAvailablePrinters();
                    var printer = availablePrinters.FirstOrDefault(p => 
                        string.Equals(p.Name, printerName, StringComparison.OrdinalIgnoreCase));

                    if (printer == null)
                    {
                        LoggingService.Hardware.Warning("Printer", $"Printer not found: {printerName}");
                        return false;
                    }

                    // Verify printer is valid using PrinterSettings
                    var printerSettings = new PrinterSettings
                    {
                        PrinterName = printerName
                    };

                    if (!printerSettings.IsValid)
                    {
                        LoggingService.Hardware.Warning("Printer", $"Printer is not valid: {printerName}");
                        return false;
                    }

                    _selectedPrinterName = printerName;
                    LoggingService.Hardware.Information("Printer", "Printer selected", ("PrinterName", printerName));
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Printer", $"Failed to select printer: {printerName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get the default printer name from system settings
        /// Uses PrinterSettings to get the default printer
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printersettings.printername
        /// </summary>
        /// <returns>Default printer name, or null if no default printer is set</returns>
        public string? GetDefaultPrinterName()
        {
            try
            {
                // Create a PrinterSettings instance - it defaults to the system default printer
                var printerSettings = new PrinterSettings();
                
                // If no default printer is set, PrinterName will be empty
                if (string.IsNullOrWhiteSpace(printerSettings.PrinterName))
                {
                    LoggingService.Hardware.Information("Printer", "No default printer configured");
                    return null;
                }

                return printerSettings.PrinterName;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Printer", "Failed to get default printer name", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if a specific printer is available and online
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>True if printer exists and is online, false otherwise</returns>
        public bool IsPrinterAvailable(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return false;
            }

            try
            {
                var printerSettings = new PrinterSettings
                {
                    PrinterName = printerName
                };

                // IsValid checks if the printer exists and is accessible
                return printerSettings.IsValid;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Warning("Printer", $"Error checking printer availability: {printerName}",
                    ("Exception", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Get detailed status information for a specific printer
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>PrinterDevice with status information, or null if printer not found</returns>
        public PrinterDevice? GetPrinterStatus(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return null;
            }

            try
            {
                var availablePrinters = GetAvailablePrinters();
                return availablePrinters.FirstOrDefault(p => 
                    string.Equals(p.Name, printerName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Printer", $"Failed to get printer status: {printerName}", ex);
                return null;
            }
        }

        /// <summary>
        /// Refresh the cached printer status for the selected/default printer
        /// This performs a fresh check of printer status and updates the cache
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.refresh
        /// </summary>
        /// <returns>True if cache was refreshed successfully, false otherwise</returns>
        public bool RefreshCachedStatus()
        {
            lock (_lockObject)
            {
                try
                {
                    // Get the printer name to check (selected or default)
                    var printerName = _selectedPrinterName ?? GetDefaultPrinterName();
                    
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        // No printer available - clear cache
                        _cachedPrinterStatus = null;
                        _cacheTimestamp = DateTime.MinValue;
                        LoggingService.Hardware.Warning("Printer", "Cannot refresh cache - no printer selected or available");
                        return false;
                    }

                    // Get fresh printer status
                    var status = GetPrinterStatus(printerName);
                    
                    // Update cache with fresh data
                    _cachedPrinterStatus = status;
                    _cacheTimestamp = DateTime.UtcNow;
                    
                    LoggingService.Hardware.Information("Printer", "Printer status cache refreshed",
                        ("PrinterName", printerName),
                        ("IsOnline", status?.IsOnline ?? false),
                        ("Status", status?.Status ?? "Unknown"));
                    
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.Hardware.Warning("Printer", "Failed to refresh printer status cache",
                        ("Exception", ex.Message));
                    return false;
                }
            }
        }

        /// <summary>
        /// Get the cached printer status for the selected/default printer
        /// Returns cached value if available and recent (within 5 seconds), otherwise refreshes cache first
        /// This avoids repeated expensive printer status checks for better performance
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue
        /// </summary>
        /// <returns>Cached PrinterDevice with current status, or null if no printer is selected</returns>
        public PrinterDevice? GetCachedPrinterStatus()
        {
            lock (_lockObject)
            {
                // Check if cache is valid (exists and not expired)
                var cacheAge = DateTime.UtcNow - _cacheTimestamp;
                bool cacheValid = _cachedPrinterStatus != null && cacheAge < CACHE_EXPIRY;
                
                if (cacheValid)
                {
                    // Return cached value - no need to refresh
                    return _cachedPrinterStatus;
                }
                
                // Cache is expired or doesn't exist - refresh it
                RefreshCachedStatus();
                
                // Return the newly cached value (or null if refresh failed)
                return _cachedPrinterStatus;
            }
        }

        /// <summary>
        /// Get roll capacity/paper level information for a printer
        /// Attempts multiple methods: WMI, System.Printing, SNMP, and custom driver properties
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-start-page
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>RollCapacityInfo with available information, or null if printer not found</returns>
        public RollCapacityInfo? GetRollCapacity(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return null;
            }

            // Verify printer exists
            if (!IsPrinterAvailable(printerName))
            {
                return new RollCapacityInfo
                {
                    IsAvailable = false,
                    Source = "Not Available",
                    Status = "Printer Not Found",
                    Details = $"Printer '{printerName}' not found or not accessible"
                };
            }

            var result = new RollCapacityInfo
            {
                IsAvailable = false,
                Source = "Not Available",
                Status = "Unknown"
            };

            // Try Method 1: WMI (Windows Management Instrumentation)
            // This is the most likely to work for Windows printers
            try
            {
                var wmiResult = TryGetRollCapacityViaWMI(printerName);
                if (wmiResult != null && wmiResult.IsAvailable)
                {
                    LoggingService.Hardware.Information("Printer", "Roll capacity retrieved via WMI",
                        ("PrinterName", printerName),
                        ("Source", wmiResult.Source),
                        ("Status", wmiResult.Status),
                        ("RemainingPercentage", wmiResult.RemainingPercentage?.ToString() ?? "N/A"));
                    return wmiResult;
                }
                result.Details = wmiResult?.Details ?? "WMI query returned no data";
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Warning("Printer", "WMI query failed, trying other methods",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
                result.Details = $"WMI failed: {ex.Message}";
            }

            // Try Method 2: System.Printing.PrintQueue status
            try
            {
                var printQueueResult = TryGetRollCapacityViaPrintQueue(printerName);
                if (printQueueResult != null && printQueueResult.IsAvailable)
                {
                    LoggingService.Hardware.Information("Printer", "Roll capacity retrieved via PrintQueue",
                        ("PrinterName", printerName),
                        ("Source", printQueueResult.Source),
                        ("Status", printQueueResult.Status));
                    return printQueueResult;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Warning("Printer", "PrintQueue query failed",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
            }

            // Try Method 3: Custom driver properties via PrinterSettings
            try
            {
                var driverResult = TryGetRollCapacityViaDriver(printerName);
                if (driverResult != null && driverResult.IsAvailable)
                {
                    LoggingService.Hardware.Information("Printer", "Roll capacity retrieved via driver properties",
                        ("PrinterName", printerName),
                        ("Source", driverResult.Source),
                        ("Status", driverResult.Status));
                    return driverResult;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Warning("Printer", "Driver properties query failed",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
            }

            // If all methods failed, return result indicating not available
            result.Details = result.Details ?? "No methods returned roll capacity information";
            LoggingService.Hardware.Information("Printer", "Roll capacity not available via any method",
                ("PrinterName", printerName),
                ("Details", result.Details));
            return result;
        }

        /// <summary>
        /// Cancel all print jobs in the queue for the specified printer
        /// This prevents jobs from printing automatically when the printer comes back online
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printsystemjobinfo.cancel
        /// </summary>
        /// <param name="printerName">Name of the printer whose jobs should be cancelled</param>
        /// <returns>Number of jobs that were cancelled</returns>
        private int CancelAllPrintJobs(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return 0;
            }

            try
            {
                using var printServer = new LocalPrintServer();
                using var printQueue = printServer.GetPrintQueue(printerName);
                printQueue.Refresh();

                var jobs = printQueue.GetPrintJobInfoCollection();
                int cancelledCount = 0;

                foreach (PrintSystemJobInfo job in jobs)
                {
                    try
                    {
                        job.Cancel();
                        cancelledCount++;
                        Console.WriteLine($"!!! CANCELLED PRINT JOB: ID={job.JobIdentifier}, Name={job.Name} !!!");
                        LoggingService.Hardware.Information("Printer", "Cancelled print job from queue",
                            ("PrinterName", printerName),
                            ("JobId", job.JobIdentifier),
                            ("JobName", job.Name));
                    }
                    catch (Exception jobEx)
                    {
                        LoggingService.Hardware.Warning("Printer", "Failed to cancel print job",
                            ("PrinterName", printerName),
                            ("JobId", job.JobIdentifier),
                            ("Exception", jobEx.Message));
                    }
                }

                if (cancelledCount > 0)
                {
                    Console.WriteLine($"!!! CANCELLED {cancelledCount} PRINT JOB(S) FROM QUEUE FOR OFFLINE PRINTER: {printerName} !!!");
                    LoggingService.Hardware.Information("Printer", "Cancelled all print jobs from queue for offline printer",
                        ("PrinterName", printerName),
                        ("CancelledCount", cancelledCount));
                }

                return cancelledCount;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Warning("Printer", "Failed to cancel print jobs from queue",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
                return 0;
            }
        }

        /// <summary>
        /// Determine whether Windows reports the printer as offline
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.isoffline
        /// </summary>
        private bool IsPrinterOffline(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return true;
            }

            try
            {
                using var server = new LocalPrintServer();
                using var queue = server.GetPrintQueue(printerName);
                queue.Refresh();

                if (queue.IsOffline)
                {
                    return true;
                }

                if (queue.QueueStatus.HasFlag(PrintQueueStatus.Offline) ||
                    queue.QueueStatus.HasFlag(PrintQueueStatus.ServerUnknown) ||
                    queue.QueueStatus.HasFlag(PrintQueueStatus.Error))
                {
                    return true;
                }

                // Some drivers (e.g., DNP) report offline only through WMI WorkOffline flag
                try
                {
                    var query = $"SELECT WorkOffline, PrinterStatus FROM Win32_Printer WHERE Name = '{printerName.Replace("'", "''")}'";
                    using var searcher = new ManagementObjectSearcher(query);
                    using var collection = searcher.Get();
                    foreach (ManagementObject printer in collection)
                    {
                        var workOfflineValue = printer["WorkOffline"];
                        if (workOfflineValue is bool workOffline && workOffline)
                        {
                            return true;
                        }

                        // PrinterStatus = 7 means Offline per MSDN documentation
                        var statusValue = printer["PrinterStatus"]?.ToString();
                        if (statusValue == "7")
                        {
                            return true;
                        }
                    }
                }
                catch (Exception wmiEx)
                {
                    LoggingService.Hardware.Warning("Printer", "Failed to query WMI for offline status, continuing with PrintQueue result",
                        ("PrinterName", printerName),
                        ("Exception", wmiEx.Message));
                }

                return false;
            }
            catch (Exception ex)
            {
                // If we cannot query the queue (e.g., printer unplugged), treat as offline for safety
                LoggingService.Hardware.Warning("Printer", "Unable to determine printer status, assuming offline",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
                return true;
            }
        }

        /// <summary>
        /// Check if printer hardware is idle via WMI (Windows Management Instrumentation)
        /// PrinterStatus values: 1=Other, 2=Unknown, 3=Idle, 4=Printing, 5=Warmup, 6=Stopped printing, 7=Offline
        /// Reference: https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-printer
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>True if printer is idle (status 3), false if printing/busy/unknown</returns>
        private bool IsPrinterIdleViaWMI(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return false; // Can't determine status without printer name
            }

            try
            {
                var query = $"SELECT PrinterStatus, ExtendedPrinterStatus FROM Win32_Printer WHERE Name = '{printerName.Replace("'", "''")}'";
                using var searcher = new ManagementObjectSearcher(query);
                using var collection = searcher.Get();
                
                foreach (ManagementObject printer in collection)
                {
                    var printerStatus = printer["PrinterStatus"]?.ToString();
                    var extendedStatus = printer["ExtendedPrinterStatus"]?.ToString();
                    
                    // PrinterStatus = 3 means Idle per MSDN documentation
                    // This indicates the printer hardware is ready and not actively printing
                    if (printerStatus == "3")
                    {
                        Console.WriteLine($"!!! WMI: Printer {printerName} is IDLE (PrinterStatus=3, ExtendedStatus={extendedStatus ?? "N/A"}) !!!");
                        return true;
                    }
                    
                    // PrinterStatus = 4 means Printing (actively printing)
                    // PrinterStatus = 5 means Warmup (printer is warming up)
                    // PrinterStatus = 6 means Stopped printing (error state)
                    // PrinterStatus = 7 means Offline
                    // Any other status means printer is busy/not idle
                    Console.WriteLine($"!!! WMI: Printer {printerName} is NOT idle (PrinterStatus={printerStatus ?? "null"}, ExtendedStatus={extendedStatus ?? "N/A"}) !!!");
                    return false;
                }
                
                // If we can't find the printer in WMI, assume it's not idle (conservative approach)
                Console.WriteLine($"!!! WMI: Printer {printerName} not found in WMI, assuming NOT idle !!!");
                return false;
            }
            catch (Exception ex)
            {
                // If WMI query fails, assume printer is not idle (conservative approach)
                // This ensures we don't finish too early if we can't check status
                Console.WriteLine($"!!! WMI: Error checking printer idle status: {ex.Message}, assuming NOT idle !!!");
                LoggingService.Hardware.Warning("Printer", "Failed to query WMI for idle status, assuming printer is busy",
                    ("PrinterName", printerName),
                    ("Exception", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Try to get roll capacity via WMI (Windows Management Instrumentation)
        /// Queries Win32_Printer and related classes for paper/media level information
        /// </summary>
        private RollCapacityInfo? TryGetRollCapacityViaWMI(string printerName)
        {
            try
            {
                // Query Win32_Printer for basic printer information
                var query = $"SELECT * FROM Win32_Printer WHERE Name = '{printerName.Replace("'", "''")}'";
                using var searcher = new ManagementObjectSearcher(query);
                using var collection = searcher.Get();

                foreach (ManagementObject printer in collection)
                {
                    // Log ALL available properties to see what the printer exposes
                    Console.WriteLine($"!!! WMI PRINTER PROPERTIES FOR: {printerName} !!!");
                    var allProperties = new System.Text.StringBuilder();
                    foreach (PropertyData prop in printer.Properties)
                    {
                        var propValue = prop.Value?.ToString() ?? "null";
                        // Truncate very long values for readability
                        if (propValue.Length > 100)
                        {
                            propValue = propValue.Substring(0, 100) + "...";
                        }
                        Console.WriteLine($"!!!   {prop.Name} = {propValue} !!!");
                        allProperties.Append($"{prop.Name}={propValue}; ");
                    }
                    Console.WriteLine($"!!! END WMI PROPERTIES !!!");
                    
                    // Check for paper-related properties
                    var status = printer["Status"]?.ToString() ?? "Unknown";
                    var printerStatus = printer["PrinterStatus"]?.ToString() ?? "Unknown";
                    
                    // Try to get extended status information
                    // Some printers expose paper level in ExtendedPrinterStatus or other properties
                    var extendedStatus = printer["ExtendedPrinterStatus"]?.ToString();
                    var detecetedErrorState = printer["DetectedErrorState"]?.ToString();
                    
                    // Check for DNP-specific properties or paper level indicators
                    // DNP printers might expose this in custom properties
                    var result = new RollCapacityInfo
                    {
                        Source = "WMI",
                        Status = status,
                        Details = $"PrinterStatus: {printerStatus}, ExtendedStatus: {extendedStatus ?? "N/A"}"
                    };

                    // Try to parse paper level from status strings
                    // Look for patterns like "Paper Low", "Paper OK", percentage values, etc.
                    if (!string.IsNullOrEmpty(status))
                    {
                        var statusLower = status.ToLowerInvariant();
                        if (statusLower.Contains("paper low") || statusLower.Contains("low paper"))
                        {
                            result.IsAvailable = true;
                            result.Status = "Low";
                            result.RemainingPercentage = 20; // Estimate
                        }
                        else if (statusLower.Contains("out of paper") || statusLower.Contains("paper out"))
                        {
                            result.IsAvailable = true;
                            result.Status = "Out";
                            result.RemainingPercentage = 0;
                        }
                        else if (statusLower.Contains("paper ok") || statusLower.Contains("ready"))
                        {
                            result.IsAvailable = true;
                            result.Status = "OK";
                            result.RemainingPercentage = null; // Unknown but OK
                        }
                    }

                    // Try to query Win32_PrinterConfiguration for additional properties
                    try
                    {
                        var configQuery = $"SELECT * FROM Win32_PrinterConfiguration WHERE Name = '{printerName.Replace("'", "''")}'";
                        using var configSearcher = new ManagementObjectSearcher(configQuery);
                        using var configCollection = configSearcher.Get();
                        
                        Console.WriteLine($"!!! WMI PRINTER CONFIGURATION PROPERTIES FOR: {printerName} !!!");
                        foreach (ManagementObject config in configCollection)
                        {
                            // Log ALL configuration properties to see what's available
                            foreach (PropertyData prop in config.Properties)
                            {
                                var propValue = prop.Value?.ToString() ?? "null";
                                if (propValue.Length > 100)
                                {
                                    propValue = propValue.Substring(0, 100) + "...";
                                }
                                Console.WriteLine($"!!!   Config.{prop.Name} = {propValue} !!!");
                                
                                // Some printer drivers expose custom properties here
                                // Check all properties for paper-related values
                                var propName = prop.Name.ToLowerInvariant();
                                
                                if (propName.Contains("paper") || propName.Contains("level") || propName.Contains("capacity") || propName.Contains("remaining") || 
                                    propName.Contains("media") || propName.Contains("supply") || propName.Contains("roll") || propName.Contains("dnp"))
                                {
                                    result.Details += $", {prop.Name}: {propValue}";
                                    Console.WriteLine($"!!!   FOUND POTENTIAL PAPER PROPERTY: {prop.Name} = {propValue} !!!");
                                    
                                    // Try to extract numeric values
                                    if (propValue != null && int.TryParse(Regex.Match(propValue, @"\d+").Value, out int value))
                                    {
                                        if (propName.Contains("remaining") || propName.Contains("level"))
                                        {
                                            result.RemainingPrints = value;
                                            result.IsAvailable = true;
                                            Console.WriteLine($"!!!   EXTRACTED RemainingPrints: {value} !!!");
                                        }
                                        else if (propName.Contains("max") || propName.Contains("capacity"))
                                        {
                                            result.MaxCapacity = value;
                                            result.IsAvailable = true;
                                            Console.WriteLine($"!!!   EXTRACTED MaxCapacity: {value} !!!");
                                        }
                                    }
                                }
                            }
                        }
                        Console.WriteLine($"!!! END WMI CONFIGURATION PROPERTIES !!!");
                    }
                    catch (Exception ex)
                    {
                        // Config query failed, continue with basic info
                        result.Details += $", ConfigQueryError: {ex.Message}";
                    }

                    // If we got any useful information, return it
                    if (result.IsAvailable || !string.IsNullOrEmpty(result.Details))
                    {
                        return result;
                    }
                }

                return new RollCapacityInfo
                {
                    IsAvailable = false,
                    Source = "WMI",
                    Status = "Not Available",
                    Details = "WMI query returned printer but no paper level information"
                };
            }
            catch (Exception ex)
            {
                return new RollCapacityInfo
                {
                    IsAvailable = false,
                    Source = "WMI",
                    Status = "Error",
                    Details = $"WMI query exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Try to get roll capacity via System.Printing.PrintQueue
        /// Checks for paper-related status flags
        /// </summary>
        private RollCapacityInfo? TryGetRollCapacityViaPrintQueue(string printerName)
        {
            try
            {
                using var printServer = new LocalPrintServer();
                using var printQueue = printServer.GetPrintQueue(printerName);
                printQueue.Refresh();

                var result = new RollCapacityInfo
                {
                    Source = "PrintQueue",
                    Status = printQueue.QueueStatus.ToString()
                };

                // Check for paper problems
                if (printQueue.HasPaperProblem)
                {
                    result.IsAvailable = true;
                    result.Status = "Paper Problem";
                    result.RemainingPercentage = 0;
                    result.Details = "PrintQueue reports paper problem";
                    return result;
                }

                if (printQueue.IsOutOfPaper)
                {
                    result.IsAvailable = true;
                    result.Status = "Out of Paper";
                    result.RemainingPercentage = 0;
                    result.Details = "PrintQueue reports out of paper";
                    return result;
                }

                // Check queue status for paper-related flags
                var queueStatus = printQueue.QueueStatus;
                if (queueStatus.HasFlag(PrintQueueStatus.PaperProblem) || 
                    queueStatus.HasFlag(PrintQueueStatus.PaperOut))
                {
                    result.IsAvailable = true;
                    result.Status = "Paper Issue";
                    result.RemainingPercentage = 0;
                    result.Details = $"QueueStatus: {queueStatus}";
                    return result;
                }

                // Check for paper jam via status flags
                if (queueStatus.HasFlag(PrintQueueStatus.PaperJam))
                {
                    result.IsAvailable = true;
                    result.Status = "Paper Jam";
                    result.Details = "PrintQueue reports paper jam";
                    return result;
                }

                // If printer is ready with no paper problems, we can't determine level but status is OK
                if (queueStatus == PrintQueueStatus.None)
                {
                    result.IsAvailable = true;
                    result.Status = "OK";
                    result.Details = "Printer ready, paper level unknown";
                    return result;
                }

                result.Details = $"QueueStatus: {queueStatus}";
                return result;
            }
            catch (Exception ex)
            {
                return new RollCapacityInfo
                {
                    IsAvailable = false,
                    Source = "PrintQueue",
                    Status = "Error",
                    Details = $"PrintQueue query exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Try to get roll capacity via custom driver properties
        /// Some printer drivers expose paper level through PrinterSettings or custom properties
        /// </summary>
        private RollCapacityInfo? TryGetRollCapacityViaDriver(string printerName)
        {
            try
            {
                var printerSettings = new PrinterSettings
                {
                    PrinterName = printerName
                };

                if (!printerSettings.IsValid)
                {
                    return new RollCapacityInfo
                    {
                        IsAvailable = false,
                        Source = "Driver",
                        Status = "Invalid",
                        Details = "Printer settings invalid"
                    };
                }

                var result = new RollCapacityInfo
                {
                    Source = "Driver",
                    Status = "Unknown"
                };

                // Check if printer has custom properties we can access
                // Note: Most standard .NET printer APIs don't expose paper level
                // This is a placeholder for driver-specific implementations
                
                // Some drivers might expose this through:
                // - PrinterSettings.DefaultPageSettings (paper size info, but not level)
                // - Custom printer properties (requires driver-specific implementation)
                // - Printer driver SDK (if DNP provides one)

                result.Details = "Standard driver properties do not expose paper level";
                result.IsAvailable = false;
                return result;
            }
            catch (Exception ex)
            {
                return new RollCapacityInfo
                {
                    IsAvailable = false,
                    Source = "Driver",
                    Status = "Error",
                    Details = $"Driver query exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Print an image file to the selected printer
        /// Uses System.Drawing.Printing.PrintDocument to send image to printer
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printdocument
        /// </summary>
        /// <param name="imagePath">Full path to the image file to print</param>
        /// <param name="copies">Number of copies to print (default: 1)</param>
        /// <param name="paperSizeInches">Paper size in inches as (width, height) - default is 6x4 inches (landscape)</param>
        /// <param name="imagesPerPage">Number of images to print per page (for strips: 2 side-by-side, for 4x6: 1)</param>
        /// <param name="waitForCompletion">If true, waits for the print job to actually complete using print queue monitoring (default: false)</param>
        /// <returns>Tuple containing success status, actual print time in seconds, and optional error message</returns>
        public async Task<(bool success, double printTimeSeconds, string? errorMessage)> PrintImageAsync(string imagePath, int copies = 1, (float width, float height)? paperSizeInches = null, int imagesPerPage = 1, bool waitForCompletion = false)
        {
            LoggingService.Hardware.Information("Printer", "PrintImageAsync called",
                ("ImagePath", imagePath),
                ("Copies", copies),
                ("PaperSize", $"{paperSizeInches?.width ?? 6.0f}x{paperSizeInches?.height ?? 4.0f}"),
                ("ImagesPerPage", imagesPerPage),
                ("SelectedPrinter", _selectedPrinterName ?? "None"));
            
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                Console.WriteLine("!!! ERROR: Image path is null or empty !!!");
                LoggingService.Hardware.Warning("Printer", "Attempted to print with null or empty image path");
                return (false, 0.0, "Image path is empty or invalid.");
            }

            if (!File.Exists(imagePath))
            {
                LoggingService.Hardware.Warning("Printer", "Image file does not exist",
                    ("ImagePath", imagePath));
                return (false, 0.0, "Print image file not found.");
            }

            if (copies < 1)
            {
                LoggingService.Hardware.Warning("Printer", "Invalid number of copies requested",
                    ("Copies", copies));
                copies = 1;
            }

            // Ensure we have a selected printer
            if (string.IsNullOrWhiteSpace(_selectedPrinterName))
            {
                _selectedPrinterName = GetDefaultPrinterName();
                if (string.IsNullOrWhiteSpace(_selectedPrinterName))
                {
                    LoggingService.Hardware.Error("Printer", "No printer selected and no default printer available");
                    return (false, 0.0, "No printer is selected. Please select a printer and try again.");
                }
            }

            // Start timing the print operation
            var startTime = DateTime.UtcNow;

            return await Task.Run(async () =>
            {
                try
                {
                    // Load the image from file
                    Image? imageToPrint = null;
                    try
                    {
                        imageToPrint = Image.FromFile(imagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"!!! ERROR loading image: {ex.Message} !!!");
                        LoggingService.Hardware.Error("Printer", "Failed to load image file for printing", ex,
                            ("ImagePath", imagePath));
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, elapsed, "Could not load print image.");
                    }

                    if (imageToPrint == null)
                    {
                        LoggingService.Hardware.Error("Printer", "Image loaded as null", null,
                            ("ImagePath", imagePath));
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, elapsed, "Print image was empty.");
                    }

                    // Check printer connectivity before creating the document
                    // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.isoffline
                    if (IsPrinterOffline(_selectedPrinterName))
                    {
                        // Cancel any existing queued jobs to prevent them from printing when printer comes back online
                        int cancelledJobs = CancelAllPrintJobs(_selectedPrinterName);
                        
                        LoggingService.Hardware.Warning("Printer", "Printer appears to be offline, aborting print job",
                            ("PrinterName", _selectedPrinterName),
                            ("CancelledQueuedJobs", cancelledJobs));
                        Console.WriteLine($"!!! PRINTER OFFLINE: Aborting print job before sending to spooler. Cancelled {cancelledJobs} queued job(s). !!!");
                        var offlineElapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, offlineElapsed, "Printer appears to be offline. Please power on the printer and try again.");
                    }

                    // Create PrintDocument
                    using var printDocument = new PrintDocument();
                    
                    // Set printer name
                    printDocument.PrinterSettings.PrinterName = _selectedPrinterName;
                    
                    // Verify printer is valid
                    if (!printDocument.PrinterSettings.IsValid)
                    {
                        Console.WriteLine($"!!! ERROR: Printer '{_selectedPrinterName}' is not valid !!!");
                        LoggingService.Hardware.Error("Printer", "Selected printer is not valid", null,
                            ("PrinterName", _selectedPrinterName));
                        imageToPrint.Dispose();
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, elapsed, $"Printer '{_selectedPrinterName}' is not ready or reachable.");
                    }
                    // CRITICAL: Do NOT set PrinterSettings.Copies when handling copies manually via PrintPage handler
                    // Setting PrinterSettings.Copies would multiply the entire print job, causing duplicates
                    // Instead, we handle copies through the PrintPage event handler by printing multiple pages
                    // Always set to 1 and let PrintPage handler manage the page count
                    printDocument.PrinterSettings.Copies = 1;
                    
                    // Set print quality to high
                    printDocument.DefaultPageSettings.PrinterResolution.Kind = PrinterResolutionKind.High;
                    
                    // Paper is always 6x4 inches (landscape: 6" wide x 4" tall) for DNP printer
                    // Paper sizes in .NET are specified in hundredths of an inch
                    var paperWidth = paperSizeInches?.width ?? 6.0f; // Default: 6 inches wide
                    var paperHeight = paperSizeInches?.height ?? 4.0f; // Default: 4 inches tall
                    int paperWidthHundredths = (int)(paperWidth * 100); // Convert to hundredths
                    int paperHeightHundredths = (int)(paperHeight * 100); // Convert to hundredths
                    
                    // CRITICAL: DNP printer requires exactly 6x4 inches to print
                    // Try to find an existing paper size that matches, or create custom
                    // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.papersize
                    PaperSize? selectedPaperSize = null;
                    var printerSettings = printDocument.PrinterSettings;
                    
                    // First, try to find a matching paper size in the printer's available sizes
                    // CRITICAL: DNP printer has built-in (6x4) paper size that we should use
                    foreach (PaperSize ps in printerSettings.PaperSizes)
                    {
                        float psWidthInches = ps.Width / 100.0f;
                        float psHeightInches = ps.Height / 100.0f;
                        
                        // CRITICAL: Prioritize exact "6x4" name match - this is the correct paper size for DNP printer
                        // Check for "(6x4)" first (exact match), then "6x4" in name, then dimension matches
                        bool exactNameMatch = ps.PaperName.Contains("(6x4)", StringComparison.OrdinalIgnoreCase);
                        bool nameContains6x4 = ps.PaperName.Contains("6x4", StringComparison.OrdinalIgnoreCase);
                        
                        // Check if dimensions match 6x4 inches (600x400 hundredths) in landscape orientation
                        // DNP printer's (6x4) is 615x413 hundredths, which is within 0.2" tolerance
                        // For landscape 6x4, we want width > height (615 > 413)
                        bool matchesLandscape = ps.Width > ps.Height && 
                                              Math.Abs(ps.Width - paperWidthHundredths) < 20 && 
                                              Math.Abs(ps.Height - paperHeightHundredths) < 20;
                        
                        // Prioritize exact name match, then name contains 6x4, then landscape dimension match
                        // Skip "PR (4x6)" which is portrait 4x6, not landscape 6x4
                        if (exactNameMatch)
                        {
                            selectedPaperSize = ps;
                            break;
                        }
                        else if (nameContains6x4 && matchesLandscape)
                        {
                            selectedPaperSize = ps;
                            break;
                        }
                        else if (matchesLandscape && !ps.PaperName.Contains("4x6", StringComparison.OrdinalIgnoreCase))
                        {
                            // Landscape dimension match, but exclude 4x6 sizes
                            selectedPaperSize = ps;
                            break;
                        }
                    }
                    
                    // If no match found, create custom paper size
                    if (selectedPaperSize == null)
                    {
                        var paperSizeName = $"Custom {paperWidth}x{paperHeight}";
                        selectedPaperSize = new PaperSize(paperSizeName, paperWidthHundredths, paperHeightHundredths)
                        {
                            RawKind = (int)PaperKind.Custom
                        };
                        Console.WriteLine($"Created custom paper size: {selectedPaperSize.PaperName} ({selectedPaperSize.Width}x{selectedPaperSize.Height} hundredths)");
                    }
                    
                    // Set the paper size on both DefaultPageSettings and PrinterSettings
                    // Some printer drivers require it to be set on both
                    printDocument.DefaultPageSettings.PaperSize = selectedPaperSize;
                    printerSettings.DefaultPageSettings.PaperSize = selectedPaperSize;
                    Console.WriteLine($"Paper size set: {selectedPaperSize.PaperName} ({selectedPaperSize.Width}x{selectedPaperSize.Height} hundredths)");
                    
                    // Set margins to 0 to fill entire paper
                    printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                    printerSettings.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                    Console.WriteLine($"Margins set to 0 (full bleed)");
                    
                    // Always landscape for 6x4 paper
                    bool isLandscape = paperWidth > paperHeight;
                    printDocument.DefaultPageSettings.Landscape = isLandscape;
                    printerSettings.DefaultPageSettings.Landscape = isLandscape;
                    Console.WriteLine($"Orientation: {(isLandscape ? "Landscape" : "Portrait")}");
                    
                    // Verify paper size was set correctly
                    var actualPaperSize = printDocument.DefaultPageSettings.PaperSize;
                    
                    // Track current page and total images printed
                    int currentPage = 0;
                    int totalImagesPrinted = 0;
                    bool printSuccess = false;

                    // Handle PrintPage event to draw the image(s)
                    // Note: For strips, "copies" means number of copies (each copy = 2 images side-by-side)
                    //       So if copies=1 and imagesPerPage=2, we need to print 2 images (1 copy = 2 images)
                    //       Total images needed = copies * imagesPerPage
                    int totalImagesNeeded = copies * imagesPerPage;
                    
                    printDocument.PrintPage += (sender, e) =>
                    {
                        try
                        {
                            // Set high-quality rendering
                            if (e.Graphics != null)
                            {
                                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            }
                            
                            // CRITICAL: Use PageBounds directly - this is the actual printable area the printer driver provides
                            // The Graphics object draws in the PageBounds coordinate system
                            // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printpageeventargs.pagebounds
                            var pageBounds = e.PageBounds;
                            var pageSettings = e.PageSettings;
                            bool isLandscape = pageSettings.Landscape;
                            
                            // Get Graphics DPI for logging/debugging
                            float dpiX = e.Graphics?.DpiX ?? 300f;
                            float dpiY = e.Graphics?.DpiY ?? 300f;
                            
                            // Get paper size for logging
                            var paperSize = pageSettings.PaperSize;
                            float paperWidthInches = paperSize.Width / 100.0f;
                            float paperHeightInches = paperSize.Height / 100.0f;
                            
                            // Use PageBounds directly - this is what the Graphics object uses for drawing
                            float pageWidth = pageBounds.Width;
                            float pageHeight = pageBounds.Height;
                            
                            float imageWidth = imageToPrint.Width;
                            float imageHeight = imageToPrint.Height;
                            
                            if (imagesPerPage == 1)
                            {
                                // Single image per page (4x6 photos) - fill entire page
                                
                                // CRITICAL: Check if PageBounds orientation matches expected landscape orientation
                                // Some printer drivers report portrait PageBounds even when Landscape=true
                                // For 6x4 landscape paper, we expect width > height in PageBounds
                                bool pageBoundsIsPortrait = pageBounds.Height > pageBounds.Width;
                                bool imageIsLandscape = imageWidth > imageHeight;
                                bool needsRotation = pageBoundsIsPortrait && imageIsLandscape && isLandscape;
                                
                                // If PageBounds is portrait but we want landscape, we need to rotate the drawing
                                // Use the swapped dimensions for calculations
                                float effectivePageWidth = needsRotation ? pageHeight : pageWidth;
                                float effectivePageHeight = needsRotation ? pageWidth : pageHeight;
                                
                                // Calculate scaling to fill entire page while maintaining aspect ratio
                                float scaleX = effectivePageWidth / imageWidth;
                                float scaleY = effectivePageHeight / imageHeight;
                                float scale = Math.Max(scaleX, scaleY); // Use larger scale to fill page
                                
                                // Calculate scaled dimensions
                                int scaledWidth = (int)(imageWidth * scale);
                                int scaledHeight = (int)(imageHeight * scale);
                                
                                if (needsRotation && e.Graphics != null)
                                {
                                    // Save the current graphics state
                                    var state = e.Graphics.Save();
                                    
                                    // Rotate 90 degrees clockwise around the center of the page
                                    // Translate to center, rotate, then translate back
                                    e.Graphics.TranslateTransform(pageBounds.Width / 2f, pageBounds.Height / 2f);
                                    e.Graphics.RotateTransform(90f);
                                    e.Graphics.TranslateTransform(-pageBounds.Height / 2f, -pageBounds.Width / 2f);
                                    
                                    // Center the image on the rotated page
                                    int x = (int)((pageBounds.Height - scaledWidth) / 2);
                                    int y = (int)((pageBounds.Width - scaledHeight) / 2);
                                    
                                    // Draw the image
                                    e.Graphics.DrawImage(imageToPrint, x, y, scaledWidth, scaledHeight);
                                    
                                    // Restore the graphics state
                                    e.Graphics.Restore(state);
                                }
                                else
                                {
                                    // Center the image on the page (no rotation needed)
                                    int x = pageBounds.X + (pageBounds.Width - scaledWidth) / 2;
                                    int y = pageBounds.Y + (pageBounds.Height - scaledHeight) / 2;
                                    
                                    // Draw the image
                                    e.Graphics?.DrawImage(imageToPrint, x, y, scaledWidth, scaledHeight);
                                }
                                
                                totalImagesPrinted++;
                            }
                            else
                            {
                                // Multiple images per page (strips) - print side-by-side
                                
                                // Add small top margin only to push images down slightly
                                // 0.05 inches = ~15 pixels at 300 DPI
                                float topMargin = dpiY * 0.05f;
                                
                                // Calculate width per image (divide page width by number of images)
                                float imageAreaWidth = pageWidth / imagesPerPage;
                                float imageAreaHeight = pageHeight; // Full height available
                                
                                // Draw images side-by-side on this page
                                int imagesToDrawOnThisPage = Math.Min(imagesPerPage, totalImagesNeeded - totalImagesPrinted);
                                
                                for (int i = 0; i < imagesToDrawOnThisPage; i++)
                                {
                                    // Calculate position for this image
                                    float imageX = pageBounds.X + (i * imageAreaWidth);
                                    
                                    // Calculate scaling to fit within this image's area
                                    // Use smaller scale to ensure image fits without being cut off
                                    float scaleX = imageAreaWidth / imageWidth;
                                    float scaleY = imageAreaHeight / imageHeight;
                                    float scale = Math.Min(scaleX, scaleY);
                                    
                                    // Calculate scaled dimensions
                                    int scaledWidth = (int)(imageWidth * scale);
                                    int scaledHeight = (int)(imageHeight * scale);
                                    
                                    // Center horizontally within the image area
                                    float offsetX = (imageAreaWidth - scaledWidth) / 2;
                                    
                                    // Center vertically within the page bounds, then add top margin
                                    float offsetY = (imageAreaHeight - scaledHeight) / 2;
                                    
                                    int x = (int)(imageX + offsetX);
                                    int y = (int)(pageBounds.Y + topMargin + offsetY);
                                    
                                    // Draw the image
                                    e.Graphics?.DrawImage(imageToPrint, x, y, scaledWidth, scaledHeight);
                                    totalImagesPrinted++;
                                }
                            }
                            
                            currentPage++;
                            
                            // Check if we need to print more pages
                            if (totalImagesPrinted < totalImagesNeeded)
                            {
                                e.HasMorePages = true; // Signal that there are more pages to print
                            }
                            else
                            {
                                e.HasMorePages = false; // No more pages
                                printSuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"!!! ERROR in PrintPage event: {ex.Message} !!!");
                            Console.WriteLine($"Stack trace: {ex.StackTrace}");
                            LoggingService.Hardware.Error("Printer", "Error during PrintPage event", ex,
                                ("CurrentPage", currentPage),
                                ("TotalImagesPrinted", totalImagesPrinted),
                                ("TotalImagesNeeded", totalImagesNeeded),
                                ("ImagesPerPage", imagesPerPage));
                            e.Cancel = true; // Cancel the print job
                        }
                    };

                    // Get job count before printing (for monitoring completion)
                    int jobCountBefore = 0;
                    if (waitForCompletion && !string.IsNullOrWhiteSpace(_selectedPrinterName))
                    {
                        try
                        {
                            using var printServer = new LocalPrintServer();
                            using var printQueue = printServer.GetPrintQueue(_selectedPrinterName);
                            printQueue.Refresh();
                            jobCountBefore = printQueue.NumberOfJobs;
                        }
                        catch (Exception ex)
                        {
                            LoggingService.Hardware.Warning("Printer", "Could not get job count before printing, completion monitoring may not work",
                                ("Exception", ex.Message));
                        }
                    }

                    // Print the document - measure actual print time
                    Console.WriteLine($"!!! ABOUT TO CALL printDocument.Print() !!!");
                    Console.WriteLine($"!!! PrinterName: {_selectedPrinterName} !!!");
                    Console.WriteLine($"!!! PaperSize: {printDocument.DefaultPageSettings.PaperSize?.PaperName ?? "NULL"} !!!");
                    Console.WriteLine($"!!! Landscape: {printDocument.DefaultPageSettings.Landscape} !!!");
                    Console.WriteLine($"!!! TotalImagesNeeded: {totalImagesNeeded} !!!");
                    
                    try
                    {
                        printDocument.Print();
                        Console.WriteLine($"!!! printDocument.Print() CALLED SUCCESSFULLY - Job should be in queue now !!!");
                    }
                    catch (Exception printEx)
                    {
                        Console.WriteLine($"!!! EXCEPTION during printDocument.Print(): {printEx.Message} !!!");
                        Console.WriteLine($"!!! StackTrace: {printEx.StackTrace} !!!");
                        LoggingService.Hardware.Error("Printer", "Exception during Print() call", printEx);
                        imageToPrint.Dispose();
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, elapsed, "Printing failed while monitoring print job.");
                    }
                    
                    // Check if printer went offline immediately after queuing the job
                    // If so, cancel the job to prevent it from printing when printer comes back online
                    if (IsPrinterOffline(_selectedPrinterName))
                    {
                        int cancelledJobs = CancelAllPrintJobs(_selectedPrinterName);
                        Console.WriteLine($"!!! PRINTER WENT OFFLINE IMMEDIATELY AFTER QUEUING JOB - Cancelled {cancelledJobs} job(s) !!!");
                        LoggingService.Hardware.Warning("Printer", "Printer went offline immediately after queuing print job, cancelled queued jobs",
                            ("PrinterName", _selectedPrinterName),
                            ("CancelledQueuedJobs", cancelledJobs));
                        imageToPrint.Dispose();
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        return (false, elapsed, "Printer went offline after queuing print job. Please power on the printer and try again.");
                    }
                    
                    // If waitForCompletion is true, monitor the print queue until job completes
                    if (waitForCompletion && !string.IsNullOrWhiteSpace(_selectedPrinterName))
                    {
                    // Calculate total pages needed and estimated print time
                    // For strips: copies=5, imagesPerPage=2 means 3 pages (ceil(5/2))
                    // For 4x6: copies=5, imagesPerPage=1 means 5 pages (ceil(5/1))
                    int totalPagesNeeded = (int)Math.Ceiling((double)copies / imagesPerPage);
                    
                    // Calculate estimated print time using the same logic as PrintingScreen
                    // This will be used as a minimum wait time to ensure we don't finish too early
                    double estimatedPrintTimeSeconds;
                    if (totalPagesNeeded == 1)
                    {
                        estimatedPrintTimeSeconds = 18.0; // Single page: 18 seconds
                    }
                    else
                    {
                        // Progressive scaling: time per page increases as total pages increase
                        double totalTime = 0.0;
                        for (int page = 1; page <= totalPagesNeeded; page++)
                        {
                            double timeForThisPage;
                            if (page <= 2)
                            {
                                timeForThisPage = 25.0; // First 2 pages: 25 seconds per page
                            }
                            else if (page <= 4)
                            {
                                timeForThisPage = 28.0; // Pages 3-4: 28 seconds per page
                            }
                            else
                            {
                                timeForThisPage = 32.0; // Pages 5+: 32 seconds per page
                            }
                            totalTime += timeForThisPage;
                        }
                        // Add buffer for page transitions (2 seconds per transition)
                        estimatedPrintTimeSeconds = totalTime + ((totalPagesNeeded - 1) * 2.0);
                    }
                    
                    Console.WriteLine($"!!! WAIT FOR COMPLETION ENABLED - Starting monitoring !!!");
                    Console.WriteLine($"!!! PrinterName: {_selectedPrinterName}, JobCountBefore: {jobCountBefore}, TotalPages: {totalPagesNeeded}, EstimatedTime: {estimatedPrintTimeSeconds}s !!!");
                    try
                    {
                        await WaitForPrintJobCompletionAsync(_selectedPrinterName, jobCountBefore, totalPagesNeeded, estimatedPrintTimeSeconds);
                        Console.WriteLine($"!!! Print queue monitoring completed successfully !!!");
                    }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"!!! ERROR in print queue monitoring: {ex.Message} !!!");
                            Console.WriteLine($"!!! Stack trace: {ex.StackTrace} !!!");
                            LoggingService.Hardware.Warning("Printer", "Error monitoring print job completion, continuing anyway",
                                ("Exception", ex.Message),
                                ("StackTrace", ex.StackTrace ?? "N/A"));
                        }
                    }
                    else
                    {
                        Console.WriteLine($"!!! WaitForCompletion is FALSE or printer name is empty !!!");
                        Console.WriteLine($"!!! waitForCompletion={waitForCompletion}, PrinterName={_selectedPrinterName} !!!");
                    }
                    
                    // Calculate actual print time
                    var printTime = (DateTime.UtcNow - startTime).TotalSeconds;
                    
                    // Dispose of the image
                    imageToPrint.Dispose();
                    
                    if (printSuccess)
                    {
                        LoggingService.Hardware.Information("Printer", "Print job completed successfully",
                            ("PrinterName", _selectedPrinterName),
                            ("ImagePath", imagePath),
                            ("Copies", copies),
                            ("PrintTimeSeconds", printTime));
                        return (true, printTime, (string?)null);
                    }
                    else
                    {
                        Console.WriteLine("!!! PRINT JOB FAILED (printSuccess = false) !!!");
                        LoggingService.Hardware.Warning("Printer", "Print job may not have completed successfully",
                            ("PrinterName", _selectedPrinterName),
                            ("Copies", copies),
                            ("PrintTimeSeconds", printTime));
                        return (false, printTime, "Print job did not complete successfully.");
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    LoggingService.Hardware.Error("Printer", "Failed to print image", ex,
                        ("ImagePath", imagePath),
                        ("PrinterName", _selectedPrinterName),
                        ("Copies", copies),
                        ("PrintTimeSeconds", elapsed));
                    return (false, elapsed, $"Printing failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Wait for print job to complete by monitoring the print queue
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing
        /// 
        /// IMPORTANT: Windows print queue removes jobs when they're sent to the printer, not when printing finishes.
        /// Therefore, we use the estimated print time as a minimum wait to ensure we don't finish too early.
        /// </summary>
        /// <param name="printerName">Name of the printer</param>
        /// <param name="jobCountBefore">Number of jobs in queue before submitting our job</param>
        /// <param name="totalPagesNeeded">Total number of pages being printed</param>
        /// <param name="estimatedPrintTimeSeconds">Estimated print time in seconds (used as minimum wait time)</param>
        private async Task WaitForPrintJobCompletionAsync(string printerName, int jobCountBefore, int totalPagesNeeded, double estimatedPrintTimeSeconds)
        {
            await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"!!! PRINT QUEUE MONITORING STARTED !!!");
                    Console.WriteLine($"!!! PrinterName: {printerName}, JobCountBefore: {jobCountBefore}, TotalPages: {totalPagesNeeded}, EstimatedTime: {estimatedPrintTimeSeconds}s !!!");
                    
                    LoggingService.Hardware.Information("Printer", "Starting print queue monitoring",
                        ("PrinterName", printerName),
                        ("JobCountBefore", jobCountBefore),
                        ("TotalPages", totalPagesNeeded),
                        ("EstimatedPrintTimeSeconds", estimatedPrintTimeSeconds));
                    
                    using var printServer = new LocalPrintServer();
                    using var printQueue = printServer.GetPrintQueue(printerName);
                    
                    Console.WriteLine($"!!! Print queue retrieved successfully !!!");
                    
                    // Wait a moment for the job to appear in the queue
                    Thread.Sleep(500);
                    
                    const int maxWaitTimeSeconds = 300; // Maximum 5 minutes
                    const int pollIntervalMs = 1000; // Check every second
                    const int hardwareStatusPollIntervalMs = 2000; // Poll hardware status every 2 seconds after job disappears
                    
                    // Calculate minimum wait time based on estimated print time
                    // This ensures we don't check WMI status too early (printer needs time to actually start printing)
                    // For single page: use 60% of estimate (printer often finishes faster)
                    // For multiple pages: use 70% of estimate (more conservative for longer jobs)
                    int minimumWaitSeconds;
                    if (totalPagesNeeded == 1)
                    {
                        minimumWaitSeconds = (int)Math.Ceiling(estimatedPrintTimeSeconds * 0.6); // 60% for single page
                    }
                    else
                    {
                        minimumWaitSeconds = (int)Math.Ceiling(estimatedPrintTimeSeconds * 0.7); // 70% for multiple pages
                    }
                    // Ensure minimum is at least 10 seconds (safety floor)
                    minimumWaitSeconds = Math.Max(minimumWaitSeconds, 10);
                    
                    // Maximum wait is estimated time + small buffer (safety ceiling)
                    int maximumWaitSeconds = (int)Math.Ceiling(estimatedPrintTimeSeconds) + 10;
                    
                    Console.WriteLine($"!!! Wait time strategy: Minimum={minimumWaitSeconds}s (before WMI checks), Maximum={maximumWaitSeconds}s (estimated: {estimatedPrintTimeSeconds}s) !!!");
                    int elapsedSeconds = 0;
                    int? trackedJobId = null;
                    string? trackedJobName = null;
                    int secondsSinceJobDisappeared = 0;
                    bool jobWasFound = false;
                    
                    // Require multiple consecutive idle checks to avoid false positives
                    // PrinterStatus=3 can be reported while printer is still physically printing
                    // For single page: require 2 consecutive idle checks (2 seconds)
                    // For multiple pages: require 4 consecutive idle checks (8 seconds with 2s polling)
                    int requiredConsecutiveIdleChecks = totalPagesNeeded == 1 ? 2 : 4;
                    int consecutiveIdleChecks = 0;
                    int lastIdleCheckSecond = -1;
                    
                   int offlineDetectionCounter = 0;

                   while (elapsedSeconds < maxWaitTimeSeconds)
                    {
                        printQueue.Refresh();
                        var currentJobCount = printQueue.NumberOfJobs;

                       // Detect if the printer went offline while waiting
                       if (printQueue.IsOffline || IsPrinterOffline(printerName))
                       {
                           offlineDetectionCounter++;
                           if (offlineDetectionCounter >= 5) // ~5 seconds of continuous offline reporting
                           {
                               // Cancel all queued jobs to prevent them from printing when printer comes back online
                               int cancelledJobs = CancelAllPrintJobs(printerName);
                               
                               Console.WriteLine($"!!! PRINTER OFFLINE DURING MONITORING - aborting wait. Cancelled {cancelledJobs} queued job(s). !!!");
                               LoggingService.Hardware.Warning("Printer", "Printer reported offline during job monitoring, cancelled queued jobs",
                                   ("PrinterName", printerName),
                                   ("ElapsedSeconds", elapsedSeconds),
                                   ("CancelledQueuedJobs", cancelledJobs));
                               throw new InvalidOperationException("Printer went offline during print job monitoring.");
                           }
                       }
                       else
                       {
                           offlineDetectionCounter = 0;
                       }
                        
                        // Log every 5 seconds to track progress
                        if (elapsedSeconds % 5 == 0)
                        {
                            Console.WriteLine($"!!! Monitoring: Elapsed={elapsedSeconds}s, JobsBefore={jobCountBefore}, CurrentJobs={currentJobCount}, TrackedJobId={trackedJobId} !!!");
                        }
                        
                        // Get all jobs in the queue
                        var jobs = printQueue.GetPrintJobInfoCollection();
                        bool foundOurJob = false;
                        PrintSystemJobInfo? ourJob = null;
                        
                        // Look for our job (either by tracking ID or by being a new job)
                        foreach (PrintSystemJobInfo job in jobs)
                        {
                            // If we're tracking a specific job, look for it
                            if (trackedJobId.HasValue && job.JobIdentifier == trackedJobId.Value)
                            {
                                ourJob = job;
                                foundOurJob = true;
                                jobWasFound = true;
                                secondsSinceJobDisappeared = 0; // Reset counter since job is still there
                                break;
                            }
                            // If we haven't tracked a job yet, and this is a new job (count increased), track it
                            else if (!trackedJobId.HasValue && currentJobCount > jobCountBefore)
                            {
                                trackedJobId = job.JobIdentifier;
                                trackedJobName = job.Name;
                                ourJob = job;
                                foundOurJob = true;
                                jobWasFound = true;
                                Console.WriteLine($"!!! Tracking print job: ID={trackedJobId}, Name={trackedJobName} !!!");
                                break;
                            }
                        }
                        
                        // If we found our tracked job, check its status
                        if (foundOurJob && ourJob != null)
                        {
                            if (ourJob.IsCompleted)
                            {
                                Console.WriteLine($"!!! PRINT JOB COMPLETED (IsCompleted=true) !!!");
                                LoggingService.Hardware.Information("Printer", "Print job completed",
                                    ("PrinterName", printerName),
                                    ("JobId", ourJob.JobIdentifier),
                                    ("JobName", ourJob.Name),
                                    ("ElapsedSeconds", elapsedSeconds));
                                return;
                            }
                            if (ourJob.IsInError)
                            {
                                Console.WriteLine($"!!! PRINT JOB ERROR !!!");
                                LoggingService.Hardware.Warning("Printer", "Print job encountered an error",
                                    ("PrinterName", printerName),
                                    ("JobId", ourJob.JobIdentifier),
                                    ("JobName", ourJob.Name));
                                return;
                            }
                        }
                        // If we were tracking a job but it's no longer in the queue, start checking hardware status
                        // Windows removes jobs from the queue when sent to printer, not when printing finishes
                        // So we poll the printer hardware status via WMI to detect when it's actually idle
                        else if (trackedJobId.HasValue && !foundOurJob)
                        {
                            secondsSinceJobDisappeared++;
                            
                            // Reset consecutive idle counter when we first start checking (job just disappeared)
                            // This ensures we start fresh for each job
                            if (secondsSinceJobDisappeared == 1)
                            {
                                consecutiveIdleChecks = 0;
                                lastIdleCheckSecond = -1;
                                Console.WriteLine($"!!! Job {trackedJobId} disappeared from queue, will start hardware status monitoring after brief delay !!!");
                            }
                            
                            // Wait for minimum time before checking hardware status
                            // This ensures the printer has had time to actually start and process the print job
                            // After minimum time, we'll check WMI status and require consecutive idle checks
                            if (elapsedSeconds < minimumWaitSeconds)
                            {
                                // Still waiting for minimum time - don't check WMI yet
                                if (elapsedSeconds % 5 == 0)
                                {
                                    Console.WriteLine($"!!! Job {trackedJobId} disappeared, waiting minimum time: {elapsedSeconds}/{minimumWaitSeconds}s before WMI checks !!!");
                                }
                            }
                            else
                            {
                                // Minimum time passed - start checking hardware status
                                // Poll printer hardware status via WMI to see if it's actually idle
                                // Note: PrinterStatus=3 can be reported while printer is still physically printing,
                                // so we require multiple consecutive idle checks to confirm it's truly done
                                bool isPrinterIdle = IsPrinterIdleViaWMI(printerName);
                                
                                if (isPrinterIdle)
                                {
                                    // Only count consecutive idle checks if we're on a new polling interval
                                    // (hardwareStatusPollIntervalMs = 2000ms, so every 2 seconds)
                                    if (elapsedSeconds != lastIdleCheckSecond)
                                    {
                                        consecutiveIdleChecks++;
                                        lastIdleCheckSecond = elapsedSeconds;
                                        Console.WriteLine($"!!! Printer idle check #{consecutiveIdleChecks}/{requiredConsecutiveIdleChecks} at {elapsedSeconds}s !!!");
                                    }
                                    
                                    // Only return if we have enough consecutive idle checks
                                    if (consecutiveIdleChecks >= requiredConsecutiveIdleChecks)
                                    {
                                        Console.WriteLine($"!!! PRINT JOB COMPLETED (printer hardware idle for {consecutiveIdleChecks} consecutive checks) !!!");
                                        Console.WriteLine($"!!! Elapsed: {elapsedSeconds}s, Disappeared: {secondsSinceJobDisappeared}s ago !!!");
                                        LoggingService.Hardware.Information("Printer", "Print job completed (printer hardware idle)",
                                            ("PrinterName", printerName),
                                            ("JobId", trackedJobId),
                                            ("JobName", trackedJobName ?? "Unknown"),
                                            ("ElapsedSeconds", elapsedSeconds),
                                            ("SecondsSinceDisappeared", secondsSinceJobDisappeared),
                                            ("ConsecutiveIdleChecks", consecutiveIdleChecks));
                                        return;
                                    }
                                    else
                                    {
                                        // Printer is idle but need more consecutive checks
                                        if (elapsedSeconds % 5 == 0)
                                        {
                                            Console.WriteLine($"!!! Job {trackedJobId} disappeared, printer idle but need {requiredConsecutiveIdleChecks - consecutiveIdleChecks} more consecutive checks: {elapsedSeconds}s elapsed !!!");
                                        }
                                    }
                                }
                                else
                                {
                                    // Printer is still busy, reset consecutive idle counter
                                    if (consecutiveIdleChecks > 0)
                                    {
                                        Console.WriteLine($"!!! Printer became busy again, resetting idle counter (was at {consecutiveIdleChecks}/{requiredConsecutiveIdleChecks}) !!!");
                                        consecutiveIdleChecks = 0;
                                        lastIdleCheckSecond = -1;
                                    }
                                    
                                    // Log every 5 seconds to track progress
                                    if (elapsedSeconds % 5 == 0)
                                    {
                                        Console.WriteLine($"!!! Job {trackedJobId} disappeared, printer still busy: {elapsedSeconds}s elapsed (max: {maximumWaitSeconds}s) !!!");
                                    }
                                }
                            }
                            
                            // Safety check: if we've exceeded maximum wait time, return anyway
                            if (elapsedSeconds >= maximumWaitSeconds)
                            {
                                Console.WriteLine($"!!! PRINT JOB COMPLETED (maximum wait time reached) !!!");
                                Console.WriteLine($"!!! Elapsed: {elapsedSeconds}s, Maximum: {maximumWaitSeconds}s !!!");
                                LoggingService.Hardware.Warning("Printer", "Print job monitoring reached maximum wait time",
                                    ("PrinterName", printerName),
                                    ("JobId", trackedJobId),
                                    ("JobName", trackedJobName ?? "Unknown"),
                                    ("ElapsedSeconds", elapsedSeconds),
                                    ("MaximumWaitSeconds", maximumWaitSeconds));
                                return;
                            }
                            
                            // Use longer poll interval when checking hardware status (less frequent WMI queries)
                            Thread.Sleep(hardwareStatusPollIntervalMs);
                            elapsedSeconds += (hardwareStatusPollIntervalMs / 1000);
                            continue; // Skip the normal sleep at end of loop
                        }
                        // If we never found a job and count is back to original, check hardware status
                        // This handles edge cases where the job was processed very quickly
                        else if (!jobWasFound && currentJobCount <= jobCountBefore)
                        {
                            // Check hardware status immediately (no minimum wait)
                            // Require multiple consecutive idle checks even for untracked jobs
                            bool isPrinterIdle = IsPrinterIdleViaWMI(printerName);
                            
                            if (isPrinterIdle)
                            {
                                // Only count consecutive idle checks if we're on a new polling interval
                                if (elapsedSeconds != lastIdleCheckSecond)
                                {
                                    consecutiveIdleChecks++;
                                    lastIdleCheckSecond = elapsedSeconds;
                                }
                                
                                if (consecutiveIdleChecks >= requiredConsecutiveIdleChecks)
                                {
                                    Console.WriteLine($"!!! WARNING: No job found in queue, but printer is idle ({consecutiveIdleChecks} consecutive checks) !!!");
                                    Console.WriteLine($"!!! Elapsed: {elapsedSeconds}s !!!");
                                    LoggingService.Hardware.Warning("Printer", "Print job may have completed but was not tracked, printer is idle",
                                        ("PrinterName", printerName),
                                        ("ElapsedSeconds", elapsedSeconds),
                                        ("ConsecutiveIdleChecks", consecutiveIdleChecks));
                                    return;
                                }
                            }
                            else
                            {
                                // Reset counter if printer becomes busy
                                consecutiveIdleChecks = 0;
                                lastIdleCheckSecond = -1;
                            }
                            
                            if (elapsedSeconds >= maximumWaitSeconds)
                            {
                                // Maximum wait reached, return anyway
                                Console.WriteLine($"!!! WARNING: No job found in queue, maximum wait time reached !!!");
                                Console.WriteLine($"!!! Elapsed: {elapsedSeconds}s, Maximum: {maximumWaitSeconds}s !!!");
                                LoggingService.Hardware.Warning("Printer", "Print job monitoring reached maximum wait time (no job tracked)",
                                    ("PrinterName", printerName),
                                    ("ElapsedSeconds", elapsedSeconds),
                                    ("MaximumWaitSeconds", maximumWaitSeconds));
                                return;
                            }
                        }
                        
                        Thread.Sleep(pollIntervalMs);
                        elapsedSeconds++;
                    }
                    
                    LoggingService.Hardware.Warning("Printer", "Timeout waiting for print job completion",
                        ("PrinterName", printerName),
                        ("MaxWaitTimeSeconds", maxWaitTimeSeconds));
                }
                catch (Exception ex)
                {
                    LoggingService.Hardware.Error("Printer", "Error monitoring print job completion", ex,
                        ("PrinterName", printerName));
                    throw;
                }
            });
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            // No unmanaged resources to dispose, but implement for interface compliance
            _selectedPrinterName = null;
            _isInitialized = false;
            
            LoggingService.Hardware.Information("Printer", "PrinterService disposed");
        }

        #endregion
    }
}

