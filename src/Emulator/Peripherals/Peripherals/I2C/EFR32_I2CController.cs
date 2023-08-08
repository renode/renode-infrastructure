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
    public class EFR32_I2CController : EFR32_GenericI2CController, IDoubleWordPeripheral
    {
        public EFR32_I2CController(IMachine machine) : base(machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
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
                {(long)Registers.InterruptFlagSet, GenerateInterruptFlagSetRegister()},
                {(long)Registers.InterruptFlagClear, GenerateInterruptFlagClearRegister()},
                {(long)Registers.InterruptEnable, GenerateInterruptEnableRegister()},
                {(long)Registers.IORoutingPinEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("SDAPEN", 0)
                    .WithTaggedFlag("SCLPEN", 1)
                },
                {(long)Registers.IORoutingLocation, new DoubleWordRegister(this)
                    .WithTag("SDALOC", 0, 6)
                    .WithReservedBits(6, 2)
                    .WithTag("SCLLOC", 8, 6)
                    .WithReservedBits(14, 18)
                },
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
            Control = 0x00,
            Command = 0x04,
            State = 0x08,
            Status = 0x0C,
            ClockDivision = 0x10,
            SlaveAddress = 0x14,
            SlaveAddressMask = 0x18,
            ReceiveBufferData = 0x1C,
            ReceiveBufferDoubleData = 0x20,
            ReceiveBufferDataPeek = 0x24,
            ReceiveBufferDoubleDataPeek = 0x28,
            TransmitBufferData = 0x2C,
            TransmitBufferDoubleData = 0x30,
            InterruptFlag = 0x34,
            InterruptFlagSet = 0x38,
            InterruptFlagClear = 0x3C,
            InterruptEnable = 0x40,
            IORoutingPinEnable = 0x44,
            IORoutingLocation = 0x48
        }
    }
}
