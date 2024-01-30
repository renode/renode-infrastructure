//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class GD32_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public GD32_UART(IMachine machine, bool extendedMode = false) : base(machine)
        {
            IRQ = new GPIO();
            registers = new DoubleWordRegisterCollection(this);

            if(extendedMode)
            {
                DefineExtendedModeRegisters();
            }
            else
            {
                DefineBasicModeRegisters();
            }
        }

        private void DefineBasicModeRegisters()
        {
            registers.DefineRegister((long)BasicModeRegisters.Status0)
                .WithFlag(0, FieldMode.Read, name: "PERR - Parity error flag", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "FERR - Frame error flag", valueProviderCallback: _ => false)
                .WithFlag(2, FieldMode.Read, name: "NERR - Noise error flag", valueProviderCallback: _ => false)
                .WithFlag(3, FieldMode.Read, name: "ORERR - Overrun error", valueProviderCallback: _ => false)
                .WithFlag(4, FieldMode.Read, name: "IDLEF - IDLE frame detected flag", valueProviderCallback: _ => false)
                .WithFlag(5, FieldMode.Read, name: "RBNE - Read data buffer not empty", valueProviderCallback: _ => Count > 0)
                .WithFlag(6, FieldMode.Read, name: "TC - Transmission complete", valueProviderCallback: _ => false)
                .WithFlag(7, FieldMode.Read, name: "TBE - Transmit data buffer empty", valueProviderCallback: _ => true)
                .WithFlag(8, FieldMode.Read, name: "LBDF - LIN break detection flag", valueProviderCallback: _ => false)
                .WithFlag(9, FieldMode.Read, name: "CTSF - CTS change flag", valueProviderCallback: _ => false)
                .WithReservedBits(10, 22)
            ;

            registers.DefineRegister((long)BasicModeRegisters.Data)
                .WithValueField(0, 8, name: "DATA - Data",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var c))
                        {
                            this.Log(LogLevel.Warning, "Tried to read from an empty FIFO");
                            return 0;
                        }
                        return c;
                    },
                    writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(8, 24)
            ;
        }

        private void DefineExtendedModeRegisters()
        {
            registers.DefineRegister((long)ExtendedModeRegisters.Status)
                .WithFlag(0, FieldMode.Read, name: "PERR - Parity error flag", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "FERR - Frame error flag", valueProviderCallback: _ => false)
                .WithFlag(2, FieldMode.Read, name: "NERR - Noise error flag", valueProviderCallback: _ => false)
                .WithFlag(3, FieldMode.Read, name: "ORERR - Overrun error", valueProviderCallback: _ => false)
                .WithFlag(4, FieldMode.Read, name: "IDLEF - IDLE frame detected flag", valueProviderCallback: _ => false)
                .WithFlag(5, FieldMode.Read, name: "RBNE - Read data buffer not empty", valueProviderCallback: _ => Count > 0)
                .WithFlag(6, FieldMode.Read, name: "TC - Transmission complete", valueProviderCallback: _ => false)
                .WithFlag(7, FieldMode.Read, name: "TBE - Transmit data buffer empty", valueProviderCallback: _ => true)
                .WithFlag(8, FieldMode.Read, name: "LBDF - LIN break detection flag", valueProviderCallback: _ => false)
                .WithFlag(9, FieldMode.Read, name: "CTSF - CTS change flag", valueProviderCallback: _ => false)
                .WithFlag(10, FieldMode.Read, name: "CTS - CTS level", valueProviderCallback: _ => false)
                .WithFlag(11, FieldMode.Read, name: "RTF - Receiver timeout", valueProviderCallback: _ => false)
                .WithFlag(12, FieldMode.Read, name: "EBF - End of block", valueProviderCallback: _ => false)
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Read, name: "BSY - Busy", valueProviderCallback: _ => false)
                .WithFlag(17, FieldMode.Read, name: "AMF - ADDR match", valueProviderCallback: _ => false)
                .WithFlag(18, FieldMode.Read, name: "SBF - Send break", valueProviderCallback: _ => false)
                .WithFlag(19, FieldMode.Read, name: "RWU - Receiver wakeup from mute", valueProviderCallback: _ => false)
                .WithFlag(20, FieldMode.Read, name: "WUF - Wakeup from deep-sleep mode", valueProviderCallback: _ => false)
                .WithFlag(21, FieldMode.Read, name: "TEA - Transmit enable acknowledge", valueProviderCallback: _ => false)
                .WithFlag(22, FieldMode.Read, name: "REA - Receive enable acknowledge", valueProviderCallback: _ => false)
                .WithReservedBits(23, 9)
            ;

            registers.DefineRegister((long)ExtendedModeRegisters.ReceiveData)
                .WithValueField(0, 8, FieldMode.Read, name: "RDATA - Receive data",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var c))
                        {
                            this.Log(LogLevel.Warning, "Tried to read from an empty FIFO");
                            return 0;
                        }
                        return c;
                    })
                .WithReservedBits(8, 24)
            ;

            registers.DefineRegister((long)ExtendedModeRegisters.TransmitData)
                .WithValueField(0, 9, name: "TDATA - Transmit data",
                    writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(9, 23)
            ;
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public override uint BaudRate => 115200;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.One;

        public long Size => 0x400;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum BasicModeRegisters
        {
            Status0 = 0x0,
            Data = 0x4,
            BaudRate = 0x8,
            Control0 = 0xC,
            Control1 = 0x10,
            Control2 = 0x14,
            GuardTimePrescaler = 0x18,
            Control3 = 0x80,
            ReceiverTimeout = 0x84,
            Status1 = 0x88,
            CoherenceControl = 0xC0,
        }

        private enum ExtendedModeRegisters
        {
            Control0 = 0x0,
            Control1 = 0x4,
            Control2 = 0x8,
            BaudRate = 0xC,
            GuartTimePrescaler = 0x10,
            ReceiverTimeout = 0x14,
            Command = 0x18,
            Status = 0x1C,
            InterruptClear = 0x20,
            ReceiveData = 0x24,
            TransmitData = 0x28,
            ReceiveFifoContolStatus = 0xD0,
        }
    }
}

