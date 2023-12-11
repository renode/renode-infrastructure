//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Exceptions;
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class BCM2711_AUX_UART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public BCM2711_AUX_UART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
           return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint val)
        {
            RegistersCollection.Write(offset, val);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        public long Size => 0x40;

        public GPIO IRQ { get; }

        protected override void CharWritten()
        {
            // intentionally left empty
        }

        protected override void QueueEmptied()
        {
            // intentionally left empty
        }

        private void DefineRegisters()
        {
            Registers.MiniUart_IO.Define(this)
                .WithValueField(0, 8, name: "Receive data read/write",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var b))
                        {
                            this.Log(LogLevel.Warning, "Tried to read from an empty FIFO");
                            return 0;
                        }
                        return b;
                    },
                    writeCallback: (_, v) =>
                    {
                        TransmitCharacter((byte)v);
                    })
                .WithReservedBits(8, 24);

            Registers.MiniUart_LineStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "Data ready", valueProviderCallback: _ => Count > 0)
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "Receiver overrun", valueProviderCallback: _ => false)
                .WithReservedBits(2, 2)
                .WithFlag(5, FieldMode.Read, name: "Transmitter empty", valueProviderCallback: _ => true)
                .WithFlag(6, FieldMode.Read, name: "Transmitter idle", valueProviderCallback: _ => true)
                .WithReservedBits(7, 25);
        }

        private enum Registers
        {
            MiniUart_IO = 0x0, // AUX_MU_IO_REG, Mini UART I/O Data
            MiniUart_InterruptEnable = 0x4, // AUX_MU_IER_REG, Mini UART Interrupt Enable
            MiniUart_InterruptIdentify = 0x8, // AUX_MU_IIR_REG, Mini UART Interrupt Identify
            MiniUart_LineControl = 0xc, // AUX_MU_LCR_REG, Mini UART Line Control
            MiniUart_ModemControl = 0x10, // AUX_MU_MCR_REG, Mini UART Modem Control
            MiniUart_LineStatus = 0x14, // AUX_MU_LSR_REG, Mini UART Line Status
            MiniUart_ModemStatus = 0x18, // AUX_MU_MSR_REG, Mini UART Modem Status
            MiniUart_Scratch = 0x1c, // AUX_MU_SCRATCH, Mini UART Scratch
            MiniUart_ExtraControl = 0x20, // AUX_MU_CNTL_REG, Mini UART Extra Control
            MiniUart_Status = 0x24, // AUX_MU_STAT_REG, Mini UART Extra Status
            MiniUart_Baudrate = 0x28, // AUX_MU_BAUD_REG, Mini UART Baudrate
        }
    }
}
