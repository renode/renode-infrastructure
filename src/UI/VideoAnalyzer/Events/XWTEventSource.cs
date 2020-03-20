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
using Antmicro.Renode.Extensions.Analyzers.Video.Handlers;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal class XWTEventSource : IEventSource
    {
        public XWTEventSource(Widget source)
        {
            this.source = source;
        }

        public void AttachHandler(IOHandler h)
        {
            handler = h;

            source.MouseMoved += HandleMouseMoved;
            source.ButtonPressed += HandleButtonPressed;
            source.ButtonReleased += HandleButtonReleased;
            source.KeyPressed += HandleKeyPressed;
            source.KeyReleased += HandleKeyReleased;
        }

        private void HandleKeyReleased(object sender, KeyEventArgs e)
        {
#if !PLATFORM_WINDOWS && !GUI_DISABLED
            e.Handled = true;
            var entryKey = Gdk.Keymap.Default.GetEntriesForKeyval((uint)e.Key)[0].Keycode;

            var key = X11ToKeyScanCodeConverter.Instance.GetScanCode((int)entryKey);
            if(key != null)
            {
                handler.KeyReleased(key.Value);
                return;
            }
#endif
            Logger.LogAs(this, LogLevel.Warning, "Unhandled keycode: {0}", e.Key);
        }

        private void HandleKeyPressed(object sender, KeyEventArgs e)
        {
#if !PLATFORM_WINDOWS && !GUI_DISABLED
            e.Handled = true;
            var entryKey = Gdk.Keymap.Default.GetEntriesForKeyval((uint)e.Key)[0].Keycode;

            var key = X11ToKeyScanCodeConverter.Instance.GetScanCode((int)entryKey);
            if(key != null)
            {
                handler.KeyPressed(key.Value);
                return;
            }
#endif
            Logger.LogAs(this, LogLevel.Warning, "Unhandled keycode: {0}", e.Key);
        }

        private void HandleButtonReleased(object sender, ButtonEventArgs e)
        {
            if(!e.Handled)
            {
                handler.ButtonReleased(e.Button);
                e.Handled = true;
            }
        }

        private void HandleButtonPressed(object sender, ButtonEventArgs e)
        {
            if(!e.Handled)
            {
                handler.ButtonPressed(e.Button);
                e.Handled = true;
            }
        }

        private void HandleMouseMoved(object sender, MouseMovedEventArgs e)
        {
            if(lastX == null || lastY == null)
            {
                lastX = (int)e.X;
                lastY = (int)e.Y;
                return;
            }

            handler.MouseMoved((int)e.X, (int)e.Y, lastX.Value - (int)e.X, lastY.Value - (int)e.Y);
            lastX = (int)e.X;
            lastY = (int)e.Y;
        }

        public void DetachHandler()
        {
            source.MouseMoved -= HandleMouseMoved;
            source.ButtonPressed -= HandleButtonPressed;
            source.ButtonReleased -= HandleButtonReleased;
            source.KeyPressed -= HandleKeyPressed;
            source.KeyReleased -= HandleKeyReleased;

            handler = null;
        }

        private Widget source;
        private IOHandler handler;

        private int? lastX;
        private int? lastY;

        public int X { get { return lastX ?? 0; } }
        public int Y { get { return lastY ?? 0; } }
    }
}
