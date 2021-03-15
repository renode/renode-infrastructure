//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    // This model currently does not support timeout feature, rx break detection and software tx pin override
    public class OpenTitan_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_UART(Machine machine) : base(machine)
        {
            txWatermarkIRQ = new GPIO();
            rxWatermarkIRQ = new GPIO();
            txEmptyIRQ = new GPIO();
            rxOverflowIRQ = new GPIO();
            rxFrameErrorIRQ = new GPIO();
            rxBreakErrorIRQ = new GPIO();
            rxTimeoutIRQ = new GPIO();
            rxParityErrorIRQ = new GPIO();

            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            txQueue = new Queue<byte>();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void WriteChar(byte value)
        {
            if(lineLoopbackEnabled.Value)
            {
                TransmitCharacter(value);
                this.Log(LogLevel.Noisy, "Line Loopback Enabled, byte echoed by hardware.");
            }

            if(systemLoopbackEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Sytem Loopback Enabled, incoming byte not queued.");
                return;
            }
            
            if(!rxEnabled.Value)
            {
                this.Log(LogLevel.Warning, "CTRL.RX is unset, incoming byte not queued.");
                return;
            }

            if(Count < rxFIFOCapacity)
            {
                base.WriteChar(value);
            }
            else
            {
                rxOverflowIntr.Value = true;
                this.Log(LogLevel.Warning, "RX FIFO overflowed, incoming byte not queued.");
            }
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            txQueue.Clear();
            UpdateInterrupts();
        }

        public long Size => 0x30;

        public GPIO txWatermarkIRQ { get; }
        public GPIO rxWatermarkIRQ { get; }
        public GPIO txEmptyIRQ { get; }
        public GPIO rxOverflowIRQ { get; }
        public GPIO rxFrameErrorIRQ { get; }
        public GPIO rxBreakErrorIRQ { get; }
        public GPIO rxTimeoutIRQ { get; }
        public GPIO rxParityErrorIRQ { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => parityTypeField.Value == ParityType.Odd ? Parity.Odd : Parity.Even;

        public override uint BaudRate => (uint)((baudClockRate.Value * fixedClockFrequency) >> 20);

        protected override void CharWritten()
        {
            // intentionally left empty
        }

        protected override void QueueEmptied()
        {
            // intentionally left empty
        }
        
        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptState, new DoubleWordRegister(this)
                    .WithFlag(0, out txWatermarkIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.tx_watermark")
                    .WithFlag(1, out rxWatermarkIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_watermark")
                    .WithFlag(2, out txEmptyIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.tx_empty")
                    .WithFlag(3, out rxOverflowIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_overflow")
                    .WithFlag(4, out rxFrameErrorIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_frame_err")
                    .WithFlag(5, out rxBreakErrorIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_break_err")
                    .WithFlag(6, out rxTimeoutIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_timeout")
                    .WithFlag(7, out rxParityErrorIntr, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.rx_parity_err")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txWatermarkEnabled, name: "INTR_ENABLE.tx_watermark")
                    .WithFlag(1, out rxWatermarkEnabled, name: "INTR_ENABLE.rx_watermark")
                    .WithFlag(2, out txEmptyEnabled, name: "INTR_ENABLE.tx_empty")
                    .WithFlag(3, out rxOverflowEnabled, name: "INTR_ENABLE.rx_overflow")
                    .WithFlag(4, out rxFrameErrorEnabled, name: "INTR_ENABLE.rx_frame_err")
                    .WithFlag(5, out rxBreakErrorEnabled, name: "INTR_ENABLE.rx_break_err")
                    .WithFlag(6, out rxTimeoutEnabled, name: "INTR_ENABLE.rx_timeout")
                    .WithFlag(7, out rxParityErrorEnabled, name: "INTR_ENABLE.rx_parity_err")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { txWatermarkIntr.Value |= val; },  name: "INTR_TEST.tx_watermark")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { rxWatermarkIntr.Value |= val; },  name: "INTR_TEST.rx_watermark")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { txEmptyIntr.Value |= val; },  name: "INTR_TEST.tx_empty")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { rxOverflowIntr.Value |= val; },  name: "INTR_TEST.rx_overflow")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { rxFrameErrorIntr.Value |= val; },  name: "INTR_TEST.rx_frame_err")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { rxBreakErrorIntr.Value |= val; },  name: "INTR_TEST.rx_break_err")
                    .WithFlag(6, FieldMode.Write, writeCallback: (_, val) => { rxTimeoutIntr.Value |= val; },  name: "INTR_TEST.rx_timeout")
                    .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { rxParityErrorIntr.Value |= val; },  name: "INTR_TEST.rx_parity_err")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out txEnabled, name: "CTRL.TX", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            if(!lineLoopbackEnabled.Value)
                            {
                                foreach(byte value in txQueue)
                                {
                                    TransmitCharacter(value);
                                }
                            }
                            txQueue.Clear();
                            UpdateInterrupts();
                        }
                    })
                    .WithFlag(1, out rxEnabled, name: "CTRL.RX")
                    .WithFlag(2, out noiseFilterEnabled, name: "CTRL.NF")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out systemLoopbackEnabled, name: "CTRL.SLPBK")
                    .WithFlag(5, out lineLoopbackEnabled, name: "CTRL.LLPBK")
                    .WithFlag(6, out parityEnabled, name: "CTRL.PARITY_EN")
                    .WithEnumField(7, 1, out parityTypeField, name: "CTRL.PARITY_ODD")
                    .WithTag("CTRL.RXBLVL", 8, 2)
                    .WithReservedBits(10, 6)
                    .WithValueField(16, 16, out baudClockRate, name: "CTRL.NCO")
                },
                {(long)Registers.LiveStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == txFIFOCapacity, name: "STATUS.TXFULL")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => Count == rxFIFOCapacity, name: "STATUS.RXFULL")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "STATUS.TXEMPTY")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "STATUS.TXIDLE")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "STATUS.RXIDLE")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "STATUS.RXEMPTY")
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.ReadData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "RDATA", valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                        }
                        return character;   
                    })
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.WriteData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, name: "WDATA", writeCallback: (_, val) =>
                    {
                        if(systemLoopbackEnabled.Value)
                        {
                            base.WriteChar((byte)val);
                            return;
                        }

                        if(txEnabled.Value)
                        {
                            TransmitCharacter((byte)val);
                        }
                        else if(txQueue.Count < txFIFOCapacity)
                        {
                            txQueue.Enqueue((byte)val);
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO.");
                        }
                    })
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FIFOControl, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "FIFO_CTRL.RXRST", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            ClearBuffer();
                        }
                    })
                    .WithFlag(1, FieldMode.Write, name: "FIFO_CTRL.TXRST", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            txQueue.Clear();
                            
                        }
                    })
                    .WithEnumField(2, 3, out rxWatermarkField, name: "FIFO_CTRL.RXILVL")
                    .WithEnumField(5, 2, out txWatermarkField, name: "FIFO_CTRL.TXILVL")
                    .WithReservedBits(7, 25)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FIFOStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => (uint)txQueue.Count, name: "FIFO_STATUS.TXLVL")
                    .WithReservedBits(6, 10)
                    .WithValueField(16, 6, FieldMode.Read, valueProviderCallback: _ => (uint)Count, name: "FIFO_STATUS.RXLVL")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.TxPinOverrideControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("OVRD.TXEN", 0)
                    .WithTaggedFlag("OVRD.TXVAL", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.OversampledValues, new DoubleWordRegister(this)
                    .WithTag("VAL.RX", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.RxTimeoutControl, new DoubleWordRegister(this)
                    .WithTag("TIMEOUT_CTRL.VAL", 0, 24)
                    .WithReservedBits(24, 7)
                    .WithTaggedFlag("TIMEOUT_CTRL.EN", 31)
                }
            };
        }

        private void UpdateInterrupts()
        {
            txWatermarkIntr.Value |= TxWatermarkValue > txQueue.Count;
            rxWatermarkIntr.Value |= RxWatermarkValue <= Count;
            txEmptyIntr.Value |= txQueue.Count == 0;

            txWatermarkIRQ.Set(txWatermarkIntr.Value && txWatermarkEnabled.Value);
            rxWatermarkIRQ.Set(rxWatermarkIntr.Value && rxWatermarkEnabled.Value);
            txEmptyIRQ.Set(txEmptyIntr.Value && txEmptyEnabled.Value);
            rxOverflowIRQ.Set(rxOverflowIntr.Value && rxOverflowEnabled.Value);
            rxFrameErrorIRQ.Set(rxFrameErrorIntr.Value && rxFrameErrorEnabled.Value);
            rxBreakErrorIRQ.Set(rxBreakErrorIntr.Value && rxBreakErrorEnabled.Value);
            rxTimeoutIRQ.Set(rxTimeoutIntr.Value && rxTimeoutEnabled.Value);
            rxParityErrorIRQ.Set(rxParityErrorIntr.Value && rxParityErrorEnabled.Value);
        }

        private int RxWatermarkValue
        {
            get
            {
                switch(rxWatermarkField.Value)
                {
                    default:
                        this.Log(LogLevel.Error, "Unexpected state of rxWatermarkField ({0})", rxWatermarkField.Value);
                        return 1;
                    case RxWatermarkLevel.Level1:
                        return 1;
                    case RxWatermarkLevel.Level4:
                        return 4;
                    case RxWatermarkLevel.Level8:
                        return 8;
                    case RxWatermarkLevel.Level16:
                        return 16;
                    case RxWatermarkLevel.Level30:
                        return 30;
                }
            }
        }

        private int TxWatermarkValue
        {
            get
            {
                switch(txWatermarkField.Value)
                {
                    default:
                        this.Log(LogLevel.Error, "Unexpected state of txWatermarkField ({0})", txWatermarkField.Value);
                        return 2;
                    case TxWatermarkLevel.Level1:
                        return 2;
                    case TxWatermarkLevel.Level4:
                        return 4;
                    case TxWatermarkLevel.Level8:
                        return 8;
                    case TxWatermarkLevel.Level16:
                        return 16;
                }
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly Queue<byte> txQueue;
        // InterruptState
        private IFlagRegisterField txWatermarkIntr;
        private IFlagRegisterField rxWatermarkIntr;
        private IFlagRegisterField txEmptyIntr;
        private IFlagRegisterField rxOverflowIntr;
        private IFlagRegisterField rxFrameErrorIntr;
        private IFlagRegisterField rxBreakErrorIntr;
        private IFlagRegisterField rxTimeoutIntr;
        private IFlagRegisterField rxParityErrorIntr;
        // InterruptEnable
        private IFlagRegisterField txWatermarkEnabled;
        private IFlagRegisterField rxWatermarkEnabled;
        private IFlagRegisterField txEmptyEnabled;
        private IFlagRegisterField rxOverflowEnabled;
        private IFlagRegisterField rxFrameErrorEnabled;
        private IFlagRegisterField rxBreakErrorEnabled;
        private IFlagRegisterField rxTimeoutEnabled;
        private IFlagRegisterField rxParityErrorEnabled;
        // Control
        private IFlagRegisterField txEnabled;
        private IFlagRegisterField rxEnabled;
        private IFlagRegisterField noiseFilterEnabled;
        private IFlagRegisterField systemLoopbackEnabled;
        private IFlagRegisterField lineLoopbackEnabled;
        private IFlagRegisterField parityEnabled;
        private IEnumRegisterField<ParityType> parityTypeField;
        private IValueRegisterField baudClockRate;
        // FIFOControl
        private IEnumRegisterField<RxWatermarkLevel> rxWatermarkField;
        private IEnumRegisterField<TxWatermarkLevel> txWatermarkField;

        private const int rxFIFOCapacity = 32;
        private const int txFIFOCapacity = 32;
        private const ulong fixedClockFrequency = 50000000;

        private enum ParityType
        {
            Even = 0,
            Odd  = 1
        }

        private enum RxWatermarkLevel
        {
            Level1  = 0,
            Level4  = 1,
            Level8  = 2,
            Level16 = 3,
            Level30 = 4
        }

        private enum TxWatermarkLevel
        {
            Level1  = 0,
            Level4  = 1,
            Level8  = 2,
            Level16 = 3
        }

        private enum Registers : long
        {
            InterruptState       = 0x0,
            InterruptEnable      = 0x4,
            InterruptTest        = 0x8,
            Control              = 0xC,
            LiveStatus           = 0x10,
            ReadData             = 0x14,
            WriteData            = 0x18,
            FIFOControl          = 0x1C,
            FIFOStatus           = 0x20,
            TxPinOverrideControl = 0x24,
            OversampledValues    = 0x28,
            RxTimeoutControl     = 0x2C
        }
    }
}
