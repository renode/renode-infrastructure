//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Xwt;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Backends.Display.XInput;
using Antmicro.Renode.Extensions.Analyzers.Video.Handlers;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal class X11EventSource : IEventSource, IInputHandler
    {
        public X11EventSource(FrameBufferDisplayWidget source)
        {
            this.source = source;
        }

        public void AttachHandler(IOHandler h)
        {
            handler = h;

            if(!XLibHelper.IsAvailable)
            {
                MessageDialog.ShowError("libX11 library not found");
                return;
            }

            int pid;
            try
            {
                pid = source.ParentWindow.Id;
            }
            catch(DllNotFoundException)
            {
                MessageDialog.ShowError("gdk x11 library not found");
                return;
            }

            XLibHelper.GrabCursorByWindow(pid);
            XLibHelper.MoveCursorAbsolute(pid, (int)(source.ParentWindow.Size.Width / 2), (int)(source.ParentWindow.Size.Height / 2));

            XLibHelper.StartEventListenerLoop(this);
        }

        public void DetachHandler()
        {
            Stop = true;
            XLibHelper.UngrabCursor();

            handler = null;
        }

        public void ButtonPressed(int button)
        {
            handler.ButtonPressed((PointerButton)button);
        }

        public void ButtonReleased(int button)
        {
            handler.ButtonReleased((PointerButton)button);
        }

        public void KeyPressed(int keyCode)
        {
            var key = X11ToKeyScanCodeConverter.Instance.GetScanCode(keyCode);
            if(key != null)
            {
                handler.KeyPressed(key.Value);
                return;
            }

            Logger.LogAs(this, LogLevel.Warning, "Unhandled keycode: {0}", keyCode);
        }

        public void KeyReleased(int keyCode)
        {
            var key = X11ToKeyScanCodeConverter.Instance.GetScanCode(keyCode);
            if(key != null)
            {
                handler.KeyReleased(key.Value);
                return;
            }

            Logger.LogAs(this, LogLevel.Warning, "Unhandled keycode: {0}", keyCode);
        }

        public void MouseMoved(int x, int y, int dx, int dy)
        {
            var image = source.Image;
            if(image == null)
            {
                return;
            }

            if(lastX == null || lastY == null)
            {
                lastX = x;
                lastY = y;
                return;
            }

            dx = Math.Max(-20, dx);
            dx = Math.Min(20, dx);

            dy = Math.Max(-20, dy);
            dy = Math.Min(20, dy);

            var newx = (int)Math.Min(Math.Max(0, lastX.Value + dx), image.Width);
            var newy = (int)Math.Min(Math.Max(0, lastY.Value + dy), image.Height);

            if(newx != lastX || newy != lastY)
            {
                handler.MouseMoved(newx, newy, dx, dy);

                lastX = newx;
                lastY = newy;
            }
        }

        public bool Stop { get; set; }

        public bool CursorFixed { get { return true; } }

        private FrameBufferDisplayWidget source;
        private IOHandler handler;

        private int? lastX;
        private int? lastY;

        public int X { get { return lastX ?? 0; } }
        public int Y { get { return lastY ?? 0; } }

    }
}

