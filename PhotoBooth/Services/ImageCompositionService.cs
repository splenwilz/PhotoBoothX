using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for compositing captured photos onto template backgrounds
    /// </summary>
    public interface IImageCompositionService
    {
        Task<CompositionResult> ComposePhotosAsync(Template template, List<string> capturedPhotosPaths);
        Task<BitmapImage?> CreatePreviewImageAsync(string imagePath);
    }

    /// <summary>
    /// Service for compositing captured photos onto template backgrounds
    /// </summary>
    public class ImageCompositionService : IImageCompositionService
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly string _outputDirectory;

        #endregion

        #region Constructor

        public ImageCompositionService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
            
            // Create output directory on desktop
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _outputDirectory = Path.Combine(desktopPath, "PhotoBoothX_Photos");
            
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compose captured photos onto template background
        /// </summary>
        public async Task<CompositionResult> ComposePhotosAsync(Template template, List<string> capturedPhotosPaths)
        {
            try
            {
                Console.WriteLine("=== STARTING PHOTO COMPOSITION ===");
                Console.WriteLine($"Template Name: {template.Name}");
                Console.WriteLine($"Template Path: {template.TemplatePath}");
                Console.WriteLine($"Template Layout is null: {template.Layout == null}");
                Console.WriteLine($"Captured Photos Count: {capturedPhotosPaths.Count}");
                
                if (template.Layout != null)
                {
                    Console.WriteLine($"Layout ID: {template.Layout.Id}");
                    Console.WriteLine($"Layout PhotoCount: {template.Layout.PhotoCount}");
                    Console.WriteLine($"Layout Dimensions: {template.Layout.Width}x{template.Layout.Height}");
                    Console.WriteLine($"Layout PhotoAreas count: {template.Layout.PhotoAreas?.Count ?? 0}");
                }

                LoggingService.Application.Information("Starting photo composition", 
                    ("TemplateName", template.Name),
                    ("PhotoCount", capturedPhotosPaths.Count));

                // Validate inputs
                if (template.Layout == null)
                {
                    Console.WriteLine("ERROR: Template layout is missing!");
                    return CompositionResult.Error("Template layout is missing");
                }

                if (capturedPhotosPaths.Count != template.Layout.PhotoCount)
                {
                    return CompositionResult.Error($"Photo count mismatch. Expected {template.Layout.PhotoCount}, got {capturedPhotosPaths.Count}");
                }

                if (!File.Exists(template.TemplatePath))
                {
                    return CompositionResult.Error($"Template file not found: {template.TemplatePath}");
                }

                // Load template background
                using var templateImage = Image.FromFile(template.TemplatePath);
                
                Console.WriteLine("=== TEMPLATE DIMENSIONS ===");
                Console.WriteLine($"Template Image - Width: {templateImage.Width}, Height: {templateImage.Height}");
                Console.WriteLine($"Template Layout - Width: {template.Layout.Width}, Height: {template.Layout.Height}");
                Console.WriteLine($"Template Path: {template.TemplatePath}");
                
                // Create final composition with transparent/black background
                using var composedImage = new Bitmap(templateImage.Width, templateImage.Height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(composedImage);
                
                // Set high quality rendering
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                // Clear to black background instead of white (or use Color.Transparent for transparent)
                graphics.Clear(Color.Black);
                
                // Draw template background (should preserve transparency)
                graphics.DrawImage(templateImage, 0, 0, templateImage.Width, templateImage.Height);

                // Get photo areas from template layout (prefer in-memory over database)
                List<TemplatePhotoArea> photoAreas;
                
                if (template.Layout?.PhotoAreas != null && template.Layout.PhotoAreas.Count > 0)
                {
                    // Use photo areas from the template's layout object
                    photoAreas = template.Layout.PhotoAreas.OrderBy(p => p.PhotoIndex).ToList();
                    Console.WriteLine($"Using {photoAreas.Count} photo areas from template layout");
                }
                else
                {
                    // Fallback: try to get photo areas from database
                    var photoAreasResult = await _databaseService.GetTemplatePhotoAreasAsync(template.LayoutId);
                    if (!photoAreasResult.Success || photoAreasResult.Data == null)
                    {
                        return CompositionResult.Error("Failed to get template photo areas from both layout and database");
                    }
                    photoAreas = photoAreasResult.Data.OrderBy(p => p.PhotoIndex).ToList();
                    Console.WriteLine($"Using {photoAreas.Count} photo areas from database");
                }

                if (photoAreas.Count != capturedPhotosPaths.Count)
                {
                    return CompositionResult.Error($"Photo area count mismatch. Expected {photoAreas.Count}, got {capturedPhotosPaths.Count}");
                }

                Console.WriteLine("=== ALL PHOTO AREAS ===");
                for (int i = 0; i < photoAreas.Count; i++)
                {
                    var area = photoAreas[i];
                    Console.WriteLine($"Photo Area {i + 1}: X={area.X}, Y={area.Y}, Width={area.Width}, Height={area.Height}, Rotation={area.Rotation}");
                    
                    // Validate and clamp photo area to template boundaries
                    // This prevents photos from extending beyond the template canvas
                    var maxWidth = templateImage.Width - area.X;
                    var maxHeight = templateImage.Height - area.Y;
                    
                    if (area.Width > maxWidth || area.Height > maxHeight)
                    {
                        Console.WriteLine($"⚠️  Photo Area {i + 1} exceeds template boundaries! Clamping dimensions.");
                        Console.WriteLine($"   Original: Width={area.Width}, Height={area.Height}");
                        Console.WriteLine($"   Max allowed: Width={maxWidth}, Height={maxHeight}");
                        
                        // Clamp width and height to fit within template
                        area.Width = Math.Min(area.Width, maxWidth);
                        area.Height = Math.Min(area.Height, maxHeight);
                        
                        Console.WriteLine($"   Clamped to: Width={area.Width}, Height={area.Height}");
                    }
                }
                Console.WriteLine($"Total Photo Areas: {photoAreas.Count}");

                // Process each captured photo
                Console.WriteLine("=== PHOTO AREA vs CAPTURED IMAGE ANALYSIS ===");
                for (int i = 0; i < capturedPhotosPaths.Count; i++)
                {
                    var photoPath = capturedPhotosPaths[i];
                    var photoArea = photoAreas[i];

                    Console.WriteLine($"\n--- Photo {i + 1} Analysis ---");
                    Console.WriteLine($"Photo Area - X: {photoArea.X}, Y: {photoArea.Y}, Width: {photoArea.Width}, Height: {photoArea.Height}");
                    
                    if (!File.Exists(photoPath))
                    {
                        LoggingService.Application.Warning("Captured photo not found", ("PhotoPath", photoPath));
                        continue;
                    }

                    // Get actual captured image dimensions
                    using var capturedImage = Image.FromFile(photoPath);
                    Console.WriteLine($"Captured Image - Width: {capturedImage.Width}, Height: {capturedImage.Height}");
                    Console.WriteLine($"Captured Image Aspect Ratio: {(double)capturedImage.Width / capturedImage.Height:F3}");
                    Console.WriteLine($"Photo Area Aspect Ratio: {(double)photoArea.Width / photoArea.Height:F3}");
                    
                    // Calculate what the image will actually become after processing
                    var targetRect = new Rectangle(photoArea.X, photoArea.Y, photoArea.Width, photoArea.Height);
                    var sourceAspect = (double)capturedImage.Width / capturedImage.Height;
                    var targetAspect = (double)targetRect.Width / targetRect.Height;
                    
                    Console.WriteLine($"Target Rectangle - X: {targetRect.X}, Y: {targetRect.Y}, Width: {targetRect.Width}, Height: {targetRect.Height}");
                    
                    // Show what will be cropped
                    Rectangle cropRect;
                    if (sourceAspect > targetAspect)
                    {
                        // Source is wider - crop width
                        var cropWidth = (int)(capturedImage.Height * targetAspect);
                        var cropX = (capturedImage.Width - cropWidth) / 2;
                        cropRect = new Rectangle(cropX, 0, cropWidth, capturedImage.Height);
                        Console.WriteLine($"CROPPING WIDTH: Source too wide, cropping {capturedImage.Width - cropWidth}px from width");
                    }
                    else
                    {
                        // Source is taller - crop height
                        var cropHeight = (int)(capturedImage.Width / targetAspect);
                        var cropY = (capturedImage.Height - cropHeight) / 2;
                        cropRect = new Rectangle(0, cropY, capturedImage.Width, cropHeight);
                        Console.WriteLine($"CROPPING HEIGHT: Source too tall, cropping {capturedImage.Height - cropHeight}px from height");
                    }
                    
                    Console.WriteLine($"Crop Rectangle - X: {cropRect.X}, Y: {cropRect.Y}, Width: {cropRect.Width}, Height: {cropRect.Height}");
                    Console.WriteLine($"Final scaling: {cropRect.Width}x{cropRect.Height} -> {targetRect.Width}x{targetRect.Height}");
                    
                    var scaleX = (double)targetRect.Width / cropRect.Width;
                    var scaleY = (double)targetRect.Height / cropRect.Height;
                    Console.WriteLine($"Scale factors - X: {scaleX:F3}, Y: {scaleY:F3}");

                    await ProcessAndPlacePhotoAsync(graphics, photoPath, photoArea);
                }
                Console.WriteLine("=== END PHOTO ANALYSIS ===\n");

                // Apply template overlay for heart-shaped and other special templates
                await ApplyTemplateOverlayAsync(graphics, template, photoAreas);

                // Save composed image
                var outputFileName = $"composed_{template.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var outputPath = Path.Combine(_outputDirectory, outputFileName);

                await Task.Run(() =>
                {
                    composedImage.Save(outputPath, ImageFormat.Jpeg);
                });

                LoggingService.Application.Information("Photo composition completed successfully", 
                    ("OutputPath", outputPath),
                    ("FileSize", new FileInfo(outputPath).Length));

                // Create preview image for UI
                var previewImage = await CreatePreviewImageAsync(outputPath);

                return CompositionResult.CreateSuccess(outputPath, previewImage);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo composition failed", ex);
                return CompositionResult.Error($"Composition failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a preview image suitable for UI display
        /// </summary>
        public async Task<BitmapImage?> CreatePreviewImageAsync(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return null;

                return await Task.Run(() =>
                {
                    using var originalImage = Image.FromFile(imagePath);
                    
                    // Create high-quality preview (max 800px width/height for crisp display)
                    var maxSize = 800; // Doubled from 400px for much better quality
                    var scale = Math.Min((double)maxSize / originalImage.Width, (double)maxSize / originalImage.Height);
                    var thumbnailWidth = (int)(originalImage.Width * scale);
                    var thumbnailHeight = (int)(originalImage.Height * scale);

                    using var thumbnail = new Bitmap(thumbnailWidth, thumbnailHeight, PixelFormat.Format32bppArgb);
                    using var graphics = Graphics.FromImage(thumbnail);
                    
                    // Enhanced quality settings for crisp preview
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    
                    graphics.DrawImage(originalImage, 0, 0, thumbnailWidth, thumbnailHeight);

                    // Convert to BitmapImage
                    using var memory = new MemoryStream();
                    thumbnail.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create preview image", ex, ("ImagePath", imagePath));
                return null;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Process and place a single photo into the composition
        /// </summary>
        private async Task ProcessAndPlacePhotoAsync(Graphics graphics, string photoPath, TemplatePhotoArea photoArea)
        {
            try
            {
                Console.WriteLine($"\n=== PROCESSING PHOTO: {Path.GetFileName(photoPath)} ===");
                
                using var capturedPhoto = Image.FromFile(photoPath);
                Console.WriteLine($"Original captured photo: {capturedPhoto.Width}x{capturedPhoto.Height}");
                
                // Calculate the area to place the photo
                var targetRect = new Rectangle(photoArea.X, photoArea.Y, photoArea.Width, photoArea.Height);
                Console.WriteLine($"Target rectangle (where photo should be placed): X={targetRect.X}, Y={targetRect.Y}, W={targetRect.Width}, H={targetRect.Height}");
                
                // Create processed photo that fits the target area
                var photoShapeType = photoArea.ShapeType != ShapeType.Rectangle ? photoArea.ShapeType : photoArea.GetShapeTypeFromRotation();
                using var processedPhoto = await Task.Run(() => ProcessCapturedPhoto(capturedPhoto, targetRect, photoArea.Rotation, photoArea.BorderRadius, photoShapeType));
                Console.WriteLine($"Processed photo dimensions: {processedPhoto.Width}x{processedPhoto.Height}");
                
                // Verify the processed photo matches target dimensions
                if (processedPhoto.Width != targetRect.Width || processedPhoto.Height != targetRect.Height)
                {
                    Console.WriteLine($"⚠️  SIZE MISMATCH! Processed photo ({processedPhoto.Width}x{processedPhoto.Height}) != Target ({targetRect.Width}x{targetRect.Height})");
                }
                else
                {
                    Console.WriteLine($"✅ Size match: Processed photo matches target dimensions");
                }
                
                // Draw the processed photo onto the composition
                // Skip additional rotation for special shapes since masking is already applied
                // For legacy support, also check rotation values
                var shapeType = photoArea.ShapeType != ShapeType.Rectangle ? photoArea.ShapeType : photoArea.GetShapeTypeFromRotation();
                bool isSpecialShape = shapeType == ShapeType.Circle || shapeType == ShapeType.Heart || shapeType == ShapeType.Petal;
                
                if (photoArea.Rotation != 0 && !isSpecialShape)
                {
                    Console.WriteLine($"Applying rotation: {photoArea.Rotation}°");
                    
                    // Handle rotation
                    var originalTransform = graphics.Transform;
                    var centerX = photoArea.X + photoArea.Width / 2f;
                    var centerY = photoArea.Y + photoArea.Height / 2f;
                    
                    Console.WriteLine($"Rotation center: X={centerX}, Y={centerY}");
                    
                    graphics.TranslateTransform(centerX, centerY);
                    graphics.RotateTransform((float)photoArea.Rotation);
                    graphics.TranslateTransform(-centerX, -centerY);
                    
                    graphics.DrawImage(processedPhoto, targetRect);
                    Console.WriteLine($"Photo drawn with rotation at: X={targetRect.X}, Y={targetRect.Y}, W={targetRect.Width}, H={targetRect.Height}");
                    
                    graphics.Transform = originalTransform;
                }
                else
                {
                    switch (shapeType)
                    {
                        case ShapeType.Circle:
                            Console.WriteLine($"Drawing circular photo (no additional rotation needed)");
                            break;
                        case ShapeType.Heart:
                            Console.WriteLine($"Drawing heart-shaped photo (no additional rotation needed)");
                            break;
                        case ShapeType.Petal:
                            Console.WriteLine($"Drawing petal-shaped photo (no additional rotation needed)");
                            break;
                        case ShapeType.RoundedRectangle:
                            Console.WriteLine($"Drawing rounded rectangle photo (no additional rotation needed)");
                            break;
                        default:
                            Console.WriteLine($"Drawing photo without rotation");
                            break;
                    }
                    graphics.DrawImage(processedPhoto, targetRect);
                    Console.WriteLine($"Photo drawn at: X={targetRect.X}, Y={targetRect.Y}, W={targetRect.Width}, H={targetRect.Height}");
                }

                // Verify what was actually drawn
                Console.WriteLine($"Final placement verification:");
                Console.WriteLine($"  - Should occupy: X={targetRect.X} to {targetRect.X + targetRect.Width}, Y={targetRect.Y} to {targetRect.Y + targetRect.Height}");
                Console.WriteLine($"  - Processed image: {processedPhoto.Width}x{processedPhoto.Height}");
                Console.WriteLine($"  - Target area: {targetRect.Width}x{targetRect.Height}");

                LoggingService.Application.Debug("Photo placed successfully", 
                    ("PhotoPath", photoPath),
                    ("TargetRect", $"{targetRect.X},{targetRect.Y},{targetRect.Width},{targetRect.Height}"),
                    ("ProcessedSize", $"{processedPhoto.Width}x{processedPhoto.Height}"),
                    ("Rotation", photoArea.Rotation));
                    
                Console.WriteLine($"=== END PROCESSING: {Path.GetFileName(photoPath)} ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR processing photo {Path.GetFileName(photoPath)}: {ex.Message}");
                LoggingService.Application.Error("Failed to process and place photo", ex, ("PhotoPath", photoPath));
            }
        }

        /// <summary>
        /// Apply template overlay for special templates (like heart shapes) by layering template.png on top
        /// </summary>
        private async Task ApplyTemplateOverlayAsync(Graphics graphics, Template template, List<TemplatePhotoArea> photoAreas)
        {
            try
            {
                Console.WriteLine($"\n=== APPLYING TEMPLATE OVERLAY ===");
                Console.WriteLine($"Template: {template.Name}");
                Console.WriteLine($"Layout Key: {template.Layout?.LayoutKey}");
                Console.WriteLine($"Template Folder Path: {template.FolderPath}");
                
                // Prefer resolved (possibly DB-fetched) photo areas; fallback to template.Layout-backed areas
                var areas = photoAreas ?? template.Layout?.PhotoAreas ?? new List<TemplatePhotoArea>();
                Console.WriteLine($"Using photo areas source: {(photoAreas?.Count > 0 ? "resolved photoAreas" : template.Layout?.PhotoAreas?.Count > 0 ? "template.Layout fallback" : "empty fallback")}");
                
                // Debug photo areas
                if (areas != null && areas.Count > 0)
                {
                    Console.WriteLine($"Found {areas.Count} photo areas:");
                    foreach (var pa in areas)
                    {
                        Console.WriteLine($"  Photo Area {pa.Id}: Rotation={pa.Rotation}°, Position=({pa.X},{pa.Y}), Size={pa.Width}x{pa.Height}, Shape={pa.ShapeType}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️  No photo areas found in any source!");
                }
                
                // Check if this template uses special shapes (ShapeType indicates shape types)
                bool hasHeartShapes = areas?.Any(pa => pa.ShapeType == ShapeType.Heart) == true;
                bool hasPetalShapes = areas?.Any(pa => pa.ShapeType == ShapeType.Petal) == true;
                bool hasSpecialShapes = hasHeartShapes || hasPetalShapes;
                Console.WriteLine($"Has heart shapes: {hasHeartShapes}");
                Console.WriteLine($"Has petal shapes: {hasPetalShapes}");
                Console.WriteLine($"Has special shapes: {hasSpecialShapes}");
                
                if (!hasSpecialShapes)
                {
                    Console.WriteLine($"Template does not use special shapes, skipping overlay");
                    return;
                }
                
                if (hasHeartShapes)
                {
                    Console.WriteLine($"Template uses heart shapes, applying template overlay");
                }
                if (hasPetalShapes)
                {
                    Console.WriteLine($"Template uses petal shapes, applying template overlay");
                }
                
                // Find the template.png file in the template's folder
                var templateImagePath = Path.Combine(template.FolderPath, "template.png");
                Console.WriteLine($"Looking for template overlay at: {templateImagePath}");
                
                if (!File.Exists(templateImagePath))
                {
                    Console.WriteLine($"⚠️  Template overlay file not found: {templateImagePath}");
                    return;
                }
                
                // Load and apply the template overlay
                using var templateOverlay = await Task.Run(() => Image.FromFile(templateImagePath));
                Console.WriteLine($"Template overlay loaded: {templateOverlay.Width}x{templateOverlay.Height}");
                
                // Use the actual composition canvas dimensions to avoid distortion
                var canvasBounds = graphics.VisibleClipBounds;
                var canvasWidth = (int)Math.Round(canvasBounds.Width);
                var canvasHeight = (int)Math.Round(canvasBounds.Height);
                
                Console.WriteLine($"Canvas dimensions (from graphics): {canvasWidth}x{canvasHeight}");
                Console.WriteLine($"Drawing template overlay from ({templateOverlay.Width}x{templateOverlay.Height}) to canvas ({canvasWidth}x{canvasHeight})");
                graphics.DrawImage(templateOverlay, 0, 0, canvasWidth, canvasHeight);
                
                Console.WriteLine($"✅ Template overlay applied successfully");
                Console.WriteLine($"=== END TEMPLATE OVERLAY ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error applying template overlay: {ex.Message}");
                LoggingService.Application.Error("Failed to apply template overlay", ex, 
                    ("TemplateName", template.Name),
                    ("TemplateFolder", template.FolderPath));
            }
        }



        /// <summary>
        /// Process captured photo to fit target area with proper cropping and scaling
        /// </summary>
        private Bitmap ProcessCapturedPhoto(Image capturedPhoto, Rectangle targetRect, double rotation, int borderRadius = 0, ShapeType shapeType = ShapeType.Rectangle)
        {
            Console.WriteLine($"    🔄 ProcessCapturedPhoto Details:");
            Console.WriteLine($"    📥 Input: {capturedPhoto.Width}x{capturedPhoto.Height}");
            Console.WriteLine($"    🎯 Target: {targetRect.Width}x{targetRect.Height}");
            Console.WriteLine($"    🔄 Rotation: {rotation}°");
            
            // Calculate aspect ratios
            var sourceAspect = (double)capturedPhoto.Width / capturedPhoto.Height;
            var targetAspect = (double)targetRect.Width / targetRect.Height;
            
            Console.WriteLine($"    📐 Source aspect: {sourceAspect:F3}, Target aspect: {targetAspect:F3}");

            // Determine crop rectangle to maintain aspect ratio
            Rectangle cropRect;
            if (sourceAspect > targetAspect)
            {
                // Source is wider - crop width
                var cropWidth = (int)(capturedPhoto.Height * targetAspect);
                var cropX = (capturedPhoto.Width - cropWidth) / 2;
                cropRect = new Rectangle(cropX, 0, cropWidth, capturedPhoto.Height);
                Console.WriteLine($"    ✂️  Cropping WIDTH: {capturedPhoto.Width - cropWidth}px removed");
            }
            else
            {
                // Source is taller - crop height
                var cropHeight = (int)(capturedPhoto.Width / targetAspect);
                var cropY = (capturedPhoto.Height - cropHeight) / 2;
                cropRect = new Rectangle(0, cropY, capturedPhoto.Width, cropHeight);
                Console.WriteLine($"    ✂️  Cropping HEIGHT: {capturedPhoto.Height - cropHeight}px removed");
            }
            
            Console.WriteLine($"    📦 Crop area: X={cropRect.X}, Y={cropRect.Y}, W={cropRect.Width}, H={cropRect.Height}");

            // Create processed image - EXACT target dimensions
            var processedPhoto = new Bitmap(targetRect.Width, targetRect.Height, PixelFormat.Format32bppArgb);
            Console.WriteLine($"    🖼️  Creating bitmap: {processedPhoto.Width}x{processedPhoto.Height}");
            
            using var graphics = Graphics.FromImage(processedPhoto);
            
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            // Clear to transparent background
            graphics.Clear(Color.Transparent);

            // Use the provided ShapeType parameter (with fallback to rotation for legacy compatibility)
            if (shapeType == ShapeType.Rectangle && (Math.Abs(rotation - 360) < 0.1 || Math.Abs(rotation - 720) < 0.1 || Math.Abs(rotation - 1080) < 0.1 || borderRadius > 0))
            {
                // Legacy fallback: derive from rotation if ShapeType is default Rectangle
                if (Math.Abs(rotation - 360) < 0.1)
                    shapeType = ShapeType.Circle;
                else if (Math.Abs(rotation - 720) < 0.1)
                    shapeType = ShapeType.Heart;
                else if (Math.Abs(rotation - 1080) < 0.1)
                    shapeType = ShapeType.Petal;
                else if (borderRadius > 0)
                    shapeType = ShapeType.RoundedRectangle;
            }
            
            bool isCircular = shapeType == ShapeType.Circle;
            bool isHeart = shapeType == ShapeType.Heart;
            bool isPetal = shapeType == ShapeType.Petal;
            bool isRoundedRectangle = shapeType == ShapeType.RoundedRectangle;
            
            Console.WriteLine($"    🎨 Shape Type: {shapeType}");
            
            if (isHeart)
            {
                Console.WriteLine($"    💖 Heart-shaped photo detected - will use template overlay (no clipping needed)");
                // Don't apply any clipping for heart shapes - the template overlay will handle the masking
            }
            else if (isPetal)
            {
                Console.WriteLine($"    🌸 Petal-shaped photo detected - will use template overlay (no clipping needed)");
                // Don't apply any clipping for petal shapes - the template overlay will handle the masking
            }
            else if (isCircular)
            {
                Console.WriteLine($"    🔵 Creating circular mask for fully rounded photo");
                
                // Create a circular clipping region
                var centerX = targetRect.Width / 2f;
                var centerY = targetRect.Height / 2f;
                var radius = Math.Min(targetRect.Width, targetRect.Height) / 2f;
                
                using var path = new GraphicsPath();
                path.AddEllipse(centerX - radius, centerY - radius, radius * 2, radius * 2);
                
                // Set the clipping region to the circle
                graphics.SetClip(path);
                
                Console.WriteLine($"    🔵 Circular mask: center=({centerX}, {centerY}), radius={radius}");
            }
            else if (isRoundedRectangle && borderRadius > 0)
            {
                // Create a rounded rectangle clipping region with clamped radius
                using var path = new GraphicsPath();
                var rect = new RectangleF(0, 0, targetRect.Width, targetRect.Height);
                var maxRadius = (int)Math.Floor(Math.Min(rect.Width, rect.Height) / 2f);
                var r = Math.Max(0, Math.Min(borderRadius, maxRadius));
                
                Console.WriteLine($"    🔲 Creating rounded corner mask with radius: {borderRadius} (clamped to {r})");
                var d = r * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90); // Top-left
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90); // Top-right
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); // Bottom-right
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); // Bottom-left
                path.CloseFigure();
                
                // Set the clipping region to the rounded rectangle
                graphics.SetClip(path);
                
                Console.WriteLine($"    🔲 Rounded corner mask applied with radius: {borderRadius}");
            }

            // Draw cropped and scaled image - destination is ENTIRE bitmap (0,0 to full size)
            var destinationRect = new Rectangle(0, 0, targetRect.Width, targetRect.Height);
            Console.WriteLine($"    🎨 Drawing to: X={destinationRect.X}, Y={destinationRect.Y}, W={destinationRect.Width}, H={destinationRect.Height}");
            Console.WriteLine($"    📤 From crop: X={cropRect.X}, Y={cropRect.Y}, W={cropRect.Width}, H={cropRect.Height}");
            
            graphics.DrawImage(capturedPhoto, 
                destinationRect,  // Where to draw (full bitmap)
                cropRect,         // What part of source to use
                GraphicsUnit.Pixel);

            // Reset clipping region if any mask was applied
            if (isCircular || isRoundedRectangle)
            {
                graphics.ResetClip();
                if (isCircular)
                {
                    Console.WriteLine($"    🔵 Circular mask applied and reset");
                }
                else if (isRoundedRectangle)
                {
                    Console.WriteLine($"    🔲 Rounded corner mask applied and reset");
                }
            }
            else if (isHeart)
            {
                Console.WriteLine($"    💖 Heart shape processed (no clipping mask used)");
            }
            else if (isPetal)
            {
                Console.WriteLine($"    🌸 Petal shape processed (no clipping mask used)");
            }
            else
            {
                Console.WriteLine($"    🟦 Rectangle shape processed (no clipping mask used)");
            }

            Console.WriteLine($"    ✅ Processed photo created: {processedPhoto.Width}x{processedPhoto.Height}");
            return processedPhoto;
        }

        #endregion
    }

    /// <summary>
    /// Result of image composition operation
    /// </summary>
    public class CompositionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? OutputPath { get; set; }
        public BitmapImage? PreviewImage { get; set; }

        public static CompositionResult CreateSuccess(string outputPath, BitmapImage? previewImage = null)
        {
            return new CompositionResult
            {
                Success = true,
                OutputPath = outputPath,
                PreviewImage = previewImage
            };
        }

        public static CompositionResult Error(string message)
        {
            return new CompositionResult
            {
                Success = false,
                Message = message
            };
        }
    }
} 