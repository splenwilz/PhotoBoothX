using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Photobooth
{
    /// <summary>
    /// Welcome screen - matches the original TypeScript design exactly
    /// Features accurate colors, gradients, animations, and interactive effects
    /// </summary>
    public partial class WelcomeScreen : UserControl
    {
        #region Events

        /// <summary>
        /// Event fired when user wants to start using the photobooth
        /// </summary>
        public event EventHandler? StartButtonClicked;

        /// <summary>
        /// Event fired when admin access sequence is completed
        /// </summary>
        public event EventHandler? AdminAccessRequested;

        #endregion

        #region Private Fields

        private DispatcherTimer? animationTimer;
        private Random random = new Random();

        // Admin access detection (5-tap sequence)
        private int adminTapCount = 0;
        private const int ADMIN_TAP_SEQUENCE_COUNT = 5;
        private const double ADMIN_TAP_TIME_WINDOW = 3.0; // seconds
        private DispatcherTimer? adminTapTimer;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructor - initializes the welcome screen with accurate styling and animations
        /// </summary>
        public WelcomeScreen()
        {
            InitializeComponent();
            InitializeAnimations();
            CreateFloatingParticles();
            CreateSparkles();
            InitializeAdminTapDetection();
        }

        /// <summary>
        /// Sets up all visual animations to match the original design
        /// </summary>
        private void InitializeAnimations()
        {
            StartHandAnimation();
            StartFloatingOrbAnimations();
            StartParticleAnimations();
            StartSparkleAnimations();
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Creates the gentle bounce animation for the hand icon
        /// animate-gentle-bounce
        /// </summary>
        private void StartHandAnimation()
        {
            var handTransform = HandIcon.RenderTransform as TranslateTransform;
            if (handTransform != null)
            {
                var storyboard = new Storyboard();
                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = -8,
                    Duration = TimeSpan.FromSeconds(1.5),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                Storyboard.SetTarget(animation, handTransform);
                Storyboard.SetTargetProperty(animation, new PropertyPath("Y"));
                storyboard.Children.Add(animation);
                storyboard.Begin();
            }

            // Pulse animation for hand glow
            var pulseStoryboard = new Storyboard();
            var pulseAnimation = new DoubleAnimation
            {
                From = 0.3,
                To = 0.6,
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(pulseAnimation, HandGlow);
            Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath("Opacity"));
            pulseStoryboard.Children.Add(pulseAnimation);
            pulseStoryboard.Begin();
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
                    var currentTop = Canvas.GetTop(particle);
                    var newTop = currentTop - 0.3;

                    if (newTop < -particle.Height)
                    {
                        newTop = 1080 + particle.Height;
                        Canvas.SetLeft(particle, random.Next(0, 1920));
                    }

                    Canvas.SetTop(particle, newTop);
                }
            };

            animationTimer.Start();
        }

        /// <summary>
        /// Creates magical sparkles around interactive elements
        /// Sparkles around touch zone and hand icon
        /// </summary>
        private void CreateSparkles()
        {
            // Sparkles around touch zone
            for (int i = 0; i < 12; i++)
            {
                var sparkle = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = new SolidColorBrush(Color.FromRgb(103, 232, 249)) // cyan-300
                };

                double angle = i * 30 * Math.PI / 180;
                Canvas.SetLeft(sparkle, 160 + Math.Cos(angle) * 120);
                Canvas.SetTop(sparkle, 160 + Math.Sin(angle) * 120);

                SparklesCanvas.Children.Add(sparkle);
            }

            // Sparkles around hand
            CreateHandSparkles();
        }

        /// <summary>
        /// Creates sparkles around the hand icon
        /// </summary>
        private void CreateHandSparkles()
        {
            var sparkleColors = new[]
            {
                Color.FromRgb(252, 211, 77), // yellow-300
                Color.FromRgb(248, 113, 113), // pink-300  
                Color.FromRgb(103, 232, 249)  // cyan-300
            };

            var sparklePositions = new[]
            {
                new { Left = 50.0, Top = -8.0, Size = 8.0 },    // top-right
                new { Left = -24.0, Top = 48.0, Size = 6.0 },   // bottom-left  
                new { Left = -32.0, Top = 8.0, Size = 4.0 }     // left
            };

            for (int i = 0; i < 3; i++)
            {
                var sparkle = new Ellipse
                {
                    Width = sparklePositions[i].Size,
                    Height = sparklePositions[i].Size,
                    Fill = new SolidColorBrush(sparkleColors[i])
                };

                Canvas.SetLeft(sparkle, sparklePositions[i].Left);
                Canvas.SetTop(sparkle, sparklePositions[i].Top);

                HandSparklesCanvas.Children.Add(sparkle);
            }
        }

        /// <summary>
        /// Starts twinkling animations for sparkles
        /// animate-twinkle
        /// </summary>
        private void StartSparkleAnimations()
        {
            foreach (Canvas canvas in new[] { SparklesCanvas, HandSparklesCanvas })
            {
                foreach (Ellipse sparkle in canvas.Children)
                {
                    var storyboard = new Storyboard();
                    var animation = new DoubleAnimation
                    {
                        From = 0.6,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.8 + random.NextDouble() * 0.4),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(random.NextDouble() * 2)
                    };

                    Storyboard.SetTarget(animation, sparkle);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
                    storyboard.Children.Add(animation);
                    storyboard.Begin();
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles touch/click anywhere on screen to start
        /// </summary>
        private void StartOverlay_Click(object sender, RoutedEventArgs e)
        {
            OnStartButtonClicked();
        }

        /// <summary>
        /// Handles product category button clicks
        /// </summary>
        private void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string productType)
            {
                OnStartButtonClicked();
            }
        }

        /// <summary>
        /// Raises the StartButtonClicked event
        /// </summary>
        private void OnStartButtonClicked()
        {
            StartButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Admin Access

        /// <summary>
        /// Initialize the admin tap detection system
        /// </summary>
        private void InitializeAdminTapDetection()
        {
            adminTapTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ADMIN_TAP_TIME_WINDOW)
            };
            adminTapTimer.Tick += AdminTapTimer_Tick;
        }

        /// <summary>
        /// Handles taps on the admin access zone (top-left corner)
        /// </summary>
        private void AdminAccessZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                adminTapCount++;
                System.Diagnostics.Debug.WriteLine($"Admin tap {adminTapCount}/{ADMIN_TAP_SEQUENCE_COUNT}");

                // Visual feedback for the tap
                TriggerAdminIconPressEffect();

                // Reset timer
                adminTapTimer?.Stop();
                adminTapTimer?.Start();

                // Check if sequence is complete
                if (adminTapCount >= ADMIN_TAP_SEQUENCE_COUNT)
                {
                    ResetAdminTapSequence();
                    System.Diagnostics.Debug.WriteLine("Admin access sequence completed!");
                    
                    // Fire admin access event
                    AdminAccessRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin tap detection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset admin tap count when timer expires
        /// </summary>
        private void AdminTapTimer_Tick(object? sender, EventArgs e)
        {
            adminTapTimer?.Stop();
            adminTapCount = 0;
            System.Diagnostics.Debug.WriteLine("Admin tap sequence reset (timeout)");
        }

        /// <summary>
        /// Reset the admin tap sequence
        /// </summary>
        private void ResetAdminTapSequence()
        {
            adminTapTimer?.Stop();
            adminTapCount = 0;
        }

        /// <summary>
        /// Triggers a visual press effect on the admin icon
        /// </summary>
        private void TriggerAdminIconPressEffect()
        {
            try
            {
                // Create press animation (scale down then back up)
                var scaleDownAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.8,
                    Duration = TimeSpan.FromMilliseconds(80),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                var scaleUpAnimation = new DoubleAnimation
                {
                    From = 0.8,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(120),
                    BeginTime = TimeSpan.FromMilliseconds(80),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // Create opacity flash (briefly more visible)
                var flashAnimation = new DoubleAnimation
                {
                    From = 0.2,
                    To = 0.7,
                    Duration = TimeSpan.FromMilliseconds(100),
                    AutoReverse = true
                };

                // Apply animations
                AdminIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownAnimation);
                AdminIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDownAnimation);
                AdminIcon.BeginAnimation(OpacityProperty, flashAnimation);

                AdminIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation);
                AdminIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpAnimation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Admin icon press effect error: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup animations and resources
        /// </summary>
        public void Cleanup()
        {
            animationTimer?.Stop();
            animationTimer = null;
            
            // Cleanup admin tap timer
            if (adminTapTimer != null)
            {
                adminTapTimer.Stop();
                adminTapTimer.Tick -= AdminTapTimer_Tick;
                adminTapTimer = null;
            }
        }

        #endregion
    }
}