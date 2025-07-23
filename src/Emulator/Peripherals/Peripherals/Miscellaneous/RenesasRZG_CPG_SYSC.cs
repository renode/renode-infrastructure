//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RenesasRZG_CPG_SYSC : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, IPeripheralRegister<RenesasRZG_Watchdog, NumberRegistrationPoint<byte>>
    {
        public RenesasRZG_CPG_SYSC(ICPU cpu0 = null, ICPU cpu1 = null)
        {
            this.cpu0 = cpu0;
            this.cpu1 = cpu1;
            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
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

        public void Register(RenesasRZG_Watchdog wdt, NumberRegistrationPoint<byte> id)
        {
            if(id.Address < 0 || id.Address >= MaxWatchdogCount)
            {
                throw new RecoverableException($"{id.Address} is not a valid Watchdog ID");
            }
            if(watchdogs[id.Address] != null)
            {
                throw new RecoverableException($"WDT{id.Address} is already connected");
            }
            var duplicateRegistration = watchdogs.IndexOf(x => x == wdt);
            if(duplicateRegistration >= 0)
            {
                throw new RecoverableException($"WDT{id.Address} is already connected as WDT{duplicateRegistration}");
            }
            watchdogs[id.Address] = wdt;
        }

        public void Unregister(RenesasRZG_Watchdog wdt)
        {
            var i = watchdogs.IndexOf(x => x == wdt);
            if(i >= 0)
            {
                watchdogs[i] = null;
            }
        }

        public long Size => 0x20000;
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();

            DefineCPGRegisters(registerMap);
            DefineSYSCRegisters(registerMap);

            return registerMap;
        }

        private void DefineSYSCRegisters(Dictionary<long, DoubleWordRegister> registerMap)
        {
            registerMap.Add((long)Registers.MasterAccessControl0, new DoubleWordRegister(this, 0x00AAAA00)
                .WithTaggedFlag("DMAC0_AWPU", 0)
                .WithTaggedFlag("DMAC0_AWNS", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("DMAC0_AWSEL", 3)
                .WithTaggedFlag("DMAC0_ARRU", 4)
                .WithTaggedFlag("DMAC0_ARNS", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("DMAC0_ARSEL", 7)
                .WithTaggedFlag("DMAC1_AWPU", 8)
                .WithTaggedFlag("DMAC1_AWNS", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("DMAC1_AWSEL", 11)
                .WithTaggedFlag("DMAC1_ARRU", 12)
                .WithTaggedFlag("DMAC1_ARNS", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("DMAC1_ARSEL", 15)
                .WithTaggedFlag("GPU_AWPU", 16)
                .WithTaggedFlag("GPU_AWNS", 17)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("GPU_AWSEL", 19)
                .WithTaggedFlag("GPU_ARRU", 20)
                .WithTaggedFlag("GPU_ARNS", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("GPU_ARSEL", 23)
                .WithReservedBits(24, 8)
            );

            registerMap.Add((long)Registers.MasterAccessControl1, new DoubleWordRegister(this, 0x00AAAA00)
                .WithTaggedFlag("SDHI0_AWPU", 0)
                .WithTaggedFlag("SDHI0_AWNS", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SDHI0_AWSEL", 3)
                .WithTaggedFlag("SDHI0_ARRU", 4)
                .WithTaggedFlag("SDHI0_ARNS", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("SDHI0_ARSEL", 7)
                .WithTaggedFlag("SDHI1_AWPU", 8)
                .WithTaggedFlag("SDHI1_AWNS", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("SDHI1_AWSEL", 11)
                .WithTaggedFlag("SDHI1_ARRU", 12)
                .WithTaggedFlag("SDHI1_ARNS", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("SDHI1_ARSEL", 15)
                .WithTaggedFlag("GEther0_AWPU", 16)
                .WithTaggedFlag("GEther0_AWNS", 17)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("GEther0_AWSEL", 19)
                .WithTaggedFlag("GEther0_ARRU", 20)
                .WithTaggedFlag("GEther0_ARNS", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("GEther0_ARSEL", 23)
                .WithTaggedFlag("GEther1_AWPU", 24)
                .WithTaggedFlag("GEther1_AWNS", 25)
                .WithReservedBits(26, 1)
                .WithTaggedFlag("GEther1_AWSEL", 27)
                .WithTaggedFlag("GEther1_ARRU", 28)
                .WithTaggedFlag("GEther1_ARNS", 29)
                .WithReservedBits(30, 1)
                .WithTaggedFlag("GEther1_ARSEL", 31)
            );

            registerMap.Add((long)Registers.MasterAccessControl2, new DoubleWordRegister(this, 0x00AAAA00)
                .WithTaggedFlag("USB2_0H_AWPU", 0)
                .WithTaggedFlag("USB2_0H_AWNS", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("USB2_0H_AWSEL", 3)
                .WithTaggedFlag("USB2_0H_ARRU", 4)
                .WithTaggedFlag("USB2_0H_ARNS", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("USB2_0H_ARSEL", 7)
                .WithTaggedFlag("USB2_1H_AWPU", 8)
                .WithTaggedFlag("USB2_1H_AWNS", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("USB2_1H_AWSEL", 11)
                .WithTaggedFlag("USB2_1H_ARRU", 12)
                .WithTaggedFlag("USB2_1H_ARNS", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("USB2_1H_ARSEL", 15)
                .WithTaggedFlag("USB2_0D_AWPU", 16)
                .WithTaggedFlag("USB2_0D_AWNS", 17)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("USB2_0D_AWSEL", 19)
                .WithTaggedFlag("USB2_0D_ARRU", 20)
                .WithTaggedFlag("USB2_0D_ARNS", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("USB2_0D_ARSEL", 23)
                .WithReservedBits(24, 8)
            );

            registerMap.Add((long)Registers.MasterAccessControl3, new DoubleWordRegister(this, 0x00AAAA00)
                .WithTaggedFlag("H264_AWPU", 0)
                .WithTaggedFlag("H264_AWNS", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("H264_AWSEL", 3)
                .WithTaggedFlag("H264_ARRU", 4)
                .WithTaggedFlag("H264_ARNS", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("H264_ARSEL", 7)
                .WithTaggedFlag("LCDC_AWPU", 8)
                .WithTaggedFlag("LCDC_AWNS", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("LCDC_AWSEL", 11)
                .WithTaggedFlag("LCDC_ARRU", 12)
                .WithTaggedFlag("LCDC_ARNS", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("LCDC_ARSEL", 15)
                .WithTaggedFlag("DSI_AWPU", 16)
                .WithTaggedFlag("DSI_AWNS", 17)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("DSI_AWSEL", 19)
                .WithTaggedFlag("DSI_ARRU", 20)
                .WithTaggedFlag("DSI_ARNS", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("DSI_ARSEL", 23)
                .WithReservedBits(24, 8)
            );

            registerMap.Add((long)Registers.MasterAccessControl4, new DoubleWordRegister(this, 0xAAAA00AA)
                .WithTaggedFlag("ISU_AWPU", 0)
                .WithTaggedFlag("ISU_AWNS", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("ISU_AWSEL", 3)
                .WithTaggedFlag("ISU_ARRU", 4)
                .WithTaggedFlag("ISU_ARNS", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("ISU_ARSEL", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("CRU_VD_AWPU", 16)
                .WithTaggedFlag("CRU_VD_AWNS", 17)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("CRU_VD_AWSEL", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("CRU_ST_AWPU", 24)
                .WithTaggedFlag("CRU_ST_AWNS", 25)
                .WithReservedBits(26, 1)
                .WithTaggedFlag("CRU_ST_AWSEL", 27)
                .WithReservedBits(28, 4)
            );

            registerMap.Add((long)Registers.SlaveAccessControl0, new DoubleWordRegister(this, 0x0AAAAAA0)
                .WithTag("SRAM0_SL", 0, 2)
                .WithTag("SRAM1_SL", 2, 2)
                .WithReservedBits(4, 28)
            );

            registerMap.Add((long)Registers.SlaveAccessControl1, new DoubleWordRegister(this, 0x0800C0AA)
                .WithTag("TZC0_SL", 0, 2)
                .WithTag("TZC1_SL", 2, 2)
                .WithTag("TZC2_SL", 4, 2)
                .WithTag("TZC3_SL", 6, 2)
                .WithReservedBits(8, 2)
                .WithTag("CST_SL", 10, 2)
                .WithTag("CPG_SL", 12, 2)
                .WithTag("SYSC_SL", 14, 2)
                .WithTag("SYS_SL", 16, 2)
                .WithTag("GIC_SL", 18, 2)
                .WithTag("IA55_IM33_SL", 20, 2)
                .WithTag("GPIO_SL", 22, 2)
                .WithTag("MHU_SL", 24, 2)
                .WithTag("DMAC0_SL", 26, 2)
                .WithTag("DMAC1_SL", 28, 2)
                .WithReservedBits(30, 2)
            );

            registerMap.Add((long)Registers.SlaveAccessControl2, new DoubleWordRegister(this, 0x00000002)
                .WithTag("OSTM0_SL", 0, 2)
                .WithTag("OSTM1_SL", 2, 2)
                .WithTag("OSTM2_SL", 4, 2)
                .WithTag("WDT0_SL", 6, 2)
                .WithTag("WDT1_SL", 8, 2)
                .WithTag("WDT2_SL", 10, 2)
                .WithReservedBits(12, 2)
                .WithTag("MTU3A_SL", 14, 2)
                .WithTag("POE3_SL", 16, 2)
                .WithTag("GPT_SL", 18, 2)
                .WithTag("POEG_SL", 20, 2)
                .WithTag("DDR_SL", 22, 2)
                .WithReservedBits(24, 8)
            );

            registerMap.Add((long)Registers.SlaveAccessControl3, new DoubleWordRegister(this)
                .WithTag("GPU_SL", 0, 2)
                .WithTag("H264_SL", 2, 2)
                .WithTag("CRU_SL", 4, 2)
                .WithTag("ISU_SL", 6, 2)
                .WithTag("DSIPHY_SL", 8, 2)
                .WithTag("DSILINK_SL", 10, 2)
                .WithTag("LCDC_SL", 12, 2)
                .WithReservedBits(14, 2)
                .WithTag("USBT_SL", 16, 2)
                .WithTag("USB20_SL", 18, 2)
                .WithTag("USB21_SL", 20, 2)
                .WithTag("SDHI0_SL", 22, 2)
                .WithTag("SDHI1_SL", 24, 2)
                .WithTag("ETH0_SL", 26, 2)
                .WithTag("ETH1_SL", 28, 2)
                .WithReservedBits(30, 2)
            );

            registerMap.Add((long)Registers.SlaveAccessControl4, new DoubleWordRegister(this)
                .WithTag("I2C0_SL", 0, 2)
                .WithTag("I2C1_SL", 2, 2)
                .WithTag("I2C2_SL", 4, 2)
                .WithTag("I2C3_SL", 6, 2)
                .WithTag("CANFD_SL", 8, 2)
                .WithTag("RSPI_SL", 10, 2)
                .WithReservedBits(12, 4)
                .WithTag("SCIF0_SL", 16, 2)
                .WithTag("SCIF1_SL", 18, 2)
                .WithTag("SCIF2_SL", 20, 2)
                .WithTag("SCIF3_SL", 22, 2)
                .WithTag("SCIF4_SL", 24, 2)
                .WithTag("SCI0_SL", 26, 2)
                .WithTag("SCI1_SL", 28, 2)
                .WithTag("IRDA_SL", 30, 2)
            );

            registerMap.Add((long)Registers.SlaveAccessControl5, new DoubleWordRegister(this)
                .WithTag("SSIF_SL", 0, 2)
                .WithReservedBits(2, 2)
                .WithTag("SRC_SL", 4, 2)
                .WithReservedBits(6, 26)
            );

            registerMap.Add((long)Registers.SlaveAccessControl6, new DoubleWordRegister(this)
                .WithTag("ADC_SL", 0, 2)
                .WithTag("TSU_SL", 2, 2)
                .WithReservedBits(4, 28)
            );

            registerMap.Add((long)Registers.SlaveAccessControl7, new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithTag("OTP_SL", 2, 2)
                .WithReservedBits(4, 28)
            );

            registerMap.Add((long)Registers.SlaveAccessControl8, new DoubleWordRegister(this)
                .WithTag("CM33_SL", 0, 2)
                .WithTag("CA55_SL", 2, 2)
                .WithReservedBits(4, 28)
            );

            registerMap.Add((long)Registers.SlaveAccessControl10, new DoubleWordRegister(this)
                .WithTag("LSI_SL", 0, 2)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.SlaveAccessControl12, new DoubleWordRegister(this)
                .WithTag("AOF_SL", 0, 2)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.SlaveAccessControl13, new DoubleWordRegister(this)
                .WithTag("LP_SL", 0, 2)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.SlaveAccessControl14, new DoubleWordRegister(this)
                .WithTag("GPREG_SL", 0, 2)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.ErrorCorrectingCodeRam0Settings, new DoubleWordRegister(this)
                .WithTaggedFlag("VECCEN", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.ErrorCorrectingCodeRam0AccessControl, new DoubleWordRegister(this, 0x00000003)
                .WithTaggedFlag("VCEN", 0)
                .WithTaggedFlag("VLWEN", 1)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.ErrorCorrectingCodeRam1Settings, new DoubleWordRegister(this)
                .WithTaggedFlag("VECCEN", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.ErrorCorrectingCodeRam1AccessControl, new DoubleWordRegister(this, 0x00000003)
                .WithTaggedFlag("VCEN", 0)
                .WithTaggedFlag("VLWEN", 1)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.WatchdogTimer0Control, CreateWatchdogTimerControlRegister(0));

            registerMap.Add((long)Registers.WatchdogTimer1Control, CreateWatchdogTimerControlRegister(1));

            registerMap.Add((long)Registers.WatchdogTimer2Control, CreateWatchdogTimerControlRegister(2));

            registerMap.Add((long)Registers.GigabitEthernet0Config, new DoubleWordRegister(this)
                .WithReservedBits(0, 24)
                .WithTaggedFlag("FEC_GIGA_ENABLE", 24)
                .WithReservedBits(25, 7)
            );

            registerMap.Add((long)Registers.GigabitEthernet1Config, new DoubleWordRegister(this)
                .WithReservedBits(0, 24)
                .WithTaggedFlag("FEC_GIGA_ENABLE", 24)
                .WithReservedBits(25, 7)
            );

            registerMap.Add((long)Registers.I2c0Config, new DoubleWordRegister(this)
                .WithTaggedFlag("AF_BYPASS", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.I2c1Config, new DoubleWordRegister(this)
                .WithTaggedFlag("AF_BYPASS", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.I2c2Config, new DoubleWordRegister(this)
                .WithTaggedFlag("AF_BYPASS", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.I2c3Config, new DoubleWordRegister(this)
                .WithTaggedFlag("AF_BYPASS", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.CortexM33Config0, new DoubleWordRegister(this, 0x00003D08)
                .WithTag("CONFIGSSYSTICK", 0, 26)
                .WithReservedBits(26, 6)
            );

            registerMap.Add((long)Registers.CortexM33Config1, new DoubleWordRegister(this, 0x00003D08)
                .WithTag("CONFIGNSSYSTICK", 0, 26)
                .WithReservedBits(26, 6)
            );

            registerMap.Add((long)Registers.CortexM33Config2, new DoubleWordRegister(this, 0x10010000)
                .WithReservedBits(0, 7)
                .WithTag("INITSVTOR", 7, 25)
            );

            registerMap.Add((long)Registers.CortexM33Config3, new DoubleWordRegister(this, 0x10018000)
                .WithReservedBits(0, 7)
                .WithTag("INITNSVTOR", 7, 25)
            );

            registerMap.Add((long)Registers.CortexM33Lock, new DoubleWordRegister(this)
                .WithTaggedFlag("LOCKSVTAIRCR", 0)
                .WithTaggedFlag("LOCKNSVTOR", 1)
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.CortexA55Core0ResetVectorAddressLowConfig, new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithTag("RVBARADDRL0", 2, 30)
            );

            registerMap.Add((long)Registers.CortexA55Core0ResetVectorAddressHighConfig, new DoubleWordRegister(this)
                .WithTag("RVBARADDRH0", 0, 8)
                .WithReservedBits(8, 24)
            );

            registerMap.Add((long)Registers.CortexA55Core1ResetVectorAddressLowConfig, new DoubleWordRegister(this, 0x00020000)
                .WithReservedBits(0, 2)
                .WithValueField(2, 30, out cortexA55Core1ResetVectorLow, name: "RVBARADDRL1")
            );

            registerMap.Add((long)Registers.CortexA55Core1ResetVectorAddressHighConfig, new DoubleWordRegister(this)
                .WithValueField(0, 8, out cortexA55Core1ResetVectorHigh, name: "RVBARADDRH1")
                .WithReservedBits(8, 24)
            );

            registerMap.Add((long)Registers.LsiModeSignal, new DoubleWordRegister(this)
                .WithTag("STAT_MD_BOOT", 0, 3)
                .WithReservedBits(3, 6)
                .WithTaggedFlag("STAT_DEBUGEN", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("STAT_MD_CLKS", 12)
                .WithReservedBits(13, 1)
                .WithTag("STAT_MD_OSCDRV", 14, 2)
                .WithTaggedFlag("STAT_SEC_EN", 16)
                .WithReservedBits(17, 15)
            );

            registerMap.Add((long)Registers.LsiDeviceId, new DoubleWordRegister(this, 0x0841C447)
                .WithValueField(0, 32, FieldMode.Read, name: "DEV_ID")
            );

            registerMap.Add((long)Registers.LsiProduct, new DoubleWordRegister(this, (uint)(HasTwoCortexA55Cores ? 0x0 : 0x1))
                .WithFlag(0, FieldMode.Read, name: "CA55_1CPU")
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.AddressOffset0, new DoubleWordRegister(this, 0x32103210)
                .WithTag("OFS00_SXSDHI_0", 0, 4)
                .WithTag("OFS01_SXSDHI_0", 4, 4)
                .WithTag("OFS10_SXSDHI_0", 8, 4)
                .WithTag("OFS11_SXSDHI_0", 12, 4)
                .WithTag("OFS00_SXSDHI_1", 16, 4)
                .WithTag("OFS01_SXSDHI_1", 20, 4)
                .WithTag("OFS10_SXSDHI_1", 24, 4)
                .WithTag("OFS11_SXSDHI_1", 28, 4)
            );

            registerMap.Add((long)Registers.AddressOffset1, new DoubleWordRegister(this, 0x32103210)
                .WithTag("OFS00_SXGIGE_0", 0, 4)
                .WithTag("OFS01_SXGIGE_0", 4, 4)
                .WithTag("OFS10_SXGIGE_0", 8, 4)
                .WithTag("OFS11_SXGIGE_0", 12, 4)
                .WithTag("OFS00_SXGIGE_1", 16, 4)
                .WithTag("OFS01_SXGIGE_1", 20, 4)
                .WithTag("OFS10_SXGIGE_1", 24, 4)
                .WithTag("OFS11_SXGIGE_1", 28, 4)
            );

            registerMap.Add((long)Registers.AddressOffset2, new DoubleWordRegister(this, 0x32103210)
                .WithTag("OFS00_SXUSB2_0_H", 0, 4)
                .WithTag("OFS01_SXUSB2_0_H", 4, 4)
                .WithTag("OFS10_SXUSB2_0_H", 8, 4)
                .WithTag("OFS11_SXUSB2_0_H", 12, 4)
                .WithTag("OFS00_SXUSB2_1", 16, 4)
                .WithTag("OFS01_SXUSB2_1", 20, 4)
                .WithTag("OFS10_SXUSB2_1", 24, 4)
                .WithTag("OFS11_SXUSB2_1", 28, 4)
            );

            registerMap.Add((long)Registers.AddressOffset3, new DoubleWordRegister(this, 0x00003210)
                .WithTag("OFS00_SXUSB2_0_F", 0, 4)
                .WithTag("OFS01_SXUSB2_0_F", 4, 4)
                .WithTag("OFS10_SXUSB2_0_F", 8, 4)
                .WithTag("OFS11_SXUSB2_0_F", 12, 4)
                .WithReservedBits(16, 16)
            );

            registerMap.Add((long)Registers.AddressOffset4, new DoubleWordRegister(this, 0x32103210)
                .WithTag("OFS00_SXLCDC", 0, 4)
                .WithTag("OFS01_SXLCDC", 4, 4)
                .WithTag("OFS10_SXLCDC", 8, 4)
                .WithTag("OFS11_SXLCDC", 12, 4)
                .WithTag("OFS00_SXDSIL", 16, 4)
                .WithTag("OFS01_SXDSIL", 20, 4)
                .WithTag("OFS10_SXDSIL", 24, 4)
                .WithTag("OFS11_SXDSIL", 28, 4)
            );

            registerMap.Add((long)Registers.AddressOffset5, new DoubleWordRegister(this, 0x00003210)
                .WithTag("OFS00_SXH264", 0, 4)
                .WithTag("OFS01_SXH264", 4, 4)
                .WithTag("OFS10_SXH264", 8, 4)
                .WithTag("OFS11_SXH264", 12, 4)
                .WithReservedBits(16, 16)
            );

            registerMap.Add((long)Registers.AddressOffset6, new DoubleWordRegister(this, 0x32103210)
                .WithTag("OFS00_SXDMAC_S", 0, 4)
                .WithTag("OFS01_SXDMAC_S", 4, 4)
                .WithTag("OFS10_SXDMAC_S", 8, 4)
                .WithTag("OFS11_SXDMAC_S", 12, 4)
                .WithTag("OFS00_SXDMAC_NS", 16, 4)
                .WithTag("OFS01_SXDMAC_NS", 20, 4)
                .WithTag("OFS10_SXDMAC_NS", 24, 4)
                .WithTag("OFS11_SXDMAC_NS", 28, 4)
            );

            registerMap.Add((long)Registers.LowPowerSequenceControl1, new DoubleWordRegister(this)
                .WithReservedBits(0, 8)
                .WithTag("CA55SLEEP_REQ", 8, 2)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("CM33SLEEP_REQ", 12)
                .WithReservedBits(13, 11)
                .WithTag("CA55SLEEP_ACK", 24, 2)
                .WithReservedBits(26, 2)
                .WithTaggedFlag("CM33SLEEP_ACK", 28)
                .WithReservedBits(29, 3)
            );

            registerMap.Add((long)Registers.LowPowerSequenceControl2, new DoubleWordRegister(this)
                .WithTaggedFlag("CA55_STBYCTL", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.LowPowerSequenceControl5, new DoubleWordRegister(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("ASCLKQDENY_F", 1)
                .WithTaggedFlag("AMCLKQDENY_F", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("CA55SLEEP1_F", 8)
                .WithTaggedFlag("CA55SLEEP1_F", 9)
                .WithTaggedFlag("CM33SLEEP_F", 10)
                .WithReservedBits(11, 21)
            );

            registerMap.Add((long)Registers.LowPowerSequenceControl6, new DoubleWordRegister(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("ASCLKQDENY_E", 1)
                .WithTaggedFlag("AMCLKQDENY_E", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("CA55SLEEP1_E", 8)
                .WithTaggedFlag("CA55SLEEP1_E", 9)
                .WithTaggedFlag("CM33SLEEP_E", 10)
                .WithReservedBits(11, 21)
            );

            registerMap.Add((long)Registers.LowPowerSequenceControl7, new DoubleWordRegister(this)
                .WithTaggedFlag("IM33_MASK", 0)
                .WithReservedBits(1, 31)
            );

            registerMap.Add((long)Registers.LowPowerSequenceCortexM33Control0, new DoubleWordRegister(this)
                .WithTaggedFlag("SLEEPMODE", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("SLEEPDEEP", 4)
                .WithReservedBits(5, 4)
                .WithTaggedFlag("SYSRESETREQ", 9)
                .WithReservedBits(10, 22)
            );

            registerMap.Add((long)Registers.CortexA55ClockControl1, new DoubleWordRegister(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("ASCLKQACTIVE", 1)
                .WithTaggedFlag("AMCLKQACTIVE", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("PCLKQACTIVE", 8)
                .WithTaggedFlag("ATCLKQACTIVE", 9)
                .WithTaggedFlag("GICCLKQACTIVE", 10)
                .WithTaggedFlag("PDBGCLKQACTIVE", 11)
                .WithReservedBits(12, 20)
            );

            registerMap.Add((long)Registers.CortexA55ClockControl2, new DoubleWordRegister(this, 0x00000F06)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("ASCLKQREQn", 1)
                .WithTaggedFlag("AMCLKQREQn", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("PCLKQREQn", 8)
                .WithTaggedFlag("ATCLKQREQn", 9)
                .WithTaggedFlag("GICCLKQREQn", 10)
                .WithTaggedFlag("PDBGCLKQREQn", 11)
                .WithReservedBits(12, 20)
            );

            registerMap.Add((long)Registers.CortexA55ClockControl3, new DoubleWordRegister(this)
                .WithTaggedFlag("CA55_COREINSTRRUN[0]", 0)
                .WithTaggedFlag("ASCLKQACCEPTn", 1)
                .WithTaggedFlag("AMCLKQACCEPTn", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("PCLKQACCEPTn", 8)
                .WithTaggedFlag("ATCLKQACCEPTn", 9)
                .WithTaggedFlag("GICCLKQAACCEPTn", 10)
                .WithTaggedFlag("PDBGCLKQACCEPTn", 11)
                .WithReservedBits(12, 4)
                .WithTaggedFlag("CA55_COREINSTRRUN[1]", 16)
                .WithTaggedFlag("ASCLKQDENY", 17)
                .WithTaggedFlag("AMCLKQDENY", 18)
                .WithReservedBits(19, 5)
                .WithTaggedFlag("PCLKQDENY", 24)
                .WithTaggedFlag("ATCLKQDENY", 25)
                .WithTaggedFlag("GICCLKQADENY", 26)
                .WithTaggedFlag("PDBGCLKQDENY", 27)
                .WithReservedBits(28, 4)
            );

            registerMap.Add((long)Registers.GpuLowPowerSequenceControl, new DoubleWordRegister(this, 0x00001F00)
                .WithTaggedFlag("QACTIVE_GPU", 0)
                .WithTaggedFlag("QACTIVE_AXI_SLV", 1)
                .WithTaggedFlag("QACTIVE_AXI_MST", 2)
                .WithTaggedFlag("QACTIVE_ACE_SLV", 3)
                .WithTaggedFlag("QACTIVE_ACE_MST", 4)
                .WithReservedBits(5, 3)
                .WithTaggedFlag("QREQn_GPU", 8)
                .WithTaggedFlag("QREQn_AXI_SLV", 9)
                .WithTaggedFlag("QREQn_AXI_MST", 10)
                .WithTaggedFlag("QREQn_ACE_SLV", 11)
                .WithTaggedFlag("QREQn_ACE_MST", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("QACCEPTn_GPU", 16)
                .WithTaggedFlag("QACCEPTn_AXI_SLV", 17)
                .WithTaggedFlag("QACCEPTn_AXI_MST", 18)
                .WithTaggedFlag("QACCEPTn_ACE_SLV", 19)
                .WithTaggedFlag("QACCEPTn_ACE_MST", 20)
                .WithReservedBits(21, 3)
                .WithTaggedFlag("QDENY_GPU", 24)
                .WithTaggedFlag("QDENY_AXI_SLV", 25)
                .WithTaggedFlag("QDENY_AXI_MST", 26)
                .WithTaggedFlag("QDENY_ACE_SLV", 27)
                .WithTaggedFlag("QDENY_ACE_MST", 28)
                .WithReservedBits(29, 3)
            );

            registerMap.Add((long)Registers.General0, new DoubleWordRegister(this)
                .WithValueField(0, 32, name: "GPREG0")
            );

            registerMap.Add((long)Registers.General1, new DoubleWordRegister(this)
                .WithValueField(0, 32, name: "GPREG1")
            );

            registerMap.Add((long)Registers.General2, new DoubleWordRegister(this)
                .WithValueField(0, 32, name: "GPREG2")
            );

            registerMap.Add((long)Registers.General3, new DoubleWordRegister(this)
                .WithValueField(0, 32, name: "GPREG3")
            );
        }

        private void DefineCPGRegisters(Dictionary<long, DoubleWordRegister> registerMap)
        {
            var canClearWatchdogReset = new bool[MaxWatchdogCount];
            registerMap.Add((long)Registers.WDTOverflowSystemReset, new DoubleWordRegister(this)
                .WithFlags(0, MaxWatchdogCount, FieldMode.WriteOneToClear | FieldMode.Read, name: "WDTOVF",
                    valueProviderCallback: (idx, _) => watchdogs[idx]?.GeneratedReset ?? false,
                    writeCallback: (idx, _, val) =>
                    {
                        if(!canClearWatchdogReset[idx])
                        {
                            this.WarningLog("WDT reset clearing is disabled for WDT{0}. Ignoring", idx);
                            return;
                        }

                        if(val && watchdogs[idx] != null)
                        {
                            watchdogs[idx].GeneratedReset = false;
                        }
                    }
                )
                .WithReservedBits(MaxWatchdogCount, 16 - MaxWatchdogCount)
                .WithFlags(16, MaxWatchdogCount, name: "WDTOVF_EN",
                    valueProviderCallback: (_, __) => false,
                    writeCallback: (idx, _, val) =>
                    {
                        if(val)
                        {
                            canClearWatchdogReset[idx] = val;
                        }
                    })
            );

            registerMap.Add((long)Registers.WDTResetSelector, new DoubleWordRegister(this, 0x88)
                .WithFlags(0, MaxWatchdogCount, name: "WDTRSTSEL",
                    valueProviderCallback: (idx, _) => watchdogs[idx]?.SystemResetEnabled ?? false,
                    writeCallback: (idx, _, val) =>
                    {
                        this.DebugLog("System reset for WDT{0}: {1}", idx, val ? "enabled" : "disabled");
                        if(watchdogs[idx] != null)
                        {
                            watchdogs[idx].SystemResetEnabled = val;
                        }
                    }
                )
                .WithReservedBits(3, 1)
                .WithTaggedFlags("WDTRSTSEL", 4, 3)
                .WithReservedBits(7, 1)
                .WithTaggedFlags("WDTRSTSEL", 8, 3)
                .WithReservedBits(11, 21)
            );

            registerMap.Add((long)Registers.CortexA55Core1PowerStatusMonitor, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        var retVal = cortexA55Core1PowerTransitionReq.Value;
                        cortexA55Core1PowerTransitionReq.Value = false;
                        return retVal;
                    },
                    name: "PACCEPT1_MON")
                .WithFlag(1, FieldMode.Read,
                    valueProviderCallback: _ => false,
                    name: "PDENY1_MON")
                .WithReservedBits(2, 30)
            );

            registerMap.Add((long)Registers.CortexA55Core1PowerStatusControl, new DoubleWordRegister(this)
                .WithFlag(0, out cortexA55Core1PowerTransitionReq, name: "PREQ1_SET")
                .WithReservedBits(1, 15)
                .WithEnumField(16, 6, out cortexA55Core1PowerTransitionState, name:"PSTATE1_SET")
                .WithReservedBits(22, 10)
                .WithWriteCallback((_, __) =>
                {
                    if(!cortexA55Core1PowerTransitionReq.Value)
                    {
                        return;
                    }

                    if(!HasTwoCortexA55Cores)
                    {
                        this.WarningLog("Trying to change power state of cpu1, but platform has only cpu0.");
                        return;
                    }
                    switch(cortexA55Core1PowerTransitionState.Value)
                    {
                        case PowerTransitionState.Off:
                        case PowerTransitionState.OffEmulated:
                            cpu1.IsHalted = true;
                            this.DebugLog("Stopping CPU1");
                            break;
                        case PowerTransitionState.On:
                            cpu1.PC = CortexA55Core1ResetVector;
                            cpu1.IsHalted = false;
                            this.DebugLog("Starting CPU1 at 0x{0:X}", CortexA55Core1ResetVector);
                            break;
                        default:
                            this.WarningLog(
                                "Trying to trigger power state transition of cpu1 to invalid state 0x{0:X}",
                                cortexA55Core1PowerTransitionState.Value
                            );
                            break;
                    }
                })
            );

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlCA55, Registers.ClockMonitorCA55,
                Registers.ResetControlCA55, Registers.ResetMonitorCA55, NrOfCa55Clocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlGIC, Registers.ClockMonitorGIC,
                Registers.ResetControlGIC, Registers.ResetMonitorGIC, NrOfGicClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlIA55, Registers.ClockMonitorIA55,
                Registers.ResetControlIA55, Registers.ResetMonitorIA55, NrOfIA55Clocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlMHU, Registers.ClockMonitorMHU,
                Registers.ResetControlMHU, Registers.ResetMonitorMHU, NrOfMhuClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlDMAC, Registers.ClockMonitorDMAC,
                Registers.ResetControlDMAC, Registers.ResetMonitorDMAC, NrOfDmacClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlGTM, Registers.ClockMonitorGTM,
                Registers.ResetControlGTM, Registers.ResetMonitorGTM, NrOfGtmClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlGPT, Registers.ClockMonitorGPT,
                Registers.ResetControlGPT, Registers.ResetMonitorGPT, NrOfGptClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlSCIF, Registers.ClockMonitorSCIF,
                Registers.ResetControlSCIF, Registers.ResetMonitorSCIF, NrOfScifClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlRSPI, Registers.ClockMonitorRSPI,
                Registers.ResetControlRSPI, Registers.ResetMonitorRSPI, NrOfRspiClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlGPIO, Registers.ClockMonitorGPIO,
                Registers.ResetControlGPIO, Registers.ResetMonitorGPIO, NrOfGpioClocks);

            DefineRegistersForPeripheral(registerMap, Registers.ClockControlI2C, Registers.ClockMonitorI2C,
                Registers.ResetControlI2C, Registers.ResetMonitorI2C, NrOfI2cClocks);
        }

        private void DefineRegistersForPeripheral(Dictionary<long, DoubleWordRegister> registerMap, Registers clockControl, Registers clockMonitor,
            Registers resetControl, Registers resetMonitor, int nrOfClocks)
        {
            registerMap.Add((long)clockControl, new DoubleWordRegister(this)
                .WithFlags(0, nrOfClocks, out var clockEnabled, name: "CLK_ON")
                .WithReservedBits(nrOfClocks, 16 - nrOfClocks)
                .WithFlags(16, nrOfClocks, FieldMode.Set | FieldMode.Read,
                    valueProviderCallback: (_, __) => false,
                    name: "CLK_ONWEN")
                .WithReservedBits(16 + nrOfClocks, 16 - nrOfClocks)
                // We have to use register write callback,
                // because multiple fields, depending on each other,
                // can be changed in one write.
                .WithWriteCallback(CreateClockControlWriteCallback(clockControl, clockEnabled))
            );

            registerMap.Add((long)clockMonitor, new DoubleWordRegister(this)
                .WithFlags(0, nrOfClocks, FieldMode.Read,
                    valueProviderCallback: CreateClockMonitorValueProviderCallback(clockEnabled),
                    name: "CLK_MON")
                .WithReservedBits(nrOfClocks, 32 - nrOfClocks)
            );

            // We don't really implement resets, but we still should log
            // if we write to register when write is disabled.
            registerMap.Add((long)resetControl, new DoubleWordRegister(this)
                .WithFlags(0, nrOfClocks, out var resetApplied,
                    valueProviderCallback: (_, __) => false,
                    name: "UNIT_RSTB")
                .WithReservedBits(nrOfClocks, 16 - nrOfClocks)
                .WithFlags(16, nrOfClocks, FieldMode.Set | FieldMode.Read,
                    valueProviderCallback: (_, __) => false,
                    name: "UNIT_RSTWEN")
                .WithReservedBits(16 + nrOfClocks, 16 - nrOfClocks)
                // We have to use register write callback,
                // because multiple fields, depending on each other,
                // can be changed in one write.
                .WithWriteCallback(CreateResetControlWriteCallback(resetControl, resetApplied))
            );

            // Reset is instantenous, so we always return 0, which means that we aren't in reset state.
            registerMap.Add((long)resetMonitor, new DoubleWordRegister(this)
                .WithFlags(0, nrOfClocks, FieldMode.Read,
                    valueProviderCallback: CreateResetMonitorValueProviderCallback(resetApplied),
                    name: "RST_MON")
                .WithReservedBits(nrOfClocks, 32 - nrOfClocks)
            );
        }

        private Action<uint, uint> CreateClockControlWriteCallback(Registers register, IFlagRegisterField[] clockEnable)
        {
            return (oldVal, newVal) =>
            {
                var invalidClocks = GetInvalidBits(oldVal, newVal, ClockEnableBitsOffset,
                    ClockEnableBitsSize, ClockWriteEnableBitsOffset, ClockWriteEnableBitsSize);

                if(invalidClocks != 0)
                {
                    BitHelper.ForeachActiveBit(invalidClocks, clockIdx =>
                    {
                        this.WarningLog(
                            "Trying to toggle clock {0} in register {1}. Writing to this clock is disabled. Clock status won't be changed.",
                            clockIdx,
                            register
                        );
                        clockEnable[clockIdx].Value = !clockEnable[clockIdx].Value;
                    });
                }
            };
         }

        private Action<uint, uint> CreateResetControlWriteCallback(Registers register, IFlagRegisterField[] resetApplied)
        {
            return (oldVal, newVal) =>
            {
                var invalidResets = GetInvalidBits(oldVal, newVal, ResetEnableBitsOffset,
                    ResetEnableBitsSize, ResetWriteEnableBitsOffset, ResetWriteEnableBitsSize);

                if(invalidResets != 0)
                {
                    BitHelper.ForeachActiveBit(invalidResets, resetIdx =>
                    {
                        this.WarningLog(
                            "Trying to toggle reset signal {0} in register {1}. Writing to this signal is disabled. Signal status won't be changed.",
                            resetIdx,
                            register
                        );
                        resetApplied[resetIdx].Value = !resetApplied[resetIdx].Value;
                    });
                }
            };
        }

        private DoubleWordRegister CreateWatchdogTimerControlRegister(int id)
        {
            return new DoubleWordRegister(this, 0x00010000)
                .WithFlag(0, name: "WDTSTOP",
                    valueProviderCallback: _ => watchdogs[id]?.ForceStop ?? false,
                    writeCallback: (_, val) =>
                    {
                        if(watchdogs[id] != null)
                        {
                            watchdogs[id].ForceStop = val;
                        }
                    }
                )
                .WithReservedBits(1, 15)
                .WithTaggedFlag("WDTSTOPMASK", 16)
                .WithReservedBits(17, 15);
        }

        private Func<int, bool, bool> CreateClockMonitorValueProviderCallback(IFlagRegisterField[] clockEnabled)
        {
            return (clockIdx, _) => clockEnabled[clockIdx].Value;
        }

        private Func<int, bool, bool> CreateResetMonitorValueProviderCallback(IFlagRegisterField[] resetApplied)
        {
            return (resetIdx, _) =>
            {
                var retVal = !resetApplied[resetIdx].Value;
                resetApplied[resetIdx].Value = true;
                return retVal;
            };
        }

        private uint GetInvalidBits(uint oldVal, uint newVal, int valueOffset, int valueSize, int maskOffset, int maskSize)
        {
            var mask = BitHelper.GetValue(newVal, maskOffset, maskSize);
            oldVal = BitHelper.GetValue(oldVal, valueOffset, valueSize);
            newVal = BitHelper.GetValue(newVal, valueOffset, valueSize);

            // We mark as invalid, bits that changed, but were not masked.
            var changed = oldVal ^ newVal;
            var invalid = changed & ~mask;

            return invalid;
        }

        private ulong CortexA55Core1ResetVector => (cortexA55Core1ResetVectorLow.Value << 2) | (cortexA55Core1ResetVectorHigh.Value << 32);
        private bool HasTwoCortexA55Cores => cpu1 != null;

        private IFlagRegisterField cortexA55Core1PowerTransitionReq;
        private IEnumRegisterField<PowerTransitionState> cortexA55Core1PowerTransitionState;
        private IValueRegisterField cortexA55Core1ResetVectorLow;
        private IValueRegisterField cortexA55Core1ResetVectorHigh;

        private RenesasRZG_Watchdog[] watchdogs = new RenesasRZG_Watchdog[MaxWatchdogCount];

        private readonly ICPU cpu0;
        private readonly ICPU cpu1;

        private const int NrOfCa55Clocks = 13;
        private const int NrOfGicClocks = 2;
        private const int NrOfIA55Clocks = 2;
        private const int NrOfMhuClocks = 1;
        private const int NrOfDmacClocks = 2;
        private const int NrOfGtmClocks = 3;
        private const int NrOfGptClocks = 1;
        private const int NrOfI2cClocks = 4;
        private const int NrOfScifClocks = 5;
        private const int NrOfRspiClocks = 3;
        private const int NrOfGpioClocks = 1;

        private const int ClockEnableBitsOffset = 0;
        private const int ClockEnableBitsSize = 16;
        private const int ClockWriteEnableBitsOffset = 16;
        private const int ClockWriteEnableBitsSize = 16;
        private const int ResetEnableBitsOffset = 0;
        private const int ResetEnableBitsSize = 16;
        private const int ResetWriteEnableBitsOffset = 16;
        private const int ResetWriteEnableBitsSize = 16;
        private const int MaxWatchdogCount = 3;

        private const long CPGOffset = 0;
        private const long SYSCOffset = 0x10_000;

        private enum PowerTransitionState
        {
            Off = 0x0,
            OffEmulated = 0x1,
            On = 0x8,
        }

        private enum Registers : long
        {
            WDTOverflowSystemReset                     = 0xB10 + CPGOffset, // CPG_WDTOVF_RST
            WDTResetSelector                           = 0xB14 + CPGOffset, // CPG_WDTRST_SEL
            CortexA55Core1PowerStatusMonitor           = 0xB40 + CPGOffset, // CPG_CORE1_PCHMON
            CortexA55Core1PowerStatusControl           = 0xB44 + CPGOffset, // CPG_CORE1_PCHCTL

            // Clock Control
            ClockControlCA55                           = 0x500 + CPGOffset, // CPG_CLKON_CA55
            ClockControlGIC                            = 0x514 + CPGOffset, // CPG_CLKON_GIC600
            ClockControlIA55                           = 0x518 + CPGOffset, // CPG_CLKON_IA55
            ClockControlMHU                            = 0x520 + CPGOffset, // CPG_CLKON_MHU
            ClockControlDMAC                           = 0x52C + CPGOffset, // CPG_CLKON_DAMC_REG
            ClockControlGTM                            = 0x534 + CPGOffset, // CPG_CLKON_GTM
            ClockControlGPT                            = 0x540 + CPGOffset, // CPG_CLKON_GPT
            ClockControlI2C                            = 0x580 + CPGOffset, // CPG_CLKON_I2C
            ClockControlSCIF                           = 0x584 + CPGOffset, // CPG_CLKON_SCIF
            ClockControlRSPI                           = 0x590 + CPGOffset, // CPG_CLKON_RSPI
            ClockControlGPIO                           = 0x598 + CPGOffset, // CPG_CLKON_GPIO

            // Clock Monitor
            ClockMonitorCA55                           = 0x680 + CPGOffset, // CPG_CLMON_CA55
            ClockMonitorGIC                            = 0x694 + CPGOffset, // CPG_CLKMON_GIC600
            ClockMonitorIA55                           = 0x698 + CPGOffset, // CPG_CLKMON_IA55
            ClockMonitorMHU                            = 0x6A0 + CPGOffset, // CPG_CLMON_MHU
            ClockMonitorDMAC                           = 0x6AC + CPGOffset, // CPG_CLKMON_DAMC_REG
            ClockMonitorGTM                            = 0x6B4 + CPGOffset, // CPG_CLMON_GTM
            ClockMonitorGPT                            = 0x6C0 + CPGOffset, // CPG_CLMON_GPT
            ClockMonitorI2C                            = 0x700 + CPGOffset, // CPG_CLMON_I2C
            ClockMonitorSCIF                           = 0x704 + CPGOffset, // CPG_CLMON_SCIF
            ClockMonitorRSPI                           = 0x710 + CPGOffset, // CPG_CLMON_RSPI
            ClockMonitorGPIO                           = 0x718 + CPGOffset, // CPG_CLMON_GPIO

            // Reset Control
            ResetControlCA55                           = 0x800 + CPGOffset, // CPG_RST_CA55
            ResetControlGIC                            = 0x814 + CPGOffset, // CPG_RST_GIC600
            ResetControlIA55                           = 0x818 + CPGOffset, // CPG_RST_IA55
            ResetControlMHU                            = 0x820 + CPGOffset, // CPG_RST_MHU
            ResetControlDMAC                           = 0x82C + CPGOffset, // CPG_RST_DMAC
            ResetControlGTM                            = 0x834 + CPGOffset, // CPG_RST_GTM
            ResetControlGPT                            = 0x844 + CPGOffset, // CPG_RST_GPT
            ResetControlI2C                            = 0x880 + CPGOffset, // CPG_RST_I2C
            ResetControlSCIF                           = 0x884 + CPGOffset, // CPG_RST_SCIF
            ResetControlRSPI                           = 0x890 + CPGOffset, // CPG_RST_RSPI
            ResetControlGPIO                           = 0x898 + CPGOffset, // CPG_RST_GPIO

            // Reset Monitor
            ResetMonitorCA55                           = 0x980 + CPGOffset, // CPG_RSTMON_CA55
            ResetMonitorGIC                            = 0x994 + CPGOffset, // CPG_RSTMON_GIC600
            ResetMonitorIA55                           = 0x998 + CPGOffset, // CPG_RSTMON_IA55
            ResetMonitorMHU                            = 0x9A0 + CPGOffset, // CPG_RSTMON_MHU
            ResetMonitorDMAC                           = 0x9AC + CPGOffset, // CPG_RSTMON_DMAC
            ResetMonitorGTM                            = 0x9B4 + CPGOffset, // CPG_RSTMON_GTM
            ResetMonitorGPT                            = 0x9C0 + CPGOffset, // CPG_RSTMON_GPT
            ResetMonitorI2C                            = 0xA00 + CPGOffset, // CPG_RSTMON_I2C
            ResetMonitorSCIF                           = 0xA04 + CPGOffset, // CPG_RSTMON_SCIF
            ResetMonitorRSPI                           = 0xA10 + CPGOffset, // CPG_RSTMON_RSPI
            ResetMonitorGPIO                           = 0xA18 + CPGOffset, // CPG_RSTMON_GPIO

            MasterAccessControl0                       = 0x000 + SYSCOffset,
            MasterAccessControl1                       = 0x004 + SYSCOffset,
            MasterAccessControl2                       = 0x008 + SYSCOffset,
            MasterAccessControl3                       = 0x00C + SYSCOffset,
            MasterAccessControl4                       = 0x010 + SYSCOffset,
            SlaveAccessControl0                        = 0x100 + SYSCOffset,
            SlaveAccessControl1                        = 0x104 + SYSCOffset,
            SlaveAccessControl2                        = 0x108 + SYSCOffset,
            SlaveAccessControl3                        = 0x10C + SYSCOffset,
            SlaveAccessControl4                        = 0x110 + SYSCOffset,
            SlaveAccessControl5                        = 0x114 + SYSCOffset,
            SlaveAccessControl6                        = 0x118 + SYSCOffset,
            SlaveAccessControl7                        = 0x11C + SYSCOffset,
            SlaveAccessControl8                        = 0x120 + SYSCOffset,
            SlaveAccessControl10                       = 0x128 + SYSCOffset,
            SlaveAccessControl12                       = 0x130 + SYSCOffset,
            SlaveAccessControl13                       = 0x134 + SYSCOffset,
            SlaveAccessControl14                       = 0x138 + SYSCOffset,
            ErrorCorrectingCodeRam0Settings            = 0x200 + SYSCOffset,
            ErrorCorrectingCodeRam0AccessControl       = 0x204 + SYSCOffset,
            ErrorCorrectingCodeRam1Settings            = 0x210 + SYSCOffset,
            ErrorCorrectingCodeRam1AccessControl       = 0x214 + SYSCOffset,
            WatchdogTimer0Control                      = 0x220 + SYSCOffset,
            WatchdogTimer1Control                      = 0x230 + SYSCOffset,
            WatchdogTimer2Control                      = 0x240 + SYSCOffset,
            GigabitEthernet0Config                     = 0x330 + SYSCOffset,
            GigabitEthernet1Config                     = 0x340 + SYSCOffset,
            I2c0Config                                 = 0x400 + SYSCOffset,
            I2c1Config                                 = 0x410 + SYSCOffset,
            I2c2Config                                 = 0x420 + SYSCOffset,
            I2c3Config                                 = 0x430 + SYSCOffset,
            CortexM33Config0                           = 0x804 + SYSCOffset,
            CortexM33Config1                           = 0x808 + SYSCOffset,
            CortexM33Config2                           = 0x80C + SYSCOffset,
            CortexM33Config3                           = 0x810 + SYSCOffset,
            CortexM33Lock                              = 0x814 + SYSCOffset,
            CortexA55Core0ResetVectorAddressLowConfig  = 0x858 + SYSCOffset,
            CortexA55Core0ResetVectorAddressHighConfig = 0x85C + SYSCOffset,
            CortexA55Core1ResetVectorAddressLowConfig  = 0x860 + SYSCOffset,
            CortexA55Core1ResetVectorAddressHighConfig = 0x864 + SYSCOffset,
            LsiModeSignal                              = 0xA00 + SYSCOffset,
            LsiDeviceId                                = 0xA04 + SYSCOffset,
            LsiProduct                                 = 0xA08 + SYSCOffset,
            AddressOffset0                             = 0xC00 + SYSCOffset,
            AddressOffset1                             = 0xC04 + SYSCOffset,
            AddressOffset2                             = 0xC08 + SYSCOffset,
            AddressOffset3                             = 0xC0C + SYSCOffset,
            AddressOffset4                             = 0xC10 + SYSCOffset,
            AddressOffset5                             = 0xC14 + SYSCOffset,
            AddressOffset6                             = 0xC18 + SYSCOffset,
            LowPowerSequenceControl1                   = 0xD04 + SYSCOffset,
            LowPowerSequenceControl2                   = 0xD08 + SYSCOffset,
            LowPowerSequenceControl5                   = 0xD14 + SYSCOffset,
            LowPowerSequenceControl6                   = 0xD18 + SYSCOffset,
            LowPowerSequenceControl7                   = 0xD1C + SYSCOffset,
            LowPowerSequenceCortexM33Control0          = 0xD24 + SYSCOffset,
            CortexA55ClockControl1                     = 0xD38 + SYSCOffset,
            CortexA55ClockControl2                     = 0xD3C + SYSCOffset,
            CortexA55ClockControl3                     = 0xD40 + SYSCOffset,
            GpuLowPowerSequenceControl                 = 0xD50 + SYSCOffset,
            General0                                   = 0xE00 + SYSCOffset,
            General1                                   = 0xE04 + SYSCOffset,
            General2                                   = 0xE08 + SYSCOffset,
            General3                                   = 0xE0C + SYSCOffset,
        }
    }
}
