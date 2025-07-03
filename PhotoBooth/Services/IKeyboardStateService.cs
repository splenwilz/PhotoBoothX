using System.Windows.Controls;

namespace Photobooth.Services
{
    public interface IKeyboardStateService
    {
        bool IsShiftPressed { get; }
        bool IsCapsLockOn { get; }
        
        void UpdateKeyCase(Control keyboard, VirtualKeyboardMode mode);
        void UpdateShiftButtonState(Control keyboard);
        void UpdateCapsLockButtonState(Control keyboard);
        void ToggleShift();
        void ToggleCapsLock();
    }
} 