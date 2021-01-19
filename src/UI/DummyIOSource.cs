//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Terminal;

namespace Antmicro.Renode.UI
{
    public class DummyIOSource : IPassiveIOSource
    {
        public void CancelRead()
        {
        }

        public void Dispose()
        {
        }

        public void Flush()
        {
        }

        public bool TryPeek(out int value)
        {
            value = 0;
            return true;
        }

        public int Read()
        {
            return 0;
        }

        public void Write(byte b)
        {
        }
    }
}
