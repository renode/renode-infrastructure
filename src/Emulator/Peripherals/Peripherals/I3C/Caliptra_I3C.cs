//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I3C
{
    partial class Caliptra_I3C : IKnownSize
    {
        public long Size => 0x1000;

        partial void Init()
        {
            HciVersion.VERSION.ValueProviderCallback = _ => 0x120;
            DctSectionOffset.TABLE_OFFSET.ValueProviderCallback = _ => 0x800;
            DctSectionOffset.TABLE_SIZE.ValueProviderCallback = _ => 0x7F;
            PioSectionOffset.SECTION_OFFSET.ValueProviderCallback = _ => 0x80;
            ExtCapsSectionOffset.SECTION_OFFSET.ValueProviderCallback = _ => 0x100;
            IntCtrlCmdsEn.ICC_SUPPORT.ValueProviderCallback = _ => true;
            IntCtrlCmdsEn.MIPI_CMDS_SUPPORTED.ValueProviderCallback = _ => 0x35;
            PIOQueueSize.CR_QUEUE_SIZE.ValueProviderCallback = _ => 0x40;
            AltQueueSize.ALT_RESP_QUEUE_SIZE.ValueProviderCallback = _ => 0x40;
            AltQueueSize.ALT_RESP_QUEUE_EN.ValueProviderCallback = _ => false;
            AltQueueSize.EXT_IBI_QUEUE_EN.ValueProviderCallback = _ => false;

            CreateInterruptForceCallback(
                IntrForce.HC_INTERNAL_ERR_FORCE,
                IntrStatus.HC_INTERNAL_ERR_STAT,
                IntrStatusEnable.HC_INTERNAL_ERR_STAT_EN);

            CreateInterruptForceCallback(
                IntrForce.HC_SEQ_CANCEL_FORCE,
                IntrStatus.HC_SEQ_CANCEL_STAT,
                IntrStatusEnable.HC_SEQ_CANCEL_STAT_EN);

            CreateInterruptForceCallback(
                IntrForce.HC_WARN_CMD_SEQ_STALL_FORCE,
                IntrStatus.HC_WARN_CMD_SEQ_STALL_STAT,
                IntrStatusEnable.HC_WARN_CMD_SEQ_STALL_STAT_EN);

            CreateInterruptForceCallback(
                IntrForce.HC_ERR_CMD_SEQ_TIMEOUT_FORCE,
                IntrStatus.HC_ERR_CMD_SEQ_TIMEOUT_STAT,
                IntrStatusEnable.HC_ERR_CMD_SEQ_TIMEOUT_STAT_EN);

            CreateInterruptForceCallback(
                IntrForce.SCHED_CMD_MISSED_TICK_FORCE,
                IntrStatus.SCHED_CMD_MISSED_TICK_STAT,
                IntrStatusEnable.SCHED_CMD_MISSED_TICK_STAT_EN);

            CreateInterruptForceCallback(
                InterruptForce.RX_DESC_STAT_FORCE,
                InterruptStatus.RX_DESC_STAT,
                InterruptEnable.RX_DESC_STAT_EN);

            CreateInterruptForceCallback(
                InterruptForce.TX_DESC_STAT_FORCE,
                InterruptStatus.TX_DESC_STAT,
                InterruptEnable.TX_DESC_THLD_STAT_EN);

            CreateInterruptForceCallback(
                InterruptForce.RX_DATA_THLD_FORCE,
                InterruptStatus.RX_DATA_THLD_STAT,
                InterruptEnable.RX_DATA_THLD_STAT_EN);

            CreateInterruptForceCallback(
                InterruptForce.RX_DESC_THLD_FORCE,
                InterruptStatus.RX_DESC_THLD_STAT,
                InterruptEnable.RX_DESC_THLD_STAT_EN);

            CreateInterruptForceCallback(
                InterruptForce.IBI_DONE_FORCE,
                InterruptStatus.IBI_DONE,
                InterruptEnable.IBI_DONE_EN);
        }

        private void CreateInterruptForceCallback(IFlagRegisterField force, IFlagRegisterField status, IFlagRegisterField enabled)
        {
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
        }
    }
}
