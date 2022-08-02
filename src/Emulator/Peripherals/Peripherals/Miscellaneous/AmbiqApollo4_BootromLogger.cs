//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class AmbiqApollo4_BootromLogger : IDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_BootromLogger(long bootromBaseAddress)
        {
            this.bootromBaseAddress = bootromBaseAddress;
        }

        public uint ReadDoubleWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

        void IPeripheral.Reset()
        {
            // Intentionally left blank.
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // In writes from the Bootrom, LR is the value so the first three bytes should match the bootrom's base address.
            if((value & 0xFFFFFF00) == bootromBaseAddress)
            {
                // LR always points to the address following the 'bl[x]' instruction and might have bit0 set.
                var callerAddress = (value & 0xFFFFFFFE) - 4;
                var callerOffset = callerAddress - bootromBaseAddress;

                this.Log(LogLevel.Error, "Unimplemented BOOTROM function called: {0} (0x{1:X8})", (BootromFunctionOffsets)callerOffset, callerAddress);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public long Size => 0x4;

        private readonly long bootromBaseAddress;

        enum BootromFunctionOffsets
        {
            MassErase = 0x4C,
            PageErase = 0x50,
            ProgramMain = 0x54,
            ProgramInfoArea = 0x58,
            ProgramMain2 = 0x6C,
            ReadWord = 0x74,
            WriteWord = 0x78,
            InfoErase = 0x80,
            Recovery = 0x98,
            Delay = 0x9C,
        }
    }
}
