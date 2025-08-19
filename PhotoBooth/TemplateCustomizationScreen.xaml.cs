using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Template customization screen - allows users to customize templates with icons, filters, and text overlays
    /// </summary>
    public partial class TemplateCustomizationScreen : UserControl, IDisposable, INotifyPropertyChanged
    {
        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly MainWindow? _mainWindow; // Reference to MainWindow for operation mode check
        private Template? _currentTemplate;
        private TemplateCategory? _currentCategory;
        private ProductInfo? _currentProduct;
        private readonly List<string> _selectedCustomizations;
        private bool _disposed = false;
        
        // Animation fields
        private DispatcherTimer? animationTimer;
        private Random random = new Random();

        // Data binding properties
        private string _templateDimensionsText = "Dimensions: Loading...";

        // Credits
        private decimal _currentCredits = 0;

        #endregion

        #region Public Properties

        /// <summary>
        /// Template dimensions text for data binding
        /// </summary>
        public string TemplateDimensionsText
        {
            get => _templateDimensionsText;
            set
            {
                if (_templateDimensionsText != value)
                {
                    _templateDimensionsText = value;
                    OnPropertyChanged(nameof(TemplateDimensionsText));
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when the back button is clicked
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// Event fired when the user is ready to continue to photo capture
        /// </summary>
        #pragma warning disable CS0067 // The event is never used but is part of the interface contract
        public event EventHandler<TemplateCustomizedEventArgs>? TemplateSelected;
        #pragma warning restore CS0067

        /// <summary>
        /// Event fired when the user wants to start the photo capture session
        /// </summary>
        public event EventHandler<PhotoSessionStartEventArgs>? PhotoSessionStartRequested;

        /// <summary>
        /// Property changed event for data binding
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor for dependency injection
        /// </summary>
        public TemplateCustomizationScreen(IDatabaseService databaseService, MainWindow? mainWindow = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _mainWindow = mainWindow;
            _selectedCustomizations = new List<string>();
            
            InitializeComponent();
            InitializeAnimations();
            
            // Set the data context for binding
            DataContext = this;
            
            // Initialize credits display
            RefreshCreditsFromDatabase();
        }

        /// <summary>
        /// Constructor for design-time support
        /// </summary>
        public TemplateCustomizationScreen() : this(new DatabaseService())
        {
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Sets up all visual animations to match the original design
        /// </summary>
        private void InitializeAnimations()
        {
            CreateFloatingParticles();
            StartFloatingOrbAnimations();
            StartParticleAnimations();
        }

        /// <summary>
        /// Creates floating animations for the background orbs
        /// animate-float-slow, animate-float-medium, animate-float-fast
        /// </summary>
        private void StartFloatingOrbAnimations()
        {
            foreach (Ellipse orb in FloatingOrbsCanvas.Children)
            {
                var translateTransform = new TranslateTransform();
                orb.RenderTransform = translateTransform;

                var storyboard = new Storyboard();

                // Vertical floating
                var yAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = random.Next(-20, -5),
                    Duration = TimeSpan.FromSeconds(3 + random.NextDouble() * 2),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                // Horizontal floating
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
                storyboard.Begin();
            }
        }

        /// <summary>
        /// Creates floating particles as in the original design
        /// Array.from({ length: 20 }) floating particles
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

                Canvas.SetLeft(particle, random.Next(0, 1920));
                Canvas.SetTop(particle, random.Next(0, 1080));

                ParticlesCanvas.Children.Add(particle);
            }
        }

        /// <summary>
        /// Animates floating particles with continuous movement
        /// animate-float-particle
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
                        Canvas.SetLeft(particle, random.Next(-10, (int)ActualWidth));
                    }
                }
            };

            animationTimer.Start();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the category and automatically load the first template
        /// </summary>
        public async Task SetCategoryAsync(TemplateCategory category, ProductInfo? product = null)
        {
            _currentCategory = category;
            _currentProduct = product;
            
            // Load the first template from this category
            await LoadFirstTemplateFromCategoryAsync();
        }

        /// <summary>
        /// Set a specific template to customize
        /// </summary>
        public void SetTemplate(Template template, ProductInfo? product = null)
        {
            _currentTemplate = template;
            _currentProduct = product;
            
            // Reset dimensions text while loading
            TemplateDimensionsText = "Dimensions: Loading...";
            
            LoadTemplatePreview();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Load the first template from the current category
        /// </summary>
        private async Task LoadFirstTemplateFromCategoryAsync()
        {
            if (_currentCategory == null) return;

            try
            {
                if (LoadingPanel != null)
                    LoadingPanel.Visibility = Visibility.Visible;

                var templatesResult = await _databaseService.GetTemplatesByCategoryAsync(_currentCategory.Id);
                
                if (templatesResult.Success && templatesResult.Data != null && templatesResult.Data.Any())
                {
                    // Filter templates that match the current product
                    var matchingTemplates = templatesResult.Data
                        .Where(t => DoesTemplateMatchProduct(t, _currentProduct))
                        .ToList();

                    if (matchingTemplates.Any())
                    {
                        _currentTemplate = matchingTemplates.First();
                        LoadTemplatePreview();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading template from category: {ex.Message}");
            }
            finally
            {
                if (LoadingPanel != null)
                    LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Load the template preview image and information
        /// </summary>
        private void LoadTemplatePreview()
        {
            if (_currentTemplate == null) return;

            // Load template image
            if (TemplatePreviewImage != null)
            {
                if (!string.IsNullOrEmpty(_currentTemplate.PreviewPath) && 
                    System.IO.File.Exists(_currentTemplate.PreviewPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_currentTemplate.PreviewPath, UriKind.Absolute);
                    bitmap.EndInit();
                    TemplatePreviewImage.Source = bitmap;

                    // Scale up templates with width below 2000 pixels
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var viewbox = this.FindName("TemplatePreviewContainer") as Viewbox;
                        if (viewbox != null && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                        {
                            LoggingService.Application.Debug("Original template dimensions",
                                ("PixelWidth", bitmap.PixelWidth),
                                ("PixelHeight", bitmap.PixelHeight));

                            // Define maximum preview dimensions
                            const int maxWidth = 800;
                            const int maxHeight = 600;

                            // Calculate scale factor for large templates to fit in preview while maintaining high quality
                            double scaleFactor = Math.Min((double)maxWidth / bitmap.PixelWidth, (double)maxHeight / bitmap.PixelHeight);
                            
                            // Only scale down templates that are too large
                            if (scaleFactor < 1.0)
                            {
                                int newWidth = (int)(bitmap.PixelWidth * scaleFactor);
                                int newHeight = (int)(bitmap.PixelHeight * scaleFactor);
                                
                                LoggingService.Application.Debug("Scaling down template to fit preview",
                                    ("ScaleFactor", scaleFactor),
                                    ("NewWidth", newWidth),
                                    ("NewHeight", newHeight));

                                TemplatePreviewImage.Width = newWidth;
                                TemplatePreviewImage.Height = newHeight;
                            }
                            else
                            {
                                // For smaller templates, use original size for crisp display
                                LoggingService.Application.Debug("Using original template size for preview");
                                TemplatePreviewImage.Width = bitmap.PixelWidth;
                                TemplatePreviewImage.Height = bitmap.PixelHeight;
                            }

                            // Update template dimensions display via data binding
                            TemplateDimensionsText = $"Dimensions: {bitmap.PixelWidth}x{bitmap.PixelHeight}";
                        }
                    }));
                }
                else
                {
                    // Show placeholder if no preview available
                    TemplatePreviewImage.Source = null;
                    TemplateDimensionsText = "Dimensions: Unknown";
                }
            }

            // Update template information
            if (TemplateNameText != null)
                TemplateNameText.Text = _currentTemplate.Name ?? "Unnamed Template";
            
            if (TemplateDescriptionText != null)
                TemplateDescriptionText.Text = _currentTemplate.Description ?? "Customize this template with icons, filters, and text overlays.";
        }

        /// <summary>
        /// Check if a template matches the current product type
        /// </summary>
        private bool DoesTemplateMatchProduct(Template template, ProductInfo? product)
        {
            if (product == null || template.Layout == null) return true;

            var productType = product.Type?.ToLowerInvariant();
            var templateCategory = template.Layout.ProductCategory?.Name?.ToLowerInvariant();

            return productType switch
            {
                "strips" or "photostrips" => templateCategory == "strips" || templateCategory == "photo strips",
                "4x6" or "photo4x6" => templateCategory == "4x6" || templateCategory == "photos",
                "phone" or "smartphoneprint" => templateCategory == "4x6" || templateCategory == "photos",
                _ => true
            };
        }

        /// <summary>
        /// Handle continue button click - proceed to photo capture
        /// </summary>
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplate == null) return;

            // Create simplified session args
            var sessionArgs = new PhotoSessionStartEventArgs(
                _currentTemplate, 
                _currentProduct, 
                new List<string>(),  // No customizations in simplified version
                1,     // Default 1 photo
                5,     // Default 5 second timer
                true   // Flash enabled by default
            );

            PhotoSessionStartRequested?.Invoke(this, sessionArgs);
        }

        /// <summary>
        /// Handle back button click
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refresh credits from database
        /// </summary>
        private async void RefreshCreditsFromDatabase()
        {
            try
            {
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
                System.Diagnostics.Debug.WriteLine($"Error refreshing credits from database: {ex.Message}");
                _currentCredits = 0;
                UpdateCreditsDisplay();
            }
        }

        /// <summary>
        /// Updates the credits display with validation
        /// </summary>
        /// <param name="credits">Current credit amount</param>
        public void UpdateCredits(decimal credits)
        {
            if (credits < 0)
            {
                credits = 0;
            }

            _currentCredits = credits;
            UpdateCreditsDisplay();
        }

        /// <summary>
        /// Update credits display
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
                        displayText = $"Credits: ${_currentCredits:F2}";
                    }
                    CreditsDisplay.Text = displayText;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update credits display: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Clean up animations
                animationTimer?.Stop();
                animationTimer = null;
                
                // Clean up managed resources if needed
                _disposed = true;
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for template customization completion
    /// </summary>
    public class TemplateCustomizedEventArgs : EventArgs
    {
        public Template Template { get; }
        public ProductInfo? Product { get; }
        public List<string> Customizations { get; }

        public TemplateCustomizedEventArgs(Template template, ProductInfo? product, List<string> customizations)
        {
            Template = template;
            Product = product;
            Customizations = customizations;
        }
    }

    /// <summary>
    /// Event arguments for photo session start request
    /// </summary>
    public class PhotoSessionStartEventArgs : EventArgs
    {
        public Template Template { get; }
        public ProductInfo? Product { get; }
        public List<string> Customizations { get; }
        public int PhotoCount { get; }
        public int TimerSeconds { get; }
        public bool FlashEnabled { get; }

        public PhotoSessionStartEventArgs(Template template, ProductInfo? product, List<string> customizations, 
                                         int photoCount, int timerSeconds, bool flashEnabled)
        {
            Template = template;
            Product = product;
            Customizations = customizations;
            PhotoCount = photoCount;
            TimerSeconds = timerSeconds;
            FlashEnabled = flashEnabled;
        }
    }
} 