using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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
            public const double WideWidth = 300.0;
            public const double WideHeight = 200.0;
            public const double TallWidth = 200.0;
            public const double TallHeight = 400.0;
            public const double SquareWidth = 280.0;
            public const double SquareHeight = 280.0;
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

        /// <summary>
        /// Event fired when admin access sequence is completed
        /// </summary>
        public event EventHandler? AdminAccessRequested;

        #endregion

        #region Private Fields

        private List<TemplateInfo> allTemplates = new List<TemplateInfo>();
        private List<TemplateInfo> filteredTemplates = new List<TemplateInfo>();
        private string currentCategory = "All";
        private int currentPage = 0;
        private int totalPages = 0;
        private ProductInfo? selectedProduct;

        // Admin access tracking
        private int adminTapCount = 0;
        private DateTime lastAdminTap = DateTime.MinValue;

        // Animation resources
        private readonly List<Storyboard> activeStoryboards = new List<Storyboard>();

        // File system watcher for automatic template refresh
        private FileSystemWatcher? templateWatcher;
        private DispatcherTimer? refreshDelayTimer;

        private bool disposed = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the template selection screen
        /// </summary>
        public TemplateSelectionScreen()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            InitializeTemplateWatcher();
        }

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeAnimations();
                LoadTemplates();
            }
            catch (Exception ex)
            {
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
        /// Loads all templates from the Templates folder with seasonal prioritization
        /// </summary>
        private async void LoadTemplates()
        {
            try
            {
                allTemplates.Clear();
                var templatesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.TemplatesFolder);

                if (!Directory.Exists(templatesPath))
                {
                    Directory.CreateDirectory(templatesPath);
                    System.Diagnostics.Debug.WriteLine($"Created templates directory: {templatesPath}");

                    // Update display with empty list
                    ApplyFilter(currentCategory);
                    return;
                }

                var templateFolders = Directory.GetDirectories(templatesPath);
                var loadedTemplates = new List<TemplateInfo>();

                System.Diagnostics.Debug.WriteLine($"Loading {templateFolders.Length} template folders...");

                foreach (var folder in templateFolders)
                {
                    try
                    {
                        var template = await LoadTemplateFromFolder(folder);
                        if (template != null && IsTemplateValidForProduct(template))
                        {
                            loadedTemplates.Add(template);
                            System.Diagnostics.Debug.WriteLine($"Loaded template: {template.TemplateName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load template from {folder}: {ex.Message}");
                    }
                }

                // Apply seasonal prioritization
                allTemplates = ApplySeasonalPrioritization(loadedTemplates);

                System.Diagnostics.Debug.WriteLine($"Total templates loaded: {allTemplates.Count}");

                // Apply current filter and update display
                ApplyFilter(currentCategory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Template loading failed: {ex.Message}");
                ShowErrorMessage("Failed to load templates.");
            }
        }

        /// <summary>
        /// Loads a single template from its folder with three-size system
        /// </summary>
        private async Task<TemplateInfo?> LoadTemplateFromFolder(string folderPath)
        {
            try
            {
                var configPath = System.IO.Path.Combine(folderPath, Constants.ConfigFileName);
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Config file missing: {configPath}");
                    return null;
                }

                var configJson = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<TemplateConfig>(configJson);

                if (config == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse config: {configPath}");
                    return null;
                }

                // Find preview image
                var previewPath = FindPreviewImage(folderPath);
                if (string.IsNullOrEmpty(previewPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Preview image missing: {folderPath}");
                    return null;
                }

                // Find template image
                var templatePath = System.IO.Path.Combine(folderPath, "template.png");
                if (!File.Exists(templatePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Template image missing: {templatePath}");
                    return null;
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
            if (selectedProduct == null) return true;

            // Map product types to template categories
            return selectedProduct.Type.ToLowerInvariant() switch
            {
                "strips" => template.Config.Category?.ToLowerInvariant() == "strip",
                "4x6" => template.Config.Category?.ToLowerInvariant() == "4x6",
                "phone" => template.Config.Category?.ToLowerInvariant() == "4x6", // Phone prints use 4x6 templates
                _ => true
            };
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
                currentCategory = category;

                if (category.ToLowerInvariant() == "all")
                {
                    filteredTemplates = new List<TemplateInfo>(allTemplates);
                }
                else
                {
                    filteredTemplates = allTemplates
                        .Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Reset to first page
                currentPage = 0;
                totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredTemplates.Count / Constants.TemplatesPerPage));

                UpdateCategoryButtons();
                UpdateTemplateDisplay();
                UpdatePagination();
            }
            catch (Exception ex)
            {
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
                var startIndex = currentPage * Constants.TemplatesPerPage;
                var pageTemplates = filteredTemplates
                    .Skip(startIndex)
                    .Take(Constants.TemplatesPerPage)
                    .ToList();

                TemplatesContainer.ItemsSource = pageTemplates;

                // Update navigation buttons
                PrevButton.IsEnabled = currentPage > 0;
                NextButton.IsEnabled = currentPage < totalPages - 1;
            }
            catch (Exception ex)
            {
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
                var buttons = new[] { AllButton, ClassicButton, FunButton, HolidayButton, SeasonalButton };
                var categories = new[] { "All", "Classic", "Fun", "Holiday", "Seasonal" };

                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null)
                    {
                        buttons[i].Tag = string.Equals(categories[i], currentCategory, StringComparison.OrdinalIgnoreCase)
                            ? "Selected" : null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category button update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates pagination display
        /// </summary>
        private void UpdatePagination()
        {
            try
            {
                // Update page info
                PageInfo.Text = $"Page {currentPage + 1} of {totalPages}";

                // Update pagination dots
                PaginationDots.Children.Clear();

                for (int i = 0; i < totalPages; i++)
                {
                    var dot = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Margin = new Thickness(4),
                        Fill = i == currentPage
                            ? new SolidColorBrush(Colors.White)
                            : new SolidColorBrush(Colors.White) { Opacity = 0.3 }
                    };

                    PaginationDots.Children.Add(dot);
                }
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
        /// Handles category button clicks
        /// </summary>
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Content is string category)
                {
                    ApplyFilter(category);
                }
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
                if (sender is Button button && button.Tag is TemplateInfo template)
                {
                    TemplateSelected?.Invoke(this, new TemplateSelectedEventArgs(template));
                }
            }
            catch (Exception ex)
            {
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

        /// <summary>
        /// Handles admin corner tap sequence
        /// </summary>
        private void AdminCornerTap_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var now = DateTime.Now;

                // Reset sequence if too much time has passed
                if ((now - lastAdminTap).TotalSeconds > Constants.AdminTapTimeWindow)
                {
                    adminTapCount = 0;
                }

                adminTapCount++;
                lastAdminTap = now;

                System.Diagnostics.Debug.WriteLine($"Admin tap {adminTapCount}/{Constants.AdminTapSequenceCount}");

                if (adminTapCount >= Constants.AdminTapSequenceCount)
                {
                    adminTapCount = 0;
                    AdminAccessRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin tap error: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animation initialization failed: {ex.Message}");
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