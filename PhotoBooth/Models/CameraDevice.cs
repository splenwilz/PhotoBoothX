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
} 