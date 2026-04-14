//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class AescUART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AescUART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
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

        public long Size => 0x28;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 0;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

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
            Registers.DataWidth.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "dataWidth", valueProviderCallback: _ => 8)
            ;

            Registers.SamplingSizes.Define(this)
                .WithTag("samplingSizes", 0, 32)
            ;

            Registers.FifoDepths.Define(this)
                .WithTag("fifoDepths", 0, 32)
            ;

            Registers.Permissions.Define(this)
                .WithTag("permissions", 0, 32)
            ;

            Registers.ReadWrite.Define(this)
                .WithValueField(0, 8,
                    writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                    valueProviderCallback: _ =>
                    {
                        lastReadValid = Count > 0;
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Debug, "Trying to read from an empty Rx FIFO.");
                        }
                        return character;
                    }, name: "readWrite")
                .WithReservedBits(8, 8)
                .WithFlag(16, FieldMode.Read, name: "rxValid", valueProviderCallback: _ => lastReadValid)
                .WithReservedBits(17, 15)
            ;

            Registers.FifoStatus.Define(this)
                .WithReservedBits(0, 16)
                .WithValueField(16, 8, FieldMode.Read, name: "txFifoCount", valueProviderCallback: _ => 1)
                .WithReservedBits(24, 8)
            ;

            Registers.ClockDiv.Define(this)
                .WithTag("clockDiv", 0, 32)
            ;

            Registers.FrameCfg.Define(this)
                .WithTag("frameCfg", 0, 32)
            ;

            Registers.InterruptPending.Define(this)
                .WithFlag(0, FieldMode.Read, name: "txPending", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "rxPending", valueProviderCallback: _ => Count != 0)
                .WithReservedBits(2, 30)
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out txInterruptEnabled, name: "txInterruptEnable")
                .WithFlag(1, out rxInterruptEnabled, name: "rxInterruptEnable")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
        }

        private void UpdateInterrupts()
        {
            var pending = rxInterruptEnabled.Value && Count != 0;
            IRQ.Set(pending);
        }

        private bool lastReadValid;
        private IFlagRegisterField txInterruptEnabled;
        private IFlagRegisterField rxInterruptEnabled;

        private enum Registers : long
        {
            DataWidth = 0x00,
            SamplingSizes = 0x04,
            FifoDepths = 0x08,
            Permissions = 0x0C,
            ReadWrite = 0x10,
            FifoStatus = 0x14,
            ClockDiv = 0x18,
            FrameCfg = 0x1C,
            InterruptPending = 0x20,
            InterruptEnable = 0x24,
        }
    }
}
