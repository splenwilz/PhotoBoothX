using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Input;
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
        private readonly MainWindow? _mainWindow; // Reference to MainWindow for operation mode check
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
        private decimal _currentCredits = 0;

        #endregion

        #region Constructor

        public CameraCaptureScreen(IDatabaseService databaseService, MainWindow? mainWindow = null)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _mainWindow = mainWindow;
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
            
            // Initialize credits display
            RefreshCreditsFromDatabase();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize camera session with template
        /// </summary>
        public async Task<bool> InitializeSessionAsync(Template template)
        {
            try
            {
                LoggingService.Application.Information("Initializing camera capture session",
                    ("TemplateName", template.Name),
                    ("PhotoCount", template.PhotoCount),
                    ("HasLayout", template.Layout != null),
                    ("LayoutPhotoCount", template.Layout?.PhotoCount ?? 0),
                    ("LayoutPhotoAreasCount", template.Layout?.PhotoAreas?.Count ?? 0));
                
                _currentTemplate = template;
                _currentPhotoIndex = 0;
                _capturedPhotos.Clear();
                
                LoggingService.Application.Debug("Creating progress indicators",
                    ("PhotoCount", template.PhotoCount));
                
                // Create progress indicators
                CreateProgressIndicators(template.PhotoCount);
                UpdateProgressIndicators();
                
                // Update UI with template info
                UpdateTemplateInfo();
                UpdateStatusText($"Photo 1 of {template.PhotoCount} - Get ready!");
                
                // Start camera with optimized settings
                var cameraStarted = await _cameraService.StartCameraAsync();
                if (!cameraStarted)
                {
                    LoggingService.Application.Error("Failed to start camera during session initialization", null);
                    ShowErrorMessage("Failed to start camera. Please check your camera connection.");
                    return false;
                }

                // Start optimized preview updates
                _previewTimer?.Start();
                
                // Start automatic photo sequence after a short delay
                StartAutoPhotoSequence();
                
                LoggingService.Application.Information("Camera capture session initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize camera session", ex);
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
        /// Show error message with enhanced styling and retry option
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            if (ErrorMessageText != null && ErrorMessageBorder != null)
        {
            ErrorMessageText.Text = message;
            ErrorMessageBorder.Visibility = Visibility.Visible;
            
                // Auto-hide after 15 seconds for long error messages
                _errorHideTimer?.Stop();
                _errorHideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(15)
                };
                _errorHideTimer.Tick += (s, e) =>
            {
                _errorHideTimer.Stop();
                    ErrorMessageBorder.Visibility = Visibility.Collapsed;
                };
                _errorHideTimer.Start();
            }
        }

        /// <summary>
        /// Show retry option for camera errors
        /// </summary>
        private void ShowRetryOption()
        {
            // Create retry button if it doesn't exist
            var retryButton = this.FindName("RetryButton") as Button;
            var diagnosticsButton = this.FindName("DiagnosticsButton") as Button;
            
            if (retryButton == null && ErrorMessageBorder != null)
            {
                // Create a new StackPanel container if ErrorMessageBorder only has TextBlock
                if (ErrorMessageBorder.Child is TextBlock textBlock)
                {
                    // Remove the TextBlock and add it to a StackPanel
                    ErrorMessageBorder.Child = null;
                    
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    stackPanel.Children.Add(textBlock);
                    
                    // Create button container
                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    
                    // Create retry button
                    retryButton = new Button
                    {
                        Name = "RetryButton",
                        Content = "🔄 Retry",
                        Width = 140,
                        Height = 45,
                        Margin = new Thickness(0, 0, 10, 0),
                        Style = (Style)FindResource("WelcomeScreenButtonStyle"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    retryButton.Click += RetryButton_Click;
                    buttonPanel.Children.Add(retryButton);
                    
                    // Create diagnostics button
                    diagnosticsButton = new Button
                    {
                        Name = "DiagnosticsButton",
                        Content = "🔧 Diagnose",
                        Width = 140,
                        Height = 45,
                        Margin = new Thickness(10, 0, 0, 0),
                        Style = (Style)FindResource("WelcomeScreenButtonStyle"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    diagnosticsButton.Click += DiagnosticsButton_Click;
                    buttonPanel.Children.Add(diagnosticsButton);
                    
                    // Add subtle entrance animations
                    AddButtonEntranceAnimation(retryButton, 0);
                    AddButtonEntranceAnimation(diagnosticsButton, 150); // Slight delay for staggered effect
                    
                    stackPanel.Children.Add(buttonPanel);
                    ErrorMessageBorder.Child = stackPanel;
                    
                    // Register the buttons so they can be found later
                    this.RegisterName("RetryButton", retryButton);
                    this.RegisterName("DiagnosticsButton", diagnosticsButton);
                }
            }
            
            if (retryButton != null)
            {
                retryButton.Visibility = Visibility.Visible;
            }
            if (diagnosticsButton != null)
            {
                diagnosticsButton.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handle retry button click
        /// </summary>
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked retry camera button");
                
                // Hide error overlay
                if (ErrorMessageBorder != null)
            {
                ErrorMessageBorder.Visibility = Visibility.Collapsed;
                }
                
                // Show loading message
                UpdateStatusText("Retrying camera connection...");
                
                // Restart camera initialization
                Task.Run(async () =>
                {
                    await Task.Delay(500); // Small delay to show loading message
                    
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (_currentTemplate != null)
                        {
                            // Re-initialize the camera session
                            var success = await InitializeSessionAsync(_currentTemplate);
                            if (!success)
                            {
                                UpdateStatusText("Camera retry failed. Please check your camera and try again.");
                            }
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error during camera retry", ex);
                ShowErrorMessage($"Retry failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle diagnostics button click
        /// </summary>
        private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("User clicked camera diagnostics button");
                
                // Show loading
                UpdateStatusText("Running camera diagnostics...");
                
                // Run diagnostics
                var diagnostics = await _cameraService.RunDiagnosticsAsync();
                
                // Show diagnostics results
                ShowDiagnosticsResults(diagnostics);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error running camera diagnostics", ex);
                ShowErrorMessage($"Diagnostics failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Show camera diagnostics results in a dialog
        /// </summary>
        private void ShowDiagnosticsResults(CameraDiagnosticResult diagnostics)
        {
            var message = $"Camera Diagnostics Report\n\n";
            message += $"Overall Status: {diagnostics.OverallStatus}\n";
            message += $"Cameras Detected: {diagnostics.CamerasDetected}\n";
            message += $"Privacy Settings: {(diagnostics.PrivacySettingsAllowed ? "Allowed" : "Blocked")}\n";
            message += $"Can Access Camera: {(diagnostics.CanAccessCamera ? "Yes" : "No")}\n";
            message += $"Windows Version: {diagnostics.WindowsVersion}\n\n";
            
            if (diagnostics.ConflictingProcesses.Any())
            {
                message += $"Conflicting Apps: {string.Join(", ", diagnostics.ConflictingProcesses)}\n\n";
            }
            
            if (diagnostics.Issues.Any())
            {
                message += "Issues Found:\n";
                foreach (var issue in diagnostics.Issues)
                {
                    message += $"• {issue}\n";
                }
                message += "\n";
            }
            
            if (diagnostics.Solutions.Any())
            {
                message += "Recommended Solutions:\n";
                for (int i = 0; i < diagnostics.Solutions.Count; i++)
                {
                    message += $"{i + 1}. {diagnostics.Solutions[i]}\n";
                }
            }
            
            if (diagnostics.Issues.Count == 0)
            {
                message += "No issues detected. The camera should be working properly.";
            }
            
            // Show as message box
            MessageBox.Show(message, "Camera Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }



        /// <summary>
        /// Add subtle entrance animation to buttons (similar to welcome screen animations)
        /// </summary>
        private void AddButtonEntranceAnimation(Button button, int delayMilliseconds)
        {
            // Start with button scaled down and transparent
            var scaleTransform = new ScaleTransform(0.8, 0.8);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            
            button.RenderTransform = transformGroup;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.Opacity = 0;
            
            // Create entrance animation
            var storyboard = new Storyboard();
            storyboard.BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds);
            
            // Scale animation (elastic ease-out like welcome screen)
            var scaleXAnimation = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            
            var scaleYAnimation = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            
            // Opacity animation
            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Set animation targets
            Storyboard.SetTarget(scaleXAnimation, button);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.Children[0].ScaleX"));
            
            Storyboard.SetTarget(scaleYAnimation, button);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.Children[0].ScaleY"));
            
            Storyboard.SetTarget(opacityAnimation, button);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            
            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Children.Add(opacityAnimation);
            
            // Start the animation
            storyboard.Begin();
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
                
                LoggingService.Application.Information("Starting photo capture",
                    ("CurrentPhotoIndex", _currentPhotoIndex + 1),
                    ("PhotoCount", _currentTemplate.PhotoCount));
                
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
                    
                    LoggingService.Application.Information("Photo captured successfully",
                        ("CurrentPhotoIndex", _currentPhotoIndex),
                        ("PhotoCount", _currentTemplate.PhotoCount));
                    
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
                
                LoggingService.Application.Error("Error in photo capture", ex);
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
                
                LoggingService.Application.Information("Photo session completed",
                    ("CapturedPhotoCount", _capturedPhotos.Count));
                
                // Fire event with captured photos - now includes composition request
                PhotosCaptured?.Invoke(this, new PhotosCapturedEventArgs(_currentTemplate!, _capturedPhotos));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error completing photo session", ex);
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
                    LoggingService.Application.Warning("Slow update detected",
                        ("TotalMilliseconds", totalTime.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error in camera preview update", ex);
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
                LoggingService.Application.Error("Camera error occurred", null,
                    ("ErrorMessage", errorMessage));
                    
                // Show retry option for camera errors
                ShowRetryOption();
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

        #region Credits Management

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
                LoggingService.Application.Error("Failed to update credits display", ex);
            }
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
                    LoggingService.Application.Information("Disposing camera capture screen",
                        ("IsCapturing", _isCapturing));
                    
                    // Stop all sounds first
                    SoundService.Instance.StopAllSounds();
                    
                    // Stop all timers first
                    if (_countdownTimer != null)
                    {
                        _countdownTimer.Stop();
                        _countdownTimer = null;
                        LoggingService.Application.Information("CameraCaptureScreen: Countdown timer stopped");
                    }
                    
                    if (_previewTimer != null)
                    {
                        _previewTimer.Stop();
                        _previewTimer = null;
                        LoggingService.Application.Information("CameraCaptureScreen: Preview timer stopped");
                    }
                    
                    if (_autoTriggerTimer != null)
                    {
                        _autoTriggerTimer.Stop();
                        _autoTriggerTimer = null;
                        LoggingService.Application.Information("CameraCaptureScreen: Auto trigger timer stopped");
                    }
                    
                    if (_animationTimer != null)
                    {
                        _animationTimer.Stop();
                        _animationTimer = null;
                        LoggingService.Application.Information("CameraCaptureScreen: Animation timer stopped");
                    }
                    
                    if (_errorHideTimer != null)
                    {
                        _errorHideTimer.Stop();
                        _errorHideTimer = null;
                        LoggingService.Application.Information("CameraCaptureScreen: Error hide timer stopped");
                    }
                    
                    // Unsubscribe from events and dispose camera service
                    if (_cameraService != null)
                    {
                        LoggingService.Application.Information("CameraCaptureScreen: Disposing camera service");
                        _cameraService.PreviewFrameReady -= OnCameraPreviewFrameReady;
                        _cameraService.CameraError -= OnCameraError;
                        _cameraService.Dispose();
                        LoggingService.Application.Information("CameraCaptureScreen: Camera service disposed");
                    }
                    
                    // Clear captured photos list
                    _capturedPhotos?.Clear();
                    
                    LoggingService.Application.Information("Camera capture screen disposed successfully");
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Error("Error during disposal", ex);
                    LoggingService.Application.Error("CameraCaptureScreen ERROR stack trace", null,
                        ("StackTrace", ex.StackTrace ?? "No stack trace available"));
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