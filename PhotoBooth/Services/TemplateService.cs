using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;

namespace Photobooth.Services
{
    public class TemplateService
    {
        private readonly string _templatesFolder;
        private readonly string _thumbnailsFolder;

        public TemplateService()
        {
            _templatesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            _thumbnailsFolder = Path.Combine(_templatesFolder, "Thumbnails");
            
            // Ensure directories exist
            Directory.CreateDirectory(_templatesFolder);
            Directory.CreateDirectory(_thumbnailsFolder);
        }

        public async Task<(string filePath, string thumbnailPath, int width, int height, int fileSize)> ProcessTemplateFileAsync(string sourceFilePath, string templateName)
        {
            try
            {
                // Validate source file exists and is accessible
                if (!File.Exists(sourceFilePath))
                    throw new FileNotFoundException("Source file not found", sourceFilePath);
                
                // Generate unique filename
                var extension = Path.GetExtension(sourceFilePath);
                var fileName = $"{templateName}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(_templatesFolder, fileName);
                
                // Ensure the destination doesn't already exist
                if (File.Exists(filePath))
                    throw new InvalidOperationException($"Destination file already exists: {filePath}");
                
                // Copy original file
                File.Copy(sourceFilePath, filePath, false);
                
                // Get file info
                var fileInfo = new FileInfo(filePath);
                var fileSize = (int)fileInfo.Length;
                
                // Get image dimensions and create thumbnail
                var (width, height, thumbnailPath) = await CreateThumbnailAsync(filePath, fileName);
                
                return (filePath, thumbnailPath, width, height, fileSize);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to process template file: {ex.Message}", ex);
            }
        }

        private async Task<(int width, int height, string thumbnailPath)> CreateThumbnailAsync(string sourceFilePath, string fileName)
        {
            return await Task.Run(() =>
            {
                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.UriSource = new Uri(sourceFilePath);
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.EndInit();
                
                var originalWidth = originalImage.PixelWidth;
                var originalHeight = originalImage.PixelHeight;
                
                // Create thumbnail
                var thumbnailSize = 200; // 200x200 max
                var scale = Math.Min((double)thumbnailSize / originalWidth, (double)thumbnailSize / originalHeight);
                var thumbnailWidth = (int)(originalWidth * scale);
                var thumbnailHeight = (int)(originalHeight * scale);
                
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.UriSource = new Uri(sourceFilePath);
                thumbnail.DecodePixelWidth = thumbnailWidth;
                thumbnail.DecodePixelHeight = thumbnailHeight;
                thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                thumbnail.EndInit();
                
                var thumbnailFileName = Path.GetFileNameWithoutExtension(fileName) + "_thumb.jpg";
                var thumbnailPath = Path.Combine(_thumbnailsFolder, thumbnailFileName);
                
                // Save thumbnail using WPF encoder
                using var fileStream = new FileStream(thumbnailPath, FileMode.Create);
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                encoder.Save(fileStream);
                
                return (originalWidth, originalHeight, thumbnailPath);
            });
        }

        public BitmapImage LoadThumbnail(string thumbnailPath)
        {
            try
            {
                if (!File.Exists(thumbnailPath))
                {
                    return CreatePlaceholderImage();
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(thumbnailPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 200; // Optimize memory usage
                bitmap.EndInit();
                bitmap.Freeze(); // Make thread-safe
                
                return bitmap;
            }
            catch
            {
                return CreatePlaceholderImage();
            }
        }

        private BitmapImage CreatePlaceholderImage()
        {
            // Create a simple placeholder image
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/Resources/template-placeholder.png");
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public bool IsValidImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp";
        }

        public bool IsFileSizeValid(string filePath, int maxSizeMB = 10)
        {
            var fileInfo = new FileInfo(filePath);
            var maxSizeBytes = maxSizeMB * 1024 * 1024;
            return fileInfo.Length <= maxSizeBytes;
        }

        public string FormatFileSize(int? fileSizeBytes)
        {
            if (!fileSizeBytes.HasValue) return "Unknown";
            
            var bytes = fileSizeBytes.Value;
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }

        public void DeleteTemplateFiles(string filePath, string? thumbnailPath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    File.Delete(thumbnailPath);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - file cleanup is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to delete template files: {ex.Message}");
            }
        }
    }
} 
