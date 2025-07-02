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
            Console.WriteLine($"=== KEYBOARD INPUT SERVICE DEBUG ===");
            Console.WriteLine($"Button clicked: {button?.GetType().Name}");
            Console.WriteLine($"Button content: '{button?.Content}'");
            
            if (button?.Content == null) 
            {
                Console.WriteLine("❌ Button or content is null - ignoring click");
                return;
            }

            var content = button.Content.ToString();
            Console.WriteLine($"Content string: '{content}'");
            
            if (string.IsNullOrEmpty(content)) 
            {
                Console.WriteLine("❌ Content string is null or empty - ignoring click");
                return;
            }

            // Handle special keys with string matching FIRST (before enum parsing)
            switch (content.ToLower())
            {
                case "space":
                case " ":
                    Console.WriteLine("✅ Recognized as space key");
                    OnKeyPressed?.Invoke(" ");
                    break;
                case "tab":
                    Console.WriteLine("✅ Recognized as tab key");
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Tab);
                    break;
                case "enter":
                case "return":
                    Console.WriteLine("✅ Recognized as enter key");
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Enter);
                    break;
                case "backspace":
                case "←":
                    Console.WriteLine("✅ Recognized as backspace key");
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Backspace);
                    break;
                case "shift":
                    Console.WriteLine("✅ Recognized as shift key");
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.Shift);
                    break;
                case "caps":
                case "caps lock":
                    Console.WriteLine("✅ Recognized as caps lock key");
                    OnSpecialKeyPressed?.Invoke(VirtualKeyboardSpecialKey.CapsLock);
                    break;
                case "close":
                case "×":
                    Console.WriteLine("✅ Recognized as close key (handled at UI level)");
                    // Close is handled at the UI level, not as a special key
                    break;
                default:
                    // Try enum parsing only for non-numeric content
                    if (!char.IsDigit(content, 0) && Enum.TryParse<VirtualKeyboardSpecialKey>(content, true, out var specialKey))
                    {
                        Console.WriteLine($"✅ Recognized as special key: {specialKey}");
                        OnSpecialKeyPressed?.Invoke(specialKey);
                        return;
                    }
                    
                    // Regular key press
                    if (content.Length <= MaxKeyNameLength) // Single character or short strings
                    {
                        Console.WriteLine($"✅ Recognized as regular key: '{content}' (firing OnKeyPressed event)");
                        OnKeyPressed?.Invoke(content);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Content too long ({content.Length} > {MaxKeyNameLength}): '{content}' - ignoring");
                    }
                    break;
            }
        }
    }
} 