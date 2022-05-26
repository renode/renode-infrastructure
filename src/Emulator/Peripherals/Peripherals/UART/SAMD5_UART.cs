//
// Copyright (c) 2010-2022 Antmicro
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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SAMD5_UART : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public SAMD5_UART(Machine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();
            
            Reset();
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
        }

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

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
            receiveComplete.Value = false;
            UpdateInterrupt();
        }

        private void DefineRegisters()
        {
            Registers.DataRegister.Define(this)
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
                .WithReservedBits(8, 24)
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
            var flag = (dataRegisterEmptyInterruptEnable.Value && Count > 0)
                || (transmitCompleteInterruptEnable.Value && transmitComplete.Value)
                || (receiveCompleteInterruptEnable.Value && receiveComplete.Value)
                || (receiveStartInterruptEnable.Value && receiveStart.Value);

            this.Log(LogLevel.Warning, "IRQ set to: {0}", flag);
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
            ControlA = 0x0,
            ControlB = 0x4,
            ControlC = 0x8,
            Baud = 0xC,
            ReceivePulseLength = 0xE,
            InterruptEnableClear = 0x14,
            InterruptEnableSet = 0x16,
            InterruptFlag = 0x18,
            Status = 0x1A,
            SynchronizationBusy = 0x1C,
            ReceiveErrorCount = 0x20,
            Length = 0x22,
            DataRegister = 0x28,
            DebugControl = 0x30
        }
    }
}

