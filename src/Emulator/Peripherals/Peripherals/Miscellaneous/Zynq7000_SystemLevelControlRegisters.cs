//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class Zynq7000_SystemLevelControlRegisters : BasicDoubleWordPeripheral, IKnownSize
    {
        public Zynq7000_SystemLevelControlRegisters(IMachine machine, BaseCPU cpu0, BaseCPU cpu1 = null) : base(machine)
        {
            cpuControls = new List<CPUControl>
            {
                new CPUControl(cpu0),
                new CPUControl(cpu1)
            };
            BuildRegisters();
            Initialize();
        }

        public override void Reset()
        {
            base.Reset();
            Initialize();
        }

        public long Size => 0x1000;

        private void BuildRegisters()
        {
            Registers.ArmPLLControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x0001A008);
            Registers.DDRPLLControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x0001A008);
            Registers.IOPLLControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x0001A008);
            Registers.PLLStatus.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x0000003F);
            Registers.CPUClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x1F000400);
            Registers.DDRClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x18400003);
            Registers.AMBAPeripheralClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x01FFCCCD);
            Registers.SDIORefClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x00001E03);
            Registers.GigE0RefClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x00003C01);
            Registers.CPUClockRatioModeSelect.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 1);
            Registers.UARTRefClockControl.Define(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => 0x3F03);

            Registers.WriteProtectionLock.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, FieldMode.Write, name: "LockKey",
                    writeCallback: (_, val) => ChangeWriteProtection((uint)val, lockKey, true, "lock")
                );
            Registers.WriteProtectionUnlock.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, FieldMode.Write, name: "UnlockKey",
                    writeCallback: (_, val) => ChangeWriteProtection((uint)val, unlockKey, false, "unlock")
                );
            Registers.WriteProtectionStatus.Define(this)
                .WithReservedBits(1, 31)
                .WithFlag(0, FieldMode.Read, name: "WriteProtected", valueProviderCallback: (_) => writeProtected);

            Registers.CPUResetAndClockControl.Define(this)
                .WithReservedBits(9, 23)
                .WithTaggedFlag("CPUPeripheralSoftReset", 8)
                .WithReservedBits(6, 2)
                .WithFlag(5, name: "CPU1ClockStop",
                    writeCallback: (_, val) => { if(!writeProtected) cpuControls[1].IsStopped = val; },
                    valueProviderCallback: (_) => cpuControls[1].IsStopped
                )
                .WithFlag(4, name: "CPU0ClockStop",
                    writeCallback: (_, val) => { if(!writeProtected) cpuControls[0].IsStopped = val; },
                    valueProviderCallback: (_) => cpuControls[0].IsStopped
                )
                .WithReservedBits(2, 2)
                .WithFlag(1, name: "CPU1Reset",
                    writeCallback: (prevVal, val) => { if(!writeProtected) cpuControls[1].InReset = val; },
                    valueProviderCallback: (_) => cpuControls[1].InReset
                )
                .WithFlag(0, name: "CPU0Reset",
                    writeCallback: (prevVal, val) => { if(!writeProtected) cpuControls[0].InReset = val; },
                    valueProviderCallback: (_) => cpuControls[0].InReset
                )
                .WithWriteCallback((prevVal, val) =>
                    {
                        CheckWriteProtection(prevVal, val);
                        foreach(var control in cpuControls)
                        {
                            control.UpdateState();
                        }
                    });
        }

        private void ChangeWriteProtection(uint value, uint expectedValue, bool requestedProtection, string keyName)
        {
            if(value == expectedValue)
            {
                writeProtected = requestedProtection;
                return;
            }
            this.Log(LogLevel.Warning, "Invalid {0} key: 0x{1:x}", keyName, value);
        }

        private void CheckWriteProtection(ulong previousValue, ulong value)
        {
            if(previousValue != value && writeProtected)
            {
                this.Log(LogLevel.Warning, "Trying to change a register value while a write protection is on.");
            }
        }

        private void Initialize()
        {
            writeProtected = true;
            foreach(var control in cpuControls)
            {
                control.ResetState();
            }
        }

        private bool writeProtected;
        private readonly IReadOnlyList<CPUControl> cpuControls;

        private const uint lockKey = 0x767B;
        private const uint unlockKey = 0xDF0D;

        private class CPUControl
        {
            public CPUControl(BaseCPU cpu)
            {
                this.cpu = cpu;
            }

            public void ResetState()
            {
                stopRequested = false;
            }

            public void UpdateState()
            {
                if(cpu == null)
                {
                    return;
                }
                cpu.IsHalted = stopRequested || InReset;
                if(InReset)
                {
                    cpu.Reset();
                    cpu.Resume();
                }
            }

            public bool IsStopped
            {
                get => cpu == null || stopRequested || cpu.IsHalted;
                set => stopRequested = value;
            }

            public bool InReset { get; set; }

            private readonly BaseCPU cpu;
            private bool stopRequested;
        }

        private enum Registers : long
        {
            SecureConfigurationLock = 0x000, // SCL
            WriteProtectionLock = 0x004, // SLCR_LOCK
            WriteProtectionUnlock = 0x008, // SLCR_UNLOCK
            WriteProtectionStatus = 0x00C, // SLCR_LOCKSTA
            ArmPLLControl = 0x100, // ARM_PLL_CTRL
            DDRPLLControl = 0x104, // DDR_PLL_CTRL
            IOPLLControl = 0x108, // IO_PLL_CTRL
            PLLStatus = 0x10C, // PLL_STATUS
            ArmPLLConfiguration = 0x110, // ARM_PLL_CFG
            DDRPLLConfiguration = 0x114, // DDR_PLL_CFG
            IOPLLConfiguration = 0x118, // IO_PLL_CFG
            CPUClockControl = 0x120, // ARM_CLK_CTRL
            DDRClockControl = 0x124, // DDR_CLK_CTRL
            DCIClockControl = 0x128, // DCI_CLK_CTRL
            AMBAPeripheralClockControl = 0x12C, // APER_CLK_CTRL
            USB0ULPIClockControl = 0x130, // USB0_CLK_CTRL
            USB1ULPIClockControl = 0x134, // USB1_CLK_CTRL
            GigE0RxClockAndRxSignalsSelect = 0x138, // GEM0_RCLK_CTRL
            GigE1RxClockAndRxSignalsSelect = 0x13C, // GEM1_RCLK_CTRL
            GigE0RefClockControl = 0x140, // GEM0_CLK_CTRL
            GigE1RefClockControl = 0x144, // GEM1_CLK_CTRL
            SMCRefClockControl = 0x148, // SMC_CLK_CTRL
            QuadSPIRefClockControl = 0x14C, // LQSPI_CLK_CTRL
            SDIORefClockControl = 0x150, // SDIO_CLK_CTRL
            UARTRefClockControl = 0x154, // UART_CLK_CTRL
            SPIRefClockControl = 0x158, // SPI_CLK_CTRL
            CANRefClockControl = 0x15C, // CAN_CLK_CTRL
            CANMIOClockControl = 0x160, // CAN_MIOCLK_CTRL
            SoCDebugClockControl = 0x164, // DBG_CLK_CTRL
            PCAPClockControl = 0x168, // PCAP_CLK_CTRL
            CentralInterconnectClockControl = 0x16C, // TOPSW_CLK_CTRL
            PLClock0OutputControl = 0x170, // FPGA0_CLK_CTRL
            PLClock0ThrottleControl = 0x174, // FPGA0_THR_CTRL
            PLClock0ThrottleCountControl = 0x178, // FPGA0_THR_CNT
            PLClock0ThrottleStatusRead = 0x17C, // FPGA0_THR_STA
            PLClock1OutputControl = 0x180, // FPGA1_CLK_CTRL
            PLClock1ThrottleControl = 0x184, // FPGA1_THR_CTRL
            PLClock1ThrottleCount = 0x188, // FPGA1_THR_CNT
            PLClock1ThrottleStatusControl = 0x18C, // FPGA1_THR_STA
            PLClock2OutputControl = 0x190, // FPGA2_CLK_CTRL
            PLClock2ThrottleControl = 0x194, // FPGA2_THR_CTRL
            PLClock2ThrottleCount = 0x198, // FPGA2_THR_CNT
            PLClock2ThrottleStatus = 0x19C, // FPGA2_THR_STA
            PLClock3OutputControl = 0x1A0, // FPGA3_CLK_CTRL
            PLClock3ThrottleControl = 0x1A4, // FPGA3_THR_CTRL
            PLClock3ThrottleCount = 0x1A8, // FPGA3_THR_CNT
            PLClock3ThrottleStatus = 0x1AC, // FPGA3_THR_STA
            CPUClockRatioModeSelect = 0x1C4, // CLK_621_TRUE
            PSSoftwareResetControl = 0x200, // PSS_RST_CTRL
            DDRSoftwareResetControl = 0x204, // DDR_RST_CTRL
            CentralInterconnectResetControl = 0x208, // TOPSW_RST_CTRL
            DMACSoftwareResetControl = 0x20C, // DMAC_RST_CTRL
            USBSoftwareResetControl = 0x210, // USB_RST_CTRL
            GigabitEthernetSWResetControl = 0x214, // GEM_RST_CTRL
            SDIOSoftwareResetControl = 0x218, // SDIO_RST_CTRL
            SPISoftwareResetControl = 0x21C, // SPI_RST_CTRL
            CANSoftwareResetControl = 0x220, // CAN_RST_CTRL
            I2CSoftwareResetControl = 0x224, // I2C_RST_CTRL
            UARTSoftwareResetControl = 0x228, // UART_RST_CTRL
            GPIOSoftwareResetControl = 0x22C, // GPIO_RST_CTRL
            QuadSPISoftwareResetControl = 0x230, // LQSPI_RST_CTRL
            SMCSoftwareResetControl = 0x234, // SMC_RST_CTRL
            OCMSoftwareResetControl = 0x238, // OCM_RST_CTRL
            FPGASoftwareResetControl = 0x240, // FPGA_RST_CTRL
            CPUResetAndClockControl = 0x244, // A9_CPU_RST_CTRL
            WatchdogTimerResetControl = 0x24C, // RS_AWDT_CTRL
            RebootStatus = 0x258, Persistent, // REBOOT_STATUS
            BootModeStrappingPins = 0x25C, // BOOT_MODE
            APUControl = 0x300, // APU_CTRL
            SWDTClockSourceSelect = 0x304, // WDT_CLK_SEL
            DMACTrustZoneConfig = 0x440, // TZ_DMA_NS
            DMACTrustZoneConfigForInterrupts = 0x444, // TZ_DMA_IRQ_NS
            DMACTrustZoneConfigForPeripherals = 0x448, // TZ_DMA_PERIPH_NS
            PSIDCODE = 0x530, // PSS_IDCODE
            DDRUrgentControl = 0x600, // DDR_URGENT
            DDRCalibrationStartTriggers = 0x60C, // DDR_CAL_START
            DDRRefreshStartTriggers = 0x614, // DDR_REF_START
            DDRCommandStoreStatus = 0x618, // DDR_CMD_STA
            DDRUrgentSelect = 0x61C, // DDR_URGENT_SEL
            DDRDFIStatus = 0x620, // DDR_DFI_STATUS
            MIOPin0Control = 0x700, // MIO_PIN_00
            MIOPin1Control = 0x704, // MIO_PIN_01
            MIOPin2Control = 0x708, // MIO_PIN_02
            MIOPin3Control = 0x70C, // MIO_PIN_03
            MIOPin4Control = 0x710, // MIO_PIN_04
            MIOPin5Control = 0x714, // MIO_PIN_05
            MIOPin6Control = 0x718, // MIO_PIN_06
            MIOPin7Control = 0x71C, // MIO_PIN_07
            MIOPin8Control = 0x720, // MIO_PIN_08
            MIOPin9Control = 0x724, // MIO_PIN_09
            MIOPin10Control = 0x728, // MIO_PIN_10
            MIOPin11Control = 0x72C, // MIO_PIN_11
            MIOPin12Control = 0x730, // MIO_PIN_12
            MIOPin13Control = 0x734, // MIO_PIN_13
            MIOPin14Control = 0x738, // MIO_PIN_14
            MIOPin15Control = 0x73C, // MIO_PIN_15
            MIOPin16Control = 0x740, // MIO_PIN_16
            MIOPin17Control = 0x744, // MIO_PIN_17
            MIOPin18Control = 0x748, // MIO_PIN_18
            MIOPin19Control = 0x74C, // MIO_PIN_19
            MIOPin20Control = 0x750, // MIO_PIN_20
            MIOPin21Control = 0x754, // MIO_PIN_21
            MIOPin22Control = 0x758, // MIO_PIN_22
            MIOPin23Control = 0x75C, // MIO_PIN_23
            MIOPin24Control = 0x760, // MIO_PIN_24
            MIOPin25Control = 0x764, // MIO_PIN_25
            MIOPin26Control = 0x768, // MIO_PIN_26
            MIOPin27Control = 0x76C, // MIO_PIN_27
            MIOPin28Control = 0x770, // MIO_PIN_28
            MIOPin29Control = 0x774, // MIO_PIN_29
            MIOPin30Control = 0x778, // MIO_PIN_30
            MIOPin31Control = 0x77C, // MIO_PIN_31
            MIOPin32Control = 0x780, // MIO_PIN_32
            MIOPin33Control = 0x784, // MIO_PIN_33
            MIOPin34Control = 0x788, // MIO_PIN_34
            MIOPin35Control = 0x78C, // MIO_PIN_35
            MIOPin36Control = 0x790, // MIO_PIN_36
            MIOPin37Control = 0x794, // MIO_PIN_37
            MIOPin38Control = 0x798, // MIO_PIN_38
            MIOPin39Control = 0x79C, // MIO_PIN_39
            MIOPin40Control = 0x7A0, // MIO_PIN_40
            MIOPin41Control = 0x7A4, // MIO_PIN_41
            MIOPin42Control = 0x7A8, // MIO_PIN_42
            MIOPin43Control = 0x7AC, // MIO_PIN_43
            MIOPin44Control = 0x7B0, // MIO_PIN_44
            MIOPin45Control = 0x7B4, // MIO_PIN_45
            MIOPin46Control = 0x7B8, // MIO_PIN_46
            MIOPin47Control = 0x7BC, // MIO_PIN_47
            MIOPin48Control = 0x7C0, // MIO_PIN_48
            MIOPin49Control = 0x7C4, // MIO_PIN_49
            MIOPin50Control = 0x7C8, // MIO_PIN_50
            MIOPin51Control = 0x7CC, // MIO_PIN_51
            MIOPin52Control = 0x7D0, // MIO_PIN_52
            MIOPin53Control = 0x7D4, // MIO_PIN_53
            LoopbackFunctionWithinMIO = 0x804, // MIO_LOOPBACK
            MIOPinTriStateEnables_0 = 0x80C, // MIO_MST_TRI0
            MIOPinTriStateEnables_1 = 0x810, // MIO_MST_TRI1
            SDIO0WPCDSelect = 0x830, // SD0_WP_CD_SEL
            SDIO1WPCDSelect = 0x834, // SD1_WP_CD_SEL
            LevelShiftersEnable = 0x900, // LVL_SHFTR_EN
            OCMAddressMapping = 0x910, // OCM_CFG
            Reserved = 0xA1C, // Reserved
            PSIOBufferControl = 0xB00, // GPIOB_CTRL
            MIOGPIOBCMOS1V8Config = 0xB04, // GPIOB_CFG_CMOS18
            MIOGPIOBCMOS2V5Config = 0xB08, // GPIOB_CFG_CMOS25
            MIOGPIOBCMOS3V3Config = 0xB0C, // GPIOB_CFG_CMOS33
            MIOGPIOBHSTLConfig = 0xB14, // GPIOB_CFG_HSTL
            MIOGPIOBDriverBiasControl = 0xB18, // GPIOB_DRVR_BIAS_CTRL
            DDRIOBConfigAddress_0 = 0xB40, // DDRIOB_ADDR0
            DDRIOBConfigAddress_1 = 0xB44, // DDRIOB_ADDR1
            DDRIOBConfigForData_0 = 0xB48, // DDRIOB_DATA0
            DDRIOBConfigForData_1 = 0xB4C, // DDRIOB_DATA1
            DDRIOBConfigForDQS_0 = 0xB50, // DDRIOB_DIFF0
            DDRIOBConfigForDQS_1 = 0xB54, // DDRIOB_DIFF1
            DDRIOBConfigForClockOutput = 0xB58, // DDRIOB_CLOCK
            DriveAndSlewControlsForAddressAndCommandPinsOfTheDDRInterface = 0xB5C, // DDRIOB_DRIVE_SLEW_ADDR
            DriveAndSlewControlsForDQPinsOfTheDDRInterface = 0xB60, // DDRIOB_DRIVE_SLEW_DATA
            DriveAndSlewControlsForDQSPinsOfTheDDRInterface = 0xB64, // DDRIOB_DRIVE_SLEW_DIFF
            DriveAndSlewControlsForClockPinsOfTheDDRInterface = 0xB68, // DDRIOB_DRIVE_SLEW_CLOCK
            DDRIOBBufferControl = 0xB6C, // DDRIOB_DDR_CTRL
            DDRIOBDCIConfig = 0xB70, // DDRIOB_DCI_CTRL
            DDRIOBufferDCIStatus = 0xB74, // DDRIOB_DCI_STATUS
        }
    }
}
