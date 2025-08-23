using System;
using System.Windows.Media.Imaging;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for QR code generation service
    /// Used for smartphone WiFi connection and photo upload URLs
    /// </summary>
    public interface IQRCodeService
    {
        #region WiFi QR Codes

        /// <summary>
        /// Generate QR code for WiFi connection with upload URL
        /// Uses the standard WIFI QR format with custom extension for upload URL
        /// </summary>
        /// <param name="ssid">WiFi network name</param>
        /// <param name="password">WiFi password</param>
        /// <param name="uploadUrl">Photo upload URL</param>
        /// <param name="security">Security type (WPA/WEP/nopass)</param>
        /// <returns>QR code data string</returns>
        string GenerateWiFiConnectionQR(string ssid, string password, string uploadUrl, string security = "WPA");

        /// <summary>
        /// Generate simple QR code for upload URL only
        /// For users who manually connect to WiFi
        /// </summary>
        /// <param name="uploadUrl">Photo upload URL</param>
        /// <returns>QR code data string</returns>
        string GenerateUploadUrlQR(string uploadUrl);

        #endregion

        #region QR Code Images

        /// <summary>
        /// Create QR code image from data string
        /// </summary>
        /// <param name="data">QR code data</param>
        /// <param name="size">Image size in pixels (default: 300)</param>
        /// <param name="margin">Margin around QR code (default: 4)</param>
        /// <returns>BitmapImage for WPF display</returns>
        BitmapImage CreateQRImage(string data, int size = 300, int margin = 4);

        /// <summary>
        /// Create high-contrast QR code optimized for kiosk display
        /// </summary>
        /// <param name="data">QR code data</param>
        /// <param name="size">Image size in pixels</param>
        /// <returns>High-contrast BitmapImage</returns>
        BitmapImage CreateKioskQRImage(string data, int size = 400);

        #endregion

        #region Utilities

        /// <summary>
        /// Validate QR code data format
        /// </summary>
        /// <param name="data">QR code data to validate</param>
        /// <returns>True if data is valid QR format</returns>
        bool ValidateQRData(string data);

        /// <summary>
        /// Get estimated QR code complexity level
        /// Used to determine optimal size and error correction
        /// </summary>
        /// <param name="data">QR code data</param>
        /// <returns>Complexity level (Low/Medium/High)</returns>
        QRComplexity GetDataComplexity(string data);

        /// <summary>
        /// Generate user-friendly instructions for QR code scanning
        /// </summary>
        /// <param name="ssid">WiFi network name</param>
        /// <param name="password">WiFi password</param>
        /// <param name="uploadUrl">Upload URL for manual entry</param>
        /// <returns>Formatted instruction text</returns>
        string GenerateConnectionInstructions(string ssid, string password, string uploadUrl);

        #endregion
    }

    #region Enums

    /// <summary>
    /// QR code data complexity levels
    /// </summary>
    public enum QRComplexity
    {
        /// <summary>
        /// Simple data, low error correction needed
        /// </summary>
        Low,

        /// <summary>
        /// Medium complexity, standard error correction
        /// </summary>
        Medium,

        /// <summary>
        /// Complex data, high error correction recommended
        /// </summary>
        High
    }

    #endregion
}
