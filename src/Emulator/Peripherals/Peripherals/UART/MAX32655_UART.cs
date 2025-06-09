//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class MAX32655_UART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public MAX32655_UART(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
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
            IRQ.Unset();
        }

        public override uint BaudRate => 9600;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.Even;

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            // intentionally left empty
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithValueField(0, 4, out receiveFifoThreshold, changeCallback: (_, __) =>
                    {
                        if(receiveFifoThreshold.Value < 1 || receiveFifoThreshold.Value > ReceiveFifoDepth)
                        {
                            this.WarningLog("Receive FIFO Threshold set to reserved value ({0})", receiveFifoThreshold.Value);
                        }
                    }, name: "rx_thd_val"
                )
                .WithTaggedFlag("par_en", 4)
                .WithTaggedFlag("par_eo", 5)
                .WithTaggedFlag("par_md", 6)
                .WithTaggedFlag("cts_dis", 7)
                .WithTaggedFlag("tx_flush", 8)
                .WithFlag(9, writeCallback: (_, value) => { if(value) ClearBuffer(); }, name: "rx_flush")
                .WithTag("char_size", 10, 2)
                .WithTaggedFlag("stopbits", 12)
                .WithTaggedFlag("hfc_en", 13)
                .WithTaggedFlag("rtsdc", 14)
                .WithFlag(15, out baudClockReady, name: "bclken")
                .WithTag("bclksrc", 16, 2)
                .WithTaggedFlag("dpfe_en", 18)
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => baudClockReady.Value, name: "bclkrdy")
                .WithTaggedFlag("ucagm", 20)
                .WithTaggedFlag("fdm", 21)
                .WithTaggedFlag("desm", 22)
                .WithReservedBits(23, 9)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "tx_busy")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "rx_busy")
                .WithReservedBits(2, 2)
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "rx_em")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => Count >= ReceiveFifoDepth, name: "rx_full")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => true, name: "tx_em")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => false, name: "tx_full")
                .WithValueField(8, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)Count.Clamp(0, ReceiveFifoDepth), name: "rx_lvl")
                .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => 0, name: "tx_lvl")
                .WithReservedBits(16, 16)
            ;

            Registers.InterruptEnable.Define(this)
                .WithTaggedFlag("rx_ferr", 0)
                .WithTaggedFlag("rx_par", 1)
                .WithTaggedFlag("cts_ev", 2)
                .WithTaggedFlag("rx_ov", 3)
                .WithFlag(4, out receiveFifoThresholdInterruptEnable, name: "rx_thd")
                .WithReservedBits(5, 1)
                .WithFlag(6, out transmitFifoHalfEmptyInteruptEnable, name: "tx_he")
                .WithReservedBits(7, 25)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptFlag.Define(this)
                .WithTaggedFlag("rx_ferr", 0)
                .WithTaggedFlag("rx_par", 1)
                .WithTaggedFlag("cts_ev", 2)
                .WithTaggedFlag("rx_ov", 3)
                .WithFlag(4, out receiveFifoThresholdInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "rx_thd")
                .WithReservedBits(5, 1)
                .WithFlag(6, out transmitFifoHalfEmptyInterupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_he")
                .WithReservedBits(7, 25)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ClockDivisor.Define(this)
                .WithTag("clkdiv", 0, 20)
                .WithReservedBits(20, 12)
            ;

            Registers.OversamplingControl.Define(this)
                .WithTag("osr", 0, 3)
                .WithReservedBits(3, 29)
            ;

            Registers.TransmitFifo.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0, name: "data")
                .WithReservedBits(8, 24)
            ;

            Registers.PinControl.Define(this)
                .WithTaggedFlag("cts", 0)
                .WithTaggedFlag("rts", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.FifoData.Define(this)
                .WithValueField(0, 8, name: "data",
                    valueProviderCallback: _ => TryGetCharacter(out var value) ? (ulong)value : 0,
                    writeCallback: (_, value) =>
                    {
                        TransmitCharacter((byte)value);
                        transmitFifoHalfEmptyInterupt.Value |= true;
                        UpdateInterrupts();
                    }
                )
                .WithTaggedFlag("rx_par", 8)
                .WithReservedBits(9, 23)
            ;

            Registers.DmaControl.Define(this)
                .WithTag("tx_thd_val", 0, 4)
                .WithTaggedFlag("tx_en", 4)
                .WithTag("rx_thd_val", 5, 4)
                .WithTaggedFlag("rx_en", 9)
                .WithReservedBits(10, 22)
            ;

            Registers.WakeupInterruptEnable.Define(this)
                .WithTaggedFlag("rx_ne", 0)
                .WithTaggedFlag("rx_full", 1)
                .WithTaggedFlag("rx_thd", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.WakeupInterruptFlag.Define(this)
                .WithTaggedFlag("rx_ne", 0)
                .WithTaggedFlag("rx_full", 1)
                .WithTaggedFlag("rx_thd", 2)
                .WithReservedBits(3, 29)
            ;
        }

        private void UpdateInterrupts()
        {
            receiveFifoThresholdInterrupt.Value |= Count >= (int)receiveFifoThreshold.Value;
            var state = false;
            state |= receiveFifoThresholdInterrupt.Value && receiveFifoThresholdInterruptEnable.Value;
            state |= transmitFifoHalfEmptyInterupt.Value && transmitFifoHalfEmptyInteruptEnable.Value;
            IRQ.Set(state);
            this.NoisyLog("IRQ {0}", state ? "set" : "unset");
        }

        private IValueRegisterField receiveFifoThreshold;
        private IFlagRegisterField receiveFifoThresholdInterruptEnable;
        private IFlagRegisterField receiveFifoThresholdInterrupt;
        private IFlagRegisterField transmitFifoHalfEmptyInteruptEnable;
        private IFlagRegisterField transmitFifoHalfEmptyInterupt;
        private IFlagRegisterField baudClockReady;

        private const int TransmitFifoDepth = 8;
        private const int ReceiveFifoDepth = 8;

        private enum Registers
        {
            Control = 0x00,
            Status = 0x04,
            InterruptEnable = 0x08,
            InterruptFlag = 0x0C,
            ClockDivisor = 0x10,
            OversamplingControl = 0x14,
            TransmitFifo = 0x18,
            PinControl = 0x1C,
            FifoData = 0x20,
            DmaControl = 0x30,
            WakeupInterruptEnable = 0x34,
            WakeupInterruptFlag = 0x38,
        }
    }
}
