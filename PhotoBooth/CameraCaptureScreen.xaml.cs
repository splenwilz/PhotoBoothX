using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AForge.Video;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Camera capture screen with countdown timer and live preview
    /// </summary>
    public partial class CameraCaptureScreen : UserControl, IDisposable
    {
        #region Events

        /// <summary>
        /// Event fired when user clicks back button
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// Event fired when all photos are captured and ready for composition
        /// </summary>
        public event EventHandler<PhotosCapturedEventArgs>? PhotosCaptured;

        #endregion

        #region Private Fields

        private readonly CameraService _cameraService;
        private readonly IDatabaseService _databaseService;
        private Template? _currentTemplate;
        private DispatcherTimer? _countdownTimer;
        private DispatcherTimer? _previewTimer;
        private DispatcherTimer? _autoTriggerTimer;
        private DispatcherTimer? _animationTimer;
        private DispatcherTimer? _errorHideTimer;
        private int _countdownValue;
        private int _currentPhotoIndex;
        private List<string> _capturedPhotos;
        private bool _isCapturing = false;
        private bool _disposed = false;
        private Random _random = new Random();

        #endregion

        #region Constructor

        public CameraCaptureScreen(IDatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _cameraService = new CameraService();
            _capturedPhotos = new List<string>();

            // Subscribe to camera events
            _cameraService.PreviewFrameReady += OnCameraPreviewFrameReady;
            _cameraService.CameraError += OnCameraError;

            // Initialize preview timer for camera updates (reduced frequency to prevent freezing)
            _previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // ~20 FPS (reduced from 30 FPS to prevent freezing)
            };
            _previewTimer.Tick += UpdateCameraPreview;

            // Initialize animations
            InitializeAnimations();
            CreateFloatingParticles();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize camera session with template
        /// </summary>
        public bool InitializeSession(Template template)
        {
            try
            {
                Console.WriteLine("=== INITIALIZING CAMERA CAPTURE SESSION ===");
                Console.WriteLine($"Template Name: {template.Name}");
                Console.WriteLine($"Template PhotoCount: {template.PhotoCount}");
                Console.WriteLine($"Template Layout is null: {template.Layout == null}");
                if (template.Layout != null)
                {
                    Console.WriteLine($"Layout PhotoCount: {template.Layout.PhotoCount}");
                    Console.WriteLine($"Layout PhotoAreas count: {template.Layout.PhotoAreas?.Count ?? 0}");
                }
                
                _currentTemplate = template;
                _currentPhotoIndex = 0;
                _capturedPhotos.Clear();
                
                Console.WriteLine($"Creating progress indicators for {template.PhotoCount} photos");
                
                // Create progress indicators
                CreateProgressIndicators(template.PhotoCount);
                UpdateProgressIndicators();
                
                // Update UI with template info
                UpdateTemplateInfo();
                UpdateStatusText($"Photo 1 of {template.PhotoCount} - Get ready!");
                
                // Start camera with optimized settings
                var cameraStarted = _cameraService.StartCamera();
                if (!cameraStarted)
                {
                    ShowErrorMessage("Failed to start camera. Please check your camera connection.");
                    return false;
                }

                // Start optimized preview updates
                _previewTimer?.Start();
                
                // Start automatic photo sequence after a short delay
                StartAutoPhotoSequence();
                
                Console.WriteLine("=== CAMERA CAPTURE SESSION INITIALIZED ===");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR initializing camera session: {ex.Message}");
                ShowErrorMessage($"Failed to initialize camera: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initialize floating animations
        /// </summary>
        private void InitializeAnimations()
        {
            StartFloatingOrbAnimations();
            StartParticleAnimations();
        }

        /// <summary>
        /// Create floating particles
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
            }
        }

        /// <summary>
        /// Start floating orb animations
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
                    To = _random.Next(-20, -5),
                    Duration = TimeSpan.FromSeconds(3 + _random.NextDouble() * 2),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                // Horizontal floating
                var xAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = _random.Next(-10, 10),
                    Duration = TimeSpan.FromSeconds(4 + _random.NextDouble() * 2),
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
        /// Start particle animations (optimized to reduce conflicts with camera)
        /// </summary>
        private void StartParticleAnimations()
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Reduced frequency to avoid conflicts with camera updates
            };

            _animationTimer.Tick += (s, e) =>
            {
                // Check if control is disposed before accessing UI elements
                if (_disposed)
                    return;
                    
                foreach (Ellipse particle in ParticlesCanvas.Children)
                {
                    var currentTop = Canvas.GetTop(particle);
                    var newTop = currentTop - 0.5;

                    if (newTop < -10)
                    {
                        newTop = ActualHeight + 10;
                        Canvas.SetLeft(particle, _random.Next(0, (int)ActualWidth));
                    }

                    Canvas.SetTop(particle, newTop);
                }
            };

            _animationTimer.Start();
        }

        /// <summary>
        /// Start automatic photo sequence
        /// </summary>
        private void StartAutoPhotoSequence()
        {
            // Start the first photo after 3 seconds
            _autoTriggerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _autoTriggerTimer.Tick += async (s, e) =>
            {
                _autoTriggerTimer.Stop();
                await StartPhotoCaptureSequence();
            };

            _autoTriggerTimer.Start();
        }

        /// <summary>
        /// Create progress indicator dots
        /// </summary>
        private void CreateProgressIndicators(int photoCount)
        {
            ProgressIndicator.Children.Clear();
            
            for (int i = 0; i < photoCount; i++)
            {
                var dot = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Fill = Brushes.White,
                    Opacity = 0.3,
                    Margin = new Thickness(8, 0, 8, 0)
                };
                
                ProgressIndicator.Children.Add(dot);
            }
        }

        /// <summary>
        /// Update progress indicators
        /// </summary>
        private void UpdateProgressIndicators()
        {
            for (int i = 0; i < ProgressIndicator.Children.Count; i++)
            {
                if (ProgressIndicator.Children[i] is Ellipse dot)
                {
                    dot.Opacity = i < _currentPhotoIndex ? 1.0 : 0.3;
                    dot.Fill = i < _currentPhotoIndex ? new SolidColorBrush(Color.FromRgb(165, 243, 252)) : Brushes.White;
                }
            }
        }

        /// <summary>
        /// Update template info display
        /// </summary>
        private void UpdateTemplateInfo()
        {
            if (_currentTemplate != null)
            {
                TemplateNameText.Text = _currentTemplate.Name ?? "Template";
                PhotoCountText.Text = $"{_currentTemplate.PhotoCount} Photos";
            }
        }

        /// <summary>
        /// Update status text
        /// </summary>
        private void UpdateStatusText(string text)
        {
            StatusText.Text = text;
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
        public void HideLoading()
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
            
            // Stop and dispose previous timer if exists
            if (_errorHideTimer != null)
            {
                _errorHideTimer.Stop();
                _errorHideTimer = null;
            }
            
            // Auto-hide after 5 seconds
            _errorHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _errorHideTimer.Tick += (s, e) =>
            {
                ErrorMessageBorder.Visibility = Visibility.Collapsed;
                _errorHideTimer.Stop();
                _errorHideTimer = null;
            };
            _errorHideTimer.Start();
        }

        /// <summary>
        /// Start photo capture sequence
        /// </summary>
        private async Task StartPhotoCaptureSequence()
        {
            if (_isCapturing || _currentTemplate == null)
                return;

            try
            {
                _isCapturing = true;
                
                Console.WriteLine($"Starting photo capture {_currentPhotoIndex + 1} of {_currentTemplate.PhotoCount}");
                
                // Reduce camera preview updates during photo capture to prevent UI freezing
                _cameraService.SetPhotoCaptureActive(true);
                
                // Show countdown
                await StartCountdown();
                
                // Play camera shutter sound
                SoundService.Instance.PlayCameraShutter();
                
                // Capture photo
                var photoPath = await _cameraService.CapturePhotoAsync($"photo_{_currentPhotoIndex + 1}_{DateTime.Now:yyyyMMdd_HHmmss}");
                
                if (photoPath != null)
                {
                    _capturedPhotos.Add(photoPath);
                    _currentPhotoIndex++;
                    UpdateProgressIndicators();
                    
                    Console.WriteLine($"Photo {_currentPhotoIndex} captured successfully");
                    
                    // Check if we've captured all photos
                    if (_currentPhotoIndex >= _currentTemplate.PhotoCount)
                    {
                        // Resume normal camera preview updates
                        _cameraService.SetPhotoCaptureActive(false);
                        
                        // Play success sound for completing all photos
                        SoundService.Instance.PlaySuccess();
                        await CompletePhotoSession();
                    }
                    else
                    {
                        // Prepare for next photo automatically
                        await Task.Delay(1000); // 1 second pause between photos (reduced from 2)
                        UpdateStatusText($"Photo {_currentPhotoIndex + 1} of {_currentTemplate.PhotoCount} - Get ready!");
                        _isCapturing = false;
                        
                        // Resume normal camera preview updates
                        _cameraService.SetPhotoCaptureActive(false);
                        
                        // Start next photo automatically
                        _autoTriggerTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1.5) // 1.5 seconds before next countdown (reduced from 3)
                        };

                        _autoTriggerTimer.Tick += async (s, e) =>
                        {
                            _autoTriggerTimer.Stop();
                            await StartPhotoCaptureSequence();
                        };

                        _autoTriggerTimer.Start();
                    }
                }
                else
                {
                    // Resume normal camera preview updates on error
                    _cameraService.SetPhotoCaptureActive(false);
                    
                    // Play error sound for failed capture
                    SoundService.Instance.PlayError();
                    ShowErrorMessage("Failed to capture photo. Please try again.");
                    _isCapturing = false;
                }
            }
            catch (Exception ex)
            {
                // Resume normal camera preview updates on exception
                _cameraService.SetPhotoCaptureActive(false);
                
                Console.WriteLine($"ERROR in photo capture: {ex.Message}");
                SoundService.Instance.PlayError();
                ShowErrorMessage($"Capture failed: {ex.Message}");
                _isCapturing = false;
            }
        }

        /// <summary>
        /// Start countdown animation
        /// </summary>
        private async Task StartCountdown()
        {
            _countdownValue = 3;
            CountdownText.Text = _countdownValue.ToString();
            CountdownOverlay.Visibility = Visibility.Visible;
            
            // Play initial countdown beep
            SoundService.Instance.PlayCountdownBeep();
            
            // Create countdown timer
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            var tcs = new TaskCompletionSource<bool>();
            
            _countdownTimer.Tick += (s, e) =>
            {
                _countdownValue--;
                
                if (_countdownValue > 0)
                {
                    CountdownText.Text = _countdownValue.ToString();
                    
                    // Play countdown beep for each number
                    SoundService.Instance.PlayCountdownBeep();
                    
                    // Animate countdown number
                    var scaleAnimation = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(500),
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    var scaleTransform = new ScaleTransform();
                    CountdownText.RenderTransform = scaleTransform;
                    CountdownText.RenderTransformOrigin = new Point(0.5, 0.5);
                    
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                }
                else
                {
                    _countdownTimer.Stop();
                    CountdownOverlay.Visibility = Visibility.Collapsed;
                    tcs.SetResult(true);
                }
            };
            
            _countdownTimer.Start();
            await tcs.Task;
        }

        /// <summary>
        /// Complete photo session and trigger composition
        /// </summary>
        private Task CompletePhotoSession()
        {
            try
            {
                UpdateStatusText("All photos captured! Composing your photos...");
                
                // Stop camera preview but keep the last captured photo visible
                _previewTimer?.Stop();
                _cameraService.StopCamera();
                
                // Show loading overlay on camera screen instead of immediate transition
                ShowLoading("Composing your photos...");
                
                Console.WriteLine($"Photo session completed - {_capturedPhotos.Count} photos captured");
                
                // Fire event with captured photos - now includes composition request
                PhotosCaptured?.Invoke(this, new PhotosCapturedEventArgs(_currentTemplate!, _capturedPhotos));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR completing photo session: {ex.Message}");
                ShowErrorMessage($"Session completion failed: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Update camera preview image (optimized with additional checks)
        /// </summary>
        private void UpdateCameraPreview(object? sender, EventArgs e)
        {
            try
            {
                var updateStart = DateTime.Now;

                // Only update if camera is active and new frame is available
                if (!_cameraService.IsCapturing)
                    return;

                // Only update if new frame is available (reduces CPU usage)
                if (_cameraService.IsNewFrameAvailable())
                {
                    var previewBitmap = _cameraService.GetPreviewBitmap();
                    if (previewBitmap != null && CameraPreviewImage.Source != previewBitmap)
                    {
                        CameraPreviewImage.Source = previewBitmap;
                    }
                }

                var totalTime = DateTime.Now - updateStart;
                if (totalTime.TotalMilliseconds > 20)
                {
                    Console.WriteLine($"[PREVIEW] ⚠️ SLOW UPDATE: {totalTime.TotalMilliseconds:F0}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW] ❌ ERROR: {ex.Message}");
                // Optionally reduce timer frequency if errors occur frequently
            }
        }

        /// <summary>
        /// Handle new camera preview frame (optimized event)
        /// </summary>
        private void OnCameraPreviewFrameReady(object? sender, WriteableBitmap e)
        {
            // This is now handled by the direct WriteableBitmap update
            // The frame is already optimally processed by CameraService
        }

        /// <summary>
        /// Handle camera errors
        /// </summary>
        private void OnCameraError(object? sender, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                ShowErrorMessage(errorMessage);
                Console.WriteLine($"Camera error occurred: {errorMessage}");
            });
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle back button click
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButtonClicked?.Invoke(this, EventArgs.Empty);
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
                try
                {
                    Console.WriteLine($"CameraCaptureScreen: Disposing camera capture screen (IsCapturing: {_isCapturing})");
                    
                    // Stop all sounds first
                    SoundService.Instance.StopAllSounds();
                    
                    // Stop all timers first
                    if (_countdownTimer != null)
                    {
                        _countdownTimer.Stop();
                        _countdownTimer = null;
                        Console.WriteLine("CameraCaptureScreen: Countdown timer stopped");
                    }
                    
                    if (_previewTimer != null)
                    {
                        _previewTimer.Stop();
                        _previewTimer = null;
                        Console.WriteLine("CameraCaptureScreen: Preview timer stopped");
                    }
                    
                    if (_autoTriggerTimer != null)
                    {
                        _autoTriggerTimer.Stop();
                        _autoTriggerTimer = null;
                        Console.WriteLine("CameraCaptureScreen: Auto trigger timer stopped");
                    }
                    
                    if (_animationTimer != null)
                    {
                        _animationTimer.Stop();
                        _animationTimer = null;
                        Console.WriteLine("CameraCaptureScreen: Animation timer stopped");
                    }
                    
                    if (_errorHideTimer != null)
                    {
                        _errorHideTimer.Stop();
                        _errorHideTimer = null;
                        Console.WriteLine("CameraCaptureScreen: Error hide timer stopped");
                    }
                    
                    // Unsubscribe from events and dispose camera service
                    if (_cameraService != null)
                    {
                        Console.WriteLine("CameraCaptureScreen: Disposing camera service");
                        _cameraService.PreviewFrameReady -= OnCameraPreviewFrameReady;
                        _cameraService.CameraError -= OnCameraError;
                        _cameraService.Dispose();
                        Console.WriteLine("CameraCaptureScreen: Camera service disposed");
                    }
                    
                    // Clear captured photos list
                    _capturedPhotos?.Clear();
                    
                    Console.WriteLine("CameraCaptureScreen: Camera capture screen disposed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CameraCaptureScreen ERROR during disposal: {ex.Message}");
                    Console.WriteLine($"CameraCaptureScreen ERROR stack trace: {ex.StackTrace}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for when photos are captured
    /// </summary>
    public class PhotosCapturedEventArgs : EventArgs
    {
        public Template Template { get; }
        public List<string> CapturedPhotosPaths { get; }

        public PhotosCapturedEventArgs(Template template, List<string> capturedPhotosPaths)
        {
            Template = template;
            CapturedPhotosPaths = capturedPhotosPaths;
        }
    }
} 