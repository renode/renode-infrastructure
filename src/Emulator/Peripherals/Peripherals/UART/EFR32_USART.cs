//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART.Silabs;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class EFR32_USART : EFR32_GenericUSART, IDoubleWordPeripheral
    {
        public EFR32_USART(IMachine machine, uint clockFrequency = 19000000) : base(machine, clockFrequency)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, GenerateControlRegister()},
                {(long)Registers.FrameFormat, GenerateFrameFormatRegister()},
                {(long)Registers.TriggerControl, GenerateTriggerControlRegister()},
                {(long)Registers.Command, GenerateCommandRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.ClockControl, GenerateClockControlRegister()},
                {(long)Registers.RxBufferDataExtended, GenerateRxBufferDataExtendedRegister()},
                {(long)Registers.RxBufferData, GenerateRxBufferDataRegister()},
                {(long)Registers.RxBufferDoubleDataExtended, GenerateRxBufferDoubleDataExtendedRegister()},
                {(long)Registers.RxBufferDoubleData, GenerateRxBufferDoubleDataRegister()},
                {(long)Registers.RxBufferDataExtendedPeek, GenerateRxBufferDataExtendedPeekRegister()},
                {(long)Registers.RxBufferDoubleDataExtendedPeek, GenerateRxBufferDoubleDataExtendedPeekRegister()},
                {(long)Registers.TxBufferDataExtended, GenerateTxBufferDataExtendedRegister()},
                {(long)Registers.TxBufferData, GenerateTxBufferDataRegister()},
                {(long)Registers.TxBufferDoubleDataExtended, GenerateTxBufferDoubleDataExtendedRegister()},
                {(long)Registers.TxBufferDoubleData, GenerateTxBufferDoubleDataRegister()},
                {(long)Registers.InterruptFlag, GenerateInterruptFlagRegister()},
                {(long)Registers.InterruptFlagSet, GenerateInterruptFlagSetRegister()},
                {(long)Registers.InterruptFlagClear, GenerateInterruptFlagClearRegister()},
                {(long)Registers.InterruptEnable, GenerateInterruptEnableRegister()},
                {(long)Registers.IrDAControl, GenerateIrDAControlRegister()},
                {(long)Registers.USARTInput, GenerateUSARTInputRegister()},
                {(long)Registers.I2SControl, GenerateI2SControlRegister()},
                {(long)Registers.Timing, GenerateTimingRegister()},
                {(long)Registers.ControlExtended, GenerateControlExtendedRegister()},
                {(long)Registers.TimeCompare0, GenerateTimeCompare0Register()},
                {(long)Registers.TimeCompare1, GenerateTimeCompare1Register()},
                {(long)Registers.TimeCompare2, GenerateTimeCompare2Register()},
                {(long)Registers.IORoutingPinEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("RXPEN", 0)
                    .WithTaggedFlag("TXPEN", 1)
                    .WithTaggedFlag("CSPEN", 2)
                    .WithTaggedFlag("CLKPEN", 3)
                    .WithTaggedFlag("CTSPEN", 4)
                    .WithTaggedFlag("RTSPEN", 5)
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.IORoutingLocation0, new DoubleWordRegister(this)
                    .WithTag("RXLOC", 0, 6)
                    .WithReservedBits(6, 2)
                    .WithTag("TXLOC", 8, 6)
                    .WithReservedBits(14, 2)
                    .WithTag("CSLOC", 16, 6)
                    .WithReservedBits(22, 2)
                    .WithTag("CLKLOC", 24, 6)
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.IORoutingLocation1, new DoubleWordRegister(this)
                    .WithTag("CTSLOC", 0, 6)
                    .WithReservedBits(6, 2)
                    .WithTag("RTSLOC", 8, 6)
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.Test, GenerateTestRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public override void Reset()
        {
            base.Reset();
        }

        private enum Registers
        {
            Control = 0x0,
            FrameFormat = 0x4,
            TriggerControl = 0x8,
            Command = 0xC,
            Status = 0x10,
            ClockControl = 0x14,
            RxBufferDataExtended = 0x18,
            RxBufferData = 0x1C,
            RxBufferDoubleDataExtended = 0x20,
            RxBufferDoubleData = 0x24,
            RxBufferDataExtendedPeek = 0x28,
            RxBufferDoubleDataExtendedPeek = 0x2C,
            TxBufferDataExtended = 0x30,
            TxBufferData = 0x34,
            TxBufferDoubleDataExtended = 0x38,
            TxBufferDoubleData = 0x3C,
            InterruptFlag = 0x40,
            InterruptFlagSet = 0x44,
            InterruptFlagClear = 0x48,
            InterruptEnable = 0x4C,
            IrDAControl = 0x50,
            USARTInput = 0x58,
            I2SControl = 0x5C,
            Timing = 0x60,
            ControlExtended = 0x64,
            TimeCompare0 = 0x68,
            TimeCompare1 = 0x6C,
            TimeCompare2 = 0x70,
            IORoutingPinEnable = 0x74,
            IORoutingLocation0 = 0x78,
            IORoutingLocation1 = 0x7C,
            Test = 0x80,
        }

        private readonly DoubleWordRegisterCollection registers;
    }
}
