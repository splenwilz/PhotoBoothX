using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Photobooth.Controls;

namespace Photobooth.Services
{
    public class NotificationService
    {
        private static NotificationService? _instance;
        private static readonly object _lock = new object();
        private readonly List<NotificationToast> _activeNotifications = new();
        private Panel? _notificationContainer;

        public static NotificationService Instance 
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new NotificationService();
                    }
                }
                return _instance;
            }
        }

        private NotificationService() { }

        /// <summary>
        /// Initialize the notification service with a container panel (usually from MainWindow)
        /// </summary>
        public void Initialize(Panel notificationContainer)
        {
            _notificationContainer = notificationContainer;
        }

        /// <summary>
        /// Show a success notification
        /// </summary>
        public void ShowSuccess(string title, string message, int autoCloseSeconds = 5)
        {
            ShowNotification(title, message, NotificationToast.NotificationType.Success, autoCloseSeconds);
        }

        /// <summary>
        /// Show an error notification
        /// </summary>
        public void ShowError(string title, string message, int autoCloseSeconds = 8)
        {
            ShowNotification(title, message, NotificationToast.NotificationType.Error, autoCloseSeconds);
        }

        /// <summary>
        /// Show a warning notification
        /// </summary>
        public void ShowWarning(string title, string message, int autoCloseSeconds = 6)
        {
            ShowNotification(title, message, NotificationToast.NotificationType.Warning, autoCloseSeconds);
        }

        /// <summary>
        /// Show an info notification
        /// </summary>
        public void ShowInfo(string title, string message, int autoCloseSeconds = 5)
        {
            ShowNotification(title, message, NotificationToast.NotificationType.Info, autoCloseSeconds);
        }

        /// <summary>
        /// Show a notification with specified type
        /// </summary>
        private void ShowNotification(string title, string message, NotificationToast.NotificationType type, int autoCloseSeconds)
        {
            if (_notificationContainer == null)
            {
                // Fallback to old MessageBox if not initialized
                MessageBox.Show($"{title}\n\n{message}", "Notification", MessageBoxButton.OK, 
                    type == NotificationToast.NotificationType.Error ? MessageBoxImage.Error :
                    type == NotificationToast.NotificationType.Warning ? MessageBoxImage.Warning :
                    MessageBoxImage.Information);
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                var notification = new NotificationToast();
                
                // Position the notification
                Panel.SetZIndex(notification, 9999);
                notification.HorizontalAlignment = HorizontalAlignment.Right;
                notification.VerticalAlignment = VerticalAlignment.Top;
                notification.Margin = new Thickness(0, 16 + (_activeNotifications.Count * 90), 16, 0);

                // Handle notification closed event
                notification.NotificationClosed += (s, e) =>
                {
                    // Remove from both the tracking list and the UI container
                    _activeNotifications.Remove(notification);
                    _notificationContainer.Children.Remove(notification);
                    RepositionNotifications();
                };

                // Add to container and show
                _notificationContainer.Children.Add(notification);
                _activeNotifications.Add(notification);
                
                notification.ShowNotification(title, message, type, autoCloseSeconds);
            });
        }

        /// <summary>
        /// Reposition remaining notifications when one is closed
        /// </summary>
        private void RepositionNotifications()
        {
            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                var notification = _activeNotifications[i];
                notification.Margin = new Thickness(0, 16 + (i * 90), 16, 0);
            }
        }

        /// <summary>
        /// Clear all active notifications
        /// </summary>
        public void ClearAll()
        {
            var notificationsToClose = _activeNotifications.ToList();
            foreach (var notification in notificationsToClose)
            {
                if (_notificationContainer?.Children.Contains(notification) == true)
                {
                    _notificationContainer.Children.Remove(notification);
                }
            }
            _activeNotifications.Clear();
        }

        /// <summary>
        /// Quick helper methods for common scenarios
        /// </summary>
        public static class Quick
        {
            public static void Success(string message) => Instance.ShowSuccess("Success", message);
            public static void Error(string message) => Instance.ShowError("Error", message);
            public static void Warning(string message) => Instance.ShowWarning("Warning", message);
            public static void Info(string message) => Instance.ShowInfo("Info", message);
            
            public static void SettingsSaved() => Instance.ShowSuccess("Settings Updated", "Your settings have been saved successfully!");
            public static void SettingsError(string error) => Instance.ShowError("Settings Error", $"Failed to save settings: {error}");
            
            public static void UserCreated(string username) => Instance.ShowSuccess("User Created", $"User '{username}' has been created successfully!");
            public static void UserDeleted(string username) => Instance.ShowSuccess("User Deleted", $"User '{username}' has been deleted successfully!");
            
            public static void LogoUploaded() => Instance.ShowSuccess("Logo Uploaded", "Logo uploaded successfully! Click 'Save Settings' to apply changes.");
            public static void SupplyUpdated() => Instance.ShowSuccess("Supply Updated", "New roll installed successfully!");
            
            // Additional helpers for common scenarios
            public static void PasswordUpdated() => Instance.ShowSuccess("Password Updated", "Your password has been changed successfully!");
            public static void SessionInvalid() => Instance.ShowWarning("Session Invalid", "Your session has expired. Please login again.");
            public static void AccessDenied() => Instance.ShowWarning("Access Denied", "You don't have permission to perform this action.");
            public static void ValidationError(string field) => Instance.ShowWarning("Validation Error", $"{field} is required.");
        }
    }
} 