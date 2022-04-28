//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_WINDOWS
using System;
using System.IO;
using System.Threading;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class WindowsFileLocker : IDisposable
    {
        public WindowsFileLocker(string fileToLock)
        {
            path = fileToLock;
            var counter = 0;
            while(true)
            {
                try
                {
                    file = File.Open(fileToLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return;
                }
                catch(IOException)
                {
                    // ignore exception
                }

                Thread.Sleep(500);
                counter++;
                if(counter == 10)
                {
                    counter = 0;
                    Logger.Log(LogLevel.Warning, "Still trying to lock file {0}", fileToLock);
                }
            }
        }

        public void Dispose()
        {
            file.Close();
            try
            {
                File.Delete(path);
            }
            catch(IOException)
            {
                // ignore exception
            }
        }

        private readonly FileStream file;
        private readonly string path;
    }
}
#endif
