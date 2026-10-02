//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 ADC Controller — Dual-engine, 16-channel, 10-bit ADC
    // Reference: QEMU hw/adc/aspeed_adc.c
    //
    // Two independent engines (0 and 1), each with 8 channels.
    // Data registers pack two channels per word with 10-bit values.
    // Reading a data register auto-increments channel values (simulated sampling).
    // Threshold bounds checking triggers per-channel interrupt flags.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_ADC : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_ADC()
        {
            IRQ = new GPIO();
            engines = new EngineState[NR_ENGINES];
            for(int i = 0; i < NR_ENGINES; i++)
            {
                engines[i] = new EngineState();
            }
            Reset();
        }

        public long Size => REGISTER_SPACE_SIZE;
        public GPIO IRQ { get; }

        public void Reset()
        {
            for(int i = 0; i < NR_ENGINES; i++)
            {
                Array.Clear(engines[i].Regs, 0, engines[i].Regs.Length);
                engines[i].Regs[R_VGA_DETECT_CTRL] = 0x0000000F;
                engines[i].Regs[R_CLOCK_CTRL] = 0x0000000F;
            }
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset < 0 || offset >= REGISTER_SPACE_SIZE)
            {
                return 0;
            }

            int engineIdx = (int)(offset / ENGINE_SIZE);
            if(engineIdx >= NR_ENGINES)
            {
                return 0;
            }

            var eng = engines[engineIdx];
            uint localOffset = (uint)(offset % ENGINE_SIZE);
            uint reg = localOffset / 4;

            if(reg >= NR_REGS)
            {
                return 0;
            }

            // Data registers: reading triggers auto-increment and threshold check
            if(reg >= R_DATA_CH1_CH0 && reg <= R_DATA_CH7_CH6)
            {
                uint val = eng.Regs[reg];
                // Auto-increment: lower channel += 7, upper channel += 5
                uint lower = val & ADC_L_MASK;
                uint upper = (val >> 16) & ADC_L_MASK;
                lower = (lower + 7) & ADC_L_MASK;
                upper = (upper + 5) & ADC_L_MASK;
                eng.Regs[reg] = lower | (upper << 16);

                // Check thresholds for both channels
                uint dataIdx = reg - R_DATA_CH1_CH0;
                uint lowerCh = dataIdx * 2;
                uint upperCh = dataIdx * 2 + 1;
                CheckThreshold(eng, lowerCh, lower);
                CheckThreshold(eng, upperCh, upper);

                return val;
            }

            return eng.Regs[reg];
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= REGISTER_SPACE_SIZE)
            {
                return;
            }

            int engineIdx = (int)(offset / ENGINE_SIZE);
            if(engineIdx >= NR_ENGINES)
            {
                return;
            }

            var eng = engines[engineIdx];
            uint localOffset = (uint)(offset % ENGINE_SIZE);
            uint reg = localOffset / 4;

            if(reg >= NR_REGS)
            {
                return;
            }

            switch(reg)
            {
                case R_ENGINE_CTRL:
                    // AUTO_COMP (bit 5) is always cleared; INIT (bit 8) is set when EN (bit 0) = 1
                    value &= ~AUTO_COMP;
                    if((value & EN) != 0)
                    {
                        value |= INIT;
                    }
                    eng.Regs[reg] = value;
                    return;

                case R_INT_CTRL:
                    eng.Regs[reg] = value & 0xFF;
                    UpdateIRQ();
                    return;

                case R_VGA_DETECT_CTRL:
                case R_CLOCK_CTRL:
                    eng.Regs[reg] = value;
                    return;

                case R_INT_SOURCE:
                    // W1C — write-1-to-clear
                    eng.Regs[reg] &= ~(value & 0xFFFF);
                    UpdateIRQ();
                    return;

                case R_COMPENSATING:
                    eng.Regs[reg] = value & 0x0F;
                    return;

                default:
                    if(reg >= R_DATA_CH1_CH0 && reg <= R_DATA_CH7_CH6)
                    {
                        eng.Regs[reg] = value & ADC_LH_MASK;
                        return;
                    }
                    if(reg >= R_BOUNDS_CH0 && reg <= R_BOUNDS_CH7)
                    {
                        eng.Regs[reg] = value & ADC_LH_MASK;
                        return;
                    }
                    if(reg >= R_HYST_CH0 && reg <= R_HYST_CH7)
                    {
                        eng.Regs[reg] = value & HYST_MASK;
                        return;
                    }
                    eng.Regs[reg] = value;
                    return;
            }
        }

        private void CheckThreshold(EngineState eng, uint channel, uint value)
        {
            if(channel >= NR_CHANNELS_PER_ENGINE)
            {
                return;
            }
            uint boundsReg = R_BOUNDS_CH0 + channel;
            uint lower = eng.Regs[boundsReg] & ADC_L_MASK;
            uint upper = (eng.Regs[boundsReg] >> 16) & ADC_L_MASK;

            // If bounds are both 0, skip threshold check (default)
            if(lower == 0 && upper == 0)
            {
                return;
            }

            if(value < lower || value > upper)
            {
                eng.Regs[R_INT_SOURCE] |= (uint)(1 << (int)channel);
                UpdateIRQ();
            }
        }

        private void UpdateIRQ()
        {
            // Aggregate: check if any engine has (INT_SOURCE & INT_CTRL) non-zero
            bool pending = false;
            for(int i = 0; i < NR_ENGINES; i++)
            {
                if((engines[i].Regs[R_INT_SOURCE] & engines[i].Regs[R_INT_CTRL]) != 0)
                {
                    pending = true;
                    break;
                }
            }
            IRQ.Set(pending);
        }

        private sealed class EngineState
        {
            public readonly uint[] Regs = new uint[NR_REGS];
        }

        private readonly EngineState[] engines;

        // Constants
        private const int NR_ENGINES = 2;
        private const int NR_CHANNELS_PER_ENGINE = 8;
        private const int ENGINE_SIZE = 0x100;
        private const int NR_REGS = ENGINE_SIZE / 4;  // 64 regs per engine
        private const int REGISTER_SPACE_SIZE = 0x1000;

        // Register offsets (word index within engine)
        private const uint R_ENGINE_CTRL    = 0x00 / 4;
        private const uint R_INT_CTRL       = 0x04 / 4;
        private const uint R_VGA_DETECT_CTRL = 0x08 / 4;
        private const uint R_CLOCK_CTRL     = 0x0C / 4;
        private const uint R_DATA_CH1_CH0   = 0x10 / 4;
        private const uint R_DATA_CH3_CH2   = 0x14 / 4;
        private const uint R_DATA_CH5_CH4   = 0x18 / 4;
        private const uint R_DATA_CH7_CH6   = 0x1C / 4;
        private const uint R_BOUNDS_CH0     = 0x20 / 4;
        private const uint R_BOUNDS_CH1     = 0x24 / 4;
        private const uint R_BOUNDS_CH2     = 0x28 / 4;
        private const uint R_BOUNDS_CH3     = 0x2C / 4;
        private const uint R_BOUNDS_CH4     = 0x30 / 4;
        private const uint R_BOUNDS_CH5     = 0x34 / 4;
        private const uint R_BOUNDS_CH6     = 0x38 / 4;
        private const uint R_BOUNDS_CH7     = 0x3C / 4;
        private const uint R_HYST_CH0       = 0x40 / 4;
        private const uint R_HYST_CH1       = 0x44 / 4;
        private const uint R_HYST_CH2       = 0x48 / 4;
        private const uint R_HYST_CH3       = 0x4C / 4;
        private const uint R_HYST_CH4       = 0x50 / 4;
        private const uint R_HYST_CH5       = 0x54 / 4;
        private const uint R_HYST_CH6       = 0x58 / 4;
        private const uint R_HYST_CH7       = 0x5C / 4;
        private const uint R_INT_SOURCE     = 0x60 / 4;
        private const uint R_COMPENSATING   = 0x64 / 4;

        // Bit masks
        private const uint ADC_L_MASK  = 0x3FF;
        private const uint ADC_LH_MASK = 0x03FF03FF;
        private const uint HYST_MASK   = 0x83FF3FFF;
        private const uint EN          = 0x01;
        private const uint AUTO_COMP   = 0x20;
        private const uint INIT        = 0x100;
    }
}
