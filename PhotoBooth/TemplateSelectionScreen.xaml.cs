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
using System.Windows.Shapes;
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
        private readonly MainWindow? _mainWindow; // Reference to MainWindow for operation mode check

        // Credit system integration
        private AdminDashboardScreen? _adminDashboard;
        private decimal _currentCredits = 0;

        // File system watcher for automatic template refresh
        private FileSystemWatcher? templateWatcher;
        private DispatcherTimer? refreshDelayTimer;

        private bool disposed = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the template selection screen
        /// </summary>
        public TemplateSelectionScreen(IDatabaseService databaseService, ITemplateConversionService templateConversionService, MainWindow? mainWindow = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _templateConversionService = templateConversionService ?? throw new ArgumentNullException(nameof(templateConversionService));
            _mainWindow = mainWindow;
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
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Template selection screen loaded",
                    ("SelectedProductType", selectedProduct?.Type ?? "NULL"));
                
                _ = RefreshCurrentCredits(); // This now includes UpdateCreditsDisplay()
                
                await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Template screen initialization failed", ex);
                ShowErrorMessage("Failed to load templates. Please restart the application.");
            }
        }

        /// <summary>
        /// Sets up the screen for a specific product type
        /// </summary>
        public async Task SetProductTypeAsync(ProductInfo product)
        {
            try
            {
                selectedProduct = product ?? throw new ArgumentNullException(nameof(product));
                await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to set product type", ex, ("ProductType", product?.Type ?? "Unknown"));
            }
        }

        /// <summary>
        /// Refresh product prices from database and update the selected product
        /// This ensures we have the latest prices even if admin made changes
        /// </summary>
        public async Task RefreshProductPricesAsync()
        {
            try
            {
                LoggingService.Application.Information("Refreshing product prices in TemplateSelectionScreen");
                
                var result = await _databaseService.GetProductsAsync();
                if (result.Success && result.Data != null)
                {
                    // Find the matching product in the database
                    var dbProduct = result.Data.FirstOrDefault(p => 
                        selectedProduct != null && 
                        GetProductTypeFromName(selectedProduct.Type) == p.ProductType);
                    
                    if (dbProduct != null && selectedProduct != null)
                    {
                        // Update the selected product with the latest price from database
                        selectedProduct.Price = dbProduct.Price;
                        LoggingService.Application.Information("Updated product price from database", 
                            ("ProductType", selectedProduct.Type),
                            ("NewPrice", selectedProduct.Price));
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error refreshing product prices in TemplateSelectionScreen", ex);
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

                    refreshDelayTimer.Tick += async (s, args) =>
                    {
                        refreshDelayTimer.Stop();
                        await RefreshTemplatesAsync();
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
        public async Task RefreshTemplatesAsync()
        {
            try
            {
                LoggingService.Application.Information("Refreshing templates...");
                await LoadTemplatesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to refresh templates", ex);
            }
        }

        /// <summary>
        /// Loads templates from database without categorization
        /// </summary>
        private async Task LoadTemplatesAsync()
        {
            try
            {
                // Show loading state
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                
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
                
                // Hide loading state
                LoadingPanel.Visibility = Visibility.Collapsed;
                
                // Update display
                UpdateTemplateDisplay();

                LoggingService.Application.Information("Template loading completed");
            }
            catch (Exception ex)
            {
                // Hide loading state and show error
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                
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

                // Calculate actual file size from template.png
                long templateFileSize = 0;
                try
                {
                    var templateFileInfo = new FileInfo(templatePath);
                    templateFileSize = templateFileInfo.Length;
                    LoggingService.Application.Information("Template file size calculated", 
                        ("FileSizeBytes", templateFileSize),
                        ("FileSizeFormatted", FormatFileSize(templateFileSize)));
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Warning("Could not calculate template file size", ("Exception", ex.Message));
                    templateFileSize = 0; // Fallback to 0
                }

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
                    TemplateSize = GetTemplateSizeCategory(aspectRatio),
                    
                    // Actual file size
                    FileSize = templateFileSize
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
        /// Maps product type name to ProductType enum
        /// </summary>
        private ProductType GetProductTypeFromName(string? productTypeName)
        {
            return productTypeName?.ToLowerInvariant() switch
            {
                "strips" or "photostrips" => ProductType.PhotoStrips,
                "4x6" or "photo4x6" => ProductType.Photo4x6,
                "phone" or "smartphoneprint" => ProductType.SmartphonePrint,
                _ => ProductType.PhotoStrips // Default to strips
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
        /// Updates the template display
        /// </summary>
        private void UpdateTemplateDisplay()
        {
            try
            {
                LoggingService.Application.Information("Updating template display",
                    ("FilteredTemplateCount", filteredTemplates.Count));

                // Clear existing templates
                TemplatesUniformGrid.Children.Clear();

                if (filteredTemplates.Count == 0)
                {
                    EmptyStatePanel.Visibility = Visibility.Visible;
                    return;
                }

                EmptyStatePanel.Visibility = Visibility.Collapsed;

                // Add templates to grid
                foreach (var template in filteredTemplates)
                {
                    var templateCard = CreateTemplateCard(template);
                    TemplatesUniformGrid.Children.Add(templateCard);
                }

                // Update template count
                // TemplateCountInfo.Text = $"{filteredTemplates.Count} template{(filteredTemplates.Count == 1 ? "" : "s")} available";

                LoggingService.Application.Information("Template display updated successfully",
                    ("CardsCreated", filteredTemplates.Count));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating template display", ex);
                EmptyStatePanel.Visibility = Visibility.Visible;
                // TemplateCountInfo.Text = "Error loading templates";
            }
        }

        /// <summary>
        /// Creates a modern template card with admin-style layout
        /// </summary>
        private Border CreateTemplateCard(TemplateInfo template)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), // #E2E8F0
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(16), // Increased margin for more spacing
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand
            };

            // Add hover effect
            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #F8FAFC
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #CBD5E1
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = Brushes.White;
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Auto height for preview
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Auto height for info

            // Template preview container with rounded corners
            var previewBorder = new Border
            {
                CornerRadius = new CornerRadius(8, 8, 0, 0), // Rounded top corners only
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), // #F1F5F9
                MinHeight = 300,
                MaxHeight = 500,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var previewBitmap = new BitmapImage();
            previewBitmap.BeginInit();
            previewBitmap.CacheOption = BitmapCacheOption.OnLoad; // Prevents file locking
            previewBitmap.UriSource = new Uri(template.PreviewImagePath, UriKind.RelativeOrAbsolute);
            previewBitmap.EndInit();
            var previewImage = new Image
            {
                Source = previewBitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = Cursors.Hand
            };

            // Use Clip for better performance
            previewImage.Loaded += (s, e) =>
            {
                var img = s as Image;
                if (img != null && img.ActualWidth > 0 && img.ActualHeight > 0)
                {
                    img.Clip = new RectangleGeometry
                    {
                        Rect = new Rect(0, 0, img.ActualWidth, img.ActualHeight),
                        RadiusX = 8,
                        RadiusY = 8
                    };
                }
            };

            // Put the image directly in the rounded border
            previewBorder.Child = previewImage;

            Grid.SetRow(previewBorder, 0);

            // Template info panel
            var infoPanel = new StackPanel
            {
                Margin = new Thickness(16)
            };

            // Header with name and badges
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = template.TemplateName,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)), // #374151
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 16
            };
            Grid.SetColumn(nameText, 0);

            // Category badge
            var categoryBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)), // #F3F4F6
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), // #D1D5DB
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = template.Category ?? "Classic",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)) // #4B5563
                }
            };
            Grid.SetColumn(categoryBadge, 1);

            // Template Type Badge - Fix the detection logic
            var isStrip = IsStripTemplate(template);
            var templateTypeBadge = new Border
            {
                Background = isStrip ? 
                    new SolidColorBrush(Color.FromRgb(254, 243, 199)) : // Yellow for Strip
                    new SolidColorBrush(Color.FromRgb(219, 234, 254)), // Blue for 4x6
                BorderBrush = isStrip ? 
                    new SolidColorBrush(Color.FromRgb(245, 158, 11)) : // Yellow border for Strip
                    new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue border for 4x6
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = isStrip ? "Strip" : "4x6",
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = isStrip ? 
                        new SolidColorBrush(Color.FromRgb(146, 64, 14)) : // Yellow text for Strip
                        new SolidColorBrush(Color.FromRgb(30, 58, 138)) // Blue text for 4x6
                }
            };
            Grid.SetColumn(templateTypeBadge, 2);

            headerGrid.Children.Add(nameText);
            headerGrid.Children.Add(categoryBadge);
            headerGrid.Children.Add(templateTypeBadge);

            // Details grid (matching admin exactly - price and file size)
            var detailsGrid = new Grid { Margin = new Thickness(0, 8, 0, 12) };
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var templatePrice = GetTemplatePrice(template);
            var hasSufficientCredits = HasSufficientCredits(templatePrice);

            var priceText = new TextBlock
            {
                Text = $"${templatePrice:F2}",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = hasSufficientCredits ? 
                    new SolidColorBrush(Color.FromRgb(34, 197, 94)) : // Green if affordable
                    new SolidColorBrush(Color.FromRgb(239, 68, 68))   // Red if not affordable
            };
            Grid.SetColumn(priceText, 0);

            // Use actual file size from template data if available
            var fileSize = template.FileSize > 0 ? template.FileSize : 100; // Fallback to 100 bytes if no file size
            var sizeText = new TextBlock
            {
                Text = FormatFileSize(fileSize), // Use existing FormatFileSize method
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), // #64748B
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(sizeText, 1);

            detailsGrid.Children.Add(priceText);
            detailsGrid.Children.Add(sizeText);

            infoPanel.Children.Add(headerGrid);
            infoPanel.Children.Add(detailsGrid);

            Grid.SetRow(infoPanel, 1);

            grid.Children.Add(previewBorder);
            grid.Children.Add(infoPanel);

            card.Child = grid;

            // Make the entire card clickable with credit validation
            card.MouseLeftButtonDown += (s, e) =>
        {
            try
            {
                    LoggingService.Application.Information("Template card clicked", ("TemplateName", template.TemplateName));
                    
                    var templatePrice = GetTemplatePrice(template);
                    var hasSufficientCredits = HasSufficientCredits(templatePrice);
                    
                    if (!hasSufficientCredits)
                    {
                        // Show insufficient credits notification using custom notification system
                        var shortfall = templatePrice - _currentCredits;
                        if (_mainWindow?.IsFreePlayMode == true)
                        {
                            // This shouldn't happen in free play mode, but if it does, show a generic error
                            var message = "Unable to select this template.\n\nPlease contact staff for assistance.";
                            NotificationService.Instance.ShowWarning("Selection Error", message, 8);
                        }
                        else
                        {
                            var message = $"Template price: ${templatePrice:F2}\n" +
                                         $"Current credits: ${_currentCredits:F2}\n" +
                                         $"Additional credits needed: ${shortfall:F2}\n\n" +
                                         $"Please add more credits to continue.";
                            
                            NotificationService.Instance.ShowWarning("Insufficient Credits", message, 8);
                        }
                        
                        LoggingService.Application.Warning("Template selection blocked - insufficient credits",
                            ("TemplateName", template.TemplateName),
                            ("RequiredPrice", templatePrice),
                            ("CurrentCredits", _currentCredits),
                            ("Shortfall", shortfall));
                        
                        return; // Don't proceed with template selection
                    }
                    
                    // Proceed with template selection
                    TemplateSelected?.Invoke(this, new TemplateSelectedEventArgs(template));
                    
                    LoggingService.Application.Information("Template selection approved",
                        ("TemplateName", template.TemplateName),
                        ("Price", templatePrice),
                        ("CreditsAfterPurchase", _currentCredits - templatePrice));
            }
            catch (Exception ex)
            {
                    LoggingService.Application.Error("Error handling template card click", ex);
            }
            };

            LoggingService.Application.Information("Created modern template card",
                ("TemplateName", template.TemplateName ?? "Unknown"),
                ("Category", template.Category ?? "Unknown"),
                ("IsStrip", isStrip));

            return card;
        }

        /// <summary>
        /// Formats file size in human-readable format (matching admin method)
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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
        /// Handles responsive column adjustment based on available width
        /// Currently disabled to maintain fixed 4-column kiosk layout
        /// </summary>
        private void TemplatesUniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (sender is UniformGrid grid)
                {
                    // Fixed 4-column layout for kiosk application
                    // Responsive behavior disabled to maintain consistent admin layout match
                    if (grid.Columns != 4)
                    {
                        grid.Columns = 4;
                        LoggingService.Application.Information($"Template grid columns reset to 4 (kiosk mode) for width {e.NewSize.Width:F0}px");
                    }

                    /* RESPONSIVE LOGIC - Available if needed for different hardware configurations
                    var width = e.NewSize.Width;
                    
                    // Calculate optimal columns based on available width
                    // Each template card is roughly 300px wide with margins
                    var optimalColumns = width switch
                    {
                        >= 1400 => 4, // Full kiosk resolution (1920x1080) - primary target  
                        >= 1000 => 3, // Medium screens/smaller kiosks
                        >= 700 => 2,  // Small screens/tablets
                        _ => 1        // Very small screens (failsafe)
                    };

                    // Only update if columns actually changed to avoid unnecessary re-layout
                    if (grid.Columns != optimalColumns)
                    {
                        grid.Columns = optimalColumns;
                        LoggingService.Application.Information($"Template grid columns adjusted to {optimalColumns} for width {width:F0}px");
                    }
                    */
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error in template grid size changed handler", ex);
                System.Diagnostics.Debug.WriteLine($"Template grid size changed error: {ex.Message}");
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

        /// <summary>
        /// Determines if a template is a strip template using multiple detection methods
        /// </summary>
        private bool IsStripTemplate(TemplateInfo template)
        {
            // Method 1: Check template name for "strip" keyword
            if (template.TemplateName?.ToLowerInvariant().Contains("strip") == true)
                return true;

            // Method 2: Check folder path for "strip" keyword
            if (template.FolderPath?.ToLowerInvariant().Contains("strip") == true)
                return true;

            // Method 3: Check template size property
            if (template.TemplateSize?.ToLowerInvariant().Contains("strip") == true)
                return true;

            // Method 4: Check aspect ratio - strips are typically tall (height > width)
            if (template.Config?.Dimensions != null)
            {
                var aspectRatio = (double)template.Config.Dimensions.Width / template.Config.Dimensions.Height;
                // If height is significantly greater than width, it's likely a strip
                if (aspectRatio < 0.8) // Width is less than 80% of height
                    return true;
            }

            // Default to 4x6 if none of the above conditions match
            return false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the admin dashboard reference for credit checking
        /// </summary>
        public void SetAdminDashboard(AdminDashboardScreen adminDashboard)
        {
            _adminDashboard = adminDashboard;
            _ = RefreshCurrentCredits();
        }

        /// <summary>
        /// Refresh current credits from admin dashboard or database
        /// </summary>
        private async Task RefreshCurrentCredits()
        {
            try
            {
                // Try to get credits from admin dashboard first (if available)
                if (_adminDashboard != null)
                {
                    _currentCredits = _adminDashboard.GetCurrentCredits();
                    UpdateCreditsDisplay();
                    return;
                }

                // Fallback: Get credits directly from database asynchronously
                var creditsResult = await _databaseService.GetSettingValueAsync<decimal>("System", "CurrentCredits");
                
                if (creditsResult.Success)
                {
                    _currentCredits = creditsResult.Data;
                }
                else
                {
                    _currentCredits = 0;
                }
                UpdateCreditsDisplay();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error refreshing credits", ex);
                _currentCredits = 0;
                UpdateCreditsDisplay();
            }
        }

        /// <summary>
        /// Check if user has sufficient credits for a template
        /// </summary>
        private bool HasSufficientCredits(decimal templatePrice)
        {
            Console.WriteLine($"=== TEMPLATE CREDIT CHECK === Price: {templatePrice}, Credits: {_currentCredits}");
            Console.WriteLine($"=== TEMPLATE CREDIT CHECK === MainWindow: {_mainWindow != null}, IsFreePlayMode: {_mainWindow?.IsFreePlayMode}");
            
            // Check if we're in free play mode - if so, always return true
            if (_mainWindow?.IsFreePlayMode == true)
            {
                Console.WriteLine($"=== TEMPLATE CREDIT CHECK === FREE PLAY MODE - ALLOWING SELECTION");
                LoggingService.Application.Information("Free play mode detected - skipping credit check for template selection",
                    ("TemplatePrice", templatePrice),
                    ("OperationMode", _mainWindow.CurrentOperationMode));
                return true;
            }
            
            var hasSufficient = _currentCredits >= templatePrice;
            Console.WriteLine($"=== TEMPLATE CREDIT CHECK === COIN MODE - HasSufficient: {hasSufficient}");
            return hasSufficient;
        }



        /// <summary>
        /// Get the price for a template based on product type
        /// </summary>
        private decimal GetTemplatePrice(TemplateInfo template)
        {
            try
            {
                // Use actual product price from the selected product
                if (selectedProduct?.Price > 0)
                {
                    return selectedProduct.Price;
                }
                
                // Fallback to default if product price not available
                LoggingService.Application.Warning("Product price not available, using default", 
                    ("ProductType", selectedProduct?.Type ?? "Unknown"));
                return 3.00m;
            }
            catch
            {
                return 3.00m; // Default fallback price
            }
        }

        /// <summary>
        /// Updates the credits display with validation
        /// </summary>
        /// <param name="credits">Current credit amount</param>
        public void UpdateCredits(decimal credits)
        {
            _currentCredits = credits;
            try
            {
                LoggingService.Application.Debug("Credits updated", ("Credits", credits));
                UpdateCreditsDisplay();
                // Refresh template display to update affordability indicators
                UpdateTemplateDisplay();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating credits display", ex);
            }
        }

        /// <summary>
        /// Updates the credits display text safely
        /// </summary>
        private void UpdateCreditsDisplay()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(UpdateCreditsDisplay);
                    return;
                }
                if (CreditsDisplay != null)
                {
                    string displayText;
                    if (_mainWindow?.IsFreePlayMode == true)
                    {
                        displayText = "Free Play Mode";
                    }
                    else
                    {
                        displayText = $"Credits: ${_currentCredits:F0}";
                    }
                    CreditsDisplay.Text = displayText;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update credits display", ex);
            }
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
        
        // Actual file size information
        public long FileSize { get; set; }
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
