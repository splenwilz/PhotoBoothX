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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;

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

            // Three consistent template sizes
            public const double WideWidth = 300.0;     // Even larger cards
            public const double WideHeight = 210.0;    // Even larger cards
            public const double TallWidth = 280.0;     // Even larger cards
            public const double TallHeight = 210.0;    // Even larger cards
            public const double SquareWidth = 290.0;   // Even larger cards
            public const double SquareHeight = 210.0;  // Even larger cards
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
        private string currentCategory = "All";
        private int currentPage = 0;
        private int totalPages = 0;
        private ProductInfo? selectedProduct;
        private readonly IDatabaseService _databaseService;

        // Animation resources
        private readonly List<Storyboard> activeStoryboards = new List<Storyboard>();
        private DispatcherTimer? animationTimer;
        private Random animationRandom = new Random();

        // File system watcher for automatic template refresh
        private FileSystemWatcher? templateWatcher;
        private DispatcherTimer? refreshDelayTimer;

        private bool disposed = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the template selection screen
        /// </summary>
        public TemplateSelectionScreen(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            InitializeComponent();
            this.Loaded += OnLoaded;
            InitializeTemplateWatcher();
        }

        /// <summary>
        /// Constructor for design-time support
        /// </summary>
        public TemplateSelectionScreen() : this(new DatabaseService())
        {
        }

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("=== TEMPLATE SELECTION SCREEN LOADED ===");
                Console.WriteLine($"Selected product: {selectedProduct?.Type ?? "NULL"}");
                InitializeAnimations();
                LoadTemplates();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Template screen initialization failed: {ex.Message}");
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
        /// Sets the selected category to filter templates
        /// </summary>
        public void SetSelectedCategory(TemplateCategory category)
        {
            try
            {
                if (category != null)
                {
                    currentCategory = category.Name;
                    System.Diagnostics.Debug.WriteLine($"Selected category: {currentCategory}");
                    
                    // Apply the filter with the selected category
                    ApplyFilter(currentCategory);
                    
                    // Update category buttons to reflect selection
                    UpdateCategoryButtons();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set selected category: {ex.Message}");
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
        /// Loads all templates from the database
        /// </summary>
        private async void LoadTemplates()
        {
            try
            {
                Console.WriteLine("=== STARTING TEMPLATE LOADING FROM DATABASE ===");
                allTemplates.Clear();

                // Set to show all templates since category filtering UI is removed
                currentCategory = "All";

                // Load templates from database
                var result = await _databaseService.GetAllTemplatesAsync(showAllSeasons: false);
                Console.WriteLine($"Database query result: Success={result.Success}");

                if (!result.Success)
                {
                    Console.WriteLine($"Failed to load templates from database: {result.ErrorMessage}");
                    ShowErrorMessage($"Failed to load templates: {result.ErrorMessage}");
                    ApplyFilter(currentCategory);
                    return;
                }

                if (result.Data == null || !result.Data.Any())
                {
                    Console.WriteLine("No templates found in database");
                    ShowErrorMessage("No templates found. Please add templates through the admin panel.");
                    ApplyFilter(currentCategory);
                    return;
                }

                Console.WriteLine($"Loaded {result.Data.Count} templates from database");

                // Convert database templates to TemplateInfo objects
                var loadedTemplates = new List<TemplateInfo>();
                foreach (var dbTemplate in result.Data)
                {
                    try
                    {
                        Console.WriteLine($"Processing template: {dbTemplate.Name} (Category: {dbTemplate.CategoryName})");
                        
                        var templateInfo = ConvertDatabaseTemplateToTemplateInfo(dbTemplate);
                        
                        if (templateInfo != null)
                        {
                            var isValid = IsTemplateValidForProduct(templateInfo);
                            Console.WriteLine($"Template '{templateInfo.TemplateName}' is valid for current product: {isValid}");
                            Console.WriteLine($"  - Template category: {templateInfo.Category}");
                            Console.WriteLine($"  - Selected product: {selectedProduct?.Type ?? "NULL"}");
                            
                            if (isValid)
                            {
                                loadedTemplates.Add(templateInfo);
                                Console.WriteLine($"✓ Added template: {templateInfo.TemplateName}");
                            }
                            else
                            {
                                Console.WriteLine($"✗ Skipped template: {templateInfo.TemplateName} (not valid for product)");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"✗ Failed to convert template: {dbTemplate.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR processing template {dbTemplate.Name}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Failed to process template {dbTemplate.Name}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Loaded {loadedTemplates.Count} valid templates before seasonal prioritization");

                // Apply seasonal prioritization
                allTemplates = ApplySeasonalPrioritization(loadedTemplates);
                Console.WriteLine($"After seasonal prioritization: {allTemplates.Count} templates");

                foreach (var template in allTemplates)
                {
                    Console.WriteLine($"  - {template.TemplateName} (Category: {template.Category})");
                }

                // Apply current filter and update display
                Console.WriteLine($"Applying filter for category: '{currentCategory}'");
                ApplyFilter(currentCategory);
                Console.WriteLine("=== TEMPLATE LOADING COMPLETE ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR in template loading: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Template loading failed: {ex.Message}");
                ShowErrorMessage("Failed to load templates.");
            }
        }

        /// <summary>
        /// Converts a database Template object to a TemplateInfo object for UI display
        /// </summary>
        private TemplateInfo? ConvertDatabaseTemplateToTemplateInfo(Template dbTemplate)
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
        /// Loads a single template from its folder with three-size system (DEPRECATED - kept for compatibility)
        /// </summary>
        private async Task<TemplateInfo?> LoadTemplateFromFolder(string folderPath)
        {
            try
            {
                Console.WriteLine($"--- Loading template from folder: {folderPath} ---");

                var configPath = System.IO.Path.Combine(folderPath, Constants.ConfigFileName);
                Console.WriteLine($"Looking for config at: {configPath}");
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Config file missing: {configPath}");
                    System.Diagnostics.Debug.WriteLine($"Config file missing: {configPath}");
                    return null;
                }

                Console.WriteLine("Reading config file...");
                var configJson = await File.ReadAllTextAsync(configPath);
                Console.WriteLine($"Config JSON length: {configJson.Length} chars");
                
                var config = JsonSerializer.Deserialize<TemplateConfig>(configJson);

                if (config == null)
                {
                    Console.WriteLine($"Failed to parse config: {configPath}");
                    System.Diagnostics.Debug.WriteLine($"Failed to parse config: {configPath}");
                    return null;
                }

                Console.WriteLine($"Config parsed successfully: {config.TemplateName} (Category: {config.Category})");

                // Find preview image
                Console.WriteLine("Looking for preview image...");
                var previewPath = FindPreviewImage(folderPath);
                if (string.IsNullOrEmpty(previewPath))
                {
                    Console.WriteLine($"Preview image missing: {folderPath}");
                    System.Diagnostics.Debug.WriteLine($"Preview image missing: {folderPath}");
                    return null;
                }
                Console.WriteLine($"Preview image found: {previewPath}");

                // Find template image
                var templatePath = System.IO.Path.Combine(folderPath, "template.png");
                Console.WriteLine($"Looking for template image at: {templatePath}");
                if (!File.Exists(templatePath))
                {
                    Console.WriteLine($"Template image missing: {templatePath}");
                    System.Diagnostics.Debug.WriteLine($"Template image missing: {templatePath}");
                    return null;
                }
                Console.WriteLine("Template image found");

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
                System.Diagnostics.Debug.WriteLine($"Error loading template from {folderPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets standard display size based on aspect ratio - THREE SIZES ONLY
        /// </summary>
        private (double width, double height) GetStandardDisplaySize(int actualWidth, int actualHeight)
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
        private string GetAspectRatioText(double aspectRatio)
        {
            if (aspectRatio > 1.3) return "Wide";
            if (aspectRatio < 0.8) return "Tall";
            return "Square";
        }

        /// <summary>
        /// Gets template size category for CSS-like styling
        /// </summary>
        private string GetTemplateSizeCategory(double aspectRatio)
        {
            if (aspectRatio > 1.3) return "wide";
            if (aspectRatio < 0.8) return "tall";
            return "square";
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
        /// </summary>
        private bool IsTemplateValidForProduct(TemplateInfo template)
        {
            Console.WriteLine($"--- Validating template for product ---");
            Console.WriteLine($"Template: {template.TemplateName}");
            Console.WriteLine($"Template category: '{template.Category}'");
            Console.WriteLine($"Selected product: {selectedProduct?.Type ?? "NULL"}");

            if (selectedProduct == null) 
            {
                Console.WriteLine("No selected product - returning true");
                return true;
            }

            // For now, allow all templates regardless of product type
            // The database design assumes templates are categorized differently (Classic, Fun, Holiday, etc.)
            // rather than by product type (strips, 4x6, phone)
            // Template layouts determine the product compatibility
            
            Console.WriteLine("Template valid for product: true (allowing all templates for now)");
            return true;
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
                System.Diagnostics.Debug.WriteLine($"Seasonal prioritization failed: {ex.Message}");
                return templates.OrderBy(t => t.TemplateName).ToList();
            }
        }

        #endregion

        #region Filtering and Pagination

        /// <summary>
        /// Applies category filter and updates display
        /// </summary>
        private void ApplyFilter(string category)
        {
            try
            {
                Console.WriteLine($"=== APPLYING FILTER ===");
                Console.WriteLine($"Filter category: '{category}'");
                Console.WriteLine($"All templates count: {allTemplates.Count}");

                currentCategory = category;

                if (category.ToLowerInvariant() == "all")
                {
                    filteredTemplates = new List<TemplateInfo>(allTemplates);
                    Console.WriteLine($"Showing all templates: {filteredTemplates.Count}");
                }
                else
                {
                    filteredTemplates = allTemplates
                        .Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    Console.WriteLine($"Filtered templates for '{category}': {filteredTemplates.Count}");
                    
                    // Debug: show which templates matched
                    foreach (var template in filteredTemplates)
                    {
                        Console.WriteLine($"  - Matched: {template.TemplateName} (Category: '{template.Category}')");
                    }
                    
                    // Debug: show which templates didn't match
                    var nonMatching = allTemplates.Where(t => 
                        !string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
                    Console.WriteLine($"Non-matching templates: {nonMatching.Count}");
                    foreach (var template in nonMatching)
                    {
                        Console.WriteLine($"  - No match: {template.TemplateName} (Category: '{template.Category}')");
                    }
                }

                // Reset to first page
                currentPage = 0;
                totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredTemplates.Count / Constants.TemplatesPerPage));
                Console.WriteLine($"Total pages calculated: {totalPages}");

                Console.WriteLine($"Updating UI components...");
                UpdateCategoryButtons();
                UpdateTemplateDisplay();
                UpdatePagination();
                Console.WriteLine("=== FILTER APPLIED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ApplyFilter: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Filter application failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the template display for current page
        /// </summary>
        private void UpdateTemplateDisplay()
        {
            try
            {
                if (CategorizedTemplatesContainer == null) return;

                // Clear existing content
                CategorizedTemplatesContainer.Children.Clear();

                // Group templates by category
                var groupedTemplates = filteredTemplates
                    .GroupBy(t => t.Category)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (!groupedTemplates.Any())
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

                int totalTemplateCount = 0;

                // Create sections for each category
                foreach (var categoryGroup in groupedTemplates)
                {
                    var categoryName = categoryGroup.Key;
                    var categoryTemplates = categoryGroup.ToList();
                    totalTemplateCount += categoryTemplates.Count;

                    // Create category header
                    var categoryHeader = new TextBlock
                    {
                        Text = categoryName,
                        FontSize = 28, // Smaller header
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        Margin = new Thickness(15, 25, 15, 15), // Add left/right margin to align with templates
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    
                    // Add gradient underline effect
                    var headerContainer = new StackPanel();
                    headerContainer.Children.Add(categoryHeader);
                    
                    var underline = new Border
                    {
                        Height = 3,
                        Width = 150, // Smaller underline
                        HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(2),
                        Background = new LinearGradientBrush(
                            Color.FromRgb(168, 85, 247), // Purple
                            Color.FromRgb(59, 130, 246), // Blue
                            new Point(0, 0), new Point(1, 0)),
                        Margin = new Thickness(15, -8, 15, 15) // Add left/right margin to align with templates
                    };
                    headerContainer.Children.Add(underline);
                    
                    CategorizedTemplatesContainer.Children.Add(headerContainer);

                    // Create template grid for this category
                    var templateGrid = CreateTemplateGrid(categoryTemplates);
                    CategorizedTemplatesContainer.Children.Add(templateGrid);
                }

                // Update template count info
                if (TemplateCountInfo != null)
                {
                    var categoryCount = groupedTemplates.Count;
                    TemplateCountInfo.Text = $"{totalTemplateCount} templates in {categoryCount} categories";
                }

                Console.WriteLine($"=== CATEGORIZED TEMPLATE DISPLAY UPDATED ===");
                Console.WriteLine($"Categories: {groupedTemplates.Count}, Total templates: {totalTemplateCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in UpdateTemplateDisplay: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Template display update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates category button visual states
        /// </summary>
        private void UpdateCategoryButtons()
        {
            try
            {
                // Category buttons have been removed from UI - no longer need to update them
                // This method is kept for compatibility but does nothing
                System.Diagnostics.Debug.WriteLine($"Current category: {currentCategory} (UI buttons removed)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category button update failed: {ex.Message}");
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
            Console.WriteLine($"Created template card for: {template.TemplateName}");
            Console.WriteLine($"Button Tag set to: {imageButton.Tag?.GetType().Name}");

            container.Children.Add(imageButton);
            container.Children.Add(nameLabel);

            return container;
        }

        /// <summary>
        /// Updates pagination display (deprecated for categorized view)
        /// </summary>
        private void UpdatePagination()
        {
            try
            {
                // Pagination is no longer used in categorized view
                // This method is kept for compatibility
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pagination update failed: {ex.Message}");
            }
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
                System.Diagnostics.Debug.WriteLine($"Back button error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles category button clicks (DEPRECATED - category buttons removed from UI)
        /// </summary>
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Category buttons have been removed from UI - this method is kept for compatibility
                System.Diagnostics.Debug.WriteLine("CategoryButton_Click called but category buttons have been removed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category button error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles template card selection
        /// </summary>
        private void TemplateCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("=== TEMPLATE CARD CLICKED ===");
                Console.WriteLine($"Sender type: {sender?.GetType().Name}");
                
                if (sender is Button button)
                {
                    Console.WriteLine($"Button found. Tag type: {button.Tag?.GetType().Name}");
                    Console.WriteLine($"Button Tag value: {button.Tag}");
                    
                    if (button.Tag is TemplateInfo template)
                    {
                        Console.WriteLine($"TemplateInfo found: {template.TemplateName}");
                        Console.WriteLine($"Template Category: {template.Category}");
                        Console.WriteLine($"Template Path: {template.PreviewImagePath}");
                        Console.WriteLine($"TemplateSelected event subscribers: {TemplateSelected?.GetInvocationList()?.Length ?? 0}");
                        
                        Console.WriteLine("Invoking TemplateSelected event...");
                        TemplateSelected?.Invoke(this, new TemplateSelectedEventArgs(template));
                        Console.WriteLine("TemplateSelected event invoked successfully");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Button.Tag is not TemplateInfo!");
                        Console.WriteLine($"Actual Tag type: {button.Tag?.GetType().FullName ?? "null"}");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: Sender is not a Button!");
                    Console.WriteLine($"Actual sender type: {sender?.GetType().FullName ?? "null"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in TemplateCard_Click: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Template selection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles previous page navigation
        /// </summary>
        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    UpdateTemplateDisplay();
                    UpdatePagination();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Previous page error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles next page navigation
        /// </summary>
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPage < totalPages - 1)
                {
                    currentPage++;
                    UpdateTemplateDisplay();
                    UpdatePagination();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Next page error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error showing message: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes floating orb animations
        /// </summary>
        private void InitializeAnimations()
        {
            try
            {
                // Initialize floating orbs
                var random = new Random();
                foreach (Ellipse orb in FloatingOrbsCanvas.Children)
                {
                    var translateTransform = new TranslateTransform();
                    orb.RenderTransform = translateTransform;

                    var storyboard = new Storyboard();

                    var yAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = random.Next(-20, -5),
                        Duration = TimeSpan.FromSeconds(3 + random.NextDouble() * 2),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };

                    var xAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = random.Next(-10, 10),
                        Duration = TimeSpan.FromSeconds(4 + random.NextDouble() * 2),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };

                    Storyboard.SetTarget(yAnimation, translateTransform);
                    Storyboard.SetTargetProperty(yAnimation, new PropertyPath("Y"));
                    Storyboard.SetTarget(xAnimation, translateTransform);
                    Storyboard.SetTargetProperty(xAnimation, new PropertyPath("X"));

                    storyboard.Children.Add(yAnimation);
                    storyboard.Children.Add(xAnimation);
                    activeStoryboards.Add(storyboard);
                    storyboard.Begin();
                }

                // Create and animate floating particles
                CreateFloatingParticles();
                StartParticleAnimations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animation initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates floating particles as in the original design
        /// </summary>
        private void CreateFloatingParticles()
        {
            for (int i = 0; i < 20; i++)
            {
                var particle = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Colors.White) { Opacity = 0.3 },
                };

                Canvas.SetLeft(particle, animationRandom.Next(0, 1920));
                Canvas.SetTop(particle, animationRandom.Next(0, 1080));

                ParticlesCanvas.Children.Add(particle);
            }
        }

        /// <summary>
        /// Animates floating particles with continuous movement
        /// </summary>
        private void StartParticleAnimations()
        {
            animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            animationTimer.Tick += (s, e) =>
            {
                foreach (Ellipse particle in ParticlesCanvas.Children)
                {
                    var left = Canvas.GetLeft(particle);
                    var top = Canvas.GetTop(particle);

                    // Move particle slowly upward and slightly to the right
                    Canvas.SetTop(particle, top - 0.5);
                    Canvas.SetLeft(particle, left + 0.2);

                    // Reset particle position when it goes off screen
                    if (top < -10 || left > ActualWidth + 10)
                    {
                        Canvas.SetTop(particle, ActualHeight + 10);
                        Canvas.SetLeft(particle, animationRandom.Next(-10, (int)ActualWidth));
                    }
                }
            };

            animationTimer.Start();
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

                // Stop particle animations
                animationTimer?.Stop();
                animationTimer = null;

                // Stop animations
                foreach (var storyboard in activeStoryboards)
                {
                    storyboard.Stop();
                }
                activeStoryboards.Clear();
            }
            catch (Exception ex)
            {
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
