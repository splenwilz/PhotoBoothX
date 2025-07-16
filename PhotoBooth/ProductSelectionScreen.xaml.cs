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
            RefreshCreditsFromDatabase();
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
                InitializeAnimations();
                RefreshCreditsFromDatabase();
                _ = LoadProductsFromDatabase(); // Load prices from database
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
                var result = await _databaseService.GetProductsAsync();
                if (result.Success && result.Data != null)
                {
                    _products = result.Data;
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
        /// Update the UI price displays with database values
        /// </summary>
        private void UpdateProductPricesInUI()
        {
            try
            {
                var photoStrips = _products.FirstOrDefault(p => p.ProductType == ProductType.PhotoStrips);
                var photo4x6 = _products.FirstOrDefault(p => p.ProductType == ProductType.Photo4x6);

                if (photoStrips != null && PhotoStripsPriceText != null)
                {
                    PhotoStripsPriceText.Text = $"${photoStrips.Price:F0}";
                }

                if (photo4x6 != null && Photo4x6PriceText != null)
                {
                    Photo4x6PriceText.Text = $"${photo4x6.Price:F0}";
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

                if (photoStrips != null && ProductConfiguration.Products.ContainsKey("strips"))
                {
                    ProductConfiguration.Products["strips"].Price = photoStrips.Price;
                }

                if (photo4x6 != null && ProductConfiguration.Products.ContainsKey("4x6"))
                {
                    ProductConfiguration.Products["4x6"].Price = photo4x6.Price;
                }

                if (smartphonePrint != null && ProductConfiguration.Products.ContainsKey("phone"))
                {
                    ProductConfiguration.Products["phone"].Price = smartphonePrint.Price;
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
        private async void RefreshCreditsFromDatabase()
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
