using System.Windows;
using System.Windows.Controls;
using Photobooth.Services;
using System;
using System.Reflection;

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
                    AttachEventHandlers(control);
                    control.Loaded += Control_Loaded;
                    control.Unloaded += Control_Unloaded;
                }
                else
                {
                    // Disable virtual keyboard
                    DetachEventHandlers(control);
                    control.Loaded -= Control_Loaded;
                    control.Unloaded -= Control_Unloaded;
                }
            }
        }

        private static async void Control_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Control control)
                {
                    Console.WriteLine($"=== VirtualKeyboardBehavior.Control_Loaded: {control.Name} ===");
                    // Re-attach event handlers when control is loaded (after being unloaded)
                    AttachEventHandlers(control);
                    
                    // Subscribe to window deactivation to hide keyboard when app loses focus
                    var window = Window.GetWindow(control);
                    if (window != null)
                    {
                        // De-dupe subscription in case of multiple loads
                        window.Deactivated -= OnWindowDeactivated;
                        window.Deactivated += OnWindowDeactivated;
                    }
                    
                    // Check if control already has focus after re-attaching handlers
                    if (control.IsFocused || control.IsKeyboardFocused)
                    {
                        Console.WriteLine($"Control {control.Name} already has focus, manually triggering keyboard logic");
                        // Manually trigger the virtual keyboard logic since GotFocus won't fire
                        var parentWindow = Window.GetWindow(control);
                        if (parentWindow != null)
                        {
                            Console.WriteLine($"Manually calling VirtualKeyboardService.ShowKeyboardAsync for {control.Name}");
                            await VirtualKeyboardService.Instance.ShowKeyboardAsync(control, parentWindow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error in VirtualKeyboardBehavior Control_Loaded", ex);
            }
        }

        private static void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                Console.WriteLine($"=== VirtualKeyboardBehavior.Control_Unloaded: {control.Name} ===");
                // Detach event handlers when control is unloaded
                DetachEventHandlers(control);
                
                // Unsubscribe from window events to avoid leaks
                var window = Window.GetWindow(control);
                if (window != null)
                {
                    window.Deactivated -= OnWindowDeactivated;
                }
            }
        }

        private static void AttachEventHandlers(Control control)
        {
            // Remove first to avoid duplicate handlers
                control.GotFocus -= Control_GotFocus;
                control.LostFocus -= Control_LostFocus;
            
            // Attach handlers
            control.GotFocus += Control_GotFocus;
            control.LostFocus += Control_LostFocus;
        }

        private static void DetachEventHandlers(Control control)
        {
            Console.WriteLine($"=== VirtualKeyboardBehavior.DetachEventHandlers: {control.Name} ===");
            control.GotFocus -= Control_GotFocus;
            control.LostFocus -= Control_LostFocus;
            Console.WriteLine($"Detached handlers for {control.Name}");
        }

        private static async void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Control control)
                {
                    Console.WriteLine($"=== VirtualKeyboardBehavior.Control_GotFocus: {control.Name} ===");
                    
                    // IMMEDIATELY update the active input in the virtual keyboard service
                    // This ensures the virtual keyboard knows which control is active before any key presses
                    var virtualKeyboardService = VirtualKeyboardService.Instance;
                    if (virtualKeyboardService != null)
                    {
                        // Use reflection to directly set the _activeInput field
                        var field = typeof(VirtualKeyboardService).GetField("_activeInput", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(virtualKeyboardService, control);
                            Console.WriteLine($"=== VirtualKeyboardBehavior: Directly set _activeInput to {control.Name} ===");
                        }
                    }
                    
                    // Find the parent window
                    var parentWindow = Window.GetWindow(control);
                    if (parentWindow != null)
                    {
                        Console.WriteLine($"Calling VirtualKeyboardService.ShowKeyboardAsync for {control.Name}");
                        // Show virtual keyboard for this control
                        await VirtualKeyboardService.Instance.ShowKeyboardAsync(control, parentWindow);
                        Console.WriteLine($"VirtualKeyboardService.ShowKeyboardAsync completed for {control.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"No parent window found for {control.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error in VirtualKeyboardBehavior Control_GotFocus", ex);
            }
        }

        private static void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            // Note: We don't hide the keyboard on LostFocus because the user might be 
            // switching between input controls and we want to keep the keyboard visible
        }

        private static void OnWindowDeactivated(object? sender, EventArgs e)
        {
            try
            {
                VirtualKeyboardService.Instance.HideKeyboard();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error hiding virtual keyboard on window deactivation", ex);
            }
        }

        #endregion
    }
} 