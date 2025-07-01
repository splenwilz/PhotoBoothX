using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Photo preview screen showing composed images with "How do they look?" UI
    /// </summary>
    public partial class PhotoPreviewScreen : UserControl, IDisposable
    {
        #region Events

        /// <summary>
        /// Event fired when user wants to retake photos
        /// </summary>
        public event EventHandler<RetakePhotosEventArgs>? RetakePhotosRequested;

        /// <summary>
        /// Event fired when user loves the photos and wants to proceed
        /// </summary>
        public event EventHandler<PhotosApprovedEventArgs>? PhotosApproved;

        /// <summary>
        /// Event fired when user clicks back button
        /// </summary>
        public event EventHandler? BackButtonClicked;

        #endregion

        #region Private Fields

        private readonly ImageCompositionService _compositionService;
        private readonly IDatabaseService _databaseService;
        private Template? _currentTemplate;
        private List<string>? _capturedPhotosPaths;
        private string? _composedImagePath;
        private bool _disposed = false;
        
        // Animation fields
        private DispatcherTimer? animationTimer;
        private Random random = new Random();

        #endregion

        #region Constructor

        public PhotoPreviewScreen(IDatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _compositionService = new ImageCompositionService(databaseService);
            
            // Initialize animations
            InitializeAnimatedBackground();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize and compose photos for preview
        /// </summary>
        public async Task InitializeWithPhotosAsync(Template template, List<string> capturedPhotosPaths)
        {
            try
            {
                _currentTemplate = template;
                _capturedPhotosPaths = new List<string>(capturedPhotosPaths);

                // Update UI with template info
                TemplateNameText.Text = template.Name ?? "Template";
                PhotoCountText.Text = $"{template.PhotoCount} Photo{(template.PhotoCount > 1 ? "s" : "")} • Ready to print";

                // Show loading overlay
                ShowLoading("Composing your photos...");

                LoggingService.Application.Information("Starting photo composition for preview", 
                    ("TemplateName", template.Name ?? "Unknown"),
                    ("PhotoCount", capturedPhotosPaths.Count));

                // Compose photos
                var compositionResult = await _compositionService.ComposePhotosAsync(template, capturedPhotosPaths);

                if (compositionResult.Success && compositionResult.OutputPath != null)
                {
                    _composedImagePath = compositionResult.OutputPath;

                    // Display composed image in both preview and full-size
                    if (compositionResult.PreviewImage != null)
                    {
                        ComposedPhotoImage.Source = compositionResult.PreviewImage;
                    }
                    else
                    {
                        // Fallback: load image from file
                        await LoadComposedImageAsync(compositionResult.OutputPath);
                    }

                    HideLoading();

                    LoggingService.Application.Information("Photo composition completed successfully", 
                        ("OutputPath", compositionResult.OutputPath));
                }
                else
                {
                    HideLoading();
                    ShowErrorMessage($"Failed to compose photos: {compositionResult.Message}");

                    LoggingService.Application.Error("Photo composition failed", null,
                        ("ErrorMessage", compositionResult.Message ?? "Unknown error"));
                }
            }
            catch (Exception ex)
            {
                HideLoading();
                ShowErrorMessage($"An error occurred: {ex.Message}");
                LoggingService.Application.Error("Photo preview initialization failed", ex);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Load composed image from file path
        /// </summary>
        private async Task LoadComposedImageAsync(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    ShowErrorMessage("Composed image file not found.");
                    return;
                }

                var bitmapImage = await Task.Run(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                });

                ComposedPhotoImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load composed image", ex, ("ImagePath", imagePath));
                ShowErrorMessage("Failed to load preview image.");
            }
        }

        /// <summary>
        /// Show loading overlay with message
        /// </summary>
        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hide loading overlay
        /// </summary>
        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Show error message
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageBorder.Visibility = Visibility.Visible;

            // Auto-hide after 8 seconds
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            hideTimer.Tick += (s, e) =>
            {
                ErrorMessageBorder.Visibility = Visibility.Collapsed;
                hideTimer.Stop();
            };
            hideTimer.Start();
        }

        /// <summary>
        /// Clear all displayed content
        /// </summary>
        private void ClearContent()
        {
            ComposedPhotoImage.Source = null;
            _composedImagePath = null;
            _capturedPhotosPaths?.Clear();
            _currentTemplate = null;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle retake photos button click
        /// </summary>
        private void RetakeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("=== RETAKE BUTTON CLICKED ===");
                Console.WriteLine($"Current template is null: {_currentTemplate == null}");
                
                if (_currentTemplate == null)
                {
                    Console.WriteLine("ERROR: Retake requested but no current template");
                    ShowErrorMessage("No template available for retake.");
                    return;
                }

                Console.WriteLine($"Template Name: {_currentTemplate.Name ?? "NULL"}");
                Console.WriteLine($"Template PhotoCount: {_currentTemplate.PhotoCount}");

                // Save template reference before clearing content
                var templateForRetake = _currentTemplate;

                // Clear current content
                ClearContent();

                // Fire retake event with saved template
                Console.WriteLine("Firing RetakePhotosRequested event");
                RetakePhotosRequested?.Invoke(this, new RetakePhotosEventArgs(templateForRetake));
                Console.WriteLine("RetakePhotosRequested event fired successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in RetakeButton_Click: {ex.Message}");
                Console.WriteLine($"ERROR stack trace: {ex.StackTrace}");
                ShowErrorMessage("Failed to process retake request.");
            }
        }

        /// <summary>
        /// Handle back button click
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked back from photo preview");
                
                // Clear current content
                ClearContent();
                
                // Fire back button event
                BackButtonClicked?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Back button click failed", ex);
            }
        }

        /// <summary>
        /// Handle "I Love Them!" button click
        /// </summary>
        private void LoveThemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentTemplate == null || string.IsNullOrEmpty(_composedImagePath))
                {
                    LoggingService.Application.Warning("Photos approved but missing template or composed image");
                    ShowErrorMessage("No photos to approve.");
                    return;
                }

                LoggingService.Application.Information("User approved photos", 
                    ("TemplateName", _currentTemplate.Name),
                    ("ComposedImagePath", _composedImagePath));

                // Fire approval event
                PhotosApproved?.Invoke(this, new PhotosApprovedEventArgs(_currentTemplate, _composedImagePath, _capturedPhotosPaths ?? new List<string>()));
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Photo approval failed", ex);
                ShowErrorMessage("Failed to process photo approval.");
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Initialize animated background with floating orbs and particles
        /// </summary>
        private void InitializeAnimatedBackground()
        {
            CreateFloatingParticles();
            StartFloatingOrbAnimations();
            StartParticleAnimations();
        }

        /// <summary>
        /// Creates floating animations for the background orbs
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
        /// Creates floating particles in the background
        /// </summary>
        private void CreateFloatingParticles()
        {
            for (int i = 0; i < 15; i++)
            {
                var particle = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Colors.White) { Opacity = 0.25 },
                };

                Canvas.SetLeft(particle, random.Next(0, 1920));
                Canvas.SetTop(particle, random.Next(0, 1080));

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
                Interval = TimeSpan.FromMilliseconds(100)
            };

            animationTimer.Tick += (s, e) =>
            {
                foreach (Ellipse particle in ParticlesCanvas.Children)
                {
                    var currentTop = Canvas.GetTop(particle);
                    var newTop = currentTop - 0.5;

                    if (newTop < -10)
                    {
                        // Reset particle to bottom with new random position
                        Canvas.SetTop(particle, 1090);
                        Canvas.SetLeft(particle, random.Next(0, 1920));
                    }
                    else
                    {
                        Canvas.SetTop(particle, newTop);
                    }
                }
            };

            animationTimer.Start();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Stop and dispose animation timer
                if (animationTimer != null)
                {
                    animationTimer.Stop();
                    animationTimer = null;
                }
                
                ClearContent();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for retake photos request
    /// </summary>
    public class RetakePhotosEventArgs : EventArgs
    {
        public Template Template { get; }

        public RetakePhotosEventArgs(Template template)
        {
            Template = template;
        }
    }

    /// <summary>
    /// Event arguments for photos approved
    /// </summary>
    public class PhotosApprovedEventArgs : EventArgs
    {
        public Template Template { get; }
        public string ComposedImagePath { get; }
        public List<string> OriginalPhotosPaths { get; }

        public PhotosApprovedEventArgs(Template template, string composedImagePath, List<string> originalPhotosPaths)
        {
            Template = template;
            ComposedImagePath = composedImagePath;
            OriginalPhotosPaths = originalPhotosPaths;
        }
    }
} 