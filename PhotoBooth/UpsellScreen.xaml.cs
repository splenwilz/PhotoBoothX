using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    /// Upselling screen for extra copies and cross-selling
    /// Handles business logic per contract: extra copies, cross-selling, timeout
    /// </summary>
    public partial class UpsellScreen : UserControl, IDisposable
    {
        #region Events

        /// <summary>
        /// Event fired when upselling is complete and ready to proceed to printing
        /// </summary>
        public event EventHandler<UpsellCompletedEventArgs>? UpsellCompleted;

        /// <summary>
        /// Event fired when timeout occurs and should proceed to printing
        /// </summary>
        public event EventHandler? UpsellTimeout;

        #endregion

        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private Template? _currentTemplate;
        private ProductInfo? _originalProduct;
        private string? _composedImagePath;
        private List<string>? _capturedPhotosPaths;
        
        // Upselling state
        private UpsellStage _currentStage = UpsellStage.ExtraCopies;
        private int _selectedExtraCopies = 0;
        private bool _crossSellAccepted = false;
        private ProductInfo? _crossSellProduct;
        private decimal _originalPrice = 0;
        private decimal _extraCopiesPrice = 0;
        private decimal _crossSellPrice = 0;
        private int _currentQuantity = 3; // For 3+ copies adjustment

        // Timeout and animation
        private DispatcherTimer? _timeoutTimer;
        private DispatcherTimer? _progressTimer;
        private DispatcherTimer? _animationTimer;
        private const double TIMEOUT_SECONDS = 180.0; // 3 minutes per stage
        private double _currentTimeoutProgress = 0;
        
        // Animation
        private readonly Random _random = new Random();
        private readonly List<Ellipse> _particles = new List<Ellipse>();
        private bool _disposed = false;
        
        // Credits
        private decimal _currentCredits = 0;

        // Photo selection for cross-sell
        private int _selectedPhotoIndex = 0;
        private List<string> _capturedPhotos = new List<string>();
        private string? _selectedPhotoForCrossSell = null;

        // Pricing configuration (loaded from database)
        private const decimal STRIPS_PRICE = 5.00m;
        private const decimal PHOTO_4X6_PRICE = 3.00m;
        
        // Extra copy pricing (configurable via admin dashboard)
        private bool _useCustomExtraCopyPricing = false;
        private decimal _extraCopyPrice1 = 3.00m;
        private decimal _extraCopyPrice2 = 5.00m;
        private decimal _extraCopyPriceAdditional = 1.50m;

        #endregion

        #region Constructor

        public UpsellScreen(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            InitializeComponent();
            this.Loaded += OnLoaded;
            InitializeAnimations();
            
            // Initialize credits display
            _ = RefreshCreditsFromDatabase(); // Fire and forget
        }

        // Parameterless constructor removed to enforce proper dependency injection
        // Use UpsellScreen(IDatabaseService databaseService) instead

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the upsell screen with session data
        /// </summary>
        public async Task InitializeAsync(Template template, ProductInfo originalProduct, string composedImagePath, List<string> capturedPhotosPaths)
        {
            try
            {
                Console.WriteLine($"=== UPSELL SCREEN INITIALIZATION DEBUG ===");
                Console.WriteLine($"Template: {template.Name ?? "Unknown"}");
                Console.WriteLine($"Original Product - Name: {originalProduct.Name}, Type: {originalProduct.Type}, Price: ${originalProduct.Price:F2}");
                
                _currentTemplate = template;
                _originalProduct = originalProduct;
                _composedImagePath = composedImagePath;
                _capturedPhotosPaths = new List<string>(capturedPhotosPaths);
                _originalPrice = originalProduct.Price;

                Console.WriteLine($"Set _originalPrice to: ${_originalPrice:F2}");

                LoggingService.Application.Information("Upsell screen initialized",
                    ("OriginalProduct", originalProduct.Type),
                    ("OriginalPrice", originalProduct.Price),
                    ("TemplateName", template.Name ?? "Unknown"));

                // Load extra copy pricing from database
                Console.WriteLine($"Loading extra copy pricing from database...");
                await LoadExtraCopyPricingFromDatabase();

                // Update pricing displays
                Console.WriteLine($"Updating pricing displays...");
                UpdatePricingDisplays();

                // Start with extra copies stage
                await StartExtraCopiesStage();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell screen initialization failed", ex);
                throw;
            }
        }

        #endregion

        #region Database Operations

        /// <summary>
        /// Load extra copy pricing configuration from database
        /// </summary>
        private async Task LoadExtraCopyPricingFromDatabase()
        {
            try
            {
                // Get the selected product's pricing configuration from database
                var productsResult = await _databaseService.GetAllAsync<Product>();
                
                if (productsResult.Success && productsResult.Data?.Count > 0)
                {
                    // Find the product that matches our selected product type, or use first as fallback
                    var product = productsResult.Data.FirstOrDefault(p => 
                        _originalProduct != null && 
                        GetProductTypeFromName(_originalProduct.Type) == p.ProductType) 
                        ?? productsResult.Data[0];
                    
                    _useCustomExtraCopyPricing = product.UseCustomExtraCopyPricing;
                    
                    // Use the selected product's price as the base price for calculations
                    var basePrice = _originalProduct?.Price ?? product.Price;
                    
                    Console.WriteLine($"=== PRICING CALCULATION DEBUG ===");
                    Console.WriteLine($"Database product found - Type: {product.ProductType}, Price: ${product.Price:F2}");
                    Console.WriteLine($"UseCustomExtraCopyPricing: {_useCustomExtraCopyPricing}");
                    Console.WriteLine($"Base price for calculations: ${basePrice:F2}");
                    
                    if (_useCustomExtraCopyPricing)
                    {
                        // Use product-specific pricing if available, otherwise fall back to legacy pricing
                        var productType = GetProductTypeFromName(_originalProduct?.Type);
                        
                        switch (productType)
                        {
                            case ProductType.PhotoStrips:
                                var stripsPrice = product.StripsExtraCopyPrice ?? product.ExtraCopy1Price ?? basePrice;
                                var stripsDiscount = product.StripsMultipleCopyDiscount ?? 0;
                                _extraCopyPrice1 = stripsPrice;
                                _extraCopyPrice2 = (stripsPrice * 2) * (1 - stripsDiscount / 100);
                                _extraCopyPriceAdditional = stripsPrice * (1 - stripsDiscount / 100);
                                Console.WriteLine($"PHOTO STRIPS SIMPLIFIED PRICING: Price=${stripsPrice:F2}, Discount={stripsDiscount:F0}%");
                                break;
                                
                            case ProductType.Photo4x6:
                                var photo4x6Price = product.Photo4x6ExtraCopyPrice ?? product.ExtraCopy1Price ?? basePrice;
                                var photo4x6Discount = product.Photo4x6MultipleCopyDiscount ?? 0;
                                _extraCopyPrice1 = photo4x6Price;
                                _extraCopyPrice2 = (photo4x6Price * 2) * (1 - photo4x6Discount / 100);
                                _extraCopyPriceAdditional = photo4x6Price * (1 - photo4x6Discount / 100);
                                Console.WriteLine($"4X6 PHOTOS SIMPLIFIED PRICING: Price=${photo4x6Price:F2}, Discount={photo4x6Discount:F0}%");
                                break;
                                
                            case ProductType.SmartphonePrint:
                                var smartphonePrice = product.SmartphoneExtraCopyPrice ?? product.ExtraCopy1Price ?? basePrice;
                                var smartphoneDiscount = product.SmartphoneMultipleCopyDiscount ?? 0;
                                _extraCopyPrice1 = smartphonePrice;
                                _extraCopyPrice2 = (smartphonePrice * 2) * (1 - smartphoneDiscount / 100);
                                _extraCopyPriceAdditional = smartphonePrice * (1 - smartphoneDiscount / 100);
                                Console.WriteLine($"SMARTPHONE PRINT SIMPLIFIED PRICING: Price=${smartphonePrice:F2}, Discount={smartphoneDiscount:F0}%");
                                break;
                                
                            default:
                                _extraCopyPrice1 = product.ExtraCopy1Price ?? basePrice;
                                _extraCopyPrice2 = product.ExtraCopy2Price ?? (basePrice * 2);
                                _extraCopyPriceAdditional = product.ExtraCopyAdditionalPrice ?? basePrice;
                                Console.WriteLine($"LEGACY CUSTOM PRICING:");
                                break;
                        }
                        
                        Console.WriteLine($"- ExtraCopy1Price: ${_extraCopyPrice1:F2}");
                        Console.WriteLine($"- ExtraCopy2Price: ${_extraCopyPrice2:F2}");
                        Console.WriteLine($"- ExtraCopyAdditionalPrice: ${_extraCopyPriceAdditional:F2}");
                    }
                    else
                    {
                        // Use base product price for all extra copies (no discount)
                        _extraCopyPrice1 = basePrice;
                        _extraCopyPrice2 = basePrice * 2;
                        _extraCopyPriceAdditional = basePrice;
                        
                        Console.WriteLine($"BASE PRICING ENABLED (no custom pricing):");
                        Console.WriteLine($"- ExtraCopy1Price: ${_extraCopyPrice1:F2}");
                        Console.WriteLine($"- ExtraCopy2Price: ${_extraCopyPrice2:F2}");
                        Console.WriteLine($"- ExtraCopyAdditionalPrice: ${_extraCopyPriceAdditional:F2}");
                    }

                    LoggingService.Application.Information("Extra copy pricing loaded from database",
                        ("UseCustomPricing", _useCustomExtraCopyPricing),
                        ("SelectedProductType", _originalProduct?.Type ?? "Unknown"),
                        ("BasePrice", basePrice),
                        ("ExtraCopy1Price", _extraCopyPrice1),
                        ("ExtraCopy2Price", _extraCopyPrice2),
                        ("ExtraCopyAdditionalPrice", _extraCopyPriceAdditional));
                    

                }
                else
                {
                    LoggingService.Application.Warning("Failed to load extra copy pricing from database, using defaults",
                        ("Error", productsResult.ErrorMessage ?? "No products found"));
                    
                    // Use the selected product's price as fallback
                    if (_originalProduct != null)
                    {
                        _extraCopyPrice1 = _originalProduct.Price;
                        _extraCopyPrice2 = _originalProduct.Price * 2;
                        _extraCopyPriceAdditional = _originalProduct.Price;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error loading extra copy pricing from database, using defaults", ex);
                
                // Use the selected product's price as fallback
                if (_originalProduct != null)
                {
                    _extraCopyPrice1 = _originalProduct.Price;
                    _extraCopyPrice2 = _originalProduct.Price * 2;
                    _extraCopyPriceAdditional = _originalProduct.Price;
                }
            }
        }

        /// <summary>
        /// Convert product type name to ProductType enum
        /// </summary>
        private ProductType GetProductTypeFromName(string? productTypeName)
        {
            return productTypeName?.ToLower() switch
            {
                "strips" => ProductType.PhotoStrips,
                "4x6" => ProductType.Photo4x6,
                "phone" => ProductType.SmartphonePrint,
                _ => ProductType.PhotoStrips // Default fallback
            };
        }

        #endregion

        #region Stage Management

        /// <summary>
        /// Start the extra copies upselling stage
        /// </summary>
        private async Task StartExtraCopiesStage()
        {
            try
            {
                LoggingService.Application.Information("Starting extra copies upsell stage");
                
                _currentStage = UpsellStage.ExtraCopies;
                
                // Reset UI
                ExtraCopiesSection.Visibility = Visibility.Visible;
                CrossSellSection.Visibility = Visibility.Collapsed;
                ContinueButton.Visibility = Visibility.Collapsed;

                // Set titles and play audio
                TitleText.Text = "You look great!";
                SubtitleText.Text = "Would you like extra copies?";
                
                // Play audio message
                await PlayAudioMessage("you look great, do you want extra copies?");
                
                // Start timeout timer
                StartTimeoutTimer();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Extra copies stage start failed", ex);
            }
        }

        /// <summary>
        /// Start the cross-selling stage
        /// </summary>
        private async Task StartCrossSellStage()
        {
            try
            {
                LoggingService.Application.Information("Starting cross-sell upsell stage",
                    ("OriginalProduct", _originalProduct?.Type ?? "Unknown"),
                    ("ExtraCopiesSelected", _selectedExtraCopies));
                
                _currentStage = UpsellStage.CrossSell;
                
                // Hide extra copies, show cross-sell
                ExtraCopiesSection.Visibility = Visibility.Collapsed;
                CrossSellSection.Visibility = Visibility.Visible;
                ContinueButton.Visibility = Visibility.Visible;

                // Determine cross-sell product
                _crossSellProduct = GetCrossSellProduct();
                if (_crossSellProduct == null)
                {
                    // No cross-sell available, proceed to completion
                    CompleteUpselling();
                    return;
                }

                // Update cross-sell UI
                UpdateCrossSellDisplay();
                
                // Play audio for cross-sell
                var audioMessage = _originalProduct?.Type?.ToLower() == "strips" 
                    ? "Would you like to add a 4x6 photo?" 
                    : "Would you like to add photo strips?";
                await PlayAudioMessage(audioMessage);
                
                // Restart timeout timer for cross-sell stage
                StartTimeoutTimer();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Cross-sell stage start failed", ex);
                CompleteUpselling(); // Fallback to completion
            }
        }

        /// <summary>
        /// Complete the upselling process
        /// </summary>
        private async void CompleteUpselling()
        {
            try
            {
                Console.WriteLine($"=== COMPLETE UPSELLING DEBUG ===");
                Console.WriteLine($"FINAL ORDER SUMMARY:");
                Console.WriteLine($"  Original Product: {_originalProduct?.Name} (${_originalProduct?.Price:F2})");
                Console.WriteLine($"  Selected Extra Copies: {_selectedExtraCopies}");
                Console.WriteLine($"  Extra Copies Price: ${_extraCopiesPrice:F2}");
                Console.WriteLine($"  Cross-sell Accepted: {_crossSellAccepted}");
                Console.WriteLine($"  Cross-sell Price: ${_crossSellPrice:F2}");
                
                StopTimeoutTimer();

                var totalAdditionalCost = _extraCopiesPrice + (_crossSellAccepted ? _crossSellPrice : 0);
                
                // Calculate total order cost (original + additional)
                var originalPrice = _originalProduct?.Price ?? 0;
                var totalOrderCost = originalPrice + totalAdditionalCost;

                Console.WriteLine($"PRICING BREAKDOWN:");
                Console.WriteLine($"  Original Price: ${originalPrice:F2}");
                Console.WriteLine($"  Extra Copies Cost: ${_extraCopiesPrice:F2}");
                Console.WriteLine($"  Cross-sell Cost: ${(_crossSellAccepted ? _crossSellPrice : 0):F2}");
                Console.WriteLine($"  Total Additional Cost: ${totalAdditionalCost:F2}");
                Console.WriteLine($"  TOTAL ORDER COST: ${totalOrderCost:F2}");

                LoggingService.Application.Information("Upselling completed",
                    ("ExtraCopies", _selectedExtraCopies),
                    ("CrossSellAccepted", _crossSellAccepted),
                    ("ExtraCopiesPrice", _extraCopiesPrice),
                    ("CrossSellPrice", _crossSellPrice),
                    ("TotalAdditionalCost", totalAdditionalCost),
                    ("OriginalPrice", originalPrice),
                    ("TotalOrderCost", totalOrderCost));

                // CRITICAL: Validate credits before proceeding
                if (!await ValidateCreditsForTotalOrderAsync(totalOrderCost))
                {
                    return; // Validation failed, error message already shown
                }

                // Create upsell result
                var upsellResult = new UpsellResult
                {
                    OriginalProduct = _originalProduct!,
                    OriginalTemplate = _currentTemplate!,
                    ComposedImagePath = _composedImagePath!,
                    CapturedPhotosPaths = _capturedPhotosPaths!,
                    ExtraCopies = _selectedExtraCopies,
                    ExtraCopiesPrice = _extraCopiesPrice,
                    CrossSellAccepted = _crossSellAccepted,
                    CrossSellProduct = _crossSellAccepted ? _crossSellProduct : null, // Only pass if accepted
                    CrossSellPrice = _crossSellPrice,
                    TotalAdditionalCost = totalAdditionalCost,
                    SelectedPhotoForCrossSell = _selectedPhotoForCrossSell
                };

                // Fire completion event
                UpsellCompleted?.Invoke(this, new UpsellCompletedEventArgs(upsellResult));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upselling completion failed", ex);
                UpsellCompleted?.Invoke(this, new UpsellCompletedEventArgs(CreateEmptyUpsellResult()));
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle copy button clicks (1, 2, 4+ copies)
        /// </summary>
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"=== COPY BUTTON CLICK DEBUG ===");
                if (sender is Button button && button.Tag is string tagValue && int.TryParse(tagValue, out int copies))
                {
                    Console.WriteLine($"Button tag parsed - copies: {copies}");
                    if (copies == 3)
                    {
                        // Show quantity selector for 3+ copies
                        Console.WriteLine("3+ copies selected - showing quantity selector");
                        _currentQuantity = 3;
                        Console.WriteLine($"Set _currentQuantity to: {_currentQuantity}");
                        ShowQuantitySelector();
                    }
                    else
                    {
                        // Direct selection for 1 or 2 copies
                        Console.WriteLine($"Direct selection - {copies} copies");
                        _selectedExtraCopies = copies;
                        _extraCopiesPrice = CalculateExtraCopyPrice(copies);
                        
                        Console.WriteLine($"Set _selectedExtraCopies to: {_selectedExtraCopies}");
                        Console.WriteLine($"Set _extraCopiesPrice to: ${_extraCopiesPrice:F2}");
                        
                        LoggingService.Application.Information("Extra copies selected",
                            ("Copies", copies),
                            ("Price", _extraCopiesPrice));

                        Console.WriteLine("Starting cross-sell stage...");
                        await StartCrossSellStage();
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: Failed to parse button tag or sender is not a button");
                    if (sender is Button btn)
                    {
                        Console.WriteLine($"Button tag value: '{btn.Tag?.ToString() ?? "null"}'");
                    }
                }
                Console.WriteLine($"=== COPY BUTTON CLICK COMPLETE ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: CopyButton_Click exception: {ex.Message}");
                LoggingService.Application.Error("Copy button click failed", ex);
            }
        }

        /// <summary>
        /// Handle quantity adjustment buttons (+ and -)
        /// </summary>
        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"[DEBUG] PlusButton_Click triggered - Current quantity: {_currentQuantity}");
            e.Handled = true; // Prevent event bubbling to parent card button
            
            if (_currentQuantity < 10)
            {
                _currentQuantity++;
                Console.WriteLine($"[DEBUG] Quantity increased to: {_currentQuantity}");
                UpdateQuantityDisplay();
            }
            else
            {
                Console.WriteLine("[DEBUG] Quantity already at max (10)");
            }
        }

        private void MinusButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"[DEBUG] MinusButton_Click triggered - Current quantity: {_currentQuantity}");
            e.Handled = true; // Prevent event bubbling to parent card button
            
            if (_currentQuantity > 3)
            {
                _currentQuantity--;
                Console.WriteLine($"[DEBUG] Quantity decreased to: {_currentQuantity}");
                UpdateQuantityDisplay();
            }
            else
            {
                Console.WriteLine("[DEBUG] Quantity already at min (3)");
            }
        }

        /// <summary>
        /// Confirm the selected quantity for 4+ copies
        /// </summary>
        private async void ConfirmQuantity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"=== CONFIRM QUANTITY DEBUG ===");
                Console.WriteLine($"Current quantity: {_currentQuantity}");
                
                _selectedExtraCopies = _currentQuantity;
                _extraCopiesPrice = CalculateExtraCopyPrice(_currentQuantity);
                
                Console.WriteLine($"Set _selectedExtraCopies to: {_selectedExtraCopies}");
                Console.WriteLine($"Set _extraCopiesPrice to: ${_extraCopiesPrice:F2}");
                
                LoggingService.Application.Information("Extra copies quantity confirmed",
                    ("Copies", _currentQuantity),
                    ("Price", _extraCopiesPrice));

                Console.WriteLine("Starting cross-sell stage...");
                await StartCrossSellStage();
                Console.WriteLine($"=== CONFIRM QUANTITY COMPLETE ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Quantity confirmation failed: {ex.Message}");
                LoggingService.Application.Error("Quantity confirmation failed", ex);
            }
        }

        /// <summary>
        /// Handle "Continue" button click for cross-sell acceptance
        /// </summary>
        private async void Continue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _crossSellAccepted = true;
                _crossSellPrice = _crossSellProduct?.Price ?? 0;
                
                // Validate credits before proceeding
                var totalOrderCost = (_originalProduct?.Price ?? 0) + _extraCopiesPrice + _crossSellPrice;
                if (!await ValidateCreditsForTotalOrderAsync(totalOrderCost))
                {
                    _crossSellAccepted = false; // Reset if validation fails
                    _crossSellPrice = 0;
                    return;
                }
                
                // Get the selected photo for the cross-sell
                if (_capturedPhotos.Count > 0 && 
                    _selectedPhotoIndex >= 0 && 
                    _selectedPhotoIndex < _capturedPhotos.Count)
                {
                    _selectedPhotoForCrossSell = _capturedPhotos[_selectedPhotoIndex];
                }
                else
                {
                    _selectedPhotoForCrossSell = null;
                }
                
                LoggingService.Application.Information("Cross-sell accepted",
                    ("CrossSellProduct", _crossSellProduct?.Type ?? "Unknown"),
                    ("CrossSellPrice", _crossSellPrice),
                    ("SelectedPhoto", _selectedPhotoForCrossSell ?? "None"),
                    ("PhotoIndex", _selectedPhotoIndex + 1),
                    ("TotalPhotos", _capturedPhotos.Count));

                CompleteUpselling();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Cross-sell acceptance failed", ex);
            }
        }

        /// <summary>
        /// Handle "No Thanks" button
        /// </summary>
        private async void NoThanks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentStage == UpsellStage.ExtraCopies)
                {
                    // Skip extra copies, go to cross-sell
                    _selectedExtraCopies = 0;
                    _extraCopiesPrice = 0;
                    await StartCrossSellStage();
                }
                else if (_currentStage == UpsellStage.CrossSell)
                {
                    // Decline cross-sell, validate credits for order without cross-sell
                    _crossSellAccepted = false;
                    _crossSellPrice = 0;
                    
                    // Validate credits for order without cross-sell
                    var totalOrderCost = (_originalProduct?.Price ?? 0) + _extraCopiesPrice;
                    if (!await ValidateCreditsForTotalOrderAsync(totalOrderCost))
                    {
                        return; // Validation failed, don't proceed
                    }
                    
                    CompleteUpselling();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("No thanks button failed", ex);
            }
        }

        /// <summary>
        /// Navigate to previous photo in cross-sell preview
        /// </summary>
        private void PrevPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPhotoIndex > 0)
            {
                _selectedPhotoIndex--;
                UpdatePhotoDisplay();
                LoggingService.Application.Information($"Navigated to photo {_selectedPhotoIndex + 1} of {_capturedPhotos.Count}");
            }
        }

        /// <summary>
        /// Navigate to next photo in cross-sell preview
        /// </summary>
        private void NextPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPhotoIndex < _capturedPhotos.Count - 1)
            {
                _selectedPhotoIndex++;
                UpdatePhotoDisplay();
                LoggingService.Application.Information($"Navigated to photo {_selectedPhotoIndex + 1} of {_capturedPhotos.Count}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update pricing displays based on original product
        /// </summary>
        private void UpdatePricingDisplays()
        {
            Console.WriteLine($"=== UPDATE PRICING DISPLAYS DEBUG ===");
            
            // Update copy pricing using configurable values from admin dashboard
            // Round UP to nearest dollar for display (no decimals in photo booth pricing)
            OneCopyPrice.Text = $"${Math.Ceiling(_extraCopyPrice1)}";
            TwoCopyPrice.Text = $"${Math.Ceiling(_extraCopyPrice2)}";
            
            Console.WriteLine($"OneCopyPrice display set to: {OneCopyPrice.Text}");
            Console.WriteLine($"TwoCopyPrice display set to: {TwoCopyPrice.Text}");
            
            // Calculate 3+ copy price using simplified pricing model
            decimal threeCopyPrice = CalculateExtraCopyPrice(3);
            ThreeCopyPrice.Text = $"${Math.Ceiling(threeCopyPrice)}";
            
            Console.WriteLine($"CalculateExtraCopyPrice(3) returned: ${threeCopyPrice:F2}");
            Console.WriteLine($"ThreeCopyPrice display set to: {ThreeCopyPrice.Text}");
            Console.WriteLine($"=== PRICING DISPLAYS UPDATED ===");
        }

        /// <summary>
        /// Get the appropriate cross-sell product
        /// </summary>
        public ProductInfo? GetCrossSellProduct()
        {
            if (_originalProduct == null) return null;

            // Cross-sell logic: Strips → 4x6, 4x6 → Strips
            return _originalProduct.Type?.ToLower() switch
            {
                "strips" or "photostrips" => new ProductInfo 
                { 
                    Type = "4x6", 
                    Name = "4x6 Photos", 
                    Description = "High-quality print",
                    Price = GetDatabasePriceForProductType("Photo4x6") ?? PHOTO_4X6_PRICE 
                },
                "4x6" or "photo4x6" => new ProductInfo 
                { 
                    Type = "strips", 
                    Name = "Photo Strips", 
                    Description = "Classic 4-photo strip",
                    Price = GetDatabasePriceForProductType("PhotoStrips") ?? STRIPS_PRICE 
                },
                _ => null // No cross-sell for phone prints or unknown types
            };
        }

        /// <summary>
        /// Get product price from database by product type
        /// </summary>
        private decimal? GetDatabasePriceForProductType(string productType)
        {
            try
            {
                var productsResult = _databaseService.GetAllAsync<Product>().Result;
                if (productsResult.Success && productsResult.Data?.Count > 0)
                {
                    var product = productsResult.Data.FirstOrDefault(p => p.ProductType.ToString() == productType);
                    return product?.Price;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Warning("Failed to get database price for cross-sell product", 
                    ("ProductType", productType),
                    ("ErrorMessage", ex.Message));
            }
            return null; // Fallback to hardcoded constant
        }

        /// <summary>
        /// Calculate extra copy pricing based on quantity using configurable pricing
        /// </summary>
        public decimal CalculateExtraCopyPrice(int copies)
        {
            Console.WriteLine($"=== CALCULATE EXTRA COPY PRICE DEBUG ===");
            Console.WriteLine($"Calculating price for {copies} copies");
            Console.WriteLine($"Current pricing values:");
            Console.WriteLine($"  _extraCopyPrice1: ${_extraCopyPrice1:F2}");
            Console.WriteLine($"  _extraCopyPrice2: ${_extraCopyPrice2:F2}");
            Console.WriteLine($"  _extraCopyPriceAdditional: ${_extraCopyPriceAdditional:F2}");
            
            decimal result = copies switch
            {
                1 => _extraCopyPrice1,
                2 => _extraCopyPrice2,
                >= 3 => _extraCopyPrice2 + ((copies - 2) * _extraCopyPriceAdditional),
                _ => 0
            };
            
            if (copies >= 3)
            {
                Console.WriteLine($"For {copies} copies: ${_extraCopyPrice2:F2} + (({copies} - 2) * ${_extraCopyPriceAdditional:F2}) = ${result:F2}");
            }
            else
            {
                Console.WriteLine($"For {copies} copies: ${result:F2}");
            }
            
            Console.WriteLine($"=== CALCULATION RESULT: ${result:F2} ===");
            return result;
        }

        /// <summary>
        /// Set the original product for testing purposes
        /// </summary>
        public void SetOriginalProductForTesting(ProductInfo product)
        {
            _originalProduct = product;
        }

        /// <summary>
        /// Set extra copy pricing configuration for testing purposes
        /// </summary>
        public void SetExtraCopyPricingForTesting(bool useCustomPricing, decimal basePrice, decimal? extraCopy1Price = null, decimal? extraCopy2Price = null, decimal? extraCopyAdditionalPrice = null)
        {
            _useCustomExtraCopyPricing = useCustomPricing;
            
            if (useCustomPricing)
            {
                _extraCopyPrice1 = extraCopy1Price ?? basePrice;
                _extraCopyPrice2 = extraCopy2Price ?? (basePrice * 2);
                _extraCopyPriceAdditional = extraCopyAdditionalPrice ?? basePrice;
            }
            else
            {
                // Use base product price for all extra copies (no discount)
                _extraCopyPrice1 = basePrice;
                _extraCopyPrice2 = basePrice * 2;
                _extraCopyPriceAdditional = basePrice;
            }
        }

        /// <summary>
        /// Update the cross-sell display
        /// </summary>
        private void UpdateCrossSellDisplay()
        {
            if (_crossSellProduct == null) return;

            var isStrips = _crossSellProduct.Type?.ToLower() == "strips";
            
            // Update the cross-sell button text based on the product type
            ContinueButton.Content = isStrips ? "Yes! Add Photo Strips" : "Yes! Add 4x6 Photos";
            
            // Initialize the photo carousel
            try
            {
                if (_capturedPhotosPaths != null && _capturedPhotosPaths.Count > 0)
                {
                    // Populate the captured photos list
                    _capturedPhotos.Clear();
                    foreach (var photoPath in _capturedPhotosPaths)
                    {
                        if (File.Exists(photoPath))
                        {
                            _capturedPhotos.Add(photoPath);
                        }
                    }

                    if (_capturedPhotos.Count > 0)
                    {
                        _selectedPhotoIndex = 0;
                        UpdatePhotoDisplay();
                        UpdateNavigationButtons();
                        Console.WriteLine($"[DEBUG] Photo carousel initialized with {_capturedPhotos.Count} photos");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] No valid captured photos found");
                        SetCrossSellFallbackPreview(isStrips);
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] No captured photos available for cross-sell preview");
                    SetCrossSellFallbackPreview(isStrips);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error initializing photo carousel: {ex.Message}");
                LoggingService.Application.Error("Failed to initialize photo carousel", ex);
                SetCrossSellFallbackPreview(isStrips);
            }
        }

        /// <summary>
        /// Update the photo display with the currently selected photo
        /// </summary>
        private void UpdatePhotoDisplay()
        {
            try
            {
                if (_capturedPhotos.Count == 0 || _selectedPhotoIndex < 0 || _selectedPhotoIndex >= _capturedPhotos.Count)
                {
                    return;
                }

                var photoPath = _capturedPhotos[_selectedPhotoIndex];
                Console.WriteLine($"[DEBUG] Displaying photo {_selectedPhotoIndex + 1}/{_capturedPhotos.Count}: {photoPath}");

                // Dispose previous image if exists
                if (CrossSellPreview.Child is Image oldImage)
                {
                    oldImage.Source = null;
                }

                var image = new Image
                {
                    Source = new BitmapImage(new Uri(photoPath, UriKind.Absolute)),
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                CrossSellPreview.Child = image;

                // Update the photo counter
                PhotoCounter.Text = $"{_selectedPhotoIndex + 1} / {_capturedPhotos.Count}";
                
                UpdateNavigationButtons();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error updating photo display: {ex.Message}");
                LoggingService.Application.Error("Failed to update photo display", ex);
            }
        }

        /// <summary>
        /// Update navigation button states
        /// </summary>
        private void UpdateNavigationButtons()
        {
            if (_capturedPhotos.Count <= 1)
            {
                PrevPhotoButton.IsEnabled = false;
                NextPhotoButton.IsEnabled = false;
            }
            else
            {
                PrevPhotoButton.IsEnabled = _selectedPhotoIndex > 0;
                NextPhotoButton.IsEnabled = _selectedPhotoIndex < _capturedPhotos.Count - 1;
            }
        }

        /// <summary>
        /// Set fallback preview when captured photo cannot be loaded
        /// </summary>
        private void SetCrossSellFallbackPreview(bool isStrips)
        {
            CrossSellPreview.Child = new TextBlock
            {
                Text = isStrips ? "📸" : "🖼️",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
        }

        /// <summary>
        /// Show the quantity selector for 4+ copies (now integrated in the card)
        /// </summary>
        private void ShowQuantitySelector()
        {
            Console.WriteLine("[DEBUG] ShowQuantitySelector called");
            // Since the quantity selector is now integrated in the 4+ card, we just update the display
            UpdateQuantityDisplay();
        }

        /// <summary>
        /// Update the quantity display and confirm button
        /// </summary>
        private void UpdateQuantityDisplay()
        {
            Console.WriteLine($"=== UPDATE QUANTITY DISPLAY DEBUG ===");
            Console.WriteLine($"Current quantity: {_currentQuantity}");
            try
            {
                CardQuantityDisplay.Text = _currentQuantity.ToString();
                var price = CalculateExtraCopyPrice(_currentQuantity);
                // Round UP to nearest dollar for display (no decimals in photo booth pricing)
                ThreeCopyPrice.Text = $"${Math.Ceiling(price)}";
                ConfirmQuantityButton.Content = $"Select {_currentQuantity} Copies";
                
                Console.WriteLine($"CardQuantityDisplay.Text set to: {CardQuantityDisplay.Text}");
                Console.WriteLine($"Price calculated: ${price:F2}");
                Console.WriteLine($"ThreeCopyPrice.Text set to: {ThreeCopyPrice.Text}");
                Console.WriteLine($"ConfirmQuantityButton.Content set to: {ConfirmQuantityButton.Content}");
                Console.WriteLine($"=== QUANTITY DISPLAY UPDATED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: UpdateQuantityDisplay failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create empty upsell result for fallback scenarios
        /// </summary>
        private UpsellResult CreateEmptyUpsellResult()
        {
            return new UpsellResult
            {
                OriginalProduct = _originalProduct!,
                OriginalTemplate = _currentTemplate!,
                ComposedImagePath = _composedImagePath ?? "",
                CapturedPhotosPaths = _capturedPhotosPaths ?? new List<string>(),
                ExtraCopies = 0,
                ExtraCopiesPrice = 0,
                CrossSellAccepted = false,
                CrossSellProduct = null, // Always null for fallback
                CrossSellPrice = 0,
                TotalAdditionalCost = 0,
                SelectedPhotoForCrossSell = null
            };
        }

        #endregion

        #region Timeout Management

        /// <summary>
        /// Start the timeout timer for the current stage
        /// </summary>
        private void StartTimeoutTimer()
        {
            StopTimeoutTimer();
            
            _currentTimeoutProgress = 0;
            TimeoutProgress.Value = 0;
            TimeoutProgress.Maximum = 100;

            // Main timeout timer
            _timeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(TIMEOUT_SECONDS)
            };
            _timeoutTimer.Tick += TimeoutTimer_Tick;
            _timeoutTimer.Start();

            // Progress update timer (update every 100ms)
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            LoggingService.Application.Information("Timeout timer started",
                ("Stage", _currentStage.ToString()),
                ("TimeoutSeconds", TIMEOUT_SECONDS));
        }

        /// <summary>
        /// Stop the timeout timer
        /// </summary>
        private void StopTimeoutTimer()
        {
            _timeoutTimer?.Stop();
            _progressTimer?.Stop();
            _timeoutTimer = null;
            _progressTimer = null;
        }

        /// <summary>
        /// Handle timeout expiration
        /// </summary>
        private async void TimeoutTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Warning("Upsell timeout occurred",
                    ("Stage", _currentStage.ToString()),
                    ("TimeElapsed", TIMEOUT_SECONDS));

                StopTimeoutTimer();

                if (_currentStage == UpsellStage.ExtraCopies)
                {
                    // Timeout on extra copies, proceed to cross-sell
                    _selectedExtraCopies = 0;
                    _extraCopiesPrice = 0;
                    await StartCrossSellStage();
                }
                else if (_currentStage == UpsellStage.CrossSell)
                {
                    // Timeout on cross-sell, complete upselling
                    _crossSellAccepted = false;
                    _crossSellPrice = 0; // Reset cross-sell price on timeout
                    CompleteUpselling();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Timeout handling failed", ex);
                UpsellTimeout?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Update the progress bar
        /// </summary>
        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            _currentTimeoutProgress += (100.0 / (TIMEOUT_SECONDS * 10)); // 10 updates per second
            TimeoutProgress.Value = Math.Min(_currentTimeoutProgress, 100);
        }

        #endregion

        #region Audio Playback

        /// <summary>
        /// Play audio message for upselling
        /// </summary>
        private async Task PlayAudioMessage(string message)
        {
            try
            {
                LoggingService.Application.Information("Playing upsell audio message",
                    ("Message", message),
                    ("Stage", _currentStage.ToString()));

                // TODO: Implement actual TTS or audio file playback
                // For now, this is a placeholder that logs the audio message
                // In a full implementation, you would:
                // 1. Use System.Speech.Synthesis.SpeechSynthesizer for TTS
                // 2. Or play pre-recorded audio files
                // 3. Or integrate with a professional TTS service

                await Task.Delay(100); // Simulate audio playback delay
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Audio playback failed", ex,
                    ("Message", message));
            }
        }

        #endregion

        #region Animation

        /// <summary>
        /// Initialize background animations (matches welcome screen exactly)
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
                LoggingService.Application.Error("Animation initialization failed", ex);
            }
        }

        /// <summary>
        /// Start floating orb animations (exactly like welcome screen)
        /// </summary>
        private void StartFloatingOrbAnimations()
        {
            foreach (Ellipse orb in FloatingOrbsCanvas.Children)
            {
                var translateTransform = new TranslateTransform();
                orb.RenderTransform = translateTransform;

                // Create circular floating motion for each orb
                var storyboard = new Storyboard();
                storyboard.RepeatBehavior = RepeatBehavior.Forever;

                // X movement (horizontal floating)
                var animationX = new DoubleAnimation
                {
                    From = -30,
                    To = 30,
                    Duration = TimeSpan.FromSeconds(8 + _random.NextDouble() * 4),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                // Y movement (vertical floating)
                var animationY = new DoubleAnimation
                {
                    From = -20,
                    To = 20,
                    Duration = TimeSpan.FromSeconds(6 + _random.NextDouble() * 4),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = TimeSpan.FromSeconds(_random.NextDouble() * 2) // Stagger the animations
                };

                Storyboard.SetTarget(animationX, orb);
                Storyboard.SetTargetProperty(animationX, new PropertyPath("RenderTransform.X"));

                Storyboard.SetTarget(animationY, orb);
                Storyboard.SetTargetProperty(animationY, new PropertyPath("RenderTransform.Y"));

                storyboard.Children.Add(animationX);
                storyboard.Children.Add(animationY);

                storyboard.Begin();
            }
        }

        /// <summary>
        /// Create floating particles (exactly like welcome screen)
        /// </summary>
        private void CreateFloatingParticles()
        {
            for (int i = 0; i < 15; i++)
            {
                var particle = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Colors.White) { Opacity = 0.3 },
                };

                Canvas.SetLeft(particle, _random.Next(0, 1920));
                Canvas.SetTop(particle, _random.Next(0, 1080));

                ParticlesCanvas.Children.Add(particle);
                _particles.Add(particle);
            }
        }

        /// <summary>
        /// Start particle animations (exactly like welcome screen)
        /// </summary>
        private void StartParticleAnimations()
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Slower update for smoother movement
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        /// <summary>
        /// Update particle positions with smooth upward movement (like welcome screen)
        /// </summary>
        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var particle in _particles)
            {
                var currentTop = Canvas.GetTop(particle);
                var newTop = currentTop - 0.3; // Slow upward drift

                if (newTop < -particle.Height)
                {
                    newTop = 1080 + particle.Height;
                    Canvas.SetLeft(particle, _random.Next(0, 1920));
                }

                Canvas.SetTop(particle, newTop);
            }
        }

        #endregion

        #region Credits Management

        /// <summary>
        /// Refresh credits from database
        /// </summary>
        private async Task RefreshCreditsFromDatabase()
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
                LoggingService.Application.Error("Error refreshing credits from database", ex);
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
                if (CreditsDisplay != null)
                {
                    CreditsDisplay.Text = $"Credits: ${_currentCredits:F0}";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to update credits display", ex);
            }
        }

        /// <summary>
        /// Validate that user has sufficient credits for the total order
        /// </summary>
        private async Task<bool> ValidateCreditsForTotalOrderAsync(decimal totalOrderCost)
        {
            try
            {
                // Refresh credits to get most up-to-date balance
                await RefreshCreditsFromDatabase();

                if (_currentCredits < totalOrderCost)
                {
                    var shortfall = totalOrderCost - _currentCredits;
                    
                    LoggingService.Application.Warning("Insufficient credits for total order",
                        ("TotalOrderCost", totalOrderCost),
                        ("CurrentCredits", _currentCredits),
                        ("Shortfall", shortfall));

                    // Show error message to user - simplified to avoid information disclosure
                    var message = "Insufficient credits for this order.\n\nPlease contact staff to add more credits or modify your order.";

                    NotificationService.Instance.ShowError("Insufficient Credits", message, 10);
                    
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Credit validation failed", ex);
                NotificationService.Instance.ShowError("Credit Validation Error", 
                    "Unable to validate credits. Please try again or contact support.");
                return false;
            }
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Handle control loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Upsell screen loaded");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell screen load failed", ex);
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                StopTimeoutTimer();
                _animationTimer?.Stop();
                _animationTimer = null;
                
                FloatingOrbsCanvas.Children.Clear();
                ParticlesCanvas.Children.Clear();
                _particles.Clear();

                LoggingService.Application.Information("Upsell screen disposed");
                _disposed = true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Upsell screen disposal failed", ex);
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Upselling stage enumeration
    /// </summary>
    public enum UpsellStage
    {
        ExtraCopies,
        CrossSell
    }

    /// <summary>
    /// Upselling result data
    /// </summary>
    public class UpsellResult
    {
        public ProductInfo OriginalProduct { get; set; } = null!;
        public Template OriginalTemplate { get; set; } = null!;
        public string ComposedImagePath { get; set; } = "";
        public List<string> CapturedPhotosPaths { get; set; } = new();
        public int ExtraCopies { get; set; }
        public decimal ExtraCopiesPrice { get; set; }
        public bool CrossSellAccepted { get; set; }
        public ProductInfo? CrossSellProduct { get; set; }
        public decimal CrossSellPrice { get; set; }
        public decimal TotalAdditionalCost { get; set; }
        public string? SelectedPhotoForCrossSell { get; set; } // Path to the specific photo selected for cross-sell
    }

    /// <summary>
    /// Event arguments for upsell completion
    /// </summary>
    public class UpsellCompletedEventArgs : EventArgs
    {
        public UpsellResult Result { get; }

        public UpsellCompletedEventArgs(UpsellResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }

    #endregion
}
