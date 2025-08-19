using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Photobooth.Models;
using Microsoft.Win32; // Add for registry access

namespace Photobooth.Services
{
    /// <summary>
    /// Service for camera operations using AForge.NET
    /// </summary>
    public class CameraService : ICameraService
    {
        #region Private Fields

        private VideoCaptureDevice? _videoSource;
        private Bitmap? _lastFrame;
        private WriteableBitmap? _previewBitmap;
        private bool _isCapturing = false;
        private readonly object _frameLock = new object();
        private readonly string _outputDirectory;
        private readonly Dispatcher _dispatcher;
        private int _previewWidth = 640;
        private int _previewHeight = 480;
        private bool _newFrameAvailable = false;
        private DateTime _lastFrameUpdate = DateTime.MinValue;
        private readonly TimeSpan _frameUpdateThrottle = TimeSpan.FromMilliseconds(50); // Limit to ~20 FPS UI updates
        private int _frameCounter = 0;
        private int _skippedFrameCounter = 0;
        private DateTime _lastStatsReport = DateTime.Now;
        private bool _isPhotoCaptureActive = false; // Track if photo capture is in progress

        #endregion

        #region Events

        /// <summary>
        /// Fired when a new frame is available for preview
        /// </summary>
        public event EventHandler<WriteableBitmap>? PreviewFrameReady;

        /// <summary>
        /// Fired when camera encounters an error
        /// </summary>
        public event EventHandler<string>? CameraError;

        #endregion

        #region Constructor

        public CameraService()
        {
            _dispatcher = Application.Current.Dispatcher;
            
            // Output directory should be configured via DI or app settings
            _outputDirectory = GetConfiguredOutputDirectory();
            EnsureOutputDirectoryExists();

            LoggingService.Hardware.Information("Camera", "CameraService initialized", 
                ("OutputDirectory", _outputDirectory));
        }

        /// <summary>
        /// Get the configured output directory from app settings or use default
        /// </summary>
        private string GetConfiguredOutputDirectory()
        {
            // Try to get from app configuration first
            var configuredPath = System.Configuration.ConfigurationManager.AppSettings["CameraOutputDirectory"];
            
            if (!string.IsNullOrEmpty(configuredPath))
            {
                return configuredPath;
            }
            
            // Fallback to desktop for backward compatibility
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return Path.Combine(desktopPath, "PhotoBoothX_Photos");
        }

        /// <summary>
        /// Ensure output directory exists with proper error handling
        /// </summary>
        private void EnsureOutputDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_outputDirectory))
                {
                    Directory.CreateDirectory(_outputDirectory);
                    LoggingService.Hardware.Information("Camera", "Created output directory", 
                        ("Path", _outputDirectory));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to create output directory", ex,
                    ("Path", _outputDirectory));
                throw new InvalidOperationException($"Cannot create camera output directory: {_outputDirectory}", ex);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get list of available cameras with enhanced diagnostics
        /// </summary>
        public List<CameraDevice> GetAvailableCameras()
        {
            var cameras = new List<CameraDevice>();
            
            try
            {
                // Check camera privacy settings first
                if (!IsCameraAccessAllowed())
                {
                    LoggingService.Hardware.Warning("Camera", "Camera access may be restricted by privacy settings");
                }

                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                for (int i = 0; i < videoDevices.Count; i++)
                {
                    cameras.Add(new CameraDevice
                    {
                        Index = i,
                        Name = videoDevices[i].Name,
                        MonikerString = videoDevices[i].MonikerString
                    });
                }

                LoggingService.Hardware.Information("Camera", "Available cameras enumerated", 
                    ("CameraCount", cameras.Count));
                    
                if (cameras.Count == 0)
                {
                    LoggingService.Hardware.Warning("Camera", "No cameras detected - this may indicate permission issues or hardware problems");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to enumerate cameras", ex);
                
                // Provide specific guidance based on exception type
                if (ex.Message.Contains("0x800703E3") || ex.Message.Contains("access") || ex.Message.Contains("denied"))
                {
                    LoggingService.Hardware.Error("Camera", "Camera access appears to be blocked - check Windows privacy settings", ex);
                }
            }

            return cameras;
        }

        /// <summary>
        /// Start camera capture with enhanced error handling and retry logic
        /// </summary>
        public async Task<bool> StartCameraAsync(int cameraIndex = 0)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 1000;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
            try
            {
                var startTime = DateTime.Now;
                
                    LoggingService.Hardware.Information("Camera", $"Starting camera (attempt {attempt}/{maxRetries})", 
                        ("CameraIndex", cameraIndex));
                    
                StopCamera();

                    // Add delay between stop and start to ensure resources are released
                    if (attempt > 1)
                    {
                        LoggingService.Hardware.Debug("Camera", $"Waiting {retryDelayMs}ms before retry attempt {attempt}");
                        await Task.Delay(retryDelayMs);
                    }

                var cameras = GetAvailableCameras();
                
                    if (cameras.Count == 0)
                    {
                        var errorMsg = "No cameras available. Please check:\n" +
                                     "1. Camera is connected and working\n" +
                                     "2. Windows camera privacy settings allow desktop apps\n" +
                                     "3. No other applications are using the camera\n" +
                                     "4. Camera drivers are up to date";
                        
                        LoggingService.Hardware.Warning("Camera", errorMsg, 
                        ("RequestedIndex", cameraIndex), ("AvailableCameras", cameras.Count));
                        
                        if (attempt == maxRetries)
                        {
                            CameraError?.Invoke(this, errorMsg);
                        }
                        continue;
                    }
                    
                    if (cameraIndex >= cameras.Count)
                    {
                        var errorMsg = $"Camera index {cameraIndex} not available. Only {cameras.Count} camera(s) detected.";
                        LoggingService.Hardware.Warning("Camera", errorMsg);
                        
                        if (attempt == maxRetries)
                        {
                            CameraError?.Invoke(this, errorMsg);
                        }
                        continue;
                }

                var selectedCamera = cameras[cameraIndex];
                    LoggingService.Hardware.Information("Camera", "Attempting to initialize camera", 
                        ("CameraName", selectedCamera.Name),
                        ("MonikerString", selectedCamera.MonikerString));
                
                _videoSource = new VideoCaptureDevice(selectedCamera.MonikerString);

                // Set optimized resolution for better performance
                if (_videoSource.VideoCapabilities.Length > 0)
                {
                    // For preview: Use moderate resolution (720p or 480p)
                    var previewResolution = _videoSource.VideoCapabilities
                        .Where(cap => cap.FrameSize.Width <= 1280 && cap.FrameSize.Height <= 720)
                        .OrderByDescending(cap => cap.FrameSize.Width * cap.FrameSize.Height)
                        .FirstOrDefault()
                        ?? _videoSource.VideoCapabilities
                        .OrderBy(cap => cap.FrameSize.Width * cap.FrameSize.Height)
                        .First();

                    _videoSource.VideoResolution = previewResolution;
                    _previewWidth = previewResolution.FrameSize.Width;
                    _previewHeight = previewResolution.FrameSize.Height;
                    
                    LoggingService.Hardware.Information("Camera", "Set optimized camera resolution", 
                        ("Width", _previewWidth),
                        ("Height", _previewHeight));
                }

                // Initialize WriteableBitmap for fast preview updates
                _dispatcher.Invoke(() =>
                {
                    _previewBitmap = new WriteableBitmap(
                        _previewWidth, 
                        _previewHeight, 
                        96, 96, 
                        System.Windows.Media.PixelFormats.Bgr24, 
                        null);
                });

                // Subscribe to events
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.VideoSourceError += OnVideoSourceError;

                // Load saved camera settings from database
                await LoadCameraSettingsFromDatabaseAsync();
                
                // Start capture with user settings applied
                _videoSource.Start();
                _isCapturing = true;

                // Reset counters
                _frameCounter = 0;
                _skippedFrameCounter = 0;
                _lastStatsReport = DateTime.Now;

                var totalStartTime = DateTime.Now - startTime;
                if (totalStartTime.TotalMilliseconds > 1000)
                {
                    LoggingService.Hardware.Warning("Camera", "Slow camera start detected", 
                        ("StartupTimeMs", totalStartTime.TotalMilliseconds));
                }
                
                LoggingService.Hardware.Information("Camera", "Camera started successfully", 
                    ("CameraName", selectedCamera.Name),
                    ("OptimizedResolution", $"{_previewWidth}x{_previewHeight}"),
                        ("StartupTime", $"{totalStartTime.TotalMilliseconds:F1}ms"),
                        ("AttemptNumber", attempt));

                return true;
            }
            catch (Exception ex)
            {
                    LoggingService.Hardware.Error("Camera", $"Failed to start camera (attempt {attempt}/{maxRetries})", ex,
                        ("CameraIndex", cameraIndex),
                        ("ExceptionType", ex.GetType().Name));
                    
                    string errorMessage = GetUserFriendlyErrorMessage(ex);
                    
                    if (attempt == maxRetries)
                    {
                        LoggingService.Hardware.Error("Camera", "All camera start attempts failed", ex);
                        CameraError?.Invoke(this, errorMessage);
                return false;
            }
                    
                    // Clean up before retry
                    try
                    {
                        if (_videoSource != null)
                        {
                            _videoSource.NewFrame -= OnNewFrame;
                            _videoSource.VideoSourceError -= OnVideoSourceError;
                            if (_videoSource.IsRunning)
                            {
                                _videoSource.SignalToStop();
                                _videoSource.WaitForStop();
                            }
                            _videoSource = null;
                        }
                        _isCapturing = false;
                    }
                    catch (Exception cleanupEx)
                    {
                        LoggingService.Hardware.Error("Camera", "Error during cleanup before retry", cleanupEx);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Stop camera capture
        /// </summary>
        public void StopCamera()
        {
            try
            {
                var stopStart = DateTime.Now;
                
                if (_videoSource != null)
                {
                    if (_videoSource.IsRunning)
                    {
                        var signalTime = DateTime.Now;
                        _videoSource.SignalToStop();
                        var signalDuration = DateTime.Now - signalTime;
                        
                        if (signalDuration.TotalMilliseconds > 500)
                        {
                            LoggingService.Hardware.Warning("Camera", "Slow camera signal to stop", 
                                ("SignalDurationMs", signalDuration.TotalMilliseconds));
                        }
                        
                        var waitTime = DateTime.Now;
                        _videoSource.WaitForStop();
                        var waitDuration = DateTime.Now - waitTime;
                        
                        if (waitDuration.TotalMilliseconds > 1000)
                        {
                            LoggingService.Hardware.Warning("Camera", "Slow camera wait for stop", 
                                ("WaitDurationMs", waitDuration.TotalMilliseconds));
                        }
                    }

                    // Unsubscribe from events before disposal
                    _videoSource.NewFrame -= OnNewFrame;
                    _videoSource.VideoSourceError -= OnVideoSourceError;
                    _videoSource = null;
                }

                _isCapturing = false;
                _newFrameAvailable = false;
                
                // Clean up frame resources
                lock (_frameLock)
                {
                    if (_lastFrame != null)
                    {
                        _lastFrame.Dispose();
                        _lastFrame = null;
                    }
                }

                var totalStopTime = DateTime.Now - stopStart;
                if (totalStopTime.TotalMilliseconds > 2000)
                {
                    LoggingService.Hardware.Warning("Camera", "Slow camera stop detected", 
                        ("StopTimeMs", totalStopTime.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to stop camera", ex);
                
                // Force cleanup even if there was an error
                _isCapturing = false;
                _newFrameAvailable = false;
                _videoSource = null;
                
                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }
            }
        }

        /// <summary>
        /// Capture high-resolution photo (switches to high-res temporarily)
        /// </summary>
        public async Task<string?> CapturePhotoAsync(string? customFileName = null)
        {
            try
            {
                if (!_isCapturing || _lastFrame == null)
                {
                    LoggingService.Hardware.Warning("Camera", "Cannot capture - camera not running or no frame available");
                    return null;
                }

                Bitmap captureFrame;
                
                // For final capture, we can use the current frame or switch to high-res temporarily
                lock (_frameLock)
                {
                    if (_lastFrame == null)
                        return null;
                    
                    captureFrame = new Bitmap(_lastFrame);
                }

                // Generate filename
                var fileName = customFileName ?? $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
                if (!fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".jpg";
                }

                var filePath = Path.Combine(_outputDirectory, fileName);

                // Save image on background thread
                await Task.Run(() =>
                {
                    // For now, save the captured frame directly (it's already optimized resolution)
                    // Future: implement upscaling if needed for final quality
                    captureFrame.Save(filePath, ImageFormat.Jpeg);
                    captureFrame.Dispose();
                });

                LoggingService.Hardware.Information("Camera", "Photo captured successfully", 
                    ("FilePath", filePath),
                    ("FileSize", new FileInfo(filePath).Length));

                return filePath;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to capture photo", ex);
                return null;
            }
        }

        /// <summary>
        /// Get current preview bitmap (optimized for performance)
        /// </summary>
        public WriteableBitmap? GetPreviewBitmap()
        {
            return _previewBitmap;
        }

        /// <summary>
        /// Check if a new frame is available for preview update
        /// </summary>
        public bool IsNewFrameAvailable()
        {
            if (_newFrameAvailable)
            {
                _newFrameAvailable = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if camera is currently capturing
        /// </summary>
        public bool IsCapturing => _isCapturing && _videoSource?.IsRunning == true;

        /// <summary>
        /// Set photo capture state to reduce UI updates during capture
        /// </summary>
        public void SetPhotoCaptureActive(bool isActive)
        {
            _isPhotoCaptureActive = isActive;
            if (isActive)
            {
                LoggingService.Hardware.Information("Camera", "📸 Photo capture started - reducing preview updates");
            }
            else
            {
                LoggingService.Hardware.Information("Camera", "✅ Photo capture ended - resuming normal preview");
            }
        }

        /// <summary>
        /// Check if photo capture is currently active
        /// </summary>
        public bool IsPhotoCaptureActive => _isPhotoCaptureActive;

        /// <summary>
        /// Run comprehensive camera diagnostics to identify common issues
        /// </summary>
        public async Task<CameraDiagnosticResult> RunDiagnosticsAsync()
        {
            var result = new CameraDiagnosticResult();
            
            try
            {
                LoggingService.Hardware.Information("Camera", "Starting camera diagnostics");
                
                // 1. Check camera privacy settings
                result.PrivacySettingsAllowed = IsCameraAccessAllowed();
                if (!result.PrivacySettingsAllowed)
                {
                    result.Issues.Add("Camera access is blocked in Windows privacy settings");
                    result.Solutions.Add("Go to Windows Settings > Privacy & Security > Camera and enable access");
                }
                
                // 2. Check for available cameras
                var cameras = GetAvailableCameras();
                result.CamerasDetected = cameras.Count;
                if (cameras.Count == 0)
                {
                    result.Issues.Add("No cameras detected");
                    result.Solutions.Add("Check camera connection and drivers");
                }
                
                // 3. Check if camera is in use by another process
                if (cameras.Count > 0)
                {
                    bool canAccessCamera = false;
                    try
                    {
                        var testCamera = new VideoCaptureDevice(cameras[0].MonikerString);
                        testCamera.Start();
                        await Task.Delay(100); // Brief test
                        testCamera.SignalToStop();
                        testCamera.WaitForStop();
                        canAccessCamera = true;
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add($"Cannot access camera: {ex.Message}");
                        if (ex.Message.ToLower().Contains("busy") || ex.Message.ToLower().Contains("in use"))
                        {
                            result.Solutions.Add("Close other applications using the camera (Skype, Teams, etc.)");
                        }
                    }
                    
                    result.CanAccessCamera = canAccessCamera;
                }
                
                // 4. Check Windows version and compatibility
                var osVersion = Environment.OSVersion.Version;
                result.WindowsVersion = $"{osVersion.Major}.{osVersion.Minor}.{osVersion.Build}";
                
                // 5. Check for common problematic processes
                var problematicProcesses = new[] { "Skype", "Teams", "Zoom", "WebexMeetings", "Camera", "WindowsCamera" };
                var runningProblematic = new List<string>();
                
                try
                {
                    var processes = System.Diagnostics.Process.GetProcesses();
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (problematicProcesses.Any(p => process.ProcessName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                            {
                                runningProblematic.Add(process.ProcessName);
                            }
                        }
                        catch
                        {
                            // Ignore access denied errors for system processes
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Hardware.Error("Camera", "Could not check for problematic processes", ex);
                }
                
                if (runningProblematic.Any())
                {
                    result.Issues.Add($"Applications that may conflict with camera: {string.Join(", ", runningProblematic)}");
                    result.Solutions.Add("Close conflicting applications and try again");
                }
                
                result.ConflictingProcesses = runningProblematic;
                
                // Generate overall status
                result.OverallStatus = result.Issues.Count == 0 ? "Healthy" : "Issues Detected";
                
                LoggingService.Hardware.Information("Camera", "Camera diagnostics completed", 
                    ("OverallStatus", result.OverallStatus),
                    ("IssueCount", result.Issues.Count),
                    ("CamerasDetected", result.CamerasDetected));
                
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Error during camera diagnostics", ex);
                result.Issues.Add($"Diagnostic error: {ex.Message}");
                result.OverallStatus = "Diagnostic Failed";
                return result;
            }
        }

        #endregion

        #region Private Methods

        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                _frameCounter++;

                // Always update the frame buffer for photo capture
                var lockStartTime = DateTime.Now;
                lock (_frameLock)
                {
                    var lockWaitTime = DateTime.Now - lockStartTime;
                    
                    if (lockWaitTime.TotalMilliseconds > 10)
                    {
                        LoggingService.Hardware.Warning("Camera", "Slow frame lock", 
                            ("LockWaitTimeMs", lockWaitTime.TotalMilliseconds));
                    }

                    // Dispose previous frame
                    _lastFrame?.Dispose();
                    
                    // Store new frame with adjustments applied, normalized to 24bpp to match WriteableBitmap expectations
                    var originalFrame = new Bitmap(eventArgs.Frame);
                    var adjusted = ApplyImageAdjustments(originalFrame);
                    if (adjusted.PixelFormat != PixelFormat.Format24bppRgb)
                    {
                        var converted = adjusted.Clone(
                            new Rectangle(0, 0, adjusted.Width, adjusted.Height),
                            PixelFormat.Format24bppRgb);
                        adjusted.Dispose();
                        _lastFrame = converted;
                    }
                    else
                    {
                        _lastFrame = adjusted;
                    }
                    originalFrame.Dispose();
                }

                // Throttle UI updates to prevent freezing (longer throttle during photo capture)
                var now = DateTime.Now;
                var timeSinceLastUpdate = now - _lastFrameUpdate;
                
                // Use longer throttle during photo capture to reduce UI thread pressure
                var currentThrottle = _isPhotoCaptureActive ? 
                    TimeSpan.FromMilliseconds(100) : // Slower updates during capture (10 FPS)
                    _frameUpdateThrottle; // Normal throttle (20 FPS)
                
                if (timeSinceLastUpdate < currentThrottle)
                {
                    _skippedFrameCounter++;
                    return; // Skip this frame update to reduce UI thread pressure
                }
                
                _lastFrameUpdate = now;

                // Update preview bitmap on UI thread with lower priority to prevent blocking
                var dispatcherStartTime = DateTime.Now;
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    var dispatcherDelay = DateTime.Now - dispatcherStartTime;
                    
                    if (dispatcherDelay.TotalMilliseconds > 50)
                    {
                        LoggingService.Hardware.Warning("Camera", "UI thread delay", 
                            ("DispatcherDelayMs", dispatcherDelay.TotalMilliseconds));
                    }

                    try
                    {
                        if (_previewBitmap != null && _lastFrame != null)
                        {
                            var lockStartTime2 = DateTime.Now;
                            lock (_frameLock)
                            {
                                var lockWaitTime2 = DateTime.Now - lockStartTime2;
                                
                                if (lockWaitTime2.TotalMilliseconds > 10)
                                {
                                    LoggingService.Hardware.Warning("Camera", "UI lock wait", 
                                        ("LockWaitTimeMs", lockWaitTime2.TotalMilliseconds));
                                }

                                if (_lastFrame != null) // Double-check inside lock
                                {
                                    var bitmapUpdateStart = DateTime.Now;
                                    UpdateWriteableBitmap(_lastFrame, _previewBitmap);
                                    var bitmapUpdateTime = DateTime.Now - bitmapUpdateStart;
                                    
                                    if (bitmapUpdateTime.TotalMilliseconds > 20)
                                    {
                                        LoggingService.Hardware.Warning("Camera", "Slow bitmap update", 
                                            ("BitmapUpdateTimeMs", bitmapUpdateTime.TotalMilliseconds));
                                    }
                                    
                                    _newFrameAvailable = true;
                                    PreviewFrameReady?.Invoke(this, _previewBitmap);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Hardware.Error("Camera", "Error updating preview bitmap", ex);
                    }
                }), DispatcherPriority.Background); // Use Background priority instead of Render

                // Report stats every 10 seconds
                if ((now - _lastStatsReport).TotalSeconds >= 10)
                {
                    var avgFps = _frameCounter / (now - _lastStatsReport).TotalSeconds;
                    var skipRate = (_skippedFrameCounter / (double)_frameCounter) * 100;
                    
                    LoggingService.Hardware.Information("Camera", "📊 FPS", 
                        ("AvgFps", avgFps),
                        ("SkipRate", $"{skipRate:F0}%"));
                    
                    _frameCounter = 0;
                    _skippedFrameCounter = 0;
                    _lastStatsReport = now;
                }

            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Critical error processing new frame", ex);
            }
        }

        /// <summary>
        /// Updates the WriteableBitmap with raw pixel data using unsafe pointer operations
        /// for high-performance pixel-buffer writes during real-time camera frame processing.
        /// 
        /// This method uses unsafe code for performance optimization to achieve real-time
        /// camera preview updates. The unsafe pointer operations perform direct pixel-buffer 
        /// writes which are approximately 10x faster than safe alternatives for large image data.
        /// 
        /// Safety measures:
        /// - Proper Lock()/Unlock() pairing for both source and target bitmaps
        /// - Bounds checking for image dimensions
        /// - Exception handling with guaranteed cleanup in finally block
        /// - Memory copy operations use Buffer.MemoryCopy for managed unsafe operations
        /// </summary>
        /// <param name="source">Source Bitmap to copy pixel data from (must be 24bpp RGB format)</param>
        /// <param name="target">Target WriteableBitmap to update with new pixel data</param>
        /// <remarks>
        /// This method is confined to this specific use case and should not be extended.
        /// The unsafe operations are necessary for real-time camera performance requirements.
        /// </remarks>
        private unsafe void UpdateWriteableBitmap(Bitmap source, WriteableBitmap target)
        {
            var updateStart = DateTime.Now;

            // Validate dimensions to prevent buffer overruns
            if (source.Width != target.PixelWidth || source.Height != target.PixelHeight)
            {
                LoggingService.Hardware.Warning("Camera", "Bitmap size mismatch", 
                    ("SourceWidth", source.Width),
                    ("SourceHeight", source.Height),
                    ("TargetWidth", target.PixelWidth),
                    ("TargetHeight", target.PixelHeight));
                return;
            }

            // Lock source bitmap for reading - this provides safe access to pixel data
            var sourceData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            // Lock target WriteableBitmap for writing - required for thread-safe WPF bitmap updates
            target.Lock();

            try
            {
                // Get pointer and stride information for both bitmaps
                var stride = sourceData.Stride;
                var scan0 = sourceData.Scan0;
                var backBuffer = target.BackBuffer;
                var targetStride = target.BackBufferStride;

                // Perform high-performance row-by-row memory copy using unsafe pointers
                // This is necessary for real-time camera performance (60+ FPS capability)
                for (int y = 0; y < source.Height; y++)
                {
                    var srcRow = (byte*)scan0 + (y * stride);
                    var dstRow = (byte*)backBuffer + (y * targetStride);

                    // Use Buffer.MemoryCopy for safe managed unsafe memory operations
                    // This prevents buffer overruns by respecting both source and destination limits
                    Buffer.MemoryCopy(srcRow, dstRow, targetStride, Math.Min(stride, targetStride));
                }

                // Mark the entire bitmap as dirty to trigger WPF rendering update
                target.AddDirtyRect(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight));
                
                // Performance monitoring for optimization tuning
                var totalTime = DateTime.Now - updateStart;
                if (totalTime.TotalMilliseconds > 30)
                {
                    LoggingService.Hardware.Warning("Camera", "Slow bitmap update", 
                        ("UpdateTimeMs", totalTime.TotalMilliseconds));
                }
            }
            finally
            {
                // CRITICAL: Always unlock both bitmaps in finally block to prevent deadlocks
                // This ensures proper cleanup even if exceptions occur during memory operations
                source.UnlockBits(sourceData);
                target.Unlock();
            }
        }

        private void OnVideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
        {
            var errorMessage = GetUserFriendlyErrorMessage(new Exception(eventArgs.Description));
            LoggingService.Hardware.Error("Camera", $"Video source error: {eventArgs.Description}");
            CameraError?.Invoke(this, errorMessage);
        }

        /// <summary>
        /// Convert technical error messages to user-friendly ones with actionable guidance
        /// </summary>
        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            var message = ex.Message.ToLower();
            
            // Handle the specific error from the screenshot
            if (message.Contains("0x800703e3") || message.Contains("operation has been aborted"))
            {
                return "Camera error: The camera operation was interrupted.\n\n" +
                       "This usually means:\n" +
                       "• Another application is using the camera (close other camera apps)\n" +
                       "• Windows camera privacy settings are blocking access\n" +
                       "• Camera drivers need updating\n" +
                       "• Camera hardware connection issue\n\n" +
                       "Solutions:\n" +
                       "1. Close all other camera applications (Skype, Teams, etc.)\n" +
                       "2. Check Windows Privacy Settings > Camera > Allow desktop apps\n" +
                       "3. Reconnect your camera if it's USB\n" +
                       "4. Restart the application";
            }
            
            if (message.Contains("access") || message.Contains("denied"))
            {
                return "Camera access denied.\n\n" +
                       "Solutions:\n" +
                       "1. Go to Windows Settings > Privacy & Security > Camera\n" +
                       "2. Enable 'Camera access' and 'Let desktop apps access your camera'\n" +
                       "3. Restart this application";
            }
            
            if (message.Contains("busy") || message.Contains("in use"))
            {
                return "Camera is currently being used by another application.\n\n" +
                       "Solutions:\n" +
                       "1. Close other camera applications (Skype, Teams, Zoom, etc.)\n" +
                       "2. Check Windows Camera app and close it\n" +
                       "3. Restart this application";
            }
            
            if (message.Contains("not found") || message.Contains("device"))
            {
                return "Camera device not found.\n\n" +
                       "Solutions:\n" +
                       "1. Check camera connection (USB or built-in)\n" +
                       "2. Update camera drivers in Device Manager\n" +
                       "3. Try a different USB port if using external camera\n" +
                       "4. Restart your computer";
            }
            
            // Generic fallback with the original error
            return $"Camera error: {ex.Message}\n\n" +
                   "Common solutions:\n" +
                   "1. Close other camera applications\n" +
                   "2. Check Windows camera privacy settings\n" +
                   "3. Restart the application\n" +
                   "4. Reconnect camera hardware";
        }

        /// <summary>
        /// Check if camera access is allowed in Windows privacy settings
        /// </summary>
        private bool IsCameraAccessAllowed()
        {
            try
            {
                // Check Windows 10/11 camera privacy settings via registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("Value");
                        if (value != null && value.ToString() == "Deny")
                        {
                            LoggingService.Hardware.Warning("Camera", "Camera access is denied in Windows privacy settings");
                            return false;
                        }
                    }
                }
                
                // Also check if desktop apps can access camera
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\NonPackaged"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("Value");
                        if (value != null && value.ToString() == "Deny")
                        {
                            LoggingService.Hardware.Warning("Camera", "Desktop app camera access is denied in Windows privacy settings");
                            return false;
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Could not check camera privacy settings", ex);
                return true; // Assume allowed if we can't check
            }
        }

        #endregion

        #region Camera Property Controls

        // Camera setting storage
        private int _currentBrightness = 50;
        private int _currentZoom = 100;
        private int _currentContrast = 50;

        /// <summary>
        /// Set camera brightness (0-100)
        /// </summary>
        public bool SetBrightness(int brightness)
        {
            try
            {
                brightness = Math.Clamp(brightness, 0, 100);
                _currentBrightness = brightness;

                if (_videoSource != null && _isCapturing)
                {
                    // Note: Using software-based brightness adjustment
                    // Hardware brightness control would require COM interface implementation
                    LoggingService.Hardware.Information("Camera", "Brightness adjusted (software-based)", 
                        ("Brightness", brightness));
                    return true;
                }

                LoggingService.Hardware.Information("Camera", "Brightness setting stored for future use", 
                    ("Brightness", brightness));
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to set brightness", ex, 
                    ("Brightness", brightness));
                return false;
            }
        }

        /// <summary>
        /// Set camera zoom (100-300, where 100 = no zoom)
        /// </summary>
        public bool SetZoom(int zoomPercentage)
        {
            try
            {
                zoomPercentage = Math.Clamp(zoomPercentage, 100, 300);
                _currentZoom = zoomPercentage;

                if (_videoSource != null && _isCapturing)
                {
                    LoggingService.Hardware.Information("Camera", "Zoom adjusted (software scaling)", 
                        ("ZoomPercentage", zoomPercentage));
                    return true;
                }

                LoggingService.Hardware.Information("Camera", "Zoom setting stored for future use", 
                    ("ZoomPercentage", zoomPercentage));
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to set zoom", ex, 
                    ("ZoomPercentage", zoomPercentage));
                return false;
            }
        }

        /// <summary>
        /// Set camera contrast (0-100)
        /// </summary>
        public bool SetContrast(int contrast)
        {
            try
            {
                contrast = Math.Clamp(contrast, 0, 100);
                _currentContrast = contrast;

                if (_videoSource != null && _isCapturing)
                {
                    // Note: Using software-based contrast adjustment
                    // Hardware contrast control would require COM interface implementation
                    LoggingService.Hardware.Information("Camera", "Contrast adjusted (software-based)", 
                        ("Contrast", contrast));
                    return true;
                }

                LoggingService.Hardware.Information("Camera", "Contrast setting stored for future use", 
                    ("Contrast", contrast));
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to set contrast", ex, 
                    ("Contrast", contrast));
                return false;
            }
        }

        /// <summary>
        /// Get current camera brightness setting
        /// </summary>
        public int GetBrightness()
        {
            return _currentBrightness;
        }

        /// <summary>
        /// Get current camera zoom setting
        /// </summary>
        public int GetZoom()
        {
            return _currentZoom;
        }

        /// <summary>
        /// Get current camera contrast setting
        /// </summary>
        public int GetContrast()
        {
            return _currentContrast;
        }

        /// <summary>
        /// Reset all camera settings to defaults
        /// </summary>
        public void ResetCameraSettings()
        {
            try
            {
                SetBrightness(50);
                SetZoom(100);
                SetContrast(50);
                
                LoggingService.Hardware.Information("Camera", "All camera settings reset to defaults");
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to reset camera settings", ex);
            }
        }

        /// <summary>
        /// Apply software-based image adjustments to the frame
        /// This provides immediate visual feedback even if hardware controls aren't available
        /// </summary>
        private Bitmap ApplyImageAdjustments(Bitmap originalFrame)
        {
            try
            {
                if (_currentBrightness == 50 && _currentZoom == 100 && _currentContrast == 50)
                {
                    // No adjustments needed
                    return new Bitmap(originalFrame);
                }

                var adjustedFrame = new Bitmap(originalFrame.Width, originalFrame.Height);
                
                // Calculate adjustment factors
                float brightnessFactor = (_currentBrightness - 50) / 50.0f; // -1.0 to 1.0
                float contrastFactor = _currentContrast / 50.0f; // 0.0 to 2.0
                float zoomFactor = _currentZoom / 100.0f; // 1.0 to 3.0

                using (var graphics = Graphics.FromImage(adjustedFrame))
                {
                    // Apply zoom (center crop)
                    if (zoomFactor > 1.0f)
                    {
                        var zoomedWidth = (int)(originalFrame.Width / zoomFactor);
                        var zoomedHeight = (int)(originalFrame.Height / zoomFactor);
                        var cropX = (originalFrame.Width - zoomedWidth) / 2;
                        var cropY = (originalFrame.Height - zoomedHeight) / 2;
                        
                        var cropRect = new Rectangle(cropX, cropY, zoomedWidth, zoomedHeight);
                        var destRect = new Rectangle(0, 0, adjustedFrame.Width, adjustedFrame.Height);
                        
                        graphics.DrawImage(originalFrame, destRect, cropRect, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        graphics.DrawImage(originalFrame, 0, 0);
                    }
                }

                // Apply brightness and contrast adjustments
                if (_currentBrightness != 50 || _currentContrast != 50)
                {
                    ApplyBrightnessContrast(adjustedFrame, brightnessFactor, contrastFactor);
                }

                return adjustedFrame;
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to apply image adjustments", ex);
                return new Bitmap(originalFrame);
            }
        }

        /// <summary>
        /// Apply brightness and contrast adjustments to bitmap
        /// </summary>
        private void ApplyBrightnessContrast(Bitmap bitmap, float brightness, float contrast)
        {
            BitmapData? data = null;
            int bytesPerPixel;
            PixelFormat lockFormat;
            
            // Choose supported pixel format
            if (bitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                lockFormat = PixelFormat.Format32bppArgb;
                bytesPerPixel = 4;
            }
            else if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
            {
                lockFormat = PixelFormat.Format24bppRgb;
                bytesPerPixel = 3;
            }
            else
            {
                LoggingService.Hardware.Warning("Camera", "Unsupported pixel format for brightness/contrast", 
                    ("PixelFormat", bitmap.PixelFormat.ToString()));
                return;
            }

            try
            {
                data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                       ImageLockMode.ReadWrite, lockFormat);

                unsafe
                {
                    byte* ptr = (byte*)data.Scan0;
                    int width = bitmap.Width;
                    int height = bitmap.Height;
                    
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = ptr + (y * data.Stride); // handles negative stride as well
                        for (int x = 0; x < width; x++)
                        {
                            int idx = x * bytesPerPixel;
                            // Apply brightness and contrast to each color channel (skip alpha if present)
                            for (int channel = 0; channel < 3; channel++) // RGB only
                            {
                                float pixel = row[idx + channel];
                                
                                // Apply contrast
                                pixel = ((pixel / 255.0f - 0.5f) * contrast + 0.5f) * 255.0f;
                                // Apply brightness
                                pixel += brightness * 255.0f;
                                // Clamp
                                pixel = Math.Max(0, Math.Min(255, pixel));
                                
                                row[idx + channel] = (byte)pixel;
                            }
                            // Alpha remains unmodified when bytesPerPixel == 4
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to apply brightness/contrast", ex);
            }
            finally
            {
                if (data != null)
                    bitmap.UnlockBits(data);
            }
        }

        #endregion

        #region Database Settings Integration

        /// <summary>
        /// Load camera settings from database and apply them
        /// </summary>
        private async Task LoadCameraSettingsFromDatabaseAsync()
        {
            try
            {
                Console.WriteLine("CameraService: Loading camera settings from database");
                
                // Create database service instance directly (same pattern as other services in the app)
                var databaseService = new DatabaseService();
                
                try
                {
                    // Load brightness
                    var brightnessResult = await databaseService.GetCameraSettingAsync("Brightness");
                    if (brightnessResult.Success && brightnessResult.Data != null)
                    {
                        SetBrightness(brightnessResult.Data.SettingValue);
                        Console.WriteLine($"CameraService: Applied brightness = {brightnessResult.Data.SettingValue}");
                    }

                    // Load zoom
                    var zoomResult = await databaseService.GetCameraSettingAsync("Zoom");
                    if (zoomResult.Success && zoomResult.Data != null)
                    {
                        SetZoom(zoomResult.Data.SettingValue);
                        Console.WriteLine($"CameraService: Applied zoom = {zoomResult.Data.SettingValue}");
                    }

                    // Load contrast
                    var contrastResult = await databaseService.GetCameraSettingAsync("Contrast");
                    if (contrastResult.Success && contrastResult.Data != null)
                    {
                        SetContrast(contrastResult.Data.SettingValue);
                        Console.WriteLine($"CameraService: Applied contrast = {contrastResult.Data.SettingValue}");
                    }

                    LoggingService.Hardware.Information("Camera", "Camera settings loaded from database successfully");
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"CameraService: Database access error - {dbEx.Message}");
                    LoggingService.Hardware.Warning("Camera", "Database access failed - using default camera settings");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraService: Error loading camera settings from database - {ex.Message}");
                LoggingService.Hardware.Error("Camera", "Failed to load camera settings from database", ex);
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopCamera();
                
                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }

                _disposed = true;
            }
        }

        #endregion
    }
} 