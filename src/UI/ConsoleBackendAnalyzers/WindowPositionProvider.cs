//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

#if PLATFORM_WINDOWS
using System.Windows;
#endif
using Xwt;
using Antmicro.Renode.Utilities;
using Point = Xwt.Point;

namespace Antmicro.Renode.UI
{
    public class WindowPositionProvider
    {
        static WindowPositionProvider()
        {
            Instance = new WindowPositionProvider();
        }

        public static WindowPositionProvider Instance { get; private set; }

        public Point GetNextPosition()
        {
            lock(innerLock)
            {
                nextPosition = SnapToViewport(nextPosition);
                var result = nextPosition;
                nextPosition.X += offset.X;
                nextPosition.Y += offset.Y;
                return result;
            }
        }

        private Point SnapToViewport(Point position)
        {
            if(!ConfigurationManager.Instance.Get("termsharp", "window-allow-outside-viewport", false))
            {
                var currentScreen = Desktop.GetScreenAtLocation(position) ?? Desktop.PrimaryScreen;
                if(!currentScreen.VisibleBounds.Contains(position))
                {
                    position.X = currentScreen.VisibleBounds.X;
                    position.Y = currentScreen.VisibleBounds.Y;
#if PLATFORM_WINDOWS
                    position.X += SystemParameters.BorderWidth;
                    position.Y += SystemParameters.WindowCaptionHeight + SystemParameters.ResizeFrameHorizontalBorderHeight;
#endif
                }
            }
            return position;
        }

        private WindowPositionProvider()
        {
#if PLATFORM_WINDOWS
            nextPosition = new Point(SystemParameters.BorderWidth, SystemParameters.WindowCaptionHeight + SystemParameters.ResizeFrameHorizontalBorderHeight);
#else
            nextPosition = new Point(0, 0);
#endif
            nextPosition.X += ConfigurationManager.Instance.Get("termsharp", "window-initial-offset-x", 0);
            nextPosition.Y += ConfigurationManager.Instance.Get("termsharp", "window-initial-offset-y", 0);

            var x = ConfigurationManager.Instance.Get("termsharp", "window-next-offset-x", 30, i => i >= 0);
            var y = ConfigurationManager.Instance.Get("termsharp", "window-next-offset-y", 50, i => i >= 0);
            offset = new Point(x, y);
            innerLock = new object();
        }

        private readonly object innerLock;

        private Point nextPosition;
        private readonly Point offset;
    }
}
