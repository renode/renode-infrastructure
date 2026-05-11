//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

using Xwt;

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

        private WindowPositionProvider()
        {
            if(RuntimeInfo.IsWindows())
            {
                // We can't reference WPF directly because Renode is built as a non-Windows target (and WPF is a Windows-only target dependency), but let's use dynamic import to get what we need for `Copy`
                var assembly = Assembly.Load("PresentationFramework");
                var systemParameters = assembly.GetType("System.Windows.SystemParameters");
                borderWidth = (double)systemParameters.GetProperty("BorderWidth").GetValue(null);
                borderTotalHeight = (double)systemParameters.GetProperty("WindowCaptionHeight").GetValue(null)
                    + (double)systemParameters.GetProperty("ResizeFrameHorizontalBorderHeight").GetValue(null);
            }
            nextPosition = new Point(borderWidth, borderTotalHeight);
            nextPosition.X += ConfigurationManager.Instance.Get("termsharp", "window-initial-offset-x", 0);
            nextPosition.Y += ConfigurationManager.Instance.Get("termsharp", "window-initial-offset-y", 0);

            var x = ConfigurationManager.Instance.Get("termsharp", "window-next-offset-x", 30, i => i >= 0);
            var y = ConfigurationManager.Instance.Get("termsharp", "window-next-offset-y", 50, i => i >= 0);
            offset = new Point(x, y);
            innerLock = new object();
        }

        private Point SnapToViewport(Point position)
        {
            if(!ConfigurationManager.Instance.Get("termsharp", "window-allow-outside-viewport", false))
            {
                var currentScreen = Desktop.GetScreenAtLocation(position) ?? Desktop.PrimaryScreen;
                if(!currentScreen.VisibleBounds.Contains(position))
                {
                    position.X = currentScreen.VisibleBounds.X + borderWidth;
                    position.Y = currentScreen.VisibleBounds.Y + borderTotalHeight;
                }
            }
            return position;
        }

        private Point nextPosition;

        private readonly object innerLock;
        private readonly Point offset;

        private readonly double borderWidth;
        private readonly double borderTotalHeight;
    }
}