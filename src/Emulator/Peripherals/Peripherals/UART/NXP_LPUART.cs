//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NXP_LPUART : UARTBase, IUARTWithBufferState, ILINController, IBytePeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public NXP_LPUART(IMachine machine, long frequency = 8000000, bool hasGlobalRegisters = true, bool hasFifoRegisters = true, uint fifoSize = DefaultFIFOSize, bool separateIRQs = false) : base(machine)
        {
            this.frequency = frequency;
            this.hasGlobalRegisters = hasGlobalRegisters;
            this.separateIRQs = separateIRQs;

            locker = new object();
            IRQ = new GPIO();
            SeparateRxIRQ = new GPIO();
            DMA = new GPIO();
            txQueue = new Queue<byte>();
            if (!Misc.IsPowerOfTwo(fifoSize))
            {
                throw new ConstructionException($"The `{nameof(fifoSize)}` argument must be a power of 2, given: {fifoSize}.");
            }
            rxFIFOCapacity = fifoSize;
            txFIFOCapacity = fifoSize;

            var registersMap = new Dictionary<long, DoubleWordRegister>();

            if (hasGlobalRegisters)
            {
                registersMap.Add((long)GlobalRegs.Global, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, out reset, name: "RST / Software Reset", writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                Reset();
                            }
                        })
                    .WithReservedBits(2, 30)
                );
            }

            registersMap.Add(CommonRegistersOffset + (long)CommonRegs.BaudRate, new DoubleWordRegister(this)
                .WithValueField(0, 13, out baudRateModuloDivisor, name: "SBR / Baud Rate Modulo Divisor")
                .WithFlag(13, out stopBitNumberSelect, name: "SBNS / Stop Bit Number Select")
                .WithTaggedFlag("RXEDGIE / RX Input Active Edge Interrupt Enable", 14)
                .WithFlag(15, out linBreakDetectInterruptEnable, name: "LBKDIE / LIN Break Detect Interrupt Enable")
                .WithTaggedFlag("RESYNCDIS / Resynchronization Disable", 16)
                .WithFlag(17, out bothEdgeSampling, name: "BOTHEDGE / Both Edge Sampling")
                .WithTag("MATCFG / Match Configuration", 18, 2)
                .WithTaggedFlag("RIDMAE / Receiver Idle DMA Enable", 20)
                .WithFlag(21, out receiverDMAEnabled, name: "RDMAE / Receiver Full DMA Enable")
                .WithReservedBits(22, 1)
                .WithFlag(23, out transmitterDMAEnabled, name: "TDMAE / Transmitter DMA Enable")
                .WithValueField(24, 5, out oversamplingRatio, name: "OSR / Oversampling Ratio", writeCallback: (current, val) =>
                    {
                        if (1 == val || 2 == val)
                        {
                            this.Log(LogLevel.Warning, "Tried to set the Oversampling Ratio to reserved value: 0x{0:X}. Old value kept.", val);
                            oversamplingRatio.Value = current;
                        }
                    })
                .WithTaggedFlag("M10 / 10-bit Mode select", 29)
                .WithTaggedFlag("MAEN2 / Match Address Mode Enable 2", 30)
                .WithTaggedFlag("MAEN1 / Match Address Mode Enable 1", 31)
                .WithWriteCallback((_, __) =>
                    {
                        if ((3 >= oversamplingRatio.Value && 6 <= oversamplingRatio.Value) && !bothEdgeSampling.Value)
                        {
                            this.Log(LogLevel.Warning, "Oversampling Ratio set to value: 0x{0:X}, but this requires Both Edge Sampling to be set.", oversamplingRatio.Value);
                        }
                        UpdateGPIOOutputs();
                    })
            );

            registersMap.Add(CommonRegistersOffset + (long)CommonRegs.Status, new DoubleWordRegister(this)
                .WithReservedBits(0, 14)
                .WithTaggedFlag("MA2F / Match 2 Flag", 14)
                .WithTaggedFlag("MA1F / Match 1 Flag", 15)
                .WithTaggedFlag("PF / Parity Error Flag", 16)
                .WithTaggedFlag("FE / Framing Error Flag", 17)
                .WithTaggedFlag("NF / Noise Flag", 18)
                .WithFlag(19, out receiverOverrun, FieldMode.Read | FieldMode.WriteOneToClear, name: "OR / Receiver Overrun Flag")
                .WithTaggedFlag("IDLE / Idle Line Flag", 20)
                // Despite the name below flag should be set when Watermark level is exceeded
                .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => BufferState == BufferState.Ready, name: "RDRF / Receive Data Register Full")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "TC / Transmission Complete Flag")
                .WithFlag(23, out transmitDataRegisterEmpty, FieldMode.Read, name: "TDRE / Transmission Data Register Empty Flag")
                .WithTaggedFlag("RAF / Receiver Active Flag", 24)
                .WithFlag(25, out linBreakDetection, name: "LBKDE / LIN Break Detection Enable")
                .WithTaggedFlag("BRK13 / Break Character Generation Length", 26)
                .WithTaggedFlag("RWUID / Receive Wake Up Idle Detect", 27)
                .WithTaggedFlag("RXINV / Receive Data Inversion", 28)
                .WithTaggedFlag("MSBF / MSB First", 29)
                .WithTaggedFlag("RXEDGIF / RXD Pin Active Edge Interrupt Flag", 30)
                .WithFlag(31, out linBreakDetect, FieldMode.Read | FieldMode.WriteOneToClear, name: "LBKDIF / LIN Break Detect Interrupt Flag")
            );

            registersMap.Add(CommonRegistersOffset + (long)CommonRegs.Control, new DoubleWordRegister(this)
                .WithFlag(0, out parityType, name: "PT / Parity Type")
                .WithFlag(1, out parityEnabled, name: "PE / Parity Enable")
                .WithTaggedFlag("ILT / Idle Line Type Select", 2)
                .WithTaggedFlag("WAKE / Receiver Wakeup Method Select", 3)
                .WithTaggedFlag("M / 9-Bit or 8-Bit Mode Select", 4)
                .WithFlag(5, out receiverSource, name: "RSRC / Receiver Source Select")
                .WithTaggedFlag("DOZEEN / Doze Enable", 6)
                .WithFlag(7, out loopMode, name: "LOOPS / Loop Mode Select")
                .WithTag("IDLECFG / Idle Configuration", 8, 3)
                .WithTaggedFlag("M7 / 7-Bit Mode Select", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("MA2IE / Match 2 Interrupt Enable", 14)
                .WithTaggedFlag("MA1IE / Match 1 Interrupt Enable", 15)
                .WithTaggedFlag("SBK / Send Break", 16)
                .WithTaggedFlag("RWU / Receiver Wakeup Control", 17)
                .WithFlag(18, out receiverEnabled, name: "RE / Receiver Enable")
                .WithFlag(19, out transmitterEnabled, name: "TE / Transmitter Enable", writeCallback: (_, val) =>
                    {
                        if (val)
                        {
                            foreach (var value in txQueue)
                            {
                                TransmitData(value);
                            }
                            txQueue.Clear();
                        }
                    })
                .WithTaggedFlag("ILIE / Idle Line Interrupt Enable", 20)
                .WithFlag(21, out receiverInterruptEnabled, name: "RIE / Receiver Interrupt Enable")
                .WithFlag(22, out transmissionCompleteInterruptEnabled, name: "TCIE / Transmission Complete Interrupt Enable")
                .WithFlag(23, out transmitterInterruptEnabled, name: "TIE / Transmission Interrupt Enable")
                .WithTaggedFlag("PEIE / Parity Error Interrupt Enable", 24)
                .WithTaggedFlag("FEIE / Framing Error Interrupt Enable", 25)
                .WithTaggedFlag("NEIE / Noise Error Interrupt Enable", 26)
                .WithFlag(27, out overrunInterruptEnable, name: "ORIE / Overrun Interrupt Enable")
                .WithTaggedFlag("TXINV / Transmit Data Inversion", 28)
                .WithFlag(29, out transmissionPinDirectionOutNotIn, name: "TXDIR / TXD Pin Direction in Single-Wire Mode")
                .WithTaggedFlag("R9T8 / Receive Bit 9 / Transmit Bit 8", 30)
                .WithTaggedFlag("R8T9 / Receive Bit 8 / Transmit Bit 9", 31)
                .WithWriteCallback((_, __) => UpdateGPIOOutputs())
            );

            registersMap.Add(CommonRegistersOffset + (long)CommonRegs.Data, new DoubleWordRegister(this)
                .WithValueField(0, 9, valueProviderCallback: _ =>
                    {
                        if (!this.TryGetCharacter(out var b))
                        {
                            receiveFifoUnderflowInterrupt.Value = true;
                            this.Log(LogLevel.Warning, "Trying to read form an empty fifo");
                        }
                        else
                        {
                            OnBufferStateChanged();
                        }
                        return b;
                    },
                    writeCallback: (_, val) =>
                    {
                        var breakCharacter = transmitSpecialCharacter.Value && val == 0;
                        if (breakCharacter && linBreakDetection.Value)
                        {
                            // We have to broadcast LIN break
                            BroadcastLINBreak?.Invoke();
                        }
                        else if (transmitterEnabled.Value)
                        {
                            TransmitData((byte)val);
                        }
                        else if (txQueue.Count < txMaxBytes)
                        {
                            txQueue.Enqueue((byte)val);

                            UpdateFillLevels();
                        }
                        else
                        {
                            transmitFifoOverflowInterrupt.Value = true;
                            this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO.");
                        }
                        UpdateGPIOOutputs();

                    })
                .WithReservedBits(10, 1)
                .WithTaggedFlag("IDLINE / Idle Line", 11)
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => BufferState == BufferState.Empty, name: "RXEMPT / Receive Buffer Empty")
                .WithFlag(13, out transmitSpecialCharacter, name: "FRETSC / Frame Error / Transmit Special Character")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "PARITYE / PARITYE")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => false, name: "NOISY / NOISY")
                .WithReservedBits(16, 16)
                .WithReadCallback((_, __) =>
                {
                    UpdateBufferState();
                    UpdateInterrupt();
                })
            );

            registersMap.Add(CommonRegistersOffset + (long)CommonRegs.MatchAddress, new DoubleWordRegister(this)
                .WithTag("MA1 / Match Address 1", 0, 10)
                .WithReservedBits(10, 6)
                .WithTag("MA2 / Match Address 2", 16, 10)
                .WithReservedBits(26, 6)
            );

            if (hasFifoRegisters)
            {
                registersMap.Add(FifoRegistersOffset + (long)FifoRegs.Fifo, new DoubleWordRegister(this)
                    .WithValueField(0, 3, FieldMode.Read, valueProviderCallback: _ => CalculateFIFODatawordsCount(rxFIFOCapacity), name: "RXFIFOSIZE / Receive FIFO Buffer Depth")
                    .WithFlag(3, out receiveFifoEnabled, name: "RXFE / Receive FIFO Enable", writeCallback: (current, val) =>
                        {
                            if (current != val && (transmitterEnabled.Value || receiverEnabled.Value))
                            {
                                this.Log(LogLevel.Warning, "Both CTRL[TE] and CTRL[RE] must be cleared prior to changing this field.");
                                return;
                            }
                            if (!val)
                            {
                                rxMaxBytes = 1;
                                // assuming that disabling fifo clears it
                                ClearBuffer();
                            }
                            else
                            {
                                rxMaxBytes = (int)rxFIFOCapacity;
                            }
                        })
                    .WithValueField(4, 3, FieldMode.Read, valueProviderCallback: _ => CalculateFIFODatawordsCount(txFIFOCapacity), name: "TXFIFOSIZE / Transmit FIFO Buffer Depth")
                    .WithFlag(7, out transmitFifoEnabled, name: "TXFE / Transmit FIFO Enable", writeCallback: (current, val) =>
                        {
                            if (current != val && (transmitterEnabled.Value || receiverEnabled.Value))
                            {
                                this.Log(LogLevel.Warning, "Both CTRL[TE] and CTRL[RE] must be cleared prior to changing this field.");
                                return;
                            }
                            if (!val)
                            {
                                txMaxBytes = 1;
                                // assuming that disabling fifo clears it
                                txQueue.Clear();
                            }
                            else
                            {
                                txMaxBytes = (int)txFIFOCapacity;
                            }
                        })
                    .WithFlag(8, out receiveFifoUnderflowEnabled, name: "RXUFE / Receive FIFO Underflow Interrupt Enable")
                    .WithFlag(9, out transmitFifoOverflowEnabled, name: "TXOFE / Transmit FIFO Overflow Interrupt Enable")
                    .WithTag("RXIDEN / Receiver Idle Empty Enable", 10, 3)
                    .WithReservedBits(13, 1)
                    .WithFlag(14, FieldMode.Write, name: "RXFLUSH / Receive FIFO/Buffer Flush", writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                ClearBuffer();
                            }
                        })
                    .WithFlag(15, FieldMode.Write, name: "TXFLUSH / Transmit FIFO/Buffer Flush", writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                txQueue.Clear();
                            }
                        })
                    .WithFlag(16, out receiveFifoUnderflowInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXUF / Receiver Buffer Underflow Flag")
                    .WithFlag(17, out transmitFifoOverflowInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "TXOF / Transmitter Buffer Overflow Flag")
                    .WithReservedBits(18, 4)
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => BufferState == BufferState.Empty, name: "RXEMPT / Receive Buffer Empty")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "TXEMPT / Transmit Buffer Empty")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                );

                registersMap.Add(FifoRegistersOffset + (long)FifoRegs.Watermark, new DoubleWordRegister(this)
                    .WithValueField(0, 2, writeCallback: (_, val) => { transmitWatermark = DecodeFifoCount(val); }, name: "TXWATER / Transmit Watermark")
                    .WithReservedBits(2, 6)
                    .WithValueField(8, 3, FieldMode.Read, valueProviderCallback: _ => (uint)txQueue.Count, name: "TXCOUNT / Transmit Counter")
                    .WithReservedBits(11, 5)
                    .WithValueField(16, 2, writeCallback: (_, val) => { receiveWatermark = (uint)val; }, name: "RXWATER / Receive Watermark")
                    .WithReservedBits(18, 6)
                    .WithValueField(24, 3, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        /* This value is not backed by the manual.
                         * Since the manual is vague in this regard it is impossible to tell if this should be encoded the same
                         * way as Fifo depth (FIFO_SIZE) or real count clipped at the maximum possible to express with just 3 bits.
                         * As the available drivers suggest the second approach -  this is what we use here.
                         * But this should be adjusted if proven to not work or if the manual gets updated */
                        return (uint)Math.Min(Count, 0b111);
                    }, name: "RXCOUNT / Receive Counter")
                    .WithReservedBits(27, 5)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                );
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            lock (locker)
            {
                base.Reset(); // reset clears all buffered characters
                registers.Reset();
                txQueue.Clear();
                latestBufferState = BufferState.Empty;
                rxMaxBytes = 1;
                txMaxBytes = 1;
                UpdateBufferState();
                UpdateGPIOOutputs();
                reset.Value = true;
            }
        }

        public byte ReadByte(long offset)
        {
            lock (locker)
            {
                if (!IsDataRegister(offset))
                {
                    this.Log(LogLevel.Warning, "Trying to read byte from {0} (0x{0:X}), not supported", offset);
                    return 0;
                }

                return (byte)registers.Read(offset);
            }
        }

        public void WriteByte(long offset, byte value)
        {
            lock (locker)
            {
                if (!IsDataRegister(offset))
                {
                    this.Log(LogLevel.Warning, "Trying to read byte from {0} (0x{0:X}), not supported", offset);
                    return;
                }

                registers.Write(offset, value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock (locker)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock (locker)
            {
                registers.Write(offset, value);
            }
        }

        public override void WriteChar(byte data)
        {
            lock (locker)
            {
                if (loopMode.Value)
                {
                    if (receiverSource.Value && transmissionPinDirectionOutNotIn.Value)
                    {
                        this.Log(LogLevel.Warning, "Data ignored, uart operates in Single-Wire mode and txPin is set to output. (value: 0x{0:X})", data);
                        return;
                    }

                    if (!receiverSource.Value)
                    {
                        this.Log(LogLevel.Warning, "Data ignored, uart operates in Loop mode. (value: 0x{0:X})", data);
                        return;
                    }
                }

                if (receiverOverrun.Value)
                {
                    this.Log(LogLevel.Info, "Data ignored, receiver has been overrun. (value: 0x{0:X})", data);
                    return;
                }

                if (Count >= rxMaxBytes)
                {
                    this.Log(LogLevel.Info, "rxFIFO/Buffer is overflowing but we are buffering character", data, rxMaxBytes);
                }

                base.WriteChar(data);
                UpdateBufferState();
                UpdateInterrupt();
            }
        }

        public void ReceiveLINBreak()
        {
            linBreakDetect.Value |= linBreakDetection.Value;
            UpdateGPIOOutputs();
        }

        public event Action BroadcastLINBreak;
        public event Action<BufferState> BufferStateChanged;

        public BufferState BufferState { get; private set; }

        public long Size => 0x30;

        public override Bits StopBits => stopBitNumberSelect.Value ? Bits.Two : Bits.One;

        public override Parity ParityBit => parityEnabled.Value ? Parity.None : parityType.Value ? Parity.Odd : Parity.Even;

        public override uint BaudRate => (baudRateModuloDivisor.Value == 0)
            ? 0
            : (uint)(frequency / ((oversamplingRatio.Value == 0 ? 16 : (uint)(oversamplingRatio.Value + 1)) * (uint)baudRateModuloDivisor.Value));

        public GPIO IRQ { get; }
        public GPIO SeparateRxIRQ { get; }
        public GPIO DMA { get; }

        private void UpdateBufferState()
        {
            var count = Count;
            if (count == 0)
            {
                BufferState = BufferState.Empty;
                return;
            }

            if (receiveFifoEnabled.Value)
            {
                if ((ulong)count > receiveWatermark)
                {
                    BufferState = BufferState.Ready;
                    return;
                }
            }

            if (count >= rxMaxBytes)
            {
                BufferState = BufferState.Full;
            }
            BufferState = BufferState.Ready;
        }

        protected void TransmitData(byte data)
        {
            if (!loopMode.Value)
            {
                TransmitCharacter(data);
            }
            else if (receiverSource.Value)
            {
                if (!transmissionPinDirectionOutNotIn.Value)
                {
                    this.Log(LogLevel.Warning, "Data not transmitted, uart operates in Single-Wire mode and txPin set to input. (value: 0x{0:X})", data);
                    return;
                }
                TransmitCharacter(data);
            }
            else
            {
                WriteChar(data);
            }
        }

        protected override void CharWritten()
        {
            UpdateGPIOOutputs();
        }

        protected override void QueueEmptied()
        {
            UpdateGPIOOutputs();
        }

        private long CommonRegistersOffset => hasGlobalRegisters ? 0x10 : 0x0;
        private long FifoRegistersOffset => 0x18 + CommonRegistersOffset;

        private void UpdateGPIOOutputs()
        {
            UpdateFillLevels();
            UpdateInterrupt();
            UpdateDMA();
        }

        private void UpdateInterrupt()
        {
            var rxUnderflow = receiveFifoUnderflowEnabled.Value && receiveFifoUnderflowInterrupt.Value;
            var rx = receiverInterruptEnabled.Value && BufferState == BufferState.Ready; // Watermark level exceeded
            var linBreak = linBreakDetect.Value && linBreakDetectInterruptEnable.Value;
            var rxOverrun = overrunInterruptEnable.Value && receiverOverrun.Value;
            var rxRequest = rxUnderflow || rx || linBreak || rxOverrun;

            var txOverflow = transmitFifoOverflowEnabled.Value && transmitFifoOverflowInterrupt.Value;
            var tx = transmitterInterruptEnabled.Value && transmitDataRegisterEmpty.Value;
            var txComplete = transmissionCompleteInterruptEnabled.Value && (txQueue.Count == 0);
            var txRequest = txOverflow || tx || txComplete;

            if (separateIRQs)
            {
                SeparateRxIRQ.Set(rxRequest);
                this.Log(LogLevel.Debug, "Setting SeparateRxIRQ to {0}; rxUnderflow {1}, rx {2}, linBreak {3}", rxRequest, rxUnderflow, rx, linBreak);

                IRQ.Set(txRequest);
                this.Log(LogLevel.Debug, "Setting IRQ to {0}; txOverflow {1}, tx {2}, txComplete {3}", txRequest, txOverflow, tx, txComplete);
            }
            else
            {
                var irqState = txRequest || rxRequest;
                this.Log(LogLevel.Noisy, "Setting IRQ to {0}, rxUnderflow {1}, txOverflow {2}, tx {3}, rx {4}, txComplete {5}, linBreak {6}", irqState, rxUnderflow, txOverflow, tx, rx, txComplete, linBreak);
                IRQ.Set(irqState);
            }
        }

        private void UpdateDMA()
        {
            var drqState = false;

            drqState |= transmitterDMAEnabled.Value && transmitDataRegisterEmpty.Value;
            drqState |= receiverDMAEnabled.Value && BufferState == BufferState.Full;

            DMA.Set(drqState);
            this.Log(LogLevel.Noisy, "Setting DMA request to {0}", drqState);
        }

        private void UpdateFillLevels()
        {
            OnBufferStateChanged();
            if (transmitFifoEnabled.Value)
            {
                transmitDataRegisterEmpty.Value = txQueue.Count <= (int)transmitWatermark;
            }
            else
            {
                transmitDataRegisterEmpty.Value = txQueue.Count == 0;
            }
        }

        private void OnBufferStateChanged()
        {
            var state = BufferState;
            if (latestBufferState != state)
            {
                latestBufferState = state;
                BufferStateChanged?.Invoke(state);
            }
        }

        private bool IsDataRegister(long offset)
        {
            return offset == CommonRegistersOffset + (long)CommonRegs.Data;
        }

        private uint CalculateFIFODatawordsCount(uint capacity)
        {
            if (capacity == 1)
            {
                return 0;
            }
            return (uint)Misc.Logarithm2((int)capacity) - 1;
        }

        private uint DecodeFifoCount(ulong encodedValue)
        {
            if (encodedValue == 0)
            {
                return 1;
            }
            return (uint)Math.Pow(2, encodedValue + 1);
        }

        private BufferState latestBufferState = BufferState.Empty;
        private int rxMaxBytes = 1;
        private int txMaxBytes = 1;
        private readonly object locker;
        private readonly Queue<byte> txQueue;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IFlagRegisterField reset;
        private readonly IFlagRegisterField stopBitNumberSelect;
        private readonly IFlagRegisterField bothEdgeSampling;
        private readonly IFlagRegisterField transmitterDMAEnabled;
        private readonly IFlagRegisterField receiverDMAEnabled;
        private readonly IFlagRegisterField receiverOverrun;
        private readonly IFlagRegisterField transmitDataRegisterEmpty;
        private readonly IFlagRegisterField parityType;
        private readonly IFlagRegisterField parityEnabled;
        private readonly IFlagRegisterField receiverSource;
        private readonly IFlagRegisterField loopMode;
        private readonly IFlagRegisterField receiverEnabled;
        private readonly IFlagRegisterField transmitterEnabled;
        private readonly IFlagRegisterField receiverInterruptEnabled;
        private readonly IFlagRegisterField transmissionCompleteInterruptEnabled;
        private readonly IFlagRegisterField transmitterInterruptEnabled;
        private readonly IFlagRegisterField overrunInterruptEnable;
        private readonly IFlagRegisterField transmissionPinDirectionOutNotIn;
        private readonly IFlagRegisterField receiveFifoEnabled;
        private readonly IFlagRegisterField transmitFifoEnabled;
        private readonly IFlagRegisterField receiveFifoUnderflowEnabled;
        private readonly IFlagRegisterField transmitFifoOverflowEnabled;
        private readonly IFlagRegisterField receiveFifoUnderflowInterrupt;
        private readonly IFlagRegisterField transmitFifoOverflowInterrupt;
        private readonly IFlagRegisterField linBreakDetectInterruptEnable;
        private readonly IFlagRegisterField transmitSpecialCharacter;
        private readonly IFlagRegisterField linBreakDetect;
        private readonly IFlagRegisterField linBreakDetection;
        private readonly IValueRegisterField baudRateModuloDivisor;
        private readonly IValueRegisterField oversamplingRatio;
        private readonly long frequency;
        private readonly bool hasGlobalRegisters;
        private readonly bool separateIRQs;
        private readonly uint txFIFOCapacity;
        private readonly uint rxFIFOCapacity;
        private uint transmitWatermark;
        private uint receiveWatermark;

        private const uint DefaultFIFOSize = 256;
        private const int DataSize = 8;

        // enum belows intentionally do not contain `Register(s)` in the name
        // to avoid using them when logging accesses (which might have been
        // misleading depending on `hasGlobalRegisters` and `hasFifoRegisters` configuration)
        private enum GlobalRegs
        {
            VersionID = 0x0,
            Parameter = 0x4,
            Global = 0x8,
            PinConfiguration = 0xc,
        }

        private enum CommonRegs
        {
            BaudRate = 0x0,
            Status = 0x4,
            Control = 0x8,
            Data = 0xc,
            MatchAddress = 0x10,
            ModemIrDA = 0x14,
        }

        private enum FifoRegs
        {
            Fifo = 0x0,
            Watermark = 0x4,
        }
    }
}
