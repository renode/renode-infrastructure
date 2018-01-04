//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Input;
using Xwt;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Handlers
{
    internal class AbsolutePointerHandler : PointerHandler
    {
        public AbsolutePointerHandler(IAbsolutePositionPointerInput tablet, FrameBufferDisplayWidget widget) : base(tablet)
        {
            this.widget = widget;
            previousCursorType = widget.Cursor;
            // it looks strange, but without it handling cursor for the first time causes glitches
            ShowCursor();
        }

        public override void Init()
        {
            // this will hide cursor and draw cross if needed
            PointerMoved(lastX, lastY, 0, 0);
        }

        public override void ButtonReleased(int button)
        {
            base.ButtonReleased(button);
            if(isCursorOverImageRectangle)
            {
                HideCursor();
            }
        }

        public override void PointerMoved(int x, int y, int dx, int dy)
        {
            lastX = x;
            lastY = y;

            var image = widget.Image;
            if(image == null)
            {
                return;
            }

            var ainput = (IAbsolutePositionPointerInput)input;
            var imgRect = widget.ActualImageArea;

            // if cursor doesn't touch actual image, return
            if(!imgRect.IntersectsWith(new Rectangle(x, y, 1, 1)))
            {
                ShowCursor();
                isCursorOverImageRectangle = false;
                return;
            }
            if(!isCursorOverImageRectangle)
            {
                widget.CanGetFocus = true;
                widget.SetFocus();
                isCursorOverImageRectangle = true;
                HideCursor();
            }

            //  this fragment converts click-point coordinates:
            //  from
            //     FrameBufferAnalyzer coordinates system (i.e., XWT coordinates of canvas on which the buffer was drawn)
            //  through
            //     Buffer coordinates system (i.e., resolution set on emulated graphic card)
            //  to
            //     Touchscreen coordinates system
            var maxX = (int)image.Width;
            var maxY = (int)image.Height;

            var imageX = ((x - imgRect.X) / imgRect.Width) * maxX;
            var imageY = ((y - imgRect.Y) / imgRect.Height) * maxY;

            X = (int)imageX;
            Y = (int)imageY;

            var touchscreenX = (int)Math.Round(imageX * ainput.MaxX / maxX);
            var touchscreenY = (int)Math.Round(imageY * ainput.MaxY / maxY);

            ainput.MoveTo(touchscreenX, touchscreenY);

            var opm = OnPointerMoved;
            if(opm != null)
            {
                opm(X, Y);
            }
        }

        public int X { get; protected set; }
        public int Y { get; protected set; }

        public event Action<int, int> OnPointerMoved;

        private void HideCursor()
        {
            if(widget.Cursor == CursorType.Invisible)
            {
                return;
            }
            previousCursorType = widget.Cursor;
            widget.Cursor = CursorType.Invisible;
        }

        private void ShowCursor()
        {
            widget.Cursor = previousCursorType;
        }

        private int lastX, lastY;
        private CursorType previousCursorType;
        private readonly FrameBufferDisplayWidget widget;
        private bool isCursorOverImageRectangle;
    }
}

