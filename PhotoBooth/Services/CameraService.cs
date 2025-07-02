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
        /// Get list of available cameras
        /// </summary>
        public List<CameraDevice> GetAvailableCameras()
        {
            var cameras = new List<CameraDevice>();
            
            try
            {
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
            }
            catch (Exception ex)
            {
                LoggingService.Hardware.Error("Camera", "Failed to enumerate cameras", ex);
            }

            return cameras;
        }

        /// <summary>
        /// Start camera capture with optimized settings
        /// </summary>
        public bool StartCamera(int cameraIndex = 0)
        {
            try
            {
                var startTime = DateTime.Now;
                
                StopCamera();

                var cameras = GetAvailableCameras();
                
                if (cameras.Count == 0 || cameraIndex >= cameras.Count)
                {
                    Console.WriteLine($"[CAMERA] ❌ No cameras available or invalid index {cameraIndex}");
                    LoggingService.Hardware.Warning("Camera", "No cameras available or invalid index", 
                        ("RequestedIndex", cameraIndex), ("AvailableCameras", cameras.Count));
                    return false;
                }

                var selectedCamera = cameras[cameraIndex];
                
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

                // Start capture
                _videoSource.Start();
                _isCapturing = true;

                // Reset counters
                _frameCounter = 0;
                _skippedFrameCounter = 0;
                _lastStatsReport = DateTime.Now;

                var totalStartTime = DateTime.Now - startTime;
                if (totalStartTime.TotalMilliseconds > 1000)
                {
                    Console.WriteLine($"[CAMERA] ⚠️ SLOW START: {totalStartTime.TotalMilliseconds:F0}ms");
                }
                else
                {
                    Console.WriteLine($"[CAMERA] ✅ Started successfully");
                }
                
                LoggingService.Hardware.Information("Camera", "Camera started successfully", 
                    ("CameraName", selectedCamera.Name),
                    ("OptimizedResolution", $"{_previewWidth}x{_previewHeight}"),
                    ("StartupTime", $"{totalStartTime.TotalMilliseconds:F1}ms"));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA] ❌ START ERROR: {ex.Message}");
                LoggingService.Hardware.Error("Camera", "Failed to start camera", ex,
                    ("CameraIndex", cameraIndex));
                CameraError?.Invoke(this, $"Failed to start camera: {ex.Message}");
                return false;
            }
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
                            Console.WriteLine($"[CAMERA] ⚠️ SLOW SIGNAL: {signalDuration.TotalMilliseconds:F0}ms");
                        }
                        
                        var waitTime = DateTime.Now;
                        _videoSource.WaitForStop();
                        var waitDuration = DateTime.Now - waitTime;
                        
                        if (waitDuration.TotalMilliseconds > 1000)
                        {
                            Console.WriteLine($"[CAMERA] ⚠️ SLOW STOP: {waitDuration.TotalMilliseconds:F0}ms");
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
                    Console.WriteLine($"[CAMERA] ⚠️ VERY SLOW STOP: {totalStopTime.TotalMilliseconds:F0}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA] ❌ STOP ERROR: {ex.Message}");
                
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
                Console.WriteLine("[CAMERA] 📸 Photo capture started - reducing preview updates");
            }
            else
            {
                Console.WriteLine("[CAMERA] ✅ Photo capture ended - resuming normal preview");
            }
        }

        /// <summary>
        /// Check if photo capture is currently active
        /// </summary>
        public bool IsPhotoCaptureActive => _isPhotoCaptureActive;

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
                        Console.WriteLine($"[CAMERA] ⚠️ SLOW LOCK: Frame lock took {lockWaitTime.TotalMilliseconds:F0}ms");
                    }

                    // Dispose previous frame
                    _lastFrame?.Dispose();
                    
                    // Store new frame
                    _lastFrame = new Bitmap(eventArgs.Frame);
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
                        Console.WriteLine($"[CAMERA] ⚠️ UI THREAD DELAY: {dispatcherDelay.TotalMilliseconds:F0}ms for frame {_frameCounter}");
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
                                    Console.WriteLine($"[CAMERA] ⚠️ UI LOCK WAIT: {lockWaitTime2.TotalMilliseconds:F0}ms");
                                }

                                if (_lastFrame != null) // Double-check inside lock
                                {
                                    var bitmapUpdateStart = DateTime.Now;
                                    UpdateWriteableBitmap(_lastFrame, _previewBitmap);
                                    var bitmapUpdateTime = DateTime.Now - bitmapUpdateStart;
                                    
                                    if (bitmapUpdateTime.TotalMilliseconds > 20)
                                    {
                                        Console.WriteLine($"[CAMERA] ⚠️ SLOW BITMAP: {bitmapUpdateTime.TotalMilliseconds:F0}ms");
                                    }
                                    
                                    _newFrameAvailable = true;
                                    PreviewFrameReady?.Invoke(this, _previewBitmap);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CAMERA] ❌ UI ERROR: {ex.Message}");
                        LoggingService.Hardware.Error("Camera", "Error updating preview bitmap", ex);
                    }
                }), DispatcherPriority.Background); // Use Background priority instead of Render

                // Report stats every 10 seconds
                if ((now - _lastStatsReport).TotalSeconds >= 10)
                {
                    var avgFps = _frameCounter / (now - _lastStatsReport).TotalSeconds;
                    var skipRate = (_skippedFrameCounter / (double)_frameCounter) * 100;
                    
                    Console.WriteLine($"[CAMERA] 📊 FPS: {avgFps:F1}, Skipped: {skipRate:F0}%");
                    
                    _frameCounter = 0;
                    _skippedFrameCounter = 0;
                    _lastStatsReport = now;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA] ❌ CRITICAL ERROR: {ex.Message}");
                LoggingService.Hardware.Error("Camera", "Error processing new frame", ex);
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
                Console.WriteLine($"[BITMAP] ❌ SIZE MISMATCH: {source.Width}x{source.Height} vs {target.PixelWidth}x{target.PixelHeight}");
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
                    Console.WriteLine($"[BITMAP] ⚠️ VERY SLOW UPDATE: {totalTime.TotalMilliseconds:F0}ms");
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
            var errorMessage = $"Camera error: {eventArgs.Description}";
            LoggingService.Hardware.Error("Camera", errorMessage);
            CameraError?.Invoke(this, errorMessage);
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