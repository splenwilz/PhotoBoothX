using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public class KeyboardStateService : IKeyboardStateService
    {
        // Static readonly dictionary for better performance
        private static readonly Dictionary<string, string> ShiftMappings = new Dictionary<string, string>
        {
            // Numbers to symbols
            {"1", "!"}, {"2", "@"}, {"3", "#"}, {"4", "$"}, {"5", "%"},
            {"6", "^"}, {"7", "&"}, {"8", "*"}, {"9", "("}, {"0", ")"},
            
            // Punctuation marks
            {"-", "_"}, {"=", "+"}, {"[", "{"}, {"]", "}"},
            {";", ":"}, {"'", "\""}, {",", "<"}, {".", ">"}, {"/", "?"},
            
            // Reverse mappings for when shift is released
            {"!", "1"}, {"@", "2"}, {"#", "3"}, {"$", "4"}, {"%", "5"},
            {"^", "6"}, {"&", "7"}, {"*", "8"}, {"(", "9"}, {")", "0"},
            {"_", "-"}, {"+", "="}, {"{", "["}, {"}", "]"},
            {":", ";"}, {"\"", "'"}, {"<", ","}, {">", "."}, {"?", "/"}
        };

        public bool IsShiftPressed { get; private set; } = false;
        public bool IsCapsLockOn { get; private set; } = false;

        public void ToggleShift()
        {
            IsShiftPressed = !IsShiftPressed;
        }

        public void ToggleCapsLock()
        {
            IsCapsLockOn = !IsCapsLockOn;
        }

        public void UpdateKeyCase(Control keyboard, VirtualKeyboardMode mode)
        {
            if (mode != VirtualKeyboardMode.Text && mode != VirtualKeyboardMode.Password)
                return;

            bool shouldBeUppercase = IsCapsLockOn || IsShiftPressed;

            // Find the text mode layout
            var textModeLayout = FindChildByName(keyboard, "TextModeLayout");
            if (textModeLayout == null) return;

            // Update all letter keys
            var letterKeyNames = new[]
            {
                "KeyQ", "KeyW", "KeyE", "KeyR", "KeyT", "KeyY", "KeyU", "KeyI", "KeyO", "KeyP",
                "KeyA", "KeyS", "KeyD", "KeyF", "KeyG", "KeyH", "KeyJ", "KeyK", "KeyL",
                "KeyZ", "KeyX", "KeyC", "KeyV", "KeyB", "KeyN", "KeyM"
            };

            foreach (var keyName in letterKeyNames)
            {
                if (FindChildByName(textModeLayout, keyName) is Button key && key.Content != null)
                {
                    var letter = key.Content.ToString();
                    if (!string.IsNullOrEmpty(letter))
                    {
                        key.Content = shouldBeUppercase ? letter.ToUpper() : letter.ToLower();
                    }
                }
            }

            // Update number and punctuation keys with shift mappings
            UpdateNumberAndPunctuationKeys(textModeLayout);

            // Reset shift after use (but not caps lock)
            if (IsShiftPressed)
            {
                IsShiftPressed = false;
            }
        }

        public void UpdateShiftButtonState(Control keyboard)
        {
            var shiftButton = FindChildByName(keyboard, "ShiftButton") as Button;
            if (shiftButton == null) return;

            var normalStyle = keyboard.TryFindResource("SpecialKeyStyle") as Style;
            var activeStyle = keyboard.TryFindResource("ActionKeyStyle") as Style;
            
            if (normalStyle == null || activeStyle == null)
            {
                // Log warning or use default styles
                return;
            }
            
            shiftButton.Style = IsShiftPressed ? activeStyle : normalStyle;
        }

        public void UpdateCapsLockButtonState(Control keyboard)
        {
            var capsLockButton = FindChildByName(keyboard, "CapsLockButton") as Button;
            if (capsLockButton == null) return;

            var normalStyle = keyboard.TryFindResource("SpecialKeyStyle") as Style;
            var activeStyle = keyboard.TryFindResource("ActionKeyStyle") as Style;
            
            if (normalStyle == null || activeStyle == null)
            {
                // Log warning or use default styles
                return;
            }
            
            capsLockButton.Style = IsCapsLockOn ? activeStyle : normalStyle;
        }

        private void UpdateNumberAndPunctuationKeys(FrameworkElement textModeLayout)
        {
            UpdateButtonsInContainer(textModeLayout, ShiftMappings);
        }

        private void UpdateButtonsInContainer(DependencyObject container, Dictionary<string, string> mappings)
        {
            try
            {
                var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(container);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(container, i);
                    
                    if (child is Button button && button.Content != null)
                    {
                        var currentContent = button.Content.ToString();
                        if (!string.IsNullOrEmpty(currentContent))
                        {
                            // Skip special buttons (they have longer text content)
                            if (currentContent.Length == 1 && mappings.ContainsKey(currentContent))
                            {
                                // Only apply shift mapping if shift is pressed
                                if (IsShiftPressed)
                                {
                                    button.Content = mappings[currentContent];
                                }
                                else if (!IsShiftPressed && mappings.ContainsValue(currentContent))
                                {
                                    // Revert shifted character back to normal
                                    foreach (var kvp in mappings)
                                    {
                                        if (kvp.Value == currentContent && !IsShiftedCharacter(kvp.Key))
                                        {
                                            button.Content = kvp.Key;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Recursively search children
                    UpdateButtonsInContainer(child, mappings);
                }
            }
            catch (System.Exception ex)
            {
                // Log the specific error for debugging while avoiding crashes
                System.Diagnostics.Debug.WriteLine($"Visual tree traversal error: {ex.Message}");
            }
        }

        private bool IsShiftedCharacter(string character)
        {
            var shiftedChars = new[] { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "{", "}", ":", "\"", "<", ">", "?" };
            return System.Array.Exists(shiftedChars, c => c == character);
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