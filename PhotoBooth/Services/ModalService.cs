using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Photobooth.Services
{
    /// <summary>
    /// Service for managing in-window modal dialogs with keyboard-aware positioning
    /// </summary>
    public class ModalService
    {
        private static ModalService? _instance;
        private Grid? _modalOverlayContainer;
        private ContentControl? _modalContentContainer;
        private Border? _modalBackdrop;
        private bool _isKeyboardVisible = false;
        private TranslateTransform? _modalTransform;

        public static ModalService Instance => _instance ??= new ModalService();

        private ModalService() 
        {
            // Subscribe to virtual keyboard events
            VirtualKeyboardService.Instance.KeyboardVisibilityChanged += OnKeyboardVisibilityChanged;
        }

        /// <summary>
        /// Initialize the modal service with the main window containers
        /// </summary>
        public void Initialize(Grid modalOverlayContainer, ContentControl modalContentContainer, Border modalBackdrop)
        {
            _modalOverlayContainer = modalOverlayContainer;
            _modalContentContainer = modalContentContainer;
            _modalBackdrop = modalBackdrop;

            // Set up transform for modal positioning
            if (_modalContentContainer != null)
            {
                _modalTransform = new TranslateTransform();
                _modalContentContainer.RenderTransform = _modalTransform;
            }
        }

        /// <summary>
        /// Handle virtual keyboard visibility changes
        /// </summary>
        private void OnKeyboardVisibilityChanged(object? sender, bool isVisible)
        {
            try
            {
                _isKeyboardVisible = isVisible;
                
                // Only adjust if modal is currently shown
                if (IsModalShown)
                {
                    AdjustModalPosition();
                }
            }
            catch
            {
                // Log to proper logging service if needed
            }
        }

        /// <summary>
        /// Adjust modal position based on keyboard visibility
        /// </summary>
        private void AdjustModalPosition()
        {
            if (_modalContentContainer == null || _modalTransform == null) return;

            try
            {
                double targetY = 0;

                if (_isKeyboardVisible)
                {
                    // Move modal up when keyboard is visible
                    // Keyboard is typically around 300px high, so move modal up by 150px
                    targetY = -150;
                }
                else
                {
                    // Return to center when keyboard is hidden
                    targetY = 0;
                }

                // Animate the position change
                var animation = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                _modalTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            }
            catch
            {
                // Log to proper logging service if needed
            }
        }

        /// <summary>
        /// Show a UserControl as an in-window modal
        /// </summary>
        public void ShowModal(UserControl modalContent)
        {
            if (_modalOverlayContainer == null || _modalContentContainer == null)
            {
                return;
            }

            try
            {
                // Set the modal content
                _modalContentContainer.Content = modalContent;
                
                // Show the overlay
                _modalOverlayContainer.Visibility = Visibility.Visible;
                
                // Adjust position based on current keyboard state
                AdjustModalPosition();
                
                // Animate in
                AnimateModalIn();
            }
            catch
            {
                // Log to proper logging service if needed
            }
        }

        /// <summary>
        /// Hide the current modal
        /// </summary>
        public void HideModal()
        {
            if (_modalOverlayContainer == null || _modalContentContainer == null)
            {
                return;
            }

            try
            {
                // Close virtual keyboard when modal is hidden
                VirtualKeyboardService.Instance.HideKeyboard();
                
                // Animate out
                AnimateModalOut(() =>
                {
                    // Hide the overlay
                    _modalOverlayContainer.Visibility = Visibility.Collapsed;
                    
                    // Clear the content
                    _modalContentContainer.Content = null;
                    
                    // Reset transform
                    if (_modalTransform != null)
                    {
                        _modalTransform.Y = 0;
                    }
                });
            }
            catch
            {
                // Log to proper logging service if needed
            }
        }

        /// <summary>
        /// Check if a modal is currently shown
        /// </summary>
        public bool IsModalShown => _modalOverlayContainer?.Visibility == Visibility.Visible;

        private void AnimateModalIn()
        {
            if (_modalContentContainer == null) return;

            // Scale and fade in animation
            var scaleTransform = new System.Windows.Media.ScaleTransform(0.8, 0.8);
            var transformGroup = new System.Windows.Media.TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(_modalTransform ?? new TranslateTransform());
            
            _modalContentContainer.RenderTransform = transformGroup;
            _modalContentContainer.RenderTransformOrigin = new Point(0.5, 0.5);
            _modalContentContainer.Opacity = 0;

            var storyboard = new Storyboard();

            // Scale animation
            var scaleXAnimation = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleYAnimation = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            // Opacity animation
            var opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            Storyboard.SetTarget(scaleXAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.Children[0].ScaleX"));
            
            Storyboard.SetTarget(scaleYAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.Children[0].ScaleY"));
            
            Storyboard.SetTarget(opacityAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Children.Add(opacityAnimation);

            storyboard.Begin();
        }

        private void AnimateModalOut(Action onComplete)
        {
            if (_modalContentContainer == null)
            {
                onComplete?.Invoke();
                return;
            }

            var storyboard = new Storyboard();

            // Scale animation
            var scaleXAnimation = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleYAnimation = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            // Opacity animation
            var opacityAnimation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));

            Storyboard.SetTarget(scaleXAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.Children[0].ScaleX"));
            
            Storyboard.SetTarget(scaleYAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.Children[0].ScaleY"));
            
            Storyboard.SetTarget(opacityAnimation, _modalContentContainer);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Children.Add(opacityAnimation);

            storyboard.Completed += (s, e) => onComplete?.Invoke();
            storyboard.Begin();
        }
    }
} 
