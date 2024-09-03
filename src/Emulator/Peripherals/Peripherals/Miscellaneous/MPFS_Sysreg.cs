//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MPFS_Sysreg : IDoubleWordPeripheral, IKnownSize
    {
        public MPFS_Sysreg(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);

            peripheralMap = new PeripheralDisableInfo[]
            {
                new PeripheralDisableInfo { Address = 0x20200000, Name = "ENVM" },
                new PeripheralDisableInfo { Address = 0x20110000, Name = "MAC0" },
                new PeripheralDisableInfo { Address = 0x20112000, Name = "MAC1" },
                new PeripheralDisableInfo { Address = 0x20008000, Name = "MMC" },
                new PeripheralDisableInfo { Address = 0x20125000, Name = "TIMER" },
                new PeripheralDisableInfo { Address = 0x20000000, Name = "MMUART0" },
                new PeripheralDisableInfo { Address = 0x20100000, Name = "MMUART1" },
                new PeripheralDisableInfo { Address = 0x20102000, Name = "MMUART2" },
                new PeripheralDisableInfo { Address = 0x20104000, Name = "MMUART3" },
                new PeripheralDisableInfo { Address = 0x20106000, Name = "MMUART4" },
                new PeripheralDisableInfo { Address = 0x20108000, Name = "SPI0" },
                new PeripheralDisableInfo { Address = 0x20109000, Name = "SPI1" },
                new PeripheralDisableInfo { Address = 0x2010A000, Name = "I2C0" },
                new PeripheralDisableInfo { Address = 0x2010B000, Name = "I2C1" },
                new PeripheralDisableInfo { Address = 0x2010C000, Name = "CAN0" },
                new PeripheralDisableInfo { Address = 0x2010D000, Name = "CAN1" },
                new PeripheralDisableInfo { Address = 0x20201000, Name = "USB" },
                new PeripheralDisableInfo { Address = null, Name = "FPGA" },
                new PeripheralDisableInfo { Address = 0x20124000, Name = "MSRTC" },
                new PeripheralDisableInfo { Address = 0x21000000, Name = "QSPI" },
                new PeripheralDisableInfo { Address = 0x20120000, Name = "GPIO0" },
                new PeripheralDisableInfo { Address = 0x20121000, Name = "GPIO1" },
                new PeripheralDisableInfo { Address = 0x20122000, Name = "GPIO2" },
                new PeripheralDisableInfo { Address = 0x20080000, Name = "DDRC" },
                new PeripheralDisableInfo { Address = null, Name = "FIC0" },
                new PeripheralDisableInfo { Address = null, Name = "FIC1" },
                new PeripheralDisableInfo { Address = null, Name = "FIC2" },
                new PeripheralDisableInfo { Address = null, Name = "FIC3" },
                new PeripheralDisableInfo { Address = 0x22000000, Name = "ATHENA" },
                new PeripheralDisableInfo { Address = null, Name = "CFM" },
                new PeripheralDisableInfo { Address = null, Name = "SGMII" },
            };

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                // Enables the clock to the MSS peripheral. When the clock is off the peripheral should not be accessed.
                {(long)Registers.SubblkClockCr, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) => ManageClock(val, 0), name: "ENVM")
                    .WithFlag(1, writeCallback: (_, val) => ManageClock(val, 1), name: "MAC0")
                    .WithFlag(2, writeCallback: (_, val) => ManageClock(val, 2), name: "MAC1")
                    .WithFlag(3, writeCallback: (_, val) => ManageClock(val, 3), name: "MMC")
                    .WithFlag(4, writeCallback: (_, val) => ManageClock(val, 4), name: "TIMER")
                    .WithFlag(5, writeCallback: (_, val) => ManageClock(val, 5), name: "MMUART0")
                    .WithFlag(6, writeCallback: (_, val) => ManageClock(val, 6), name: "MMUART1")
                    .WithFlag(7, writeCallback: (_, val) => ManageClock(val, 7), name: "MMUART2")
                    .WithFlag(8, writeCallback: (_, val) => ManageClock(val, 8), name: "MMUART3")
                    .WithFlag(9, writeCallback: (_, val) => ManageClock(val, 9), name: "MMUART4")
                    .WithFlag(10, writeCallback: (_, val) => ManageClock(val, 10), name: "SPI0")
                    .WithFlag(11, writeCallback: (_, val) => ManageClock(val, 11), name: "SPI1")
                    .WithFlag(12, writeCallback: (_, val) => ManageClock(val, 12), name: "I2C0")
                    .WithFlag(13, writeCallback: (_, val) => ManageClock(val, 13), name: "I2C1")
                    .WithFlag(14, writeCallback: (_, val) => ManageClock(val, 14), name: "CAN0")
                    .WithFlag(15, writeCallback: (_, val) => ManageClock(val, 15), name: "CAN1")
                    .WithFlag(16, writeCallback: (_, val) => ManageClock(val, 16), name: "USB")
                    .WithTag("FPGA", 17, 1)
                    .WithFlag(18, writeCallback: (_, val) => ManageClock(val, 18), name: "MSRTC")
                    .WithFlag(19, writeCallback: (_, val) => ManageClock(val, 19), name: "QSPI")
                    .WithFlag(20, writeCallback: (_, val) => ManageClock(val, 20), name: "GPIO0")
                    .WithFlag(21, writeCallback: (_, val) => ManageClock(val, 21), name: "GPIO1")
                    .WithFlag(22, writeCallback: (_, val) => ManageClock(val, 22), name: "GPIO2")
                    .WithFlag(23, writeCallback: (_, val) => ManageClock(val, 23), name: "DDRC")
                    .WithTag("FIC0", 24, 1)
                    .WithTag("FIC1", 25, 1)
                    .WithTag("FIC2", 26, 1)
                    .WithTag("FIC3", 27, 1)
                    .WithFlag(28, writeCallback: (_, val) => ManageClock(val, 28), name: "ATHENA")
                    .WithTag("CFM", 29, 1)
                    .WithReservedBits(30, 1)
                    .WithReservedBits(31, 1)
                },

                // Holds the MSS peripherals in reset. When in reset the peripheral should not be accessed.
                {(long)Registers.SoftResetCr, new DoubleWordRegister(this, 0x7FFFFFFE)
                    .WithFlag(0, writeCallback: (_, val) => ManageSoftReset(val, 0), name: "ENVM")
                    .WithFlag(1, writeCallback: (_, val) => ManageSoftReset(val, 1), name: "MAC0")
                    .WithFlag(2, writeCallback: (_, val) => ManageSoftReset(val, 2), name: "MAC1")
                    .WithFlag(3, writeCallback: (_, val) => ManageSoftReset(val, 3), name: "MMC")
                    .WithFlag(4, writeCallback: (_, val) => ManageSoftReset(val, 4), name: "TIMER")
                    .WithFlag(5, writeCallback: (_, val) => ManageSoftReset(val, 5), name: "MMUART0")
                    .WithFlag(6, writeCallback: (_, val) => ManageSoftReset(val, 6), name: "MMUART1")
                    .WithFlag(7, writeCallback: (_, val) => ManageSoftReset(val, 7), name: "MMUART2")
                    .WithFlag(8, writeCallback: (_, val) => ManageSoftReset(val, 8), name: "MMUART3")
                    .WithFlag(9, writeCallback: (_, val) => ManageSoftReset(val, 9), name: "MMUART4")
                    .WithFlag(10, writeCallback: (_, val) => ManageSoftReset(val, 10), name: "SPI0")
                    .WithFlag(11, writeCallback: (_, val) => ManageSoftReset(val, 11), name: "SPI1")
                    .WithFlag(12, writeCallback: (_, val) => ManageSoftReset(val, 12), name: "I2C0")
                    .WithFlag(13, writeCallback: (_, val) => ManageSoftReset(val, 13), name: "I2C1")
                    .WithFlag(14, writeCallback: (_, val) => ManageSoftReset(val, 14), name: "CAN0")
                    .WithFlag(15, writeCallback: (_, val) => ManageSoftReset(val, 15), name: "CAN1")
                    .WithFlag(16, writeCallback: (_, val) => ManageSoftReset(val, 16), name: "USB")
                    .WithTag("FPGA", 17, 1)
                    .WithFlag(18, writeCallback: (_, val) => ManageSoftReset(val, 18), name: "MSRTC")
                    .WithFlag(19, writeCallback: (_, val) => ManageSoftReset(val, 19), name: "QSPI")
                    .WithFlag(20, writeCallback: (_, val) => ManageSoftReset(val, 20), name: "GPIO0")
                    .WithFlag(21, writeCallback: (_, val) => ManageSoftReset(val, 21), name: "GPIO1")
                    .WithFlag(22, writeCallback: (_, val) => ManageSoftReset(val, 22), name: "GPIO2")
                    .WithFlag(23, writeCallback: (_, val) => ManageSoftReset(val, 23), name: "DDRC")
                    .WithTag("FIC0", 24, 1)
                    .WithTag("FIC1", 25, 1)
                    .WithTag("FIC2", 26, 1)
                    .WithTag("FIC3", 27, 1)
                    .WithFlag(28, writeCallback: (_, val) => ManageSoftReset(val, 28), name: "ATHENA")
                    .WithTag("CFM", 29, 1)
                    .WithTag("SGMII", 30, 1)
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.ClockConfigCr, new DoubleWordRegister(this, 0x10)
                    .WithTag("ClockConfig", 0, 32)
                },
                {(long)Registers.EnvmCr, new DoubleWordRegister(this, 0xFF)
                    .WithTag("Envm", 0, 32)
                },
                {(long)Registers.RtcClockCr, new DoubleWordRegister(this, 0x1064)
                    .WithTag("RtcClock", 0, 32)
                },
                {(long)Registers.PllStatusSr, new DoubleWordRegister(this, 0x707)
                    .WithTag("PllStatus", 0, 32)
                },
                {(long)Registers.EdacSr, new DoubleWordRegister(this)
                    .WithTag("Edac", 0, 32)
                },
                {(long)Registers.EdacIntenCr, new DoubleWordRegister(this)
                    .WithTag("EdacInten", 0, 32)
                },
                {(long)Registers.EdacCntMmc, new DoubleWordRegister(this)
                    .WithTag("EdacCntMmc", 0, 32)
                },
                {(long)Registers.EdacCntDdrc, new DoubleWordRegister(this)
                    .WithTag("EdacCntDrdc", 0, 32)
                },
                {(long)Registers.EdacCntMac0, new DoubleWordRegister(this)
                    .WithTag("EdacCntMac0", 0, 32)
                },
                {(long)Registers.EdacCntMac1, new DoubleWordRegister(this)
                    .WithTag("EdacCntMac1", 0, 32)
                },
                {(long)Registers.EdacCntUsb, new DoubleWordRegister(this)
                    .WithTag("EdacCntUsb", 0, 32)
                },
                {(long)Registers.EdacCntCan0, new DoubleWordRegister(this)
                    .WithTag("EdacCntCan0", 0, 32)
                },
                {(long)Registers.EdacCntCan1, new DoubleWordRegister(this)
                    .WithTag("EdacCntCan1", 0, 32)
                },
                {(long)Registers.MaintenanceIntSr, new DoubleWordRegister(this)
                    .WithTag("MaintenanceInt", 0, 32)
                },
                {(long)Registers.MiscSr, new DoubleWordRegister(this)
                    .WithTag("Misc", 0, 32)
                },
                {(long)Registers.DLLStatusSr, new DoubleWordRegister(this)
                    .WithTag("DLLStatus", 0, 32)
                },
                {(long)Registers.BootFailC, new DoubleWordRegister(this)
                    .WithTag("BootFail", 0, 32)
                },
                {(long)Registers.DeviceStatus, new DoubleWordRegister(this, 0x1F09)
                    .WithTag("Devicestatus", 0, 32)
                },
                {(long)Registers.MpuViolationSr, new DoubleWordRegister(this)
                    .WithTag("MpuViolation", 0, 32)
                },
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public long Size => 0x1000;

        private void ManageClock(bool val, int index)
        {
            var info = peripheralMap[index];
            if(val)
            {
                info.Type &= ~(DisableType.Clock);
            }
            else
            {
                info.Type |= DisableType.Clock;
            }
            ManagePeripheral(info);
        }

        private void ManageSoftReset(bool val, int index)
        {
            var info = peripheralMap[index];
            if(val)
            {
                info.Type |= DisableType.SoftReset;
            }
            else
            {
                info.Type &= ~(DisableType.SoftReset);
            }
            ManagePeripheral(info);
        }

        private void ManagePeripheral(PeripheralDisableInfo info)
        {
            if(!info.Address.HasValue)
            {
                this.Log(LogLevel.Warning, "Cannot manage peripheral {0} because of invalid address.", info.Name);
                return;
            }
            var peripheral = sysbus.WhatPeripheralIsAt(info.Address.Value);
            if(peripheral == null)
            {
                this.Log(LogLevel.Warning, "Cannot manage peripheral {0} because it is not registered.", info.Name);
                return;
            }
            if((info.Type & DisableType.Clock) == 0)
            {
                this.Log(LogLevel.Debug, "Enabling peripheral {0}.", info.Name);
                sysbus.EnablePeripheral(peripheral);
            }
            else
            {
                this.Log(LogLevel.Debug, "Disabling peripheral {0}.", info.Name);
                sysbus.DisablePeripheral(peripheral);
            }
            if((info.Type & DisableType.SoftReset) != 0)
            {
                this.Log(LogLevel.Debug, "Resetting peripheral {0}.", info.Name);
                peripheral.Reset();
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly PeripheralDisableInfo[] peripheralMap;
        private readonly IBusController sysbus;

        private struct PeripheralDisableInfo
        {
            public ulong? Address;
            public DisableType Type;
            public string Name;
        }

        [Flags]
        private enum DisableType
        {
            None = 0,
            Clock = 0x1,
            SoftReset = 0x2
        }

        private enum Registers
        {
            Temp0 = 0x0,
            Temp1 = 0x4,
            ClockConfigCr = 0x8,
            RtcClockCr = 0xC,
            FabricResetCr = 0x10,
            BootFailC = 0x14,
            MssLowPowerCr = 0x18,
            ConfigLockCr = 0x1C,
            ResetSr = 0x20,
            DeviceStatus = 0x24,
            FabIntenU541 = 0x40,
            FabIntenU542 = 0x44,
            FabIntenU543 = 0x48,
            FabIntenU544 = 0x4C,
            FabIntenMisc = 0x50,
            GpioInterruptFabCr = 0x54,
            ApbbusCr = 0x80,
            SubblkClockCr = 0x84,
            SoftResetCr = 0x88,
            AhbaxiCr = 0x8C,
            DfiapbCr = 0x98,
            GpioCr = 0x9C,
            Mac0Cr = 0xA4,
            Mac1Cr = 0xA8,
            UsbCr = 0xAC,
            MeshCr = 0xB0,
            MeshSeedCr = 0xB4,
            EnvmCr = 0xB8,
            ReservedBc = 0xBC,
            QosPeripheralCr = 0xC0,
            QosCplexioCr = 0xC4,
            QosCplexddrCr = 0xC8,
            MpuViolationSr = 0xF0,
            MpuViolationIntenCr = 0xF4,
            SwFailAddr0Cr = 0xF8,
            SwFailAddr1Cr = 0xFC,
            EdacSr = 0x100,
            EdacIntenCr = 0x104,
            EdacCntMmc = 0x108,
            EdacCntDdrc = 0x10C,
            EdacCntMac0 = 0x110,
            EdacCntMac1 = 0x114,
            EdacCntUsb = 0x118,
            EdacCntCan0 = 0x11C,
            EdacCntCan1 = 0x120,
            EdacInjectCr = 0x124,
            MaintenanceIntenCr = 0x140,
            PllStatusIntenCr = 0x144,
            MaintenanceIntSr = 0x148,
            PllStatusSr = 0x14C,
            CfmTimerCr = 0x150,
            MiscSr = 0x154,
            DLLStatusSr = 0x15C,
            RamLightsleepCr = 0x168,
            RamDeepsleepCr = 0x16C,
            RamShutdownCr = 0x170,
            Iomux0Cr = 0x200,
            Iomux1Cr = 0x204,
            Iomux2Cr = 0x208,
            Iomux3Cr = 0x20C,
            Iomux4Cr = 0x210,
            Iomux5Cr = 0x214,
            Iomux6Cr = 0x218,
            MssioBank4CfgCr = 0x230,
            MssioBank4IoCfg0Cr = 0x234,
            MssioBank4IoCfg1Cr = 0x238,
            MssioBank4IoCfg2Cr = 0x23C,
            MssioBank4IoCfg3Cr = 0x240,
            MssioBank4IoCfg4Cr = 0x244,
            MssioBank4IoCfg5Cr = 0x248,
            MssioBank4IoCfg6Cr = 0x24C,
            MssioBank2CfgCr = 0x250,
            MssioBank2IoCfg0Cr = 0x254,
            MssioBank2IoCfg1Cr = 0x258,
            MssioBank2IoCfg2Cr = 0x25C,
            MssioBank2IoCfg3Cr = 0x260,
            MssioBank2IoCfg4Cr = 0x264,
            MssioBank2IoCfg5Cr = 0x268,
            MssioBank2IoCfg6Cr = 0x26C,
            MssioBank2IoCfg7Cr = 0x270,
            MssioBank2IoCfg8Cr = 0x274,
            MssioBank2IoCfg9Cr = 0x278,
            MssioBank2IoCfg10Cr = 0x27C,
            MssioBank2IoCfg11Cr = 0x280,
            MssSpare0Cr = 0x2A8,
            MssSpare1Cr = 0x2AC,
            MssSpare0Sr = 0x2B0,
            MssSpare1Sr = 0x2B4,
            MssSpare2Sr = 0x2B8,
            MssSpare3Sr = 0x2BC,
            MssSpare4Sr = 0x2C0,
            MssSpare5Sr = 0x2C4,
            SpareRegisterRw = 0x2D0,
            SpareRegisterW1p = 0x2D4,
            SpareRegisterRo = 0x2D8,
            SparePerimRw = 0x2DC
        }
    }
}
