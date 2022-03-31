//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Threading;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    public class FileBackend : TextBackend 
    {
        public FileBackend(SequencedFilePath filePath, bool flushAfterEachWrite = false)
        {
            var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            output = new StreamWriter(stream);
            sync = new object();
            timer = new Timer(x => Flush(), null, 0, 5000);
            this.flushAfterEachWrite = flushAfterEachWrite;
        }

        public override void Log(LogEntry entry)
        {
            if(!ShouldBeLogged(entry))
            {
                return;
            }

            lock(sync)
            {
                if(isDisposed)
                {
                    return;
                }

                var type = entry.Type;
                var message = FormatLogEntry(entry);
                output.WriteLine(string.Format("{0:HH:mm:ss} [{1}] {2}", CustomDateTime.Now, type, message));
                if(flushAfterEachWrite)
                {
                    output.Flush();
                }
            }
        }

        public override void Dispose()
        {
            lock(sync)
            {
                timer.Dispose();
                if(isDisposed)
                {
                    return;
                }
                output.Dispose();
                isDisposed = true;
            }
        }

        public override void Flush()
        {
            lock(sync)
            {
                if(isDisposed)
                {
                    return;
                }
                
                output.Flush();
            }
        }
            
        private bool isDisposed;
        private readonly Timer timer;
        private readonly object sync;
        private readonly TextWriter output;
        private readonly bool flushAfterEachWrite;
    }
}

