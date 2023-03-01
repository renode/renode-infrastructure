//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
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
    // This model doesn't implement idling, matching, anything that requires more than 8 bit wide data and irDA
    public class LowPower_UART : UARTBase, IBytePeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public LowPower_UART(Machine machine, long frequency = 8000000) : base(machine)
        {
            this.frequency = frequency;

            locker = new object();
            IRQ = new GPIO();
            DMA = new GPIO();
            txQueue = new Queue<byte>();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Global, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, out reset, name: "RST / Software Reset", writeCallback: (_, val) => 
                        {
                            if(val)
                            {
                                Reset();
                            }
                        })
                    .WithReservedBits(2, 30)
                },

                {(long)Registers.BaudRate, new DoubleWordRegister(this)
                    .WithValueField(0, 13, out baudRateModuloDivisor, name: "SBR / Baud Rate Modulo Divisor")
                    .WithFlag(13, out stopBitNumberSelect, name: "SBNS / Stop Bit Number Select")
                    .WithTaggedFlag("RXEDGIE / RX Input Active Edge Interrupt Enable", 14)
                    .WithTaggedFlag("LBKDIE / LIN Break Detect Interrupt Enable", 15)
                    .WithTaggedFlag("RESYNCDIS / Resynchronization Disable", 16)
                    .WithFlag(17, out bothEdgeSampling, name: "BOTHEDGE / Both Edge Sampling")
                    .WithTag("MATCFG / Match Configuration", 18, 2)
                    .WithTaggedFlag("RIDMAE / Receiver Idle DMA Enable", 20)
                    .WithFlag(21, out receiverDMAEnabled, name: "RDMAE / Receiver Full DMA Enable")
                    .WithReservedBits(22, 1)
                    .WithFlag(23, out transmitterDMAEnabled, name: "TDMAE / Transmitter DMA Enable")
                    .WithValueField(24, 5, out oversamplingRatio, name: "OSR / Oversampling Ratio", writeCallback: (current, val) =>
                        {
                            if(1 == val || 2 == val)
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
                            if((3 >= oversamplingRatio.Value && 6 <= oversamplingRatio.Value) && !bothEdgeSampling.Value)
                            {
                                this.Log(LogLevel.Warning, "Oversampling Ratio set to value: 0x{0:X}, but this requires Both Edge Sampling to be set.", oversamplingRatio.Value);
                            }
                            UpdateGPIOOutputs();
                        })
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithReservedBits(0, 14)
                    .WithTaggedFlag("MA2F / Match 2 Flag", 14)
                    .WithTaggedFlag("MA1F / Match 1 Flag", 15)
                    .WithTaggedFlag("PF / Parity Error Flag", 16)
                    .WithTaggedFlag("FE / Framing Error Flag", 17)
                    .WithTaggedFlag("NF / Noise Flag", 18)
                    .WithFlag(19, out receiverOverrun, FieldMode.Read | FieldMode.WriteOneToClear, name: "OR / Receiver Overrun Flag")
                    .WithTaggedFlag("IDLE / Idle Line Flag", 20)
                    .WithFlag(21, out receiveDataRegisterFull, FieldMode.Read, name: "RDRF / Receive Data Register Full")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "TC / Transmission Complete Flag")
                    .WithFlag(23, out transmitDataRegisterEmpty, FieldMode.Read, name: "TDRE / Transmission Data Register Empty Flag")
                    .WithTaggedFlag("RAF / Receiver Active Flag", 24)
                    .WithTaggedFlag("LBKDE / LIN Break Detection Enable", 25)
                    .WithTaggedFlag("BRK13 / Break Character Generation Length", 26)
                    .WithTaggedFlag("RWUID / Receive Wake Up Idle Detect", 27)
                    .WithTaggedFlag("RXINV / Receive Data Inversion", 28)
                    .WithTaggedFlag("MSBF / MSB First", 29)
                    .WithTaggedFlag("RXEDGIF / RXD Pin Active Edge Interrupt Flag", 30)
                    .WithTaggedFlag("LBKDIF / LIN Break Detect Interrupt Flag", 31)
                },

                {(long)Registers.Control, new DoubleWordRegister(this)
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
                            if(val)
                            {
                                foreach(var value in txQueue)
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
                    .WithTaggedFlag("ORIE / Overrun Interrupt Enable", 27)
                    .WithTaggedFlag("TXINV / Transmit Data Inversion", 28)
                    .WithFlag(29, out transmissionPinDirectionOutNotIn , name: "TXDIR / TXD Pin Direction in Single-Wire Mode")
                    .WithTaggedFlag("R9T8 / Receive Bit 9 / Transmit Bit 8", 30)
                    .WithTaggedFlag("R8T9 / Receive Bit 8 / Transmit Bit 9", 31)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                },

                {(long)Registers.Fifo, new DoubleWordRegister(this)
                    .WithValueField(0, 3, FieldMode.Read, valueProviderCallback: _ => rxFIFOSize, name: "RXFIFOSIZE / Receive FIFO Buffer Depth")
                    .WithFlag(3, out receiveFifoEnabled, name: "RXFE / Receive FIFO Enable", writeCallback: (current, val) =>
                        {
                            if(current != val && (transmitterEnabled.Value || receiverEnabled.Value))
                            {
                                this.Log(LogLevel.Warning, "Both CTRL[TE] and CTRL[RE] must be cleared prior to changing this field.");
                                return;
                            }
                            if(!val)
                            {
                                rxMaxBytes = 1;
                                // assuming that disabling fifo clears it
                                ClearBuffer();
                            }
                            else
                            {
                                rxMaxBytes = rxFIFOCapacity;
                            }
                        })
                    .WithValueField(4, 3, FieldMode.Read, valueProviderCallback: _ => txFIFOSize, name: "TXFIFOSIZE / Transmit FIFO Buffer Depth")
                    .WithFlag(7, out transmitFifoEnabled, name: "TXFE / Transmit FIFO Enable", writeCallback: (current, val) =>
                        {
                            if(current != val && (transmitterEnabled.Value || receiverEnabled.Value))
                            {
                                this.Log(LogLevel.Warning, "Both CTRL[TE] and CTRL[RE] must be cleared prior to changing this field.");
                                return;
                            }
                            if(!val)
                            {
                                txMaxBytes = 1;
                                // assuming that disabling fifo clears it
                                txQueue.Clear();
                            }
                            else
                            {
                                txMaxBytes = txFIFOCapacity;
                            }
                        })
                    .WithFlag(8, out receiveFifoUnderflowEnabled, name: "RXUFE / Receive FIFO Underflow Interrupt Enable")
                    .WithFlag(9, out transmitFifoOverflowEnabled, name: "TXOFE / Transmit FIFO Overflow Interrupt Enable")
                    .WithTag("RXIDEN / Receiver Idle Empty Enable", 10, 3)
                    .WithReservedBits(13, 1)
                    .WithFlag(14, FieldMode.Write, name: "RXFLUSH / Receive FIFO/Buffer Flush", writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                ClearBuffer();
                            }
                        })
                    .WithFlag(15, FieldMode.Write, name: "TXFLUSH / Transmit FIFO/Buffer Flush", writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                txQueue.Clear();
                            }
                        })
                    .WithFlag(16, out receiveFifoUnderflowInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXUF / Receiver Buffer Underflow Flag")
                    .WithFlag(17, out transmitFifoOverflowInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "TXOF / Transmitter Buffer Overflow Flag")
                    .WithReservedBits(18, 4)
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "RXEMPT / Receive Buffer Empty")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => txQueue.Count == 0, name: "TXEMPT / Transmit Buffer Empty")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                },

                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 9, valueProviderCallback: _ =>
                        {
                            if(!this.TryGetCharacter(out var b))
                            {
                                receiveFifoUnderflowInterrupt.Value = true;
                                this.Log(LogLevel.Warning, "Trying to read form an empty fifo");
                            }

                            UpdateFillLevels();
                            return b;
                        },
                        writeCallback: (_, val) =>
                        {
                            if(transmitterEnabled.Value)
                            {
                                TransmitData((byte)val);
                            }
                            else if(txQueue.Count < txMaxBytes)
                            {
                                txQueue.Enqueue((byte)val);

                                UpdateFillLevels();
                            }
                            else
                            {
                                transmitFifoOverflowInterrupt.Value = true;
                                this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO.");
                            }
                        })
                    .WithReservedBits(10, 1)
                    .WithTaggedFlag("IDLINE / Idle Line", 11)
                    .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "RXEMPT / Receive Buffer Empty")
                    .WithTaggedFlag("FRETSC / Frame Error / Transmit Special Character", 13)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "PARITYE / PARITYE")
                    .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => false, name: "NOISY / NOISY")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                },

                {(long)Registers.MatchAddress, new DoubleWordRegister(this)
                    .WithTag("MA1 / Match Address 1", 0, 10)
                    .WithReservedBits(10, 6)
                    .WithTag("MA2 / Match Address 2", 16, 10)
                    .WithReservedBits(26, 6)
                },

                {(long)Registers.Watermark, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out transmitWatermark, name: "TXWATER / Transmit Watermark")
                    .WithReservedBits(2, 6)
                    .WithValueField(8, 3, FieldMode.Read, valueProviderCallback: _ => (uint)txQueue.Count, name: "TXCOUNT / Transmit Counter")
                    .WithReservedBits(11, 5)
                    .WithValueField(16, 2, out receiveWatermark, name: "RXWATER / Receive Watermark")
                    .WithReservedBits(18, 6)
                    .WithValueField(24, 3, FieldMode.Read, valueProviderCallback: _ => (uint)Math.Min(Count, rxMaxBytes), name: "RXCOUNT / Receive Counter")
                    .WithReservedBits(27, 5)
                    .WithWriteCallback((_, __) => UpdateGPIOOutputs())
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                registers.Reset();
                txQueue.Clear();
                UpdateGPIOOutputs();
                reset.Value = true;
            }
        }

        public byte ReadByte(long offset)
        {
            lock(locker)
            {
                if((Registers)offset == Registers.Data)
                {
                    return (byte)registers.Read(offset);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to read byte from {0} (0x{0:X}), not supported", (Registers)offset);
                    return 0;
                }
            }
        }

        public void WriteByte(long offset, byte value)
        {
            lock(locker)
            {
                if((Registers)offset == Registers.Data)
                {
                    registers.Write(offset, value);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to read byte from {0} (0x{0:X}), not supported", (Registers)offset);
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(locker)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(locker)
            {
                registers.Write(offset, value);
            }
        }

        public override void WriteChar(byte data)
        {
            lock(locker)
            {
                if(loopMode.Value)
                {
                    if(receiverSource.Value && transmissionPinDirectionOutNotIn.Value)
                    {
                        this.Log(LogLevel.Warning, "Data ignored, uart operates in Single-Wire mode and txPin is set to output. (value: 0x{0:X})", data);
                        return;
                    }

                    if(!receiverSource.Value)
                    {
                        this.Log(LogLevel.Warning, "Data ignored, uart operates in Loop mode. (value: 0x{0:X})", data);
                        return;
                    }
                }

                if(receiverOverrun.Value)
                {
                    this.Log(LogLevel.Info, "Data ignored, receiver has been overrun. (value: 0x{0:X})", data);
                    return;
                }

                if(Count >= rxMaxBytes)
                {
                    this.Log(LogLevel.Info, "rxFIFO/Buffer is overflowing but we are buffering character", data, rxMaxBytes);
                }

                base.WriteChar(data);
            }
        }

        public long Size => 0x30;

        public override Bits StopBits => stopBitNumberSelect.Value ? Bits.Two : Bits.One;

        public override Parity ParityBit => parityEnabled.Value ? Parity.None : parityType.Value ? Parity.Odd : Parity.Even;

        public override uint BaudRate => (baudRateModuloDivisor.Value == 0)
            ? 0
            : (uint)(frequency / ((oversamplingRatio.Value == 0 ? 16 : (uint)(oversamplingRatio.Value + 1)) * (uint)baudRateModuloDivisor.Value));

        public GPIO IRQ { get; }
        public GPIO DMA { get; }

        protected void TransmitData(byte data)
        {
            if(!loopMode.Value)
            {
                TransmitCharacter(data);
            }
            else if(receiverSource.Value)
            {
                if(!transmissionPinDirectionOutNotIn.Value)
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

        private void UpdateGPIOOutputs()
        {
            UpdateFillLevels();
            UpdateInterrupt();
            UpdateDMA();
        }

        private void UpdateInterrupt()
        {
            var rxUnderflow = receiveFifoUnderflowEnabled.Value && receiveFifoUnderflowInterrupt.Value;
            var txOverflow = transmitFifoOverflowEnabled.Value && transmitFifoOverflowInterrupt.Value;
            var tx = transmitterInterruptEnabled.Value && transmitDataRegisterEmpty.Value;
            var rx = receiverInterruptEnabled.Value && (Count > 0);
            var txComplete = transmissionCompleteInterruptEnabled.Value && (txQueue.Count == 0);

            var irqState = rxUnderflow || txOverflow || tx || rx || txComplete;
            IRQ.Set(irqState);
            this.Log(LogLevel.Noisy, "Setting IRQ to {0}, rxUnderflow {1}, txOverflow {2}, tx {3}, rx {4}, txComplete {5}", irqState, rxUnderflow, txOverflow, tx, rx, txComplete);
        }

        private void UpdateDMA()
        {
            var drqState = false;

            drqState |= transmitterDMAEnabled.Value && transmitDataRegisterEmpty.Value;
            drqState |= receiverDMAEnabled.Value && receiveDataRegisterFull.Value;

            DMA.Set(drqState);
            this.Log(LogLevel.Noisy, "Setting DMA request to {0}", drqState);
        }

        private void UpdateFillLevels()
        {
            if(receiveFifoEnabled.Value)
            {
                receiveDataRegisterFull.Value = Count > (int)receiveWatermark.Value;
            }
            else
            {
                receiveDataRegisterFull.Value = Count == rxMaxBytes;
            }

            if(transmitFifoEnabled.Value)
            {
                transmitDataRegisterEmpty.Value = txQueue.Count <= (int)transmitWatermark.Value;
            }
            else
            {
                transmitDataRegisterEmpty.Value = txQueue.Count == 0;
            }
        }

        private int rxMaxBytes;
        private int txMaxBytes;
        private readonly object locker;
        private readonly Queue<byte> txQueue;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IFlagRegisterField reset;
        private readonly IFlagRegisterField stopBitNumberSelect;
        private readonly IFlagRegisterField bothEdgeSampling;
        private readonly IFlagRegisterField transmitterDMAEnabled;
        private readonly IFlagRegisterField receiverDMAEnabled;
        private readonly IFlagRegisterField receiverOverrun;
        private readonly IFlagRegisterField receiveDataRegisterFull;
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
        private readonly IFlagRegisterField transmissionPinDirectionOutNotIn;
        private readonly IFlagRegisterField receiveFifoEnabled;
        private readonly IFlagRegisterField transmitFifoEnabled;
        private readonly IFlagRegisterField receiveFifoUnderflowEnabled;
        private readonly IFlagRegisterField transmitFifoOverflowEnabled;
        private readonly IFlagRegisterField receiveFifoUnderflowInterrupt;
        private readonly IFlagRegisterField transmitFifoOverflowInterrupt;
        private readonly IValueRegisterField baudRateModuloDivisor;
        private readonly IValueRegisterField oversamplingRatio;
        private readonly IValueRegisterField transmitWatermark;
        private readonly IValueRegisterField receiveWatermark;
        private readonly long frequency;

        // txFIFOSize (TXFIFOSIZE) represents txFIFOCapacity
        private const int txFIFOCapacity = 256;
        private const uint txFIFOSize = 0b111;
        // rxFIFOSize (RXFIFOSIZE) represents rxFIFOCapacity
        private const int rxFIFOCapacity = 256;
        private const uint rxFIFOSize = 0b111;
        private const int dataSize = 8;

        private enum Registers
        {
            VersionID = 0x0,
            Parameter = 0x4,
            Global = 0x8,
            PinConfiguration = 0xc,
            BaudRate = 0x10,
            Status = 0x14,
            Control = 0x18,
            Data = 0x1c,
            MatchAddress = 0x20,
            ModemIrDA = 0x24,
            Fifo = 0x28,
            Watermark = 0x2c,
        }
    }
}
