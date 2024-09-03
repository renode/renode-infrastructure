//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2020-2023 Microchip
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
            var value = 0x0u;
            switch(offset)
            {
                case 0x08: /* DDRPHY_STARTUP */
                    value = 0x200;
                    break;
                case 0x010: /* PLL_DI_0_1 */
                    value = 0x200;
                    break;
                case 0x02C: /* SSCG_REG_2 */
                    value = 0xFFFFFFFF;
                    break;
                case 0x0B4: /* RPC_RESET_MAIN_PLL */
                    value = 0x52454E44; //In HW, this would return 0x00, returning this signature instead.
                    break;
                case 0x864: /* ADDCMD_STATUS0 */
                    value = 0x44332211;
                    break;
                case 0x868: /* ADDCMD_STATUS_1 */
                    value = 0xDDCCBBAA;
                    break;
                case 0x81C: /* GT_ERR_COMB */
                    value = 0x0;
                    break;
                case 0x820: /* GT_CLK_SEL */
                    value = 0x0;
                    break;
                case 0x824: /* GT_TXDLY */
                    value = 0xFFFFFFFF;
                    break;
                case 0x834: /* DQ_DQS_ERR_DONE */
                    value = 0x8;
                    break;
                case 0x84C: /* DQDQS_STATUS1 */
                    value = 0xFF;
                    break;
                case 0x850: /* DQDQS_STATUS2 */
                    value = 0xFF;
                    break;
                case 0xC08: /* PLL_CNTL */
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
            byte value = 0x0;

            switch(offset)
            {
                case 0x814:
                    value = (byte)(1 << byteRead);
                    byteRead = (byteRead + 1) % 5;
                    break;
                case 0x834:
                    value = 0x8;
                    break;
                case 0x84C:
                case 0x84D:
                case 0x84E:
                case 0x84F:
                    value = 0xFF;
                    break;
                default:
                    value = 0x0;
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
            byteRead = 0x0;
            readToggle = 0x0;
        }

        public long Size => 0x1000; // Size is address space on sysbus

        private uint readToggle = 0x0;
        private int byteRead = 0x0;
    }
}
