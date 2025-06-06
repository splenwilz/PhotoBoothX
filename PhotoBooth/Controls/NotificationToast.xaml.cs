using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Photobooth.Controls
{
    public partial class NotificationToast : UserControl
    {
        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }

        public event EventHandler? NotificationClosed;

        private DispatcherTimer? _autoCloseTimer;

        public NotificationToast()
        {
            InitializeComponent();
        }

        public void ShowNotification(string title, string message, NotificationType type, int autoCloseSeconds = 5)
        {
            // Set content
            TitleText.Text = title;
            MessageText.Text = message;

            // Configure appearance based on type
            ConfigureNotificationType(type);

            // Show with animation
            AnimateIn();

            // Auto-close timer
            if (autoCloseSeconds > 0)
            {
                _autoCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(autoCloseSeconds)
                };
                _autoCloseTimer.Tick += (s, e) =>
                {
                    _autoCloseTimer.Stop();
                    CloseNotification();
                };
                _autoCloseTimer.Start();
            }
        }

        private void ConfigureNotificationType(NotificationType type)
        {
            Style borderStyle;
            string icon;
            Brush iconBackground;

            switch (type)
            {
                case NotificationType.Success:
                    borderStyle = (Style)FindResource("SuccessNotificationStyle");
                    icon = "✓";
                    iconBackground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                    TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x6D, 0x34));
                    break;
                case NotificationType.Error:
                    borderStyle = (Style)FindResource("ErrorNotificationStyle");
                    icon = "✕";
                    iconBackground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    break;
                case NotificationType.Warning:
                    borderStyle = (Style)FindResource("WarningNotificationStyle");
                    icon = "⚠";
                    iconBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x73, 0x06));
                    break;
                case NotificationType.Info:
                default:
                    borderStyle = (Style)FindResource("InfoNotificationStyle");
                    icon = "ℹ";
                    iconBackground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                    TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF));
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));
                    break;
            }

            NotificationBorder.Style = borderStyle;
            IconText.Text = icon;
            IconBorder.Background = iconBackground;
        }

        private void AnimateIn()
        {
            var slideAnimation = new DoubleAnimation
            {
                From = -100,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
        }

        private void AnimateOut()
        {
            var slideAnimation = new DoubleAnimation
            {
                From = 0,
                To = -100,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            slideAnimation.Completed += (s, e) =>
            {
                NotificationClosed?.Invoke(this, EventArgs.Empty);
            };

            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
        }

        private void CloseNotification_Click(object sender, RoutedEventArgs e)
        {
            CloseNotification();
        }

        private void CloseNotification_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CloseNotification();
        }

        public void CloseNotification()
        {
            _autoCloseTimer?.Stop();
            AnimateOut();
        }
    }
} 