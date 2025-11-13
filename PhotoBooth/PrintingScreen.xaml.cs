using System;
using System.Linq;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    public partial class PrintingScreen : UserControl, IDisposable
    {
        #region Events
        public event EventHandler? PrintingCompleted;
        
        #pragma warning disable CS0067 // Event is never used - reserved for future cancellation feature
        public event EventHandler? PrintingCancelled;
        #pragma warning restore CS0067
        #endregion

        #region Constants
        private static readonly TimeSpan PROGRESS_TIMER_INTERVAL = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan COUNTDOWN_TIMER_INTERVAL = TimeSpan.FromSeconds(1);
        private const int LOW_SUPPLIES_THRESHOLD = 50;
        private const int DEFAULT_COUNTDOWN_SECONDS = 10;
        #endregion

        #region Private Fields
        private readonly IDatabaseService _databaseService;
        private readonly IPrinterService? _printerService; // Printer service for detecting available printers
        private readonly MainWindow? _mainWindow; // Reference to MainWindow for operation mode check
        private AdminDashboardScreen? _adminDashboardScreen; // For credit management
        private DispatcherTimer? _progressTimer;
        private DispatcherTimer? _countdownTimer;
        private double _currentProgress = 0;
        private int _countdownSeconds = DEFAULT_COUNTDOWN_SECONDS;
        private bool _disposed = false;
        private decimal _currentCredits = 0;

        // Print job details
        private Template? _template;
        private ProductInfo? _product;
        private string? _composedImagePath;
        private int _totalCopies = 1;
        private int _extraCopies = 0;
        private ProductInfo? _crossSellProduct;
        private decimal _totalOrderCost = 0; // Total cost for the entire order
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize printing screen with required services
        /// </summary>
        /// <param name="databaseService">Database service for data operations</param>
        /// <param name="adminDashboardScreen">Admin dashboard for credit management (optional)</param>
        /// <param name="mainWindow">Main window for operation mode check (optional)</param>
        /// <param name="printerService">Printer service for printer detection (optional, will create default if not provided)</param>
        public PrintingScreen(IDatabaseService databaseService, AdminDashboardScreen? adminDashboardScreen = null, MainWindow? mainWindow = null, IPrinterService? printerService = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _adminDashboardScreen = adminDashboardScreen;
            _mainWindow = mainWindow;
            // Use provided printer service or create a new one
            _printerService = printerService ?? new PrinterService();
            InitializeComponent();
            Loaded += PrintingScreen_Loaded;
        }

        public PrintingScreen() : this(new DatabaseService())
        {
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the printing screen with order details
        /// </summary>
        public Task InitializePrintJob(Template template, ProductInfo product, string composedImagePath, 
            int extraCopies = 0, ProductInfo? crossSellProduct = null, decimal? totalOrderCostOverride = null)
        {
            try
            {
                Console.WriteLine("=== INITIALIZING PRINT JOB ===");
                Console.WriteLine($"Template: {template?.Name ?? "NULL"}");
                Console.WriteLine($"Product Type: {product?.Type ?? "NULL"}");
                Console.WriteLine($"Extra Copies: {extraCopies}");
                Console.WriteLine($"Cross-sell Product: {crossSellProduct?.Name ?? "None"}");
                
                // Reset UI state from any previous print jobs
                ResetPrintingState();

                _template = template;
                _product = product;
                _composedImagePath = composedImagePath;
                _extraCopies = extraCopies;
                _crossSellProduct = crossSellProduct;
                _totalCopies = 1 + extraCopies + (crossSellProduct != null ? 1 : 0);

                // Calculate total order cost (prefer caller-provided to keep billing consistent with UpsellScreen)
                _totalOrderCost = totalOrderCostOverride ?? CalculateTotalOrderCost();
                Console.WriteLine($"CALCULATED TOTAL ORDER COST: ${_totalOrderCost}");

                // Update UI with order details
                UpdateOrderDisplay();

                // Start the printing process asynchronously AFTER navigation (non-blocking)
                // This ensures navigation is instant, then we check printer status and start printing
                // The printer status check uses WMI queries which are slow, so we don't block navigation
                // Fire-and-forget: start the task but don't await it
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Run on background thread to avoid blocking UI
                        // StartPrintingProcess() will use Dispatcher.Invoke for UI updates
                        await StartPrintingProcess();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Failed to start printing process asynchronously", ex);
                        Dispatcher.Invoke(() => ShowError("Failed to start printing process. Please try again."));
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize print job", ex);
                ShowError("Failed to start printing process. Please try again.");
            }
            
            // Return completed task immediately since we're not awaiting anything
            // The actual printing work runs in the background Task.Run
            return Task.CompletedTask;
        }

        /// <summary>
        /// Calculate the total cost for the entire order
        /// </summary>
        private decimal CalculateTotalOrderCost()
        {
            decimal totalCost = 0;

            // Base template cost - use actual product price
            if (_product?.Price != null)
            {
                totalCost += _product.Price;
            }

            // Extra copies cost - use same price as base product
            if (_extraCopies > 0 && _product?.Price != null)
            {
                // For now, use base product price per extra copy (simplified pricing)
                // This matches the UpsellScreen logic when custom pricing is disabled
                totalCost += _extraCopies * _product.Price;
            }

            // Cross-sell product cost
            if (_crossSellProduct != null)
            {
                totalCost += _crossSellProduct.Price;
            }

            return totalCost;
        }

        /// <summary>
        /// Reset the printing screen UI state for a new print job
        /// </summary>
        private void ResetPrintingState()
        {
            try
            {
                // Stop any running timers
                StopProgressTimer();
                StopCountdownTimer();

                // Reset progress state
                _currentProgress = 0;

                // Reset UI elements
                PrintProgress.Value = 0;
                ProgressText.Text = "0%";
                StatusText.Text = "Preparing print job...";
                
                // Reset header text
                HeaderText.Text = "Now Printing...";
                SubHeaderText.Text = "Your photos are being prepared";

                // Hide completion and error cards
                CompletionCard.Visibility = Visibility.Collapsed;
                ErrorCard.Visibility = Visibility.Collapsed;
                
                // Hide back button
                BackButton.Visibility = Visibility.Collapsed;

                LoggingService.Application.Information("Printing screen state reset for new job");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to reset printing state", ex);
            }
        }

        /// <summary>
        /// Update order details display
        /// </summary>
        private void UpdateOrderDisplay()
        {
            if (_template != null && _product != null)
            {
                TemplateText.Text = _template.Name ?? "Unknown Template";
                ProductTypeText.Text = _product.Name ?? _product.Type;
                PhotoCountText.Text = $"{_template.PhotoCount} Photos";
                
                var copiesText = _totalCopies == 1 ? "1 Set" : $"{_totalCopies} Sets";
                if (_extraCopies > 0)
                {
                    copiesText += $" (+{_extraCopies} extra)";
                }
                if (_crossSellProduct != null)
                {
                    copiesText += $" + {_crossSellProduct.Name}";
                }
                CopiesText.Text = copiesText;
            }

            // Printer status will be loaded asynchronously after navigation (in PrintingScreen_Loaded)
            // This ensures navigation is instant and doesn't block on WMI queries
            // Set initial "checking" state so we don't show incorrect "online" status
            if (PrinterNameText != null)
            {
                PrinterNameText.Text = "○ Checking printer status...";
                PrinterNameText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280")); // Gray
            }
            
            // TODO: Replace remaining simulation data with actual services:
            // - PrintsRemainingText.Text should come from IConsumablesService.GetRemainingSupplies()
            // - QueuePositionText.Text should come from IPrintQueueService.GetPosition()
            // - EstimatedTimeText.Text should be calculated from actual queue and printer speed
            
            // Update remaining status - these will be updated asynchronously after navigation
            // Show "Checking..." for all async-loaded data to provide consistent user experience
            PrintsRemainingText.Text = "Checking..."; 
            QueuePositionText.Text = "Checking..."; 
            EstimatedTimeText.Text = "Calculating...";
        }

        /// <summary>
        /// Update printer status display using cached PrinterDevice
        /// This is faster than UpdatePrinterStatus() as it uses pre-cached status
        /// Reference: Uses cached status initialized on app startup
        /// </summary>
        /// <param name="cachedStatus">Cached PrinterDevice with current status, or null if not available</param>
        private void UpdatePrinterStatusDisplay(PrinterDevice? cachedStatus)
        {
            try
            {
                if (cachedStatus == null)
                {
                    PrinterNameText.Text = "Printer Not Available";
                    PrinterNameText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444")); // Red
                    return;
                }

                // Display printer name with status indicator
                // Green dot (●) for online, red circle (○) for offline
                var statusIndicator = cachedStatus.IsOnline ? "●" : "○";
                var statusColor = cachedStatus.IsOnline ? "#10B981" : "#EF4444"; // Green for online, red for offline
                
                // Build printer display text with model if available
                var printerDisplayText = $"{statusIndicator} {cachedStatus.Name}";
                if (!string.IsNullOrWhiteSpace(cachedStatus.Model) && 
                    !cachedStatus.Name.Equals(cachedStatus.Model, StringComparison.OrdinalIgnoreCase))
                {
                    printerDisplayText += $" ({cachedStatus.Model})";
                }
                
                PrinterNameText.Text = printerDisplayText;
                PrinterNameText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(statusColor));
                
                // Update status text to show "Offline" if printer is offline
                // This provides clear feedback to the user immediately
                if (!cachedStatus.IsOnline)
                {
                    // Set status text to show "Printer Offline" for clear user feedback
                    // This is shown in the status display before any print job starts
                    StatusText.Text = "Printer Offline";
                }
                
                LoggingService.Application.Information("Printer status display updated from cache",
                    ("PrinterName", cachedStatus.Name),
                    ("IsOnline", cachedStatus.IsOnline),
                    ("Status", cachedStatus.Status));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update printer status display", ex);
                PrinterNameText.Text = "Printer Status Error";
            }
        }

        /// <summary>
        /// Update printer status display with actual printer information
        /// Uses IPrinterService to get available printers and display the selected/default printer
        /// Displays detailed printer information including model, capabilities, and status
        /// </summary>
        private void UpdatePrinterStatus()
        {
            try
            {
                if (_printerService == null)
                {
                    LoggingService.Application.Warning("Printer service not available - using fallback");
                    PrinterNameText.Text = "No Printer Service";
                    return;
                }

                // Get available printers
                var availablePrinters = _printerService.GetAvailablePrinters();
                
                if (availablePrinters.Count == 0)
                {
                    LoggingService.Application.Warning("No printers detected on system");
                    PrinterNameText.Text = "No Printers Available";
                    return;
                }

                // Try to get the selected/default printer
                var selectedPrinterName = _printerService.SelectedPrinterName;
                PrinterDevice? selectedPrinter = null;

                if (!string.IsNullOrWhiteSpace(selectedPrinterName))
                {
                    selectedPrinter = availablePrinters.FirstOrDefault(p => 
                        string.Equals(p.Name, selectedPrinterName, StringComparison.OrdinalIgnoreCase));
                }

                // If no selected printer found, use the first available printer or default
                if (selectedPrinter == null)
                {
                    selectedPrinter = availablePrinters.FirstOrDefault(p => p.IsDefault) 
                        ?? availablePrinters.FirstOrDefault();
                }

                if (selectedPrinter != null)
                {
                    // Display printer name with status indicator and model information
                    var statusIndicator = selectedPrinter.IsOnline ? "●" : "○";
                    var statusColor = selectedPrinter.IsOnline ? "#10B981" : "#EF4444";
                    
                    // Build printer display text with model if available
                    var printerDisplayText = $"{statusIndicator} {selectedPrinter.Name}";
                    if (!string.IsNullOrWhiteSpace(selectedPrinter.Model) && 
                        !selectedPrinter.Name.Equals(selectedPrinter.Model, StringComparison.OrdinalIgnoreCase))
                    {
                        printerDisplayText += $" ({selectedPrinter.Model})";
                    }
                    
                    PrinterNameText.Text = printerDisplayText;
                    PrinterNameText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(statusColor));
                    
                    LoggingService.Application.Information("Printer status updated",
                        ("PrinterName", selectedPrinter.Name),
                        ("Model", selectedPrinter.Model ?? "Unknown"),
                        ("IsOnline", selectedPrinter.IsOnline),
                        ("IsDefault", selectedPrinter.IsDefault),
                        ("Status", selectedPrinter.Status),
                        ("SupportsColor", selectedPrinter.SupportsColor),
                        ("MaxCopies", selectedPrinter.MaxCopies));
                    
                    // Try to get roll capacity information
                    try
                    {
                        var rollCapacity = _printerService.GetRollCapacity(selectedPrinter.Name);
                        
                        if (rollCapacity != null)
                        {
                            // Update PrintsRemainingText if we have useful information
                            if (rollCapacity.IsAvailable)
                            {
                                if (rollCapacity.RemainingPrints.HasValue)
                                {
                                    PrintsRemainingText.Text = rollCapacity.RemainingPrints.Value.ToString();
                                }
                                else if (rollCapacity.RemainingPercentage.HasValue)
                                {
                                    // Estimate based on percentage (assuming max 700 for 4x6, 1400 for strips)
                                    // This is a rough estimate - actual capacity depends on print size
                                    var estimatedMax = 700; // Default for 4x6 prints
                                    var estimatedRemaining = (int)(estimatedMax * (rollCapacity.RemainingPercentage.Value / 100.0));
                                    PrintsRemainingText.Text = $"~{estimatedRemaining}";
                                }
                                else if (rollCapacity.Status != "Unknown")
                                {
                                    PrintsRemainingText.Text = rollCapacity.Status;
                                }
                            }
                            
                            LoggingService.Application.Information("Roll capacity retrieved",
                                ("PrinterName", selectedPrinter.Name),
                                ("IsAvailable", rollCapacity.IsAvailable),
                                ("Source", rollCapacity.Source),
                                ("Status", rollCapacity.Status),
                                ("RemainingPercentage", rollCapacity.RemainingPercentage?.ToString() ?? "N/A"),
                                ("RemainingPrints", rollCapacity.RemainingPrints?.ToString() ?? "N/A"),
                                ("MaxCapacity", rollCapacity.MaxCapacity?.ToString() ?? "N/A"),
                                ("Details", rollCapacity.Details ?? "N/A"));
                        }
                    }
                    catch (Exception rollEx)
                    {
                        LoggingService.Application.Warning("Failed to get roll capacity",
                            ("PrinterName", selectedPrinter.Name),
                            ("Exception", rollEx.Message));
                    }
                }
                else
                {
                    PrinterNameText.Text = "Printer Not Available";
                    LoggingService.Application.Warning("Could not determine printer to display");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update printer status", ex);
                PrinterNameText.Text = "Printer Status Error";
            }
        }

        /// <summary>
        /// Update queue position and estimated time from actual print queue
        /// This performs a real-time check of the print queue
        /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.numberofjobs
        /// </summary>
        private void UpdateQueuePositionAndEstimatedTime()
        {
            try
            {
                if (_printerService == null)
                {
                    // Fallback to simulation if no service
                    QueuePositionText.Text = "1 of 1";
                    EstimatedTimeText.Text = CalculateEstimatedTime();
                    return;
                }

                var printerName = _printerService.GetDefaultPrinterName();
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    QueuePositionText.Text = "No printer";
                    EstimatedTimeText.Text = "N/A";
                    return;
                }

                // Get actual queue information from print queue
                using var printServer = new LocalPrintServer();
                using var printQueue = printServer.GetPrintQueue(printerName);
                printQueue.Refresh();

                int totalJobs = printQueue.NumberOfJobs;
                
                // Our position is total jobs + 1 (we're about to add our job)
                int ourPosition = totalJobs + 1;
                int totalInQueue = ourPosition;

                // Update queue position display
                QueuePositionText.Text = $"{ourPosition} of {totalInQueue}";

                // Calculate estimated time with actual queue data
                EstimatedTimeText.Text = CalculateEstimatedTime(ourPosition, totalInQueue);

                LoggingService.Application.Information("Queue position updated",
                    ("PrinterName", printerName),
                    ("TotalJobs", totalJobs),
                    ("OurPosition", ourPosition),
                    ("TotalCopies", _totalCopies));
            }
            catch (Exception ex)
            {
                // Fallback to simulation on error
                LoggingService.Application.Warning("Failed to get queue position, using fallback",
                    ("Exception", ex.Message));
                QueuePositionText.Text = "1 of 1";
                EstimatedTimeText.Text = CalculateEstimatedTime();
            }
        }

        /// <summary>
        /// Calculate estimated time based on queue position and print copies
        /// Uses actual print queue data when available
        /// Reference: Typical DNP printer speed is ~30-45 seconds per 4x6 print
        /// </summary>
        /// <param name="queuePosition">Position in queue (1-based), null if unknown</param>
        /// <param name="totalJobsInQueue">Total jobs in queue, null if unknown</param>
        /// <returns>Formatted time estimate string</returns>
        private string CalculateEstimatedTime(int? queuePosition = null, int? totalJobsInQueue = null)
        {
            // Base time per print (30 seconds for typical photo print)
            // Reference: Typical DNP printer speed is ~30-45 seconds per 4x6 print
            int baseTimeSeconds = 30;
            
            // Calculate time for our copies
            int ourCopiesTime = baseTimeSeconds * _totalCopies;
            
            // If we have queue info, add time for jobs ahead of us
            if (queuePosition.HasValue && totalJobsInQueue.HasValue && queuePosition.Value > 1)
            {
                // Estimate 30 seconds per job ahead (conservative estimate)
                int jobsAhead = queuePosition.Value - 1;
                int queueWaitTime = jobsAhead * baseTimeSeconds;
                int totalSeconds = queueWaitTime + ourCopiesTime;
                return FormatTime(totalSeconds);
            }
            
            // Fallback: just our copies time
            return FormatTime(ourCopiesTime);
        }

        /// <summary>
        /// Format time in seconds to a human-readable string
        /// </summary>
        /// <param name="seconds">Time in seconds</param>
        /// <returns>Formatted time string (e.g., "45 seconds" or "1:23")</returns>
        private string FormatTime(double seconds)
        {
            if (seconds < 60)
            {
                return $"{(int)seconds} second{((int)seconds == 1 ? "" : "s")}";
            }
            else
            {
                var totalSeconds = (int)seconds;
                var minutes = totalSeconds / 60;
                var remainingSeconds = totalSeconds % 60;
                return $"{minutes}:{remainingSeconds:D2}";
            }
        }
        #endregion

        #region Event Handlers
        private async void PrintingScreen_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load printer status asynchronously AFTER navigation (non-blocking)
                // Navigation happens instantly, then we update the UI with printer info
                // Using Task.Run to avoid blocking the UI thread during WMI queries
                await Task.Run(() =>
                {
                    // Do the slow WMI queries on background thread
                    // Then update UI on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        UpdatePrinterStatus();
                    });
                });
                
                // Update queue position and estimated time asynchronously (non-blocking)
                // This checks the print queue which can be slow, so we do it after navigation
                await Task.Run(() =>
                {
                    // Check print queue on background thread
                    // Then update UI on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        UpdateQueuePositionAndEstimatedTime();
                    });
                });
                
                // Refresh credits display asynchronously AFTER navigation (non-blocking)
                // This is a fast database query, but keeping it async ensures no blocking
                await RefreshCreditsFromDatabase();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to refresh printer status on screen load", ex);
            }
        }

        /// <summary>
        /// Handle retry button preview mouse down - test if button receives mouse events
        /// </summary>
        private void RetryButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            LoggingService.Application.Information("Retry button preview mouse down");
        }

        /// <summary>
        /// Handle retry button click - step 2: validate data and check printer status (async to avoid blocking UI)
        /// </summary>
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            // Disable button immediately to prevent double-clicks
            RetryButton.IsEnabled = false;
            
            // Run validation and printer check asynchronously to avoid blocking UI
            // WMI queries are slow, so we do them on a background thread
            _ = Task.Run(() =>
            {
                try
                {
                    LoggingService.Application.Information("Retry button clicked - starting retry process");
                    
                    // Step 2: Validate we have the required data
                    if (_template == null)
                    {
                        Dispatcher.Invoke(() => ShowError("Cannot retry: Template information is missing. Please start a new order."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    if (_product == null)
                    {
                        Dispatcher.Invoke(() => ShowError("Cannot retry: Product information is missing. Please start a new order."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    if (string.IsNullOrEmpty(_composedImagePath) || !System.IO.File.Exists(_composedImagePath))
                    {
                        Dispatcher.Invoke(() => ShowError("Cannot retry: Image file is missing. Please start a new order."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    // Step 2B: Check printer status (refresh first to get latest status)
                    // This is slow (WMI queries), so we're already on background thread
                    if (_printerService == null)
                    {
                        Dispatcher.Invoke(() => ShowError("Printer service not available. Please contact support."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    // Refresh printer status to get latest information (slow WMI query)
                    _printerService.RefreshCachedStatus();
                    
                    // Get printer name first
                    var printerName = _printerService.GetDefaultPrinterName();
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        Dispatcher.Invoke(() => ShowError("No printer found. Please configure a printer and try again."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    // Get current printer status (slow WMI query)
                    var printerStatus = _printerService.GetPrinterStatus(printerName);
                    if (printerStatus == null)
                    {
                        Dispatcher.Invoke(() => ShowError("Could not check printer status. Please try again."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    if (!printerStatus.IsOnline)
                    {
                        Dispatcher.Invoke(() => ShowError("Printer is still offline. Please turn on the printer and try again."));
                        Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                        return;
                    }
                    
                    LoggingService.Application.Information("Retry validation passed - printer is online and data is valid");
                    
                    // Step 3: Reset UI and restart printing
                    // Reset UI state on UI thread (hide error card, reset progress, etc.)
                    Dispatcher.Invoke(() =>
                    {
                        // Reset printing state (hides error card, resets progress, etc.)
                        ResetPrintingState();
                        
                        // Hide retry button since we're starting a new print attempt
                        RetryButton.Visibility = Visibility.Collapsed;
                        RetryButton.IsEnabled = false;
                    });
                    
                    // Restart printing process asynchronously (same pattern as InitializePrintJob)
                    // This runs on background thread, so StartPrintingProcess() will use Dispatcher.Invoke for UI updates
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StartPrintingProcess();
                        }
                        catch (Exception printEx)
                        {
                            LoggingService.Application.Error("Retry printing process failed", printEx);
                            Dispatcher.Invoke(() => ShowError($"Retry printing failed: {printEx.Message}"));
                        }
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Error("Retry button click handler failed", ex);
                    Dispatcher.Invoke(() => ShowError($"Retry failed: {ex.Message}"));
                    Dispatcher.Invoke(() => RetryButton.IsEnabled = true);
                }
            });
        }

        /// <summary>
        /// Handle back button click - navigate to welcome screen
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back button from printing screen");
                
                // Stop any running timers
                StopProgressTimer();
                StopCountdownTimer();
                
                // Navigate back to welcome screen
                _mainWindow?.NavigateToWelcome();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Back navigation failed", ex);
                // Fallback: try to navigate anyway
                try
                {
                    _mainWindow?.NavigateToWelcome();
                }
                catch
                {
                    // If navigation fails, at least we tried
                }
            }
        }
        #endregion

        #region Printing Process
        /// <summary>
        /// Start the printing process simulation
        /// </summary>
        private async Task StartPrintingProcess()
        {
            try
            {
                LoggingService.Application.Information("Starting print job",
                    ("TemplateName", _template?.Name ?? "Unknown"),
                    ("ProductType", _product?.Type ?? "Unknown"),
                    ("TotalCopies", _totalCopies));

                // CRITICAL: Check actual printer status BEFORE starting any progress
                // Get real-time printer status (not cached) to ensure accuracy
                // This prevents starting progress bar when printer is actually offline
                // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.printing.printqueue.isoffline
                if (_printerService != null)
                {
                    // Get printer name to check
                    var printerName = _printerService.SelectedPrinterName ?? _printerService.GetDefaultPrinterName();
                    
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        Console.WriteLine($"!!! NO PRINTER SELECTED - Aborting !!!");
                        ShowError("No printer is selected. Please select a printer and try again.");
                        return;
                    }
                    
                    // Get actual current printer status (not cached) for accurate check
                    var currentStatus = _printerService.GetPrinterStatus(printerName);
                    
                    // Check if printer is offline or not available
                    if (currentStatus == null || !currentStatus.IsOnline)
                    {
                        // Printer is offline or not available - show error immediately
                        // Don't start progress bar, timer, or attempt printing
                        var statusMessage = currentStatus?.Status ?? "Not Available";
                        
                        LoggingService.Application.Warning("Print job aborted - printer offline (actual status)",
                            ("PrinterName", printerName),
                            ("Status", statusMessage),
                            ("IsOnline", currentStatus?.IsOnline ?? false));
                        
                        // Update UI to show offline status immediately (on UI thread)
                        Dispatcher.Invoke(() =>
                        {
                            UpdatePrinterStatusDisplay(currentStatus);
                            ShowError("Printer is offline. Please power on the printer and try again.");
                        });
                        return; // Exit early - do NOT start progress timer or continue
                    }
                    
                    // Printer is online - update status display (on UI thread)
                    Console.WriteLine($"!!! PRINTER IS ONLINE (ACTUAL) - Proceeding with print job !!!");
                    Dispatcher.Invoke(() => UpdatePrinterStatusDisplay(currentStatus));
                }
                else
                {
                    // No printer service available - show error (on UI thread)
                    Console.WriteLine($"!!! NO PRINTER SERVICE AVAILABLE - Aborting !!!");
                    Dispatcher.Invoke(() => ShowError("Printer service not available. Please contact support."));
                    return;
                }

                // Initialize progress
                _currentProgress = 0;
                
                // Update status on UI thread (we're in Task.Run, so need Dispatcher)
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Preparing print job...";
                    HeaderText.Text = "Now Printing...";
                    SubHeaderText.Text = "Your photos are being prepared";
                    
                    // Start progress timer
                    StartProgressTimer();
                });

                Console.WriteLine($"!!! ABOUT TO CALL SimulatePrintingStages() !!!");
                
                // Simulate the actual printing process
                await SimulatePrintingStages();
                
                Console.WriteLine($"!!! SimulatePrintingStages() COMPLETED !!!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! EXCEPTION in StartPrintingProcess(): {ex.Message} !!!");
                Console.WriteLine($"!!! StackTrace: {ex.StackTrace} !!!");
                LoggingService.Application.Error("Printing process failed", ex);
                Dispatcher.Invoke(() => ShowError($"Printing failed: {ex.Message}"));
            }
        }

        /// <summary>
        /// Execute actual printing stages using printer service
        /// </summary>
        private async Task SimulatePrintingStages()
        {
            try
            {
                Console.WriteLine($"!!! SimulatePrintingStages() STARTED !!!");
                Console.WriteLine($"!!! _composedImagePath: {_composedImagePath ?? "NULL"} !!!");
                Console.WriteLine($"!!! _printerService: {(_printerService != null ? "NOT NULL" : "NULL")} !!!");
                
                // Validate we have the required data
                if (string.IsNullOrEmpty(_composedImagePath) || !System.IO.File.Exists(_composedImagePath))
                {
                    Console.WriteLine($"!!! ERROR: Image path invalid or file not found: {_composedImagePath ?? "NULL"} !!!");
                    LoggingService.Application.Error("Cannot print - composed image path is invalid", null,
                        ("ImagePath", _composedImagePath ?? "null"));
                    Dispatcher.Invoke(() => ShowError("Print image file not found. Please try again."));
                    return;
                }

                if (_printerService == null)
                {
                    Console.WriteLine($"!!! ERROR: Printer service is NULL !!!");
                    LoggingService.Application.Error("Cannot print - printer service not available", null);
                    Dispatcher.Invoke(() => ShowError("Printer service not available. Please contact support."));
                    return;
                }

                // Stage 1: Processing (0-20%)
                Dispatcher.Invoke(() => StatusText.Text = "Processing images...");
                await UpdateProgressTo(20, TimeSpan.FromSeconds(1));

                // Stage 2: Preparing printer (20-40%)
                Dispatcher.Invoke(() => StatusText.Text = "Preparing printer...");
                
                // Ensure printer is selected
                if (string.IsNullOrWhiteSpace(_printerService.SelectedPrinterName))
                {
                    var defaultPrinter = _printerService.GetDefaultPrinterName();
                    if (!string.IsNullOrWhiteSpace(defaultPrinter))
                    {
                        _printerService.SelectPrinter(defaultPrinter);
                    }
                }

                await UpdateProgressTo(40, TimeSpan.FromSeconds(1));

                // Stage 3: Printing (40-90%)
                Dispatcher.Invoke(() => StatusText.Text = $"Printing {_totalCopies} set{(_totalCopies == 1 ? "" : "s")}...");
                await UpdateProgressTo(40, TimeSpan.FromMilliseconds(100));
                
                // Print the image(s) - single call handles all copies
                // Paper is always 6x4 inches (landscape) for DNP printer
                // Strips: Print 2 copies side-by-side on one 6x4 paper
                // 4x6 Photos: Print 1 copy full size on one 6x4 paper
                (float width, float height) paperSize = (6.0f, 4.0f); // Always 6x4 inches
                int imagesPerPage = 1;
                
                var productType = _product?.Type?.ToLowerInvariant() ?? "";
                if (productType.Contains("strip") || productType == "strips" || productType == "photostrips")
                {
                    // Strips: Print 2 copies side-by-side on one 6x4 paper
                    imagesPerPage = 2;
                }
                else if (productType.Contains("4x6") || productType == "photo4x6")
                {
                    // 4x6 Photos: Print 1 copy full size on one 6x4 paper
                    imagesPerPage = 1;
                }
                else
                {
                    // Default: 1 image per page
                    imagesPerPage = 1;
                }
                
                // Calculate how many pages we need
                // For strips: if we need 2 copies, that's 1 page (2 images per page)
                // For 4x6: if we need 1 copy, that's 1 page (1 image per page)
                int totalPagesNeeded = (int)Math.Ceiling((double)_totalCopies / imagesPerPage);
                
                Console.WriteLine($"!!! CALLING PrintImageAsync !!!");
                Console.WriteLine($"!!! ImagePath: {_composedImagePath} !!!");
                Console.WriteLine($"!!! PrinterName: {_printerService?.SelectedPrinterName ?? "None"} !!!");
                Console.WriteLine($"!!! TotalCopies: {_totalCopies}, ImagesPerPage: {imagesPerPage} !!!");
                
                LoggingService.Application.Information("Calling PrintImageAsync",
                    ("ImagePath", _composedImagePath),
                    ("PrinterName", _printerService?.SelectedPrinterName ?? "None"),
                    ("ProductType", productType),
                    ("PaperSize", $"{paperSize.width}x{paperSize.height}"),
                    ("ImagesPerPage", imagesPerPage),
                    ("TotalCopies", _totalCopies));
                
                // Print all copies at once - the PrintPage handler will handle multiple images per page
                // Set progress to 50% before starting print job
                await UpdateProgressTo(50, TimeSpan.FromMilliseconds(100));
                
                // Calculate estimated physical printing time BEFORE sending job
                // This ensures we can display the correct total time estimate
                // IMPORTANT: totalPagesNeeded accounts for imagesPerPage (strips: 2 per page, 4x6: 1 per page)
                // Note: System.Drawing.Printing doesn't provide estimated print times from the printer,
                // so we use adaptive estimates based on actual observed print times.
                // As the number of pages increases, printers slow down due to:
                // - Heating up (thermal/photo printers)
                // - Paper feeding mechanisms taking longer
                // - Printer buffer/processing overhead
                // We use progressive scaling to account for this slowdown.
                double estimatedPhysicalPrintTimeSeconds;
                if (totalPagesNeeded == 1)
                {
                    // Single page prints faster (18 seconds based on actual printer performance)
                    estimatedPhysicalPrintTimeSeconds = 18.0;
                }
                else
                {
                    // Progressive scaling: time per page increases as total pages increase
                    // This accounts for printer slowdown with longer print jobs
                    double totalTime = 0.0;
                    for (int page = 1; page <= totalPagesNeeded; page++)
                    {
                        double timeForThisPage;
                        if (page <= 2)
                        {
                            // First 2 pages: 25 seconds per page
                            timeForThisPage = 25.0;
                        }
                        else if (page <= 4)
                        {
                            // Pages 3-4: 28 seconds per page (slight slowdown)
                            timeForThisPage = 28.0;
                        }
                        else
                        {
                            // Pages 5+: 32 seconds per page (more significant slowdown)
                            timeForThisPage = 32.0;
                        }
                        totalTime += timeForThisPage;
                    }
                    
                    // Add buffer for page transitions (2 seconds per transition)
                    estimatedPhysicalPrintTimeSeconds = totalTime + ((totalPagesNeeded - 1) * 2.0);
                }
                
                Console.WriteLine($"!!! TIMER CALCULATION DEBUG !!!");
                Console.WriteLine($"!!! TotalCopies: {_totalCopies}, ImagesPerPage: {imagesPerPage}, TotalPagesNeeded: {totalPagesNeeded}, EstimatedTime: {estimatedPhysicalPrintTimeSeconds} seconds !!!");
                
                LoggingService.Application.Information("Calculating estimated print time",
                    ("TotalCopies", _totalCopies),
                    ("ImagesPerPage", imagesPerPage),
                    ("TotalPagesNeeded", totalPagesNeeded),
                    ("EstimatedPhysicalPrintTimeSeconds", estimatedPhysicalPrintTimeSeconds));
                
                // Call PrintImageAsync with waitForCompletion=true to wait for actual print completion
                // This uses Windows print queue monitoring to detect when printing actually finishes
                // Note: This will block until the printer physically completes the job
                Dispatcher.Invoke(() => StatusText.Text = $"Printing {_totalCopies} set{(_totalCopies == 1 ? "" : "s")}...");
                
                // Start progress animation in parallel with print job (using estimated time as fallback)
                // If completion monitoring works, the actual time will be used; otherwise estimate is shown
                var progressTask = UpdateProgressTo(85, TimeSpan.FromSeconds(estimatedPhysicalPrintTimeSeconds));
                
                // Call PrintImageAsync with completion monitoring enabled
                // _printerService is guaranteed to be non-null here due to earlier null check and early return
                if (_printerService == null)
                {
                    ShowError("Printer service not available. Please contact support.");
                    return;
                }
                
                Console.WriteLine($"!!! ABOUT TO CALL PrintImageAsync - waitForCompletion=true !!!");
                
                (bool printSuccess, double actualPrintTimeSeconds, string? errorMessage) = await _printerService.PrintImageAsync(
                    _composedImagePath, 
                    copies: _totalCopies, 
                    paperSize, 
                    imagesPerPage, 
                    waitForCompletion: true);
                
                Console.WriteLine($"!!! PrintImageAsync RETURNED !!!");
                Console.WriteLine($"!!! printSuccess: {printSuccess} !!!");
                Console.WriteLine($"!!! actualPrintTimeSeconds: {actualPrintTimeSeconds} !!!");
                Console.WriteLine($"!!! errorMessage: {errorMessage ?? "None"} !!!");
                
                // Update estimated time display with actual print time (on UI thread)
                Dispatcher.Invoke(() => EstimatedTimeText.Text = FormatTime(actualPrintTimeSeconds));
                
                if (!printSuccess)
                {
                    LoggingService.Application.Error("Print job failed", null,
                        ("TotalCopies", _totalCopies),
                        ("ImagePath", _composedImagePath),
                        ("ActualPrintTimeSeconds", actualPrintTimeSeconds),
                        ("ErrorMessage", errorMessage ?? "Unknown"));

                    // Fast-forward progress animation and show error state
                    await Task.WhenAny(progressTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
                    var userMessage = string.IsNullOrWhiteSpace(errorMessage)
                        ? "Printing could not start. Please check the printer and try again."
                        : errorMessage!;
                    Dispatcher.Invoke(() => ShowError(userMessage));
                    return;
                }
                else
                {
                    LoggingService.Application.Information("Print job completed",
                        ("TotalCopies", _totalCopies),
                        ("ImagesPerPage", imagesPerPage),
                        ("TotalPages", totalPagesNeeded),
                        ("ActualPrintTimeSeconds", actualPrintTimeSeconds));
                }

                // Wait for progress animation to complete (if it hasn't already)
                await progressTask;

                // Stage 4: Finishing (85-100%)
                Dispatcher.Invoke(() =>
                {
                    if (printSuccess)
                    {
                        StatusText.Text = "Finishing...";
                    }
                    else
                    {
                        StatusText.Text = "Some prints may have failed...";
                    }
                });
                
                // Final delay before completing (minimum 2 seconds)
                await UpdateProgressTo(100, TimeSpan.FromSeconds(2));

                // Complete
                await CompletePrinting();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! EXCEPTION in SimulatePrintingStages(): {ex.Message} !!!");
                Console.WriteLine($"!!! StackTrace: {ex.StackTrace} !!!");
                LoggingService.Application.Error("Printing process failed", ex);
                Dispatcher.Invoke(() => ShowError($"Printing process encountered an error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Update progress to target percentage over specified duration
        /// </summary>
        private async Task UpdateProgressTo(double targetProgress, TimeSpan duration)
        {
            var startProgress = _currentProgress;
            var progressDelta = targetProgress - startProgress;
            
            // TODO: Make animation parameters configurable for customization
            var steps = 10; // Fewer steps for faster animation updates
            var stepDuration = duration.TotalMilliseconds / steps;
            var progressPerStep = progressDelta / steps;

            for (int i = 0; i < steps && !_disposed; i++)
            {
                _currentProgress += progressPerStep;
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (_disposed) return;
                    PrintProgress.Value = _currentProgress;
                    ProgressText.Text = $"{_currentProgress:F0}%";
                });

                await Task.Delay(TimeSpan.FromMilliseconds(stepDuration));
            }

            // Ensure we hit the exact target
            _currentProgress = targetProgress;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_disposed) return;
                PrintProgress.Value = _currentProgress;
                ProgressText.Text = $"{_currentProgress:F0}%";
            });
        }

        /// <summary>
        /// Complete the printing process
        /// </summary>
        private async Task CompletePrinting()
        {
            try
            {
                Console.WriteLine("=== PRINTING COMPLETION STARTED ===");
                Console.WriteLine($"Template: {_template?.Name ?? "NULL"}");
                Console.WriteLine($"Total Copies: {_totalCopies}");
                Console.WriteLine($"Total Order Cost: ${_totalOrderCost}");
                Console.WriteLine($"Admin Dashboard Available: {_adminDashboardScreen != null}");
                
                StopProgressTimer();

                // CRITICAL: Deduct credits and record sale transaction
                Console.WriteLine("=== CALLING PAYMENT PROCESSING ===");
                bool creditDeductionSuccess = await ProcessPaymentAndSalesTransaction();

                // Update header based on credit deduction result (on UI thread)
                Dispatcher.Invoke(() =>
                {
                    if (creditDeductionSuccess)
                    {
                        HeaderText.Text = "Printing Complete!";
                        SubHeaderText.Text = "Your photos are ready";
                        StatusText.Text = "Complete";

                        // Show completion card
                        CompletionCard.Visibility = Visibility.Visible;
                        
                        // Show back button
                        BackButton.Visibility = Visibility.Visible;

                        // Start countdown timer
                        StartCountdownTimer();
                    }
                    else
                    {
                        // Credit deduction failed - show error state
                        if (_mainWindow?.IsFreePlayMode == true)
                        {
                            // In free play mode, this shouldn't happen, but if it does, show a generic error
                            HeaderText.Text = "Printing Error";
                            SubHeaderText.Text = "Please contact staff for assistance";
                            StatusText.Text = "Error";
                            
                            ErrorMessageText.Text = "An unexpected error occurred during printing. Please contact staff for assistance.";
                            ErrorCard.Visibility = Visibility.Visible;
                            
                            // Show back button
                            BackButton.Visibility = Visibility.Visible;
                            
                            // Show retry button
                            Console.WriteLine("!!! SETTING RETRY BUTTON VISIBLE (error case 1) !!!");
                            RetryButton.Visibility = Visibility.Visible;
                            RetryButton.IsEnabled = true;
                            Console.WriteLine($"!!! RetryButton.Visibility set to: {RetryButton.Visibility} !!!");
                            Console.WriteLine($"!!! RetryButton.IsEnabled set to: {RetryButton.IsEnabled} !!!");
                        }
                        else
                        {
                            // Credit deduction failed in coin mode - show payment error
                            HeaderText.Text = "Payment Required";
                            SubHeaderText.Text = "Please contact staff to add credits";
                            StatusText.Text = "Payment Failed";
                            
                            ErrorMessageText.Text = "Your photos were printed but payment could not be processed. Please contact staff to resolve the payment issue.";
                            ErrorCard.Visibility = Visibility.Visible;
                            
                            // Show back button
                            BackButton.Visibility = Visibility.Visible;
                            
                            // Show retry button (though retry won't help with payment issues, but user can still try)
                            Console.WriteLine("!!! SETTING RETRY BUTTON VISIBLE (error case 2) !!!");
                            RetryButton.Visibility = Visibility.Visible;
                            RetryButton.IsEnabled = true;
                            Console.WriteLine($"!!! RetryButton.Visibility set to: {RetryButton.Visibility} !!!");
                            Console.WriteLine($"!!! RetryButton.IsEnabled set to: {RetryButton.IsEnabled} !!!");
                        }
                        
                        // Start countdown timer to return to welcome screen
                        StartCountdownTimer();
                    }
                });

                // Immediately refresh credits display to show updated balance
                _ = RefreshCreditsFromDatabase();
                
                // Show appropriate notification based on operation mode
                if (creditDeductionSuccess)
                {
                    if (_mainWindow?.IsFreePlayMode == true)
                    {
                        NotificationService.Instance.ShowSuccess("Printing Complete", 
                            $"Your photos have been printed successfully!\nFree play mode - no payment required.", 5);
                    }
                    else
                    {
                        // Show updated credit balance notification for coin mode
                        var newBalance = _adminDashboardScreen?.GetCurrentCredits() ?? 0;
                        NotificationService.Instance.ShowSuccess("Payment Processed", 
                            $"${_totalOrderCost:F2} deducted successfully.\nRemaining credits: ${newBalance:F2}", 5);
                    }
                }

                // Update consumables (simulation)
                await UpdateConsumables();

                // Update credits display
                _ = RefreshCreditsFromDatabase();
                
                Console.WriteLine("=== PRINTING COMPLETION FINISHED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== PRINTING COMPLETION ERROR: {ex.Message} ===");
                LoggingService.Application.Error("Print completion failed", ex);
                Dispatcher.Invoke(() => ShowError("Print completion encountered an error."));
            }
        }

        /// <summary>
        /// Process payment (credit deduction) and record sales transaction
        /// </summary>
        private async Task<bool> ProcessPaymentAndSalesTransaction()
        {
            try
            {
                Console.WriteLine("--- PAYMENT PROCESSING START ---");
                Console.WriteLine($"Admin Dashboard: {(_adminDashboardScreen != null ? "AVAILABLE" : "NULL")}");
                Console.WriteLine($"Total Order Cost: ${_totalOrderCost}");
                Console.WriteLine($"Should Process Credits: {_adminDashboardScreen != null && _totalOrderCost > 0}");
                
                // 1. First create sales transaction record
                Console.WriteLine("--- CREATING SALES TRANSACTION ---");
                var transactionId = await CreateSalesTransaction(false); // Initially pending
                Console.WriteLine($"Transaction ID Created: {transactionId}");
                
                // 2. Deduct credits if admin dashboard is available and link to transaction
                bool creditDeductionSuccess = false;
                
                // Check if we're in free play mode - if so, skip credit deduction
                if (_mainWindow?.IsFreePlayMode == true)
                {
                    Console.WriteLine("--- FREE PLAY MODE DETECTED - SKIPPING CREDIT DEDUCTION ---");
                    Console.WriteLine($"--- FREE PLAY MODE DETAILS --- TotalOrderCost: {_totalOrderCost}, OperationMode: {_mainWindow.CurrentOperationMode}");
                    
                    // Log current credit balance before and after (should be the same)
                    var currentCredits = _adminDashboardScreen?.GetCurrentCredits() ?? 0;
                    Console.WriteLine($"--- FREE PLAY MODE CREDIT BALANCE --- Before: ${currentCredits}, After: ${currentCredits} (NO CHANGE)");
                    
                    LoggingService.Application.Information("Free play mode detected - skipping credit deduction",
                        ("TotalOrderCost", _totalOrderCost),
                        ("OperationMode", _mainWindow.CurrentOperationMode));
                    creditDeductionSuccess = true; // Mark as successful since no deduction is needed
                }
                else if (_adminDashboardScreen != null && _totalOrderCost > 0)
                {
                    Console.WriteLine("--- ATTEMPTING CREDIT DEDUCTION ---");
                    Console.WriteLine($"--- CREDIT DEDUCTION DETAILS --- MainWindow: {_mainWindow != null}, IsFreePlayMode: {_mainWindow?.IsFreePlayMode}");
                    var orderDescription = BuildOrderDescription();
                    Console.WriteLine($"Order Description: {orderDescription}");
                    Console.WriteLine($"Deducting ${_totalOrderCost} from credits...");
                    
                    var creditsBefore = _adminDashboardScreen.GetCurrentCredits();
                    Console.WriteLine($"--- CREDIT DEDUCTION BEFORE --- Credits: ${creditsBefore}");
                    
                    creditDeductionSuccess = await _adminDashboardScreen.DeductCreditsAsync(_totalOrderCost, orderDescription, transactionId);
                    
                    var creditsAfter = _adminDashboardScreen.GetCurrentCredits();
                    Console.WriteLine($"--- CREDIT DEDUCTION AFTER --- Credits: ${creditsAfter}, Change: ${creditsAfter - creditsBefore}");
                    Console.WriteLine($"Credit Deduction Result: {(creditDeductionSuccess ? "SUCCESS" : "FAILED")}");
                    
                    if (!creditDeductionSuccess)
                    {
                        Console.WriteLine("!!! CREDIT DEDUCTION FAILED !!!");
                        
                        // Show error message to user
                        var currentCredits = _adminDashboardScreen?.GetCurrentCredits() ?? 0;
                        var shortfall = _totalOrderCost - currentCredits;
                        
                        var errorMessage = $"Insufficient credits to complete this order.\n\n" +
                                          $"Order total: ${_totalOrderCost:F2}\n" +
                                          $"Current credits: ${currentCredits:F2}\n" +
                                          $"Additional credits needed: ${shortfall:F2}\n\n" +
                                          $"Please contact staff to add more credits.";
                        
                        NotificationService.Instance.ShowError("Payment Failed", errorMessage, 10);
                    }
                }
                else
                {
                    Console.WriteLine("--- CREDIT DEDUCTION SKIPPED ---");
                    if (_adminDashboardScreen == null)
                        Console.WriteLine("Reason: Admin Dashboard is NULL");
                    if (_totalOrderCost <= 0)
                    {
                        Console.WriteLine($"Reason: Zero-cost order (${_totalOrderCost}) - auto-completing as free");
                        LoggingService.Application.Information("Zero-cost order - skipping credit deduction",
                            ("TotalOrderCost", _totalOrderCost));
                        creditDeductionSuccess = true;
                    }
                }

                // 3. Update transaction status based on credit deduction result
                if (transactionId > 0)
                {
                    Console.WriteLine($"--- UPDATING TRANSACTION STATUS: {(creditDeductionSuccess ? "COMPLETED" : "FAILED")} ---");
                    await UpdateTransactionStatus(transactionId, creditDeductionSuccess);
                }

                Console.WriteLine("--- PAYMENT PROCESSING COMPLETE ---");
                Console.WriteLine($"Final Result - Credit Deduction: {creditDeductionSuccess}, Transaction: {transactionId}");
                
                return creditDeductionSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! PAYMENT PROCESSING ERROR: {ex.Message} !!!");
                LoggingService.Application.Error("Failed to process payment and sales transaction", ex);
                // Continue with printing completion even if payment processing fails
                return false;
            }
        }

        /// <summary>
        /// Build description for the order
        /// </summary>
        private string BuildOrderDescription()
        {
            var description = $"{_template?.Name ?? "Unknown Template"}";
            
            if (_extraCopies > 0)
            {
                description += $" + {_extraCopies} extra copies";
            }
            
            if (_crossSellProduct != null)
            {
                description += $" + {_crossSellProduct.Name}";
            }
            
            return description;
        }

        /// <summary>
        /// Create sales transaction record in database
        /// </summary>
        private async Task<int> CreateSalesTransaction(bool creditDeductionSuccess)
        {
            try
            {
                // Find the correct Product ID from database
                var productId = await GetProductIdFromDatabase();
                Console.WriteLine($"--- TRANSACTION DEBUG: ProductId = {productId} ---");
                
                if (productId == 0)
                {
                    LoggingService.Application.Warning("Could not find product ID, using default value");
                    productId = 1; // Default fallback
                }

                // Create transaction record
                var transaction = new Transaction
                {
                    TransactionCode = GenerateTransactionCode(),
                    ProductId = productId,
                    TemplateId = _template?.Id,
                    Quantity = _totalCopies,
                    BasePrice = GetBasePrice(),
                    TotalPrice = _totalOrderCost,
                    PaymentMethod = (_mainWindow?.IsFreePlayMode == true || _totalOrderCost <= 0) ? PaymentMethod.Free : PaymentMethod.Credit,
                    PaymentStatus = creditDeductionSuccess || (_mainWindow?.IsFreePlayMode == true) 
                        ? PaymentStatus.Completed 
                        : PaymentStatus.Pending,
                    CreatedAt = DateTime.Now,
                    CompletedAt = (creditDeductionSuccess || (_mainWindow?.IsFreePlayMode == true)) ? DateTime.Now : null,
                    Notes = BuildOrderDescription()
                };

                Console.WriteLine($"--- TRANSACTION DEBUG: Creating transaction ${transaction.TotalPrice} ---");

                var insertResult = await _databaseService.InsertAsync(transaction);
                
                Console.WriteLine($"--- TRANSACTION DEBUG: Insert result ---");
                Console.WriteLine($"Success: {insertResult.Success}");
                Console.WriteLine($"Data: {insertResult.Data}");
                Console.WriteLine($"ErrorMessage: {insertResult.ErrorMessage}");
                
                if (insertResult.Success)
                {
                    LoggingService.Application.Information("Sales transaction recorded",
                        ("TransactionId", insertResult.Data),
                        ("TransactionCode", transaction.TransactionCode),
                        ("PaymentStatus", transaction.PaymentStatus));

                    // Create print job record
                    await CreatePrintJobRecord(insertResult.Data);
                    
                    return insertResult.Data;
                }
                else
                {
                    LoggingService.Application.Error("Failed to record sales transaction", null,
                        ("Error", insertResult.ErrorMessage ?? "Unknown error"));
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- TRANSACTION DEBUG: Exception: {ex.Message} ---");
                LoggingService.Application.Error("Failed to create sales transaction", ex);
                return 0;
            }
        }

        /// <summary>
        /// Update transaction status after credit deduction attempt
        /// </summary>
        private async Task UpdateTransactionStatus(int transactionId, bool creditDeductionSuccess)
        {
            try
            {
                Console.WriteLine($"--- UPDATE STATUS DEBUG: Updating transaction {transactionId} to {(creditDeductionSuccess ? "Completed" : "Failed")} ---");
                
                // Get the transaction first
                var transactionResult = await _databaseService.GetByIdAsync<Transaction>(transactionId);
                if (transactionResult.Success && transactionResult.Data != null)
                {
                    var transaction = transactionResult.Data;
                    Console.WriteLine($"--- UPDATE STATUS DEBUG: Current status = {transaction.PaymentStatus}, Setting to = {(creditDeductionSuccess ? PaymentStatus.Completed : PaymentStatus.Failed)} ---");
                    
                    transaction.PaymentStatus = creditDeductionSuccess ? PaymentStatus.Completed : PaymentStatus.Failed;
                    transaction.CompletedAt = creditDeductionSuccess ? DateTime.Now : null;

                    var updateResult = await _databaseService.UpdateAsync(transaction);
                    
                    Console.WriteLine($"--- UPDATE STATUS DEBUG: Update result = Success: {updateResult.Success}, Error: {updateResult.ErrorMessage} ---");
                    
                    if (updateResult.Success)
                    {
                        LoggingService.Application.Information("Transaction status updated",
                            ("TransactionId", transactionId),
                            ("PaymentStatus", transaction.PaymentStatus));
                    }
                    else
                    {
                        LoggingService.Application.Error("Failed to update transaction status", null,
                            ("TransactionId", transactionId),
                            ("Error", updateResult.ErrorMessage ?? "Unknown error"));
                    }
                }
                else
                {
                    Console.WriteLine($"--- UPDATE STATUS DEBUG: Failed to get transaction {transactionId}: Success={transactionResult.Success}, Error={transactionResult.ErrorMessage} ---");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- UPDATE STATUS DEBUG: Exception: {ex.Message} ---");
                LoggingService.Application.Error("Failed to update transaction status", ex,
                    ("TransactionId", transactionId));
            }
        }

        /// <summary>
        /// Get Product ID from database based on current product type
        /// </summary>
        private async Task<int> GetProductIdFromDatabase()
        {
            try
            {
                Console.WriteLine($"--- PRODUCT DEBUG: Looking for product type '{_product?.Type}' ---");
                
                var productsResult = await _databaseService.GetAllAsync<Product>();
                
                if (productsResult.Success && productsResult.Data != null)
                {
                    var targetType = (_product?.Type?.ToLowerInvariant()) switch
                    {
                        "strips" or "photostrips" => ProductType.PhotoStrips,
                        "4x6" or "photo4x6"       => ProductType.Photo4x6,
                        "phone" or "smartphoneprint" => ProductType.SmartphonePrint,
                        _ => (ProductType?)null
                    };

                    var matchingProduct = targetType.HasValue
                        ? productsResult.Data.FirstOrDefault(p => p.ProductType == targetType.Value)
                        : null;
                    
                    Console.WriteLine($"--- PRODUCT DEBUG: Found product ID {matchingProduct?.Id ?? 0} ({matchingProduct?.Name}) via precise ProductType mapping ---");
                    
                    return matchingProduct?.Id ?? 1;
                }
                else
                {
                    Console.WriteLine($"--- PRODUCT DEBUG: Database query failed: {productsResult.ErrorMessage} ---");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- PRODUCT DEBUG: Exception: {ex.Message} ---");
                LoggingService.Application.Error("Failed to get product ID from database", ex);
            }
            
            return 1; // Default fallback
        }

        /// <summary>
        /// Get base price for the main product (excluding extras)
        /// </summary>
        private decimal GetBasePrice()
        {
            // Use actual product price instead of hardcoded values
            return _product?.Price ?? 3.00m; // Default fallback price if product price is null
        }

        /// <summary>
        /// Generate unique transaction code
        /// </summary>
        private string GenerateTransactionCode()
        {
            return $"TRX-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}-{Random.Shared.Next(1000, 9999)}";
        }

        /// <summary>
        /// Create print job record for supply tracking
        /// </summary>
        private async Task CreatePrintJobRecord(int transactionId)
        {
            try
            {
                var printJob = new PrintJob
                {
                    TransactionId = transactionId,
                    Copies = _totalCopies,
                    PrintStatus = PrintStatus.Completed,
                    StartedAt = DateTime.Now.AddSeconds(-30), // Approximate start time
                    CompletedAt = DateTime.Now,
                    PrintsUsed = _totalCopies,
                    PrinterName = "DNP DS620A" // Default printer name
                };

                var printJobResult = await _databaseService.InsertAsync(printJob);
                
                if (printJobResult.Success)
                {
                    LoggingService.Application.Information("Print job record created",
                        ("PrintJobId", printJobResult.Data),
                        ("Copies", printJob.Copies),
                        ("PrintsUsed", printJob.PrintsUsed));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create print job record", ex);
            }
        }

        /// <summary>
        /// Update consumables tracking (simulation)
        /// </summary>
        private async Task UpdateConsumables()
        {
            try
            {
                // In real implementation, this would:
                // 1. Decrement paper count
                // 2. Decrement ribbon/ink usage
                // 3. Update database
                // 4. Check for low supplies warning

                await Task.Delay(100); // Simulate database update

                // Update remaining prints display
                if (!int.TryParse(PrintsRemainingText.Text, out var currentRemaining))
                {
                    LoggingService.Application.Warning("Invalid remaining prints value",
                        ("Text", PrintsRemainingText.Text));
                    currentRemaining = 0;
                }
                var newRemaining = Math.Max(0, currentRemaining - _totalCopies);
                PrintsRemainingText.Text = newRemaining.ToString();

                // Show warning if low supplies
                if (newRemaining < LOW_SUPPLIES_THRESHOLD)
                {
                    PrintsRemainingText.Foreground = System.Windows.Media.Brushes.Orange;
                    LoggingService.Application.Warning("Low consumables detected", ("RemainingPrints", newRemaining));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Consumables update failed", ex);
                // Don't fail the whole process for consumables tracking
            }
        }

        /// <summary>
        /// Show error state
        /// </summary>
        private void ShowError(string errorMessage)
        {
            try
            {
                // Stop all timers and reset progress to 0
                StopProgressTimer();
                StopCountdownTimer();
                
                // Reset progress bar to 0 to ensure no progress is displayed
                _currentProgress = 0;
                PrintProgress.Value = 0;
                ProgressText.Text = "0%";

                HeaderText.Text = "Printing Error";
                SubHeaderText.Text = "Something went wrong";
                StatusText.Text = "Error";
                
                ErrorMessageText.Text = errorMessage;
                ErrorCard.Visibility = Visibility.Visible;
                
                // Show back button
                BackButton.Visibility = Visibility.Visible;
                
                // Ensure retry button is visible and enabled
                Console.WriteLine("!!! SETTING RETRY BUTTON VISIBLE (ShowError method) !!!");
                if (RetryButton == null)
                {
                    Console.WriteLine("!!! ERROR: RetryButton is NULL !!!");
                }
                else
                {
                    RetryButton.Visibility = Visibility.Visible;
                    RetryButton.IsEnabled = true;
                    Console.WriteLine($"!!! RetryButton.Visibility set to: {RetryButton.Visibility} !!!");
                    Console.WriteLine($"!!! RetryButton.IsEnabled set to: {RetryButton.IsEnabled} !!!");
                    Console.WriteLine($"!!! RetryButton.IsVisible: {RetryButton.IsVisible} !!!");
                    Console.WriteLine($"!!! ErrorCard.Visibility: {ErrorCard.Visibility} !!!");
                    Console.WriteLine($"!!! ErrorCard.IsVisible: {ErrorCard.IsVisible} !!!");
                }
                
                LoggingService.Application.Error("Printing error displayed", null, ("ErrorMessage", errorMessage));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to show error state", ex);
            }
        }
        #endregion

        #region Timer Management
        /// <summary>
        /// Start progress animation timer
        /// </summary>
        private void StartProgressTimer()
        {
            StopProgressTimer();
            
            _progressTimer = new DispatcherTimer
            {
                Interval = PROGRESS_TIMER_INTERVAL
            };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();
        }

        /// <summary>
        /// Stop progress timer
        /// </summary>
        private void StopProgressTimer()
        {
            if (_progressTimer != null)
            {
                _progressTimer.Stop();
                _progressTimer.Tick -= ProgressTimer_Tick;
                _progressTimer = null;
            }
        }

        /// <summary>
        /// Start countdown timer for auto-return to welcome
        /// </summary>
        private void StartCountdownTimer()
        {
            StopCountdownTimer();
            
            _countdownSeconds = DEFAULT_COUNTDOWN_SECONDS;
            _countdownTimer = new DispatcherTimer
            {
                Interval = COUNTDOWN_TIMER_INTERVAL
            };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
        }

        /// <summary>
        /// Stop countdown timer
        /// </summary>
        private void StopCountdownTimer()
        {
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Tick -= CountdownTimer_Tick;
                _countdownTimer = null;
            }
        }

        /// <summary>
        /// Handle progress timer tick
        /// </summary>
        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            // Additional progress animations could go here
            // For now, main progress is handled in UpdateProgressTo
        }

        /// <summary>
        /// Handle countdown timer tick
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _countdownSeconds = Math.Max(0, _countdownSeconds - 1);
            
            if (_countdownSeconds <= 0)
            {
                StopCountdownTimer();
                PrintingCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                CountdownText.Text = $"Returning to welcome screen in {_countdownSeconds} seconds...";
            }
        }
        #endregion

        #region Credits Management

        /// <summary>
        /// Refresh credits from database
        /// </summary>
        private async Task RefreshCreditsFromDatabase()
        {
            try
            {
                var creditsResult = await _databaseService.GetSettingValueAsync<decimal>("System", "CurrentCredits");
                if (creditsResult.Success)
                {
                    _currentCredits = creditsResult.Data;
                }
                else
                {
                    _currentCredits = 0;
                }
                UpdateCreditsDisplay();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error refreshing credits from database", ex);
                _currentCredits = 0;
                UpdateCreditsDisplay();
            }
        }

        /// <summary>
        /// Updates the credits display with validation
        /// </summary>
        /// <param name="credits">Current credit amount</param>
        public void UpdateCredits(decimal credits)
        {
            if (credits < 0)
            {
                credits = 0;
            }

            _currentCredits = credits;
            UpdateCreditsDisplay();
        }

        /// <summary>
        /// Update credits display
        /// </summary>
        private void UpdateCreditsDisplay()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(UpdateCreditsDisplay);
                    return;
                }
                if (CreditsDisplay != null)
                {
                    string displayText;
                    if (_mainWindow?.IsFreePlayMode == true)
                    {
                        displayText = "Free Play Mode";
                    }
                    else
                    {
                        displayText = $"Credits: ${_currentCredits:F2}";
                    }
                    CreditsDisplay.Text = displayText;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update credits display", ex);
            }
        }

        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopProgressTimer();
                StopCountdownTimer();
                _disposed = true;
                
                LoggingService.Application.Information("PrintingScreen disposed");
            }
        }
        #endregion
    }
} 