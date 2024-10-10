//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2024 Sean "xobs" Cross
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class ESP32_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public ESP32_UART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            interruptRawStatuses = new bool[InterruptsCount];
            interruptMasks = new bool[InterruptsCount];

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Fifo, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                        valueProviderCallback: _ => {
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from an empty FIFO.");
                            }
                            return character;
                        })
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.RxFilter, new DoubleWordRegister(this)
                    .WithTag("GLITCH_FILT", 0, 8) // when input pulse width is lower than this value, the pulse is ignored.
                    .WithTaggedFlag("GLITCH_FILT_EN", 8) // Set this bit to enable Rx signal filter.
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.RawInterruptStatus, new DoubleWordRegister(this)
                    .WithFlags(0, 20, FieldMode.Read, valueProviderCallback: (interrupt, _) => interruptRawStatuses[interrupt])
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 20, changeCallback: (interrupt, _, newValue) => { interruptMasks[interrupt] = newValue; UpdateInterrupts(); })
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.InterruptClear, new DoubleWordRegister(this)
                    .WithFlags(0, 20, FieldMode.Write, writeCallback: (interrupt, _, newValue) => { if(newValue) ClearInterrupt(interrupt); })
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithTag("RXFIFO_CNT", 0, 10)
                    .WithTaggedFlag("UART_DSRN", 13)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: (_) => true, name: "UART_CTSN")
                    .WithFlag(15, FieldMode.Read, valueProviderCallback: (_) => true, name: "UART_RXD")
                    .WithTag("TXFIFO_CNT", 16, 10)
                    .WithFlag(29, FieldMode.Read, valueProviderCallback: (_) => true, name: "UART_DTRN")
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: (_) => true, name: "UART_RTSN")
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: (_) => true, name: "UART_TXD")
                },
                {(long)Registers.Config0, new DoubleWordRegister(this)
                    .WithTaggedFlag("UART_PARITY", 0)
                    .WithTaggedFlag("UART_PARITY_EN", 1)
                    .WithTag("UART_BIT_NUM", 2, 2)
                    .WithTag("UART_STOPBIT_NUM", 4, 2)
                    .WithTaggedFlag("UART_RXFIFO_RST", 17)
                    .WithTaggedFlag("UART_TXFIFO_RST", 18)
                    .WithTaggedFlag("UART_ERR_WR_MASK", 26)
                    .WithTaggedFlag("UART_AUTOBAUD_EN", 27)
                },
                {(long)Registers.Config1, new DoubleWordRegister(this)
                    .WithTag("RXFIFO_FULL_THRHD", 0, 10) // It will produce rxfifo_full_int interrupt when receiver receives more data than this register value.
                    .WithTag("TXFIFO_EMPTY_THRHD", 10, 10) // It will produce txfifo_empty_int interrupt when the data amount in Tx-FIFO is less than this register value.
                    .WithTaggedFlag("DIS_RX_DAT_OVF", 20) // Disable UART Rx data overflow detect.
                    .WithTaggedFlag("RX_TOUT_FLOW_DIS", 21) // Set this bit to stop accumulating idle_cnt when hardware flow control works.
                    .WithTaggedFlag("RX_FLOW_EN", 22) // This is the flow enable bit for UART receiver.
                    .WithTaggedFlag("RX_TOUT_EN", 23) // This is the enble bit for uart receiver's timeout function.
                },
                {(long)Registers.EdgeChangeCount, new DoubleWordRegister(this)
                    .WithTag("RXD_EDGE_CNT", 0, 10) // This register stores the count of rxd edge change. It is used in baud rate-detect process.
                    .WithReservedBits(10, 22)
                },
                {(long)Registers.ClockDivider, new DoubleWordRegister(this)
                    .WithTag("CLKDIV", 0, 12) // Clock divider configuration
                    .WithReservedBits(12, 8)
                    .WithTag("FRAG", 20, 4) // The decimal part of the frequency divider factor.
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.CoreClock, new DoubleWordRegister(this)
                    .WithTag("SCLK_DIV_B", 0, 6) // The  denominator of the frequency divider factor.
                    .WithTag("SCLK_DIV_A", 6, 6) // The numerator of the frequency divider factor.
                    .WithTag("SCLK_DIV_NUM", 12, 8) // The integral part of the frequency divider factor.
                    .WithTag("SCLK_SEL", 20, 2) // UART clock source select. 1: 80Mhz, 2: 8Mhz, 3: XTAL.
                    .WithTaggedFlag("SCLK_EN", 22) // Set this bit to enable UART Tx/Rx clock.
                    .WithTaggedFlag("RST_CORE", 23) // Write 1 then write 0 to this bit, reset UART Tx/Rx.
                    .WithTaggedFlag("TX_SCLK_EN", 24) // Set this bit to enable UART Tx clock.
                    .WithTaggedFlag("RX_SCLK_EN", 25) // Set this bit to enable UART Rx clock.
                    .WithReservedBits(26, 6)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            UpdateInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void ClearInterrupt(int interrupt)
        {
            this.Log(LogLevel.Noisy, "Clearing {0} interrupt.", (Interrupts)interrupt);
            interruptRawStatuses[interrupt] = false;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            bool flag = MaskedInterruptStatus != 0;
            this.Log(LogLevel.Debug, "Setting IRQ to {0}", flag);
            IRQ.Set(flag);
        }

        private uint InterruptMask => Renode.Utilities.BitHelper.GetValueFromBitsArray(interruptMasks);

        private uint RawInterruptStatus => Renode.Utilities.BitHelper.GetValueFromBitsArray(interruptRawStatuses);

        private uint MaskedInterruptStatus => RawInterruptStatus & InterruptMask;

        private readonly bool[] interruptMasks;
        private readonly bool[] interruptRawStatuses;
        private readonly DoubleWordRegisterCollection registers;

        private const uint InterruptsCount = 20;

        private enum Interrupts
        {
            RxFifoFull,
            TxFifoEmpty,
            ParityError,
            DataFrameError,
            RxFifoOverflow,
            DSREdgeChange,
            CTSEdgeChange,
            BreakDetected,
            ReceiverTimeout,
            SWFlowXON,
            SWFlowXOFF,
            GlitchDetected,
            TxDone,
            TxIdleDone,
            TxSentAllData,
            RS485ParityError,
            RS485DataFrameError,
            RS485Clash,
            CMDCharDetected,
            Wakeup,
        }

        private enum Registers : long
        {
            // FIFO Configuration
            Fifo                     = 0x00,
            ThresholdAndAllocation   = 0x60,

            // UART interrupt register
            RawInterruptStatus       = 0x04,
            MaskedInterruptStatus    = 0x08,
            InterruptEnable          = 0x0c,
            InterruptClear           = 0x10,

            // Configuration register
            ClockDivider             = 0x14,
            RxFilter                 = 0x18,
            Config0                  = 0x20,
            Config1                  = 0x24,
            SoftwareFlowControl      = 0x34,
            SleepMode                = 0x38,
            SoftwareFlowControlChar0 = 0x3c,
            SoftwareFlowControlChar1 = 0x40,
            TxBreakCharacter         = 0x44,
            IdleTime                 = 0x48,
            RS485Mode                = 0x4c,
            CoreClock                = 0x78,

            // Status register
            Status = 0x1c,
            TxFifoOffsetAddress      = 0x64,
            RxFifoOffsetAddress      = 0x68,
            TxRxStatus               = 0x6c,

            // Autobaud register
            MinLowPulseDuration      = 0x28,
            MinHighPulseDuration     = 0x2c,
            EdgeChangeCount          = 0x30,
            HighPulse                = 0x70,
            LowPulse                 = 0x74,

            // Escape sequence selection configuration
            PreSequenceTiming        = 0x50,
            PostSequenceTiming       = 0x54,
            Timeout                  = 0x58,
            ATEscapeSequenceDetect   = 0x5c,

            // Version
            Version = 0x7c,
            ID = 0x80,
        }
    }
}
