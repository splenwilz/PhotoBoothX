using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Photobooth.Services;
using Photobooth.Models;

namespace Photobooth
{
    /// <summary>
    /// Smartphone connection screen for WiFi setup and photo upload
    /// Displays QR code and instructions for smartphone photo transfer
    /// </summary>
    public partial class SmartphoneConnectScreen : UserControl, IDisposable
    {
        #region Events

        /// <summary>
        /// Event fired when user wants to go back to product selection
        /// </summary>
        public event EventHandler? BackRequested;

        /// <summary>
        /// Event fired when user wants to skip and use camera instead
        /// </summary>
        public event EventHandler? SkipRequested;

        /// <summary>
        /// Event fired when a photo is successfully uploaded
        /// </summary>
        public event EventHandler<PhotoUploadedEventArgs>? PhotoUploaded;

        /// <summary>
        /// Event fired when connection times out
        /// </summary>
        public event EventHandler? ConnectionTimedOut;

        #endregion

        #region Private Fields

        private readonly IWiFiHotspotService _wifiService;
        private readonly IPhotoUploadService _uploadService;
        private readonly IQRCodeService _qrService;
        private readonly ProductInfo _productInfo;

        private DispatcherTimer? _timeoutTimer;
        private DispatcherTimer? _statusUpdateTimer;
        private const double TIMEOUT_MINUTES = 5.0;
        private DateTime _sessionStartTime;
        private bool _disposed = false;
        private bool _photoReceived = false;

        // Connection state
        private bool _wifiStarted = false;
        private bool _serverStarted = false;
        private int _connectedDevices = 0;
        private int _photosUploaded = 0;

        #endregion

        #region Constructor

        public SmartphoneConnectScreen(ProductInfo productInfo)
        {
            _productInfo = productInfo ?? throw new ArgumentNullException(nameof(productInfo));
            
            // Initialize services
            _wifiService = new WiFiHotspotService();
            _uploadService = new PhotoUploadService();
            _qrService = new QRCodeService();

            InitializeComponent();
            
            // Subscribe to service events
            SubscribeToServiceEvents();
            
            // Initialize UI
            InitializeUI();
            
            // Start connection process
            _ = StartConnectionProcessAsync();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Force stop the connection session
        /// </summary>
        public async Task StopConnectionAsync()
        {
            try
            {
                StopTimers();
                
                await _uploadService.StopServiceAsync();
                await _wifiService.StopHotspotAsync();
                
                LoggingService.Application.Information("Smartphone connection session stopped");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error stopping smartphone connection", ex);
            }
        }

        #endregion

        #region Private Methods - Initialization

        private void InitializeUI()
        {
            _sessionStartTime = DateTime.Now;
            
            // Set initial status
            UpdateWiFiStatus("Starting...", "Initializing WiFi hotspot", false);
            UpdateServerStatus("Starting...", "Initializing upload server", false);
            UpdateConnectionStatus("Waiting...", "0 devices connected", false);
            UpdateUploadStatus("Ready", "Waiting for photo", false);
            
            // Initialize timeout
            TimeoutProgressBar.Value = 0;
            UpdateTimeoutDisplay(TIMEOUT_MINUTES);
        }

        private void SubscribeToServiceEvents()
        {
            // WiFi hotspot events
            _wifiService.StatusChanged += WiFiService_StatusChanged;
            _wifiService.ClientConnected += WiFiService_ClientConnected;
            _wifiService.ClientDisconnected += WiFiService_ClientDisconnected;
            _wifiService.HotspotError += WiFiService_HotspotError;

            // Upload service events
            _uploadService.StatusChanged += UploadService_StatusChanged;
            _uploadService.PhotoUploaded += UploadService_PhotoUploaded;
            _uploadService.UploadError += UploadService_UploadError;
        }

        #endregion

        #region Private Methods - Connection Process

        private async Task StartConnectionProcessAsync()
        {
            try
            {
                ShowLoadingOverlay("Setting up smartphone connection...");

                LoggingService.Application.Information("Starting smartphone connection process");

                // Step 1: Start WiFi hotspot
                UpdateLoadingText("Starting WiFi hotspot...");
                var wifiStarted = await _wifiService.StartHotspotAsync();
                
                if (!wifiStarted)
                {
                    ShowError("Failed to start WiFi hotspot. Please try again.");
                    return;
                }

                // Step 2: Start upload service
                UpdateLoadingText("Starting upload server...");
                var uploadStarted = await _uploadService.StartServiceAsync(8080, _wifiService.HotspotIPAddress);
                
                if (!uploadStarted)
                {
                    ShowError("Failed to start upload server. Please try again.");
                    return;
                }

                // Step 3: Generate QR code
                UpdateLoadingText("Generating QR code...");
                await GenerateQRCodeAsync();

                // Step 4: Start timers
                StartTimers();

                HideLoadingOverlay();

                LoggingService.Application.Information("Smartphone connection setup completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to start smartphone connection", ex);
                ShowError($"Connection setup failed: {ex.Message}");
            }
        }

        private async Task GenerateQRCodeAsync()
        {
            try
            {
                if (_wifiService.CurrentSSID == null || _wifiService.CurrentPassword == null || _uploadService.UploadUrl == null)
                {
                    throw new InvalidOperationException("WiFi or upload service not properly initialized");
                }

                // Generate WiFi + Upload QR code
                var qrData = _qrService.GenerateWiFiConnectionQR(
                    _wifiService.CurrentSSID,
                    _wifiService.CurrentPassword,
                    _uploadService.UploadUrl);

                // Create QR image
                var qrImage = _qrService.CreateKioskQRImage(qrData, 350);
                QRCodeImage.Source = qrImage;

                // Update manual connection info
                ManualSSIDText.Text = $"Network: {_wifiService.CurrentSSID}";
                ManualPasswordText.Text = $"Password: {_wifiService.CurrentPassword}";
                ManualUrlText.Text = $"Upload URL: {_uploadService.UploadUrl}";

                LoggingService.Application.Information("QR code generated successfully",
                    ("SSID", _wifiService.CurrentSSID),
                    ("UploadURL", _uploadService.UploadUrl));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to generate QR code", ex);
                throw;
            }
        }

        #endregion

        #region Private Methods - Timer Management

        private void StartTimers()
        {
            // Timeout timer
            _timeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timeoutTimer.Tick += TimeoutTimer_Tick;
            _timeoutTimer.Start();

            // Status update timer
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();

            LoggingService.Application.Information("Connection timers started");
        }

        private void StopTimers()
        {
            _timeoutTimer?.Stop();
            _statusUpdateTimer?.Stop();
            _timeoutTimer = null;
            _statusUpdateTimer = null;
        }

        private void TimeoutTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var elapsed = DateTime.Now - _sessionStartTime;
                var remaining = TimeSpan.FromMinutes(TIMEOUT_MINUTES) - elapsed;

                if (remaining.TotalSeconds <= 0)
                {
                    // Timeout occurred
                    StopTimers();
                    LoggingService.Application.Warning("Smartphone connection timed out");
                    ConnectionTimedOut?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Update progress bar
                var progressPercent = (elapsed.TotalMinutes / TIMEOUT_MINUTES) * 100;
                TimeoutProgressBar.Value = Math.Min(progressPercent, 100);
                
                // Update time display
                UpdateTimeoutDisplay(remaining.TotalMinutes);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error in timeout timer", ex);
            }
        }

        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Update connection count (simplified - would need real monitoring)
                // In production, this would query the WiFi service for connected devices
                
                // Update upload statistics
                var stats = _uploadService.GetSessionStats();
                _photosUploaded = stats.PhotosUploaded;
                
                UpdateUploadStatus(
                    _photosUploaded > 0 ? "Photos Received" : "Ready for upload",
                    $"{_photosUploaded} photos uploaded",
                    _photosUploaded > 0);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating status", ex);
            }
        }

        #endregion

        #region Private Methods - Service Event Handlers

        private void WiFiService_StatusChanged(object? sender, HotspotStatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                switch (e.NewStatus)
                {
                    case HotspotStatus.Starting:
                        UpdateWiFiStatus("Starting...", "Initializing WiFi hotspot", false);
                        break;
                    case HotspotStatus.Active:
                        UpdateWiFiStatus("Active", $"Network: {_wifiService.CurrentSSID}", true);
                        _wifiStarted = true;
                        break;
                    case HotspotStatus.Error:
                        UpdateWiFiStatus("Error", e.ErrorMessage ?? "Unknown error", false);
                        break;
                    case HotspotStatus.Stopped:
                        UpdateWiFiStatus("Stopped", "WiFi hotspot stopped", false);
                        _wifiStarted = false;
                        break;
                }
            });
        }

        private void WiFiService_ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _connectedDevices++;
                UpdateConnectionStatus("Device Connected", $"{_connectedDevices} devices connected", true);
                
                LoggingService.Application.Information("Device connected to WiFi",
                    ("DeviceIP", e.ClientIPAddress),
                    ("DeviceName", e.ClientDeviceName ?? "Unknown"));
            });
        }

        private void WiFiService_ClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _connectedDevices = Math.Max(0, _connectedDevices - 1);
                UpdateConnectionStatus(
                    _connectedDevices > 0 ? "Devices Connected" : "Waiting for connection",
                    $"{_connectedDevices} devices connected",
                    _connectedDevices > 0);
            });
        }

        private void WiFiService_HotspotError(object? sender, string errorMessage)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Console.WriteLine($"[SmartphoneConnect] WiFi hotspot error: {errorMessage}");
                
                // Show user-friendly error in the UI
                UpdateWiFiStatus("Error", "WiFi service issue", false);
                
                // Show a more detailed error message in the manual connection section
                if (errorMessage.Contains("Administrator") || errorMessage.Contains("WLAN AutoConfig"))
                {
                    // Update the manual connection text to show the error
                    ManualSSIDText.Text = "⚠️ Administrator Required";
                    ManualPasswordText.Text = "WiFi hotspot needs Administrator privileges";
                    ManualUrlText.Text = "Solution: Restart as Administrator or start WLAN AutoConfig service";
                }
                else if (errorMessage.Contains("No WiFi adapter") || errorMessage.Contains("WiFi-enabled device"))
                {
                    // Update for no WiFi hardware
                    ManualSSIDText.Text = "📡 No WiFi Adapter Detected";
                    ManualPasswordText.Text = "This system doesn't have WiFi capability";
                    ManualUrlText.Text = "Alternative: Use USB cable or 'Skip & Use Camera Instead'";
                }
            });
        }

        private void UploadService_StatusChanged(object? sender, UploadServiceStatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                switch (e.NewStatus)
                {
                    case UploadServiceStatus.Starting:
                        UpdateServerStatus("Starting...", "Initializing upload server", false);
                        break;
                    case UploadServiceStatus.Running:
                        UpdateServerStatus("Running", $"Server: {_uploadService.UploadUrl}", true);
                        _serverStarted = true;
                        break;
                    case UploadServiceStatus.Error:
                        UpdateServerStatus("Error", e.ErrorMessage ?? "Unknown error", false);
                        break;
                    case UploadServiceStatus.Stopped:
                        UpdateServerStatus("Stopped", "Upload server stopped", false);
                        _serverStarted = false;
                        break;
                }
            });
        }

        private void UploadService_PhotoUploaded(object? sender, PhotoUploadedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _photoReceived = true;
                StopTimers();
                
                LoggingService.Application.Information("Photo uploaded successfully",
                    ("FileName", e.OriginalFileName),
                    ("FileSize", e.FileSize),
                    ("ClientIP", e.ClientIPAddress));
                
                // Fire event to parent
                PhotoUploaded?.Invoke(this, e);
            });
        }

        private void UploadService_UploadError(object? sender, UploadErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LoggingService.Application.Warning("Photo upload error",
                    ("Error", e.ErrorMessage),
                    ("ClientIP", e.ClientIPAddress ?? "Unknown"));
                
                // Update status but don't stop - allow retry
                UpdateUploadStatus("Upload Error", e.ErrorMessage, false);
            });
        }

        #endregion

        #region Private Methods - UI Updates

        private void UpdateWiFiStatus(string status, string details, bool success)
        {
            WiFiStatusText.Text = status;
            WiFiStatusDetails.Text = details;
            WiFiStatusIcon.Text = success ? "✅" : (status == "Starting..." ? "⏳" : "❌");
            WiFiStatusBorder.Background = new SolidColorBrush(success ? Color.FromRgb(34, 197, 94) : 
                                                              status == "Starting..." ? Color.FromRgb(234, 179, 8) : 
                                                              Color.FromRgb(239, 68, 68));
        }

        private void UpdateServerStatus(string status, string details, bool success)
        {
            ServerStatusText.Text = status;
            ServerStatusDetails.Text = details;
            ServerStatusIcon.Text = success ? "✅" : (status == "Starting..." ? "⏳" : "❌");
            ServerStatusBorder.Background = new SolidColorBrush(success ? Color.FromRgb(34, 197, 94) : 
                                                               status == "Starting..." ? Color.FromRgb(234, 179, 8) : 
                                                               Color.FromRgb(239, 68, 68));
        }

        private void UpdateConnectionStatus(string status, string details, bool success)
        {
            ConnectionStatusText.Text = status;
            ConnectionStatusDetails.Text = details;
            ConnectionStatusIcon.Text = success ? "📱" : "⏳";
            ConnectionStatusBorder.Background = new SolidColorBrush(success ? Color.FromRgb(34, 197, 94) : Color.FromRgb(107, 114, 128));
        }

        private void UpdateUploadStatus(string status, string details, bool success)
        {
            UploadStatusText.Text = status;
            UploadStatusDetails.Text = details;
            UploadStatusIcon.Text = success ? "✅" : "⬆️";
            UploadStatusBorder.Background = new SolidColorBrush(success ? Color.FromRgb(34, 197, 94) : Color.FromRgb(107, 114, 128));
        }

        private void UpdateTimeoutDisplay(double remainingMinutes)
        {
            var minutes = (int)remainingMinutes;
            var seconds = (int)((remainingMinutes - minutes) * 60);
            TimeoutText.Text = $"{minutes}:{seconds:D2} remaining";
        }

        private void ShowLoadingOverlay(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void UpdateLoadingText(string message)
        {
            LoadingText.Text = message;
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            HideLoadingOverlay();
            MessageBox.Show(message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region Event Handlers

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                StopTimers();
                
                // Unsubscribe from events
                if (_wifiService != null)
                {
                    _wifiService.StatusChanged -= WiFiService_StatusChanged;
                    _wifiService.ClientConnected -= WiFiService_ClientConnected;
                    _wifiService.ClientDisconnected -= WiFiService_ClientDisconnected;
                    _wifiService.HotspotError -= WiFiService_HotspotError;
                    _wifiService.Dispose();
                }

                if (_uploadService != null)
                {
                    _uploadService.StatusChanged -= UploadService_StatusChanged;
                    _uploadService.PhotoUploaded -= UploadService_PhotoUploaded;
                    _uploadService.UploadError -= UploadService_UploadError;
                    _uploadService.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
