//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class EFR32xG2_I2CController : EFR32_GenericI2CController, IDoubleWordPeripheral
    {
        public EFR32xG2_I2CController(IMachine machine) : base(machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IpVersion, new DoubleWordRegister(this)
                    .WithTag("IPVERSION", 0, 32)
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "DISABLING")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Control, GenerateControlRegister()},
                {(long)Registers.Command, GenerateCommandRegister()},
                {(long)Registers.State, GenerateStateRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.ClockDivision, GenerateClockDivisionRegister()},
                {(long)Registers.SlaveAddress, GenerateSlaveAddressRegister()},
                {(long)Registers.SlaveAddressMask, GenerateSlaveAddressMaskRegister()},
                {(long)Registers.ReceiveBufferData, GenerateReceiveBufferDataRegister()},
                {(long)Registers.ReceiveBufferDoubleData, GenerateReceiveBufferDoubleDataRegister()},
                {(long)Registers.ReceiveBufferDataPeek, GenerateReceiveBufferDataPeekRegister()},
                {(long)Registers.ReceiveBufferDoubleDataPeek, GenerateReceiveBufferDoubleDataPeekRegister()},
                {(long)Registers.TransmitBufferData, GenerateTransmitBufferDataRegister()},
                {(long)Registers.TransmitBufferDoubleData, GenerateTransmitBufferDoubleDataRegister()},
                {(long)Registers.InterruptFlag, GenerateInterruptFlagRegister()},
                {(long)Registers.InterruptEnable, GenerateInterruptEnableRegister()},
                {(long)Registers.InterruptFlagSet, GenerateInterruptFlagSetRegister()},
                {(long)Registers.InterruptFlagClear, GenerateInterruptFlagClearRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, map);
        }

        public uint ReadDoubleWord(long offset)
        {
            if((offset & AliasMask) != 0)
            {
                this.Log(LogLevel.Warning, "Reading from Set/Clr/Tgl alias at offset 0x{0:X} is not supported", offset);
            }
            return registers.Read(offset & InternalOffsetMask);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // Dedicated alias registers (e.g., IF_SET / IF_CLR) handle their own semantics directly.
            if(registers.HasRegisterAtOffset(offset))
            {
                registers.Write(offset, value);
                return;
            }

            var internalOffset = offset & InternalOffsetMask;
            switch(offset & AliasMask)
            {
            case SetRegisterOffset:
                registers.Write(internalOffset, registers.Read(internalOffset) | value);
                break;
            case ClearRegisterOffset:
                registers.Write(internalOffset, registers.Read(internalOffset) & ~value);
                break;
            case ToggleRegisterOffset:
                if(internalOffset == (long)Registers.InterruptFlag)
                {
                    var current = registers.Read(internalOffset);
                    registers.Write((long)Registers.InterruptFlagClear, current & value);
                    registers.Write((long)Registers.InterruptFlagSet, ~current & value);
                }
                else
                {
                    registers.Write(internalOffset, registers.Read(internalOffset) ^ value);
                }
                break;
            default:
                registers.Write(offset, value);
                break;
            }
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        private readonly DoubleWordRegisterCollection registers;

        private const long InternalOffsetMask = 0x0FFF;
        private const long AliasMask = 0xF000;
        private const long SetRegisterOffset = 0x1000;
        private const long ClearRegisterOffset = 0x2000;
        private const long ToggleRegisterOffset = 0x3000;

        private enum Registers
        {
            IpVersion = 0x0,
            Enable = 0x4,
            Control = 0x08,
            Command = 0x0C,
            State = 0x10,
            Status = 0x14,
            ClockDivision = 0x18,
            SlaveAddress = 0x1C,
            SlaveAddressMask = 0x20,
            ReceiveBufferData = 0x24,
            ReceiveBufferDoubleData = 0x28,
            ReceiveBufferDataPeek = 0x2C,
            ReceiveBufferDoubleDataPeek = 0x30,
            TransmitBufferData = 0x34,
            TransmitBufferDoubleData = 0x38,
            InterruptFlag = 0x3C,
            InterruptEnable = 0x40,
            InterruptFlagSet = 0x103C,
            InterruptFlagClear = 0x203C
        }
    }
}