//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

#if PLATFORM_WINDOWS
using System.Windows;
#endif
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
                var result = nextPosition;
                nextPosition.X += offset.X;
                nextPosition.Y += offset.Y;
                return result;
            }
        }

        private WindowPositionProvider()
        {
#if PLATFORM_WINDOWS
            nextPosition = new Point(SystemParameters.BorderWidth, SystemParameters.WindowCaptionHeight + SystemParameters.ResizeFrameHorizontalBorderHeight);
#else
            nextPosition = new Point(0, 0);
#endif
            var x = ConfigurationManager.Instance.Get("termsharp", "window-offset-x", 30, i => i >= 0);
            var y = ConfigurationManager.Instance.Get("termsharp", "window-offset-y", 50, i => i >= 0);
            offset = new Point(x, y);
            innerLock = new object();
        }

        private readonly object innerLock;

        private Point nextPosition;
        private readonly Point offset;
    }
}
