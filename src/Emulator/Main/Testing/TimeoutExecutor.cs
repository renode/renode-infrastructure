//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;

namespace Antmicro.Renode.Testing
{
    public static class TimeoutExecutor
    {
        public static T Execute<T>(Func<T> func, int timeout)
        {
            T result;
            TryExecute(func, timeout, out result);
            return result;
        }

        public static bool TryExecute<T>(Func<T> func, int timeout, out T result)
        {
            T res = default(T);
            var thread = new Thread(() => res = func())
            {
                IsBackground = true,
                Name = typeof(TimeoutExecutor).Name
            };
            thread.Start();
            var finished = thread.Join(timeout);
            if (!finished)
            {
            #if NET
                thread.Interrupt();
            #else
                thread.Abort();
            #endif
            }
            result = res;
            return finished;
        }

        public static bool WaitForEvent(ManualResetEvent e, int timeout)
        {
            return e.WaitOne(timeout);
        }
    }
}

