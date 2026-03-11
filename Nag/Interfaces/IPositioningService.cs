using Avalonia.Controls;

namespace Nag.Interfaces
{
    public interface IPositioningService
    {
        void PositionBottomRight(Window window);
        void RemoveWindow(Window window);
    }
}
