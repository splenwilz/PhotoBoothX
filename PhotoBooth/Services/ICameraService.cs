using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for camera operations to enable mocking and testing
    /// </summary>
    public interface ICameraService : IDisposable
    {
        #region Events

        /// <summary>
        /// Fired when a new frame is available for preview
        /// </summary>
        event EventHandler<WriteableBitmap>? PreviewFrameReady;

        /// <summary>
        /// Fired when camera encounters an error
        /// </summary>
        event EventHandler<string>? CameraError;

        #endregion

        #region Properties

        /// <summary>
        /// Check if camera is currently capturing
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Check if photo capture is currently active
        /// </summary>
        bool IsPhotoCaptureActive { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Get list of available cameras
        /// </summary>
        List<CameraDevice> GetAvailableCameras();

        /// <summary>
        /// Start camera capture with optimized settings
        /// </summary>
        Task<bool> StartCameraAsync(int cameraIndex = 0);

        /// <summary>
        /// Stop camera capture
        /// </summary>
        void StopCamera();

        /// <summary>
        /// Capture high-resolution photo
        /// </summary>
        Task<string?> CapturePhotoAsync(string? customFileName = null);

        /// <summary>
        /// Get current preview bitmap
        /// </summary>
        WriteableBitmap? GetPreviewBitmap();

        /// <summary>
        /// Check if a new frame is available for preview update
        /// </summary>
        bool IsNewFrameAvailable();

        /// <summary>
        /// Set photo capture state to reduce UI updates during capture
        /// </summary>
        void SetPhotoCaptureActive(bool isActive);

        /// <summary>
        /// Run comprehensive camera diagnostics to identify common issues
        /// </summary>
        Task<CameraDiagnosticResult> RunDiagnosticsAsync();

        /// <summary>
        /// Set camera brightness (0-100)
        /// </summary>
        bool SetBrightness(int brightness);

        /// <summary>
        /// Set camera zoom (100-300, where 100 = no zoom)
        /// </summary>
        bool SetZoom(int zoomPercentage);

        /// <summary>
        /// Set camera contrast (0-100)
        /// </summary>
        bool SetContrast(int contrast);

        /// <summary>
        /// Get current camera brightness setting
        /// </summary>
        int GetBrightness();

        /// <summary>
        /// Get current camera zoom setting
        /// </summary>
        int GetZoom();

        /// <summary>
        /// Get current camera contrast setting
        /// </summary>
        int GetContrast();

        /// <summary>
        /// Reset all camera settings to defaults
        /// </summary>
        void ResetCameraSettings();

        #endregion
    }
} 