using System.Windows;
using System.Windows.Controls;

namespace Photobooth.Services
{
    public interface IKeyboardInputService
    {
        void HandleKeyClick(Button button, RoutedEventArgs e);
        event System.Action<string>? OnKeyPressed;
        event System.Action<VirtualKeyboardSpecialKey>? OnSpecialKeyPressed;
    }
} 