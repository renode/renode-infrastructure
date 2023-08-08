//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.UART.Silabs;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class EFR32xG22_USART : EFR32_GenericUSART, IDoubleWordPeripheral
    {
        public EFR32xG22_USART(IMachine machine, uint clockFrequency = 19000000) : base(machine, clockFrequency)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IpVersion, new DoubleWordRegister(this)
                    .WithTag("IPVERSION", 0, 32)
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithTaggedFlag("ENABLE", 0)
                    .WithReservedBits(1, 31)
                },
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
                {(long)Registers.InterruptEnable, GenerateInterruptEnableRegister()},
                {(long)Registers.IrDAControl, GenerateIrDAControlRegister()},
                {(long)Registers.I2SControl, GenerateI2SControlRegister()},
                {(long)Registers.Timing, GenerateTimingRegister()},
                {(long)Registers.ControlExtended, GenerateControlExtendedRegister()},
                {(long)Registers.TimeCompare0, GenerateTimeCompare0Register()},
                {(long)Registers.TimeCompare1, GenerateTimeCompare1Register()},
                {(long)Registers.TimeCompare2, GenerateTimeCompare2Register()},
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
            IpVersion = 0x0,
            Enable = 0x4,
            Control = 0x8,
            FrameFormat = 0xC,
            TriggerControl = 0x10,
            Command = 0x14,
            Status = 0x18,
            ClockControl = 0x1C,
            RxBufferDataExtended = 0x20,
            RxBufferData = 0x24,
            RxBufferDoubleDataExtended = 0x28,
            RxBufferDoubleData = 0x2C,
            RxBufferDataExtendedPeek = 0x30,
            RxBufferDoubleDataExtendedPeek = 0x34,
            TxBufferDataExtended = 0x38,
            TxBufferData = 0x3C,
            TxBufferDoubleDataExtended = 0x40,
            TxBufferDoubleData = 0x44,
            InterruptFlag = 0x48,
            InterruptEnable = 0x4C,
            IrDAControl = 0x50,
            I2SControl = 0x54,
            Timing = 0x58,
            ControlExtended = 0x5c,
            TimeCompare0 = 0x60,
            TimeCompare1 = 0x64,
            TimeCompare2 = 0x68,
        }

        private readonly DoubleWordRegisterCollection registers;
    }
}
