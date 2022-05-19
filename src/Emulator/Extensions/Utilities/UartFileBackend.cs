//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Text;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Extensions.Utilities
{
    public static class UartFileBackendExtensions
    {
        public static void CreateFileBackend(this IUART uart, string path, bool immediateFlush = false)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            var name = ExternalNamePrefix + Path.GetFullPath(path);

            ((IHasChildren<IExternal>)emulation.ExternalsManager).TryGetByName(name, out var found);
            if(found)
            {
                throw new RecoverableException($"The {path} is alredy used as UART file backend. Please close it using 'CloseFileBackend' before re-using");
            }

            emulation.ExternalsManager.AddExternal(new UartFileBackend(path, uart, immediateFlush), name);
        }

        public static void CloseFileBackend(this IUART uart, string path)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            var name = ExternalNamePrefix + Path.GetFullPath(path);

            var external = (UartFileBackend)((IHasChildren<IExternal>)emulation.ExternalsManager).TryGetByName(name, out var success);
            if(!success)
            {
                throw new RecoverableException($"Couldn't find active {path} file backend for the UART");
            }

            emulation.ExternalsManager.RemoveExternal(external);
            external.Dispose();
        }

        private const string ExternalNamePrefix = "__uart_file_backend__";
    }

    public class UartFileBackend : IExternal, IDisposable
    {
        public UartFileBackend(SequencedFilePath path, IUART uart, bool immediateFlush = false)
        {
            this.uart = uart;
            this.immediateFlush = immediateFlush;

            // SequencedFilePath asserts that file in given path doesn't exist
            writer = new BinaryWriter(File.Open(path, FileMode.CreateNew));
            uart.CharReceived += WriteChar;
        }

        public void Dispose()
        {
            uart.CharReceived -= WriteChar;
            writer.Dispose();
        }

        private void WriteChar(byte value)
        {
            writer.Write(value);
            if(immediateFlush)
            {
                writer.Flush();
            }
        }

        private readonly bool immediateFlush;
        private readonly IUART uart;
        private readonly BinaryWriter writer;
    }
}
