using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class MH1903_DMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_DMA(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x400;

        public GPIO IRQ { get; set; } = new GPIO();

        private void DefineRegisters()
        {
            // DMA Channels 0-7 (each channel is 0x58 bytes = 22 DWORDs)
            for(int ch = 0; ch < 8; ch++)
            {
                var baseOffset = (Registers)((long)Registers.CH0_SAR_L + (ch * 0x58));

                // Source Address Register Low/High
                ((Registers)((long)baseOffset + 0x00)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sar_l");
                ((Registers)((long)baseOffset + 0x04)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sar_h");

                // Destination Address Register Low/High
                ((Registers)((long)baseOffset + 0x08)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dar_l");
                ((Registers)((long)baseOffset + 0x0C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dar_h");

                // Linked List Pointer Low/High
                ((Registers)((long)baseOffset + 0x10)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_llp_l");
                ((Registers)((long)baseOffset + 0x14)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_llp_h");

                // Control Register Low/High
                ((Registers)((long)baseOffset + 0x18)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_ctl_l");
                ((Registers)((long)baseOffset + 0x1C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_ctl_h");

                // Source Status Low/High
                ((Registers)((long)baseOffset + 0x20)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sstat_l");
                ((Registers)((long)baseOffset + 0x24)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sstat_h");

                // Destination Status Low/High
                ((Registers)((long)baseOffset + 0x28)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dstat_l");
                ((Registers)((long)baseOffset + 0x2C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dstat_h");

                // Source Status Address Low/High
                ((Registers)((long)baseOffset + 0x30)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sstatar_l");
                ((Registers)((long)baseOffset + 0x34)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sstatar_h");

                // Destination Status Address Low/High
                ((Registers)((long)baseOffset + 0x38)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dstatar_l");
                ((Registers)((long)baseOffset + 0x3C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dstatar_h");

                // Configuration Register Low/High
                ((Registers)((long)baseOffset + 0x40)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_cfg_l");
                ((Registers)((long)baseOffset + 0x44)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_cfg_h");

                // Source Gather Register Low/High
                ((Registers)((long)baseOffset + 0x48)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sgr_l");
                ((Registers)((long)baseOffset + 0x4C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_sgr_h");

                // Destination Scatter Register Low/High
                ((Registers)((long)baseOffset + 0x50)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dsr_l");
                ((Registers)((long)baseOffset + 0x54)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"ch{ch}_dsr_h");
            }

            // Interrupt Registers - Raw Status
            Registers.RAW_TFR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_tfr_l");
            Registers.RAW_TFR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_tfr_h");
            Registers.RAW_BLOCK_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_block_l");
            Registers.RAW_BLOCK_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_block_h");
            Registers.RAW_SRCTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_srctran_l");
            Registers.RAW_SRCTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_srctran_h");
            Registers.RAW_DSTTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_dsttran_l");
            Registers.RAW_DSTTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_dsttran_h");
            Registers.RAW_ERR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_err_l");
            Registers.RAW_ERR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "raw_err_h");

            // Interrupt Registers - Status
            Registers.STATUS_TFR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_tfr_l");
            Registers.STATUS_TFR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_tfr_h");
            Registers.STATUS_BLOCK_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_block_l");
            Registers.STATUS_BLOCK_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_block_h");
            Registers.STATUS_SRCTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_srctran_l");
            Registers.STATUS_SRCTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_srctran_h");
            Registers.STATUS_DSTTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_dsttran_l");
            Registers.STATUS_DSTTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_dsttran_h");
            Registers.STATUS_ERR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_err_l");
            Registers.STATUS_ERR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_err_h");

            // Interrupt Registers - Mask
            Registers.MASK_TFR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_tfr_l");
            Registers.MASK_TFR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_tfr_h");
            Registers.MASK_BLOCK_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_block_l");
            Registers.MASK_BLOCK_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_block_h");
            Registers.MASK_SRCTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_srctran_l");
            Registers.MASK_SRCTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_srctran_h");
            Registers.MASK_DSTTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_dsttran_l");
            Registers.MASK_DSTTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_dsttran_h");
            Registers.MASK_ERR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_err_l");
            Registers.MASK_ERR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "mask_err_h");

            // Interrupt Registers - Clear
            Registers.CLEAR_TFR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_tfr_l");
            Registers.CLEAR_TFR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_tfr_h");
            Registers.CLEAR_BLOCK_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_block_l");
            Registers.CLEAR_BLOCK_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_block_h");
            Registers.CLEAR_SRCTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_srctran_l");
            Registers.CLEAR_SRCTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_srctran_h");
            Registers.CLEAR_DSTTRAN_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_dsttran_l");
            Registers.CLEAR_DSTTRAN_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_dsttran_h");
            Registers.CLEAR_ERR_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_err_l");
            Registers.CLEAR_ERR_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "clear_err_h");

            // Combined Interrupt Status
            Registers.STATUS_INT_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_int_l");
            Registers.STATUS_INT_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "status_int_h");

            // Software handshaking
            Registers.REQ_SRCREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "req_srcreg_l");
            Registers.REQ_SRCREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "req_srcreg_h");
            Registers.REQ_DSTREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "req_dstreg_l");
            Registers.REQ_DSTREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "req_dstreg_h");
            Registers.SGL_REQ_SRCREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sgl_req_srcreg_l");
            Registers.SGL_REQ_SRCREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sgl_req_srcreg_h");
            Registers.SGL_REQ_DSTREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sgl_req_dstreg_l");
            Registers.SGL_REQ_DSTREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sgl_req_dstreg_h");
            Registers.LST_SRCREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "lst_srcreg_l");
            Registers.LST_SRCREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "lst_srcreg_h");
            Registers.LST_DSTREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "lst_dstreg_l");
            Registers.LST_DSTREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "lst_dstreg_h");

            // DMA Configuration registers
            Registers.DMA_CFGREG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dma_cfgreg_l");
            Registers.DMA_CFGREG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dma_cfgreg_h");
            Registers.CH_EN_REG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ch_en_reg_l");
            Registers.CH_EN_REG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ch_en_reg_h");
            Registers.DMA_ID_REG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "dma_id_reg_l");
            Registers.DMA_ID_REG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "dma_id_reg_h");
            Registers.DMA_TEST_REG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dma_test_reg_l");
            Registers.DMA_TEST_REG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dma_test_reg_h");

            // Reserved 0x3B8-0x3BF
            Registers.RESERVED_3B8.Define(this)
                .WithReservedBits(0, 32);
            Registers.RESERVED_3BC.Define(this)
                .WithReservedBits(0, 32);

            // Component parameter registers (read-only)
            Registers.COMP_PARAMS_6_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_6_l");
            Registers.COMP_PARAMS_6_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_6_h");
            Registers.COMP_PARAMS_5_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_5_l");
            Registers.COMP_PARAMS_5_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_5_h");
            Registers.COMP_PARAMS_4_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_4_l");
            Registers.COMP_PARAMS_4_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_4_h");
            Registers.COMP_PARAMS_3_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_3_l");
            Registers.COMP_PARAMS_3_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_3_h");
            Registers.COMP_PARAMS_2_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_2_l");
            Registers.COMP_PARAMS_2_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_2_h");
            Registers.COMP_PARAMS_1_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_1_l");
            Registers.COMP_PARAMS_1_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "comp_params_1_h");
            Registers.COMPONENT_ID_REG_L.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "component_id_reg_l");
            Registers.COMPONENT_ID_REG_H.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "component_id_reg_h");
        }

        private enum Registers : long
        {
            // Channel 0 registers (0x000 - 0x057)
            CH0_SAR_L = 0x000,
            CH0_SAR_H = 0x004,
            CH0_DAR_L = 0x008,
            CH0_DAR_H = 0x00C,
            CH0_LLP_L = 0x010,
            CH0_LLP_H = 0x014,
            CH0_CTL_L = 0x018,
            CH0_CTL_H = 0x01C,
            CH0_SSTAT_L = 0x020,
            CH0_SSTAT_H = 0x024,
            CH0_DSTAT_L = 0x028,
            CH0_DSTAT_H = 0x02C,
            CH0_SSTATAR_L = 0x030,
            CH0_SSTATAR_H = 0x034,
            CH0_DSTATAR_L = 0x038,
            CH0_DSTATAR_H = 0x03C,
            CH0_CFG_L = 0x040,
            CH0_CFG_H = 0x044,
            CH0_SGR_L = 0x048,
            CH0_SGR_H = 0x04C,
            CH0_DSR_L = 0x050,
            CH0_DSR_H = 0x054,

            // Channels 1-7 follow same pattern at offsets 0x58, 0xB0, 0x108, 0x160, 0x1B8, 0x210, 0x268

            // Interrupt raw status registers (0x2C0)
            RAW_TFR_L = 0x2C0,
            RAW_TFR_H = 0x2C4,
            RAW_BLOCK_L = 0x2C8,
            RAW_BLOCK_H = 0x2CC,
            RAW_SRCTRAN_L = 0x2D0,
            RAW_SRCTRAN_H = 0x2D4,
            RAW_DSTTRAN_L = 0x2D8,
            RAW_DSTTRAN_H = 0x2DC,
            RAW_ERR_L = 0x2E0,
            RAW_ERR_H = 0x2E4,

            // Interrupt status registers (0x2E8)
            STATUS_TFR_L = 0x2E8,
            STATUS_TFR_H = 0x2EC,
            STATUS_BLOCK_L = 0x2F0,
            STATUS_BLOCK_H = 0x2F4,
            STATUS_SRCTRAN_L = 0x2F8,
            STATUS_SRCTRAN_H = 0x2FC,
            STATUS_DSTTRAN_L = 0x300,
            STATUS_DSTTRAN_H = 0x304,
            STATUS_ERR_L = 0x308,
            STATUS_ERR_H = 0x30C,

            // Interrupt mask registers (0x310)
            MASK_TFR_L = 0x310,
            MASK_TFR_H = 0x314,
            MASK_BLOCK_L = 0x318,
            MASK_BLOCK_H = 0x31C,
            MASK_SRCTRAN_L = 0x320,
            MASK_SRCTRAN_H = 0x324,
            MASK_DSTTRAN_L = 0x328,
            MASK_DSTTRAN_H = 0x32C,
            MASK_ERR_L = 0x330,
            MASK_ERR_H = 0x334,

            // Interrupt clear registers (0x338)
            CLEAR_TFR_L = 0x338,
            CLEAR_TFR_H = 0x33C,
            CLEAR_BLOCK_L = 0x340,
            CLEAR_BLOCK_H = 0x344,
            CLEAR_SRCTRAN_L = 0x348,
            CLEAR_SRCTRAN_H = 0x34C,
            CLEAR_DSTTRAN_L = 0x350,
            CLEAR_DSTTRAN_H = 0x354,
            CLEAR_ERR_L = 0x358,
            CLEAR_ERR_H = 0x35C,

            // Combined interrupt status (0x360)
            STATUS_INT_L = 0x360,
            STATUS_INT_H = 0x364,

            // Software handshaking (0x368)
            REQ_SRCREG_L = 0x368,
            REQ_SRCREG_H = 0x36C,
            REQ_DSTREG_L = 0x370,
            REQ_DSTREG_H = 0x374,
            SGL_REQ_SRCREG_L = 0x378,
            SGL_REQ_SRCREG_H = 0x37C,
            SGL_REQ_DSTREG_L = 0x380,
            SGL_REQ_DSTREG_H = 0x384,
            LST_SRCREG_L = 0x388,
            LST_SRCREG_H = 0x38C,
            LST_DSTREG_L = 0x390,
            LST_DSTREG_H = 0x394,

            // DMA Configuration (0x398)
            DMA_CFGREG_L = 0x398,
            DMA_CFGREG_H = 0x39C,
            CH_EN_REG_L = 0x3A0,
            CH_EN_REG_H = 0x3A4,
            DMA_ID_REG_L = 0x3A8,
            DMA_ID_REG_H = 0x3AC,
            DMA_TEST_REG_L = 0x3B0,
            DMA_TEST_REG_H = 0x3B4,

            // Reserved (0x3B8)
            RESERVED_3B8 = 0x3B8,
            RESERVED_3BC = 0x3BC,

            // Component parameters (0x3C0)
            COMP_PARAMS_6_L = 0x3C0,
            COMP_PARAMS_6_H = 0x3C4,
            COMP_PARAMS_5_L = 0x3C8,
            COMP_PARAMS_5_H = 0x3CC,
            COMP_PARAMS_4_L = 0x3D0,
            COMP_PARAMS_4_H = 0x3D4,
            COMP_PARAMS_3_L = 0x3D8,
            COMP_PARAMS_3_H = 0x3DC,
            COMP_PARAMS_2_L = 0x3E0,
            COMP_PARAMS_2_H = 0x3E4,
            COMP_PARAMS_1_L = 0x3E8,
            COMP_PARAMS_1_H = 0x3EC,
            COMPONENT_ID_REG_L = 0x3F0,
            COMPONENT_ID_REG_H = 0x3F4,
        }
    }
}
