//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using System.IO;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.SD
{
    public sealed class SDHCI : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IDisposable
    {        
        public SDHCI(/*string fileName, int size, BusWidth bits = BusWidth.Bits32, bool nonPersistent = false*/)
        {
        }
        
        public byte ReadByte(long offset)
        {
                this.LogUnhandledRead(offset);
                return 0;
        }

        public ushort ReadWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

        public uint ReadDoubleWord(long offset)
        {
            switch (offset) {
                case 0x24: // SDHC_PRNSTS
                        this.Log(LogLevel.Warning, "Read from 0x24 - SDHC_PRNSTS");
                        return 0xFFFFFFFF;
                default:
                        this.LogUnhandledRead(offset);
                        break;
            }
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            this.LogUnhandledWrite(offset, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            this.LogUnhandledWrite(offset, value);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.LogUnhandledWrite(offset, value);
        }

        public void Reset()
        {
        }
        
        public void Dispose()
        {
        }
        
    }
}

