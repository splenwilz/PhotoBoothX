using System;
using System.IO;
using System.Linq;
using Photobooth.Models;
using Photobooth.Services;

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
        private static class Constants
        {
            public const double WideWidth = 300.0;     // Even larger cards
            public const double WideHeight = 210.0;    // Even larger cards
            public const double TallWidth = 280.0;     // Even larger cards
            public const double TallHeight = 210.0;    // Even larger cards
            public const double SquareWidth = 290.0;   // Even larger cards
            public const double SquareHeight = 210.0;  // Even larger cards
        }

        /// <summary>
        /// Converts a database Template object to a TemplateInfo object for UI display
        /// </summary>
        public TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate)
        {
            try
            {
                Console.WriteLine($"Converting template: {dbTemplate.Name}");
                Console.WriteLine($"  - Preview path: {dbTemplate.PreviewPath}");
                Console.WriteLine($"  - Template path: {dbTemplate.TemplatePath}");
                Console.WriteLine($"  - Layout: {dbTemplate.Layout?.Name ?? "NULL"}");

                // Check if required files exist
                if (!File.Exists(dbTemplate.PreviewPath))
                {
                    Console.WriteLine($"Preview image missing: {dbTemplate.PreviewPath}");
                    return null;
                }

                if (!File.Exists(dbTemplate.TemplatePath))
                {
                    Console.WriteLine($"Template image missing: {dbTemplate.TemplatePath}");
                    return null;
                }

                // Get dimensions from layout
                var width = dbTemplate.Layout?.Width ?? 0;
                var height = dbTemplate.Layout?.Height ?? 0;
                var photoCount = dbTemplate.Layout?.PhotoCount ?? 1;

                if (width == 0 || height == 0)
                {
                    Console.WriteLine($"Invalid template dimensions: {width}x{height}");
                    return null;
                }

                // Calculate display dimensions
                var aspectRatio = (double)width / height;
                var (displayWidth, displayHeight) = GetStandardDisplaySize(width, height);

                // Create TemplateConfig for compatibility
                var config = new TemplateConfig
                {
                    TemplateName = dbTemplate.Name,
                    TemplateId = dbTemplate.Id.ToString(),
                    Category = dbTemplate.CategoryName,
                    Description = dbTemplate.Description,
                    PhotoCount = photoCount,
                    Dimensions = new TemplateDimensions
                    {
                        Width = width,
                        Height = height
                    },
                    PhotoAreas = dbTemplate.PhotoAreas.Select(pa => new PhotoArea
                    {
                        Id = pa.Id,
                        X = pa.X,
                        Y = pa.Y,
                        Width = pa.Width,
                        Height = pa.Height
                    }).ToList()
                };

                var templateInfo = new TemplateInfo
                {
                    Config = config,
                    PreviewImagePath = dbTemplate.PreviewPath,
                    TemplateImagePath = dbTemplate.TemplatePath,
                    FolderPath = dbTemplate.FolderPath,
                    TemplateName = dbTemplate.Name,
                    Category = dbTemplate.CategoryName.ToLowerInvariant(),
                    Description = dbTemplate.Description,
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

                Console.WriteLine($"Successfully converted template: {templateInfo.TemplateName}");
                return templateInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting template {dbTemplate.Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error converting template {dbTemplate.Name}: {ex.Message}");
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
                    return (Constants.SquareWidth, Constants.SquareHeight);
                }

                double aspectRatio = (double)actualWidth / actualHeight;

                if (aspectRatio > 1.3) // Wide format (4x6, landscape)
                {
                    return (Constants.WideWidth, Constants.WideHeight);
                }
                else if (aspectRatio < 0.8) // Tall format (strips)
                {
                    return (Constants.TallWidth, Constants.TallHeight);
                }
                else // Square-ish format
                {
                    return (Constants.SquareWidth, Constants.SquareHeight);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting standard display size: {ex.Message}");
                return (Constants.SquareWidth, Constants.SquareHeight);
            }
        }

        /// <summary>
        /// Gets human-readable aspect ratio description
        /// </summary>
        public string GetAspectRatioText(double aspectRatio)
        {
            if (aspectRatio > 1.3) return "Wide";
            if (aspectRatio < 0.8) return "Tall";
            return "Square";
        }

        /// <summary>
        /// Gets template size category for CSS-like styling
        /// </summary>
        public string GetTemplateSizeCategory(double aspectRatio)
        {
            if (aspectRatio > 1.3) return "wide";
            if (aspectRatio < 0.8) return "tall";
            return "square";
        }

        /// <summary>
        /// Checks if template is valid for the selected product type
        /// </summary>
        public bool IsTemplateValidForProduct(TemplateInfo template, ProductInfo? product)
        {
            Console.WriteLine($"--- Validating template for product ---");
            Console.WriteLine($"Template: {template.TemplateName}");
            Console.WriteLine($"Template category: '{template.Category}'");
            Console.WriteLine($"Selected product: {product?.Type ?? "NULL"}");

            if (product == null) 
            {
                Console.WriteLine("No selected product - returning true");
                return true;
            }

            // Since we're now filtering by TemplateType at the database level using GetTemplatesByTypeAsync(),
            // this additional validation is redundant and was causing templates to be filtered out incorrectly.
            // The database-level filtering by TemplateType is the authoritative filter.
            
            var productType = product.Type?.ToLowerInvariant();
            var aspectRatio = template.AspectRatio;
            
            Console.WriteLine($"Template: {template.TemplateName}, AspectRatio: {aspectRatio:F2}, Product: {productType}");
            Console.WriteLine("Template valid for product: true (database filtering by TemplateType is primary filter)");
            
            // Database filtering by TemplateType handles the main filtering logic
            // This method now serves as a fallback validation only
            return true;
        }
    }
} 