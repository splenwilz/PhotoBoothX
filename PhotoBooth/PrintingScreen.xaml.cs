using System;
using System.Linq;
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
        public PrintingScreen(IDatabaseService databaseService, AdminDashboardScreen? adminDashboardScreen = null, MainWindow? mainWindow = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _adminDashboardScreen = adminDashboardScreen;
            _mainWindow = mainWindow;
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
        public async Task InitializePrintJob(Template template, ProductInfo product, string composedImagePath, 
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

                // Start the printing simulation
                await StartPrintingProcess();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! PRINT JOB INITIALIZATION ERROR: {ex.Message} !!!");
                LoggingService.Application.Error("Failed to initialize print job", ex);
                ShowError("Failed to start printing process. Please try again.");
            }
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

            // TODO: Replace with actual printer service integration
            // - PrinterNameText.Text should come from IPrinterService.GetConnectedPrinters()
            // - PrintsRemainingText.Text should come from IConsumablesService.GetRemainingSupplies()
            // - QueuePositionText.Text should come from IPrintQueueService.GetPosition()
            // - EstimatedTimeText.Text should be calculated from actual queue and printer speed
            
            // Update printer status (simulation data)
            PrinterNameText.Text = "DNP DS620A"; 
            PrintsRemainingText.Text = "642"; 
            QueuePositionText.Text = "1 of 1"; 
            EstimatedTimeText.Text = CalculateEstimatedTime();
        }

        /// <summary>
        /// Calculate estimated print time based on job complexity
        /// </summary>
        private string CalculateEstimatedTime()
        {
            // Simple estimation - in real implementation this would consider:
            // - Print quality settings
            // - Number of copies
            // - Printer speed
            // - Current queue length
            int baseTimeSeconds = 30; // Base time for one print
            int totalSeconds = baseTimeSeconds * _totalCopies;
            
            if (totalSeconds < 60)
                return $"{totalSeconds} seconds";
            else
            {
                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;
                return $"{minutes}:{seconds:D2}";
            }
        }
        #endregion

        #region Event Handlers
        private async void PrintingScreen_Loaded(object sender, RoutedEventArgs e)
        {
            try
        {
                // Initialize credits display after UI is loaded
                await RefreshCreditsFromDatabase();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to refresh credits on screen load", ex);
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required state
                if (_template == null || _product == null || string.IsNullOrEmpty(_composedImagePath))
                {
                    LoggingService.Application.Error("Cannot retry - missing required print job data", null,
                        ("HasTemplate", _template != null),
                        ("HasProduct", _product != null),
                        ("HasComposedImage", !string.IsNullOrEmpty(_composedImagePath)));
                    ShowError("Cannot retry print job. Please start a new session.");
                    return;
                }

                // Reset UI state and restart printing
                ResetPrintingState();
                
                // Set retry status
                StatusText.Text = "Retrying...";

                // Restart printing process
                await StartPrintingProcess();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print retry failed", ex);
                ShowError("Retry failed. Please contact support.");
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

                // Initialize progress
                _currentProgress = 0;
                
                // Update status
                StatusText.Text = "Preparing print job...";
                HeaderText.Text = "Now Printing...";
                SubHeaderText.Text = "Your photos are being prepared";

                // Start progress timer
                StartProgressTimer();

                // Simulate the actual printing process
                await SimulatePrintingStages();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Printing process failed", ex);
                ShowError($"Printing failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Simulate different stages of printing
        /// </summary>
        private async Task SimulatePrintingStages()
        {
            try
            {
                // Stage 1: Processing (0-20%)
                StatusText.Text = "Processing images...";
                await UpdateProgressTo(20, TimeSpan.FromSeconds(2));

                // Stage 2: Preparing printer (20-40%)
                StatusText.Text = "Preparing printer...";
                await UpdateProgressTo(40, TimeSpan.FromSeconds(1));

                // Stage 3: Printing (40-90%)
                StatusText.Text = $"Printing {_totalCopies} set{(_totalCopies == 1 ? "" : "s")}...";
                await UpdateProgressTo(90, TimeSpan.FromSeconds(4));

                // Stage 4: Finishing (90-100%)
                StatusText.Text = "Finishing...";
                await UpdateProgressTo(100, TimeSpan.FromSeconds(1));

                // Complete
                await CompletePrinting();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print simulation failed", ex);
                ShowError("Printing process encountered an error.");
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
            var steps = 50; // 20ms per step for smooth animation
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

                // Update header based on credit deduction result
                if (creditDeductionSuccess)
                {
                HeaderText.Text = "Printing Complete!";
                SubHeaderText.Text = "Your photos are ready";
                StatusText.Text = "Complete";

                // Show completion card
                CompletionCard.Visibility = Visibility.Visible;

                    // Immediately refresh credits display to show updated balance
                    _ = RefreshCreditsFromDatabase();
                    
                    // Show appropriate notification based on operation mode
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
                    }
                    else
                    {
                        // Credit deduction failed in coin mode - show payment error
                        HeaderText.Text = "Payment Required";
                        SubHeaderText.Text = "Please contact staff to add credits";
                        StatusText.Text = "Payment Failed";
                        
                        ErrorMessageText.Text = "Your photos were printed but payment could not be processed. Please contact staff to resolve the payment issue.";
                        ErrorCard.Visibility = Visibility.Visible;
                    }
                    
                    // Start countdown timer to return to welcome screen
                    StartCountdownTimer();
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
                ShowError("Print completion encountered an error.");
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
                StopProgressTimer();
                StopCountdownTimer();

                HeaderText.Text = "Printing Error";
                SubHeaderText.Text = "Something went wrong";
                StatusText.Text = "Error";
                
                ErrorMessageText.Text = errorMessage;
                ErrorCard.Visibility = Visibility.Visible;

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