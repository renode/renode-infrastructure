//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I3C
{
    partial class Chipsalliance_I3C : IKnownSize
    {
        public long Size => 0x1000;

        partial void Init()
        {
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
