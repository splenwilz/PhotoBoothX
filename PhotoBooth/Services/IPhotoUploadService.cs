using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for photo upload web service
    /// Provides HTTP server for smartphone photo uploads
    /// </summary>
    public interface IPhotoUploadService : IDisposable
    {
        #region Events

        /// <summary>
        /// Fired when a photo is successfully uploaded
        /// </summary>
        event EventHandler<PhotoUploadedEventArgs>? PhotoUploaded;

        /// <summary>
        /// Fired when upload service status changes
        /// </summary>
        event EventHandler<UploadServiceStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// Fired when an upload error occurs
        /// </summary>
        event EventHandler<UploadErrorEventArgs>? UploadError;

        #endregion

        #region Properties

        /// <summary>
        /// Check if upload service is running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Current server port
        /// </summary>
        int? CurrentPort { get; }

        /// <summary>
        /// Base URL for uploads
        /// </summary>
        string? BaseUrl { get; }

        /// <summary>
        /// Upload URL for clients
        /// </summary>
        string? UploadUrl { get; }

        /// <summary>
        /// Number of photos uploaded in current session
        /// </summary>
        int PhotosUploadedCount { get; }

        /// <summary>
        /// Current upload session ID
        /// </summary>
        string? SessionId { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Start the photo upload web service
        /// </summary>
        /// <param name="port">Port to listen on (default: 8080)</param>
        /// <param name="hostIpAddress">IP address to bind to (default: hotspot IP)</param>
        /// <returns>True if service started successfully</returns>
        Task<bool> StartServiceAsync(int port = 8080, string? hostIpAddress = null);

        /// <summary>
        /// Stop the photo upload web service
        /// </summary>
        /// <returns>True if service stopped successfully</returns>
        Task<bool> StopServiceAsync();

        /// <summary>
        /// Get list of uploaded photos in current session
        /// </summary>
        /// <returns>List of photo file paths</returns>
        List<string> GetUploadedPhotos();

        /// <summary>
        /// Clear uploaded photos and start new session
        /// </summary>
        void StartNewSession();

        /// <summary>
        /// Get upload statistics for current session
        /// </summary>
        /// <returns>Upload session statistics</returns>
        UploadSessionStats GetSessionStats();

        /// <summary>
        /// Configure upload restrictions
        /// </summary>
        /// <param name="maxFileSize">Maximum file size in bytes (default: 10MB)</param>
        /// <param name="allowedTypes">Allowed MIME types (default: images only)</param>
        /// <param name="maxFiles">Maximum files per session (default: 1)</param>
        void ConfigureUploadRestrictions(long maxFileSize = 10485760, string[]? allowedTypes = null, int maxFiles = 1);

        #endregion
    }

    #region Event Args

    public class PhotoUploadedEventArgs : EventArgs
    {
        public string FilePath { get; set; }
        public string OriginalFileName { get; set; }
        public long FileSize { get; set; }
        public string MimeType { get; set; }
        public string ClientIPAddress { get; set; }
        public DateTime UploadedAt { get; set; }
        public string SessionId { get; set; }

        public PhotoUploadedEventArgs(string filePath, string originalFileName, long fileSize, string mimeType, string clientIP, string sessionId)
        {
            FilePath = filePath;
            OriginalFileName = originalFileName;
            FileSize = fileSize;
            MimeType = mimeType;
            ClientIPAddress = clientIP;
            UploadedAt = DateTime.Now;
            SessionId = sessionId;
        }
    }

    public class UploadServiceStatusChangedEventArgs : EventArgs
    {
        public UploadServiceStatus OldStatus { get; set; }
        public UploadServiceStatus NewStatus { get; set; }
        public string? ErrorMessage { get; set; }

        public UploadServiceStatusChangedEventArgs(UploadServiceStatus oldStatus, UploadServiceStatus newStatus, string? errorMessage = null)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            ErrorMessage = errorMessage;
        }
    }

    public class UploadErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public string? ClientIPAddress { get; set; }
        public string? FileName { get; set; }
        public DateTime ErrorAt { get; set; }

        public UploadErrorEventArgs(string errorMessage, string? clientIP = null, string? fileName = null)
        {
            ErrorMessage = errorMessage;
            ClientIPAddress = clientIP;
            FileName = fileName;
            ErrorAt = DateTime.Now;
        }
    }

    #endregion

    #region Data Classes

    public class UploadSessionStats
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime SessionStarted { get; set; }
        public int PhotosUploaded { get; set; }
        public long TotalBytesUploaded { get; set; }
        public int UploadErrors { get; set; }
        public TimeSpan SessionDuration => DateTime.Now - SessionStarted;
    }

    #endregion

    #region Enums

    public enum UploadServiceStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    #endregion
}
