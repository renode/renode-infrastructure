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
            I3cBase_HciVersion.VERSION.ValueProviderCallback = _ => 0x120;
            I3cBase_DctSectionOffset.TABLE_OFFSET.ValueProviderCallback = _ => 0x800;
            I3cBase_DctSectionOffset.TABLE_SIZE.ValueProviderCallback = _ => 0x7F;
            I3cBase_PioSectionOffset.SECTION_OFFSET.ValueProviderCallback = _ => 0x80;
            I3cBase_ExtCapsSectionOffset.SECTION_OFFSET.ValueProviderCallback = _ => 0x100;
            I3cBase_IntCtrlCmdsEn.ICC_SUPPORT.ValueProviderCallback = _ => true;
            I3cBase_IntCtrlCmdsEn.MIPI_CMDS_SUPPORTED.ValueProviderCallback = _ => 0x35;
            PiOControl_QueueSize.CR_QUEUE_SIZE.ValueProviderCallback = _ => 0x40;
            PiOControl_AltQueueSize.ALT_RESP_QUEUE_SIZE.ValueProviderCallback = _ => 0x40;
            PiOControl_AltQueueSize.ALT_RESP_QUEUE_EN.ValueProviderCallback = _ => false;
            PiOControl_AltQueueSize.EXT_IBI_QUEUE_EN.ValueProviderCallback = _ => false;

            CreateInterruptForceCallback(
                I3cBase_IntrForce.HC_INTERNAL_ERR_FORCE,
                I3cBase_IntrStatus.HC_INTERNAL_ERR_STAT,
                I3cBase_IntrStatusEnable.HC_INTERNAL_ERR_STAT_EN);

            CreateInterruptForceCallback(
                I3cBase_IntrForce.HC_SEQ_CANCEL_FORCE,
                I3cBase_IntrStatus.HC_SEQ_CANCEL_STAT,
                I3cBase_IntrStatusEnable.HC_SEQ_CANCEL_STAT_EN);

            CreateInterruptForceCallback(
                I3cBase_IntrForce.HC_WARN_CMD_SEQ_STALL_FORCE,
                I3cBase_IntrStatus.HC_WARN_CMD_SEQ_STALL_STAT,
                I3cBase_IntrStatusEnable.HC_WARN_CMD_SEQ_STALL_STAT_EN);

            CreateInterruptForceCallback(
                I3cBase_IntrForce.HC_ERR_CMD_SEQ_TIMEOUT_FORCE,
                I3cBase_IntrStatus.HC_ERR_CMD_SEQ_TIMEOUT_STAT,
                I3cBase_IntrStatusEnable.HC_ERR_CMD_SEQ_TIMEOUT_STAT_EN);

            CreateInterruptForceCallback(
                I3cBase_IntrForce.SCHED_CMD_MISSED_TICK_FORCE,
                I3cBase_IntrStatus.SCHED_CMD_MISSED_TICK_STAT,
                I3cBase_IntrStatusEnable.SCHED_CMD_MISSED_TICK_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.RX_DESC_STAT_FORCE,
                I3cEc_Tti_InterruptStatus.RX_DESC_STAT,
                I3cEc_Tti_InterruptEnable.RX_DESC_STAT_EN);

            CreateInterruptForceCallback(
                I3cEc_Tti_InterruptForce.TX_DESC_STAT_FORCE,
                I3cEc_Tti_InterruptStatus.TX_DESC_STAT,
                I3cEc_Tti_InterruptEnable.TX_DESC_THLD_STAT_EN);

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
