//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Xwt;
#if !PLATFORM_WINDOWS
using Xwt.GtkBackend;
#endif
using System.Threading;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.UI
{
    public class XwtProvider : IDisposable
    {
        static XwtProvider()
        {
            internalLock = new object();
            UiThreadId = -1;
        }

        public XwtProvider(IUserInterfaceProvider uiProvider, bool autostart = true)
        {
            Emulator.UserInterfaceProvider = uiProvider;
            if(autostart)
            {
                StartXwtThreadOnMainThread();
            }
        }

        public void Dispose()
        {
            StopXwtThread();
        }

        public void Initialize()
        {
#if PLATFORM_WINDOWS
            Application.Initialize(ToolkitType.Wpf);
#else
            Application.Initialize(ToolkitType.Gtk);
#endif
        }

        public void RunMainLoopInCurrentThread()
        {
            lock(internalLock)
            {
                if(UiThreadId != -1)
                {
                    throw new ArgumentException(string.Format("UI thread is already running: {0}", UiThreadId));
                }
                UiThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            Application.UnhandledException += (sender, arg) => CrashHandler.HandleCrash(arg.ErrorException);
#if !PLATFORM_WINDOWS
            GLib.ExceptionManager.UnhandledException += arg => CrashHandler.HandleCrash((Exception)arg.ExceptionObject);
#endif
            Application.Run();

#if !PLATFORM_WINDOWS
            GtkTextLayoutBackendHandler.DisposeResources();
#endif
            lock(internalLock)
            {
                UiThreadId = -1;
            }
        }

        public static int UiThreadId { get; private set; }

        private void StartXwtThreadOnMainThread()
        {
            var manualResetEvent = new ManualResetEventSlim();
            Emulator.ExecuteOnMainThread(() =>
            {
                // XWT thread has to be initialized on the first thread at OSX
                Initialize();
                manualResetEvent.Set();
                RunMainLoopInCurrentThread();
            });
            // we should wait here for the initalization of XWT
            // as further code might want to use it
            manualResetEvent.Wait();
        }

        private void StopXwtThread()
        {
            lock(internalLock)
            {
                if(UiThreadId == -1)
                {
                    return;
                }
                ApplicationExtensions.InvokeInUIThreadAndWait(Application.Exit);
            }
        }

        private static object internalLock;
    }
}

