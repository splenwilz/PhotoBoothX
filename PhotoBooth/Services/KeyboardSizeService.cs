using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public class KeyboardSizeService : IKeyboardSizeService
    {
        public KeyboardSize CurrentSize { get; private set; } = KeyboardSize.Large;

        public void ApplyKeyboardSize(FrameworkElement keyboard, KeyboardSize size)
        {
            CurrentSize = size;
            
            // For the VirtualKeyboard, we don't apply scale transforms here
            // because it has its own specialized sizing logic that handles
            // full-width layouts and proper positioning
            // The scale transforms were interfering with the width calculations
            
            // Just update the size button states
            UpdateSizeButtonStates(keyboard);
        }

        public void UpdateSizeButtonStates(FrameworkElement keyboard)
        {
            // Find size buttons and update their states
            var smallButton = FindChildByName(keyboard, "SmallSizeButton") as Button;
            var mediumButton = FindChildByName(keyboard, "MediumSizeButton") as Button;
            var largeButton = FindChildByName(keyboard, "LargeSizeButton") as Button;

            if (keyboard.TryFindResource("SpecialKeyStyle") is Style normalStyle &&
                keyboard.TryFindResource("ActionKeyStyle") is Style activeStyle)
            {
                // Reset all buttons to normal state
                if (smallButton != null) smallButton.Style = normalStyle;
                if (mediumButton != null) mediumButton.Style = normalStyle;
                if (largeButton != null) largeButton.Style = normalStyle;

                // Highlight the active button
                var activeButton = CurrentSize switch
                {
                    KeyboardSize.Small => smallButton,
                    KeyboardSize.Medium => mediumButton,
                    KeyboardSize.Large => largeButton,
                    _ => largeButton
                };

                if (activeButton != null)
                {
                    activeButton.Style = activeStyle;
                }
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