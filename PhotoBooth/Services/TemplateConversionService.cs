using System;
using System.IO;
using System.Linq;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Configuration;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for template conversion and validation operations
    /// </summary>
    public interface ITemplateConversionService
    {
        TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate);
        (double width, double height) GetStandardDisplaySize(int actualWidth, int actualHeight);
        string GetAspectRatioText(double aspectRatio);
        string GetTemplateSizeCategory(double aspectRatio);
        bool IsTemplateValidForProduct(TemplateInfo template, ProductInfo? product);
    }

    /// <summary>
    /// Service for converting database templates to UI templates and related operations
    /// Extracted from MainWindow and TemplateSelectionScreen to follow DRY principle
    /// </summary>
    public class TemplateConversionService : ITemplateConversionService
    {
        // Note: Display size constants moved to PhotoboothConfiguration.TemplateDisplaySizes

        /// <summary>
        /// Converts a database Template object to a TemplateInfo object for UI display
        /// </summary>
        public TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate)
        {
            try
            {
                LoggingService.Application.Debug("Converting database template to template info",
                    ("TemplateId", (object)dbTemplate.Id),
                    ("TemplateName", dbTemplate.Name ?? "Unknown"),
                    ("TemplatePath", dbTemplate.TemplatePath ?? "Unknown"),
                    ("PreviewPath", dbTemplate.PreviewPath ?? "Unknown"),
                    ("HasLayout", dbTemplate.Layout != null),
                    ("LayoutId", (object?)dbTemplate.Layout?.Id ?? "None"),
                    ("LayoutPhotoCount", (object?)dbTemplate.Layout?.PhotoCount ?? 0),
                    ("LayoutPhotoAreasCount", dbTemplate.Layout?.PhotoAreas?.Count ?? 0),
                    ("DirectPhotoAreasCount", dbTemplate.PhotoAreas?.Count ?? 0));
                
                LoggingService.Application.Debug("Converting template: {TemplateName}",
                    ("TemplateName", dbTemplate.Name ?? "Unknown"),
                    ("PreviewPath", dbTemplate.PreviewPath ?? "Unknown"),
                    ("TemplatePath", dbTemplate.TemplatePath ?? "Unknown"),
                    ("Layout", dbTemplate.Layout?.Name ?? "NULL"));

                // Check if required files exist
                if (!File.Exists(dbTemplate.PreviewPath))
                {
                    LoggingService.Application.Warning("Preview image missing for template {TemplateName}",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("PreviewPath", dbTemplate.PreviewPath ?? "Unknown"));
                    return null;
                }

                if (!File.Exists(dbTemplate.TemplatePath))
                {
                    LoggingService.Application.Warning("Template image missing for template {TemplateName}",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("TemplatePath", dbTemplate.TemplatePath ?? "Unknown"));
                    return null;
                }

                // Get dimensions from layout
                var width = dbTemplate.Layout?.Width ?? 0;
                var height = dbTemplate.Layout?.Height ?? 0;
                var photoCount = dbTemplate.Layout?.PhotoCount ?? 1;

                if (width == 0 || height == 0)
                {
                    LoggingService.Application.Warning("Invalid template dimensions for template {TemplateName}",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("Width", width),
                        ("Height", height));
                    return null;
                }

                // Calculate display dimensions
                var aspectRatio = (double)width / height;
                var (displayWidth, displayHeight) = GetStandardDisplaySize(width, height);

                // Create TemplateConfig for compatibility
                var photoAreas = new List<PhotoArea>();
                
                // Try to get photo areas from Layout first, then from direct PhotoAreas property
                if (dbTemplate.Layout?.PhotoAreas != null && dbTemplate.Layout.PhotoAreas.Any())
                {
                    LoggingService.Application.Debug("Using layout photo areas for template conversion",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("PhotoAreasCount", dbTemplate.Layout.PhotoAreas.Count));
                    photoAreas = dbTemplate.Layout.PhotoAreas.Select(pa => new PhotoArea
                    {
                        Id = pa.PhotoIndex.ToString(),
                        X = pa.X,
                        Y = pa.Y,
                        Width = pa.Width,
                        Height = pa.Height
                    }).ToList();
                }
                else if (dbTemplate.PhotoAreas != null && dbTemplate.PhotoAreas.Any())
                {
                    LoggingService.Application.Debug("Using direct photo areas for template conversion",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("PhotoAreasCount", dbTemplate.PhotoAreas.Count));
                    photoAreas = dbTemplate.PhotoAreas.Select(pa => new PhotoArea
                    {
                        Id = pa.Id,
                        X = pa.X,
                        Y = pa.Y,
                        Width = pa.Width,
                        Height = pa.Height
                    }).ToList();
                }
                else
                {
                    LoggingService.Application.Warning("No photo areas found for template - using empty list",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("TemplateId", dbTemplate.Id));
                }
                
                // Log photo area details for debugging
                if (photoAreas.Any())
                {
                    LoggingService.Application.Debug("Template photo areas configured",
                        ("TemplateName", dbTemplate.Name ?? "Unknown"),
                        ("PhotoAreasCount", photoAreas.Count),
                        ("PhotoAreas", photoAreas.Select((pa, i) => new { 
                            Index = i + 1, 
                            X = pa.X, 
                            Y = pa.Y, 
                            Width = pa.Width, 
                            Height = pa.Height 
                        })));
                }
                
                var config = new TemplateConfig
                {
                    TemplateName = dbTemplate.Name ?? "Unknown Template",
                    TemplateId = dbTemplate.Id.ToString(),
                    Category = dbTemplate.CategoryName ?? "Unknown",
                    Description = dbTemplate.Description ?? "No description available",
                    PhotoCount = photoCount,
                    Dimensions = new TemplateDimensions
                    {
                        Width = width,
                        Height = height
                    },
                    PhotoAreas = photoAreas
                };

                var templateInfo = new TemplateInfo
                {
                    Config = config,
                    PreviewImagePath = dbTemplate.PreviewPath ?? string.Empty,
                    TemplateImagePath = dbTemplate.TemplatePath ?? string.Empty,
                    FolderPath = dbTemplate.FolderPath ?? string.Empty,
                    TemplateName = dbTemplate.Name ?? "Unknown Template",
                    Category = (dbTemplate.CategoryName ?? "Unknown").ToLowerInvariant(),
                    Description = dbTemplate.Description ?? "No description available",
                    IsSeasonalTemplate = dbTemplate.Category?.IsSeasonalCategory ?? false,
                    SeasonPriority = dbTemplate.Category?.SeasonalPriority ?? 0,

                    // Display properties
                    DisplayWidth = displayWidth,
                    DisplayHeight = displayHeight,
                    DimensionText = $"{width} × {height}",
                    AspectRatio = aspectRatio,
                    AspectRatioText = GetAspectRatioText(aspectRatio),
                    TemplateSize = GetTemplateSizeCategory(aspectRatio)
                };

                LoggingService.Application.Debug("Successfully converted template: {TemplateName}",
                    ("TemplateName", templateInfo.TemplateName ?? "Unknown"));
                return templateInfo;
            }
            catch (FileNotFoundException ex)
            {
                LoggingService.Application.Warning("Template files not found for {TemplateName}",
                    ("TemplateName", dbTemplate.Name ?? "Unknown"),
                    ("MissingFile", ex.FileName ?? "Unknown"));
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error converting template {TemplateName}", ex,
                    ("TemplateName", dbTemplate.Name ?? "Unknown"));
                return null;
            }
        }

        /// <summary>
        /// Gets standard display size based on aspect ratio - THREE SIZES ONLY
        /// </summary>
        public (double width, double height) GetStandardDisplaySize(int actualWidth, int actualHeight)
        {
            try
            {
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return (PhotoboothConfiguration.TemplateDisplaySizes.SquareWidth, 
                           PhotoboothConfiguration.TemplateDisplaySizes.SquareHeight);
                }

                double aspectRatio = (double)actualWidth / actualHeight;

                if (aspectRatio > PhotoboothConfiguration.AspectRatioThresholds.WideThreshold) // Wide format (4x6, landscape)
                {
                    return (PhotoboothConfiguration.TemplateDisplaySizes.WideWidth, 
                           PhotoboothConfiguration.TemplateDisplaySizes.WideHeight);
                }
                else if (aspectRatio < PhotoboothConfiguration.AspectRatioThresholds.TallThreshold) // Tall format (strips)
                {
                    return (PhotoboothConfiguration.TemplateDisplaySizes.TallWidth, 
                           PhotoboothConfiguration.TemplateDisplaySizes.TallHeight);
                }
                else // Square-ish format
                {
                    return (PhotoboothConfiguration.TemplateDisplaySizes.SquareWidth, 
                           PhotoboothConfiguration.TemplateDisplaySizes.SquareHeight);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Warning("Error calculating display size, using default square size", 
                    ("ActualWidth", actualWidth),
                    ("ActualHeight", actualHeight),
                    ("Error", ex.Message));
                return (PhotoboothConfiguration.TemplateDisplaySizes.SquareWidth, 
                       PhotoboothConfiguration.TemplateDisplaySizes.SquareHeight);
            }
        }

        /// <summary>
        /// Gets human-readable aspect ratio description
        /// </summary>
        public string GetAspectRatioText(double aspectRatio)
        {
            if (aspectRatio > PhotoboothConfiguration.AspectRatioThresholds.WideThreshold) return "Wide";
            if (aspectRatio < PhotoboothConfiguration.AspectRatioThresholds.TallThreshold) return "Tall";
            return "Square";
        }

        /// <summary>
        /// Gets template size category for CSS-like styling
        /// </summary>
        public string GetTemplateSizeCategory(double aspectRatio)
        {
            if (aspectRatio > PhotoboothConfiguration.AspectRatioThresholds.WideThreshold) return "wide";
            if (aspectRatio < PhotoboothConfiguration.AspectRatioThresholds.TallThreshold) return "tall";
            return "square";
        }

        /// <summary>
        /// Checks if template is valid for the selected product type
        /// Since database-level TemplateType filtering is the primary filter, this serves as a fallback validation only
        /// </summary>
        public bool IsTemplateValidForProduct(TemplateInfo template, ProductInfo? product)
        {
            LoggingService.Application.Debug("Validating template for product",
                ("TemplateName", template.TemplateName ?? "Unknown"),
                ("TemplateCategory", template.Category),
                ("ProductType", product?.Type ?? "NULL"));

            if (product == null) 
            {
                LoggingService.Application.Debug("No selected product - allowing all templates");
                return true;
            }

            // Since we're now filtering by TemplateType at the database level using GetTemplatesByTypeAsync(),
            // the database-level filtering by TemplateType is the authoritative and primary filter.
            // This method now serves as a fallback validation only and should be permissive.
            
            var productType = product.Type?.ToLowerInvariant();
            var aspectRatio = template.AspectRatio;
            
            LoggingService.Application.Debug("Template validation - allowing (database filtering is primary)",
                ("TemplateName", template.TemplateName ?? "Unknown"),
                ("AspectRatio", aspectRatio.ToString("F2")),
                ("ProductType", productType ?? "NULL"));
            
            // Database filtering by TemplateType handles the main filtering logic
            // Return true to avoid double-filtering that was causing templates to be incorrectly filtered out
            return true;
        }


    }
} 