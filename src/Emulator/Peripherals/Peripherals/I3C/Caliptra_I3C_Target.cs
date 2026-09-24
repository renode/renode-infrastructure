//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I3C
{
    partial class Caliptra_I3C_Target : IKnownSize
    {
        public void AddBinaryForStreaming(ReadFilePath fileName, int index)
        {
            if(recoveryImages.TryAdd(index, fileName))
            {
                this.Log(LogLevel.Info, "Registered binary located at {0} as recovery image {1}", fileName, index);
            }
            else
            {
                this.Log(LogLevel.Error, "Failed to register binary located at {0} as recovery image {1} - slot already taken", fileName, index);
            }
        }

        public Caliptra_AXI_DMA ConnectedDma { get; set; }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x1000;

        // For information about the recovery flow simulated in this model, visit: https://chipsalliance.github.io/caliptra-web/docs/2.1/subsystem/i3c_axi_recovery_flow.html
        // This model doesn't handle errors reported by the Firmware during the recovery
        private void LoadImageToIndirectQueue(int index)
        {
            if(!recoveryImages.ContainsKey(index))
            {
                this.Log(LogLevel.Error, "No image with index {0} exists. It won't be streamed!", index);
                return;
            }
            using var fs = File.OpenRead(recoveryImages[index]);

            var buff = new byte[4];
            while(fs.Read(buff, 0, sizeof(uint)) != 0)
            {
                imageRawData.Enqueue(BitConverter.ToUInt32(buff));
                Array.Clear(buff);
            }
            this.Log(LogLevel.Debug, "Recovery Image {0} is ready", index);

            I3cEc_SoCMgmtIf_RecIntfCfg.REC_PAYLOAD_DONE.Value = true;
            // payload_available should be asserted
            ConnectedDma?.SetPayloadAvailable(true);
        }

        partial void Reset()
        {
            IRQ.Unset();
        }

        partial void Init()
        {
            I3cEc_SecFwRecoveryIf_DeviceStatus0.DEV_STATUS.WriteCallback = (_, val) =>
            {
                this.Log(LogLevel.Debug, "Setting DEV_STATUS to 0x{0:X}", val);
                switch(val)
                {
                case (ulong)DeviceStatus.StatusPending:
                    // No action required for us
                    break;
                case (ulong)DeviceStatus.RecoveryMode:
                    // Resetting registers that are used to signal the recovery state
                    I3cEc_SoCMgmtIf_RecIntfCfg.REC_PAYLOAD_DONE.Value = false;
                    I3cEc_SecFwRecoveryIf_RecoveryCtrl.ACTIVATE_REC_IMG.Value = 0x0;
                    // ... and flushing the indirect queue
                    imageRawData.Clear();
                    // ... and clearing DMA signals
                    ConnectedDma?.SetImageActivated(false);
                    ConnectedDma?.SetPayloadAvailable(false);
                    this.Log(LogLevel.Debug, "Setting DEV_STATUS to prepare image recovery!");
                    break;
                case (ulong)DeviceStatus.RecoveryPending:
                    I3cEc_SecFwRecoveryIf_RecoveryCtrl.ACTIVATE_REC_IMG.Value = 0xF;
                    // image_activated_o should be triggered
                    ConnectedDma?.SetImageActivated(true);
                    break;
                default:
                    this.Log(LogLevel.Error, "Unsupported DEV_STATUS value 0x{0:x}", val);
                    break;
                }
            };

            I3cEc_SecFwRecoveryIf_RecoveryStatus.DEV_REC_STATUS.WriteCallback = (_, val) =>
            {
                this.Log(LogLevel.Debug, "Setting DEV_REC_STATUS to 0x{0:X}", val);
                switch(val)
                {
                case (ulong)DeviceRecoveryStatus.AwaitingRecoveryImage:
                {
                    if(I3cEc_SecFwRecoveryIf_DeviceStatus0.DEV_STATUS.Value != (ulong)DeviceStatus.RecoveryMode)
                    {
                        this.Log(LogLevel.Warning, "DEV_STATUS is not 0x3 (0). Cannot initiate recovery", nameof(DeviceStatus.RecoveryMode));
                        return;
                    }
                    var imageIndex = I3cEc_SecFwRecoveryIf_RecoveryStatus.REC_IMG_INDEX.Value;
                    this.Log(LogLevel.Debug, "Setting DEV_REC_STATUS to initiate image recovery, with image index {0}", imageIndex);
                    LoadImageToIndirectQueue((int)imageIndex);
                    break;
                }
                case (ulong)DeviceRecoveryStatus.ProcessingRecoveryImage:
                    // No action required for us
                    break;
                case (ulong)DeviceRecoveryStatus.RecoverySuccessful:
                    this.Log(LogLevel.Debug, "Recovery was successful", val);
                    ConnectedDma?.SetPayloadAvailable(false);
                    ConnectedDma?.SetImageActivated(false);
                    break;
                default:
                    this.Log(LogLevel.Error, "Unsupported DEV_REC_STATUS value 0x{0:x}", val);
                    break;
                }
            };

            I3cEc_SecFwRecoveryIf_IndirectFifoData.DATA.ValueProviderCallback = _ =>
            {
                if(!imageRawData.TryDequeue(out var res))
                {
                    this.WarningLog("Reading from empty Indirect FIFO");
                    return 0;
                }
                return (ulong)res;
            };

            I3cEc_SecFwRecoveryIf_IndirectFifoCtrl1.IMAGE_SIZE.ValueProviderCallback = _ =>
            {
                return (ulong)imageRawData.Count;
            };

            I3cEc_SecFwRecoveryIf_IndirectFifoCtrl0.RESET.WriteCallback = (_, val) =>
            {
                imageRawData.Clear();
            };

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.RX_DESC_STAT_FORCE,
                I3cEc_Tti_InterruptStatus.RX_DESC_STAT,
                I3cEc_Tti_InterruptEnable.RX_DESC_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.TX_DESC_STAT_FORCE,
                I3cEc_Tti_InterruptStatus.TX_DESC_STAT,
                I3cEc_Tti_InterruptEnable.TX_DESC_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.RX_DATA_THLD_FORCE,
                I3cEc_Tti_InterruptStatus.RX_DATA_THLD_STAT,
                I3cEc_Tti_InterruptEnable.RX_DATA_THLD_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.RX_DESC_THLD_FORCE,
                I3cEc_Tti_InterruptStatus.RX_DESC_THLD_STAT,
                I3cEc_Tti_InterruptEnable.RX_DESC_THLD_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.IBI_DONE_FORCE,
                I3cEc_Tti_InterruptStatus.IBI_DONE,
                I3cEc_Tti_InterruptEnable.IBI_DONE_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.CRR_RESPONSE_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.CRR_RESPONSE_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.CRR_RESPONSE_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.STBY_CR_DYN_ADDR_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.STBY_CR_DYN_ADDR_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.STBY_CR_DYN_ADDR_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.STBY_CR_ACCEPT_NACKED_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.STBY_CR_ACCEPT_NACKED_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.STBY_CR_ACCEPT_NACKED_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.STBY_CR_ACCEPT_OK_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.STBY_CR_ACCEPT_OK_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.STBY_CR_ACCEPT_OK_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.STBY_CR_ACCEPT_ERR_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.STBY_CR_ACCEPT_ERR_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.STBY_CR_ACCEPT_ERR_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.STBY_CR_OP_RSTACT_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.STBY_CR_OP_RSTACT_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.STBY_CR_OP_RSTACT_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.CCC_PARAM_MODIFIED_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.CCC_PARAM_MODIFIED_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.CCC_PARAM_MODIFIED_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.CCC_UNHANDLED_NACK_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.CCC_UNHANDLED_NACK_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.CCC_UNHANDLED_NACK_SIGNAL_EN);

            CreateInterruptForceCallback(
                I3cEc_StdbyCtrlMode_StbyCrIntrForce.CCC_FATAL_RSTDAA_ERR_FORCE,
                I3cEc_StdbyCtrlMode_StbyCrIntrStatus.CCC_FATAL_RSTDAA_ERR_STAT,
                I3cEc_StdbyCtrlMode_StbyCrIntrSignalEnable.CCC_FATAL_RSTDAA_ERR_SIGNAL_EN);
        }

        private void CreateInterruptForceCallback(IFlagRegisterField force, IFlagRegisterField status, IFlagRegisterField enabled)
        {
            interruptStatusRegisters.Add(status);
            force.WriteCallback += (_, value) =>
            {
                if(value && enabled.Value)
                {
                    status.Value = value;
                    UpdateInterrupts();
                }
            };
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interruptStatusRegisters.Any(e => e.Value == true));
        }

        private readonly List<IFlagRegisterField> interruptStatusRegisters = new();
        private readonly Dictionary<int, ReadFilePath> recoveryImages = [];
        private readonly Queue<uint> imageRawData = new();

        private enum DeviceStatus
        {
            StatusPending = 0x0,
            DeviceHealthy = 0x1,
            DeviceError = 0x2,
            RecoveryMode = 0x3,
            RecoveryPending = 0x4,
            RunningRecovery = 0x5,
            BootFailure = 0xE,
            FatalError = 0xF,
        }

        private enum DeviceRecoveryStatus
        {
            NotInRecoveryMode = 0x0,
            AwaitingRecoveryImage = 0x1,
            ProcessingRecoveryImage = 0x2,
            RecoverySuccessful = 0x3,
            RecoveryFailed = 0xC,
            RecoveryImageAuthenticationError = 0xD,
            ErrorEnteringRecoveryMode = 0xE,
            InvalidComponentAddressSpace = 0xF,
        }
    }
}
