using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Configuration;

namespace Photobooth
{
    /// <summary>
    /// Template selection screen with three-size system and automatic template refresh
    /// </summary>
    public partial class TemplateSelectionScreen : UserControl, IDisposable
    {
        #region Constants

        private static class Constants
        {
            public const int TemplatesPerPage = 3;
            public const string TemplatesFolder = "Templates";
            public const string ConfigFileName = "config.json";
            public const int AdminTapSequenceCount = 5;
            public const double AdminTapTimeWindow = 3.0; // seconds

            // Note: Template display sizes moved to PhotoboothConfiguration.TemplateDisplaySizes
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when user wants to go back to product selection
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// Event fired when user selects a template
        /// </summary>
        public event EventHandler<TemplateSelectedEventArgs>? TemplateSelected;





        #endregion

        #region Private Fields

        private List<TemplateInfo> allTemplates = new List<TemplateInfo>();
        private List<TemplateInfo> filteredTemplates = new List<TemplateInfo>();
        private ProductInfo? selectedProduct;
        private readonly IDatabaseService _databaseService;
        private readonly ITemplateConversionService _templateConversionService;



        // File system watcher for automatic template refresh
        private FileSystemWatcher? templateWatcher;
        private DispatcherTimer? refreshDelayTimer;

        private bool disposed = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the template selection screen
        /// </summary>
        public TemplateSelectionScreen(IDatabaseService databaseService, ITemplateConversionService templateConversionService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _templateConversionService = templateConversionService ?? throw new ArgumentNullException(nameof(templateConversionService));
            InitializeComponent();
            this.Loaded += OnLoaded;
            InitializeTemplateWatcher();
        }

        /// <summary>
        /// Constructor for design-time support
        /// </summary>
        public TemplateSelectionScreen() : this(new DatabaseService(), new TemplateConversionService())
        {
        }

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Template selection screen loaded",
                    ("SelectedProductType", selectedProduct?.Type ?? "NULL"));
                LoadTemplates();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template screen initialization failed", ex);
                System.Diagnostics.Debug.WriteLine($"Template screen initialization failed: {ex.Message}");
                ShowErrorMessage("Failed to load templates. Please restart the application.");
            }
        }

        /// <summary>
        /// Sets up the screen for a specific product type
        /// </summary>
        public void SetProductType(ProductInfo product)
        {
            try
            {
                selectedProduct = product ?? throw new ArgumentNullException(nameof(product));
                LoadTemplates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set product type: {ex.Message}");
            }
        }



        /// <summary>
        /// Sets up file system watcher to detect template changes
        /// </summary>
        private void InitializeTemplateWatcher()
        {
            try
            {
                var templatesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.TemplatesFolder);

                if (Directory.Exists(templatesPath))
                {
                    templateWatcher = new FileSystemWatcher(templatesPath)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite
                    };

                    // Watch for changes, deletions, and new files
                    templateWatcher.Created += OnTemplatesFolderChanged;
                    templateWatcher.Deleted += OnTemplatesFolderChanged;
                    templateWatcher.Renamed += OnTemplatesFolderChanged;
                    templateWatcher.Changed += OnTemplatesFolderChanged;

                    System.Diagnostics.Debug.WriteLine($"Template watcher initialized for: {templatesPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Templates folder does not exist: {templatesPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize template watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template folder changes with debouncing
        /// </summary>
        private void OnTemplatesFolderChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Use dispatcher to update UI from background thread
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Template folder changed: {e.ChangeType} - {e.Name}");

                    // Stop existing timer if running
                    refreshDelayTimer?.Stop();

                    // Create new timer with delay to avoid multiple rapid refreshes
                    refreshDelayTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(1000) // 1 second delay
                    };

                    refreshDelayTimer.Tick += (s, args) =>
                    {
                        refreshDelayTimer.Stop();
                        RefreshTemplates();
                    };

                    refreshDelayTimer.Start();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling template folder change: {ex.Message}");
            }
        }

        #endregion

        #region Template Loading

        /// <summary>
        /// Refreshes the template list - public method for manual refresh
        /// </summary>
        public void RefreshTemplates()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Refreshing templates...");
                LoadTemplates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh templates: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads templates from database without categorization
        /// </summary>
        private async void LoadTemplates()
        {
            try
            {
                LoggingService.Application.Information("Loading templates by type",
                    ("SelectedProductType", selectedProduct?.Type ?? "NULL"));

                allTemplates.Clear();

                // Map product type to template type
                TemplateType templateType = GetTemplateTypeFromProduct(selectedProduct);
                LoggingService.Application.Information("Template type determined",
                    ("TemplateType", templateType));

                // Load templates from database filtered by type
                var dbTemplates = await _databaseService.GetTemplatesByTypeAsync(templateType);
                LoggingService.Application.Information("Database returned",
                    ("Success", dbTemplates.Success),
                    ("Templates", dbTemplates.Data?.Count() ?? 0));

                if (dbTemplates.Success && dbTemplates.Data != null)
                {
                    foreach (var dbTemplate in dbTemplates.Data)
                    {
                        var templateInfo = ConvertDatabaseTemplateToTemplateInfo(dbTemplate);
                        if (templateInfo != null)
                        {
                            var isValid = IsTemplateValidForProduct(templateInfo);
                            LoggingService.Application.Information("Template validation result",
                                ("TemplateName", templateInfo.TemplateName),
                                ("Valid", isValid));
                            
                            if (isValid)
                            {
                                allTemplates.Add(templateInfo);
                            }
                        }
                    }
                }

                LoggingService.Application.Information("Total valid templates loaded",
                    ("TemplateCount", allTemplates.Count));

                // Apply seasonal prioritization
                allTemplates = ApplySeasonalPrioritization(allTemplates);

                // Show all templates without filtering
                filteredTemplates = new List<TemplateInfo>(allTemplates);
                
                // Update display
                UpdateTemplateDisplay();

                LoggingService.Application.Information("Template loading completed");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error loading templates", ex);
                System.Diagnostics.Debug.WriteLine($"Template loading failed: {ex.Message}");
                ShowErrorMessage($"Failed to load templates: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a database Template object to a TemplateInfo object for UI display
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate)
        {
            return _templateConversionService.ConvertDatabaseTemplateToTemplateInfo(dbTemplate);
        }

        /// <summary>
        /// Loads a single template from its folder with three-size system (DEPRECATED - kept for compatibility)
        /// </summary>
        private async Task<TemplateInfo?> LoadTemplateFromFolder(string folderPath)
        {
            try
            {
                LoggingService.Application.Information("Loading template from folder",
                    ("FolderPath", folderPath));

                var configPath = System.IO.Path.Combine(folderPath, Constants.ConfigFileName);
                LoggingService.Application.Information("Looking for config at",
                    ("ConfigPath", configPath));
                
                if (!File.Exists(configPath))
                {
                    LoggingService.Application.Information("Config file missing");
                    System.Diagnostics.Debug.WriteLine($"Config file missing: {configPath}");
                    return null;
                }

                LoggingService.Application.Information("Reading config file...");
                var configJson = await File.ReadAllTextAsync(configPath);
                LoggingService.Application.Information("Config JSON length",
                    ("ConfigJsonLength", configJson.Length));
                
                var config = JsonSerializer.Deserialize<TemplateConfig>(configJson);

                if (config == null)
                {
                    LoggingService.Application.Error("Failed to parse config");
                    System.Diagnostics.Debug.WriteLine($"Failed to parse config: {configPath}");
                    return null;
                }

                LoggingService.Application.Information("Config parsed successfully",
                    ("TemplateName", config.TemplateName),
                    ("Category", config.Category));

                // Find preview image
                LoggingService.Application.Information("Looking for preview image...");
                var previewPath = FindPreviewImage(folderPath);
                if (string.IsNullOrEmpty(previewPath))
                {
                    LoggingService.Application.Information("Preview image missing");
                    System.Diagnostics.Debug.WriteLine($"Preview image missing: {folderPath}");
                    return null;
                }
                LoggingService.Application.Information("Preview image found",
                    ("PreviewPath", previewPath));

                // Find template image
                var templatePath = System.IO.Path.Combine(folderPath, "template.png");
                LoggingService.Application.Information("Looking for template image at",
                    ("TemplatePath", templatePath));
                if (!File.Exists(templatePath))
                {
                    LoggingService.Application.Information("Template image missing");
                    System.Diagnostics.Debug.WriteLine($"Template image missing: {templatePath}");
                    return null;
                }
                LoggingService.Application.Information("Template image found");

                // Calculate consistent display dimensions
                var (displayWidth, displayHeight) = GetStandardDisplaySize(
                    config.Dimensions.Width,
                    config.Dimensions.Height
                );

                var aspectRatio = (double)config.Dimensions.Width / config.Dimensions.Height;

                return new TemplateInfo
                {
                    Config = config,
                    PreviewImagePath = previewPath,
                    TemplateImagePath = templatePath,
                    FolderPath = folderPath,
                    TemplateName = config.TemplateName,
                    Category = config.Category?.ToLowerInvariant() ?? "classic",
                    Description = config.Description ?? "",
                    IsSeasonalTemplate = IsSeasonalTemplate(config),
                    SeasonPriority = GetSeasonPriority(config),

                    // Three-size system display properties
                    DisplayWidth = displayWidth,
                    DisplayHeight = displayHeight,
                    DimensionText = $"{config.Dimensions.Width} × {config.Dimensions.Height}",
                    AspectRatio = aspectRatio,
                    AspectRatioText = GetAspectRatioText(aspectRatio),
                    TemplateSize = GetTemplateSizeCategory(aspectRatio)
                };
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error loading template from folder", ex);
                System.Diagnostics.Debug.WriteLine($"Error loading template from {folderPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maps product type to template type for filtering
        /// </summary>
        private TemplateType GetTemplateTypeFromProduct(ProductInfo? product)
        {
            if (product == null) return TemplateType.Strip; // Default to strips

            return product.Type?.ToLowerInvariant() switch
            {
                "strips" or "photostrips" => TemplateType.Strip,
                "4x6" or "photo4x6" => TemplateType.Photo4x6,
                "phone" or "smartphoneprint" => TemplateType.Photo4x6, // Phone prints use 4x6 templates
                _ => TemplateType.Strip // Default to strips
            };
        }

        /// <summary>
        /// Gets standard display size based on aspect ratio - THREE SIZES ONLY
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private (double width, double height) GetStandardDisplaySize(int actualWidth, int actualHeight)
        {
            return _templateConversionService.GetStandardDisplaySize(actualWidth, actualHeight);
        }

        /// <summary>
        /// Gets human-readable aspect ratio description
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private string GetAspectRatioText(double aspectRatio)
        {
            return _templateConversionService.GetAspectRatioText(aspectRatio);
        }

        /// <summary>
        /// Gets template size category for CSS-like styling
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private string GetTemplateSizeCategory(double aspectRatio)
        {
            return _templateConversionService.GetTemplateSizeCategory(aspectRatio);
        }

        /// <summary>
        /// Finds preview image with any supported extension
        /// </summary>
        private string? FindPreviewImage(string folderPath)
        {
            var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

            foreach (var ext in supportedExtensions)
            {
                var previewPath = System.IO.Path.Combine(folderPath, $"preview{ext}");
                if (File.Exists(previewPath))
                {
                    return previewPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if template is valid for the selected product type
        /// Delegates to TemplateConversionService for consistency
        /// </summary>
        private bool IsTemplateValidForProduct(TemplateInfo template)
        {
            return _templateConversionService.IsTemplateValidForProduct(template, selectedProduct);
        }

        /// <summary>
        /// Applies seasonal prioritization to template list
        /// </summary>
        private List<TemplateInfo> ApplySeasonalPrioritization(List<TemplateInfo> templates)
        {
            try
            {
                // Get active seasons (this will be replaced with actual season logic)
                var activeSeasons = GetActiveSeasons();

                // Separate seasonal and regular templates
                var seasonalTemplates = templates
                    .Where(t => t.IsSeasonalTemplate)
                    .OrderBy(t => t.SeasonPriority)
                    .ToList();

                var regularTemplates = templates
                    .Where(t => !t.IsSeasonalTemplate)
                    .OrderBy(t => t.TemplateName)
                    .ToList();

                // Combine: seasonal first, then regular
                var prioritizedTemplates = new List<TemplateInfo>();
                prioritizedTemplates.AddRange(seasonalTemplates);
                prioritizedTemplates.AddRange(regularTemplates);

                return prioritizedTemplates;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Seasonal prioritization failed", ex);
                System.Diagnostics.Debug.WriteLine($"Seasonal prioritization failed: {ex.Message}");
                return templates.OrderBy(t => t.TemplateName).ToList();
            }
        }

        #endregion

        #region Filtering and Pagination



        /// <summary>
        /// Updates the template display for current page - unified view without categories
        /// </summary>
        private void UpdateTemplateDisplay()
        {
            try
            {
                if (CategorizedTemplatesContainer == null) return;

                // Clear existing content
                CategorizedTemplatesContainer.Children.Clear();

                if (!filteredTemplates.Any())
                {
                    // Show empty state
                    var emptyMessage = new TextBlock
                    {
                        Text = "No templates available for the selected product type.",
                        FontSize = 18,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 50, 0, 0),
                        Opacity = 0.8
                    };
                    CategorizedTemplatesContainer.Children.Add(emptyMessage);
                    
                    if (TemplateCountInfo != null)
                        TemplateCountInfo.Text = "No templates found";
                    return;
                }

                // Create a single unified grid for all templates
                var templateGrid = CreateTemplateGrid(filteredTemplates);
                CategorizedTemplatesContainer.Children.Add(templateGrid);

                // Update template count info
                if (TemplateCountInfo != null)
                    TemplateCountInfo.Text = $"{filteredTemplates.Count} templates available";
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template display update failed", ex);
                System.Diagnostics.Debug.WriteLine($"Template display update failed: {ex.Message}");
            }
        }



        /// <summary>
        /// Creates a template grid for a category's templates
        /// </summary>
        private UniformGrid CreateTemplateGrid(List<TemplateInfo> templates)
        {
            var grid = new UniformGrid
            {
                Columns = 4, // 4 templates per row (larger cards need fewer columns)
                Margin = new Thickness(0, 0, 0, 20) // Minimal space after each category
            };

            foreach (var template in templates)
            {
                var templateCard = CreateTemplateCard(template);
                grid.Children.Add(templateCard);
            }

            return grid;
        }

        /// <summary>
        /// Creates a template card button
        /// </summary>
        private UIElement CreateTemplateCard(TemplateInfo template)
        {
            // Create container for image + text below
            var container = new StackPanel
            {
                Margin = new Thickness(15), // More padding around template cards
                HorizontalAlignment = HorizontalAlignment.Left // Left align the whole container
            };

            // Create the image button (no text overlay)
            var imageButton = new Button
            {
                Width = template.DisplayWidth,
                Height = template.DisplayHeight,
                Tag = template,
                Style = null, // Remove custom style
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent, // Remove border
                BorderThickness = new Thickness(0), // No border
                Padding = new Thickness(0),
                Cursor = Cursors.Hand
            };

            // Create the image content
            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(16), // Larger border radius
                ClipToBounds = true,
                Background = Brushes.Black
            };

            var image = new Image
            {
                Source = new BitmapImage(new Uri(template.PreviewImagePath, UriKind.RelativeOrAbsolute)),
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            imageBorder.Child = image;
            imageButton.Content = imageBorder;

            // Add drop shadow to button
            imageButton.Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                Opacity = 0.3,
                ShadowDepth = 3
            };

            // Create the template name below the image
            var nameLabel = new TextBlock
            {
                Text = template.TemplateName,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Left, // Left align template name
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = template.DisplayWidth,
                HorizontalAlignment = HorizontalAlignment.Left // Left align the text block
            };

            imageButton.Click += TemplateCard_Click;
            LoggingService.Application.Information("Created template card for",
                ("TemplateName", template.TemplateName ?? "Unknown"));
            LoggingService.Application.Information("Button Tag set to",
                ("TagType", imageButton.Tag?.GetType().Name ?? "null"));

            container.Children.Add(imageButton);
            container.Children.Add(nameLabel);

            return container;
        }



        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles back button click
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BackButtonClicked?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Back button error", ex);
                System.Diagnostics.Debug.WriteLine($"Back button error: {ex.Message}");
            }
        }





        /// <summary>
        /// Handles template card selection
        /// </summary>
        private void TemplateCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Template card clicked");
                LoggingService.Application.Information("Sender type",
                    ("SenderType", sender?.GetType().Name ?? "null"));
                
                if (sender is Button button)
                {
                    LoggingService.Application.Information("Button found",
                        ("ButtonType", button.Tag?.GetType().Name ?? "null"));
                    LoggingService.Application.Information("Button Tag value",
                        ("TagValue", button.Tag?.ToString() ?? "null"));
                    
                    if (button.Tag is TemplateInfo template)
                    {
                        LoggingService.Application.Information("TemplateInfo found",
                            ("TemplateName", template.TemplateName ?? "Unknown"),
                            ("TemplateCategory", template.Category ?? "Unknown"),
                            ("TemplatePath", template.PreviewImagePath ?? "Unknown"));
                        LoggingService.Application.Information("TemplateSelected event subscribers",
                            ("SubscriberCount", TemplateSelected?.GetInvocationList()?.Length ?? 0));
                        
                        LoggingService.Application.Information("Invoking TemplateSelected event...");
                        TemplateSelected?.Invoke(this, new TemplateSelectedEventArgs(template));
                        LoggingService.Application.Information("TemplateSelected event invoked successfully");
                    }
                    else
                    {
                        LoggingService.Application.Error("ERROR: Button.Tag is not TemplateInfo!");
                        LoggingService.Application.Information("Actual Tag type",
                            ("ActualTagType", button.Tag?.GetType().FullName ?? "null"));
                    }
                }
                else
                {
                    LoggingService.Application.Error("ERROR: Sender is not a Button!");
                    LoggingService.Application.Information("Actual sender type",
                        ("ActualSenderType", sender?.GetType().FullName ?? "null"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("EXCEPTION in TemplateCard_Click", ex);
                LoggingService.Application.Information("Stack trace",
                    ("StackTrace", ex.StackTrace ?? "No stack trace available"));
                System.Diagnostics.Debug.WriteLine($"Template selection error: {ex.Message}");
            }
        }





        #endregion

        #region Helper Methods

        /// <summary>
        /// Determines if a template is seasonal based on its category
        /// </summary>
        private bool IsSeasonalTemplate(TemplateConfig config)
        {
            var seasonalCategories = new[] { "holiday", "seasonal", "christmas", "halloween", "valentine", "easter" };
            return seasonalCategories.Contains(config.Category?.ToLowerInvariant());
        }

        /// <summary>
        /// Gets the season priority for template ordering
        /// </summary>
        private int GetSeasonPriority(TemplateConfig config)
        {
            // TODO: Implement actual season priority logic based on current date
            // For now, return 0 for regular seasonal priority
            return 0;
        }

        /// <summary>
        /// Gets currently active seasons
        /// </summary>
        private List<string> GetActiveSeasons()
        {
            // TODO: Implement actual season management
            // For now, return empty list
            return new List<string>();
        }

        /// <summary>
        /// Shows error message to user
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            try
            {
                NotificationService.Quick.Error(message);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error showing message", ex);
                System.Diagnostics.Debug.WriteLine($"Error showing message: {ex.Message}");
            }
        }



        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the credits display with validation
        /// </summary>
        /// <param name="credits">Current credit amount</param>
        public void UpdateCredits(decimal credits)
        {
            // Credits display logic if needed for this screen
        }

        #endregion

        #region Resource Management

        /// <summary>
        /// Cleanup resources including file system watcher
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Stop and dispose file system watcher
                if (templateWatcher != null)
                {
                    templateWatcher.EnableRaisingEvents = false;
                    templateWatcher.Created -= OnTemplatesFolderChanged;
                    templateWatcher.Deleted -= OnTemplatesFolderChanged;
                    templateWatcher.Renamed -= OnTemplatesFolderChanged;
                    templateWatcher.Changed -= OnTemplatesFolderChanged;
                    templateWatcher.Dispose();
                    templateWatcher = null;
                }

                // Stop refresh delay timer
                refreshDelayTimer?.Stop();
                refreshDelayTimer = null;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Cleanup error", ex);
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                Cleanup();
                disposed = true;
            }
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Template configuration from config.json
    /// </summary>
    public class TemplateConfig
    {
        public string TemplateName { get; set; } = "";
        public string TemplateId { get; set; } = "";
        public TemplateDimensions Dimensions { get; set; } = new();
        public List<PhotoArea> PhotoAreas { get; set; } = new();
        public int PhotoCount { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Template dimensions
    /// </summary>
    public class TemplateDimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Photo area definition
    /// </summary>
    public class PhotoArea
    {
        public string Id { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Complete template information with three-size display system
    /// </summary>
    public class TemplateInfo
    {
        public TemplateConfig Config { get; set; } = new();
        public string PreviewImagePath { get; set; } = "";
        public string TemplateImagePath { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsSeasonalTemplate { get; set; }
        public int SeasonPriority { get; set; }

        // Three-size display system properties
        public double DisplayWidth { get; set; }
        public double DisplayHeight { get; set; }
        public string DimensionText { get; set; } = "";
        public double AspectRatio { get; set; }
        public string AspectRatioText { get; set; } = "";
        public string TemplateSize { get; set; } = "";
    }

    /// <summary>
    /// Event arguments for template selection
    /// </summary>
    public class TemplateSelectedEventArgs : EventArgs
    {
        public TemplateInfo Template { get; }

        public TemplateSelectedEventArgs(TemplateInfo template)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
        }
    }

    #endregion
}
