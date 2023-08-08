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
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte | AllowedTranslation.WordToByte)]
    public class SAMD5_UART : UARTBase, IBytePeripheral, IKnownSize, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public SAMD5_UART(IMachine machine) : base(machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public GPIO IRQ { get; }

        public ByteRegisterCollection RegistersCollection { get; }

        public long Size => 0x100;

        public override Bits StopBits => Bits.None;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 0;

        protected override void CharWritten()
        {
            receiveStart.Value = true;
            receiveComplete.Value = true;
            UpdateInterrupt();
        }

        protected override void QueueEmptied()
        {
            receiveStart.Value = false;
            receiveComplete.Value = false;
            UpdateInterrupt();
        }

        private void DefineRegisters()
        {
            Registers.DataRegister0.Define(this)
                .WithValueField(0, 8, name: "DATA",
                    valueProviderCallback: _ =>
                    {
                        if(TryGetCharacter(out var b))
                        {
                            return b;
                        }
                        
                        this.Log(LogLevel.Warning, "Tried to read DATA, but there is nothing in the queue");
                        return 0;
                    },
                    writeCallback: (_, val) => 
                    {
                        TransmitCharacter((byte)val);
                        transmitComplete.Value = true;
                        UpdateInterrupt();
                    })
            ;

            Registers.InterruptFlag.Define(this)
                .WithFlag(0, FieldMode.Read, name: "DRE - Data Register Empty", valueProviderCallback: _ => Count == 0)
                .WithFlag(1, out transmitComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "TXC - Transmit Complete")
                .WithFlag(2, out receiveComplete, FieldMode.Read, name: "RXC - Receive Complete")
                .WithFlag(3, out receiveStart, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXS - Receive Start")
                .WithTaggedFlag("CTSIC - Clear To Send Input Change", 4)
                .WithTaggedFlag("RXBRK - Receive Break", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("ERROR", 7)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;
            
            Registers.InterruptEnableClear.Define(this)
                .WithFlag(0, out dataRegisterEmptyInterruptEnable, FieldMode.Read | FieldMode.WriteOneToClear, name: "DRE - Data Register Empty Interrupt Enable")
                .WithFlag(1, out transmitCompleteInterruptEnable, FieldMode.Read | FieldMode.WriteOneToClear, name: "TXC - Transmit Complete Interrupt Enable")
                .WithFlag(2, out receiveCompleteInterruptEnable, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXC - Receive Complete Interrupt Enable")
                .WithFlag(3, out receiveStartInterruptEnable, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXS - Receive Start Interrupt Enable")
                .WithTaggedFlag("CTSIC - Clear To Send Input Change Interrupt Enable", 4)
                .WithTaggedFlag("RXBRK - Receive Break Interrupt Enable", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("ERROR Interrupt Enable", 7)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.InterruptEnableSet.Define(this)
                .WithFlag(0, name: "DRE - Data Register Empty Interrupt Enable", valueProviderCallback: _ => dataRegisterEmptyInterruptEnable.Value, writeCallback: (_, val) => { dataRegisterEmptyInterruptEnable.Value |= val; })
                .WithFlag(1, name: "TXC - Transmit Complete Interrupt Enable", valueProviderCallback: _ => transmitCompleteInterruptEnable.Value, writeCallback: (_, val) => { transmitCompleteInterruptEnable.Value |= val; })
                .WithFlag(2, name: "RXC - Receive Complete Interrupt Enable", valueProviderCallback: _ => receiveCompleteInterruptEnable.Value, writeCallback: (_, val) => { receiveCompleteInterruptEnable.Value |= val; })
                .WithFlag(3, name: "RXS - Receive Start Interrupt Enable", valueProviderCallback: _ => receiveStartInterruptEnable.Value, writeCallback: (_, val) => { receiveStartInterruptEnable.Value |= val; })
                .WithTaggedFlag("CTSIC - Clear To Send Input Change Interrupt Enable", 4)
                .WithTaggedFlag("RXBRK - Receive Break Interrupt Enable", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("ERROR Interrupt Enable", 7)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;
        }

        private void UpdateInterrupt()
        {
            var flag = (dataRegisterEmptyInterruptEnable.Value && Count == 0)
                || (transmitCompleteInterruptEnable.Value && transmitComplete.Value)
                || (receiveCompleteInterruptEnable.Value && receiveComplete.Value)
                || (receiveStartInterruptEnable.Value && receiveStart.Value);

            this.Log(LogLevel.Debug, "IRQ set to: {0}", flag);
            IRQ.Set(flag);
        }

        private IFlagRegisterField transmitComplete;
        private IFlagRegisterField receiveComplete;
        private IFlagRegisterField receiveStart;
        
        private IFlagRegisterField dataRegisterEmptyInterruptEnable;
        private IFlagRegisterField transmitCompleteInterruptEnable;
        private IFlagRegisterField receiveCompleteInterruptEnable;
        private IFlagRegisterField receiveStartInterruptEnable;

        private enum Registers : long
        {
            ControlA0 = 0x0,
            ControlA1 = 0x1,
            ControlA2 = 0x2,
            ControlA3 = 0x3,
            ControlB0 = 0x4,
            ControlB1 = 0x5,
            ControlB2 = 0x6,
            ControlB3 = 0x7,
            ControlC0 = 0x8,
            ControlC1 = 0x9,
            ControlC2 = 0xA,
            ControlC3 = 0xB,
            Baud0 = 0xC,
            Baud1 = 0xD,
            ReceivePulseLength = 0xE,
            InterruptEnableClear = 0x14,
            InterruptEnableSet = 0x16,
            InterruptFlag = 0x18,
            Status0 = 0x1A,
            Status1 = 0x1B,
            SynchronizationBusy0 = 0x1C,
            SynchronizationBusy1 = 0x1D,
            SynchronizationBusy2 = 0x1E,
            SynchronizationBusy3 = 0x1F,
            ReceiveErrorCount = 0x20,
            Length0 = 0x22,
            Length1 = 0x23,
            DataRegister0 = 0x28,
            DataRegister1 = 0x29,
            DataRegister2 = 0x2A,
            DataRegister3 = 0x2B,
            DebugControl = 0x30
        }
    }
}

