using System.Windows;
using System.Windows.Controls;
using Photobooth.Services;

namespace Photobooth.Behaviors
{
    /// <summary>
    /// Attached behavior to automatically show virtual keyboard for input controls
    /// </summary>
    public static class VirtualKeyboardBehavior
    {
        #region Attached Property

        public static readonly DependencyProperty EnableVirtualKeyboardProperty =
            DependencyProperty.RegisterAttached(
                "EnableVirtualKeyboard",
                typeof(bool),
                typeof(VirtualKeyboardBehavior),
                new PropertyMetadata(false, OnEnableVirtualKeyboardChanged));

        public static bool GetEnableVirtualKeyboard(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableVirtualKeyboardProperty);
        }

        public static void SetEnableVirtualKeyboard(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableVirtualKeyboardProperty, value);
        }

        #endregion

        #region Event Handlers

        private static void OnEnableVirtualKeyboardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Control control)
            {
                if ((bool)e.NewValue)
                {
                    // Enable virtual keyboard
                    control.GotFocus += Control_GotFocus;
                    control.LostFocus += Control_LostFocus;
                }
                else
                {
                    // Disable virtual keyboard
                    control.GotFocus -= Control_GotFocus;
                    control.LostFocus -= Control_LostFocus;
                }
            }
        }

        private static void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                var parentWindow = Window.GetWindow(control);
                if (parentWindow != null)
                {
                    VirtualKeyboardService.Instance.ShowKeyboard(control, parentWindow);
                }
            }
        }

        private static void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            // Don't hide immediately on lost focus - let user click keyboard buttons
            // The keyboard will hide when user clicks close or outside
        }

        #endregion
    }
} 