//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2020 Hugh Breslin <Hugh.Breslin@microchip.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    /*
     * This class is a mock designed to allow DDR training software to pass.
     * All writes are ignored.
     */
    public class MPFS_DDRMock : IDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public uint ReadDoubleWord(long offset)
        {
            var value = 0u;
            switch(offset)
            {
                case 2148:
                    value = 0x44332211;
                    break;
                case 2152:
                    value = 0xDDCCBBAA;
                    break;
                case 2076:
                    value = 0;
                    break;
                case 2100:
                    value = 8;
                    break;
                case 2124:
                    value = 0xFF;
                    break;
                default:
                    value = 0xFFFFFFFF * readToggle;
                    readToggle = 1 - readToggle;
                    break;
            }
            this.Log(LogLevel.Noisy, "Reading double word from DDR controller - offset: 0x{0:X}, value: 0x{1:X}", offset, value);
            return value;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // intentionally left blank
        }

        public byte ReadByte(long offset)
        {
            byte value = 0;

            switch(offset)
            {
                case 2068:
                    value = (byte)(1 << byteRead);
                    byteRead = (byteRead + 1) % 5;
                    break;
                case 2100:
                    value = 8;
                    break;
                case 2124:
                case 2125:
                case 2126:
                case 2127:
                    value = 0xFF;
                    break;
                default:
                    value = 0;
                    break;
            }
            this.Log(LogLevel.Noisy, "Read byte from DDR controller - offset: 0x{0:X}, value 0x{1:X}", offset, value);
            return value;
        }

        public void WriteByte(long offset, byte value)
        {
            // intentionally left blank
        }

        public void Reset()
        {
            byteRead = 0;
            readToggle = 0;
        }

        public long Size => 2268; // Size is address space on sysbus

        private uint readToggle = 0;
        private int byteRead = 0;
    }
}
