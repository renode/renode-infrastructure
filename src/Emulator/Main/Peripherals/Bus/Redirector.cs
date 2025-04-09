//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Bus
{
    public static class RedirectorExtensions
    {
        public static void Redirect(this IBusController sysbus, ulong from, ulong to, ulong size)
        {
            var redirector = new Redirector(sysbus.Machine, to);
            var rangePoint = new BusRangeRegistration(from.By(size));
            sysbus.Register(redirector, rangePoint);
        }
    }

    public sealed class Redirector : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IMultibyteWritePeripheral, ICanLoadFiles
    {
        public Redirector(IMachine machine, ulong redirectedAddress)
        {
            this.redirectedAddress = redirectedAddress;
            systemBus = machine.GetSystemBus(this);
        }

        public byte ReadByte(long offset)
        {
            return systemBus.ReadByte(redirectedAddress + checked((ulong)offset));
        }

        public void WriteByte(long offset, byte value)
        {
            systemBus.WriteByte(redirectedAddress + checked((ulong)offset), value);
        }

        public ushort ReadWord(long offset)
        {
            return systemBus.ReadWord(redirectedAddress + checked((ulong)offset));
        }

        public void WriteWord(long offset, ushort value)
        {
            systemBus.WriteWord(checked((ulong)offset), value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return systemBus.ReadDoubleWord(redirectedAddress + checked((ulong)offset));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            systemBus.WriteDoubleWord(redirectedAddress + checked((ulong)offset), value);
        }

        public ulong TranslateAbsolute(ulong address)
        {
            foreach(var range in systemBus.GetRegistrationPoints(this).Select(x => x.Range))
            {
                if(range.Contains(address))
                {
                    return address - range.StartAddress + redirectedAddress;
                }
            }
            throw new RecoverableException("Cannot translate address that does not lay in redirector.");
        }

        public byte[] ReadBytes(long offset, int count, IPeripheral context = null)
        {
            return systemBus.ReadBytes(redirectedAddress + checked((ulong)offset), count, context: context);
        }

        public void WriteBytes(long offset, byte[] array, int startingIndex, int count, IPeripheral context = null)
        {
            systemBus.WriteBytes(array, redirectedAddress + checked((ulong)offset), count, context: context);
        }

        public void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu)
        {
            this.LoadFileChunks(chunks, cpu);
        }

        public void Reset()
        {

        }

        private readonly ulong redirectedAddress;
        private readonly IBusController systemBus;
    }
}

