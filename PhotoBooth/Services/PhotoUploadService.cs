using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace Photobooth.Services
{
    /// <summary>
    /// Photo upload web service using ASP.NET Core minimal API
    /// Provides simple HTTP server for smartphone photo uploads
    /// </summary>
    public class PhotoUploadService : IPhotoUploadService
    {
        #region Events

        public event EventHandler<PhotoUploadedEventArgs>? PhotoUploaded;
        public event EventHandler<UploadServiceStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<UploadErrorEventArgs>? UploadError;

        #endregion

        #region Private Fields

        private IHost? _host;
        private UploadServiceStatus _status = UploadServiceStatus.Stopped;
        private int? _currentPort;
        private string? _hostIpAddress;
        private string? _baseUrl;
        private string? _uploadUrl;
        private string _sessionId = string.Empty;
        private DateTime _sessionStarted;
        private readonly List<string> _uploadedPhotos = new();
        private int _uploadErrors = 0;
        private long _totalBytesUploaded = 0;
        private bool _disposed = false;

        // Upload configuration
        private long _maxFileSize = 10485760; // 10MB default
        private string[] _allowedMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/bmp", "image/webp" };
        private int _maxFiles = 1;

        // Upload directory
        private readonly string _uploadDirectory;

        #endregion

        #region Properties

        public bool IsRunning => _status == UploadServiceStatus.Running;
        public int? CurrentPort => _currentPort;
        public string? BaseUrl => _baseUrl;
        public string? UploadUrl => _uploadUrl;
        public int PhotosUploadedCount => _uploadedPhotos.Count;
        public string? SessionId => _sessionId;

        #endregion

        #region Constructor

        public PhotoUploadService()
        {
            // Create upload directory in the photobooth data folder
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoboothX");
            _uploadDirectory = Path.Combine(dataFolder, "SmartphoneUploads");
            
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
            }

            StartNewSession();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start the photo upload web service
        /// </summary>
        public async Task<bool> StartServiceAsync(int port = 8080, string? hostIpAddress = null)
        {
            try
            {
                if (_status == UploadServiceStatus.Running)
                {
                    return true; // Already running
                }

                LoggingService.Application.Information("Starting photo upload web service", ("Port", port));
                UpdateStatus(UploadServiceStatus.Starting);

                _currentPort = port;
                _hostIpAddress = hostIpAddress ?? "192.168.137.1"; // Default hotspot IP
                _baseUrl = $"http://{_hostIpAddress}:{port}";
                _uploadUrl = $"{_baseUrl}/upload";

                // Build ASP.NET Core host
                var builder = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseUrls(_baseUrl);
                        webBuilder.ConfigureServices(ConfigureServices);
                        webBuilder.Configure(ConfigureApp);
                        webBuilder.UseKestrel(options =>
                        {
                            options.Limits.MaxRequestBodySize = _maxFileSize;
                        });
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders(); // Suppress ASP.NET Core logging to reduce noise
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
                    });

                _host = builder.Build();
                await _host.StartAsync();

                UpdateStatus(UploadServiceStatus.Running);

                LoggingService.Application.Information("Photo upload service started successfully",
                    ("BaseURL", _baseUrl),
                    ("UploadURL", _uploadUrl));

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to start photo upload service", ex);
                UpdateStatus(UploadServiceStatus.Error, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Stop the photo upload web service
        /// </summary>
        public async Task<bool> StopServiceAsync()
        {
            try
            {
                if (_status == UploadServiceStatus.Stopped)
                {
                    return true; // Already stopped
                }

                LoggingService.Application.Information("Stopping photo upload web service");
                UpdateStatus(UploadServiceStatus.Stopping);

                if (_host != null)
                {
                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                    _host.Dispose();
                    _host = null;
                }

                _currentPort = null;
                _baseUrl = null;
                _uploadUrl = null;

                UpdateStatus(UploadServiceStatus.Stopped);

                LoggingService.Application.Information("Photo upload service stopped successfully");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to stop photo upload service", ex);
                UpdateStatus(UploadServiceStatus.Error, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Get list of uploaded photos
        /// </summary>
        public List<string> GetUploadedPhotos()
        {
            return new List<string>(_uploadedPhotos);
        }

        /// <summary>
        /// Start new upload session
        /// </summary>
        public void StartNewSession()
        {
            _sessionId = Guid.NewGuid().ToString("N")[..8]; // Short session ID
            _sessionStarted = DateTime.Now;
            _uploadedPhotos.Clear();
            _uploadErrors = 0;
            _totalBytesUploaded = 0;

            LoggingService.Application.Information("Started new upload session", ("SessionId", _sessionId));
        }

        /// <summary>
        /// Get upload session statistics
        /// </summary>
        public UploadSessionStats GetSessionStats()
        {
            return new UploadSessionStats
            {
                SessionId = _sessionId,
                SessionStarted = _sessionStarted,
                PhotosUploaded = _uploadedPhotos.Count,
                TotalBytesUploaded = _totalBytesUploaded,
                UploadErrors = _uploadErrors
            };
        }

        /// <summary>
        /// Configure upload restrictions
        /// </summary>
        public void ConfigureUploadRestrictions(long maxFileSize = 10485760, string[]? allowedTypes = null, int maxFiles = 1)
        {
            _maxFileSize = maxFileSize;
            _allowedMimeTypes = allowedTypes ?? _allowedMimeTypes;
            _maxFiles = maxFiles;

            LoggingService.Application.Information("Configured upload restrictions",
                ("MaxFileSize", maxFileSize),
                ("AllowedTypes", string.Join(", ", _allowedMimeTypes)),
                ("MaxFiles", maxFiles));
        }

        #endregion

        #region Private Methods - ASP.NET Core Configuration

        private void ConfigureServices(IServiceCollection services)
        {
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = _maxFileSize;
            });
        }

        private void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            
            app.UseEndpoints(endpoints =>
            {
                // Serve upload page
                endpoints.MapGet("/", async context =>
                {
                    await HandleUploadPageRequest(context);
                });

                // Handle photo uploads
                endpoints.MapPost("/upload", async context =>
                {
                    await HandlePhotoUpload(context);
                });

                // Serve success page
                endpoints.MapGet("/success", async context =>
                {
                    await HandleSuccessPage(context);
                });

                // Handle static assets (CSS, JS if needed)
                endpoints.MapGet("/assets/{*path}", async context =>
                {
                    await HandleStaticAssets(context);
                });
            });
        }

        #endregion

        #region Private Methods - Request Handlers

        private async Task HandleUploadPageRequest(HttpContext context)
        {
            try
            {
                if (context.Request.Method != "GET")
                {
                    context.Response.StatusCode = 405; // Method not allowed
                    return;
                }

                var html = GenerateUploadPageHTML();
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error serving upload page", ex);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error");
            }
        }

        private async Task HandlePhotoUpload(HttpContext context)
        {
            try
            {
                if (context.Request.Method != "POST")
                {
                    context.Response.StatusCode = 405;
                    await context.Response.WriteAsync("Only POST method allowed");
                    return;
                }

                // Check if we've reached the file limit
                if (_uploadedPhotos.Count >= _maxFiles)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Upload limit reached");
                    return;
                }

                if (!context.Request.HasFormContentType)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Form content required");
                    return;
                }

                var form = await context.Request.ReadFormAsync();
                var file = form.Files.FirstOrDefault();

                if (file == null || file.Length == 0)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("No file uploaded");
                    return;
                }

                // Validate file
                var validationResult = ValidateUploadedFile(file);
                if (!validationResult.IsValid)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(validationResult.ErrorMessage);
                    return;
                }

                // Save file
                var savedPath = await SaveUploadedFile(file, context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                
                if (!string.IsNullOrEmpty(savedPath))
                {
                    // Redirect to success page
                    context.Response.Redirect("/success");
                }
                else
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Failed to save file");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error handling photo upload", ex);
                _uploadErrors++;
                
                var errorMsg = "Upload failed. Please try again.";
                UploadError?.Invoke(this, new UploadErrorEventArgs(errorMsg, context.Connection.RemoteIpAddress?.ToString()));
                
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(errorMsg);
            }
        }

        private async Task HandleSuccessPage(HttpContext context)
        {
            try
            {
                var html = GenerateSuccessPageHTML();
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error serving success page", ex);
                context.Response.StatusCode = 500;
            }
        }

        private async Task HandleStaticAssets(HttpContext context)
        {
            // Simple CSS serving for styling
            if (context.Request.Path.StartsWithSegments("/assets/style.css"))
            {
                var css = GenerateCSS();
                context.Response.ContentType = "text/css";
                await context.Response.WriteAsync(css);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }

        #endregion

        #region Private Methods - File Processing

        private (bool IsValid, string ErrorMessage) ValidateUploadedFile(IFormFile file)
        {
            // Check file size
            if (file.Length > _maxFileSize)
            {
                return (false, $"File too large. Maximum size: {_maxFileSize / 1024 / 1024}MB");
            }

            // Check MIME type
            if (!_allowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Invalid file type. Please upload an image.");
            }

            // Check file extension
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return (false, "Invalid file extension. Please upload an image.");
            }

            return (true, string.Empty);
        }

        private async Task<string?> SaveUploadedFile(IFormFile file, string clientIP)
        {
            try
            {
                // Generate unique filename
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var extension = Path.GetExtension(file.FileName);
                var filename = $"smartphone_{_sessionId}_{timestamp}{extension}";
                var filePath = Path.Combine(_uploadDirectory, filename);

                // Save file
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                _uploadedPhotos.Add(filePath);
                _totalBytesUploaded += file.Length;

                // Fire event
                PhotoUploaded?.Invoke(this, new PhotoUploadedEventArgs(
                    filePath, file.FileName, file.Length, file.ContentType, clientIP, _sessionId));

                LoggingService.Application.Information("Photo uploaded successfully",
                    ("FileName", filename),
                    ("FileSize", file.Length),
                    ("ClientIP", clientIP));

                return filePath;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to save uploaded file", ex);
                return null;
            }
        }

        #endregion

        #region Private Methods - HTML Generation

        private string GenerateUploadPageHTML()
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PhotoBooth Upload</title>
    <link rel=""stylesheet"" href=""/assets/style.css"">
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>📸 PhotoBooth Upload</h1>
            <p>Upload your photo to print</p>
        </div>
        
        <form action=""/upload"" method=""post"" enctype=""multipart/form-data"" class=""upload-form"">
            <div class=""file-input-container"">
                <input type=""file"" id=""photo"" name=""photo"" accept=""image/*"" capture=""environment"" required>
                <label for=""photo"" class=""file-input-label"">
                    📷 Choose Photo
                </label>
            </div>
            
            <button type=""submit"" class=""upload-button"">
                ⬆️ Upload & Print
            </button>
        </form>
        
        <div class=""info"">
            <p>• Maximum file size: {_maxFileSize / 1024 / 1024}MB</p>
            <p>• Supported formats: JPG, PNG, GIF</p>
            <p>• Session: {_sessionId}</p>
        </div>
    </div>
    
    <script>
        // Auto-submit when file is selected (mobile-friendly)
        document.getElementById('photo').addEventListener('change', function() {{
            if (this.files.length > 0) {{
                // Show file name
                document.querySelector('.file-input-label').textContent = 'Selected: ' + this.files[0].name;
            }}
        }});
        
        // Show upload progress
        document.querySelector('.upload-form').addEventListener('submit', function() {{
            document.querySelector('.upload-button').textContent = '⏳ Uploading...';
            document.querySelector('.upload-button').disabled = true;
        }});
    </script>
</body>
</html>";
        }

        private string GenerateSuccessPageHTML()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Upload Success - PhotoBooth</title>
    <link rel=""stylesheet"" href=""/assets/style.css"">
</head>
<body>
    <div class=""container"">
        <div class=""success-message"">
            <h1>✅ Upload Successful!</h1>
            <p>Your photo has been uploaded and will be printed shortly.</p>
            <div class=""success-animation"">🎉</div>
        </div>
        
        <div class=""next-steps"">
            <h2>What happens next?</h2>
            <ul>
                <li>Your photo is being processed</li>
                <li>You can choose extra copies at the machine</li>
                <li>Your prints will be ready in moments</li>
            </ul>
        </div>
        
        <div class=""info"">
            <p>Return to the PhotoBooth machine to complete your order.</p>
        </div>
    </div>
    
    <script>
        // Auto-refresh to show updated status (optional)
        setTimeout(function() {
            document.querySelector('.success-animation').textContent = '🎊';
        }, 1000);
    </script>
</body>
</html>";
        }

        private string GenerateCSS()
        {
            return @"
/* PhotoBooth Upload Page Styles */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 20px;
}

.container {
    background: white;
    border-radius: 20px;
    padding: 40px;
    box-shadow: 0 20px 40px rgba(0,0,0,0.1);
    max-width: 500px;
    width: 100%;
    text-align: center;
}

.header h1 {
    color: #333;
    margin-bottom: 10px;
    font-size: 2.5em;
}

.header p {
    color: #666;
    font-size: 1.2em;
    margin-bottom: 30px;
}

.upload-form {
    margin: 30px 0;
}

.file-input-container {
    position: relative;
    margin-bottom: 20px;
}

.file-input-container input[type='file'] {
    position: absolute;
    opacity: 0;
    width: 100%;
    height: 100%;
    cursor: pointer;
}

.file-input-label {
    display: block;
    background: #f8f9fa;
    border: 3px dashed #dee2e6;
    border-radius: 15px;
    padding: 40px 20px;
    cursor: pointer;
    transition: all 0.3s ease;
    font-size: 1.3em;
    color: #666;
}

.file-input-label:hover {
    background: #e9ecef;
    border-color: #667eea;
    color: #667eea;
}

.upload-button {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    border: none;
    border-radius: 50px;
    padding: 15px 40px;
    font-size: 1.3em;
    cursor: pointer;
    transition: transform 0.2s ease;
    width: 100%;
    margin-top: 20px;
}

.upload-button:hover {
    transform: translateY(-2px);
}

.upload-button:disabled {
    opacity: 0.7;
    cursor: not-allowed;
}

.info {
    background: #f8f9fa;
    border-radius: 10px;
    padding: 20px;
    margin-top: 30px;
}

.info p {
    color: #666;
    font-size: 0.9em;
    margin: 5px 0;
}

.success-message h1 {
    color: #28a745;
    margin-bottom: 20px;
}

.success-animation {
    font-size: 4em;
    margin: 20px 0;
}

.next-steps {
    text-align: left;
    margin: 30px 0;
}

.next-steps h2 {
    color: #333;
    margin-bottom: 15px;
}

.next-steps ul {
    padding-left: 20px;
}

.next-steps li {
    color: #666;
    margin: 8px 0;
    font-size: 1.1em;
}

/* Mobile optimizations */
@media (max-width: 600px) {
    .container {
        padding: 20px;
        margin: 10px;
    }
    
    .header h1 {
        font-size: 2em;
    }
    
    .file-input-label {
        padding: 30px 15px;
        font-size: 1.1em;
    }
}
";
        }

        #endregion

        #region Private Methods - Status Management

        private void UpdateStatus(UploadServiceStatus newStatus, string? errorMessage = null)
        {
            var oldStatus = _status;
            _status = newStatus;

            StatusChanged?.Invoke(this, new UploadServiceStatusChangedEventArgs(oldStatus, newStatus, errorMessage));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                StopServiceAsync().Wait(5000);
                _host?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
