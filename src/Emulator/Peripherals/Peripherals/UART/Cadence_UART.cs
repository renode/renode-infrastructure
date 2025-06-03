//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Cadence_UART : UARTBase, IUARTWithBufferState, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_UART(IMachine machine, bool clearInterruptStatusOnRead = false, ulong clockFrequency = 50000000, int fifoCapacity = 64) : base(machine)
        {
            this.fifoCapacity = fifoCapacity;
            this.clearInterruptStatusOnRead = clearInterruptStatusOnRead;
            this.clockFrequency = clockFrequency;
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            rxFifoOverflow = new CadenceInterruptFlag(() => false);
            rxFifoFull = new CadenceInterruptFlag(() => Count >= fifoCapacity);
            rxFifoTrigger = new CadenceInterruptFlag(() => Count >= (int)rxTriggerLevel.Value && rxTriggerLevel.Value > 0);
            rxFifoEmpty = new CadenceInterruptFlag(() => Count == 0);
            rxTimeoutError = new CadenceInterruptFlag(() => false);
            txFifoEmpty = new CadenceInterruptFlag(() => true);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void WriteChar(byte value)
        {
            if(!RxEnabled)
            {
                this.Log(LogLevel.Warning, "Receiver isn't enabled, incoming byte not queued.");
                return;
            }

            if(Count < fifoCapacity)
            {
                base.WriteChar(value);
                UpdateBufferState();
                // Trigger the timeout interrupt immediately after each reception
                rxTimeoutError.SetSticky(true);
            }
            else
            {
                rxFifoOverflow.SetSticky(true);
                this.Log(LogLevel.Warning, "Rx FIFO overflowed, incoming byte not queued.");
            }
            UpdateSticky();
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            foreach(var flag in GetInterruptFlags())
            {
                flag.Reset();
            }
            UpdateInterrupts();
        }

        public long Size => 0x80;
        public BufferState BufferState { get; private set; }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO RxFifoFullIRQ { get; } = new GPIO();
        public GPIO RxFifoFillLevelTriggerIRQ { get; } = new GPIO();
        public GPIO RxFifoEmptyIRQ { get; } = new GPIO();

        public GPIO TxFifoEmptyIRQ { get; } = new GPIO();
        public GPIO TxFifoFullIRQ { get; } = new GPIO();
        public GPIO TxFifoFillLevelTriggerIRQ { get; } = new GPIO();
        public GPIO TxFifoNearlyFullIRQ { get; } = new GPIO();

        public event Action<BufferState> BufferStateChanged;

        public override Bits StopBits => ConvertInternalStop(stopBitsField.Value);

        public override Parity ParityBit => ConvertInternalParity(parityField.Value);

        public override uint BaudRate => (uint)(clockFrequency / (clockSource.Value ? 8U : 1U) / baudGenerator.Value / (baudDivider.Value + 1));

        protected override void CharWritten()
        {
            // Intentionally leaved empty
        }

        protected override void QueueEmptied()
        {
            UpdateSticky();
            UpdateInterrupts();
        }

        private static Bits ConvertInternalStop(InternalStop stop)
        {
            switch(stop)
            {
                default:
                case InternalStop.One:
                    return Bits.One;
                case InternalStop.OneAndHalf:
                    return Bits.OneAndAHalf;
                case InternalStop.Two:
                    return Bits.Two;
            }
        }

        private static Parity ConvertInternalParity(InternalParity parity)
        {
            switch(parity)
            {
                case InternalParity.Even:
                    return Parity.Even;
                case InternalParity.Odd:
                    return Parity.Odd;
                case InternalParity.Forced0:
                    return Parity.Forced0;
                case InternalParity.Forced1:
                    return Parity.Forced1;
                default:
                    return Parity.None;
            }
        }

        private void UpdateSticky()
        {
            foreach(CadenceInterruptFlag flag in GetInterruptFlags())
            {
                flag.UpdateStickyStatus();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(GetInterruptFlags().Any(x => x.InterruptStatus));
            RxFifoFullIRQ.Set(rxFifoFull.InterruptStatus);
            RxFifoFillLevelTriggerIRQ.Set(rxFifoTrigger.InterruptStatus);
            RxFifoEmptyIRQ.Set(rxFifoEmpty.InterruptStatus);
            TxFifoEmptyIRQ.Set(txFifoEmpty.InterruptStatus);
        }

        private void UpdateBufferState()
        {
            if((!rxFifoFull.Status && BufferState == BufferState.Full) ||
               (!rxFifoEmpty.Status && BufferState == BufferState.Empty) ||
               ((rxFifoFull.Status || rxFifoEmpty.Status) && BufferState == BufferState.Ready))
            {
                if(rxFifoEmpty.Status)
                {
                    BufferState = BufferState.Empty;
                }
                else if(rxFifoFull.Status)
                {
                    BufferState = BufferState.Full;
                }
                else
                {
                    BufferState = BufferState.Ready;
                }

                BufferStateChanged?.Invoke(BufferState);
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var interruptStatusFieldMode = FieldMode.Read | (clearInterruptStatusOnRead ? 0 : FieldMode.Write);
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this, 0x00000128)
                    .WithReservedBits(9, 23)
                    .WithTaggedFlag("stopTxBreak", 8)
                    .WithTaggedFlag("startTxBreak", 7)
                    .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "restartRxTimeout",
                        writeCallback: 
                            // Trigger the timeout interrupt immediately after each timeout counter restart
                            (_, val) => rxTimeoutError.SetSticky(val)
                    )
                    .WithFlag(5, out txDisabledReg, name: "txDisabled")
                    .WithFlag(4, out txEnabledReg, name: "txEnabled")
                    .WithFlag(3, out rxDisabledReg, name: "rxDisabled")
                    .WithFlag(2, out rxEnabledReg, name: "rxEnabled")
                    .WithFlag(1, valueProviderCallback: _ => false, name: "txReset")
                    .WithFlag(0, valueProviderCallback: _ => false, name: "rxReset",
                        writeCallback:
                            (_, val) => { if(val) this.ClearBuffer(); }
                    )
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.Mode, new DoubleWordRegister(this)
                    .WithReservedBits(14, 18)
                    .WithTag("accessSize", 12, 2)
                    .WithReservedBits(10, 2)
                    .WithTag("channelMode", 8, 2)
                    .WithEnumField(6, 2, out stopBitsField, name: "stopBits")
                    .WithEnumField(3, 3, out parityField, name: "parityType")
                    .WithTag("characterLength", 1, 2)
                    .WithFlag(0, out clockSource, name: "clockSourceSelect")
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithReservedBits(14, 18)
                    .WithTaggedFlag("rxBreakDetectInterruptEnable", 13)
                    .WithTaggedFlag("txFifoOverflowInterruptEnable", 12)
                    .WithTaggedFlag("txFifoNearlyFullInterruptEnable", 11)
                    .WithTaggedFlag("txFifoTriggerInterruptEnable", 10)
                    .WithTaggedFlag("deltaModemStatusInterruptEnable", 9)
                    .WithFlag(8, FieldMode.Write,
                        writeCallback: (_, val) => rxTimeoutError.InterruptEnable(val),
                        name: "rxTimeoutErrorInterruptEnable"
                    )
                    .WithTaggedFlag("rxParityErrorInterruptEnable", 7)
                    .WithTaggedFlag("rxFramingErrorInterruptEnable", 6)
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptEnable(val),
                        name: "rxFifoOverflowInterruptEnable"
                    )
                    .WithTaggedFlag("txFifoFullInterruptEnable", 4)
                    .WithFlag(3, FieldMode.Write,
                        writeCallback: (_, val) => txFifoEmpty.InterruptEnable(val),
                        name: "txFifoEmptyInterruptEnable"
                    )
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoFull.InterruptEnable(val),
                        name: "rxFifoFullInterruptEnable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoEmpty.InterruptEnable(val),
                        name: "rxFifoEmptyInterruptEnable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoTrigger.InterruptEnable(val),
                        name: "rxFifoTriggerInterruptEnable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptDisable, new DoubleWordRegister(this)
                    .WithReservedBits(14, 18)
                    .WithTaggedFlag("rxBreakDetectInterruptDisable", 13)
                    .WithTaggedFlag("txFifoOverflowInterruptDisable", 12)
                    .WithTaggedFlag("txFifoNearlyFullInterruptDisable", 11)
                    .WithTaggedFlag("txFifoTriggerInterruptDisable", 10)
                    .WithTaggedFlag("deltaModemStatusInterruptDisable", 9)
                    .WithFlag(8, FieldMode.Write,
                        writeCallback: (_, val) => rxTimeoutError.InterruptDisable(val),
                        name: "rxTimeoutErrorInterruptDisable"
                    )
                    .WithTaggedFlag("rxParityErrorInterruptDisable", 7)
                    .WithTaggedFlag("rxFramingErrorInterruptDisable", 6)
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptDisable(val),
                        name: "rxFifoOverflowInterruptDisable"
                    )
                    .WithTaggedFlag("txFifoFullInterruptDisable", 4)
                    .WithFlag(3, FieldMode.Write,
                        writeCallback: (_, val) => txFifoEmpty.InterruptDisable(val),
                        name: "txFifoEmptyInterruptDisable"
                    )
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoFull.InterruptDisable(val),
                        name: "rxFifoFullInterruptDisable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoEmpty.InterruptDisable(val),
                        name: "rxFifoEmptyInterruptDisable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoTrigger.InterruptDisable(val),
                        name: "rxFifoTriggerInterruptDisable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(14, 18)
                    .WithTaggedFlag("rxBreakDetectInterruptMask", 13)
                    .WithTaggedFlag("txFifoOverflowInterruptMask", 12)
                    .WithTaggedFlag("txFifoNearlyFullInterruptMask", 11)
                    .WithTaggedFlag("txFifoTriggerInterruptMask", 10)
                    .WithTaggedFlag("deltaModemStatusInterruptMask", 9)
                    .WithFlag(8, FieldMode.Read,
                        valueProviderCallback: (_) => rxTimeoutError.InterruptMask,
                        name: "rxTimeoutErrorInterruptMask"
                    )
                    .WithTaggedFlag("rxParityErrorInterruptMask", 7)
                    .WithTaggedFlag("rxFramingErrorInterruptMask", 6)
                    .WithFlag(5, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoOverflow.InterruptMask,
                        name: "rxFifoOverflowInterruptMask"
                    )
                    .WithTaggedFlag("txFifoFullInterruptMask", 4)
                    .WithFlag(3, FieldMode.Read,
                        valueProviderCallback: (_) => txFifoEmpty.InterruptMask,
                        name: "txFifoEmptyInterruptMask"
                    )
                    .WithFlag(2, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoFull.InterruptMask,
                        name: "rxFifoFullInterruptMask"
                    )
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoEmpty.InterruptMask,
                        name: "rxFifoEmptyInterruptMask"
                    )
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoTrigger.InterruptMask,
                        name: "rxFifoTriggerInterruptMask"
                    )
                },
                {(long)Registers.ChannelInterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(14, 18)
                    .WithTaggedFlag("rxBreakDetectInterruptStatus", 13)
                    .WithTaggedFlag("txFifoOverflowInterruptStatus", 12)
                    .WithTaggedFlag("txFifoNearlyFullInterruptStatus", 11)
                    .WithTaggedFlag("txFifoTriggerInterruptStatus", 10)
                    .WithTaggedFlag("deltaModemStatusInterruptStatus", 9)
                    .WithFlag(8, interruptStatusFieldMode,
                        valueProviderCallback: (_) => rxTimeoutError.StickyStatus,
                        readCallback: (_, __) => rxTimeoutError.ClearSticky(clearInterruptStatusOnRead),
                        writeCallback: (_, val) => rxTimeoutError.ClearSticky(val && !clearInterruptStatusOnRead),
                        name: "rxTimeoutErrorInterruptStatus"
                    )
                    .WithTaggedFlag("rxParityErrorInterruptStatus", 7)
                    .WithTaggedFlag("rxFramingErrorInterruptStatus", 6)
                    .WithFlag(5, interruptStatusFieldMode,
                        valueProviderCallback: (_) => rxFifoOverflow.StickyStatus,
                        readCallback: (_, __) => rxFifoOverflow.ClearSticky(clearInterruptStatusOnRead),
                        writeCallback: (_, val) => rxFifoOverflow.ClearSticky(val && !clearInterruptStatusOnRead),
                        name: "rxFifoOverflowInterruptStatus"
                    )
                    .WithTaggedFlag("txFifoFullInterruptStatus", 4)
                    .WithFlag(3, interruptStatusFieldMode,
                        valueProviderCallback: (_) => txFifoEmpty.StickyStatus,
                        // There is no sense to clear the txFifoEmptyInterruptStatus flag, because a Tx FIFO is always empty
                        name: "txFifoEmptyInterruptStatus"
                    )
                    .WithFlag(2, interruptStatusFieldMode,
                        valueProviderCallback: (_) => rxFifoFull.StickyStatus,
                        readCallback: (_, __) => rxFifoFull.ClearSticky(clearInterruptStatusOnRead),
                        writeCallback: (_, val) => rxFifoFull.ClearSticky(val && !clearInterruptStatusOnRead),
                        name: "rxFifoFullInterruptStatus"
                    )
                    .WithFlag(1, interruptStatusFieldMode,
                        valueProviderCallback: (_) => rxFifoEmpty.StickyStatus,
                        readCallback: (_, __) => rxFifoEmpty.ClearSticky(clearInterruptStatusOnRead),
                        writeCallback: (_, val) => rxFifoEmpty.ClearSticky(val && !clearInterruptStatusOnRead),
                        name: "rxFifoEmptyInterruptMStatus"
                    )
                    .WithFlag(0, interruptStatusFieldMode,
                        valueProviderCallback: (_) => rxFifoTrigger.StickyStatus,
                        readCallback: (_, __) => rxFifoTrigger.ClearSticky(clearInterruptStatusOnRead),
                        writeCallback: (_, val) => rxFifoTrigger.ClearSticky(val && !clearInterruptStatusOnRead),
                        name: "rxFifoTriggerInterruptStatus"
                    )
                    .WithReadCallback((_, __) =>
                    {
                        if(clearInterruptStatusOnRead)
                        {
                            UpdateSticky();
                            UpdateInterrupts();
                        }
                    })
                    .WithWriteCallback((_, __) =>
                    {
                        if(!clearInterruptStatusOnRead)
                        {
                            UpdateSticky();
                            UpdateInterrupts();
                        }
                    })
                },
                {(long)Registers.BaudRateGenerator, new DoubleWordRegister(this, resetValue: 0x0000028B)
                    .WithReservedBits(16, 16)
                    .WithValueField(0, 16, out baudGenerator, writeCallback: (oldVal, newVal) =>
                    {
                        if(newVal == 0)
                        {
                            // https://docs.xilinx.com/r/en-US/ug585-zynq-7000-SoC-TRM/Baud-Rate-Generator
                            this.Log(LogLevel.Warning, "Attempt to write 0 to Baud Rate Generator register was ignored. It can be programmed with a value between 1 and 65535.");
                            baudGenerator.Value = oldVal;
                        }
                    }, name: "baudRateGenerator")
                },
                {(long)Registers.RxFifoTriggerLevel, new DoubleWordRegister(this, 0x00000020)
                    .WithReservedBits(6, 26)
                    .WithValueField(0, 6, out rxTriggerLevel)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.ChannelStatus, new DoubleWordRegister(this)
                    .WithReservedBits(15, 17)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "txFifoTriggerStatus")
                    .WithTaggedFlag("rxFlowDelayTriggerStatus", 12)
                    .WithTaggedFlag("txStateMachineActiveStatus", 11)
                    .WithTaggedFlag("rxStateMachineActiveStatus", 10)
                    .WithReservedBits(5, 4)
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "txFifoFullStatus")
                    .WithFlag(3, FieldMode.Read,
                        valueProviderCallback: _ => txFifoEmpty.Status,
                        name: "txFifoEmptyStatus")
                    .WithFlag(2, FieldMode.Read,
                        valueProviderCallback: _ => rxFifoFull.Status,
                        name: "rxFifoFullStatus")
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: _ => rxFifoEmpty.Status,
                        name: "rxFifoEmptyStatus")
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: _ => rxFifoTrigger.Status,
                        name: "rxFifoTriggerStatus")
                },
                {(long)Registers.RxTxFifo, new DoubleWordRegister(this)
                    .WithValueField(0, 8,
                        writeCallback: (_, value) =>
                        {
                            if(!TxEnabled)
                            {
                                this.Log(LogLevel.Warning, "Trying to write to a disabled Tx.");
                                return;
                            }
                            this.TransmitCharacter((byte)value);
                        },
                        valueProviderCallback: _ =>
                        {
                            if(!RxEnabled)
                            {
                                this.Log(LogLevel.Warning, "Reading from disabled Rx FIFO.");
                            }
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Reading from an empty Rx FIFO, dummy data returned.");
                            }
                            return character;
                        })
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                    .WithReadCallback((_, __) =>
                    {
                        UpdateBufferState();
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.BaudRateDivider, new DoubleWordRegister(this, resetValue: 0x0000000F)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, out baudDivider, name: "baudRateDivider")
                },
                {(long)Registers.RxFifoByteStatus, new DoubleWordRegister(this)
                    .WithReservedBits(12, 20)
                    .WithTaggedFlag("byte3_break", 11)
                    .WithTaggedFlag("byte3_frm_err", 10)
                    .WithTaggedFlag("byte3_par_err", 9)
                    .WithTaggedFlag("byte2_break", 8)
                    .WithTaggedFlag("byte2_frm_err", 7)
                    .WithTaggedFlag("byte2_par_err", 6)
                    .WithTaggedFlag("byte1_break", 5)
                    .WithTaggedFlag("byte1_frm_err", 4)
                    .WithTaggedFlag("byte1_par_err", 3)
                    .WithTaggedFlag("byte0_break", 2)
                    .WithTaggedFlag("byte0_frm_err", 1)
                    .WithTaggedFlag("byte0_par_err", 0)
                }
            };
        }

        private IEnumerable<CadenceInterruptFlag> GetInterruptFlags()
        {
            yield return rxFifoOverflow;
            yield return rxFifoFull;
            yield return rxFifoTrigger;
            yield return rxFifoEmpty;
            yield return rxTimeoutError;
            yield return txFifoEmpty;
        }

        private bool TxEnabled => txEnabledReg.Value && !txDisabledReg.Value;
        private bool RxEnabled => rxEnabledReg.Value && !rxDisabledReg.Value;

        private IFlagRegisterField txDisabledReg;
        private IFlagRegisterField txEnabledReg;
        private IFlagRegisterField rxDisabledReg;
        private IFlagRegisterField rxEnabledReg;
        private IEnumRegisterField<InternalStop> stopBitsField;
        private IEnumRegisterField<InternalParity> parityField;
        private IFlagRegisterField clockSource;
        private IValueRegisterField baudGenerator;
        private IValueRegisterField rxTriggerLevel;
        private IValueRegisterField baudDivider;

        private readonly CadenceInterruptFlag rxFifoOverflow;
        private readonly CadenceInterruptFlag rxFifoFull;
        private readonly CadenceInterruptFlag rxFifoTrigger;
        private readonly CadenceInterruptFlag rxFifoEmpty;
        private readonly CadenceInterruptFlag rxTimeoutError;
        private readonly CadenceInterruptFlag txFifoEmpty;

        private readonly DoubleWordRegisterCollection registers;
        private readonly bool clearInterruptStatusOnRead;
        private readonly ulong clockFrequency;

        private int fifoCapacity;

        private enum InternalStop
        {
            One = 0b00,
            OneAndHalf = 0b01,
            Two = 0b10
        }

        private enum InternalParity
        {
            Even = 0b000,
            Odd = 0b001,
            Forced0 = 0b010,
            Forced1 = 0b011,
            None = 0b100
        }

        private enum Registers : long
        {
            Control = 0x00,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0c,
            InterruptMask = 0x10,
            ChannelInterruptStatus = 0x14,
            BaudRateGenerator = 0x18,
            RxTimeout = 0x1c,
            RxFifoTriggerLevel = 0x20,
            ModemControl = 0x24,
            ModemStatus = 0x28,
            ChannelStatus = 0x2c,
            RxTxFifo = 0x30,
            BaudRateDivider = 0x34,
            FlowDelay = 0x38,
            TxFifoTriggerLevel = 0x44,
            RxFifoByteStatus = 0x48
        }
    }
}
