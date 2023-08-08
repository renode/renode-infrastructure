//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class OpenTitan_I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_I2C(IMachine machine) : base(machine)
        {
            FormatWatermarkIRQ = new GPIO();
            RxWatermarkIRQ = new GPIO();
            FormatOverflowIRQ = new GPIO();
            RxOverflowIRQ = new GPIO();
            NakIRQ = new GPIO();
            SclInterfaceIRQ = new GPIO();
            SdaInterfaceIRQ = new GPIO();
            StretchTimeoutIRQ = new GPIO();
            SdaUnstableIRQ = new GPIO();
            TransactionCompleteIRQ = new GPIO();
            TxEmptyIRQ = new GPIO();
            TxNonEmptyIRQ = new GPIO();
            TxOverflowIRQ = new GPIO();
            AcqOverflowIRQ = new GPIO();
            AckAfterStopIRQ = new GPIO();
            HostTimeoutIRQ = new GPIO();

            FatalAlert = new GPIO();

            acquiredFifo = new Queue<AcquireFormatIndicator>();
            formatFifo = new Queue<FormatIndicator>();
            rxFifo = new Queue<byte>();
            txFifo = new Queue<byte>();

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptState, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0,  out formatWatermarkInterruptState,     FieldMode.Read | FieldMode.WriteOneToClear, name: "fmt_watermark")
                    .WithFlag(1,  out rxWatermarkInterruptState,         FieldMode.Read | FieldMode.WriteOneToClear, name: "rx_watermark")
                    .WithFlag(2,  out formatOverflowInterruptState,      FieldMode.Read | FieldMode.WriteOneToClear, name: "fmt_overflow")
                    .WithFlag(3,  out rxOverflowInterruptState,          FieldMode.Read | FieldMode.WriteOneToClear, name: "rx_overflow")
                    .WithFlag(4,  out nakInterruptState,                 FieldMode.Read | FieldMode.WriteOneToClear, name: "nak")
                    .WithFlag(5,  out sclInterfaceInterruptState,        FieldMode.Read | FieldMode.WriteOneToClear, name: "scl_interference")
                    .WithFlag(6,  out sdaInterfaceInterruptState,        FieldMode.Read | FieldMode.WriteOneToClear, name: "sda_interference")
                    .WithFlag(7,  out stretchTimeoutInterruptState,      FieldMode.Read | FieldMode.WriteOneToClear, name: "stretch_timeout")
                    .WithFlag(8,  out sdaUnstableInterruptState,         FieldMode.Read | FieldMode.WriteOneToClear, name: "sda_unstable")
                    .WithFlag(9,  out transactionCompleteInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "trans_complete")
                    .WithFlag(10, out txEmptyInterruptState,             FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_empty")
                    .WithFlag(11, out txNonEmptyInterruptState,          FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_nonempty")
                    .WithFlag(12, out txOverflowInterruptState,          FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_overflow")
                    .WithFlag(13, out acqOverflowInterruptState,         FieldMode.Read | FieldMode.WriteOneToClear, name: "acq_overflow")
                    .WithFlag(14, out ackAfterStopInterruptState,        FieldMode.Read | FieldMode.WriteOneToClear, name: "ack_stop")
                    .WithFlag(15, out hostTimeoutInterruptState,         FieldMode.Read | FieldMode.WriteOneToClear, name: "host_timeout")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptEnable, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0,  out formatWatermarkInterruptEnable,     name: "fmt_watermark")
                    .WithFlag(1,  out rxWatermarkInterruptEnable,         name: "rx_watermark")
                    .WithFlag(2,  out formatOverflowInterruptEnable,      name: "fmt_overflow")
                    .WithFlag(3,  out rxOverflowInterruptEnable,          name: "rx_overflow")
                    .WithFlag(4,  out nakInterruptEnable,                 name: "nak")
                    .WithFlag(5,  out sclInterfaceInterruptEnable,        name: "scl_interference")
                    .WithFlag(6,  out sdaInterfaceInterruptEnable,        name: "sda_interference")
                    .WithFlag(7,  out stretchTimeoutInterruptEnable,      name: "stretch_timeout")
                    .WithFlag(8,  out sdaUnstableInterruptEnable,         name: "sda_unstable")
                    .WithFlag(9,  out transactionCompleteInterruptEnable, name: "trans_complete")
                    .WithFlag(10, out txEmptyInterruptEnable,             name: "tx_empty")
                    .WithFlag(11, out txNonEmptyInterruptEnable,          name: "tx_nonempty")
                    .WithFlag(12, out txOverflowInterruptEnable,          name: "tx_overflow")
                    .WithFlag(13, out acqOverflowInterruptEnable,         name: "acq_overflow")
                    .WithFlag(14, out ackAfterStopInterruptEnable,        name: "ack_stop")
                    .WithFlag(15, out hostTimeoutInterruptEnable,         name: "host_timeout")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptTest, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0,  FieldMode.Write, writeCallback: (_, val) => { if(val) formatWatermarkInterruptState.Value = true; },     name: "fmt_watermark")
                    .WithFlag(1,  FieldMode.Write, writeCallback: (_, val) => { if(val) rxWatermarkInterruptState.Value = true; },         name: "rx_watermark")
                    .WithFlag(2,  FieldMode.Write, writeCallback: (_, val) => { if(val) formatOverflowInterruptState.Value = true; },      name: "fmt_overflow")
                    .WithFlag(3,  FieldMode.Write, writeCallback: (_, val) => { if(val) rxOverflowInterruptState.Value = true; },          name: "rx_overflow")
                    .WithFlag(4,  FieldMode.Write, writeCallback: (_, val) => { if(val) nakInterruptState.Value = true; },                 name: "nak")
                    .WithFlag(5,  FieldMode.Write, writeCallback: (_, val) => { if(val) sclInterfaceInterruptState.Value = true; },        name: "scl_interference")
                    .WithFlag(6,  FieldMode.Write, writeCallback: (_, val) => { if(val) sdaInterfaceInterruptState.Value = true; },        name: "sda_interference")
                    .WithFlag(7,  FieldMode.Write, writeCallback: (_, val) => { if(val) stretchTimeoutInterruptState.Value = true; },      name: "stretch_timeout")
                    .WithFlag(8,  FieldMode.Write, writeCallback: (_, val) => { if(val) sdaUnstableInterruptState.Value = true; },         name: "sda_unstable")
                    .WithFlag(9,  FieldMode.Write, writeCallback: (_, val) => { if(val) transactionCompleteInterruptState.Value = true; }, name: "trans_complete")
                    .WithFlag(10, FieldMode.Write, writeCallback: (_, val) => { if(val) txEmptyInterruptState.Value = true; },             name: "tx_empty")
                    .WithFlag(11, FieldMode.Write, writeCallback: (_, val) => { if(val) txNonEmptyInterruptState.Value = true; },          name: "tx_nonempty")
                    .WithFlag(12, FieldMode.Write, writeCallback: (_, val) => { if(val) txOverflowInterruptState.Value = true; },          name: "tx_overflow")
                    .WithFlag(13, FieldMode.Write, writeCallback: (_, val) => { if(val) acqOverflowInterruptState.Value = true; },         name: "acq_overflow")
                    .WithFlag(14, FieldMode.Write, writeCallback: (_, val) => { if(val) ackAfterStopInterruptState.Value = true; },        name: "ack_stop")
                    .WithFlag(15, FieldMode.Write, writeCallback: (_, val) => { if(val) hostTimeoutInterruptState.Value = true; },         name: "host_timeout")
                    .WithReservedBits(16,16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.AlertTest, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.Control, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, out enabledHost, name: "ENABLEHOST")
                    .WithFlag(1, out enabledTarget, name: "ENABLETARGET")
                    .WithTaggedFlag("LLPBK", 2)
                    .WithReservedBits(3, 29)
                    .WithWriteCallback((_, __) => {
                        if(enabledHost.Value && enabledTarget.Value)
                        {
                            this.Log(LogLevel.Warning, "This peripheral does not support working in both target and host mode in the same time. " +
                                                        "The mode is now set to the host mode.");
                            enabledHost.Value = true;
                            enabledTarget.Value = false;
                        }
                        this.NoisyLog("The mode set to {0}", enabledHost.Value ? "host" : (enabledTarget.Value ? "target" : "none"));
                        if(enabledHost.Value)
                        {
                            ExecuteCommands();
                        }
                    })
                },

                {(long)Registers.Status, new DoubleWordRegister(this, 0x3c )
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => (formatFifo.Count == MaximumFifoDepth), name: "FMTFULL")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => (rxFifo.Count == MaximumFifoDepth), name: "RXFULL")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => (formatFifo.Count == 0), name: "FMTEMPTY")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "HOSTIDLE")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "TARGETIDLE")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => (rxFifo.Count == 0), name: "RXEMPTY")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => (txFifo.Count == MaximumFifoDepth), name: "TXFULL")
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => (acquiredFifo.Count == MaximumFifoDepth), name: "ACQFULL")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => (txFifo.Count == 0), name: "TXEMPTY")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => (acquiredFifo.Count == 0), name: "ACQEMPTY")
                    .WithReservedBits(10, 22)
                },

                {(long)Registers.ReadData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            if(!Misc.TryDequeue(rxFifo, out var value))
                            {
                                this.Log(LogLevel.Error, "Queue empty, not able to dequeue");
                            }
                            return value;
                        }, name: "RDATA")
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.FormatData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 8, out formatByte, name: "FBYTE")
                    .WithFlag(8, out startFlag, name: "START")
                    .WithFlag(9, out stopFlag, name: "STOP")
                    .WithFlag(10, out readFlag, name: "READ")
                    .WithFlag(11, out readContinueFlag, name: "RCONT")
                    .WithFlag(12, out nakOkFlag, name: "NAKOK")
                    .WithReservedBits(13, 19)
                    .WithWriteCallback((_, __) => EnqueueFormat())
                },

                {(long)Registers.FifoControl, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) rxFifo.Clear(); }, name: "RXRST")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) formatFifo.Clear(); }, name: "FMTRST")
                    .WithEnumField<DoubleWordRegister, WatermarkLevel>(2, 3, out rxWatermarkLevel, name: "RXILVL")
                    .WithEnumField<DoubleWordRegister, WatermarkLevel>(5, 2, out fmtWatermarkLevel, name: "FMTILVL_FIELD")
                    .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { if(val) acquiredFifo.Clear(); }, name:"ACQRST")
                    .WithFlag(8, FieldMode.Write, writeCallback: (_, val) => { if(val) txFifo.Clear(); }, name:"TXRST")
                    .WithReservedBits(9, 23)
                    .WithWriteCallback((_, __) => UpdateWatermarks())
                 },

                {(long)Registers.FifoStatus, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: (_) => (uint)formatFifo.Count, name: "FMTLVL")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 7, FieldMode.Read, valueProviderCallback: (_) => (uint)txFifo.Count, name: "TXLVL")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 7, FieldMode.Read, valueProviderCallback: (_) => (uint)rxFifo.Count, name: "RXLVL")
                    .WithReservedBits(23, 1)
                    .WithValueField(24, 7, FieldMode.Read, valueProviderCallback: (_) => (uint)acquiredFifo.Count, name:"ACQLVL")
                    .WithReservedBits(31, 1)
                 },

                {(long)Registers.OverrideControl, new DoubleWordRegister(this, 0x0)
                    .WithTaggedFlag("TXOVRDEN", 0)
                    .WithTaggedFlag("SCLVAL", 1)
                    .WithTaggedFlag("SDAVAL", 2)
                    .WithReservedBits(3, 29)
                 },

                {(long)Registers.OversampledValues, new DoubleWordRegister(this, 0x0)
                    .WithTag("SCL_RX", 0, 16)
                    .WithTag("SDA_RX", 16, 16)
                 },

                {(long)Registers.Timing0, new DoubleWordRegister(this, 0x0)
                    .WithTag("THIGH", 0, 16)
                    .WithTag("TLOW", 16, 16)
                 },

                {(long)Registers.Timing1, new DoubleWordRegister(this, 0x0)
                    .WithTag("T_R", 0, 16)
                    .WithTag("T_F", 16, 16)
                 },

                {(long)Registers.Timing2, new DoubleWordRegister(this, 0x0)
                    .WithTag("TSU_STA", 0, 16)
                    .WithTag("THD_STA", 16, 16)
                 },

                {(long)Registers.Timing3, new DoubleWordRegister(this, 0x0)
                    .WithTag("TSU_DAT", 0, 16)
                    .WithTag("THD_DAT", 16, 16)
                 },

                {(long)Registers.Timing4, new DoubleWordRegister(this, 0x0)
                    .WithTag("TSU_STO", 0, 16)
                    .WithTag("T_BUF", 16, 16)
                 },

                {(long)Registers.ClockStrechingTimeout, new DoubleWordRegister(this, 0x0)
                    .WithTag("VAL", 0, 31)
                    .WithTaggedFlag("EN", 31)
                 },

                {(long)Registers.TargetId, new DoubleWordRegister(this, 0x0)
                    .WithTag("ADDRESS0", 0, 7)
                    .WithTag("MASK0", 7, 7)
                    .WithTag("ADDRESS1", 14, 7)
                    .WithTag("MASK1", 21, 7)
                    .WithReservedBits(28, 4)
                 },

                {(long)Registers.AcquiredData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 10, FieldMode.Read, valueProviderCallback: (_) =>
                        {
                            return acquiredFifo.TryDequeue(out var output) ? output.ToRegisterValue() : 0;
                        }, name:"ABYTE and SIGNAL")
                    .WithReservedBits(10, 22)
                },

                {(long)Registers.TransmitData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, val) => EnqueueTx((byte)val), name: "TXDATA")
                    .WithReservedBits(8, 24)
                 },

                {(long)Registers.TargetClockStretching, new DoubleWordRegister(this, 0x0)
                    .WithTaggedFlag("I2C_STRETCH_CTRL_EN_ADDR_TX", 0)
                    .WithTaggedFlag("I2C_STRETCH_CTRL_EN_ADDR_ACQ", 1)
                    .WithTaggedFlag("I2C_STRETCH_CTRL_STOP_TX", 2)
                    .WithTaggedFlag("I2C_STRETCH_CTRL_STOP_ACQ", 3)
                    .WithReservedBits(4, 28)
                },

                {(long)Registers.HostClockGenerationTimeout, new DoubleWordRegister(this, 0x0)
                    .WithTag("HOST_TIMEOUT_CTRL", 0, 32)
                }
            };

            acquiredFifo = new Queue<AcquireFormatIndicator>();
            formatFifo = new Queue<FormatIndicator>();
            rxFifo = new Queue<byte>();
            txFifo = new Queue<byte>();

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            registersCollection.Reset();
            UpdateWatermarks();
            ResetBuffers();

            currentState = State.Idle;
            transactionAddress = null;
            selectedSlave = null;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        // Write, Read and FinishTransmission methods are meant to be used only in the target mode
        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                return;
            }

            var index = 0;
            if(currentState == State.Idle || currentState == State.Transaction)
            {
                // Handle the start/repeated start byte
                EnqueueAcquired(new AcquireFormatIndicator(data[0], start: true, stop: currentState == State.Transaction));
                currentState = State.Transaction;
                index += 1;
            }
            for(; index < data.Length; index++)
            {
                EnqueueAcquired(new AcquireFormatIndicator(data[index], start: false, stop: false));
            }
        }

        public byte[] Read(int count)
        {
            var temp = new List<byte>();
            for(var i = 0; i < count; i++)
            {
                if(!txFifo.TryDequeue(out var data))
                {
                    break;
                }
                temp.Add(data);
            }
            return temp.ToArray();
        }

        public void FinishTransmission()
        {
            if(enabledHost.Value)
            {
                throw new RecoverableException("This should never be called in the host mode");
            }
            EnqueueAcquired(new AcquireFormatIndicator(0x0, start: false, stop: true));
            currentState = State.Idle;

            transactionCompleteInterruptState.Value = true;
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO FormatWatermarkIRQ { get; }
        public GPIO RxWatermarkIRQ { get; }
        public GPIO FormatOverflowIRQ { get; }
        public GPIO RxOverflowIRQ { get; }
        public GPIO NakIRQ { get; }
        public GPIO SclInterfaceIRQ { get; }
        public GPIO SdaInterfaceIRQ { get; }
        public GPIO StretchTimeoutIRQ { get; }
        public GPIO SdaUnstableIRQ { get; }
        public GPIO TransactionCompleteIRQ { get; }
        public GPIO TxEmptyIRQ { get; }
        public GPIO TxNonEmptyIRQ { get; }
        public GPIO TxOverflowIRQ { get; }
        public GPIO AcqOverflowIRQ { get; }
        public GPIO AckAfterStopIRQ { get; }
        public GPIO HostTimeoutIRQ { get; }

        public GPIO FatalAlert { get; }

        private void UpdateInterrupts()
        {
            FormatWatermarkIRQ.Set(formatWatermarkInterruptState.Value && formatWatermarkInterruptEnable.Value);
            RxWatermarkIRQ.Set(rxWatermarkInterruptState.Value && rxWatermarkInterruptEnable.Value);
            FormatOverflowIRQ.Set(formatOverflowInterruptState.Value && formatOverflowInterruptEnable.Value);
            RxOverflowIRQ.Set(rxOverflowInterruptState.Value && rxOverflowInterruptEnable.Value);
            NakIRQ.Set(nakInterruptState.Value && nakInterruptEnable.Value);
            SclInterfaceIRQ.Set(sclInterfaceInterruptState.Value && sclInterfaceInterruptEnable.Value);
            SdaInterfaceIRQ.Set(sdaInterfaceInterruptState.Value && sdaInterfaceInterruptEnable.Value);
            StretchTimeoutIRQ.Set(stretchTimeoutInterruptState.Value && stretchTimeoutInterruptEnable.Value);
            SdaUnstableIRQ.Set(sdaUnstableInterruptState.Value && sdaUnstableInterruptEnable.Value);
            TransactionCompleteIRQ.Set(transactionCompleteInterruptState.Value && transactionCompleteInterruptEnable.Value);
            TxEmptyIRQ.Set(txEmptyInterruptState.Value && txEmptyInterruptEnable.Value);
            TxNonEmptyIRQ.Set(txNonEmptyInterruptState.Value && txNonEmptyInterruptEnable.Value);
            TxOverflowIRQ.Set(txOverflowInterruptState.Value && txOverflowInterruptEnable.Value);
            AcqOverflowIRQ.Set(acqOverflowInterruptState.Value && acqOverflowInterruptEnable.Value);
            AckAfterStopIRQ.Set(ackAfterStopInterruptState.Value && ackAfterStopInterruptEnable.Value);
            HostTimeoutIRQ.Set(hostTimeoutInterruptState.Value && hostTimeoutInterruptEnable.Value);
        }

        private void UpdateWatermarks()
        {
            fmtWatermark = WatermarkEnumToValue(fmtWatermarkLevel.Value);
            rxWatermark = WatermarkEnumToValue(rxWatermarkLevel.Value);
        }

        private uint WatermarkEnumToValue(WatermarkLevel value)
        {
            switch(value)
            {
                case WatermarkLevel.Char1:
                    return 1;
                case WatermarkLevel.Char4:
                    return 4;
                case WatermarkLevel.Char8:
                    return 8;
                case WatermarkLevel.Char16:
                    return 16;
                default:
                    throw new ArgumentException("Illegal value");
            }
        }

        private void ExecuteCommands()
        {
            if(enabledTarget.Value)
            {
                throw new ApplicationException("This should not be possible in the target mode");
            }
            this.NoisyLog("Executing queued commands");
            while(formatFifo.Count > 0)
            {
                HandleCommand(formatFifo.Dequeue());
            }
        }

        private void HandleCommand(FormatIndicator command)
        {
            DebugHelper.Assert(selectedSlave != null || currentState == State.Idle, $"Cannot have no selected slave in the state {currentState}. This should have never happend");

            switch(currentState)
            {
                case State.Idle:
                    if(!command.StartOnly)
                    {
                        this.Log(LogLevel.Error, "Only a format code with a start is accepted in the idle state");
                        return;
                    }
                    if(!TryGetByAddress(command.Data, out selectedSlave))
                    {
                        this.Log(LogLevel.Error, "No device available under address {0}. All further transactions until STOP will be ignored", command.Data);
                        nakInterruptState.Value = true;
                        UpdateInterrupts();
                        currentState = State.Error;
                        return;
                    }
                    currentState = State.AwaitingAddress;
                    break;
                case State.AwaitingAddress:
                    if(!command.NoFlags)
                    {
                        this.Log(LogLevel.Error, "Expected slave address, but some of the flags are set [{0}]. Skipping", command.FlagsToString());
                        return;
                    }
                    transactionAddress = (byte)command.Data;
                    currentState = State.Transaction;
                    break;
                case State.Transaction:
                    if(command.IsRead)
                    {
                        ReadFromSlave(command.Data);
                    }
                    else if(command.NoFlags)
                    {
                        WriteToSlave(command.Data);
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Incorrect command in the 'Transaction' state. Expected read flag, or no flag when writing. Flags set: {0}", command.FlagsToString());
                        return;
                    }

                    if(command.StopFlag)
                    {
                        selectedSlave.FinishTransmission();
                        CleanupTransaction();
                    }
                    break;
                case State.Error:
                    if(command.StopFlag)
                    {
                        CleanupTransaction();
                    }
                    break;
                default:
                    throw new ArgumentException($"Illegal state: {currentState}");
            }
        }

        private void CleanupTransaction()
        {
            transactionAddress = null;
            selectedSlave = null;
            currentState = State.Idle;
        }

        private void ReadFromSlave(byte count)
        {
            // Specification does not allow a zero value - it is treated as a 256
            var bytesCount = (count == 0) ? 256 : count;
            selectedSlave.Write(new byte[] { transactionAddress.Value });
            foreach(var b in selectedSlave.Read(bytesCount))
            {
                EnqueueRx(b);
            }
        }

        private void WriteToSlave(byte data)
        {
            DebugHelper.Assert(transactionAddress != null, "Address not selected when performing read operation.");

            selectedSlave.Write(new byte[] { transactionAddress.Value, data });
        }

        private void HandleEnqueue<T>(Queue<T> queue, T value, IFlagRegisterField overflowInterrupt = null,
                                      IFlagRegisterField watermarkInterrupt = null, uint watermarkLevel = 0)
        {
            if(queue.Count == MaximumFifoDepth && overflowInterrupt != null)
            {
                overflowInterrupt.Value = true;
                UpdateInterrupts();
                this.Log(LogLevel.Warning, "Fifo {0} is at its maximum capacity of {1} elements. Dropping incoming element", queue.GetType().Name, MaximumFifoDepth);
                return;
            }
            queue.Enqueue(value);
            if(watermarkInterrupt != null && queue.Count == watermarkLevel)
            {
                watermarkInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        private void EnqueueFormat()
        {
            if(enabledTarget.Value)
            {
                this.Log(LogLevel.Warning, "Cannot enqueue commands when in target mode.");
                return;
            }
            var format = new FormatIndicator((byte)formatByte.Value, startFlag.Value, stopFlag.Value, readFlag.Value, readContinueFlag.Value,
                                             nakOkFlag.Value);
            HandleEnqueue(formatFifo, format, formatOverflowInterruptState, formatWatermarkInterruptState, fmtWatermark);
            this.Log(LogLevel.Noisy, "Enqueued format data: {0}", format);
            if(enabledHost.Value)
            {
                ExecuteCommands();
            }
        }

        private void EnqueueAcquired(AcquireFormatIndicator acquired)
        {
            if(enabledHost.Value)
            {
                this.Log(LogLevel.Warning, "Cannot enqueue acquired data when in target mode");
                return;
            }
            HandleEnqueue(acquiredFifo, acquired, acqOverflowInterruptState);
        }

        private void EnqueueRx(uint value)
        {
            HandleEnqueue(rxFifo, (byte)value, rxOverflowInterruptState, rxWatermarkInterruptState, rxWatermark);
        }

        private void EnqueueTx(byte value)
        {
            if(enabledHost.Value)
            {
                this.Log(LogLevel.Warning, "Tried to enqueue byte 0x{0:X} to the Tx fifo in the host mode. Tx Fifo is available only in the target mode");
                return;
            }
            HandleEnqueue(txFifo, value, txOverflowInterruptState);
        }

        private void ResetBuffers()
        {
            rxFifo.Clear();
            acquiredFifo.Clear();
            formatFifo.Clear();
            txFifo.Clear();
        }

        private IFlagRegisterField formatWatermarkInterruptState;
        private IFlagRegisterField rxWatermarkInterruptState;
        private IFlagRegisterField formatOverflowInterruptState;
        private IFlagRegisterField rxOverflowInterruptState;
        private IFlagRegisterField nakInterruptState;
        private IFlagRegisterField sclInterfaceInterruptState;
        private IFlagRegisterField sdaInterfaceInterruptState;
        private IFlagRegisterField stretchTimeoutInterruptState;
        private IFlagRegisterField sdaUnstableInterruptState;
        private IFlagRegisterField transactionCompleteInterruptState;
        private IFlagRegisterField txEmptyInterruptState;
        private IFlagRegisterField txNonEmptyInterruptState;
        private IFlagRegisterField txOverflowInterruptState;
        private IFlagRegisterField acqOverflowInterruptState;
        private IFlagRegisterField ackAfterStopInterruptState;
        private IFlagRegisterField hostTimeoutInterruptState;
        private IFlagRegisterField formatWatermarkInterruptEnable;
        private IFlagRegisterField rxWatermarkInterruptEnable;
        private IFlagRegisterField formatOverflowInterruptEnable;
        private IFlagRegisterField rxOverflowInterruptEnable;
        private IFlagRegisterField nakInterruptEnable;
        private IFlagRegisterField sclInterfaceInterruptEnable;
        private IFlagRegisterField sdaInterfaceInterruptEnable;
        private IFlagRegisterField stretchTimeoutInterruptEnable;
        private IFlagRegisterField sdaUnstableInterruptEnable;
        private IFlagRegisterField transactionCompleteInterruptEnable;
        private IFlagRegisterField txEmptyInterruptEnable;
        private IFlagRegisterField txNonEmptyInterruptEnable;
        private IFlagRegisterField txOverflowInterruptEnable;
        private IFlagRegisterField acqOverflowInterruptEnable;
        private IFlagRegisterField ackAfterStopInterruptEnable;
        private IFlagRegisterField hostTimeoutInterruptEnable;

        private IFlagRegisterField enabledHost;
        private IFlagRegisterField enabledTarget;
        private IFlagRegisterField startFlag;
        private IFlagRegisterField stopFlag;
        private IFlagRegisterField readFlag;
        private IFlagRegisterField readContinueFlag;
        private IFlagRegisterField nakOkFlag;
        private IValueRegisterField formatByte;

        private IEnumRegisterField<WatermarkLevel> rxWatermarkLevel;
        private IEnumRegisterField<WatermarkLevel> fmtWatermarkLevel;
        private uint rxWatermark;
        private uint fmtWatermark;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly Queue<FormatIndicator> formatFifo;
        private readonly Queue<AcquireFormatIndicator> acquiredFifo;
        private readonly Queue<byte> rxFifo;
        private readonly Queue<byte> txFifo;

        private II2CPeripheral selectedSlave;
        private byte? transactionAddress;
        private State currentState;

        private const int MaximumFifoDepth = 64;

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            Control = 0x10,
            Status = 0x14,
            ReadData = 0x18,
            FormatData = 0x1c,
            FifoControl = 0x20,
            FifoStatus = 0x24,
            OverrideControl = 0x28,
            OversampledValues = 0x2c,
            Timing0 = 0x30,
            Timing1 = 0x34,
            Timing2 = 0x38,
            Timing3 = 0x3c,
            Timing4 = 0x40,
            ClockStrechingTimeout = 0x44,
            TargetId = 0x48,
            AcquiredData = 0x4c,
            TransmitData = 0x50,
            TargetClockStretching = 0x54,
            HostClockGenerationTimeout = 0x58,
        }

        public struct AcquireFormatIndicator
        {
            public AcquireFormatIndicator(byte data, bool start, bool stop)
            {
                this.Data = data;
                if(start)
                {
                    ReadFlag = ((data & 0x1) == 1);
                }
                else
                {
                    ReadFlag = false;
                }
                this.StartFlag = start;
                this.StopFlag = stop;
            }

            public override string ToString()
            {
                return $"{Data:X2} with start: {StartFlag}, stop: {StopFlag}, read: {ReadFlag}";
            }

            public uint ToRegisterValue()
            {
                uint flags = (StartFlag ? 1u : 0u) | ((StopFlag ? 1u : 0u) << 1);
                return (uint)Data | (flags << 8);
            }

            public static AcquireFormatIndicator FromRegister(uint registerValue)
            {
                var data = (byte)registerValue;

                var startFlag = ((registerValue >> 8) & 1) == 1;
                var stopFlag = ((registerValue >> 9) & 1) == 1;

                return new AcquireFormatIndicator(data, start: startFlag, stop: stopFlag);
            }

            public byte Data { get; }
            public bool ReadFlag { get; }
            public bool StartFlag { get; }
            public bool StopFlag { get; }
        }

        public struct FormatIndicator
        {
            public FormatIndicator(byte data, bool start = false, bool stop = false, bool read = false, bool readContinue = false, bool nakOk = false)
            {
                this.Data = data;
                this.ReadFlag = read;
                this.ReadContinueFlag = readContinue;
                this.StartFlag = start;
                this.StopFlag = stop;
                this.NakOkFlag = nakOk;
            }

            public byte Data { get; }
            public bool ReadFlag { get; }
            public bool ReadContinueFlag { get; }
            public bool StartFlag { get; }
            public bool StopFlag { get; }
            public bool NakOkFlag { get; }

            public bool StartOnly => StopFlag == false && ReadFlag == false && ReadContinueFlag == false && StartFlag == true;
            public bool IsRead => ReadFlag == true || ReadContinueFlag == true;
            public bool NoFlags => StopFlag == false && ReadFlag == false && ReadContinueFlag == false && StartFlag == false;

            public override string ToString()
            {
                return $"{Data:X2} with {FlagsToString()}";
            }

            public string FlagsToString()
            {
                return $"start:{StartFlag}, stop:{StopFlag}, read:{ReadFlag}, readContinue:{ReadContinueFlag}, nakOk:{NakOkFlag}";
            }

            public uint ToRegisterFormat()
            {
                var flags = ((StartFlag ? 1 : 0) << 0) |
                            ((StopFlag ? 1 : 0) << 1) |
                            ((ReadFlag ? 1 : 0) << 2) |
                            ((ReadContinueFlag ? 1 : 0) << 3) |
                            ((NakOkFlag ? 1 : 0) << 4);
                return (uint)(Data | (flags << 8));
            }
        }

        private enum WatermarkLevel
        {
            Char1 = 0x0,
            Char4 = 0x1,
            Char8 = 0x2,
            Char16 = 0x3,
            Char30 = 0x4,
        }

        private enum State
        {
            Idle,
            AwaitingAddress,
            Transaction,
            Error,
        }
    }
}
