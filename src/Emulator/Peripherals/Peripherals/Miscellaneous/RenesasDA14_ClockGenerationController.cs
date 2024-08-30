//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // XTAL32M registers are in the RenesasDA14_XTAL32MRegisters model.
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RenesasDA14_ClockGenerationController : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA14_ClockGenerationController(IMachine machine, RenesasDA14_XTAL32MRegisters xtal32m, MappedMemory rom, MappedMemory eflashDataText)
        {
            this.machine = machine;
            this.xtal32m = xtal32m;
            this.rom = rom;
            this.eflashDataText = eflashDataText;

            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
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

        public long Size => 0x100;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.ClockAMBA.Define(this, 0x12)
                .WithValueField(0, 3, name: "HCLK_DIV")
                .WithReservedBits(3, 1)
                .WithValueField(4, 2, name: "PCLK_DIV")
                .WithFlag(6, name: "AES_CLK_ENABLE")
                .WithFlag(7, name: "QDEC_CLK_ENABLE")
                .WithReservedBits(8, 2)
                .WithValueField(10, 2, name: "QSPI_DIV")
                .WithFlag(12, name: "QSPI_ENABLE")
                .WithReservedBits(13, 19);

            Registers.ResetControl.Define(this)
                .WithFlag(0, name: "GATE_RST_WITH_FCU")
                .WithFlag(1, name: "SYS_CACHE_FLUSH_WITH_SW_RESET")
                .WithFlag(2, name: "CMAC_CACHE_FLUSH_WITH_SW_RESET")
                .WithReservedBits(3, 29);

            Registers.RadioPLL.Define(this, 0x4)
                .WithFlag(0, name: "CMAC_CLK_ENABLE")
                .WithFlag(1, name: "CMAC_CLK_SEL")
                .WithFlag(2, name: "CMAC_SYNCH_RESET")
                .WithFlag(3, name: "RFCU_ENABLE")
                .WithReservedBits(4, 28);

            Registers.ClockControl.Define(this, 0x2001)
                .WithEnumField<DoubleWordRegister, SystemClock>(0, 2, out systemClock, name: "SYS_CLK_SEL")
                .WithValueField(2, 2, name: "LP_CLK_SEL")
                .WithReservedBits(4, 1)
                .WithFlag(5, name: "XTAL32M_DISABLE")
                .WithReservedBits(6, 6)
                .WithFlag(12, FieldMode.Read, name: "RUNNING_AT_LP_CLOCK", valueProviderCallback: _ => systemClock.Value == SystemClock.RCLP)
                .WithFlag(13, FieldMode.Read, name: "RUNNING_AT_RC32M", valueProviderCallback: _ => systemClock.Value == SystemClock.RC32M)
                .WithFlag(14, FieldMode.Read, name: "RUNNING_AT_XTAL32M", valueProviderCallback: _ => systemClock.Value == SystemClock.XTAL32M)
                .WithFlag(15, FieldMode.Read, name: "RUNNING_AT_DBLR64M", valueProviderCallback: _ => systemClock.Value == SystemClock.DBLR64M)
                .WithReservedBits(16, 16);

            Registers.Switch2XTAL.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "SWITCH2XTAL",
                        writeCallback: (_, value) =>
                        {
                            if(value && systemClock.Value == SystemClock.RC32M)
                            {
                                systemClock.Value = SystemClock.XTAL32M;
                            }
                        })
                .WithReservedBits(1, 31);

            Registers.PMUClock.Define(this, 0x40F)
                .WithFlag(0, out periphSleep, name: "PERIPH_SLEEP")
                .WithFlag(1, out radioSleep, name: "RADIO_SLEEP")
                .WithFlag(2, out timSleep, name: "TIM_SLEEP")
                .WithFlag(3, out comSleep, name: "COM_SLEEP")
                .WithFlag(4, name: "MAP_BANDGAP_EN")
                .WithFlag(5, name: "RESET_ON_WAKEUP")
                .WithFlag(6, out sysSleep, name: "SYS_SLEEP")
                .WithFlag(7, name: "RETAIN_CACHE")
                .WithReservedBits(8, 1)
                .WithFlag(9, name: "LP_CLK_OUTPUT_EN")
                .WithFlag(10, out audSleep, name: "AUD_SLEEP")
                .WithFlag(11, name: "RETAIN_CMAC_CACHE")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) =>
                    {
                        if(!timSleep.Value)
                        {
                            xtal32m.Enable = true;
                        }
                    });

            Registers.SystemControl.Define(this)
                .WithEnumField<DoubleWordRegister, RemapAddress>(0, 3, out remapAddress, name: "REMAP_ADDR0")
                .WithFlag(3, name: "REMAP_INTVECT")
                .WithReservedBits(4, 3)
                .WithFlag(7, name: "DEBUGGER_ENABLE")
                .WithReservedBits(8, 2)
                .WithFlag(10, name: "CACHERAM_MUX")
                .WithReservedBits(11, 4)
                .WithFlag(15, FieldMode.WriteOneToClear, name: "SW_RESET",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }
                            if(!machine.SystemBus.TryGetCurrentCPU(out var cpu))
                            {
                                this.Log(LogLevel.Error, "Tried to initialize a software reset, but no CPU is detected. Ignoring this operation");
                                return;
                            }
                            if(remapAddress.Value != RemapAddress.EFlash)
                            {
                                this.Log(LogLevel.Error, "Tried to initialize remapping of {0} to 0x0, but it is currently unsupported. Ignoring this operation", remapAddress.ToString());
                                return;
                            }
                            // we need to cast to ICPUWithRegisters to access SetRegister
                            ICPUWithRegisters cpuWithRegisters = cpu as ICPUWithRegisters;
                            if(cpuWithRegisters == null)
                            {
                                cpu.Log(LogLevel.Error, "Current CPU is not of type ICPUWithRegisters");
                                return;
                            }
                            cpuWithRegisters.IsHalted = true;
                            machine.LocalTimeSource.ExecuteInNearestSyncedState((__) =>
                            {
                                var systemBus = machine.SystemBus;
                                const int baseRegistrationPoint = 0x0;

                                systemBus.Unregister(this.rom);
                                systemBus.Register(this.eflashDataText, new BusPointRegistration(baseRegistrationPoint));
                                // SP is register is not available in the interface, so we have to set it manually
                                // ArmRegisters is in another project, so we can't use it here
                                const int SP = 13;
                                const int PCValueInELF = 0x4;
                                const int SPValueInELF = 0x0;
                                cpuWithRegisters.PC = systemBus.ReadDoubleWord(PCValueInELF);
                                cpuWithRegisters.SetRegister(SP, systemBus.ReadDoubleWord(SPValueInELF));
                                cpuWithRegisters.Log(LogLevel.Info, "Succesfully remapped eflash to address 0x0. Restarting machine.");
                                cpuWithRegisters.Log(LogLevel.Info, "PC set to 0x{0:X}, SP set to 0x{1:X}", cpuWithRegisters.PC.RawValue, cpuWithRegisters.GetRegister(SP).RawValue);
                                cpuWithRegisters.IsHalted = false;
                            });
                        })
                .WithReservedBits(16, 16);

            Registers.SystemStatus.Define(this, 0x95A5)
                .WithFlag(0, FieldMode.Read, name: "RAD_IS_DOWN", valueProviderCallback: _ => radioSleep.Value)
                .WithFlag(1, FieldMode.Read, name: "RAD_IS_UP", valueProviderCallback: _ => !radioSleep.Value)
                .WithFlag(2, FieldMode.Read, name: "PER_IS_DOWN", valueProviderCallback: _ => periphSleep.Value)
                .WithFlag(3, FieldMode.Read, name: "PER_IS_UP", valueProviderCallback: _ => !periphSleep.Value)
                .WithFlag(4, FieldMode.Read, name: "SYS_IS_DOWN", valueProviderCallback: _ => sysSleep.Value)
                .WithFlag(5, FieldMode.Read, name: "SYS_IS_UP", valueProviderCallback: _ => !sysSleep.Value)
                .WithFlag(6, FieldMode.Read, name: "MEM_IS_DOWN")
                .WithFlag(7, FieldMode.Read, name: "MEM_IS_UP")
                .WithFlag(8, FieldMode.Read, name: "TIM_IS_DOWN", valueProviderCallback: _ => timSleep.Value)
                .WithFlag(9, FieldMode.Read, name: "TIM_IS_UP", valueProviderCallback: _ => !timSleep.Value)
                .WithFlag(10, FieldMode.Read, name: "COM_IS_DOWN", valueProviderCallback: _ => comSleep.Value)
                .WithFlag(11, FieldMode.Read, name: "COM_IS_UP", valueProviderCallback: _ => !comSleep.Value)
                .WithFlag(12, FieldMode.Read, name: "AUD_IS_DOWN", valueProviderCallback: _ => audSleep.Value)
                .WithFlag(13, FieldMode.Read, name: "AUD_IS_UP", valueProviderCallback: _ => !audSleep.Value)
                .WithFlag(14, FieldMode.Read, name: "DBG_IS_ACTIVE")
                .WithFlag(15, FieldMode.Read, name: "POWER_IS_UP")
                .WithReservedBits(16, 16);

            Registers.ClockRCLP.Define(this, 0x38)
                .WithFlag(0, name: "RCLP_DISABLE")
                .WithFlag(1, name: "RCLP_HIGH_SPEED_FORCE")
                .WithFlag(2, name: "RCLP_LOW_SPEED_FORCE")
                .WithValueField(3, 4, name: "RCLP_TRIM")
                .WithReservedBits(7, 25);

            Registers.ClockXTAL32.Define(this, 0x82E)
                .WithFlag(0, name: "XTAL32K_ENABLE")
                .WithValueField(1, 2, name: "XTAL32K_RBIAS")
                .WithValueField(3, 4, name: "XTAL32K_CUR")
                .WithFlag(7, name: "XTAL32K_DISABLE_AMPREG")
                .WithReservedBits(8, 1)
                .WithValueField(9, 4, name: "XTAL32K_VDDX_TRIM")
                .WithReservedBits(13, 19);

            Registers.ClockRC32M.Define(this, 0x3CE)
                .WithFlag(0, out rc32mEnable, name: "RC32M_ENABLE",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                systemClock.Value = SystemClock.RC32M;
                            }
                        })
                .WithValueField(1, 4, name: "RC32M_BIAS")
                .WithValueField(5, 2, name: "RC32M_RANGE")
                .WithValueField(7, 4, name: "RC32M_COSC")
                .WithReservedBits(11, 21);

            Registers.ClockRCX.Define(this, 0x57E)
                .WithFlag(0, name: "RCX_ENABLE")
                .WithValueField(1, 5, name: "RCX_CADJUST")
                .WithFlag(6, name: "RCX_C0")
                .WithValueField(7, 4, name: "RCX_BIAS")
                .WithReservedBits(11, 21);

            Registers.BandGap.Define(this)
                .WithValueField(0, 5, name: "BGR_TRIM")
                .WithReservedBits(5, 1)
                .WithValueField(6, 5, name: "BGR_ITRIM")
                .WithReservedBits(11, 21);

            Registers.ResetPadLatch0.Define(this)
                .WithValueField(0, 16, name: "P0_RESET_LATCH_EN")
                .WithReservedBits(16, 16);

            Registers.ResetPadLatch1.Define(this)
                .WithValueField(0, 16, name: "P1_RESET_LATCH_EN")
                .WithReservedBits(16, 16);

            Registers.BiasVRef.Define(this)
                .WithValueField(0, 4, name: "BIAS_VREF_RF1_SEL")
                .WithValueField(4, 4, name: "BIAS_VREF_RF2_SEL")
                .WithReservedBits(8, 24);

            Registers.ResetStatus.Define(this, 0x3F)
                .WithFlag(0, name: "PORESET_STAT")
                .WithFlag(1, name: "HWRESET_STAT")
                .WithFlag(2, name: "SWRESET_STAT")
                .WithFlag(3, name: "WDOGRESET_STAT")
                .WithFlag(4, name: "SWD_HWRESET_STAT")
                .WithFlag(5, name: "CMAC_WDOGRESET_STAT")
                .WithReservedBits(6, 26);

            Registers.SecureBoot.Define(this)
                .WithFlag(0, name: "PROT_CONFIG_SCRIPT")
                .WithFlag(1, name: "PROT_APP_KEY")
                .WithFlag(2, name: "PROT_VALID_KEY")
                .WithFlag(3, name: "PROT_USER_APP_CODE")
                .WithFlag(4, name: "FORCE_M33_DEBUGGER_OFF")
                .WithFlag(5, name: "FORCE_CMAC_DEBUGGER_OFF")
                .WithFlag(6, name: "SECURE_BOOT")
                .WithReservedBits(7, 1)
                .WithFlag(8, name: "PROT_INFO_PAGE")
                .WithReservedBits(9, 23);

            Registers.BODControl.Define(this, 0xC0)
                .WithValueField(0, 2, name: "BOD_SEL_VDD_LVL")
                .WithFlag(2, out bodVdddOkSyncRd, name: "BIS_VDD_COMP",
                    writeCallback: (_, value) => bodVdddOkSyncRd.Value = true)
                .WithFlag(3, out bodVdcdcOkSyncRd, name: "BOD_DIS_VDCDC_COMP",
                    writeCallback: (_, value) => bodVdcdcOkSyncRd.Value = true)
                .WithFlag(4, out bodVddioOkSyncRd, name: "BOD_DIS_VDDIO_COMP",
                    writeCallback: (_, value) => bodVddioOkSyncRd.Value = true)
                .WithReservedBits(5, 1)
                .WithFlag(6, out bodVdddMaskSyncRd, name: "BOD_VDD_MASK",
                    writeCallback: (_, value) => bodVdddMaskSyncRd.Value = value)
                .WithFlag(7, out bodVdcdcMaskSyncRd, name: "BOD_VDCDC_MASK",
                    writeCallback: (_, value) => bodVdcdcMaskSyncRd.Value = value)
                .WithFlag(8, out bodVddioMaskSyncRd, name: "BOD_VDDIO_MASK",
                    writeCallback: (_, value) => bodVddioMaskSyncRd.Value = value)
                .WithReservedBits(9, 23);

            Registers.AnalogStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "BANDGAP_OK", valueProviderCallback: _ => true)
                .WithFlag(1, FieldMode.Read, name: "LDO_CORE_OK", valueProviderCallback: _ => true)
                .WithFlag(2, FieldMode.Read, name: "LDO_LOW_OK", valueProviderCallback: _ => true)
                .WithFlag(3, FieldMode.Read, name: "LDO_IO_OK", valueProviderCallback: _ => true)
                .WithFlag(4, FieldMode.Read, name: "BOD_COMP_VDD_OK", valueProviderCallback: _ => true)
                .WithFlag(5, FieldMode.Read, name: "BOD_COMP_VDDIO_OK", valueProviderCallback: _ => true)
                .WithFlag(6, FieldMode.Read, name: "BOD_COMP_VDCDC_OK", valueProviderCallback: _ => true)
                .WithFlag(7, FieldMode.Read, name: "BOD_COMP_VEFLASH_OK", valueProviderCallback: _ => true)
                .WithFlag(8, FieldMode.Read, name: "LDO_GPADC_OK", valueProviderCallback: _ => true)
                .WithReservedBits(9, 23);

            Registers.PowerControl.Define(this, 0x6517)
                .WithFlag(0, name: "LDO_IO_ENABLE")
                .WithFlag(1, name: "LDO_LOW_ENABLE_ACTIVE")
                .WithFlag(2, name: "LDO_CORE_ENABLE")
                .WithFlag(3, name: "LDO_IO_RET_ENABLE_ACTIVE")
                .WithFlag(4, name: "LDO_IO_RET_ENABLE_SLEEP")
                .WithFlag(5, name: "LDO_IO_BYPASS_ACTIVE")
                .WithFlag(6, name: "LDO_IO_BYPASS_SLEEP")
                .WithFlag(7, name: "LDO_LOW_HIGH_CURRENT")
                .WithFlag(8, name: "LDO_LOW_ENABLE_SLEEP")
                .WithFlag(9, name: "LDO_CORE_RET_ENABLE_ACTIVE")
                .WithFlag(10,name: "LDO_CORE_RET_ENABLE_SLEEP")
                .WithFlag(11,name: "DCDC_ENABLE_SLEEP")
                .WithFlag(12,name: "LDO_VREF_HOLD_FORCE")
                .WithFlag(13,name: "LDO_IO_RET_VREF_ENABLE")
                .WithFlag(14,name: "LDO_CORE_RET_VREF_ENABLE")
                .WithReservedBits(15, 17);

            Registers.PowerLevel.Define(this)
                .WithValueField(0, 3, name: "VDD_LEVEL_ACTIVE")
                .WithFlag(3, name: "VDD_LEVEL_SLEEP")
                .WithValueField(4, 3, name: "VDCDC_LEVEL")
                .WithValueField(7, 4, name: "VDDIO_TRIM")
                .WithValueField(11, 2, name: "XTAL32M_LDO_LEVEL")
                .WithReservedBits(13, 19);

            Registers.StartUpStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "BOD_VDCDC_MASK_SYNC_RD", valueProviderCallback: _ => bodVdcdcMaskSyncRd.Value)
                .WithFlag(1, FieldMode.Read, name: "BOD_VDDD_MASK_SYNC_RD", valueProviderCallback: _ => bodVdddMaskSyncRd.Value)
                .WithFlag(2, FieldMode.Read, name: "BOD_VDDIO_MASK_SYNC_RD", valueProviderCallback: _ => bodVddioMaskSyncRd.Value)
                .WithValueField(3, 2, FieldMode.Read, name: "BOD_VDDD_LVL_RD")
                .WithFlag(5, FieldMode.Read, name: "BOD_VDCDC_OK_SYNC_RD", valueProviderCallback: _ => bodVdcdcOkSyncRd.Value)
                .WithFlag(6, FieldMode.Read, name: "BOD_VDDD_OK_SYNC_RD", valueProviderCallback: _ => bodVdddOkSyncRd.Value)
                .WithFlag(7, FieldMode.Read, name: "BOD_VDDIO_OK_SYNC_RD", valueProviderCallback: _ => bodVddioOkSyncRd.Value)
                .WithFlag(8, FieldMode.Read, name: "BOD_VEFLASH_OK_SYNC")
                .WithFlag(9, FieldMode.Read, name: "VEFLASH_LVL_RD")
                .WithReservedBits(10, 22);
        }

        private IMachine machine;
        private RenesasDA14_XTAL32MRegisters xtal32m;
        private MappedMemory rom;
        private MappedMemory eflashDataText;

        private IEnumRegisterField<SystemClock> systemClock;
        private IFlagRegisterField periphSleep;
        private IFlagRegisterField radioSleep;
        private IFlagRegisterField timSleep;
        private IFlagRegisterField comSleep;
        private IFlagRegisterField sysSleep;
        private IFlagRegisterField audSleep;
        private IEnumRegisterField<RemapAddress> remapAddress;
        private IFlagRegisterField rc32mEnable;
        private IFlagRegisterField bodVdddMaskSyncRd;
        private IFlagRegisterField bodVdcdcMaskSyncRd;
        private IFlagRegisterField bodVddioMaskSyncRd;
        private IFlagRegisterField bodVdcdcOkSyncRd;
        private IFlagRegisterField bodVdddOkSyncRd;
        private IFlagRegisterField bodVddioOkSyncRd;

        private enum Registers
        {
            ClockAMBA          = 0x0,
            ResetControl       = 0xC,
            RadioPLL           = 0x10,
            ClockControl       = 0x14,
            ClockTimers        = 0x18,
            Switch2XTAL        = 0x1C,
            PMUClock           = 0x20,
            SystemControl      = 0x24,
            SystemStatus       = 0x28,
            ClockRCLP          = 0x3C,
            ClockXTAL32        = 0x40,
            ClockRC32M         = 0x44,
            ClockRCX           = 0x48,
            ClockRTCDivisor    = 0x4C,
            BandGap            = 0x50,
            PadLatch0          = 0x70,
            SetPadLatch0       = 0x74,
            ResetPadLatch0     = 0x78,
            PadLatch1          = 0x7C,
            SetPadLatch1       = 0x80,
            ResetPadLatch1     = 0x84,
            PORPin             = 0x98,
            PORTimer           = 0x9C,
            BiasVRef           = 0xA4,
            ResetStatus        = 0xBC,
            RamPwrControl      = 0xC0,
            SecureBoot         = 0xCC,
            BODControl         = 0xD0,
            DiscargeRail       = 0xD4,
            AnalogStatus       = 0xDC,
            PowerControl       = 0xE0,
            PowerLevel         = 0xE4,
            HibernationControl = 0xF0,
            PMUSleepControl    = 0xF4,
            StartUpStatus      = 0xFC,
        }

        private enum SystemClock : byte
        {
            XTAL32M         = 0x0,
            RC32M           = 0x1,
            RCLP            = 0x2,
            DBLR64M         = 0x3,
        }

        private enum RemapAddress : byte
        {
            ROM                = 0x0,
            EFlash             = 0x1,
            SysRam3            = 0x4,
            SystemCacheDataRAM = 0x5,
            NoRemapping        = 0x7,
        }
    }
}
