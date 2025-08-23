using System;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace Photobooth.Services
{
    /// <summary>
    /// QR Code generation service for smartphone WiFi connection and photo upload
    /// Uses ZXing.Net library for QR code generation
    /// </summary>
    public class QRCodeService : IQRCodeService
    {
        #region Private Fields

        private readonly BarcodeWriter _barcodeWriter;

        #endregion

        #region Constructor

        public QRCodeService()
        {
            _barcodeWriter = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Height = 300,
                    Width = 300,
                    Margin = 4
                }
            };
        }

        #endregion

        #region WiFi QR Codes

        /// <summary>
        /// Generate WiFi QR code with upload URL
        /// Uses standard WIFI format: WIFI:T:WPA;S:ssid;P:password;H:false;;
        /// Extended with custom URL parameter for PhotoBooth
        /// </summary>
        public string GenerateWiFiConnectionQR(string ssid, string password, string uploadUrl, string security = "WPA")
        {
            try
            {
                if (string.IsNullOrEmpty(ssid))
                    throw new ArgumentException("SSID cannot be empty", nameof(ssid));

                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be empty", nameof(password));

                if (string.IsNullOrEmpty(uploadUrl))
                    throw new ArgumentException("Upload URL cannot be empty", nameof(uploadUrl));

                // Standard WiFi QR format with custom PhotoBooth extension
                var qrData = new StringBuilder();
                qrData.Append("WIFI:");
                qrData.Append($"T:{security};");
                qrData.Append($"S:{EscapeQRString(ssid)};");
                qrData.Append($"P:{EscapeQRString(password)};");
                qrData.Append("H:false;"); // Network is not hidden
                
                // Custom extension for PhotoBooth upload URL
                qrData.Append($"U:{EscapeQRString(uploadUrl)};");
                qrData.Append(";");

                var result = qrData.ToString();

                LoggingService.Application.Information("Generated WiFi QR code",
                    ("SSID", ssid),
                    ("UploadURL", uploadUrl),
                    ("DataLength", result.Length));

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to generate WiFi QR code", ex);
                throw;
            }
        }

        /// <summary>
        /// Generate simple upload URL QR code
        /// </summary>
        public string GenerateUploadUrlQR(string uploadUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(uploadUrl))
                    throw new ArgumentException("Upload URL cannot be empty", nameof(uploadUrl));

                LoggingService.Application.Information("Generated upload URL QR code", ("URL", uploadUrl));
                return uploadUrl;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to generate upload URL QR code", ex);
                throw;
            }
        }

        #endregion

        #region QR Code Images

        /// <summary>
        /// Create QR code image for WPF display
        /// </summary>
        public BitmapImage CreateQRImage(string data, int size = 300, int margin = 4)
        {
            try
            {
                if (string.IsNullOrEmpty(data))
                    throw new ArgumentException("QR data cannot be empty", nameof(data));

                // Configure encoding options
                var options = new EncodingOptions
                {
                    Height = size,
                    Width = size,
                    Margin = margin,
                    PureBarcode = false
                };

                // Set error correction level based on data complexity
                var complexity = GetDataComplexity(data);
                switch (complexity)
                {
                    case QRComplexity.Low:
                        options.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.L;
                        break;
                    case QRComplexity.Medium:
                        options.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.M;
                        break;
                    case QRComplexity.High:
                        options.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.H;
                        break;
                }

                // Create barcode writer with options
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = options
                };

                // Generate bitmap
                using var bitmap = writer.Write(data);
                
                // Convert to BitmapImage for WPF
                var bitmapImage = new BitmapImage();
                using var memoryStream = new MemoryStream();
                
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Make it cross-thread accessible

                LoggingService.Application.Information("Created QR code image",
                    ("Size", size),
                    ("DataLength", data.Length),
                    ("Complexity", complexity.ToString()));

                return bitmapImage;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create QR code image", ex);
                throw;
            }
        }

        /// <summary>
        /// Create high-contrast QR code for kiosk display
        /// </summary>
        public BitmapImage CreateKioskQRImage(string data, int size = 400)
        {
            try
            {
                // Use high error correction and larger margin for kiosk environment
                var options = new EncodingOptions
                {
                    Height = size,
                    Width = size,
                    Margin = 8, // Larger margin for kiosk
                    PureBarcode = false
                };

                // Use high error correction for kiosk environment
                options.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.H;

                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = options
                };

                using var bitmap = writer.Write(data);
                
                // Create high-contrast version
                var highContrastBitmap = CreateHighContrastBitmap(bitmap);
                
                // Convert to BitmapImage
                var bitmapImage = new BitmapImage();
                using var memoryStream = new MemoryStream();
                
                highContrastBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                highContrastBitmap.Dispose();

                LoggingService.Application.Information("Created kiosk QR code image", ("Size", size));

                return bitmapImage;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create kiosk QR code image", ex);
                throw;
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Validate QR code data format
        /// </summary>
        public bool ValidateQRData(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data))
                    return false;

                // Check length limits (QR codes have maximum capacity)
                if (data.Length > 2953) // Max for alphanumeric mode
                    return false;

                // Try to encode to verify validity
                var testOptions = new EncodingOptions
                {
                    Height = 100,
                    Width = 100
                };

                var testWriter = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = testOptions
                };

                using var testBitmap = testWriter.Write(data);
                return testBitmap != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get estimated QR code complexity
        /// </summary>
        public QRComplexity GetDataComplexity(string data)
        {
            if (string.IsNullOrEmpty(data))
                return QRComplexity.Low;

            var length = data.Length;

            if (length <= 100)
                return QRComplexity.Low;
            else if (length <= 500)
                return QRComplexity.Medium;
            else
                return QRComplexity.High;
        }

        /// <summary>
        /// Generate user-friendly connection instructions
        /// </summary>
        public string GenerateConnectionInstructions(string ssid, string password, string uploadUrl)
        {
            var instructions = new StringBuilder();
            
            instructions.AppendLine("📱 Connect Your Phone");
            instructions.AppendLine();
            instructions.AppendLine("Option 1: Scan QR Code");
            instructions.AppendLine("• Open your camera app");
            instructions.AppendLine("• Point at the QR code");
            instructions.AppendLine("• Tap the notification to connect");
            instructions.AppendLine();
            instructions.AppendLine("Option 2: Manual Connection");
            instructions.AppendLine($"• WiFi Network: {ssid}");
            instructions.AppendLine($"• Password: {password}");
            instructions.AppendLine($"• Then visit: {uploadUrl}");
            instructions.AppendLine();
            instructions.AppendLine("Upload your photo and we'll print it!");

            return instructions.ToString();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Escape special characters for QR string format
        /// </summary>
        private string EscapeQRString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", "\\\\")  // Escape backslashes
                .Replace(";", "\\;")    // Escape semicolons
                .Replace(",", "\\,")    // Escape commas
                .Replace("\"", "\\\""); // Escape quotes
        }

        /// <summary>
        /// Create high-contrast version of bitmap for better kiosk visibility
        /// </summary>
        private System.Drawing.Bitmap CreateHighContrastBitmap(System.Drawing.Bitmap original)
        {
            var result = new System.Drawing.Bitmap(original.Width, original.Height);
            
            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    var pixel = original.GetPixel(x, y);
                    
                    // Convert to pure black or white for maximum contrast
                    var brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    var newColor = brightness > 128 
                        ? System.Drawing.Color.White 
                        : System.Drawing.Color.Black;
                    
                    result.SetPixel(x, y, newColor);
                }
            }
            
            return result;
        }

        #endregion
    }
}
