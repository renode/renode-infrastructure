//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
#if !PLATFORM_WINDOWS
using Mono.Unix.Native;
#endif
using System.Runtime.InteropServices;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public static class FileCopier
    {
        public static void Copy(string src, string dst, bool overwrite = false)
        {
            try
            {
#if !PLATFORM_WINDOWS                
                if (ConfigurationManager.Instance.Get("file-system", "use-cow", false))
                {
                    int sfd = -1, dfd = -1;
                    try
                    {
                        sfd = Syscall.open(src, OpenFlags.O_RDONLY);
                        dfd = Syscall.open(dst, overwrite ? OpenFlags.O_CREAT | OpenFlags.O_TRUNC | OpenFlags.O_WRONLY : (OpenFlags.O_CREAT | OpenFlags.O_EXCL), FilePermissions.S_IRUSR | FilePermissions.S_IWUSR);

                        if(sfd != -1 && dfd != -1 && ioctl(dfd, 0x40049409, sfd) != -1)
                        {
                            return;
                        }
                    }
                    finally
                    {
                        if(sfd != -1)
                        {
                            Syscall.close(sfd);
                        }

                        if(dfd != -1)
                        {
                            Syscall.close(dfd);
                        }
                    }
                }
#endif
 
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
                                progressHandler.UpdateProgress((int)(100L*count/sourceLength));
                                lastTime = now;
                            }
                        }
                        while(read > 0);
                        progressHandler.Finish();
                    }
                }
            }
            catch (IOException e)
            {
                throw new RecoverableException(e);
            }
        }

#if !PLATFORM_WINDOWS
        [DllImport("libc")]
        private extern static int ioctl(int d, ulong request, int a);
#endif
    }
}

