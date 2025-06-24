using System.Windows;

namespace Photobooth.Services
{
    public interface IKeyboardStyleService
    {
        bool IsExtraTransparent { get; }
        void ApplyExtraTransparency(FrameworkElement keyboard);
    }
} 