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
using Antmicro.Renode.Extensions.Analyzers.Video.Events;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Handlers
{
    internal class IOHandler
    {
        public IOHandler(FrameBufferDisplayWidget widget)
        {
            this.widget = widget;
            this.source = new XWTEventSource(widget);
            this.source.AttachHandler(this);
        }

        public event Func<bool> GrabConfirm;

        public event Action<PointerHandler, PointerHandler> PointerInputAttached;

        public void MouseMoved(int x, int y, int dx, int dy)
        {
            // The frame buffer analyzer widget must be focused
            // in order to receive IO events.
            // Normally pointer devices are responsible for
            // handling focus (either by grabbing X11 events
            //                 or by calling SetFocus() when needed).
            // When there is no pointer device attached,
            // we fallback to focusing the widget every
            // time the mouse cursor is moved over it.                
            if(pointerHandler == null && !widget.HasFocus)
            {
                widget.CanGetFocus = true;
                widget.SetFocus();
            }

            var ph = pointerHandler;
            if(ph != null && status != Status.NotGrabbed)
            {
                ph.PointerMoved(x, y, dx, dy);
            }
        }

        public void ButtonPressed(PointerButton button)
        {
            var ph = pointerHandler;
            if(ph != null)
            {
                if(status == Status.NotGrabbed && button == PointerButton.Left)
                {
                    var gc = GrabConfirm;
                    if(gc != null && !gc())
                    {
                        return;
                    }

                    source.DetachHandler();
                    source = new X11EventSource(widget);
                    source.AttachHandler(this);
                    status = Status.Grabbed;
                    return;
                }

                ph.ButtonPressed((int)button);
            }
        }

        public void ButtonReleased(PointerButton button)
        {
            var ph = pointerHandler;
            if(ph != null)
            {
                ph.ButtonReleased((int)button);
            }
        }

        public void KeyPressed(KeyScanCode key)
        {
            var kh = keyboardHandler;

            lalt |= (key == KeyScanCode.AltL);
            lctrl |= (key == KeyScanCode.CtrlL);
            lshift |= (key == KeyScanCode.ShiftL);

            if(lctrl && lalt && lshift)
            {
                lalt = false;
                lctrl = false;
                lshift = false;

                if(status == Status.Grabbed)
                {
                    // ask if we should grab
                    source.DetachHandler();
                    source = new XWTEventSource(widget);
                    source.AttachHandler(this);
                    status = Status.NotGrabbed;
                }

                if(kh != null)
                {
                    kh.Release(KeyScanCode.AltL);
                    kh.Release(KeyScanCode.CtrlL);
                    kh.Release(KeyScanCode.ShiftL);
                }
                return;
            }

            if(kh != null)
            {
                kh.Press(key);
            }
        }

        public void KeyReleased(KeyScanCode key)
        {
            lalt &= (key != KeyScanCode.AltL);
            lctrl &= (key != KeyScanCode.CtrlL);
            lshift &= (key != KeyScanCode.ShiftL);

            var kh = keyboardHandler;
            if(kh != null)
            {
                kh.Release(key);
            }
        }

        public void Attach(IPointerInput pointer = null, IKeyboard keyboard = null)
        {
            if(pointer != null)
            {
                var previousPointerHandler = pointerHandler;

                if(pointer is IRelativePositionPointerInput)
                {
                    status = Status.NotGrabbed;
                    pointerHandler = new RelativePointerHandler((IRelativePositionPointerInput)pointer);
                }
                else if(pointer is IAbsolutePositionPointerInput)
                {
                    status = Status.NotGrabbable;
                    pointerHandler = new AbsolutePointerHandler((IAbsolutePositionPointerInput)pointer, widget);
                }

                var pia = PointerInputAttached;
                if(pia != null)
                {
                    pia(pointerHandler, previousPointerHandler);
                }
            }

            if(keyboard != null)
            {
                keyboardHandler = keyboard;
            }
        }

        public void Detach(bool pointer = false, bool keyboard = false)
        {
            if(pointer)
            {
                pointerHandler = null;
            }

            if(keyboard)
            {
                keyboardHandler = null;
            }
        }

        public void GetPosition(out Position current, out Position previous)
        {
            previous = this.previous;

            var pointerHandlerAsAbsolutePointerHandler = pointerHandler as AbsolutePointerHandler;
            if(pointerHandlerAsAbsolutePointerHandler != null)
            {
                current = new Position
                {
                    X = pointerHandlerAsAbsolutePointerHandler.X,
                    Y = pointerHandlerAsAbsolutePointerHandler.Y
                };
            }
            else
            {
                current = null;
            }

            this.previous = current;
        }

        public void Init()
        {
            var ph = pointerHandler;
            if(ph != null)
            {
                ph.Init();
            }
        }

        private PointerHandler pointerHandler;
        private IKeyboard keyboardHandler;

        private IEventSource source;
        private FrameBufferDisplayWidget widget;

        private bool lctrl;
        private bool lalt;
        private bool lshift;

        private Position previous;

        private Status status;

        public class Position
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Position Clone()
            {
                return new Position { X = this.X, Y = this.Y };
            }
        }

        private enum Status
        {
            Grabbed,
            NotGrabbable,
            NotGrabbed
        }
    }
}
