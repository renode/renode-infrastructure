//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

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
                    .WithTaggedFlag("ENABLE", 0)
                    .WithReservedBits(1, 31)
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
                {(long)Registers.InterruptFlagClear, GenerateInterruptFlagClearRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, map);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        private readonly DoubleWordRegisterCollection registers;

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
            InterruptFlagClear = 0x203C
        }
    }
}
