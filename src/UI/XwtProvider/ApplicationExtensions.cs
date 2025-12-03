//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Threading;

using Antmicro.Renode.Exceptions;

using Xwt;

namespace Antmicro.Renode.UI
{
    public static class ApplicationExtensions
    {
        static ApplicationExtensions()
        {
            actionsToRunInUIThread = new BlockingCollection<Action>();
            var t = new Thread(() =>
            {
                while(true)
                {
                    var a = actionsToRunInUIThread.Take();
                    Application.Invoke(a);
                }
            });
            t.IsBackground = true;
            t.Name = "ApplicationExtensions GUI invoker";
            t.Start();
        }

        public static void InvokeInUIThread(Action action)
        {
            CheckXwtStatus();
            if(Thread.CurrentThread.ManagedThreadId == XwtProvider.UiThreadId)
            {
                action();
            }
            else
            {
                Application.Invoke(action);
            }
        }

        public static void InvokeInUIThreadNonBlocking(Action action)
        {
            CheckXwtStatus();
            if(Thread.CurrentThread.ManagedThreadId == XwtProvider.UiThreadId)
            {
                actionsToRunInUIThread.Add(action);
            }
            else
            {
                Application.Invoke(action);
            }
        }

        public static T InvokeInUIThreadAndWait<T>(Func<T> function)
        {
            CheckXwtStatus();
            if(Thread.CurrentThread.ManagedThreadId == XwtProvider.UiThreadId)
            {
                return function();
            }

            T result = default(T);
            var mre = new ManualResetEventSlim();

            Application.Invoke(() =>
            {
                result = function();
                mre.Set();
            });

            mre.Wait();
            return result;
        }

        public static void InvokeInUIThreadAndWait(Action action)
        {
            CheckXwtStatus();
            if(Thread.CurrentThread.ManagedThreadId == XwtProvider.UiThreadId)
            {
                action();
                return;
            }

            var mre = new ManualResetEventSlim();
            Application.Invoke(() =>
            {
                action();
                mre.Set();
            });

            mre.Wait();
        }

        private static void CheckXwtStatus()
        {
            if(XwtProvider.UiThreadId == -1)
            {
                throw new RecoverableException("An action requiring GUI environment detected, but the XWT provider is not started.");
            }
        }

        private static readonly BlockingCollection<Action> actionsToRunInUIThread;
    }
}