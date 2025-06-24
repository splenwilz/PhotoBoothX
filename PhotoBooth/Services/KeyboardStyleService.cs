using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public class KeyboardStyleService : IKeyboardStyleService
    {
        public bool IsExtraTransparent { get; private set; }

        public KeyboardStyleService(bool isExtraTransparent = false)
        {
            IsExtraTransparent = isExtraTransparent;
        }

        public void ApplyExtraTransparency(FrameworkElement keyboard)
        {
            if (!IsExtraTransparent) return;

            // Set UserControl background to be transparent
            if (keyboard is UserControl userControl)
            {
                userControl.Background = System.Windows.Media.Brushes.Transparent;
            }
            
            // Find and modify the MainBorder specifically - only make the background transparent
            var mainBorder = FindChildByName(keyboard, "MainBorder") as System.Windows.Controls.Border;
            if (mainBorder != null)
            {
                // Make only the main border background transparent, keep everything else the same
                mainBorder.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(80, 248, 250, 252)); // Semi-transparent background
                    
                // Keep the border visible but slightly transparent
                mainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(128, 229, 231, 235)); // Semi-transparent border
            }

            // Apply transparent button styles - but ONLY on login screens when IsExtraTransparent is true
            ApplyTransparentButtonStyles(keyboard);
        }

        private void ApplyTransparentButtonStyles(FrameworkElement keyboard)
        {
            // Apply transparency to all buttons without changing their design
            ApplyTransparencyToAllButtons(keyboard);
        }

        private void ApplyTransparencyToAllButtons(DependencyObject parent)
        {
            try
            {
                var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is Button button)
                    {
                        // Just make the button transparent, keep all other styling the same
                        button.Opacity = 0.7; // 70% opacity - makes it see-through but still visible
                    }
                    
                    // Recursively apply to children
                    ApplyTransparencyToAllButtons(child);
                }
            }
            catch
            {
                // Ignore visual tree errors
            }
        }

        private FrameworkElement? FindChildByName(DependencyObject parent, string name)
        {
            var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is FrameworkElement element && element.Name == name)
                {
                    return element;
                }

                var result = FindChildByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
} 