// Copyright (c) 2026 Microsoft
// Licensed under the MIT license.
//
// Aspeed AST2600 LPC Controller with 4 KCS channels
// Ported from QEMU hw/misc/aspeed_lpc.c

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Aspeed_LPC : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_LPC()
        {
            registers = new uint[RegisterSpaceSize / 4];
            IRQ = new GPIO();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if (offset < 0 || offset >= RegisterSpaceSize)
            {
                this.Log(LogLevel.Warning, "LPC read out of range: 0x{0:X}", offset);
                return 0;
            }

            int idx = (int)(offset / 4);
            uint val = registers[idx];

            // IDR read side-effects: clear IBF, lower IRQ
            switch (offset)
            {
                case IDR1:
                    ClearIBF(0, STR1);
                    break;
                case IDR2:
                    ClearIBF(1, STR2);
                    break;
                case IDR3:
                    ClearIBF(2, STR3);
                    break;
                case IDR4:
                    ClearIBF(3, STR4);
                    break;
            }

            return val;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (offset < 0 || offset >= RegisterSpaceSize)
            {
                this.Log(LogLevel.Warning, "LPC write out of range: 0x{0:X}", offset);
                return;
            }

            int idx = (int)(offset / 4);

            switch (offset)
            {
                // IDR write: store data, set IBF, raise IRQ if enabled
                case IDR1:
                    registers[idx] = value & 0xFF;
                    SetIBF(0, STR1);
                    break;
                case IDR2:
                    registers[idx] = value & 0xFF;
                    SetIBF(1, STR2);
                    break;
                case IDR3:
                    registers[idx] = value & 0xFF;
                    SetIBF(2, STR3);
                    break;
                case IDR4:
                    registers[idx] = value & 0xFF;
                    SetIBF(3, STR4);
                    break;

                // ODR write: store data, set OBF
                case ODR1:
                    registers[idx] = value & 0xFF;
                    registers[STR1 / 4] |= STR_OBF;
                    break;
                case ODR2:
                    registers[idx] = value & 0xFF;
                    registers[STR2 / 4] |= STR_OBF;
                    break;
                case ODR3:
                    registers[idx] = value & 0xFF;
                    registers[STR3 / 4] |= STR_OBF;
                    break;
                case ODR4:
                    registers[idx] = value & 0xFF;
                    registers[STR4 / 4] |= STR_OBF;
                    break;

                // STR: R/W for all bits
                case STR1:
                case STR2:
                case STR3:
                case STR4:
                    registers[idx] = value & 0xFF;
                    break;

                // HICR0: channel enables — may affect IRQ state
                case HICR0:
                    registers[idx] = value;
                    UpdateIRQ();
                    break;

                // HICR2: IBF IRQ enables
                case HICR2:
                    registers[idx] = value;
                    UpdateIRQ();
                    break;

                // HICR4: KCS3 enable bit
                case HICR4:
                    registers[idx] = value;
                    UpdateIRQ();
                    break;

                // HICRB: KCS4 enable + IBF IRQ enable
                case HICRB:
                    registers[idx] = value;
                    UpdateIRQ();
                    break;

                default:
                    registers[idx] = value;
                    break;
            }
        }

        public void Reset()
        {
            Array.Clear(registers, 0, registers.Length);
            registers[HICR7 / 4] = Hicr7ResetValue;
            subdeviceIrqsPending = 0;
            IRQ.Unset();
        }

        public long Size => RegisterSpaceSize;

        public GPIO IRQ { get; }

        // Configurable HICR7 (chip ID) — survives reset
        public uint Hicr7ResetValue { get; set; } = 0;

        // --- Private helpers ---

        private bool IsChannelEnabled(int ch)
        {
            switch (ch)
            {
                case 0: return (registers[HICR0 / 4] & HICR0_LPC1E) != 0;
                case 1: return (registers[HICR0 / 4] & HICR0_LPC2E) != 0;
                case 2: return (registers[HICR0 / 4] & HICR0_LPC3E) != 0
                             && (registers[HICR4 / 4] & HICR4_KCSENBL) != 0;
                case 3: return (registers[HICRB / 4] & HICRB_LPC4E) != 0;
                default: return false;
            }
        }

        private bool IsIBFIRQEnabled(int ch)
        {
            if (!IsChannelEnabled(ch))
                return false;

            switch (ch)
            {
                case 0: return (registers[HICR2 / 4] & HICR2_IBFIE1) != 0;
                case 1: return (registers[HICR2 / 4] & HICR2_IBFIE2) != 0;
                case 2: return (registers[HICR2 / 4] & HICR2_IBFIE3) != 0;
                case 3: return (registers[HICRB / 4] & HICRB_IBFIE4) != 0;
                default: return false;
            }
        }

        private void SetIBF(int ch, long strOffset)
        {
            registers[strOffset / 4] |= STR_IBF;

            if (IsIBFIRQEnabled(ch))
            {
                subdeviceIrqsPending |= (1u << ch);
                UpdateIRQ();
            }
        }

        private void ClearIBF(int ch, long strOffset)
        {
            bool wasSet = (registers[strOffset / 4] & STR_IBF) != 0;
            registers[strOffset / 4] &= ~STR_IBF;

            if (wasSet)
            {
                subdeviceIrqsPending &= ~(1u << ch);
                UpdateIRQ();
            }
        }

        private void UpdateIRQ()
        {
            if (subdeviceIrqsPending != 0)
                IRQ.Set(true);
            else
                IRQ.Set(false);
        }

        private uint[] registers;
        private uint subdeviceIrqsPending;

        // Register space
        private const int RegisterSpaceSize = 0x1000;

        // HICR registers
        private const long HICR0 = 0x00;
        private const long HICR1 = 0x04;
        private const long HICR2 = 0x08;
        private const long HICR3 = 0x0C;
        private const long HICR4 = 0x10;
        private const long HICR5 = 0x80;
        private const long HICR6 = 0x84;
        private const long HICR7 = 0x88;
        private const long HICR8 = 0x8C;
        private const long HICRB = 0x100;

        // KCS Channel 1-3 registers
        private const long IDR1 = 0x24;
        private const long IDR2 = 0x28;
        private const long IDR3 = 0x2C;
        private const long ODR1 = 0x30;
        private const long ODR2 = 0x34;
        private const long ODR3 = 0x38;
        private const long STR1 = 0x3C;
        private const long STR2 = 0x40;
        private const long STR3 = 0x44;

        // KCS Channel 4 registers
        private const long IDR4 = 0x114;
        private const long ODR4 = 0x118;
        private const long STR4 = 0x11C;

        // HICR0 bits
        private const uint HICR0_LPC1E = (1u << 5);
        private const uint HICR0_LPC2E = (1u << 6);
        private const uint HICR0_LPC3E = (1u << 7);

        // HICR2 bits
        private const uint HICR2_IBFIE1 = (1u << 1);
        private const uint HICR2_IBFIE2 = (1u << 2);
        private const uint HICR2_IBFIE3 = (1u << 3);

        // HICR4 bits
        private const uint HICR4_KCSENBL = (1u << 2);

        // HICRB bits
        private const uint HICRB_LPC4E = (1u << 0);
        private const uint HICRB_IBFIE4 = (1u << 1);

        // STR bits
        private const uint STR_OBF = (1u << 0);
        private const uint STR_IBF = (1u << 1);
    }
}
