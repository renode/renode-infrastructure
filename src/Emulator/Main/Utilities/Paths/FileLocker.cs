//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public class FileLocker : IDisposable
    {
        public FileLocker(string fileToLock)
        {
            if(RuntimeInfo.IsWindows())
            {
                innerLocker = new WindowsFileLocker(fileToLock);
            }
            else
            {
                innerLocker = new PosixFileLocker(fileToLock);
            }
        }

        public void Dispose()
        {
            innerLocker?.Dispose();
            innerLocker = null;
        }

        private IDisposable innerLocker;
    }
}