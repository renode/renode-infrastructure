//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class RenesasDA_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA_I2C(IMachine machine) : base(machine)
        {
            txFifo = new Queue<byte>();
            transmission = new Queue<byte>();
            rxFifo = new Queue<byte>();
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            txFifo.Clear();
            transmission.Clear();
            rxFifo.Clear();
            var previousSlaveAddress = SlaveAddress;
            RegistersCollection.Reset();
            if(SlaveAddress != previousSlaveAddress)
            {
                AddressChanged?.Invoke(SlaveAddress);
            }
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        public long Size => 0x100;

        public int SlaveAddress => TrimAddress(slaveAddress.Value, use10BitSlaveAddressing.Value);

        public event Action<int> AddressChanged;

        public const int DefaultSlaveAddress = 0x55;

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x0000007f)
                .WithFlag(0, out masterEnabled, name: "I2C_MASTER_MODE")
                .WithEnumField<DoubleWordRegister, SpeedMode>(1, 2, out speedMode, name: "I2C_SPEED",
                    changeCallback: (value, previousValue) =>
                    {
                        switch(value)
                        {
                            case SpeedMode.Standard:
                            case SpeedMode.Fast:
                            case SpeedMode.HighSpeed:
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Attempted write with reserved value to I2C_SPEED (0x{0:X}), ignoring", value);
                                speedMode.Value = previousValue;
                                break;
                        }
                    }
                )
                .WithFlag(3, out use10BitSlaveAddressing, name: "I2C_10BITADDR_SLAVE",
                    changeCallback: (_, previousValue) => HandleSlaveAddressChange(previousUse10Bit: previousValue)
                )
                .WithFlag(4, out use10BitTargetAddressing, name: "I2C_10BITADDR_MASTER",
                    changeCallback: (_, previousValue) => HandleTargetAddressChange(previousUse10Bit: previousValue)
                )
                .WithFlag(5, out var restartEnabled, name: "I2C_RESTART_EN")
                .WithTaggedFlag("I2C_SLAVE_DISABLE", 6)
                .WithTaggedFlag("I2C_STOP_DET_IFADDRESSED", 7)
                .WithTaggedFlag("I2C_TX_EMPTY_CTRL", 8)
                .WithTaggedFlag("I2C_RX_FIFO_FULL_HLD_CTRL", 9)
                .WithTaggedFlag("I2C_STOP_DET_IF_MASTER_ACTIVE", 10)
                .WithReservedBits(11, 21)
                .WithWriteCallback((_, __) =>
                {
                    activityDetected.Value = true;
                    UpdateInterrupts();
                })
            ;

            Registers.TargetAddress.Define(this, DefaultSlaveAddress)
                .WithValueField(0, 10, out targetAddress, name: "IC_TAR",
                    changeCallback: (_, previousValue) =>
                    {
                        if(IsControllerEnabled(targetAddress, previousValue, "IC_TAR"))
                        {
                            return;
                        }
                        HandleTargetAddressChange(previousAddress: previousValue);
                    }
                )
                .WithTaggedFlag("GC_OR_START", 10)
                .WithTaggedFlag("SPECIAL", 11)
                .WithReservedBits(12, 20)
            ;

            Registers.SlaveAddress.Define(this, 0x00000055)
                .WithValueField(0, 10, out slaveAddress, name: "IC_SAR",
                    changeCallback: (_, previousValue) =>
                    {
                        if(IsControllerEnabled(slaveAddress, previousValue, "IC_SAR"))
                        {
                            return;
                        }
                        HandleSlaveAddressChange(previousAddress: previousValue);
                    }
                )
                .WithReservedBits(10, 22)
            ;

            Registers.HighSpeedMasterModeCodeAddress.Define(this, 0x00000001)
                .WithTag("I2C_IC_HS_MAR", 0, 3)
                .WithReservedBits(3, 29)
            ;

            Registers.DataCommand.Define(this)
                .WithValueField(0, 8, out var data, name: "I2C_DAT",
                    valueProviderCallback: _ =>
                    {
                        if(!controllerEnabled.Value)
                        {
                            this.Log(LogLevel.Debug, "Attempted read from FIFO, but controller is not enabled");
                            return 0x0;
                        }
                        if(!masterEnabled.Value)
                        {
                            this.Log(LogLevel.Debug, "Attempted read from FIFO, but master mode is not enabled");
                            return 0x0;
                        }
                        return HandleReadFromReceiveFIFO();
                    }
                )
                .WithEnumField<DoubleWordRegister, Command>(8, 1, out var command, FieldMode.Write, name: "I2C_CMD")
                .WithFlag(9, out var finishTransmission, FieldMode.Write, name: "I2C_STOP")
                .WithFlag(10, out var restartTransmission, FieldMode.Write, name: "I2C_RESTART")
                .WithReservedBits(11, 21)
                .WithReadCallback((_, __) => UpdateInterrupts())
                .WithWriteCallback((_, __) =>
                {
                    if(!controllerEnabled.Value)
                    {
                        this.Log(LogLevel.Debug, "Attempted to preform action, but controller is not enabled");
                        return;
                    }
                    if(!masterEnabled.Value)
                    {
                        this.Log(LogLevel.Debug, "Attempted perform a {0}, but master mode is not enabled", command.Value);
                        return;
                    }

                    switch(command.Value)
                    {
                        case Command.Write:
                            if(restartTransmission.Value && restartEnabled.Value)
                            {
                                PerformTransmission();
                            }
                            HandleWriteToTransmitFIFO((byte)data.Value);
                            if(finishTransmission.Value)
                            {
                                PerformTransmission();
                                SendFinishTransmission();
                            }
                            break;
                        case Command.Read:
                            PerformTransmission();
                            bytesToReceive = checked(bytesToReceive + 1);
                            break;
                        default:
                            throw new Exception("Unreachable");
                    }
                    UpdateInterrupts();
                })
            ;

            Registers.StandardSpeedSCLHighCount.Define(this, 0x00000091)
                .WithValueField(0, 16, out standardSpeedClockHighCount, name: "IC_SS_SCL_HCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(standardSpeedClockHighCount, previousValue, "IC_SS_SCL_HCNT"))
                        {
                            return;
                        }
                        if(value < ClockHighMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_SS_SCL_HCNT (0x{0:X}), setting to {1}", value, ClockHighMinCount);
                            standardSpeedClockHighCount.Value = ClockHighMinCount;
                            return;
                        }
                        if(value > StandardSpeedClockHighMaxCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value greater then {1} to IC_SS_SCL_HCNT (0x{0:X}), ignoring", value, StandardSpeedClockHighMaxCount);
                            standardSpeedClockHighCount.Value = previousValue;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.StandardSpeedSCLLowCount.Define(this, 0x000000ab)
                .WithValueField(0, 16, out standardSpeedClockLowCount, name: "IC_SS_SCL_LCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(standardSpeedClockLowCount, previousValue, "IC_SS_SCL_LCNT"))
                        {
                            return;
                        }
                        if(value < ClockLowMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_SS_SCL_LCNT (0x{0:X}), setting to {1}", value, ClockLowMinCount);
                            standardSpeedClockLowCount.Value = ClockLowMinCount;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.FastSpeedSCLHighCount.Define(this, 0x0000001a)
                .WithValueField(0, 16, out fastSpeedClockHighCount, name: "IC_FS_SCL_HCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(fastSpeedClockHighCount, previousValue, "IC_FS_SCL_HCNT"))
                        {
                            return;
                        }
                        if(value < ClockHighMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_FS_SCL_HCNT (0x{0:X}), setting to {1}", value, ClockHighMinCount);
                            fastSpeedClockHighCount.Value = ClockHighMinCount;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.FastSpeedSCLLowCount.Define(this, 0x00000032)
                .WithValueField(0, 16, out fastSpeedClockLowCount, name: "IC_FS_SCL_LCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(fastSpeedClockLowCount, previousValue, "IC_FS_SCL_LCNT"))
                        {
                            return;
                        }
                        if(value < ClockLowMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_FS_SCL_LCNT (0x{0:X}), setting to {1}", value, ClockLowMinCount);
                            fastSpeedClockLowCount.Value = ClockLowMinCount;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.HighSpeedSCLHighCount.Define(this, 0x00000006)
                .WithValueField(0, 16, out highSpeedClockHighCount, name: "IC_HS_SCL_HCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(highSpeedClockHighCount, previousValue, "IC_HS_SCL_HCNT"))
                        {
                            return;
                        }
                        if(value < ClockHighMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_HS_SCL_HCNT (0x{0:X}), setting to {1}", value, ClockHighMinCount);
                            highSpeedClockHighCount.Value = ClockHighMinCount;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.HighSpeedSCLLowCount.Define(this, 0x00000010)
                .WithValueField(0, 16, out highSpeedClockLowCount, name: "IC_HS_SCL_LCNT",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(highSpeedClockLowCount, previousValue, "IC_HS_SCL_LCNT"))
                        {
                            return;
                        }
                        if(value < ClockLowMinCount)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to IC_HS_SCL_LCNT (0x{0:X}), setting to {1}", value, ClockLowMinCount);
                            highSpeedClockLowCount.Value = ClockLowMinCount;
                        }
                    }
                )
                .WithReservedBits(16, 16)
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "R_RX_UNDER",
                    valueProviderCallback: _ => rxUnderflow.Value
                )
                .WithTaggedFlag("R_RX_OVER", 1)
                .WithFlag(2, FieldMode.Read, name: "R_RX_FULL",
                    valueProviderCallback: _ => RxFull
                )
                .WithFlag(3, FieldMode.Read, name: "R_TX_OVER",
                    valueProviderCallback: _ => txOverflow.Value
                )
                .WithFlag(4, FieldMode.Read, name: "R_TX_EMPTY",
                    valueProviderCallback: _ => TxEmpty
                )
                .WithTaggedFlag("R_RD_REQ", 5)
                .WithFlag(6, FieldMode.Read, name: "R_TX_ABRT",
                    valueProviderCallback: _ => txAbort.Value
                )
                .WithTaggedFlag("R_RX_DONE", 7)
                .WithFlag(8, FieldMode.Read, name: "R_ACTIVITY",
                    valueProviderCallback: _ => activityDetected.Value
                )
                .WithTaggedFlag("R_STOP_DET", 9)
                .WithTaggedFlag("R_START_DET", 10)
                .WithTaggedFlag("R_GEN_CALL", 11)
                .WithTaggedFlag("R_RESTART_DET", 12)
                .WithTaggedFlag("R_MASTER_ON_HOLD", 13)
                .WithFlag(14, FieldMode.Read, name: "R_SCL_STUCK_AT_LOW",
                    valueProviderCallback: _ => false
                )
                .WithReservedBits(15, 17)
            ;

            Registers.InterruptMask.Define(this, 0x000008ff)
                .WithFlag(0, out rxUnderflowMask, name: "M_RX_UNDER")
                .WithFlag(1, name: "M_RX_OVER")
                .WithFlag(2, out rxFullMask, name: "M_RX_FULL")
                .WithFlag(3, out txOverflowMask, name: "M_TX_OVER")
                .WithFlag(4, out txEmptyMask, name: "M_TX_EMPTY")
                .WithTaggedFlag("M_RD_REQ", 5)
                .WithFlag(6, out txAbortMask, name: "M_TX_ABRT")
                .WithTaggedFlag("M_RX_DONE", 7)
                .WithFlag(8, out activityDetectedMask, name: "M_ACTIVITY")
                .WithTaggedFlag("M_STOP_DET", 9)
                .WithTaggedFlag("M_START_DET", 10)
                .WithTaggedFlag("M_GEN_CALL", 11)
                .WithTaggedFlag("M_RESTART_DET", 12)
                .WithTaggedFlag("M_MASTER_ON_HOLD", 13)
                .WithFlag(14, name: "M_SCL_STUCK_AT_LOW")
                .WithReservedBits(15, 17)
                .WithChangeCallback((_ ,__) => UpdateInterrupts())
            ;

            Registers.RawInterruptStatus.Define(this)
                .WithFlag(0, out rxUnderflow, FieldMode.Read, name: "RX_UNDER")
                .WithTaggedFlag("RX_OVER", 1)
                .WithFlag(2, FieldMode.Read, name: "RX_FULL",
                    valueProviderCallback: _ => RxFull
                )
                .WithFlag(3, out txOverflow, FieldMode.Read, name: "TX_OVER")
                .WithFlag(4, FieldMode.Read, name: "TX_EMPTY",
                    valueProviderCallback: _ => TxEmpty
                )
                .WithTaggedFlag("RD_REQ", 5)
                .WithFlag(6, out txAbort, FieldMode.Read, name: "TX_ABRT")
                .WithTaggedFlag("RX_DONE", 7)
                .WithFlag(8, out activityDetected, FieldMode.Read, name: "ACTIVITY")
                .WithTaggedFlag("STOP_DET", 9)
                .WithTaggedFlag("START_DET", 10)
                .WithTaggedFlag("GEN_CALL", 11)
                .WithTaggedFlag("RESTART_DET", 12)
                .WithTaggedFlag("MASTER_ON_HOLD", 13)
                .WithFlag(14, FieldMode.Read, name: "SCL_STUCK_AT_LOW",
                    valueProviderCallback: _ => false
                )
                .WithReservedBits(15, 17)
            ;

            Registers.ReceiveFIFOThreshold.Define(this)
                .WithValueField(0, 5, out rxFifoThreshold, name: "RX_TL")
                .WithReservedBits(5, 27)
                .WithChangeCallback((_ ,__) => UpdateInterrupts())
                .WithWriteCallback((_, value) =>
                    {
                        if(value >= FifoSize)
                        {
                            rxFifoThreshold.Value = FifoSize - 1;
                            UpdateInterrupts();
                        }
                    }
                )
            ;

            Registers.TransmitFIFOThreshold.Define(this)
                .WithValueField(0, 5, out txFifoThreshold, name: "TX_TL")
                .WithReservedBits(5, 27)
                .WithChangeCallback((_ ,__) => UpdateInterrupts())
                .WithWriteCallback((_, value) =>
                    {
                        if(value >= FifoSize)
                        {
                            txFifoThreshold.Value = FifoSize - 1;
                            UpdateInterrupts();
                        }
                    }
                )
            ;

            Registers.ClearCombinedAndIndividualInterrupt.Define(this)
                .WithTaggedFlag("CLR_INTR", 0)
                .WithReservedBits(1, 31)
                .WithReadCallback((_, __) =>
                    {
                        rxUnderflow.Value = false;
                        txOverflow.Value = false;
                        txAbort.Value = false;
                        activityDetected.Value = false;
                        UpdateInterrupts();
                    }
                )
            ;

            Registers.ClearRX_UNDERInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_UNDER", 0)
                .WithReservedBits(1, 31)
                .WithReadCallback((_, __) =>
                    {
                        rxUnderflow.Value = false;
                        UpdateInterrupts();
                    }
                )
            ;

            Registers.ClearRX_OVERInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_OVER", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearTX_OVERInterrupt.Define(this)
                .WithTaggedFlag("CLR_TX_OVER", 0)
                .WithReservedBits(1, 31)
                .WithReadCallback((_, __) =>
                    {
                        txOverflow.Value = false;
                        UpdateInterrupts();
                    }
                )
            ;

            Registers.ClearRD_REQInterrupt.Define(this)
                .WithTaggedFlag("CLR_RD_REQ", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearTX_ABRTInterrupt.Define(this)
                .WithTaggedFlag("CLR_TX_ABRT", 0)
                .WithReadCallback((_, __) =>
                    {
                        txAbort.Value = false;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.ClearRX_DONEInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_DONE", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearACTIVITYInterrupt.Define(this)
                .WithTaggedFlag("CLR_ACTIVITY", 0)
                .WithReservedBits(1, 31)
                .WithReadCallback((_, __) =>
                    {
                        activityDetected.Value = false;
                        UpdateInterrupts();
                    }
                )
            ;

            Registers.ClearSTOP_DETInterrupt.Define(this)
                .WithTaggedFlag("CLR_STOP_DET", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearSTART_DETInterrupt.Define(this)
                .WithTaggedFlag("CLR_START_DET", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearGEN_CALLInterrupt.Define(this)
                .WithTaggedFlag("CLR_GEN_CALL", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.Enable.Define(this)
                .WithFlag(0, out controllerEnabled, name: "I2C_EN")
                .WithFlag(1, name: "I2C_ABORT",
                    valueProviderCallback: _ => false,
                    writeCallback: (value, _) => { if(value && controllerEnabled.Value) AbortTransmisson(); }
                )
                .WithFlag(2, out txBlocked, name: "I2C_TX_CMD_BLOCK",
                    changeCallback: (_, __) =>
                    {
                        if(!txBlocked.Value)
                        {
                            transmission.EnqueueRange(txFifo);
                            txFifo.Clear();
                            UpdateInterrupts();
                        }
                    }
                )
                .WithReservedBits(3, 29)
            ;

            Registers.Status.Define(this, 0x00000006)
                .WithFlag(0, FieldMode.Read, name: "I2C_ACTIVITY",
                    valueProviderCallback: _ => bytesToReceive != 0 || transmission.Count != 0
                )
                .WithFlag(1, FieldMode.Read, name: "TFNF",
                    valueProviderCallback: _ => txFifo.Count != FifoSize
                )
                .WithFlag(2, FieldMode.Read, name: "TFE",
                    valueProviderCallback: _ => txFifo.Count == 0
                )
                .WithFlag(3, FieldMode.Read, name: "RFNE",
                    valueProviderCallback: _ => rxFifo.Count != 0
                )
                .WithFlag(4, FieldMode.Read, name: "RFF",
                    valueProviderCallback: _ => rxFifo.Count >= FifoSize
                )
                .WithFlag(5, FieldMode.Read, name: "MST_ACTIVITY",
                    valueProviderCallback: _ => bytesToReceive != 0 || transmission.Count != 0
                )
                .WithFlag(6, FieldMode.Read, name: "SLV_ACTIVITY",
                    valueProviderCallback: _ => false
                )
                .WithFlag(7, FieldMode.Read, name: "MST_HOLD_TX_FIFO_EMPTY",
                    valueProviderCallback: _ => transmission.Count != 0
                )
                .WithFlag(8, FieldMode.Read, name: "MST_HOLD_RX_FIFO_FULL",
                    valueProviderCallback: _ => rxFifo.Count > FifoSize
                )
                .WithFlag(9, FieldMode.Read, name: "SLV_HOLD_TX_FIFO_EMPTY",
                    valueProviderCallback: _ => false
                )
                .WithFlag(10, FieldMode.Read, name: "LV_HOLD_RX_FIFO_FULL",
                    valueProviderCallback: _ => false
                )
                .WithReservedBits(11, 21)
            ;

            Registers.TransmitFIFOLevel.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "TXFLR",
                    valueProviderCallback: _ => (ulong)txFifo.Count
                )
                .WithReservedBits(6, 26)
            ;

            Registers.ReceiveFIFOLevel.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "RXFLR",
                    valueProviderCallback: _ =>
                    {
                        if(rxFifo.Count == 0)
                        {
                            // if software polls this register for RX then perform read
                            PerformReception();
                        }
                        return (ulong)rxFifo.Count.Clamp(0, FifoSize);
                    }
                )
                .WithReservedBits(6, 26)
            ;

            Registers.SDAHoldTimeLength.Define(this, 0x00000001)
                .WithTag("I2C_SDA_TX_HOLD", 0, 16)
                .WithTag("I2C_SDA_RX_HOLD", 16, 8)
                .WithReservedBits(24, 8)
            ;

            Registers.TransmitAbortSource.Define(this)
                .WithTaggedFlag("ABRT_7B_ADDR_NOACK", 0)
                .WithTaggedFlag("ABRT_10ADDR1_NOACK", 1)
                .WithTaggedFlag("ABRT_10ADDR2_NOACK", 2)
                .WithTaggedFlag("ABRT_TXDATA_NOACK", 3)
                .WithTaggedFlag("ABRT_GCALL_NOACK", 4)
                .WithTaggedFlag("ABRT_GCALL_READ", 5)
                .WithTaggedFlag("ABRT_HS_ACKDET", 6)
                .WithTaggedFlag("ABRT_SBYTE_ACKDET", 7)
                .WithTaggedFlag("ABRT_HS_NORSTRT", 8)
                .WithTaggedFlag("ABRT_SBYTE_NORSTRT", 9)
                .WithTaggedFlag("ABRT_10B_RD_NORSTRT", 10)
                .WithTaggedFlag("ABRT_MASTER_DIS", 11)
                .WithTaggedFlag("ARB_LOST", 12)
                .WithTaggedFlag("ABRT_SLVFLUSH_TXFIFO", 13)
                .WithTaggedFlag("ABRT_SLV_ARBLOST", 14)
                .WithTaggedFlag("ABRT_SLVRD_INTX", 15)
                .WithTaggedFlag("ABRT_USER_ABRT", 16)
                .WithReservedBits(17, 15)
            ;

            Registers.DMAControl.Define(this)
                .WithTaggedFlag("RDMAE", 0)
                .WithTaggedFlag("TDMAE", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.DMATransmitDataLevel.Define(this)
                .WithTag("DMATDL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.ReceiveDataLevel.Define(this)
                .WithTag("DMARDL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.SDASetup.Define(this, 0x00000064)
                .WithTag("SDA_SETUP", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.ACKGeneralCall.Define(this)
                .WithTaggedFlag("ACK_GEN_CALL", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.EnableStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "IC_EN",
                    valueProviderCallback: _ => controllerEnabled.Value
                )
                .WithTaggedFlag("SLV_DISABLED_WHILE_BUSY", 1)
                .WithTaggedFlag("SLV_RX_DATA_LOST", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.StandardSpeedAndFastSpeedSpikeSuppressionLmitSize.Define(this, 0x00000001)
                .WithValueField(0, 8, out fastSpeedSpikeLength, name: "I2C_FS_SPKLEN",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(fastSpeedSpikeLength, previousValue, "I2C_FS_SPKLEN"))
                        {
                            return;
                        }
                        if(value < MinSpikeLength)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to I2C_FS_SPKLEN (0x{0:X}), setting to {1}", value, MinSpikeLength);
                            fastSpeedSpikeLength.Value = MinSpikeLength;
                        }
                    }
                )
                .WithReservedBits(8, 24)
            ;

            Registers.HighSpeedSpikeSuppressionLimitSize.Define(this, 0x00000001)
                .WithValueField(0, 8, out highSpeedSpikeLength, name: "I2C_HS_SPKLEN",
                    changeCallback: (value, previousValue) =>
                    {
                        if(IsControllerEnabled(highSpeedSpikeLength, previousValue, "I2C_HS_SPKLEN"))
                        {
                            return;
                        }
                        if(value < MinSpikeLength)
                        {
                            this.Log(LogLevel.Warning, "Attempted write with value lesser than {1} to I2C_HS_SPKLEN (0x{0:X}), setting to {1}", value, MinSpikeLength);
                            highSpeedSpikeLength.Value = MinSpikeLength;
                        }
                    }
                )
                .WithReservedBits(8, 24)
            ;
        }

        private void UpdateInterrupts()
        {
            var irq = false;
            irq |= rxUnderflow.Value && rxUnderflowMask.Value;
            irq |= RxFull && rxFullMask.Value;
            irq |= txOverflow.Value && txOverflowMask.Value;
            irq |= TxEmpty && txEmptyMask.Value;
            irq |= txAbort.Value && txAbortMask.Value;
            irq |= activityDetected.Value && activityDetectedMask.Value;
            this.Log(LogLevel.Debug, "IRQ {0}", irq ? "set" : "unset");
            IRQ.Set(irq);
        }

        private void AbortTransmisson()
        {
            txFifo.Clear();
            txAbort.Value = true;
            UpdateInterrupts();
        }

        private byte HandleReadFromReceiveFIFO()
        {
            if(!rxFifo.TryDequeue(out var data))
            {
                PerformReception();

                if(!rxFifo.TryDequeue(out data))
                {
                    rxUnderflow.Value = true;
                    return 0x0;
                }
            }
            return data;
        }

        private void PerformReception()
        {
            if(bytesToReceive == 0)
            {
                return;
            }

            if(!TryGetByAddress(TargetAddress, out var target))
            {
                this.Log(LogLevel.Warning, "Attempted reading of {1} bytes from an unregistered slave at 0x{0:X}", TargetAddress, bytesToReceive);
                return;
            }

            var data = target.Read(bytesToReceive);
            rxFifo.EnqueueRange(data);

            if(data.Length != bytesToReceive)
            {
                this.Log(LogLevel.Noisy, "Attempted reading of {0} bytes, but received {1}", bytesToReceive, data.Length);
                var bytesMissing = bytesToReceive - data.Length;
                if(bytesMissing > 0)
                {
                     // read 0x0 bytes when slave doesn't return enough data
                    this.Log(LogLevel.Warning, "Padding data received from a slave with {0} zeros to match expected number of bytes", bytesMissing);
                    rxFifo.EnqueueRange(Enumerable.Repeat<byte>(0x0, bytesMissing));
                }
            }
            bytesToReceive = 0;

            this.Log(LogLevel.Debug, "Reading from a slave at 0x{0:X}, data: {1}", TargetAddress, data.ToLazyHexString());
        }

        private void HandleWriteToTransmitFIFO(byte data)
        {
            if(txBlocked.Value)
            {
                if(txFifo.Count == FifoSize)
                {
                    txOverflow.Value = true;
                    return;
                }
                txFifo.Enqueue(data);
            }
            else
            {
                transmission.Enqueue(data);
            }
        }

        private void PerformTransmission()
        {
            if(transmission.Count == 0)
            {
                return;
            }
            var data = transmission.ToArray();
            transmission.Clear();

            if(!TryGetByAddress(TargetAddress, out var target))
            {
                this.Log(LogLevel.Warning, "Writing to an unregistered slave at 0x{0:X}, data: {1}", TargetAddress, data.ToLazyHexString());
                return;
            }

            this.Log(LogLevel.Debug, "Writing to a slave at 0x{0:X}, data: {1}", TargetAddress, data.ToLazyHexString());
            target.Write(data);
        }

        private void SendFinishTransmission()
        {
            if(!TryGetByAddress(TargetAddress, out var target))
            {
                return;
            }
            target.FinishTransmission();
        }

        private int TrimAddress(ulong address, bool use10Bit) => (int)address & (use10Bit ? 0x3ff : 0x7f);

        private void HandleSlaveAddressChange(ulong? previousAddress = null, bool? previousUse10Bit = null)
        {
            if(SlaveAddress != TrimAddress(previousAddress ?? slaveAddress.Value, previousUse10Bit ?? use10BitSlaveAddressing.Value))
            {
                AddressChanged?.Invoke(SlaveAddress);
            }
        }

        private void HandleTargetAddressChange(ulong? previousAddress = null, bool? previousUse10Bit = null)
        {
            if(TargetAddress != TrimAddress(previousAddress ?? targetAddress.Value, previousUse10Bit ?? use10BitTargetAddressing.Value)
                && !TryGetByAddress(TargetAddress, out var __))
            {
                this.Log(LogLevel.Debug, "Addressing unregistered slave at 0x{0:X}", TargetAddress);
            }
        }

        private bool IsControllerEnabled<T>(IRegisterField<T> field, T previousValue, string targetName)
        {
            if(controllerEnabled.Value)
            {
                field.Value = previousValue;
                this.Log(LogLevel.Warning, "Attempted write to {0} while controller enabled, ignoring", targetName);
                return true;
            }
            return false;
        }

        private int TargetAddress => TrimAddress(targetAddress.Value, use10BitTargetAddressing.Value);

        private bool RxFull => (int)rxFifoThreshold.Value < rxFifo.Count;
        private bool TxEmpty => txFifo.Count < (int)txFifoThreshold.Value;

        private IFlagRegisterField masterEnabled;
        private IEnumRegisterField<SpeedMode> speedMode;
        private IFlagRegisterField use10BitSlaveAddressing;
        private IFlagRegisterField use10BitTargetAddressing;
        private IValueRegisterField targetAddress;
        private IValueRegisterField slaveAddress;
        private IValueRegisterField standardSpeedClockHighCount;
        private IValueRegisterField standardSpeedClockLowCount;
        private IValueRegisterField fastSpeedClockHighCount;
        private IValueRegisterField fastSpeedClockLowCount;
        private IValueRegisterField highSpeedClockHighCount;
        private IValueRegisterField highSpeedClockLowCount;
        private IFlagRegisterField rxUnderflowMask;
        private IFlagRegisterField rxFullMask;
        private IFlagRegisterField txOverflowMask;
        private IFlagRegisterField txEmptyMask;
        private IFlagRegisterField txAbortMask;
        private IFlagRegisterField activityDetectedMask;
        private IFlagRegisterField rxUnderflow;
        private IFlagRegisterField txOverflow;
        private IFlagRegisterField txAbort;
        private IFlagRegisterField activityDetected;
        private IValueRegisterField rxFifoThreshold;
        private IValueRegisterField txFifoThreshold;
        private IFlagRegisterField controllerEnabled;
        private IFlagRegisterField txBlocked;
        private IValueRegisterField fastSpeedSpikeLength;
        private IValueRegisterField highSpeedSpikeLength;

        private int bytesToReceive;

        private readonly Queue<byte> txFifo;
        private readonly Queue<byte> transmission;
        private readonly Queue<byte> rxFifo;

        private const uint ClockHighMinCount = 6;
        private const uint StandardSpeedClockHighMaxCount = 65525;
        private const uint ClockLowMinCount = 8;
        private const uint MinSpikeLength = 1;
        private const int FifoSize = 32;

        private enum Registers
        {
            Control = 0x0,
            TargetAddress = 0x4,
            SlaveAddress = 0x8,
            HighSpeedMasterModeCodeAddress = 0xC,
            DataCommand = 0x10,
            StandardSpeedSCLHighCount = 0x14,
            StandardSpeedSCLLowCount = 0x18,
            FastSpeedSCLHighCount = 0x1C,
            FastSpeedSCLLowCount = 0x20,
            HighSpeedSCLHighCount = 0x24,
            HighSpeedSCLLowCount = 0x28,
            InterruptStatus = 0x2C,
            InterruptMask = 0x30,
            RawInterruptStatus = 0x34,
            ReceiveFIFOThreshold = 0x38,
            TransmitFIFOThreshold = 0x3C,
            ClearCombinedAndIndividualInterrupt = 0x40,
            ClearRX_UNDERInterrupt = 0x44,
            ClearRX_OVERInterrupt = 0x48,
            ClearTX_OVERInterrupt = 0x4C,
            ClearRD_REQInterrupt = 0x50,
            ClearTX_ABRTInterrupt = 0x54,
            ClearRX_DONEInterrupt = 0x58,
            ClearACTIVITYInterrupt = 0x5C,
            ClearSTOP_DETInterrupt = 0x60,
            ClearSTART_DETInterrupt = 0x64,
            ClearGEN_CALLInterrupt = 0x68,
            Enable = 0x6C,
            Status = 0x70,
            TransmitFIFOLevel = 0x74,
            ReceiveFIFOLevel = 0x78,
            SDAHoldTimeLength = 0x7C,
            TransmitAbortSource = 0x80,
            DMAControl = 0x88,
            DMATransmitDataLevel = 0x8C,
            ReceiveDataLevel = 0x90,
            SDASetup = 0x94,
            ACKGeneralCall = 0x98,
            EnableStatus = 0x9C,
            StandardSpeedAndFastSpeedSpikeSuppressionLmitSize = 0xA0,
            HighSpeedSpikeSuppressionLimitSize = 0xA4,
        }

        private enum SpeedMode
        {
            Standard = 1,
            Fast = 2,
            HighSpeed = 3,
        }

        private enum Command
        {
            Write = 0,
            Read = 1,
        }
    }
}
