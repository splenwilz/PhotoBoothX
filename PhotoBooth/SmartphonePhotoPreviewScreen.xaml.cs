using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Smartphone photo preview screen for reviewing uploaded photos
    /// Allows user to accept, retake, or go back before printing
    /// </summary>
    public partial class SmartphonePhotoPreviewScreen : UserControl, IDisposable
    {
        #region Events

        /// <summary>
        /// Event fired when user accepts the photo and wants to proceed to printing
        /// </summary>
        public event EventHandler<PhotoAcceptedEventArgs>? PhotoAccepted;

        /// <summary>
        /// Event fired when user wants to upload a different photo
        /// </summary>
        public event EventHandler? RetakeRequested;

        /// <summary>
        /// Event fired when user wants to go back to product selection
        /// </summary>
        public event EventHandler? BackRequested;

        #endregion

        #region Private Fields

        private readonly string _photoPath;
        private readonly ProductInfo _productInfo;
        private readonly PhotoUploadedEventArgs _uploadInfo;
        private BitmapImage? _photoImage;
        private bool _photoProcessed = false;

        #endregion

        #region Constructor

        public SmartphonePhotoPreviewScreen(string photoPath, ProductInfo productInfo, PhotoUploadedEventArgs uploadInfo)
        {
            _photoPath = photoPath ?? throw new ArgumentNullException(nameof(photoPath));
            _productInfo = productInfo ?? throw new ArgumentNullException(nameof(productInfo));
            _uploadInfo = uploadInfo ?? throw new ArgumentNullException(nameof(uploadInfo));

            InitializeComponent();
            
            // Load and display the photo
            _ = LoadPhotoAsync();
        }

        #endregion

        #region Private Methods - Photo Loading

        private async Task LoadPhotoAsync()
        {
            try
            {
                ShowProcessingOverlay("Loading your photo...");

                // Load photo from file
                await Task.Run(() =>
                {
                    _photoImage = new BitmapImage();
                    _photoImage.BeginInit();
                    _photoImage.UriSource = new Uri(_photoPath, UriKind.Absolute);
                    _photoImage.CacheOption = BitmapCacheOption.OnLoad;
                    _photoImage.EndInit();
                    _photoImage.Freeze(); // Make it cross-thread accessible
                });

                // Update UI on main thread
                Dispatcher.Invoke(() =>
                {
                    PhotoPreviewImage.Source = _photoImage;
                    UpdatePhotoInfo();
                    UpdateUploadInfo();
                    UpdatePrintInfo();
                    PerformQualityAssessment();
                });

                HideProcessingOverlay();

                LoggingService.Application.Information("Smartphone photo loaded successfully",
                    ("PhotoPath", _photoPath),
                    ("Width", _photoImage.PixelWidth),
                    ("Height", _photoImage.PixelHeight));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load smartphone photo", ex);
                HideProcessingOverlay();
                ShowError("Failed to load photo. Please try uploading again.");
            }
        }

        private void UpdatePhotoInfo()
        {
            if (_photoImage == null) return;

            try
            {
                // Update dimensions
                PhotoDimensionsText.Text = $"{_photoImage.PixelWidth} × {_photoImage.PixelHeight}";

                // Update file size
                var fileInfo = new FileInfo(_photoPath);
                var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                PhotoFileSizeText.Text = $"{fileSizeMB:F1} MB";

                // Update format
                var extension = Path.GetExtension(_photoPath).ToUpperInvariant();
                PhotoFormatText.Text = extension.Replace(".", "");

                LoggingService.Application.Debug("Updated photo info display",
                    ("Dimensions", $"{_photoImage.PixelWidth}x{_photoImage.PixelHeight}"),
                    ("FileSize", fileSizeMB),
                    ("Format", extension));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update photo info", ex);
            }
        }

        private void UpdateUploadInfo()
        {
            try
            {
                // Update upload time
                var timeAgo = DateTime.Now - _uploadInfo.UploadedAt;
                if (timeAgo.TotalMinutes < 1)
                {
                    UploadTimeText.Text = "Just now";
                }
                else if (timeAgo.TotalMinutes < 60)
                {
                    UploadTimeText.Text = $"{(int)timeAgo.TotalMinutes} min ago";
                }
                else
                {
                    UploadTimeText.Text = _uploadInfo.UploadedAt.ToString("HH:mm");
                }

                // Update source device (simplified - would need user agent parsing for real device detection)
                var deviceName = DetermineDeviceType(_uploadInfo.MimeType, _uploadInfo.OriginalFileName);
                SourceDeviceText.Text = deviceName;

                LoggingService.Application.Debug("Updated upload info display",
                    ("UploadTime", _uploadInfo.UploadedAt),
                    ("DeviceName", deviceName),
                    ("ClientIP", _uploadInfo.ClientIPAddress));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update upload info", ex);
            }
        }

        private void UpdatePrintInfo()
        {
            try
            {
                // Update price
                PrintPriceText.Text = $"${_productInfo.Price:F2}";

                LoggingService.Application.Debug("Updated print info display",
                    ("Price", _productInfo.Price),
                    ("ProductType", _productInfo.Type));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update print info", ex);
            }
        }

        private void PerformQualityAssessment()
        {
            try
            {
                if (_photoImage == null) return;

                // Simple quality assessment based on image properties
                var quality = AssessPhotoQuality(_photoImage);
                PhotoQualityText.Text = quality;

                LoggingService.Application.Information("Performed photo quality assessment",
                    ("Quality", quality),
                    ("Width", _photoImage.PixelWidth),
                    ("Height", _photoImage.PixelHeight));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to assess photo quality", ex);
                PhotoQualityText.Text = "Good";
            }
        }

        #endregion

        #region Private Methods - Quality Assessment

        private string AssessPhotoQuality(BitmapImage image)
        {
            try
            {
                var width = image.PixelWidth;
                var height = image.PixelHeight;
                var totalPixels = width * height;

                // Basic quality assessment based on resolution
                if (totalPixels >= 8000000) // 8MP+
                {
                    return "Excellent";
                }
                else if (totalPixels >= 5000000) // 5MP+
                {
                    return "Very Good";
                }
                else if (totalPixels >= 3000000) // 3MP+
                {
                    return "Good";
                }
                else if (totalPixels >= 2000000) // 2MP+
                {
                    return "Fair";
                }
                else
                {
                    return "Low";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private string DetermineDeviceType(string mimeType, string fileName)
        {
            try
            {
                // Simple device type detection based on file patterns
                // In production, this would use HTTP user agent headers
                
                var lowerFileName = fileName.ToLowerInvariant();
                
                if (lowerFileName.Contains("img_") || lowerFileName.Contains("image_"))
                {
                    return "iPhone";
                }
                else if (lowerFileName.Contains("screenshot"))
                {
                    return "Mobile Device";
                }
                else if (mimeType.Contains("heic") || mimeType.Contains("heif"))
                {
                    return "iPhone";
                }
                else
                {
                    return "Smartphone";
                }
            }
            catch
            {
                return "Mobile Device";
            }
        }

        #endregion

        #region Private Methods - UI Management

        private void ShowProcessingOverlay(string message)
        {
            ProcessingText.Text = message;
            ProcessingOverlay.Visibility = Visibility.Visible;
        }

        private void HideProcessingOverlay()
        {
            ProcessingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            HideProcessingOverlay();
            MessageBox.Show(message, "Photo Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region Event Handlers

        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowProcessingOverlay("Preparing your photo for printing...");

                // Perform any final photo processing if needed
                await Task.Delay(1000); // Simulate processing time

                // Create photo accepted event args
                var acceptedArgs = new PhotoAcceptedEventArgs(
                    _photoPath,
                    _productInfo,
                    _uploadInfo);

                HideProcessingOverlay();

                LoggingService.Application.Information("User accepted smartphone photo",
                    ("PhotoPath", _photoPath),
                    ("ProductType", _productInfo.Type),
                    ("ProductPrice", _productInfo.Price));

                // Fire event to proceed to printing
                PhotoAccepted?.Invoke(this, acceptedArgs);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to accept photo", ex);
                HideProcessingOverlay();
                ShowError("Failed to process photo. Please try again.");
            }
        }

        private void RetakeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User requested to upload different photo");
                RetakeRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to handle retake request", ex);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User requested to go back from photo preview");
                BackRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to handle back request", ex);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // Clean up any resources if needed
            // Currently no unmanaged resources to dispose
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for when a smartphone photo is accepted for printing
    /// </summary>
    public class PhotoAcceptedEventArgs : EventArgs
    {
        public string PhotoPath { get; }
        public ProductInfo ProductInfo { get; }
        public PhotoUploadedEventArgs UploadInfo { get; }

        public PhotoAcceptedEventArgs(string photoPath, ProductInfo productInfo, PhotoUploadedEventArgs uploadInfo)
        {
            PhotoPath = photoPath;
            ProductInfo = productInfo;
            UploadInfo = uploadInfo;
        }
    }

    #endregion
}
