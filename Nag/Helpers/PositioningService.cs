using Avalonia;
using Avalonia.Controls;
using Nag.Interfaces;
using System.Collections.Generic;

namespace Nag.Helpers
{
    public class PositioningService : IPositioningService
    {
        private readonly List<Window> ActiveWindows = new();

        public void PositionBottomRight(Window window)
        {
            var screens = TopLevel.GetTopLevel(window)?.Screens;
            var screen = screens?.Primary;
            if (screen == null) return;
            
            var workArea = screen.WorkingArea;
            double scaling = screen.Scaling;

            if (!ActiveWindows.Contains(window))
            {
                ActiveWindows.Add(window);
            }

            double cumulativeHeightOffset = 0;
            int windowIndex = ActiveWindows.IndexOf(window);
            
            for (int i = 0; i < windowIndex; i++)
            {
                var active = ActiveWindows[i];
                if (active.IsLoaded)
                {
                    double h = active.Bounds.Height > 0 ? active.Bounds.Height : 150;
                    cumulativeHeightOffset += (h * scaling) + (15 * scaling);
                }
            }

            double winHeight = window.Bounds.Height > 0 ? window.Bounds.Height : window.MinHeight;
            if (double.IsNaN(winHeight) || winHeight == 0) winHeight = 150;
            
            double winWidth = window.Bounds.Width > 0 ? window.Bounds.Width : window.Width;
            if (double.IsNaN(winWidth) || winWidth == 0) winWidth = 520;

            int targetLeft = workArea.Right - (int)(winWidth * scaling) - (int)(10 * scaling);
            int targetTop = workArea.Bottom - (int)(winHeight * scaling) - (int)(10 * scaling) - (int)cumulativeHeightOffset;
            
            window.Position = new PixelPoint(targetLeft, targetTop);
            
            window.Topmost = false; 
            window.Topmost = true;
        }

        public void RemoveWindow(Window window)
        {
            if (ActiveWindows.Contains(window))
            {
                ActiveWindows.Remove(window);
                RepositionAll();
            }
        }

        private void RepositionAll()
        {
            for (int i = ActiveWindows.Count - 1; i >= 0; i--)
            {
                PositionBottomRight(ActiveWindows[i]);
            }
            
            for (int i = ActiveWindows.Count - 1; i >= 0; i--)
            {
                var w = ActiveWindows[i];
                w.Topmost = false;
                w.Topmost = true;
            }
        }
    }
}
