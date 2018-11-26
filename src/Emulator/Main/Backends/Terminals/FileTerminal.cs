//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using System.IO;

namespace Antmicro.Renode.Backends.Terminals
{
    public static class FileTerminalExtensions
    {
        public static void CreateFileTerminal(this Emulation emulation, string path, string name, bool emitConfig = false)
        {
            emulation.ExternalsManager.AddExternal(new FileTerminal(path, emitConfig), name);
        }
    }

    public class FileTerminal : BackendTerminal, IDisposable
    {
        public FileTerminal(string path, bool emitConfigBytes = true)
        {
	    fs =  new FileStream(path, FileMode.Create);
        }

        public override void WriteChar(byte value)
        {
	    fs.WriteByte(value);
        }

        public void Dispose()
        {
	    fs.Close();
        }

        private FileStream fs;
    }
}

