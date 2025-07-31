using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Photobooth.Services;
using Photobooth.Models;

namespace Photobooth
{
    /// <summary>
    /// Handles resource management, error recovery, and hardware compatibility
    /// </summary>
    public partial class ProductSelectionScreen : UserControl, IDisposable
    {
        #region Constants

        private static class AnimationConstants
        {
            public const int ParticleCount = 20;
            public const int ParticleSize = 8;
            public const double ParticleOpacity = 0.3;
            public const double ParticleSpeed = 0.3;
            public const int TimerInterval = 50;
            public const int DefaultScreenWidth = 1920;
            public const int DefaultScreenHeight = 1080;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when user wants to go back to welcome screen
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// Event fired when user selects a product type
        /// </summary>
        public event EventHandler<ProductSelectedEventArgs>? ProductSelected;

        #endregion

        #region Private Fields

        private decimal currentCredits = 0;
        private DispatcherTimer? animationTimer;
        private readonly Random random = new Random();
        private readonly List<Ellipse> particles = new List<Ellipse>();
        private readonly List<Storyboard> activeStoryboards = new List<Storyboard>();
        private readonly IDatabaseService _databaseService;
        private bool disposed = false;
        
        // Database-loaded products
        private List<Product> _products = new List<Product>();

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the product selection screen
        /// </summary>
        public ProductSelectionScreen(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            InitializeComponent();
            this.Loaded += OnLoaded;
            _ = RefreshCreditsFromDatabase();
        }

        /// <summary>
        /// Default constructor for design-time support
        /// </summary>
        public ProductSelectionScreen() : this(new DatabaseService())
        {
        }

        /// <summary>
        /// Handles the Loaded event to ensure proper sizing before animations
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("ProductSelectionScreen.OnLoaded called");
                InitializeAnimations();
                _ = RefreshCreditsFromDatabase();
                
                // Load products from database on UI thread to ensure proper UI updates
                _ = Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        LoggingService.Application.Information("Loading products from database on startup");
                        await LoadProductsFromDatabase();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Failed to load products on startup", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Animation initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load product pricing from database and update UI
        /// </summary>
        private async Task LoadProductsFromDatabase()
        {
            try
            {
                LoggingService.Application.Information("LoadProductsFromDatabase called");
                var result = await _databaseService.GetProductsAsync();
                if (result.Success && result.Data != null)
                {
                    _products = result.Data;
                    LoggingService.Application.Information("Products loaded from database", 
                        ("Count", _products.Count),
                        ("PhotoStrips", _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips)?.Price ?? 0),
                        ("Photo4x6", _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6)?.Price ?? 0),
                        ("SmartphonePrint", _products.FirstOrDefault(p => p.ProductType == ProductType.SmartphonePrint)?.Price ?? 0));
                    
                    UpdateProductPricesInUI();
                    UpdateProductConfiguration();
                }
                else
                {
                    LoggingService.Application.Warning("Failed to load products from database, using default prices",
                        ("Error", result.ErrorMessage ?? "Unknown error"));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error loading products from database, using default prices", ex);
            }
        }

        /// <summary>
        /// Refresh product prices from database and update UI
        /// This method can be called externally to refresh prices after admin changes
        /// </summary>
        public async Task RefreshProductPricesAsync()
        {
            try
            {
                LoggingService.Application.Information("Refreshing product prices from database");
                await LoadProductsFromDatabase();
                
                // Add a small delay to ensure the UI is fully rendered before updating prices
                await Task.Delay(100);
                // Note: UpdateProductPricesInUI() is already called by LoadProductsFromDatabase()
                // No need to call it again here
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error refreshing product prices", ex);
            }
        }

        /// <summary>
        /// Update the UI price displays with database values
        /// </summary>
        private void UpdateProductPricesInUI()
        {
            try
            {
                var photoStrips = _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips);
                var photo4x6 = _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6);
                var smartphonePrint = _products.FirstOrDefault(p => p.ProductType == ProductType.SmartphonePrint);

                if (photoStrips != null && PhotoStripsPriceText != null)
                {
                    PhotoStripsPriceText.Text = $"${photoStrips.Price:F0}";
                    LoggingService.Application.Information("Updated Photo Strips price in UI", ("Price", photoStrips.Price));
                }

                if (photo4x6 != null && Photo4x6PriceText != null)
                {
                    Photo4x6PriceText.Text = $"${photo4x6.Price:F0}";
                    LoggingService.Application.Information("Updated 4x6 Photos price in UI", ("Price", photo4x6.Price));
                }

                // Update smartphone print price in the button text
                if (smartphonePrint != null && PhonePrintButton != null)
                {
                    try
                    {
                        LoggingService.Application.Information("Attempting to update smartphone print price", ("Price", smartphonePrint.Price));
                        
                        // Force the button to update its visual tree first
                        PhonePrintButton.UpdateLayout();
                        
                        // Find the TextBlock within the button's visual tree
                        var textBlock = FindVisualChild<TextBlock>(PhonePrintButton, "PhonePrintPriceText");
                        if (textBlock != null)
                        {
                            textBlock.Text = $"Print from Phone • ${smartphonePrint.Price:F0}";
                            LoggingService.Application.Information("Successfully updated Smartphone Print price in UI", ("Price", smartphonePrint.Price));
                        }
                        else
                        {
                            LoggingService.Application.Warning("Could not find PhonePrintPriceText TextBlock in button visual tree");
                            
                            // Try to find any TextBlock in the button
                            var anyTextBlock = FindVisualChild<TextBlock>(PhonePrintButton, null);
                            if (anyTextBlock != null)
                            {
                                LoggingService.Application.Information("Found TextBlock in button", ("Name", anyTextBlock.Name), ("Text", anyTextBlock.Text));
                                
                                // Try to update this TextBlock if it contains the price
                                if (anyTextBlock.Text.Contains("Print from Phone"))
                                {
                                    anyTextBlock.Text = $"Print from Phone • ${smartphonePrint.Price:F0}";
                                }
                            }
                            else
                            {
                                LoggingService.Application.Warning("No TextBlock found in button visual tree at all");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Error updating smartphone print price", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating product prices in UI", ex);
            }
        }

        /// <summary>
        /// Update the ProductConfiguration with database values
        /// </summary>
        private void UpdateProductConfiguration()
        {
            try
            {
                var photoStrips = _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips);
                var photo4x6 = _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6);
                var smartphonePrint = _products.FirstOrDefault(p => p.ProductType == ProductType.SmartphonePrint);

                LoggingService.Application.Information("Updating ProductConfiguration with database values",
                    ("PhotoStrips", photoStrips?.Price ?? 0),
                    ("Photo4x6", photo4x6?.Price ?? 0),
                    ("SmartphonePrint", smartphonePrint?.Price ?? 0));

                if (photoStrips != null && ProductConfiguration.Products.ContainsKey("strips"))
                {
                    ProductConfiguration.Products["strips"].Price = photoStrips.Price;
                    LoggingService.Application.Information("Updated ProductConfiguration.strips.Price", ("Price", photoStrips.Price));
                }

                if (photo4x6 != null && ProductConfiguration.Products.ContainsKey("4x6"))
                {
                    ProductConfiguration.Products["4x6"].Price = photo4x6.Price;
                    LoggingService.Application.Information("Updated ProductConfiguration.4x6.Price", ("Price", photo4x6.Price));
                }

                if (smartphonePrint != null && ProductConfiguration.Products.ContainsKey("phone"))
                {
                    ProductConfiguration.Products["phone"].Price = smartphonePrint.Price;
                    LoggingService.Application.Information("Updated ProductConfiguration.phone.Price", ("Price", smartphonePrint.Price));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error updating product configuration", ex);
            }
        }

        /// <summary>
        /// Sets up all visual animations with error handling
        /// </summary>
        private void InitializeAnimations()
        {
            try
            {
                StartFloatingOrbAnimations();
                CreateFloatingParticles();
                StartParticleAnimations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize animations: {ex.Message}");
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Creates floating animations for the background orbs with proper resource tracking
        /// </summary>
        private void StartFloatingOrbAnimations()
        {
            foreach (Ellipse orb in FloatingOrbsCanvas.Children)
            {
                try
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

                    // Track storyboard for cleanup
                    activeStoryboards.Add(storyboard);
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to animate orb: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates floating particles with responsive positioning
        /// </summary>
        private void CreateFloatingParticles()
        {
            try
            {
                // Use actual screen dimensions or fall back to defaults
                var canvasWidth = Math.Max(ActualWidth > 0 ? ActualWidth : AnimationConstants.DefaultScreenWidth, AnimationConstants.DefaultScreenWidth);
                var canvasHeight = Math.Max(ActualHeight > 0 ? ActualHeight : AnimationConstants.DefaultScreenHeight, AnimationConstants.DefaultScreenHeight);

                for (int i = 0; i < AnimationConstants.ParticleCount; i++)
                {
                    var particle = new Ellipse
                    {
                        Width = AnimationConstants.ParticleSize,
                        Height = AnimationConstants.ParticleSize,
                        Fill = new SolidColorBrush(Colors.White) { Opacity = AnimationConstants.ParticleOpacity },
                    };

                    Canvas.SetLeft(particle, random.Next(0, (int)canvasWidth));
                    Canvas.SetTop(particle, random.Next(0, (int)canvasHeight));

                    particles.Add(particle);
                    ParticlesCanvas.Children.Add(particle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create particles: {ex.Message}");
            }
        }

        /// <summary>
        /// Animates floating particles with continuous movement and error handling
        /// </summary>
        private void StartParticleAnimations()
        {
            try
            {
                animationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(AnimationConstants.TimerInterval)
                };

                animationTimer.Tick += (s, e) =>
                {
                    try
                    {
                        var canvasHeight = Math.Max(ActualHeight > 0 ? ActualHeight : AnimationConstants.DefaultScreenHeight, AnimationConstants.DefaultScreenHeight);
                        var canvasWidth = Math.Max(ActualWidth > 0 ? ActualWidth : AnimationConstants.DefaultScreenWidth, AnimationConstants.DefaultScreenWidth);

                        foreach (var particle in particles)
                        {
                            var currentTop = Canvas.GetTop(particle);
                            var newTop = currentTop - AnimationConstants.ParticleSpeed;

                            if (newTop < -particle.Height)
                            {
                                newTop = canvasHeight + particle.Height;
                                Canvas.SetLeft(particle, random.Next(0, (int)canvasWidth));
                            }

                            Canvas.SetTop(particle, newTop);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Particle animation error: {ex.Message}");
                    }
                };

                animationTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start particle animations: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refresh credits directly from database
        /// </summary>
        private async Task RefreshCreditsFromDatabase()
        {
            try
            {
                var creditsResult = await _databaseService.GetSettingValueAsync<decimal>("System", "CurrentCredits");
                if (creditsResult.Success)
                {
                    currentCredits = creditsResult.Data;
                }
                else
                {
                    currentCredits = 0;
                }
                UpdateCreditsDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing credits from database: {ex.Message}");
                currentCredits = 0;
                UpdateCreditsDisplay();
            }
        }

        /// <summary>
        /// Updates the credits display with validation
        /// </summary>
        /// <param name="credits">Current credit amount</param>
        public void UpdateCredits(decimal credits)
        {
            Console.WriteLine($"=== ProductSelectionScreen.UpdateCredits DEBUG ===");
            Console.WriteLine($"Received credits value: ${credits}");
            Console.WriteLine($"Current cached credits: ${currentCredits}");
            
            if (credits < 0)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Negative credits value provided: {credits}");
                credits = 0;
            }

            currentCredits = credits;
            Console.WriteLine($"Set currentCredits to: ${currentCredits}");
            Console.WriteLine("Calling UpdateCreditsDisplay...");
            UpdateCreditsDisplay();
            Console.WriteLine("=== ProductSelectionScreen.UpdateCredits END ===");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the credits display text safely
        /// </summary>
        private void UpdateCreditsDisplay()
        {
            try
            {
                Console.WriteLine($"=== ProductSelectionScreen.UpdateCreditsDisplay DEBUG ===");
                Console.WriteLine($"CreditsDisplay is null: {CreditsDisplay == null}");
                Console.WriteLine($"currentCredits value: ${currentCredits}");
                
                if (CreditsDisplay != null)
                {
                    var displayText = $"Credits: ${currentCredits:F0}";
                    CreditsDisplay.Text = displayText;
                    Console.WriteLine($"Set CreditsDisplay.Text to: '{displayText}'");
                }
                else
                {
                    Console.WriteLine("CreditsDisplay is null - cannot update display");
                }
                Console.WriteLine($"=== ProductSelectionScreen.UpdateCreditsDisplay END ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in UpdateCreditsDisplay: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to update credits display: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles back button click with error handling
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BackButtonClicked?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Back button click error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles product card selection with validation
        /// </summary>
        private void ProductCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string productType)
                {
                    Console.WriteLine($"=== PRODUCT SELECTION DEBUG ===");
                    Console.WriteLine($"Product selected: {productType}");
                    
                    var productInfo = GetProductInfo(productType);
                    if (productInfo != null)
                    {
                        Console.WriteLine($"Product info - Name: {productInfo.Name}, Type: {productInfo.Type}, Price: ${productInfo.Price:F2}");
                        Console.WriteLine($"Invoking ProductSelected event...");
                        ProductSelected?.Invoke(this, new ProductSelectedEventArgs(productInfo));
                        Console.WriteLine($"=== PRODUCT SELECTION COMPLETE ===");
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Unknown product type selected: {productType}");
                        System.Diagnostics.Debug.WriteLine($"Unknown product type selected: {productType}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Product selection error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Product selection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles phone print button click
        /// </summary>
        private void PhonePrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productInfo = ProductConfiguration.Products["phone"];
                ProductSelected?.Invoke(this, new ProductSelectedEventArgs(productInfo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Phone print selection error: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets product information based on product type with error handling
        /// </summary>
        private ProductInfo? GetProductInfo(string productType)
        {
            try
            {
                if (ProductConfiguration.Products.TryGetValue(productType, out var product))
                {
                    return product;
                }

                System.Diagnostics.Debug.WriteLine($"Unknown product type requested: {productType}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving product info: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Resource Management & Disposal

        /// <summary>
        /// Cleanup animations and resources - Enhanced for commercial use
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Stop and dispose timer
                animationTimer?.Stop();
                animationTimer = null;

                // Stop all storyboard animations
                foreach (var storyboard in activeStoryboards)
                {
                    try
                    {
                        storyboard.Stop();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping storyboard: {ex.Message}");
                    }
                }
                activeStoryboards.Clear();

                // Clear particle collections
                particles.Clear();
                ParticlesCanvas?.Children.Clear();

                // Clear orb animations
                if (FloatingOrbsCanvas != null)
                {
                    foreach (Ellipse orb in FloatingOrbsCanvas.Children)
                    {
                        try
                        {
                            orb.BeginAnimation(UIElement.RenderTransformProperty, null);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error clearing orb animation: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// IDisposable implementation for proper resource management
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

        #region Helper Methods

        /// <summary>
        /// Find a visual child of a specific type and name in the visual tree
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element)
                {
                    // If name is null, return the first element of type T
                    // If name is provided, only return if it matches
                    if (name == null || element.Name == name)
                    {
                        return element;
                    }
                }
                
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }



        #endregion
    }

    #region Configuration and Data Classes

    /// <summary>
    /// Configuration-driven product definitions for easier maintenance
    /// </summary>
    public static class ProductConfiguration
    {
        public static readonly Dictionary<string, ProductInfo> Products = new Dictionary<string, ProductInfo>
        {
            ["strips"] = new ProductInfo
            {
                Type = "strips",
                Name = "Photo Strips",
                Description = "Classic 4-photo strip",
                Price = 5.00m
            },
            ["4x6"] = new ProductInfo
            {
                Type = "4x6",
                Name = "4x6 Photos",
                Description = "High-quality print",
                Price = 3.00m
            },
            ["phone"] = new ProductInfo
            {
                Type = "phone",
                Name = "Print from Phone",
                Description = "Print photos from your phone",
                Price = 2.00m
            }
        };
    }

    /// <summary>
    /// Event arguments for product selection
    /// </summary>
    public class ProductSelectedEventArgs : EventArgs
    {
        public ProductInfo ProductInfo { get; }

        public ProductSelectedEventArgs(ProductInfo productInfo)
        {
            ProductInfo = productInfo ?? throw new ArgumentNullException(nameof(productInfo));
        }
    }

    /// <summary>
    /// Product information data class
    /// </summary>
    public class ProductInfo
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
    }

    #endregion
}
