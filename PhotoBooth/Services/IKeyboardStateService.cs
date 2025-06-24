using System.Windows;

namespace Photobooth.Services
{
    public interface IKeyboardStateService
    {
        bool IsShiftPressed { get; }
        bool IsCapsLockOn { get; }
        
        void UpdateKeyCase(FrameworkElement keyboard, VirtualKeyboardMode mode);
        void UpdateShiftButtonState(FrameworkElement keyboard);
        void UpdateCapsLockButtonState(FrameworkElement keyboard);
        void ToggleShift();
        void ToggleCapsLock();
    }
} 