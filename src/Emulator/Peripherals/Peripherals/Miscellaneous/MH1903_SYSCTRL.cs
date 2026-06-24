using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_SYSCTRL : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_SYSCTRL(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 1020;

        public override uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        private void DefineRegisters()
        {
            Registers.FreqSel.Define(this, resetValue: 0x200D395A)
                .WithFlag(0, name: "pclk_freq_sel")
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "hclk_freq_sel")
                .WithReservedBits(5, 3)
                .WithValueField(8, 2, name: "cpu_freq_sel")
                .WithReservedBits(10, 2)
                .WithFlag(12, name: "ck12m_src")
                .WithFlag(13, name: "sys_ck_src")
                .WithReservedBits(14, 2)
                .WithValueField(16, 5, name: "xtal_sel")
                .WithReservedBits(21, 3)
                .WithValueField(24, 3, name: "power_mode")
                .WithReservedBits(27, 2)
                .WithValueField(29, 3, name: "pix_clk_freq");

            Registers.CgCtrl1.Define(this, resetValue: 0x04100000)
                .WithFlag(0, name: "uart2_cg_en")
                .WithFlag(1, name: "uart2_cg_en")
                .WithFlag(2, name: "uart2_cg_en")
                .WithFlag(3, name: "uart3_cg_en")
                .WithReservedBits(4, 4)

                .WithFlag(8, name: "spi0_cg_en")
                .WithFlag(9, name: "spi1_cg_en")
                .WithFlag(10, name: "spi2_cg_en")
                .WithReservedBits(11, 2)
                .WithFlag(13, name: "spi5_cg_en")
                .WithFlag(14, name: "sci0_cg_en")
                .WithReservedBits(15, 1)

                .WithFlag(16, name: "sci2_cg_en")
                .WithReservedBits(17, 1)
                .WithFlag(18, name: "i2c0_cg_en")
                .WithReservedBits(19, 1)
                .WithFlag(20, name: "gpio_cg_en")
                .WithFlag(21, name: "timer_cg_en")
                .WithFlag(22, name: "csi2_cg_en")
                .WithFlag(23, name: "dcmis_cg_en")

                .WithReservedBits(24, 2)
                .WithFlag(26, name: "bpu_cg_en")
                .WithFlag(27, name: "kbd_cg_en")
                .WithReservedBits(28, 1)
                .WithFlag(29, name: "crc_cg_en")
                .WithFlag(30, name: "adc_cg_en")
                .WithFlag(31, name: "trng_cg_en");

            Registers.CgCtrl2.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "crypt_cg_en")
                .WithFlag(1, name: "lcd_cg_en")
                .WithFlag(2, name: "gpu_cg_en")
                .WithFlag(3, name: "otp_cg_en")
                .WithReservedBits(4, 1)
                .WithFlag(5, name: "qr_cg_en")
                .WithReservedBits(6, 2)
                .WithReservedBits(8, 8)
                .WithReservedBits(16, 8)
                .WithReservedBits(24, 4)
                .WithFlag(28, name: "usb_cg_en")
                .WithFlag(29, name: "dma_cg_en")
                .WithReservedBits(30, 2);

            Registers.SoftRst1.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "srst_uart0")
                .WithFlag(1, name: "srst_uart1")
                .WithFlag(2, name: "srst_uart2")
                .WithFlag(3, name: "srst_uart3")
                .WithReservedBits(4, 3)
                .WithFlag(8, name: "srst_spi0")
                .WithFlag(9, name: "srst_spi1")
                .WithFlag(10, name: "srst_spi2")
                .WithReservedBits(11, 2)
                .WithFlag(13, name: "srst_spi5")
                .WithFlag(14, name: "srst_sci0")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "srst_sci2")
                .WithReservedBits(17, 1)
                .WithFlag(18, name: "srst_i2c0")
                .WithReservedBits(19, 1)
                .WithFlag(20, name: "srst_gpio")
                .WithFlag(21, name: "srst_timer0")
                .WithReservedBits(22, 1)
                .WithFlag(23, name: "srst_dcmis")
                .WithReservedBits(24, 3)
                .WithFlag(27, name: "srst_kbd")
                .WithReservedBits(28, 1)
                .WithFlag(29, name: "srst_crc")
                .WithFlag(30, name: "srst_adc")
                .WithFlag(31, name: "srst_trng");

            Registers.SoftRst2.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "srst_crypt")
                .WithFlag(1, name: "srst_lcd")
                .WithFlag(2, name: "srst_gpu")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "srst_qr")
                .WithReservedBits(6, 18)
                .WithReservedBits(24, 3)
                .WithFlag(27, name: "srst_cache")
                .WithFlag(28, name: "srst_usb")
                .WithFlag(29, name: "srst_dma")
                .WithFlag(30, name: "srst_cm3")
                .WithFlag(31, name: "srst_glb")
                .WithWriteCallback((offset, value) => OnSoftRst2Write(offset, value));

            Registers.LockR.Define(this, resetValue: 0xF0000000)
                .WithReservedBits(0, 28)
                .WithFlag(28, name: "srst_usb_lock")
                .WithFlag(29, name: "srst_dma_lock")
                .WithFlag(30, name: "srst_cm3_lock")
                .WithFlag(31, name: "srst_glb_lock");

            Registers.PherCtrl.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 16)
                .WithFlag(16, name: "sci0_cdet_inv")
                .WithReservedBits(17, 1)
                .WithFlag(18, name: "sci2_cdet_inv")
                .WithReservedBits(19, 1)
                .WithFlag(20, name: "sci0_vccen_inv")
                .WithReservedBits(21, 1)
                .WithFlag(22, name: "sci2_vccen_inv")
                .WithReservedBits(23, 1)
                .WithFlag(24, name: "spi0_slv_sel")
                .WithReservedBits(25, 7);

            Registers.Hclk1MsVal.Define(this, resetValue: 0x0000BB80)
                .WithValueField(0, 17, mode: FieldMode.Read, name: "val_1ms_hclk", valueProviderCallback: _ => 0xBB80)
                .WithReservedBits(17, 15);
            Registers.Pclk1MsVal.Define(this, resetValue: 0x00005DC0)
                .WithValueField(0, 17, mode: FieldMode.Read, name: "val_1ms_hclk", valueProviderCallback: _ => 0x5DC0)
                .WithReservedBits(17, 15);
            Registers.AnaCtrl.Define(this, resetValue: 0x0002C601)
                .WithReservedBits(0, 4)
                .WithFlag(4, name: "usb12_pd_en")
                .WithFlag(5, name: "usb33_pd_en")
                .WithReservedBits(6, 1)
                .WithFlag(7, name: "rom_pd_en")
                .WithReservedBits(8, 24);

            Registers.DmaChan.Define(this, resetValue: 0x0A0B0203)
                .WithValueField(0, 4, name: "dma_ch0_if")
                .WithReservedBits(4, 4)
                .WithValueField(8, 4, name: "dma_ch1_if")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, name: "dma_ch2_if")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, name: "dma_ch3_if")
                .WithReservedBits(28, 4);

            Registers.Sci0Glf.Define(this, resetValue: 0x20060000)
                .WithValueField(0, 20, name: "card_normal_glf")
                .WithReservedBits(20, 9)
                .WithFlag(29, name: "card_normal_bypass")
                .WithFlag(30, name: "card_normal_glf_bypass")
                .WithReservedBits(31, 1);

            Registers.CardRsvd.Define(this, resetValue: 0x00000000);

            Registers.Ldo25Cr.Define(this, resetValue: 0x00000028)
                .WithReservedBits(0, 4)
                .WithFlag(4, name: "ldo25_pd_en")
                .WithFlag(5, name: "otp_pd_en")
                .WithReservedBits(6, 26);

            Registers.DmaChan1.Define(this, resetValue: 0x0A0B0203)
                .WithValueField(0, 4, name: "dma_ch4_if")
                .WithReservedBits(4, 4)
                .WithValueField(8, 4, name: "dma_ch5_if")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, name: "dma_ch6_if")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, name: "dma_ch7_if")
                .WithReservedBits(28, 4);

            Registers.MsrCr1.Define(this, resetValue: 0x07F88800)
                .WithReservedBits(0, 26)
                .WithFlag(27, name: "pd_msr")
                .WithReservedBits(28, 4);

            Registers.MsrCr2.Define(this, resetValue: 0x0000000C)
                .WithReservedBits(0, 32);

            Registers.UsbPhyCr1.Define(this, resetValue: 0x204921AE)
                .WithReservedBits(0, 28)
                .WithFlag(29, name: "stop_ck_for_suspend")
                .WithReservedBits(30, 2);

            Registers.UsbPhyCr2.Define(this, resetValue: 0x40000000)
                .WithReservedBits(0, 32);

            Registers.UsbPhyCr3.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "idpullup")
                .WithFlag(1, name: "iddig")
                .WithReservedBits(2, 29);

            Registers.Iso7816Cr.Define(this, resetValue: 0x00000080)
                .WithReservedBits(0, 6)
                .WithValueField(6, 2, name: "7816_vsel")
                .WithReservedBits(8, 24);

            Registers.LdoCr.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);
            Registers.ChgCsr.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 28)
                .WithValueField(28, 2, name: "chg_state")
                .WithReservedBits(30, 2);

            Registers.Pm2WkFlag.Define(this, resetValue: 0x00000000);

            Registers.CalibCsr.Define(this, resetValue: 0xAB000080)
                .WithReservedBits(0, 8)
                .WithFlag(8, name: "osc12m_cal_en")
                .WithFlag(9, name: "osc12m_cal_done")
                .WithFlag(10, name: "osc12m_usb_cal_en")
                .WithReservedBits(11, 4)
                .WithFlag(15, name: "32k_cal_sel")
                .WithReservedBits(16, 16);

            Registers.DbgCr.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);

            Registers.ChipId.Define(this, resetValue: 0x03070000)
                .WithValueField(0, 32, name: "chip_id", mode: FieldMode.Read);
        }

        private void OnSoftRst2Write(uint offset, uint value)
        {
            // Check if global soft reset bit (bit 31) is set
            bool globalResetRequested = (value & 0x80000000) != 0;

            if(globalResetRequested)
            {
                // Read LOCK_R register to check if global reset is locked
                var lockRegister = RegistersCollection.Read((long)Registers.LockR);
                bool globalResetLocked = (lockRegister & 0x80000000) != 0;

                if(globalResetLocked)
                {
                    this.Log(LogLevel.Warning, "Global soft reset requested but locked (LOCK_R bit 31 is set)");
                }
                else
                {
                    this.Log(LogLevel.Info, "Global soft reset triggered via SOFT_RST2");
                    machine.RequestReset();
                }
            }

            // Check if CPU reset bit (bit 30) is set
            bool cpuResetRequested = (value & 0x40000000) != 0;

            if(cpuResetRequested)
            {
                var lockRegister = RegistersCollection.Read((long)Registers.LockR);
                bool cpuResetLocked = (lockRegister & 0x40000000) != 0;

                if(cpuResetLocked)
                {
                    this.Log(LogLevel.Warning, "CPU soft reset requested but locked (LOCK_R bit 30 is set)");
                }
                else
                {
                    this.Log(LogLevel.Info, "CPU soft reset triggered via SOFT_RST2");
                    machine.RequestReset();
                }
            }
        }

        private enum Registers : long
        {
            FreqSel = 0x0,
            CgCtrl1 = 0x4,
            CgCtrl2 = 0x8,
            SoftRst1 = 0xC,
            SoftRst2 = 0x10,
            LockR = 0x14,
            PherCtrl = 0x18,

            Hclk1MsVal = 0x2C,
            Pclk1MsVal = 0x30,
            AnaCtrl = 0x34,
            DmaChan = 0x38,
            Sci0Glf = 0x3C,

            CardRsvd = 0x48,
            Ldo25Cr = 0x4C,
            DmaChan1 = 0x50,

            MsrCr1 = 0x100,
            MsrCr2 = 0x104,
            UsbPhyCr1 = 0x108,
            UsbPhyCr2 = 0x10C,
            UsbPhyCr3 = 0x110,
            Iso7816Cr = 0x114,
            LdoCr = 0x118,
            ChgCsr = 0x11c,

            Pm2WkFlag = 0x3ec,
            CalibCsr = 0x3f0,
            DbgCr = 0x3f4,
            ChipId = 0x3f8,
        }
    }
}