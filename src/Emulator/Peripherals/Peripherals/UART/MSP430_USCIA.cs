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
    public class MSP430_USCIA : IUART, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public MSP430_USCIA()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            InterruptEnableRegister = new ByteRegister(this);
            InterruptStatusRegister = new ByteRegister(this, resetValue: 0x02);

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            InterruptEnableRegister.Reset();
            InterruptStatusRegister.Reset();

            interruptTransmitPending.Value = true;
            UpdateInterrupts();

            queue.Clear();
        }

        public void WriteChar(byte character)
        {
            queue.Enqueue(character);
            interruptReceivePending.Value = true;
            UpdateInterrupts();
        }

        [ConnectionRegionAttribute("interruptEnable")]
        public void WriteByteToInterruptEnable(long offset, byte value)
        {
            InterruptEnableRegister.Write(offset, value);
        }

        [ConnectionRegionAttribute("interruptEnable")]
        public byte ReadByteFromInterruptEnable(long offset)
        {
            return InterruptEnableRegister.Read();
        }

        [ConnectionRegionAttribute("interruptStatus")]
        public void WriteByteToInterruptStatus(long offset, byte value)
        {
            InterruptStatusRegister.Write(offset, value);
            UpdateInterrupts();
        }

        [ConnectionRegionAttribute("interruptStatus")]
        public byte ReadByteFromInterruptStatus(long offset)
        {
            return InterruptStatusRegister.Read();
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public ByteRegisterCollection RegistersCollection { get; }
        public ByteRegister InterruptEnableRegister { get; }
        public ByteRegister InterruptStatusRegister { get; }

        public long Size => 0xB;

        public GPIO RxInterrupt { get; } = new GPIO();
        public GPIO TxInterrupt { get; } = new GPIO();

        public Bits StopBits { get; }
        public Parity ParityBit { get; }
        public uint BaudRate { get; }

        public event Action<byte> CharReceived;

        private void UpdateInterrupts()
        {
            var rxInterrupt = interruptReceivePending.Value && interruptReceiveEnabled.Value;
            var txInterrupt = interruptTransmitPending.Value && interruptTransmitEnabled.Value;

            this.Log(LogLevel.Debug, "RxInterrupt={0} TxInterrupt={1}", rxInterrupt, txInterrupt);

            RxInterrupt.Set(rxInterrupt);
            TxInterrupt.Set(txInterrupt);
        }

        private void DefineRegisters()
        {
            Registers.Control0.Define(this)
                .WithFlag(0, out syncMode, name: "UCSYNC")
                .WithValueField(1, 2, out usciMode, name: "UCMODEx")
                .WithTaggedFlag("UCSPB", 3)
                .WithTaggedFlag("UC7BIT", 4)
                .WithTaggedFlag("UCMSB", 5)
                .WithTaggedFlag("UCPAR", 6)
                .WithTaggedFlag("UCPEN", 7)
            ;

            Registers.Control1.Define(this)
                .WithTaggedFlag("UCSWRST", 0)
                .WithTaggedFlag("UCTXBRK", 1)
                .WithTaggedFlag("UCTXADDR", 2)
                .WithTaggedFlag("UCDORM", 3)
                .WithTaggedFlag("UCBRKIE", 4)
                .WithTaggedFlag("UCRXEIE", 5)
                .WithTag("UCSSEL", 6, 2)
            ;

            Registers.Status.Define(this)
                .WithTaggedFlag("UCBUSY", 0)
                .WithFlag(1, FieldMode.Read, name: "UCADDR",
                    valueProviderCallback: _ => false)
                .WithTaggedFlag("UCRXERR", 2)
                .WithTaggedFlag("UCBRK", 3)
                .WithTaggedFlag("UCPE", 4)
                .WithTaggedFlag("UCOE", 5)
                .WithTaggedFlag("UCFE", 6)
                .WithFlag(7, out loopbackEnabled, name: "UCLISTEN")
            ;

            Registers.ReceiveBuffer.Define(this)
                .WithValueField(0, 8, name: "UCRXBUFx",
                    valueProviderCallback: _ =>
                    {
                        var returnValue = queue.TryDequeue(out var character) ? (byte)character : (byte)0;

                        if(queue.Count > 0)
                        {
                            interruptReceivePending.Value = true;
                            UpdateInterrupts();
                        }

                        return returnValue;
                    })
            ;

            Registers.TransmitBuffer.Define(this)
                .WithValueField(0, 8, name: "UCTXBUFx",
                    valueProviderCallback: _ => 0,
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

            InterruptEnableRegister
                .WithFlag(0, out interruptReceiveEnabled, name: "UCAxRXIE")
                .WithFlag(1, out interruptTransmitEnabled, name: "UCAxTXIE")
                .WithValueField(2, 6, name: "RESERVED")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            InterruptStatusRegister
                .WithFlag(0, out interruptReceivePending, name: "UCAxRXIFG")
                .WithFlag(1, out interruptTransmitPending, name: "UCAxTXIFG")
                .WithValueField(2, 6, name: "RESERVED")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
        }

        private Mode CurrentMode =>
            !syncMode.Value ? Mode.UART : usciMode.Value == 0x3 ? Mode.I2C : Mode.SPI;

        private IFlagRegisterField syncMode;
        private IValueRegisterField usciMode;

        private IFlagRegisterField interruptTransmitEnabled;
        private IFlagRegisterField interruptReceiveEnabled;

        private IFlagRegisterField interruptTransmitPending;
        private IFlagRegisterField interruptReceivePending;

        private IFlagRegisterField loopbackEnabled;

        private readonly Queue<byte> queue = new Queue<byte>();

        public enum Mode
        {
            UART,
            SPI,
            I2C,
        }

        public enum Registers
        {
            AutoBaud,
            IrDATransmit,
            IrDAReceive,
            Control0,
            Control1,
            BaudRate0,
            BaudRate1,
            Modulation,
            Status,
            ReceiveBuffer,
            TransmitBuffer,
        }
    }
}
