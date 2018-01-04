//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace Antmicro.Renode.Backends.Display.XInput
{
    public static class XLibHelper
    {
        static XLibHelper()
        {
            errorHandler = ((display, error) =>
                {
                    var x11e = X11Error;
                    if(x11e != null)
                    {
                        x11e();
                    }
                    return 0;
                });

            var fpointer = Marshal.GetFunctionPointerForDelegate(errorHandler);
            XSetErrorHandler(fpointer);
        }

        public static void GrabCursorByWindow(int windowId)
        {
            lock(locker)
            {
                using(var l = new XLibLocker(DisplayHandle))
                {
                    int res = 1;
                    do
                    {
                        // Not always grabbing mouse works at the first time, so the loop is necessary
                        res = XGrabPointer(DisplayHandle, windowId, true, 0x4 | 0x8 | 0x40, 1, 1, windowId, DefineEmptyCursor(windowId), 0);
                        Thread.Sleep(100);
                    }
                    while(res != 0);

                    XGrabKeyboard(DisplayHandle, windowId, true, 1, 1, 0);
                }
            }
        }

        public static void UngrabCursor()
        {
            lock(locker)
            {
                using(var l = new XLibLocker(DisplayHandle))
                {
                    XUngrabPointer(DisplayHandle, 0);
                    XUngrabKeyboard(DisplayHandle, 0);
                }

                XCloseDisplay(DisplayHandle);
                DisplayHandle = XOpenDisplay(IntPtr.Zero);
            }
        }

        public static Tuple<int, int> GetCursorPosition(int win)
        {
            IntPtr r = IntPtr.Zero, c = IntPtr.Zero;
            int rx = -1, ry = -1, wx = -1, wy = -1;
            uint m = 0;

            using(var l = new XLibLocker(DisplayHandle))
            {
                XQueryPointer(DisplayHandle, win, ref r, ref c, ref rx, ref ry, ref wx, ref wy, ref m);
            }
            return Tuple.Create(rx, ry);
        }

        public static void MoveCursorRelative(int dx, int dy)
        {
            XWarpPointer(DisplayHandle, 0, 0, 0, 0, 0, 0, dx, dy);
        }

        public static void MoveCursorAbsolute(int windowId, int x, int y)
        {
            XWarpPointer(DisplayHandle, 0, windowId, 0, 0, 0, 0, x, y);
        }

        public static void StartEventListenerLoop(IInputHandler handler)
        {
            lock(EventListenerThread)
            {
                if(!EventListenerThread.IsAlive)
                {
                    EventListenerThread.Name = "XLib event listener";
                    EventListenerThread.IsBackground = true;
                    EventListenerThread.Start(handler);
                }
            }
        }

        public static bool IsAvailable
        {
            get
            {
                try
                {
                    XOpenDisplay(IntPtr.Zero);
                    return true;
                }
                catch(Exception)
                {
                }

                return false;
            }
        }

        public static Action X11Error;

        [DllImport("libX11", EntryPoint = "XChangeActivePointerGrab")]
        internal extern static int XChangeActivePointerGrab(IntPtr display, uint eventMask, IntPtr cursor, /*Time*/int time);

        [DllImport("libX11", EntryPoint = "XGrabPointer")]
        internal extern static int XGrabPointer(IntPtr display, int grabWindow, bool ownerEvents, uint eventMask, int pointerMode, int keyboardMode, int confineTo, IntPtr cursor, /*Time*/int time);

        [DllImport("libX11", EntryPoint = "XUngrabPointer")]
        internal extern static int XUngrabPointer(IntPtr display, int time);

        [DllImport("libX11", EntryPoint = "XGrabKeyboard")]
        internal extern static int XGrabKeyboard(IntPtr display, int grabWindow, bool ownerEvents, int pointerMode, int keyboardMode, int time);

        [DllImport("libX11", EntryPoint = "XUngrabKeyboard")]
        internal extern static int XUngrabKeyboard(IntPtr display, int time);

        [DllImport("libX11", EntryPoint = "XCreatePixmapFromBitmapData")]
        internal extern static IntPtr XCreatePixmapFromBitmapData(IntPtr display, int drawable, byte[] data, int width, int height, IntPtr fg, IntPtr bg, int depth);

        [DllImport("libX11", EntryPoint = "XFreePixmap")]
        internal extern static IntPtr XFreePixmap(IntPtr display, IntPtr pixmap);

        [DllImport("libX11", EntryPoint = "XCreatePixmapCursor")]
        internal extern static IntPtr XCreatePixmapCursor(IntPtr display, IntPtr source, IntPtr mask, ref IntPtr foreground_color, ref IntPtr background_color, int x_hot, int y_hot);

        [DllImport("libX11", EntryPoint = "XWhitePixel")]
        internal extern static IntPtr XWhitePixel(IntPtr display, int screen_no);

        [DllImport("libX11", EntryPoint = "XBlackPixel")]
        internal extern static IntPtr XBlackPixel(IntPtr display, int screen_no);

        [DllImport("libX11", EntryPoint = "XRootWindow")]
        internal extern static int XRootWindow(IntPtr display, int screen_number);

        [DllImport("libX11", EntryPoint = "XDefaultScreen")]
        internal extern static int XDefaultScreen(IntPtr display);

        [DllImport("libX11", EntryPoint = "XOpenDisplay")]
        internal extern static IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11", EntryPoint = "XCloseDisplay")]
        internal extern static int XCloseDisplay(IntPtr display);

        [DllImport("libX11", EntryPoint = "XWarpPointer")]
        internal extern static void XWarpPointer(IntPtr display, int srcWindowId, int dstWindowId, int srcX, int srcY, int srcWidth, int srcHeight, int dstX, int dstY);

        [DllImport("libX11", EntryPoint = "XQueryPointer")]
        internal extern static void XQueryPointer(IntPtr display, int window, ref IntPtr root, ref IntPtr child, ref int rootX, ref int rootY, ref int winX, ref int winY, ref uint mask);

        [DllImport("libX11", EntryPoint = "XNextEvent")]
        internal extern static void XNextEvent(IntPtr display, IntPtr e);

        [DllImport("libX11", EntryPoint = "XLockDisplay")]
        internal extern static void XLockDisplay(IntPtr display);

        [DllImport("libX11", EntryPoint = "XUnlockDisplay")]
        internal extern static void XUnlockDisplay(IntPtr display);

        [DllImport("libX11", EntryPoint = "XSetErrorHandler")]
        internal extern static int XSetErrorHandler(IntPtr handler);

        [Conditional("DEBUG")]
        private static void DebugPrint(string str, params object[] p)
        {
            Console.WriteLine(str, p);
        }

        private static IntPtr DefineEmptyCursor(int windowId)
        {
            var c = Marshal.AllocHGlobal(80);
            var cursor_bits = new byte[2 * 16];
            IntPtr cursor;

            using(var l = new XLibLocker(DisplayHandle))
            {
                var pixmap = XCreatePixmapFromBitmapData(DisplayHandle, windowId, cursor_bits, 16, 16, (IntPtr)1, (IntPtr)0, 1);
                cursor = XCreatePixmapCursor(DisplayHandle, pixmap, pixmap, ref c, ref c, 0, 0);
                XFreePixmap(DisplayHandle, pixmap);
            }
            Marshal.FreeHGlobal(c);

            return cursor;
        }

        private static void EventListenerLoop(object h)
        {
            var handler = h as IInputHandler;
            if(handler == null)
            {
                return;
            }

            int initMouseX = -1;
            int initMouseY = -1;

            using(var ev = new XEvent())
            {
                handler.Stop = false;
                while(!handler.Stop)
                {
                    XNextEvent(DisplayHandle, ev.Pointer);
                    var type = ev.Type;
                    switch(type)
                    {
                    /*
                    * Mouse cursor moved
                    */
                    case XEvent.MotionNotify:

                        var rx = ev.MotionNotifyXRoot;
                        var ry = ev.MotionNotifyYRoot;

                        if(initMouseX == -1 && initMouseY == -1)
                        {
                            initMouseX = rx;
                            initMouseY = ry;
                            continue;
                        }

                        var dx = rx - initMouseX;
                        var dy = ry - initMouseY;

                        if(!(dx == 0 && dy == 0))
                        {
                            handler.MouseMoved(rx, ry, dx, dy);

                            if(handler.CursorFixed)
                            {
                                MoveCursorAbsolute(XRootWindow(DisplayHandle, XDefaultScreen(DisplayHandle)), initMouseX, initMouseY);
                            }
                        }
                        break;
                    /*
                    * Mouse button pressed
                    */
                    case XEvent.ButtonPress:
                        handler.ButtonPressed(ev.Button);
                        break;
                    /*
                    * Mouse button released
                    */
                    case XEvent.ButtonRelease:
                        handler.ButtonReleased(ev.Button);
                        break;
                    /*
                    * Keyboard key pressed
                    */
                    case XEvent.KeyPress:
                        handler.KeyPressed(ev.KeyCode);
                        break;
                    /*
                    * Keyboard key released
                    */
                    case XEvent.KeyRelease:
                        handler.KeyReleased(ev.KeyCode);
                        break;
                    }
                }
            }

            EventListenerThread = new Thread(EventListenerLoop);
        }

        private static Thread EventListenerThread = new Thread(EventListenerLoop);
        private static IntPtr DisplayHandle = XOpenDisplay(IntPtr.Zero);
        private static Func<IntPtr, IntPtr, int> errorHandler;
        private static object locker = new object();

        // inspired by OpenTK solution
        private class XLibLocker : IDisposable
        {
            public XLibLocker(IntPtr display)
            {
                this.display = display;
                XLockDisplay(display);
            }

            public void Dispose()
            {
                XUnlockDisplay(display);
            }

            private readonly IntPtr display;
        }

        private class XEvent : IDisposable
        {
            public XEvent()
            {
                Pointer = Marshal.AllocCoTaskMem(296);
            }

            public void Dispose()
            {
                Marshal.FreeCoTaskMem(Pointer);
            }

            public IntPtr Pointer { get; private set; }

            public int Type
            {
                get { return Marshal.ReadInt32(Pointer, 0x0); }
            }

            public long Time
            {
                get { return Marshal.ReadInt64(Pointer, 0x38); }
            }

            public int MotionNotifyXRoot
            {
                get { return Marshal.ReadInt32(Pointer, 0x48); }
            }

            public int MotionNotifyYRoot
            {
                get { return Marshal.ReadInt32(Pointer, 0x4c); }
            }

            public int Button
            {
                get { return Marshal.ReadInt32(Pointer, 0x54); }
            }

            public int KeyCode
            {
                get
                {
                    // yes, it's the same as Button!
                    return Marshal.ReadInt32(Pointer, 0x54);
                }
            }

            public const int KeyPress = 0x2;
            public const int KeyRelease = 0x3;
            public const int ButtonPress = 0x4;
            public const int ButtonRelease = 0x5;
            public const int MotionNotify = 0x6;
        }
    }
}

