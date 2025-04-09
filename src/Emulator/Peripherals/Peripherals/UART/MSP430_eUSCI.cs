//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class MSP430_eUSCI : IUART, IWordPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, IKnownSize
    {
        public MSP430_eUSCI()
        {
            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            rxQueue.Clear();

            UpdateInterrupts();
        }

        public ushort ReadWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void WriteChar(byte character)
        {
            rxQueue.Enqueue(character);
            interruptReceivePending.Value = true;
            UpdateInterrupts();
        }

        public event Action<byte> CharReceived;

        public WordRegisterCollection RegistersCollection { get; }

        public long Size => 0x20;

        public GPIO IRQ { get; } = new GPIO();

        public Bits StopBits { get; }
        public Parity ParityBit { get; }
        public uint BaudRate { get; }

        private void UpdateInterrupts()
        {
            var interrupt = false;

            interrupt |= interruptReceiveEnabled.Value && interruptReceivePending.Value;
            interrupt |= interruptTransmitEnabled.Value && interruptTransmitPending.Value;

            this.Log(LogLevel.Debug, "IRQ set to {0}", interrupt);

            IRQ.Set(interrupt);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithTaggedFlag("UCSWRST", 0)
                .WithTaggedFlag("UCSTEM", 1)
                .WithTaggedFlag("UCTXADDR", 2)
                .WithTaggedFlag("UCDORM", 3)
                .WithTaggedFlag("UCBRKIE", 4)
                .WithTaggedFlag("UCRXEIE", 5)
                .WithTag("UCSSEL", 6, 2)
                .WithTaggedFlag("UCSYNC", 8)
                .WithTag("UCMOD", 9, 2)
                .WithTaggedFlag("UCMST", 11)
                .WithTaggedFlag("UC7BIT", 12)
                .WithTaggedFlag("UCMSB", 13)
                .WithTaggedFlag("UCCKPL", 14)
                .WithTaggedFlag("UCCKPH", 15)
            ;

            Registers.Status.Define(this)
                .WithTaggedFlag("UCBUSY", 0)
                .WithTaggedFlag("UCADDR", 1)
                .WithTaggedFlag("UCRXERR", 2)
                .WithTaggedFlag("UCBRK", 3)
                .WithTaggedFlag("UCPE", 4)
                .WithTaggedFlag("UCOE", 5)
                .WithTaggedFlag("UCFE", 6)
                .WithFlag(7, out loopbackEnabled, name: "UCLISTEN")
                .WithReservedBits(8, 8)
            ;

            Registers.ReceiveBuffer.Define(this)
                .WithValueField(0, 8, name: "UCRXBUFx",
                    valueProviderCallback: _ =>
                    {
                        var returnValue = rxQueue.TryDequeue(out var character) ? (byte)character : (byte)0;

                        if(rxQueue.Count > 0)
                        {
                            interruptReceivePending.Value = true;
                            UpdateInterrupts();
                        }

                        return returnValue;
                    })
            ;

            Registers.TransmitBuffer.Define(this)
                .WithValueField(0, 8, name: "UCTXBUFx",
                    writeCallback: (_, value) =>
                    {
                        CharReceived?.Invoke((byte)value);

                        interruptTransmitPending.Value = true;
                        UpdateInterrupts();

                        if(loopbackEnabled.Value)
                        {
                            WriteChar((byte)value);
                        }
                    })
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptReceiveEnabled, name: "UCRXIE")
                .WithFlag(1, out interruptTransmitEnabled, name: "UCTXIE")
                .WithTaggedFlag("UCSTTIE", 2)
                .WithTaggedFlag("UCTXCPTIE", 3)
                .WithReservedBits(4, 12)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptFlag.Define(this)
                .WithFlag(0, out interruptReceivePending, name: "UCRXIFG")
                .WithFlag(1, out interruptTransmitPending, name: "UCTXIFG")
                .WithTaggedFlag("UCSTTIFG", 2)
                .WithTaggedFlag("UCTXCPTIFG", 3)
                .WithReservedBits(4, 12)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptVector.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "UCIVx",
                    valueProviderCallback: _ =>
                    {
                        if(interruptReceivePending.Value)
                        {
                            return 0x2;
                        }
                        else if(interruptTransmitPending.Value)
                        {
                            return 0x4;
                        }
                        return 0x00;
                    })
            ;
        }

        private IFlagRegisterField interruptTransmitEnabled;
        private IFlagRegisterField interruptReceiveEnabled;

        private IFlagRegisterField interruptTransmitPending;
        private IFlagRegisterField interruptReceivePending;

        private IFlagRegisterField loopbackEnabled;

        private readonly Queue<byte> rxQueue = new Queue<byte>();

        private enum Registers
        {
            Control = 0x00,
            BaudRate = 0x06,
            Modulation = 0x08,
            Status = 0x0A,
            ReceiveBuffer = 0x0C,
            TransmitBuffer = 0x0E,
            AutoBaudRate = 0x10,
            IrDA = 0x12,
            InterruptEnable = 0x1A,
            InterruptFlag = 0x1C,
            InterruptVector = 0x1E,
        }
    }
}
