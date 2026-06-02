//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Threading;

using AntShell.Terminal;

namespace Antmicro.Renode.UI
{
    public class DummyIOSource : IPassiveIOSource
    {
        public void CancelRead()
        {
            readCancelled.Set();
        }

        public void Dispose()
        {
            CancelRead();
        }

        public void Flush()
        {
        }

        public bool TryPeek(out int value)
        {
            value = -1;
            return false;
        }

        public int Read()
        {
            readCancelled.WaitOne();
            return -1;
        }

        public void Write(byte b)
        {
        }

        private readonly ManualResetEvent readCancelled = new ManualResetEvent(false);
    }
}
