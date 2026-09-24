//
// Copyright (c) 2010-2026 Antmicro
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
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.I3C
{
    class DesignWare_APB_I3C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public DesignWare_APB_I3C(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            previousCommand = null;
            responseQueue.Clear();
            receiveQueue.Clear();
            toTransmitQueue.Clear();
            respondQueueThreshold = 0;
            transactionState.ResetTransaction();
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x1000;

        private static bool TryDecodeCommand(ulong raw, out ITransferCommand command)
        {
            var transferCommandBytes = BitConverter.GetBytes(raw);
            try
            {
                command = Packet.DecodeSubclass<ITransferCommand>(transferCommandBytes, GetCommandType);
                return true;
            }
            catch(ArgumentException)
            {
                command = default(ITransferCommand);
                return false;
            }
        }

        private static Type GetCommandType(IList<byte> payload)
        {
            if(payload.Count == 0)
            {
                return null;
            }
            switch((byte)(payload[0] & 0b111))
            {
            case TransferCommand.Attribute:
                return typeof(TransferCommand);
            case DataArgumentCommand.Attribute:
                return typeof(DataArgumentCommand);
            case ShortDataArgumentCommand.Attribute:
                return typeof(ShortDataArgumentCommand);
            default:
                return null;
            }
        }

        private void DefineRegisters()
        {
            Registers.DeviceControl.Define(this)
                .WithTaggedFlag("IBA_INCLUDE", 0)
                .WithReservedBits(1, 6)
                .WithTaggedFlag("I2C_SLAVE_PRESENT", 7)
                .WithTaggedFlag("HOT_JOIN_CTRL", 8)
                .WithReservedBits(9, 15)
                .WithTag("IDLE_CNT_MULTPLIER", 24, 2)
                .WithReservedBits(26, 1)
                .WithTaggedFlag("ADAPTIVE_I2C_I3C", 27)
                .WithTaggedFlag("DMA_ENABLE", 28)
                .WithTaggedFlag("ABORT", 29)
                .WithTaggedFlag("RESUME", 30)
                .WithFlag(31, out enabled, name: "ENABLE");

            Registers.DeviceAddress.Define(this, 0x00000001)
                .WithTag("STATIC_ADDR", 0, 7)
                .WithReservedBits(7, 8)
                .WithTaggedFlag("STATIC_ADDR_VALID", 15)
                .WithTag("DYNAMIC_ADDR", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("DYNAMIC_ADDR_VALID", 31);

            Registers.HardwareCapability.Define(this, 0x0000400B)
                .WithTag("DEVICE_ROLE_CONFIG", 0, 3)
                .WithTaggedFlag("HDR_DDR_EN", 3)
                .WithTaggedFlag("HDR_TS_EN", 4)
                .WithTag("CLOCK_PERIOD", 5, 6)
                .WithTag("HDR_TX_CLOCK_PERIOD", 11, 6)
                .WithTaggedFlag("DMA_EN", 17)
                .WithTaggedFlag("SLV_HJ_CAP", 18)
                .WithTaggedFlag("SLV_IBI_CAP", 19)
                .WithReservedBits(20, 12);

            Registers.CommandQueuePort.Define(this)
                .WithValueField(0, 32, out transferCommandRaw, name: "COMMAND_QUEUE_PORT",
                    writeCallback: (__, value) =>
                    {
                        if(!TryDecodeCommand(transferCommandRaw.Value, out var decodedCommand))
                        {
                            this.ErrorLog("Ignoring unrecognized transfer command: 0x{0:X}", transferCommandRaw.Value);
                            return;
                        }
                        HandleControllerCommand(decodedCommand);
                        previousCommand = decodedCommand;
                    }
                );

            Registers.ResponseQueuePort.Define(this)
                .WithPacketField<DoubleWordRegister, Response>(0, 32, FieldMode.Read, name: "RESPONSE",
                    valueProviderCallback: _ =>
                    {
                        if(responseQueue.TryDequeue(out var response))
                        {
                            return response;
                        }
                        this.WarningLog("Attempted read from empty Response Queue, returning default response");

                        return default(Response);
                    }
                )
                .WithReadCallback((_, __) => UpdateInterrupts());

            Registers.TransferDataPort.Define(this)
                .WithValueField(0, 32, name: "XFER_DATA_PORT",
                    valueProviderCallback: _ =>
                    {
                        if(receiveQueue.TryDequeue(out var received))
                        {
                            return received;
                        }
                        this.WarningLog("Attempted read from empty Receive Queue, returning 0x0");
                        return 0x0;
                    },
                    writeCallback: (_, toTransmit) => toTransmitQueue.Enqueue((uint)toTransmit)
                )
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IBIQueuePort.Define(this)
                .WithTag("IBI_STATUS_DATA", 0, 32);

            Registers.QueueThresholdControl.Define(this, 0x01010101)
                .WithTag("CMD_EMPTY_BUF_THLD", 0, 8)
                .WithValueField(8, 8, name: "RESP_BUF_THLD",
                    valueProviderCallback: _ => respondQueueThreshold,
                    writeCallback: (_, value) =>
                    {
                        var responseQueueThreasholdCandidate = (uint)(value + 1);
                        if(responseQueueThreasholdCandidate > ResponseQueueDepth)
                        {
                            this.WarningLog(
                                "Response queue threshold {0} exceeds buffer size {1}",
                                responseQueueThreasholdCandidate,
                                ResponseQueueDepth);
                            return;
                        }
                        respondQueueThreshold = responseQueueThreasholdCandidate;
                    })
                .WithTag("IBI_DATA_THLD", 16, 8)
                .WithTag("IBI_STATUS_THLD", 24, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.DataBufferThresholdControl.Define(this, 0x01010101)
                .WithTag("TX_EMPTY_BUF_THLD", 0, 3)
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, out rxFullFifoThreshold, name: "RX_BUF_THLD")
                .WithReservedBits(11, 5)
                .WithTag("TX_START_THLD", 16, 3)
                .WithReservedBits(19, 5)
                .WithTag("RX_START_THLD", 24, 3)
                .WithReservedBits(27, 5)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IBIQueueControl.Define(this)
                .WithTaggedFlag("NOTIFY_HJ_REJECTED", 0)
                .WithTaggedFlag("NOTIFY_MR_REJECTED", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("NOTIFY_SIR_REJECTED", 3)
                .WithReservedBits(4, 28);

            Registers.ResetControl.Define(this)
                .WithTaggedFlag("SOFT_RST", 0)
                .WithFlag(1, FieldMode.WriteOneToClear, name: "CMD_QUEUE_RST",
                    writeCallback: (_, __) => previousCommand = null
                )
                .WithFlag(2, FieldMode.WriteOneToClear, name: "RESP_QUEUE_RST",
                    writeCallback: (_, __) => responseQueue.Clear()
                )
                .WithFlag(3, FieldMode.WriteOneToClear, name: "TX_FIFO_RST",
                    writeCallback: (_, __) => toTransmitQueue.Clear()
                )
                .WithFlag(4, FieldMode.WriteOneToClear, name: "RX_FIFO_RST",
                    writeCallback: (_, __) => receiveQueue.Clear()
                )
                .WithTaggedFlag("IBI_QUEUE_RST", 5)
                .WithReservedBits(6, 26);

            Registers.SlaveEventStatus.Define(this, 0x0000000B)
                .WithTaggedFlag("SIR_EN", 0)
                .WithTaggedFlag("MR_EN", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("HJ_EN", 3)
                .WithTag("ACTIVITY_STATE", 4, 2)
                .WithTaggedFlag("MRL_UPDATED", 6)
                .WithTaggedFlag("MWL_UPDATED", 7)
                .WithReservedBits(8, 24);

            Registers.InterruptStatus.Define(this)
                .WithTaggedFlag("TX_THLD_STS", 0)
                .WithFlag(1, name: "RX_THLD_STS",
                    valueProviderCallback: _ => RxThresholdStatus
                )
                .WithTaggedFlag("IBI_THLD_STS", 2)
                .WithTaggedFlag("CMD_QUEUE_READY_STS", 3)
                .WithFlag(4, name: "RESP_READY_STS",
                    valueProviderCallback: _ => ResponseReadyStatus
                )
                .WithTaggedFlag("TRANSFER_ABORT_STS", 5)
                .WithTaggedFlag("CCC_UPDATED_STS", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("DYN_ADDR_ASSGN_STS", 8)
                .WithTaggedFlag("TRANSFER_ERR_STS", 9)
                .WithTaggedFlag("DEFSLV_STS", 10)
                .WithTaggedFlag("READ_REQ_RECV_STS", 11)
                .WithTaggedFlag("IBI_UPDATED_STS", 12)
                .WithTaggedFlag("BUSOWNER_UPDATED_STS", 13)
                .WithReservedBits(14, 18);

            Registers.InterruptStatusEnable.Define(this)
                .WithTaggedFlag("TX_THLD_STS_EN", 0)
                .WithFlag(1, out rxFifoThresholdStatusEnabled, name: "RX_THLD_STS_EN")
                .WithTaggedFlag("IBI_THLD_STS_EN", 2)
                .WithTaggedFlag("CMD_QUEUE_READY_STS_EN", 3)
                .WithFlag(4, out respondReadyStatusEnabled, name: "RESP_READY_STS_EN")
                .WithTaggedFlag("TRANSFER_ABORT_STS_EN", 5)
                .WithTaggedFlag("CCC_UPDATED_STS_EN", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("DYN_ADDR_ASSGN_STS_EN", 8)
                .WithTaggedFlag("TRANSFER_ERR_STS_EN", 9)
                .WithTaggedFlag("DEFSLV_STS_EN", 10)
                .WithTaggedFlag("READ_REQ_RECV_STS_EN", 11)
                .WithTaggedFlag("IBI_UPDATED_STS_EN", 12)
                .WithTaggedFlag("BUSOWNER_UPDATED_STS_EN", 13)
                .WithReservedBits(14, 18)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptSignalEnable.Define(this)
                .WithTaggedFlag("TX_THLD_SIGNAL_EN", 0)
                .WithFlag(1, out rxFifoThresholdSignalEnabled, name: "RX_THLD_SIGNAL_EN")
                .WithTaggedFlag("IBI_THLD_SIGNAL_EN", 2)
                .WithTaggedFlag("CMD_QUEUE_READY_SIGNAL_EN", 3)
                .WithFlag(4, out responseReadySignalEnabled, name: "RESP_READY_SIGNAL_EN")
                .WithTaggedFlag("TRANSFER_ABORT_SIGNAL_EN", 5)
                .WithTaggedFlag("CCC_UPDATED_SIGNAL_EN", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("DYN_ADDR_ASSGN_SIGNAL_EN", 8)
                .WithTaggedFlag("TRANSFER_ERR_SIGNAL_EN", 9)
                .WithTaggedFlag("DEFSLV_SIGNAL_EN", 10)
                .WithTaggedFlag("READ_REQ_RECV_SIGNAL_EN", 11)
                .WithTaggedFlag("IBI_UPDATED_SIGNAL_EN", 12)
                .WithTaggedFlag("BUSOWNER_UPDATED_SIGNAL_EN", 13)
                .WithReservedBits(14, 18)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptForce.Define(this)
                .WithTaggedFlag("TX_THLD_FORCE_EN", 0)
                .WithTaggedFlag("RX_THLD_FORCE_EN", 1)
                .WithTaggedFlag("IBI_THLD_FORCE_EN", 2)
                .WithTaggedFlag("CMD_QUEUE_READY_FORCE_EN", 3)
                .WithTaggedFlag("RESP_READY_FORCE_EN", 4)
                .WithTaggedFlag("TRANSFER_ABORT_FORCE_EN", 5)
                .WithTaggedFlag("CCC_UPDATED_FORCE_EN", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("DYN_ADDR_ASSGN_FORCE_EN", 8)
                .WithTaggedFlag("TRANSFER_ERR_FORCE_EN", 9)
                .WithTaggedFlag("DEFSLV_FORCE_EN", 10)
                .WithTaggedFlag("READ_REQ_FORCE_EN", 11)
                .WithTaggedFlag("IBI_UPDATED_FORCE_EN", 12)
                .WithTaggedFlag("BUSOWNER_UPDATED_FORCE_EN", 13)
                .WithReservedBits(14, 18);

            Registers.QueueStatusLevel.Define(this, 0x00000008)
                .WithTag("CMD_QUEUE_EMPTY_LOC", 0, 8)
                .WithValueField(8, 8, name: "RESP_BUF_BLR",
                    valueProviderCallback: _ => (ulong)responseQueue.Count
                )
                .WithTag("IBI_BUF_BLR", 16, 8)
                .WithTag("IBI_STS_CNT", 24, 5)
                .WithReservedBits(29, 3);

            Registers.DataBufferStatusLevel.Define(this, 0x00000020)
                .WithTag("TX_BUF_EMPTY_LOC", 0, 8)
                .WithReservedBits(8, 8)
                .WithTag("RX_BUF_BLR", 16, 8)
                .WithReservedBits(24, 8);

            Registers.PresentState.Define(this, 0x00000007)
                .WithTaggedFlag("SCL_LINE_SIGNAL_LEVEL", 0)
                .WithTaggedFlag("SDA_LINE_SIGNAL_LEVEL", 1)
                .WithTaggedFlag("CURRENT_MASTER", 2)
                .WithReservedBits(3, 5)
                .WithTag("CM_TFR_STS", 8, 6)
                .WithReservedBits(14, 2)
                .WithTag("CM_TFR_ST_STS", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("CMD_TID", 24, 4)
                .WithTaggedFlag("CONTROLLER_IDLE", 28)
                .WithReservedBits(29, 3);

            Registers.CCCDeviceStatus.Define(this)
                .WithTag("PENDING_INTR", 0, 4)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("PROTOCOL_ERR", 5)
                .WithTag("ACTIVITY_MODE", 6, 2)
                .WithTaggedFlag("UNDERFLOW_ERR", 8)
                .WithTaggedFlag("SLAVE_BUSY", 9)
                .WithTaggedFlag("OVERFLOW_ERR", 10)
                .WithTaggedFlag("DATA_NOT_READY", 11)
                .WithTaggedFlag("BUFFER_NOT_AVAIL", 12)
                .WithTaggedFlag("FRAME_ERROR", 13)
                .WithReservedBits(14, 18);

            Registers.DeviceAddressTablePointer.Define(this, 0x00040240)
                .WithValueField(0, 16, name: "P_DEV_ADDR_TABLE_START_ADDR",
                    valueProviderCallback: _ => (ulong)Registers.DeviceAddressTable1Location
                )
                .WithValueField(16, 16, name: "DEV_ADDR_TABLE_DEPTH",
                    valueProviderCallback: _ => (ulong)DeviceAddressTableDepth
                );

            Registers.DeviceCharacteristicTablePointer.Define(this, 0x00100200)
                .WithTag("P_DEV_CHAR_TABLE_START_ADDR", 0, 12)
                .WithTag("DEV_CHAR_TABLE_DEPTH", 12, 7)
                .WithTag("PRESENT_DEV_CHAR_TABLE_INDX", 19, 2)
                .WithReservedBits(21, 11);

            Registers.VendorSpecificRegisterPointer.Define(this, 0x000000B0)
                .WithTag("P_VENDOR_REG_START_ADDR", 0, 16)
                .WithReservedBits(16, 16);

            Registers.SlaveMipiIdValue.Define(this)
                .WithTaggedFlag("SLV_PROV_ID_SEL", 0)
                .WithTag("SLV_MIPI_MFG_ID", 1, 15)
                .WithReservedBits(16, 16);

            Registers.SlavePidValue.Define(this)
                .WithTag("SLV_PID_DCR", 0, 12)
                .WithTag("SLV_INST_ID", 12, 4)
                .WithTag("SLV_PART_ID", 16, 16);

            Registers.SlaveCharacteristicControl.Define(this, 0x01000107)
                .WithTaggedFlag("MAX_DATA_SPEED_LIMIT", 0)
                .WithTaggedFlag("IBI_REQUEST_CAPABLE", 1)
                .WithTaggedFlag("IBI_PAYLOAD", 2)
                .WithTaggedFlag("OFFLINE_CAPABLE", 3)
                .WithTaggedFlag("BRIDGE_IDENTIFIER", 4)
                .WithTaggedFlag("HDR_CAPABLE", 5)
                .WithTag("DEVICE_ROLE", 6, 2)
                .WithTag("DCR", 8, 8)
                .WithTag("HDR_CAP", 16, 8)
                .WithReservedBits(24, 8);

            Registers.SlaveMaxLength.Define(this, 0x00FF00FF)
                .WithTag("MWL", 0, 16)
                .WithTag("MRL", 16, 16);

            Registers.MaxReadTurnaround.Define(this)
                .WithTag("MXDS_MAX_RD_TURN", 0, 24)
                .WithReservedBits(24, 8);

            Registers.MaxDataSpeed.Define(this)
                .WithTag("MXDS_MAX_WR_SPEED", 0, 3)
                .WithReservedBits(3, 5)
                .WithTag("MXDS_MAX_RD_SPEED", 8, 3)
                .WithReservedBits(11, 5)
                .WithTag("MXDS_CLK_DATA_TURN", 16, 3)
                .WithReservedBits(19, 13);

            Registers.SlaveInterruptRequest.Define(this)
                .WithTaggedFlag("SIR", 0)
                .WithTag("SIR_CTRL", 1, 2)
                .WithTaggedFlag("MR", 3)
                .WithReservedBits(4, 4)
                .WithTag("MDB", 8, 8)
                .WithTag("SIR_DATA_LENGTH", 16, 8)
                .WithReservedBits(24, 8);

            Registers.SlaveSirData.Define(this)
                .WithTag("SIR_DATA_BYTE0", 0, 8)
                .WithTag("SIR_DATA_BYTE1", 8, 8)
                .WithTag("SIR_DATA_BYTE2", 16, 8)
                .WithTag("SIR_DATA_BYTE3", 24, 8);

            Registers.SlaveIbiResponse.Define(this)
                .WithTag("IBI_STS", 0, 2)
                .WithReservedBits(2, 6)
                .WithTag("SIR_RESP_DATA_LENGTH", 8, 16)
                .WithReservedBits(24, 8);

            Registers.DeviceControlExtended.Define(this)
                .WithTag("DEV_OPERATION_MODE", 0, 2)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("REQMST_ACK_CTRL", 3)
                .WithReservedBits(4, 28);

            Registers.SclI3COpenDrainTiming.Define(this, 0x000A0010)
                .WithTag("I3C_OD_LCNT", 0, 8)
                .WithReservedBits(8, 8)
                .WithTag("I3C_OD_HCNT", 16, 8)
                .WithReservedBits(24, 8);

            Registers.SclI3CPushPullTiming.Define(this, 0x000A000A)
                .WithTag("I3C_PP_LCNT", 0, 8)
                .WithReservedBits(8, 8)
                .WithTag("I3C_PP_HCNT", 16, 8)
                .WithReservedBits(24, 8);

            Registers.SclI2CFastModeTiming.Define(this, 0x00100010)
                .WithTag("I2C_FM_LCNT", 0, 16)
                .WithTag("I2C_FM_HCNT", 16, 16);

            Registers.SclI2CFastModePlusTiming.Define(this, 0x00100010)
                .WithTag("I2C_FMP_LCNT", 0, 16)
                .WithTag("I2C_FMP_HCNT", 16, 8)
                .WithReservedBits(24, 8);

            Registers.SclExtendedLowCountTiming.Define(this)
                .WithTag("I3C_EXT_LCNT_1", 0, 8)
                .WithTag("I3C_EXT_LCNT_2", 8, 8)
                .WithTag("I3C_EXT_LCNT_3", 16, 8)
                .WithTag("I3C_EXT_LCNT_4", 24, 8);

            Registers.SclExtendedTerminationLowCountTiming.Define(this, 0x10000000)
                .WithTag("I3C_EXT_TERMN_LCNT", 0, 4)
                .WithReservedBits(4, 24)
                .WithTag("STOP_HLD_CNT", 28, 4);

            Registers.SdaHoldSwitchDelayTiming.Define(this, 0x00010000)
                .WithTag("SDA_OD_PP_SWITCH_DLY", 0, 3)
                .WithReservedBits(3, 5)
                .WithTag("SDA_PP_OD_SWITCH_DLY", 8, 3)
                .WithReservedBits(11, 5)
                .WithTag("SDA_TX_HOLD", 16, 3)
                .WithReservedBits(19, 13);

            Registers.BusFreeAvailableTiming.Define(this, 0x00200020)
                .WithTag("BUS_FREE_TIME", 0, 16)
                .WithTag("BUS_AVAILABLE_TIME", 16, 16);

            Registers.BusIdleTiming.Define(this, 0x00000020)
                .WithTag("BUS_IDLE_TIME", 0, 20)
                .WithReservedBits(20, 12);

            Registers.I3CVersionId.Define(this, 0x3130332A)
                .WithTag("I3C_VER_ID", 0, 32);

            Registers.I3CVersionType.Define(this, 0x6C633030)
                .WithTag("I3C_VER_TYPE", 0, 32);

            Registers.QueueSizeCapability.Define(this, 0x00004244)
                .WithTag("TX_BUF_SIZE", 0, 4)
                .WithTag("RX_BUF_SIZE", 4, 4)
                .WithTag("CMD_BUF_SIZE", 8, 4)
                .WithTag("RESP_BUF_SIZE", 12, 4)
                .WithTag("IBI_BUF_SIZE", 16, 4)
                .WithReservedBits(20, 12);

            Registers.DeviceCharacteristicTable1Location1.Define(this)
                .WithTag("MSB_PROVISIONAL_ID", 0, 32);

            Registers.DeviceCharacteristicTable1Location2.Define(this)
                .WithTag("LSB_PROVISIONAL_ID", 0, 16)
                .WithReservedBits(16, 16);

            Registers.DeviceCharacteristicTable1Location3.Define(this)
                .WithTag("DCR", 0, 8)
                .WithTag("BCR", 8, 8)
                .WithReservedBits(16, 16);

            Registers.DeviceCharacteristicTable1Location4.Define(this)
               .WithTag("DEV_DYNAMIC_ADDR", 0, 8)
               .WithReservedBits(8, 24);

            Registers.DeviceAddressTable1Location.DefineMany(this, count: DeviceAddressTableDepth,
                setup: (register, index) =>
                {
                    var device = devices[index];
                    register
                        .WithValueField(0, 7, out device.Address, name: "STATIC_ADDRESS")
                        .WithReservedBits(7, 4)
                        .WithTaggedFlag("IBI_PEC_EN", 11)
                        .WithTaggedFlag("IBI_WITH_DATA", 12)
                        .WithTaggedFlag("SIR_REJECT", 13)
                        .WithTaggedFlag("MR_REJECT", 14)
                        .WithReservedBits(15, 1)
                        .WithValueField(16, 8, name: "DEV_DYNAMIC_ADDR",
                            writeCallback: (_, value) =>
                            {
                                if(value != 0)
                                {
                                    this.WarningLog("Dynamic addresses are not supported by this peripheral");
                                }
                            }
                        )
                        .WithReservedBits(24, 5)
                        .WithTag("DEV_NACK_RETRY_CNT", 29, 2)
                        .WithFlag(31, out device.LegacyI2C, name: "LEGACY_I2C_DEVICE")
                        .WithWriteCallback((_, __) =>
                            {
                                var isDeviceActive = device.Address.Value != 0;
                                if(isDeviceActive && !device.LegacyI2C.Value)
                                {
                                    this.WarningLog("I3C targets are not supported by this peripheral");
                                }

                                if(transactionState.IsActive)
                                {
                                    this.WarningLog("Changing device address table during ongoing transaction leads to undefined behaviour");
                                }
                            }
                        );
                }
            );
        }

        private void UpdateInterrupts()
        {
            var current = (RxThresholdStatus && rxFifoThresholdSignalEnabled.Value) ||
                            (ResponseReadyStatus && responseReadySignalEnabled.Value);

            if(IRQ.IsSet != current)
            {
                this.NoisyLog("IRQ: {0}", current);
            }

            IRQ.Set(current);
        }

        private void HandleControllerCommand(ITransferCommand command)
        {
            if(command is not TransferCommand transferCommand)
            {
                return;
            }
            switch(previousCommand)
            {
            case null:
            case TransferCommand _:
                this.WarningLog("Invalid command sequence: TransferCommand requires a preceding Data Command");
                break;
            case DataArgumentCommand dataArgumentCommand:
                HandleDataArgumentTransferCommand(transferCommand, dataArgumentCommand);
                break;
            case ShortDataArgumentCommand _:
                this.WarningLog("ShortDataArgument command handling is not implemented");
                break;
            }
            UpdateInterrupts();
        }

        private void HandleDataArgumentTransferCommand(TransferCommand transferCommand, DataArgumentCommand dataArgumentCommand)
        {
            var deviceId = transferCommand.DeviceIndex;
            var deviceAddress = (int)devices[deviceId].Address.Value;
            var transactionId = transferCommand.TransactionId;
            var dataLength = (int)dataArgumentCommand.DataLength;
            var terminationOnCompletion = transferCommand.TerminationOnCompletion;
            var isRead = transferCommand.IsReadTransfer;
            var responseOnCompletion = transferCommand.ResponseOnCompletion;

            if(!TryGetByAddress(deviceAddress, out var slave))
            {
                this.WarningLog("Trying to access a non-existing I2C device at address 0x{0:X}", deviceAddress);
                var errorResponse = new Response() { ErrorStatus = Response.ErrorType.AddressNACKd, TransactionId=transactionId, DataLength=dataLength };
                responseQueue.Enqueue(errorResponse);
                return;
            }

            if(transactionState.HasDeviceChanged(deviceId, slave))
            {
                this.WarningLog("Finishing transmission due to a device change during transaction to address 0x{0:X}. Such behaviour is not covered by the documentation", deviceAddress);
                FinishTransaction(transactionState.Slave);
                transactionState.SetDevice(deviceId, slave);
            }

            if(!responseOnCompletion)
            {
                this.WarningLog("Disabling ResponseOnCompletion is not implemented, response will be generated");
                responseOnCompletion = true;
            }
            var responseDataLength = 0;
            if(isRead)
            {
                var received = slave.Read(dataLength);
                responseDataLength = received.Length;
                receiveQueue.EnqueueRange(received
                            .Chunk(4)
                            .Select(x => x.ToUInt32Smart()));
            }
            else
            {
                var intsToSend = dataLength.DivCeil(4);
                var toSend = toTransmitQueue.DequeueRange(intsToSend)
                        .SelectMany(BitConverter.GetBytes) // Split UInt32 to bytes
                        .Take(dataLength)
                        .ToArray();
                slave.Write(toSend);
                responseDataLength = 0; // The I2C Access is always successful in Renode
            }
            if(terminationOnCompletion)
            {
                FinishTransaction(slave);
            }
            if(responseOnCompletion)
            {
                var response = new Response()
                {
                    DataLength = responseDataLength,
                    TransactionId = transactionId,
                };
                responseQueue.Enqueue(response);
            }
        }

        private void FinishTransaction(II2CPeripheral slave)
        {
            slave?.FinishTransmission();
            transactionState.ResetTransaction();
        }

        private bool RxThreshold => (ulong)receiveQueue.Count >= rxFullFifoThreshold.Value;

        private bool RxThresholdStatus => RxThreshold && rxFifoThresholdStatusEnabled.Value;

        private bool ResponseReady => responseQueue.Count >= respondQueueThreshold;

        private bool ResponseReadyStatus => ResponseReady && respondReadyStatusEnabled.Value;

        private ITransferCommand previousCommand;
        private IValueRegisterField transferCommandRaw;
        private uint respondQueueThreshold;
        private IValueRegisterField rxFullFifoThreshold;

        private IFlagRegisterField responseReadySignalEnabled;
        private IFlagRegisterField respondReadyStatusEnabled;
        private IFlagRegisterField rxFifoThresholdStatusEnabled;

        private IFlagRegisterField enabled;
        private IFlagRegisterField rxFifoThresholdSignalEnabled;

        private readonly Queue<Response> responseQueue = new();
        private readonly Queue<uint> receiveQueue = new();
        private readonly Queue<uint> toTransmitQueue = new();
        private readonly TransactionState transactionState = new();
        // Devices must be initialized exactly once because their fields hold references
        // to register framework values created during register definition.
        private readonly Device[] devices = Misc.Iterate(() => new Device()).Take(DeviceAddressTableDepth).ToArray();

        private const uint ResponseQueueDepth = 4;
        private const int DeviceAddressTableDepth = 32;

        public enum DeviceRoleConfig
        {
            Master = 0x1,
            SecondaryMaster = 0x3,
            SlaveOnly = 0x4
        }

        public enum I2CSpeed
        {
            FastMode = 0,
            FastModePlus = 1
        }

        public enum Registers
        {
            DeviceControl = 0x00, // DEVICE_CTRL
            DeviceAddress = 0x04, // DEVICE_ADDR
            HardwareCapability = 0x08, // HW_CAPABILITY

            CommandQueuePort = 0x0C, // COMMAND_QUEUE_PORT
            ResponseQueuePort = 0x10, // RESPONSE_QUEUE_PORT
            TransferDataPort = 0x14, // XFER_DATA_PORT
            IBIQueuePort = 0x18, // IBI_QUEUE_PORT

            QueueThresholdControl = 0x1C, // QUEUE_THLD_CTRL
            DataBufferThresholdControl = 0x20, // DATA_BUFFER_THLD_CTRL
            IBIQueueControl = 0x24, // IBI_QUEUE_CTRL

            ResetControl = 0x34, // RESET_CTRL
            SlaveEventStatus = 0x38, // SLV_EVENT_STATUS
            InterruptStatus = 0x3C, // INTR_STATUS
            InterruptStatusEnable = 0x40, // INTR_STATUS_EN
            InterruptSignalEnable = 0x44, // INTR_SIGNAL_EN
            InterruptForce = 0x48, // INTR_FORCE

            QueueStatusLevel = 0x4C, // QUEUE_STATUS_LEVEL
            DataBufferStatusLevel = 0x50, // DATA_BUFFER_STATUS_LEVEL
            PresentState = 0x54, // PRESENT_STATE

            CCCDeviceStatus = 0x58, // CCC_DEVICE_STATUS
            DeviceAddressTablePointer = 0x5C, // DEVICE_ADDR_TABLE_POINTER
            DeviceCharacteristicTablePointer = 0x60, // DEV_CHAR_TABLE_POINTER
            VendorSpecificRegisterPointer = 0x6C, // VENDOR_SPECIFIC_REG_POINTER

            SlaveMipiIdValue = 0x70, // SLV_MIPI_ID_VALUE
            SlavePidValue = 0x74, // SLV_PID_VALUE
            SlaveCharacteristicControl = 0x78, // SLV_CHAR_CTRL
            SlaveMaxLength = 0x7C, // SLV_MAX_LEN

            MaxReadTurnaround = 0x80, // MAX_READ_TURNAROUND
            MaxDataSpeed = 0x84, // MAX_DATA_SPEED

            SlaveInterruptRequest = 0x8C, // SLV_INTR_REQ
            SlaveSirData = 0x94, // SLV_SIR_DATA
            SlaveIbiResponse = 0x98, // SLV_IBI_RESP

            DeviceControlExtended = 0xB0, // DEVICE_CTRL_EXTENDED

            SclI3COpenDrainTiming = 0xB4, // SCL_I3C_OD_TIMING
            SclI3CPushPullTiming = 0xB8, // SCL_I3C_PP_TIMING
            SclI2CFastModeTiming = 0xBC, // SCL_I2C_FM_TIMING
            SclI2CFastModePlusTiming = 0xC0, // SCL_I2C_FMP_TIMING
            SclExtendedLowCountTiming = 0xC8, // SCL_EXT_LCNT_TIMING
            SclExtendedTerminationLowCountTiming = 0xCC, // SCL_EXT_TERMN_LCNT_TIMING
            SdaHoldSwitchDelayTiming = 0xD0, // SDA_HOLD_SWITCH_DLY_TIMING

            BusFreeAvailableTiming = 0xD4, // BUS_FREE_AVAIL_TIMING
            BusIdleTiming = 0xD8, // BUS_IDLE_TIMING

            I3CVersionId = 0xE0, // I3C_VER_ID
            I3CVersionType = 0xE4, // I3C_VER_TYPE
            QueueSizeCapability = 0xE8, // QUEUE_SIZE_CAPABILITY

            DeviceCharacteristicTable1Location1 = 0x200, // DEV_CHAR_TABLE1_LOC1
            // SecondaryDeviceCharacteristicTable1 = 0x200, // SEC_DEV_CHAR_TABLE1
            DeviceCharacteristicTable1Location2 = 0x204, // DEV_CHAR_TABLE1_LOC2
            DeviceCharacteristicTable1Location3 = 0x208, // DEV_CHAR_TABLE1_LOC3
            DeviceCharacteristicTable1Location4 = 0x20C, // DEV_CHAR_TABLE1_LOC4

            DeviceAddressTable1Location = 0x240 // DEV_ADDR_TABLE1_LOC1
        }

        private class TransactionState
        {
            public void ResetTransaction()
            {
                this.DeviceId = null;
                this.Slave = null;
            }

            public void SetDevice(int deviceId, II2CPeripheral slave)
            {
                DeviceId = deviceId;
                Slave = slave;
            }

            public bool HasDeviceChanged(int deviceId, II2CPeripheral slave)
            {
                return IsActive && (DeviceId != deviceId || !ReferenceEquals(Slave, slave));
            }

            public bool IsActive => this.DeviceId.HasValue;

            public int? DeviceId = null;
            public II2CPeripheral Slave;
        }

        [LeastSignificantByteFirst]
        private class DataArgumentCommand : ITransferCommand
        {
            public override string ToString() => this.ToDebugString();

#pragma warning disable CS0649
            [PacketField, Offset(bits: 16), Width(bits: 16)]
            public UInt16 DataLength;

            [PacketField, Offset(bits: 0), Width(bits: 3)]
            public const byte Attribute = 1;
#pragma warning restore CS0649
        }

        [LeastSignificantByteFirst]
        private class TransferCommand : ITransferCommand
        {
            public override string ToString() => this.ToDebugString();

#pragma warning disable CS0649
            [PacketField, Offset(bits: 3), Width(bits: 4)]
            public byte TransactionId;

            [PacketField, Offset(bits: 16), Width(bits: 5)]
            public byte DeviceIndex;
            [PacketField, Offset(bits: 21), Width(bits: 3)]
            public I2CSpeed Speed;

            [PacketField, Offset(bits: 26), Width(bits: 1)]
            public bool ResponseOnCompletion;
            [PacketField, Offset(bits: 27), Width(bits: 1)]
            public bool ShortDataArgumentPresent;

            [PacketField, Offset(bits: 28), Width(bits: 1)]
            public bool IsReadTransfer;

            [PacketField, Offset(bits: 30), Width(bits: 1)]
            public bool TerminationOnCompletion;

            [PacketField, Offset(bits: 0), Width(bits: 3)]
            public const byte Attribute = 0;
#pragma warning restore CS0649
        }

        [LeastSignificantByteFirst]
        private class ShortDataArgumentCommand : ITransferCommand
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bits: 0), Width(bits: 3)]
            public const byte Attribute = 3;
        }

        private class Device
        {
            public IValueRegisterField Address;
            public IFlagRegisterField LegacyI2C;
        }

        private interface ITransferCommand
        {
        }

        [LeastSignificantByteFirst]
        private struct Response
        {
            public override string ToString() => this.ToDebugString();

            // Data Length
            // This field has a different representation based on the following scenarios:
            // • Write Transfers: This field represents the remaining data length of the transfer if the
            // transfer is terminated early (remaining data length = requested data length - transferred
            // data length)
            // • Read Transfers: This field represents the actual amount of data received in bytes.
            // • Address Assignment Command: This field represents the remaining device count
#pragma warning disable CS0649
            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public int DataLength;
            [PacketField, Offset(bits: 16), Width(bits: 8)]
            public int CCCHeaderType;
            [PacketField, Offset(bits: 24), Width(bits: 4)]
            public int TransactionId;
            [PacketField, Offset(bits: 28), Width(bits: 4)]
            public ErrorType ErrorStatus;
#pragma warning restore CS0649

            public enum ErrorType
            {
                NoError = 0,
                ParityError = 2,
                FrameError = 3,
                I3CBroadcastAddressNACKError = 4,
                AddressNACKd = 5,
                TransferTerminated = 8,
            }
        }
    }
}
