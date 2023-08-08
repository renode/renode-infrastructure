//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class Murax_UART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public Murax_UART(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x10;

        public override Bits StopBits => stopBits;

        public override Parity ParityBit => parity;

        public override uint BaudRate => 0;

        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithValueField(0, 8, writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                    valueProviderCallback: _ => {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                        }
                        return character;
                    })
            ;

            Registers.Status.Define(this)
                .WithTag("txInterruptEnabled", 0, 1)
                .WithFlag(1, out rxInterruptEnabled)
                .WithReservedBits(2, 6)
                .WithTag("txInterruptActive", 8, 1)
                .WithFlag(9, FieldMode.Read, name: "rxInterruptActive", valueProviderCallback: _ => Count != 0)
                .WithReservedBits(10, 6)
                .WithTag("txFifoOccupancy", 16, 8)
                .WithTag("rxFifoOccupancy", 24, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.FrameConfig.Define(this)
                .WithTag("frameDataLength", 0, 8)
                .WithEnumField<DoubleWordRegister, InnerParity>(8, 2, name: "parity",
                    writeCallback: (_, val) => 
                    {
                        switch(val)
                        {
                        case InnerParity.None:
                            parity = Parity.None;
                            break;
                        case InnerParity.Even:
                            parity = Parity.Even;
                            break;
                        case InnerParity.Odd:
                            parity = Parity.Odd;
                            break;
                        default:
                            this.Log(LogLevel.Warning, "Unexpected parity value: {0}", val);
                            break;
                        }
                    })
                .WithEnumField<DoubleWordRegister, InnerStopBits>(16, 1, name: "stopBits",
                    writeCallback: (_, val) => 
                    {
                        switch(val)
                        {
                        case InnerStopBits.One:
                            stopBits = Bits.One;
                            break;
                        case InnerStopBits.Two:
                            stopBits = Bits.Two;
                            break;
                        }
                    });
            ;
        }

        private void UpdateInterrupts()
        {
            var eventPending = (rxInterruptEnabled.Value && Count != 0);
            this.Log(LogLevel.Info, "Setting interrupt to: {0}", eventPending);
            IRQ.Set(eventPending);
        }

        private IFlagRegisterField rxInterruptEnabled;
        private Bits stopBits;
        private Parity parity;

        private enum Registers
        {
            Data = 0x0,
            Status = 0x4,
            ClockDivider = 0x8,
            FrameConfig = 0xC
        }

        private enum InnerParity
        {
            None,
            Even,
            Odd
        }

        private enum InnerStopBits
        {
            One,
            Two
        }
    }
}
