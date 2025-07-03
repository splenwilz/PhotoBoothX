using System.Windows;

namespace Photobooth.Services
{
    public enum KeyboardSize
    {
        Small,
        Medium,
        Large
    }

    public interface IKeyboardSizeService
    {
        KeyboardSize CurrentSize { get; }
        void ApplyKeyboardSize(FrameworkElement keyboard, KeyboardSize size);
        void UpdateSizeButtonStates(FrameworkElement keyboard);
    }
} 