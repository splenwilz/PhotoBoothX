using System.Collections.Generic;

namespace Photobooth.Models
{
    /// <summary>
    /// Represents a camera device available for photo capture
    /// </summary>
    public class CameraDevice
    {
        /// <summary>
        /// Zero-based index of the camera device
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Human-readable name of the camera device
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// DirectShow moniker string for camera identification
        /// </summary>
        public string MonikerString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Results from camera diagnostic checks
    /// </summary>
    public class CameraDiagnosticResult
    {
        /// <summary>
        /// Overall status of camera system
        /// </summary>
        public string OverallStatus { get; set; } = "Unknown";

        /// <summary>
        /// Whether camera access is allowed in Windows privacy settings
        /// </summary>
        public bool PrivacySettingsAllowed { get; set; }

        /// <summary>
        /// Number of cameras detected
        /// </summary>
        public int CamerasDetected { get; set; }

        /// <summary>
        /// Whether the app can successfully access the camera
        /// </summary>
        public bool CanAccessCamera { get; set; }

        /// <summary>
        /// Windows version information
        /// </summary>
        public string WindowsVersion { get; set; } = string.Empty;

        /// <summary>
        /// List of identified issues
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();

        /// <summary>
        /// List of suggested solutions
        /// </summary>
        public List<string> Solutions { get; set; } = new List<string>();

        /// <summary>
        /// List of processes that may conflict with camera access
        /// </summary>
        public List<string> ConflictingProcesses { get; set; } = new List<string>();
    }
} 