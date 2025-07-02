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
        bool StartCamera(int cameraIndex = 0);

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

        #endregion
    }
} 