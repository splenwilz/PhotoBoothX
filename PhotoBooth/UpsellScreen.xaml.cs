using System;
using System.Collections.Generic;
using System.IO;
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
        private int _currentQuantity = 4; // For 4+ copies adjustment

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

        // Photo selection for cross-sell
        private int _selectedPhotoIndex = 0;
        private List<string> _capturedPhotos = new List<string>();
        private string? _selectedPhotoForCrossSell = null;

        // Pricing configuration (should match ProductConfiguration)
        private const decimal STRIPS_PRICE = 5.00m;
        private const decimal PHOTO_4X6_PRICE = 3.00m;
        private const decimal EXTRA_COPY_PRICE_1 = 3.00m;
        private const decimal EXTRA_COPY_PRICE_2 = 5.00m;
        private const decimal EXTRA_COPY_PRICE_4_BASE = 8.00m;
        private const decimal EXTRA_COPY_PRICE_PER_ADDITIONAL = 1.50m;

        #endregion

        #region Constructor

        public UpsellScreen()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            InitializeAnimations();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the upsell screen with session data
        /// </summary>
        public async Task InitializeAsync(Template template, ProductInfo originalProduct, string composedImagePath, List<string> capturedPhotosPaths)
        {
            try
            {
                _currentTemplate = template;
                _originalProduct = originalProduct;
                _composedImagePath = composedImagePath;
                _capturedPhotosPaths = new List<string>(capturedPhotosPaths);
                _originalPrice = originalProduct.Price;

                LoggingService.Application.Information("Upsell screen initialized",
                    ("OriginalProduct", originalProduct.Type),
                    ("OriginalPrice", originalProduct.Price),
                    ("TemplateName", template.Name ?? "Unknown"));

                // Update pricing displays
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
        private void CompleteUpselling()
        {
            try
            {
                StopTimeoutTimer();

                var totalAdditionalCost = _extraCopiesPrice + (_crossSellAccepted ? _crossSellPrice : 0);

                LoggingService.Application.Information("Upselling completed",
                    ("ExtraCopies", _selectedExtraCopies),
                    ("CrossSellAccepted", _crossSellAccepted),
                    ("ExtraCopiesPrice", _extraCopiesPrice),
                    ("CrossSellPrice", _crossSellPrice),
                    ("TotalAdditionalCost", totalAdditionalCost));

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
                    CrossSellProduct = _crossSellProduct,
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
                Console.WriteLine($"[DEBUG] CopyButton_Click triggered");
                if (sender is Button button && button.Tag is string tagValue && int.TryParse(tagValue, out int copies))
                {
                    Console.WriteLine($"[DEBUG] Button tag parsed - copies: {copies}");
                    if (copies == 4)
                    {
                        // Show quantity selector for 4+ copies
                        Console.WriteLine("[DEBUG] 4+ copies selected - showing quantity selector");
                        _currentQuantity = 4;
                        ShowQuantitySelector();
                    }
                    else
                    {
                        // Direct selection for 1 or 2 copies
                        Console.WriteLine($"[DEBUG] Direct selection - {copies} copies");
                        _selectedExtraCopies = copies;
                        _extraCopiesPrice = copies == 1 ? EXTRA_COPY_PRICE_1 : EXTRA_COPY_PRICE_2;
                        
                        LoggingService.Application.Information("Extra copies selected",
                            ("Copies", copies),
                            ("Price", _extraCopiesPrice));

                        await StartCrossSellStage();
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] Failed to parse button tag or sender is not a button");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] CopyButton_Click exception: {ex.Message}");
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
            
            if (_currentQuantity > 4)
            {
                _currentQuantity--;
                Console.WriteLine($"[DEBUG] Quantity decreased to: {_currentQuantity}");
                UpdateQuantityDisplay();
            }
            else
            {
                Console.WriteLine("[DEBUG] Quantity already at min (4)");
            }
        }

        /// <summary>
        /// Confirm the selected quantity for 4+ copies
        /// </summary>
        private async void ConfirmQuantity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _selectedExtraCopies = _currentQuantity;
                _extraCopiesPrice = EXTRA_COPY_PRICE_4_BASE + ((_currentQuantity - 4) * EXTRA_COPY_PRICE_PER_ADDITIONAL);
                
                LoggingService.Application.Information("Extra copies quantity confirmed",
                    ("Copies", _currentQuantity),
                    ("Price", _extraCopiesPrice));

                await StartCrossSellStage();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Quantity confirmation failed", ex);
            }
        }

        /// <summary>
        /// Handle "Continue" button click for cross-sell acceptance
        /// </summary>
        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _crossSellAccepted = true;
                _crossSellPrice = _crossSellProduct?.Price ?? 0;
                
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
                    // Decline cross-sell, complete upselling
                    _crossSellAccepted = false;
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
            // Update copy pricing based on original product type (simplified pricing for cards)
            OneCopyPrice.Text = $"${EXTRA_COPY_PRICE_1:F0}";
            TwoCopyPrice.Text = $"${EXTRA_COPY_PRICE_2:F0}";
            FourCopyPrice.Text = $"${EXTRA_COPY_PRICE_4_BASE:F0}";
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
                    Price = PHOTO_4X6_PRICE 
                },
                "4x6" or "photo4x6" => new ProductInfo 
                { 
                    Type = "strips", 
                    Name = "Photo Strips", 
                    Description = "Classic 4-photo strip",
                    Price = STRIPS_PRICE 
                },
                _ => null // No cross-sell for phone prints or unknown types
            };
        }

        /// <summary>
        /// Calculate extra copy pricing based on quantity
        /// </summary>
        public decimal CalculateExtraCopyPrice(int copies)
        {
            return copies switch
            {
                1 => EXTRA_COPY_PRICE_1,
                2 => EXTRA_COPY_PRICE_2,
                >= 4 => EXTRA_COPY_PRICE_4_BASE + ((copies - 4) * EXTRA_COPY_PRICE_PER_ADDITIONAL),
                _ => 0
            };
        }

        /// <summary>
        /// Set the original product for testing purposes
        /// </summary>
        public void SetOriginalProductForTesting(ProductInfo product)
        {
            _originalProduct = product;
        }

        /// <summary>
        /// Update the cross-sell display
        /// </summary>
        private void UpdateCrossSellDisplay()
        {
            if (_crossSellProduct == null) return;

            var isStrips = _crossSellProduct.Type?.ToLower() == "strips";
            
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
            Console.WriteLine($"[DEBUG] UpdateQuantityDisplay called - Quantity: {_currentQuantity}");
            try
            {
                CardQuantityDisplay.Text = _currentQuantity.ToString();
                var price = EXTRA_COPY_PRICE_4_BASE + ((_currentQuantity - 4) * EXTRA_COPY_PRICE_PER_ADDITIONAL);
                FourCopyPrice.Text = $"${price:F0}";
                ConfirmQuantityButton.Content = $"Select {_currentQuantity} Copies";
                Console.WriteLine($"[DEBUG] Display updated - Price: ${price:F0}, Button text: Select {_currentQuantity} Copies");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] UpdateQuantityDisplay failed: {ex.Message}");
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
                CrossSellProduct = null,
                CrossSellPrice = 0,
                TotalAdditionalCost = 0
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
