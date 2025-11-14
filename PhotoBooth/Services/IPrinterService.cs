using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for printer operations to enable mocking and testing
    /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing
    /// </summary>
    public interface IPrinterService : IDisposable
    {
        #region Properties

        /// <summary>
        /// Check if printer service is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Get the currently selected/default printer name
        /// </summary>
        string? SelectedPrinterName { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Get list of all available printers installed on the system
        /// Uses System.Drawing.Printing.PrinterSettings.InstalledPrinters
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printersettings.installedprinters
        /// </summary>
        /// <returns>List of available printer devices</returns>
        List<PrinterDevice> GetAvailablePrinters();

        /// <summary>
        /// Select a printer by name for subsequent print operations
        /// </summary>
        /// <param name="printerName">Name of the printer to select</param>
        /// <returns>True if printer was found and selected, false otherwise</returns>
        bool SelectPrinter(string printerName);

        /// <summary>
        /// Get the default printer name from system settings
        /// </summary>
        /// <returns>Default printer name, or null if no default printer is set</returns>
        string? GetDefaultPrinterName();

        /// <summary>
        /// Check if a specific printer is available and online
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>True if printer exists and is online, false otherwise</returns>
        bool IsPrinterAvailable(string printerName);

        /// <summary>
        /// Get detailed status information for a specific printer
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>PrinterDevice with status information, or null if printer not found</returns>
        PrinterDevice? GetPrinterStatus(string printerName);

        /// <summary>
        /// Get roll capacity/paper level information for a printer
        /// Attempts multiple methods: WMI, System.Printing, SNMP, and custom driver properties
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>RollCapacityInfo with available information, or null if printer not found</returns>
        RollCapacityInfo? GetRollCapacity(string printerName);

        /// <summary>
        /// Refresh the cached printer status for the selected/default printer
        /// This performs a fresh check of printer status and updates the cache
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.refresh
        /// </summary>
        /// <returns>True if cache was refreshed successfully, false otherwise</returns>
        bool RefreshCachedStatus();

        /// <summary>
        /// Get the cached printer status for the selected/default printer
        /// Returns cached value if available and recent, otherwise refreshes cache first
        /// This avoids repeated expensive printer status checks for better performance
        /// </summary>
        /// <returns>Cached PrinterDevice with current status, or null if no printer is selected</returns>
        PrinterDevice? GetCachedPrinterStatus();

        /// <summary>
        /// Print an image file to the selected printer
        /// Uses System.Drawing.Printing.PrintDocument to send image to printer
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printdocument
        /// </summary>
        /// <param name="imagePath">Full path to the image file to print</param>
        /// <param name="copies">Number of copies to print (default: 1)</param>
        /// <param name="paperSizeInches">Paper size in inches as (width, height) - default is 6x4 inches (landscape)</param>
        /// <param name="imagesPerPage">Number of images to print per page (for strips: 2 side-by-side, for 4x6: 1)</param>
        /// <param name="waitForCompletion">If true, waits for the print job to actually complete (default: false)</param>
        /// <returns>Tuple containing success status, actual print time in seconds, and optional error message</returns>
        Task<(bool success, double printTimeSeconds, string? errorMessage)> PrintImageAsync(string imagePath, int copies = 1, (float width, float height)? paperSizeInches = null, int imagesPerPage = 1, bool waitForCompletion = false);

        #endregion
    }
}

