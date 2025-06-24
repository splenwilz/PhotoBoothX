using System;
using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public class KeyboardInputService : IKeyboardInputService
    {
        public event Action<string>? OnKeyPressed;
        public event Action<VirtualKeyboardSpecialKey>? OnSpecialKeyPressed;

        public void HandleKeyClick(Button button, RoutedEventArgs e)
        {
            if (button?.Content == null) return;

            var content = button.Content.ToString();
            if (string.IsNullOrEmpty(content)) return;

            // Handle special keys
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
                    // Regular key press
                    if (content.Length == 1 || content.Length <= 3) // Single character or short strings
                    {
                        OnKeyPressed?.Invoke(content);
                    }
                    break;
            }
        }
    }
} 