using System;
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

        #region Private Fields
        private DispatcherTimer? _progressTimer;
        private DispatcherTimer? _countdownTimer;
        private double _currentProgress = 0;
        private int _countdownSeconds = 10;
        private bool _disposed = false;

        // Print job details
        private Template? _template;
        private ProductInfo? _product;
        private string? _composedImagePath;
        private int _totalCopies = 1;
        private int _extraCopies = 0;
        private ProductInfo? _crossSellProduct;
        #endregion

        #region Constructor
        public PrintingScreen()
        {
            InitializeComponent();
            Loaded += PrintingScreen_Loaded;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the printing screen with order details
        /// </summary>
        public async Task InitializePrintJob(Template template, ProductInfo product, string composedImagePath, 
            int extraCopies = 0, ProductInfo? crossSellProduct = null)
        {
            try
            {
                // Reset UI state from any previous print jobs
                ResetPrintingState();

                _template = template;
                _product = product;
                _composedImagePath = composedImagePath;
                _extraCopies = extraCopies;
                _crossSellProduct = crossSellProduct;
                _totalCopies = 1 + extraCopies + (crossSellProduct != null ? 1 : 0);

                // Update UI with order details
                UpdateOrderDisplay();

                // Start the printing simulation
                await StartPrintingProcess();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize print job", ex);
                ShowError("Failed to start printing process. Please try again.");
            }
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

            // Update printer status (simulation)
            PrinterNameText.Text = "DNP DS620A"; // This would come from actual printer service
            PrintsRemainingText.Text = "642"; // This would come from consumables tracking
            QueuePositionText.Text = "1 of 1"; // This would come from print queue
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
                return $"{totalSeconds / 60}:{totalSeconds % 60:D2} minutes";
        }
        #endregion

        #region Event Handlers
        private void PrintingScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Start any animations or initialization here
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
                StopProgressTimer();

                LoggingService.Application.Information("Print job completed successfully",
                    ("TemplateName", _template?.Name ?? "Unknown"),
                    ("TotalCopies", _totalCopies));

                // Update header
                HeaderText.Text = "Printing Complete!";
                SubHeaderText.Text = "Your photos are ready";
                StatusText.Text = "Complete";

                // Show completion card
                CompletionCard.Visibility = Visibility.Visible;

                // Start countdown timer
                StartCountdownTimer();

                // Update consumables (simulation)
                await UpdateConsumables();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Print completion failed", ex);
                ShowError("Print completion encountered an error.");
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
                if (newRemaining < 50)
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
                Interval = TimeSpan.FromMilliseconds(100)
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
            
            _countdownSeconds = 10;
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
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
            _countdownSeconds--;
            
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