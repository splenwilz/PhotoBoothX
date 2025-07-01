using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows;
using Photobooth.Services;

namespace Photobooth.Tests.Mocks
{
    /// <summary>
    /// Mock camera service for testing that doesn't require actual camera hardware
    /// </summary>
    public class MockCameraService : ICameraService
    {
        #region Private Fields

        private bool _isCapturing = false;
        private bool _isPhotoCaptureActive = false;
        private WriteableBitmap? _mockPreviewBitmap;
        private bool _newFrameAvailable = false;
        private readonly List<CameraDevice> _mockCameras;
        private readonly string _outputDirectory;
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<WriteableBitmap>? PreviewFrameReady;
        public event EventHandler<string>? CameraError;

        #endregion

        #region Constructor

        public MockCameraService(int mockCameraCount = 1)
        {
            _mockCameras = new List<CameraDevice>();
            
            // Create mock cameras
            for (int i = 0; i < mockCameraCount; i++)
            {
                _mockCameras.Add(new CameraDevice
                {
                    Index = i,
                    Name = $"Mock Camera {i + 1}",
                    MonikerString = $"mock_camera_{i}"
                });
            }

            _outputDirectory = Path.Combine(Path.GetTempPath(), "PhotoBoothX_Mock_Photos");
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }

            // Create a mock preview bitmap if we have a dispatcher
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _mockPreviewBitmap = new WriteableBitmap(
                        640, 480, 96, 96, 
                        PixelFormats.Bgr24, null);
                });
            }
        }

        #endregion

        #region Properties

        public bool IsCapturing => _isCapturing;
        public bool IsPhotoCaptureActive => _isPhotoCaptureActive;

        #endregion

        #region Methods

        public List<CameraDevice> GetAvailableCameras()
        {
            return new List<CameraDevice>(_mockCameras);
        }

        public async Task<bool> StartCameraAsync(int cameraIndex = 0)
        {
            await Task.Delay(10); // Simulate startup time

            if (cameraIndex < 0 || cameraIndex >= _mockCameras.Count)
            {
                return false;
            }

            _isCapturing = true;
            _newFrameAvailable = true;

            // Simulate preview frame generation
            _ = Task.Run(async () =>
            {
                while (_isCapturing && !_disposed)
                {
                    await Task.Delay(50); // ~20 FPS
                    
                    if (_mockPreviewBitmap != null && PreviewFrameReady != null)
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(() =>
                        {
                            PreviewFrameReady?.Invoke(this, _mockPreviewBitmap);
                        });
                    }
                    
                    _newFrameAvailable = true;
                }
            });

            return true;
        }

        public void StopCamera()
        {
            _isCapturing = false;
            _newFrameAvailable = false;
        }

        public async Task<string?> CapturePhotoAsync(string? customFileName = null)
        {
            if (!_isCapturing)
            {
                return null;
            }

            await Task.Delay(10); // Simulate capture time

            // Generate filename
            var fileName = customFileName ?? $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            if (!fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".jpg";
            }

            var filePath = Path.Combine(_outputDirectory, fileName);

            // Create a mock image file
            await Task.Run(() =>
            {
                // Create a simple 1x1 pixel JPEG file as mock
                var mockImageBytes = new byte[] 
                {
                    0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
                    0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
                    0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
                    0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
                    0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
                    0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
                    0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
                    0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x01,
                    0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
                    0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0xFF, 0xDA,
                    0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0xD2, 0xFF, 0xD9
                };
                
                File.WriteAllBytes(filePath, mockImageBytes);
            });

            return filePath;
        }

        public WriteableBitmap? GetPreviewBitmap()
        {
            return _mockPreviewBitmap;
        }

        public bool IsNewFrameAvailable()
        {
            if (_newFrameAvailable)
            {
                _newFrameAvailable = false;
                return true;
            }
            return false;
        }

        public void SetPhotoCaptureActive(bool isActive)
        {
            _isPhotoCaptureActive = isActive;
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Simulate a camera error for testing
        /// </summary>
        public void SimulateCameraError(string errorMessage)
        {
            CameraError?.Invoke(this, errorMessage);
        }

        /// <summary>
        /// Set the number of mock cameras available
        /// </summary>
        public void SetMockCameraCount(int count)
        {
            _mockCameras.Clear();
            for (int i = 0; i < count; i++)
            {
                _mockCameras.Add(new CameraDevice
                {
                    Index = i,
                    Name = $"Mock Camera {i + 1}",
                    MonikerString = $"mock_camera_{i}"
                });
            }
        }

        #endregion

        #region IDisposable

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
                
                // Clean up mock files
                try
                {
                    if (Directory.Exists(_outputDirectory))
                    {
                        var mockFiles = Directory.GetFiles(_outputDirectory, "*.jpg");
                        foreach (var file in mockFiles)
                        {
                            try { File.Delete(file); } catch { /* Ignore */ }
                        }
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _disposed = true;
            }
        }

        #endregion
    }
} 