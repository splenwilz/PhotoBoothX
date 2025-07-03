using System;
using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public class KeyboardInputService : IKeyboardInputService
    {
        private const int MaxKeyNameLength = 3;
        
        public event Action<string>? OnKeyPressed;
        public event Action<VirtualKeyboardSpecialKey>? OnSpecialKeyPressed;

        public void HandleKeyClick(Button button, RoutedEventArgs e)
        {
            if (button?.Content == null) 
            {
                return;
            }

            var content = button.Content.ToString();
            
            if (string.IsNullOrEmpty(content)) 
            {
                return;
            }

            // Handle special keys with string matching FIRST (before enum parsing)
            switch (content.ToLower())
            {
                case "space":
                case " ":
                    OnKeyPressed?.Invoke(" ");
                    break;
                case "tab":
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Tab);
                    break;
                case "enter":
                case "return":
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Enter);
                    break;
                case "backspace":
                case "←":
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Backspace);
                    break;
                case "shift":
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Shift);
                    break;
                case "caps":
                case "caps lock":
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.CapsLock);
                    break;
                case "close":
                case "×":
                    // Close is handled at the UI level, not as a special key
                    break;
                default:
                    // Try enum parsing only for non-numeric content
                    if (!char.IsDigit(content, 0) && Enum.TryParse<VirtualKeyboardSpecialKey>(content, true, out var specialKey))
                    {
                        OnSpecialKeyPressed?.Invoke(specialKey);
                        return;
                    }
                    
                    // Regular key press
                    if (content.Length <= MaxKeyNameLength) // Single character or short strings
                    {
                        OnKeyPressed?.Invoke(content);
                    }
                    break;
            }
        }
    }
} 