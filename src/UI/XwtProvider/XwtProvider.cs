//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Xwt;
#if !GUI_DISABLED
using Xwt.GtkBackend;
// for DllMap replacement
using System.Reflection;
using System.IO;
#endif
using System.Threading;

using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UI
{
    public class XwtProvider : IDisposable
    {
        static XwtProvider()
        {
            internalLock = new object();
            UiThreadId = -1;
        }

        public static XwtProvider Create(IUserInterfaceProvider uiProvider)
        {
#if GUI_DISABLED
            return null;
#else
            Emulator.UserInterfaceProvider = uiProvider;
            var xwtProvider = new XwtProvider();
            return (xwtProvider.StartXwtThreadOnMainThread())
                ? xwtProvider
                : null;
#endif
        }

        public static int UiThreadId { get; private set; }

        public void Dispose()
        {
            StopXwtThread();
        }

        public bool Initialize()
        {
            try
            {
#if !GUI_DISABLED
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var assembly = Assembly.LoadFrom(Path.Combine(assemblyLocation, RuntimeInfo.IsWindows() ? "Xwt.WPF.dll": "Xwt.Gtk3.dll"));
                DllMap.Register(assembly);
                Application.Initialize(RuntimeInfo.IsWindows() ? ToolkitType.Wpf : ToolkitType.Gtk3);
#endif
                return true;
            }
            catch(Exception)
            {
                return false;
            }
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
#if !GUI_DISABLED
            if(RuntimeInfo.IsWindows())
#endif
            {
                Application.Run();
            }
#if !GUI_DISABLED
            else
            {
                RunApplicationGtk();
            }
#endif

            lock(internalLock)
            {
                UiThreadId = -1;
            }
        }

        // Split off Gtk-specific code into separate method to prevent Gtk import on Windows
        private static void RunApplicationGtk()
        {
#if !GUI_DISABLED
            GLib.ExceptionManager.UnhandledException += arg => CrashHandler.HandleCrash((Exception)arg.ExceptionObject);
            Application.Run();
            GtkTextLayoutBackendHandler.DisposeResources();
#endif
        }

        private static readonly object internalLock;

        private XwtProvider()
        {
        }

        private bool StartXwtThreadOnMainThread()
        {
            var initialized = false;
            var manualResetEvent = new ManualResetEventSlim();
            Emulator.ExecuteOnMainThread(() =>
            {
                // XWT thread has to be initialized on the first thread at OSX
                initialized = Initialize();
                manualResetEvent.Set();
                if(initialized)
                {
                    RunMainLoopInCurrentThread();
                }
            });
            // we should wait here for the initalization of XWT
            // as further code might want to use it
            manualResetEvent.Wait();
            return initialized;
        }

        private void StopXwtThread()
        {
            lock(internalLock)
            {
                if(UiThreadId == -1)
                {
                    return;
                }
                ApplicationExtensions.InvokeInUIThread(Application.Exit);
            }
        }
    }
}
