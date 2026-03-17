//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public static class FileCopier
    {
        public static void Copy(string src, string dst, bool overwrite = false)
        {
            try
            {
                if(RuntimeInfo.IsLinux() && ConfigurationManager.Instance.Get("file-system", "use-cow", false))
                {
                    int sfd = -1, dfd = -1;
                    try
                    {
                        sfd = LibCWrapper.Open(src, LibCWrapper.O_RDONLY);

                        if(!overwrite)
                        {
                            // XXX: This has a race condition, but since we can't use the three-argument `open` (due to dotnet not supporting FFI varargs), we can't use `O_CREAT | O_EXCL` since that would require passing permissions

                            // Check if destination already exists
                            dfd = LibCWrapper.Open(dst, LibCWrapper.O_WRONLY);
                            if(dfd != -1)
                            {
                                throw new IOException("Destination file exists");
                            }
                        }
                        dfd = LibCWrapper.Creat(dst, LibCWrapper.DEFFILEMODE);

                        if((sfd != -1) && (dfd != -1) && (LibCWrapper.Ioctl(dfd, IoctlFICLONE, sfd) != -1))
                        {
                            return;
                        }
                    }
                    finally
                    {
                        if(sfd != -1)
                        {
                            LibCWrapper.Close(sfd);
                        }

                        if(dfd != -1)
                        {
                            LibCWrapper.Close(dfd);
                        }
                    }
                }

                var lastTime = CustomDateTime.Now;
                using(var source = File.Open(src, FileMode.Open, FileAccess.Read))
                {
                    using(var destination = File.Open(dst, overwrite ? FileMode.Create : FileMode.CreateNew))
                    {
                        var progressHandler = EmulationManager.Instance.ProgressMonitor.Start("Copying...", false, true);

                        var read = 0;
                        var count = 0L;
                        var sourceLength = source.Length;
                        var buffer = new byte[64*1024];
                        do
                        {
                            read = source.Read(buffer, 0, buffer.Length);
                            destination.Write(buffer, 0, read);
                            count += read;

                            var now = CustomDateTime.Now;
                            if(now - lastTime > TimeSpan.FromSeconds(0.25))
                            {
                                progressHandler.UpdateProgress((int)(100L * count / sourceLength));
                                lastTime = now;
                            }
                        }
                        while(read > 0);
                        progressHandler.Finish();
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException(e);
            }
        }

        private const int IoctlFICLONE = 0x40049409;
    }
}