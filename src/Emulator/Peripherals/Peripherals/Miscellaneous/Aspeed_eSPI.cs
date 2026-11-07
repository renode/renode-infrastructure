//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT license.
//
// AST2600 eSPI Controller — Renode model
// Ported from QEMU hw/misc/aspeed_espi.c
//
// Implements all 4 eSPI channels (Peripheral, Virtual Wire, OOB, Flash),
// FIFO-based TX/RX, DMA address registers, W1C interrupt status,
// capability registers, MMBI, and VW system event handling.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord |
                         AllowedTranslation.WordToDoubleWord)]
    public class Aspeed_eSPI : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_eSPI(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();

            pcRxBuf = new byte[FifoSize];
            pcTxBuf = new byte[FifoSize];
            npTxBuf = new byte[FifoSize];
            oobRxBuf = new byte[FifoSize];
            oobTxBuf = new byte[FifoSize];
            flashRxBuf = new byte[FifoSize];
            flashTxBuf = new byte[FifoSize];

            registers = CreateRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            // FIFO read registers need special handling
            switch((uint)offset)
            {
                case 0x018: // PERIF_PC_RX_DATA
                    if((ctrlValue & CtrlPerifPcRxDmaEn) != 0) return 0;
                    if(pcRxPos < pcRxLen) return pcRxBuf[pcRxPos++];
                    return 0;

                case 0x048: // OOB_RX_DATA
                    if((ctrlValue & CtrlOobRxDmaEn) != 0) return 0;
                    if(oobRxPos < oobRxLen) return oobRxBuf[oobRxPos++];
                    return 0;

                case 0x068: // FLASH_RX_DATA
                    if((ctrlValue & CtrlFlashRxDmaEn) != 0) return 0;
                    if(flashRxPos < flashRxLen) return flashRxBuf[flashRxPos++];
                    return 0;
            }

            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // FIFO write registers need special handling
            switch((uint)offset)
            {
                case 0x028: // PERIF_PC_TX_DATA
                    if((ctrlValue & CtrlPerifPcTxDmaEn) == 0 && pcTxLen < FifoSize)
                        pcTxBuf[pcTxLen++] = (byte)value;
                    return;

                case 0x038: // PERIF_NP_TX_DATA
                    if((ctrlValue & CtrlPerifNpTxDmaEn) == 0 && npTxLen < FifoSize)
                        npTxBuf[npTxLen++] = (byte)value;
                    return;

                case 0x058: // OOB_TX_DATA
                    if((ctrlValue & CtrlOobTxDmaEn) == 0 && oobTxLen < FifoSize)
                        oobTxBuf[oobTxLen++] = (byte)value;
                    return;

                case 0x078: // FLASH_TX_DATA
                    if((ctrlValue & CtrlFlashTxDmaEn) == 0 && flashTxLen < FifoSize)
                        flashTxBuf[flashTxLen++] = (byte)value;
                    return;
            }

            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();

            // Set capability reset values (read-only, set after register reset)
            genCapValue = GenCapReset;
            ch0CapValue = Ch0CapReset;
            ch1CapValue = Ch1CapReset;
            ch2CapValue = Ch2CapReset;
            ch3CapValue = Ch3CapReset;
            ch3Cap2Value = Ch3Cap2Reset;

            // CTRL2 default: MCYC read/write disabled
            ctrl2Value = Ctrl2McycRdDis | Ctrl2McycWrDis;

            // SYSEVT: PLTRST# deasserted by default (host powered on)
            sysevtValue = SysevtPltrst;

            // INT_STS: RST_DEASSERT set (eSPI link up)
            intStsValue = IntRstDeassert;

            // Reset FIFO state
            ResetPerifPcRx();
            ResetPerifPcTx();
            ResetPerifNpTx();
            ResetOobRx();
            ResetOobTx();
            ResetFlashRx();
            ResetFlashTx();

            sysevt1Value = 0;
            sysevtIntEnValue = 0;
            sysevtIntStsValue = 0;
            sysevt1IntEnValue = 0;
            sysevt1IntStsValue = 0;
            intEnValue = 0;
            ctrlValue = 0;
            mmbiCtrlValue = 0;
            mmbiIntStsValue = 0;
            mmbiIntEnValue = 0;

            UpdateIrq();
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; }

        // --- Public injection methods for co-simulation ---

        /// <summary>
        /// Inject a peripheral channel RX packet (host → BMC).
        /// </summary>
        public void InjectPerifPcRx(byte cycleType, byte tag, byte[] data)
        {
            uint len = (uint)(data?.Length ?? 0);
            if(len > FifoSize)
            {
                this.Log(LogLevel.Warning, "PC RX packet too large ({0} > {1})", len, FifoSize);
                len = FifoSize;
            }

            if(data != null)
            {
                Array.Copy(data, 0, pcRxBuf, 0, (int)len);
            }
            pcRxLen = len;
            pcRxPos = 0;

            pcRxCtrlValue = ServPend | PackCtrl(cycleType, tag, len);

            intStsValue |= IntPerifPcRxCmplt;
            UpdateIrq();
        }

        /// <summary>
        /// Inject an OOB channel RX packet (host → BMC).
        /// </summary>
        public void InjectOobRx(byte cycleType, byte tag, byte[] data)
        {
            uint len = (uint)(data?.Length ?? 0);
            if(len > FifoSize)
            {
                this.Log(LogLevel.Warning, "OOB RX packet too large ({0} > {1})", len, FifoSize);
                len = FifoSize;
            }

            if(data != null)
            {
                Array.Copy(data, 0, oobRxBuf, 0, (int)len);
            }
            oobRxLen = len;
            oobRxPos = 0;

            oobRxCtrlValue = ServPend | PackCtrl(cycleType, tag, len);

            intStsValue |= IntOobRxCmplt;
            UpdateIrq();
        }

        /// <summary>
        /// Inject a Flash channel RX packet (host → BMC).
        /// </summary>
        public void InjectFlashRx(byte cycleType, byte tag, byte[] data)
        {
            uint len = (uint)(data?.Length ?? 0);
            if(len > FifoSize)
            {
                this.Log(LogLevel.Warning, "Flash RX packet too large ({0} > {1})", len, FifoSize);
                len = FifoSize;
            }

            if(data != null)
            {
                Array.Copy(data, 0, flashRxBuf, 0, (int)len);
            }
            flashRxLen = len;
            flashRxPos = 0;

            flashRxCtrlValue = ServPend | PackCtrl(cycleType, tag, len);

            intStsValue |= IntFlashRxCmplt;
            UpdateIrq();
        }

        /// <summary>
        /// Inject host-driven Virtual Wire system events.
        /// Only modifies host-driven bits; preserves slave-driven bits.
        /// </summary>
        public void InjectVwSysevt(uint hostEvents)
        {
            uint oldVal = sysevtValue;
            uint newVal = (hostEvents & SysevtHostDrivenMask) |
                          (oldVal & ~SysevtHostDrivenMask);
            sysevtValue = newVal;

            NotifySysevtChange(oldVal, newVal);
        }

        // --- Private implementation ---

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var regs = new Dictionary<long, DoubleWordRegister>();

            // 0x000 CTRL
            regs[0x000] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "CTRL",
                    writeCallback: (_, val) => HandleCtrlWrite((uint)val),
                    valueProviderCallback: _ => ctrlValue);

            // 0x004 STS (read-only)
            regs[0x004] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "STS",
                    valueProviderCallback: _ => stsValue);

            // 0x008 INT_STS (W1C)
            regs[0x008] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "INT_STS",
                    writeCallback: (_, val) =>
                    {
                        intStsValue &= ~(uint)val;
                        UpdateIrq();
                    },
                    valueProviderCallback: _ => intStsValue);

            // 0x00C INT_EN
            regs[0x00C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "INT_EN",
                    writeCallback: (_, val) =>
                    {
                        intEnValue = (uint)val;
                        UpdateIrq();
                    },
                    valueProviderCallback: _ => intEnValue);

            // 0x010 PERIF_PC_RX_DMA
            regs[0x010] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_PC_RX_DMA",
                    writeCallback: (_, val) => pcRxDmaAddr = (uint)val,
                    valueProviderCallback: _ => pcRxDmaAddr);

            // 0x014 PERIF_PC_RX_CTRL (SERV_PEND is W1C)
            regs[0x014] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_PC_RX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        if(((uint)val & ServPend) != 0)
                        {
                            pcRxCtrlValue &= ~ServPend;
                            pcRxPos = 0;
                            pcRxLen = 0;
                        }
                    },
                    valueProviderCallback: _ => pcRxCtrlValue);

            // 0x018 PERIF_PC_RX_DATA — handled in ReadDoubleWord
            regs[0x018] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "PERIF_PC_RX_DATA");

            // 0x020 PERIF_PC_TX_DMA
            regs[0x020] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_PC_TX_DMA",
                    writeCallback: (_, val) => pcTxDmaAddr = (uint)val,
                    valueProviderCallback: _ => pcTxDmaAddr);

            // 0x024 PERIF_PC_TX_CTRL
            regs[0x024] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_PC_TX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        pcTxCtrlValue = (uint)val;
                        if(((uint)val & TrigPend) != 0)
                            CompletePcTx();
                    },
                    valueProviderCallback: _ => pcTxCtrlValue);

            // 0x028 PERIF_PC_TX_DATA — handled in WriteDoubleWord
            regs[0x028] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Write, name: "PERIF_PC_TX_DATA");

            // 0x030 PERIF_NP_TX_DMA
            regs[0x030] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_NP_TX_DMA",
                    writeCallback: (_, val) => npTxDmaAddr = (uint)val,
                    valueProviderCallback: _ => npTxDmaAddr);

            // 0x034 PERIF_NP_TX_CTRL
            regs[0x034] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_NP_TX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        npTxCtrlValue = (uint)val;
                        if(((uint)val & TrigPend) != 0)
                            CompleteNpTx();
                    },
                    valueProviderCallback: _ => npTxCtrlValue);

            // 0x038 PERIF_NP_TX_DATA — handled in WriteDoubleWord
            regs[0x038] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Write, name: "PERIF_NP_TX_DATA");

            // 0x040 OOB_RX_DMA
            regs[0x040] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "OOB_RX_DMA",
                    writeCallback: (_, val) => oobRxDmaAddr = (uint)val,
                    valueProviderCallback: _ => oobRxDmaAddr);

            // 0x044 OOB_RX_CTRL
            regs[0x044] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "OOB_RX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        if(((uint)val & ServPend) != 0)
                        {
                            oobRxCtrlValue &= ~ServPend;
                            oobRxPos = 0;
                            oobRxLen = 0;
                        }
                    },
                    valueProviderCallback: _ => oobRxCtrlValue);

            // 0x048 OOB_RX_DATA — handled in ReadDoubleWord
            regs[0x048] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "OOB_RX_DATA");

            // 0x050 OOB_TX_DMA
            regs[0x050] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "OOB_TX_DMA",
                    writeCallback: (_, val) => oobTxDmaAddr = (uint)val,
                    valueProviderCallback: _ => oobTxDmaAddr);

            // 0x054 OOB_TX_CTRL
            regs[0x054] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "OOB_TX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        oobTxCtrlValue = (uint)val;
                        if(((uint)val & TrigPend) != 0)
                            CompleteOobTx();
                    },
                    valueProviderCallback: _ => oobTxCtrlValue);

            // 0x058 OOB_TX_DATA — handled in WriteDoubleWord
            regs[0x058] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Write, name: "OOB_TX_DATA");

            // 0x060 FLASH_RX_DMA
            regs[0x060] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "FLASH_RX_DMA",
                    writeCallback: (_, val) => flashRxDmaAddr = (uint)val,
                    valueProviderCallback: _ => flashRxDmaAddr);

            // 0x064 FLASH_RX_CTRL
            regs[0x064] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "FLASH_RX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        if(((uint)val & ServPend) != 0)
                        {
                            flashRxCtrlValue &= ~ServPend;
                            flashRxPos = 0;
                            flashRxLen = 0;
                        }
                    },
                    valueProviderCallback: _ => flashRxCtrlValue);

            // 0x068 FLASH_RX_DATA — handled in ReadDoubleWord
            regs[0x068] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "FLASH_RX_DATA");

            // 0x070 FLASH_TX_DMA
            regs[0x070] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "FLASH_TX_DMA",
                    writeCallback: (_, val) => flashTxDmaAddr = (uint)val,
                    valueProviderCallback: _ => flashTxDmaAddr);

            // 0x074 FLASH_TX_CTRL
            regs[0x074] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "FLASH_TX_CTRL",
                    writeCallback: (_, val) =>
                    {
                        flashTxCtrlValue = (uint)val;
                        if(((uint)val & TrigPend) != 0)
                            CompleteFlashTx();
                    },
                    valueProviderCallback: _ => flashTxCtrlValue);

            // 0x078 FLASH_TX_DATA — handled in WriteDoubleWord
            regs[0x078] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_TX_DATA");

            // 0x080 CTRL2
            regs[0x080] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "CTRL2",
                    writeCallback: (_, val) => ctrl2Value = (uint)val,
                    valueProviderCallback: _ => ctrl2Value);

            // 0x084 PERIF_MCYC_SADDR
            regs[0x084] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_MCYC_SADDR",
                    writeCallback: (_, val) => mcycSaddrValue = (uint)val,
                    valueProviderCallback: _ => mcycSaddrValue);

            // 0x088 PERIF_MCYC_TADDR
            regs[0x088] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_MCYC_TADDR",
                    writeCallback: (_, val) => mcycTaddrValue = (uint)val,
                    valueProviderCallback: _ => mcycTaddrValue);

            // 0x08C PERIF_MCYC_MASK
            regs[0x08C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "PERIF_MCYC_MASK",
                    writeCallback: (_, val) => mcycMaskValue = (uint)val,
                    valueProviderCallback: _ => mcycMaskValue);

            // 0x090 FLASH_SAFS_TADDR
            regs[0x090] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "FLASH_SAFS_TADDR",
                    writeCallback: (_, val) => flashSafsTaddrValue = (uint)val,
                    valueProviderCallback: _ => flashSafsTaddrValue);

            // 0x094 VW_SYSEVT_INT_EN
            regs[0x094] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT_INT_EN",
                    writeCallback: (_, val) =>
                    {
                        sysevtIntEnValue = (uint)val;
                        // Re-evaluate: newly enabled bits may trigger
                        NotifySysevtChange(0, sysevtValue);
                    },
                    valueProviderCallback: _ => sysevtIntEnValue);

            // 0x098 VW_SYSEVT
            regs[0x098] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT",
                    writeCallback: (_, val) =>
                    {
                        // BMC can write slave-driven bits only
                        sysevtValue = ((uint)val & SysevtSlaveDrivenMask) |
                                      (sysevtValue & SysevtHostDrivenMask);
                    },
                    valueProviderCallback: _ => sysevtValue);

            // 0x09C VW_GPIO_VAL
            regs[0x09C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_GPIO_VAL",
                    writeCallback: (_, val) =>
                    {
                        uint oldVal = vwGpioValue;
                        vwGpioValue = (uint)val;
                        if(oldVal != val)
                        {
                            intStsValue |= IntVwGpio;
                            UpdateIrq();
                        }
                    },
                    valueProviderCallback: _ => vwGpioValue);

            // 0x0A0 GEN_CAP_N_CONF (read-only)
            regs[0x0A0] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "GEN_CAP_N_CONF",
                    valueProviderCallback: _ => genCapValue);

            // 0x0A4 CH0_CAP_N_CONF (read-only)
            regs[0x0A4] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "CH0_CAP_N_CONF",
                    valueProviderCallback: _ => ch0CapValue);

            // 0x0A8 CH1_CAP_N_CONF (read-only)
            regs[0x0A8] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "CH1_CAP_N_CONF",
                    valueProviderCallback: _ => ch1CapValue);

            // 0x0AC CH2_CAP_N_CONF (read-only)
            regs[0x0AC] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "CH2_CAP_N_CONF",
                    valueProviderCallback: _ => ch2CapValue);

            // 0x0B0 CH3_CAP_N_CONF (read-only)
            regs[0x0B0] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "CH3_CAP_N_CONF",
                    valueProviderCallback: _ => ch3CapValue);

            // 0x0B4 CH3_CAP_N_CONF2 (read-only)
            regs[0x0B4] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "CH3_CAP_N_CONF2",
                    valueProviderCallback: _ => ch3Cap2Value);

            // 0x0C0 VW_GPIO_DIR
            regs[0x0C0] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_GPIO_DIR",
                    writeCallback: (_, val) => vwGpioDirValue = (uint)val,
                    valueProviderCallback: _ => vwGpioDirValue);

            // 0x0C4 VW_GPIO_GRP
            regs[0x0C4] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_GPIO_GRP",
                    writeCallback: (_, val) => vwGpioGrpValue = (uint)val,
                    valueProviderCallback: _ => vwGpioGrpValue);

            // 0x0FC INT_EN_CLR (write clears INT_EN bits)
            regs[0x0FC] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "INT_EN_CLR",
                    writeCallback: (_, val) =>
                    {
                        intEnValue &= ~(uint)val;
                        UpdateIrq();
                    },
                    valueProviderCallback: _ => 0);

            // 0x100 VW_SYSEVT1_INT_EN
            regs[0x100] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1_INT_EN",
                    writeCallback: (_, val) => sysevt1IntEnValue = (uint)val,
                    valueProviderCallback: _ => sysevt1IntEnValue);

            // 0x104 VW_SYSEVT1
            regs[0x104] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1",
                    writeCallback: (_, val) =>
                    {
                        // SUSPEND_ACK is slave-driven, SUSPEND_WARN is host-driven
                        sysevt1Value = ((uint)val & Sysevt1SuspendAck) |
                                       (sysevt1Value & Sysevt1SuspendWarn);
                    },
                    valueProviderCallback: _ => sysevt1Value);

            // 0x110 VW_SYSEVT_INT_T0
            regs[0x110] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT_INT_T0",
                    writeCallback: (_, val) => sysevtIntT0Value = (uint)val,
                    valueProviderCallback: _ => sysevtIntT0Value);

            // 0x114 VW_SYSEVT_INT_T1
            regs[0x114] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT_INT_T1",
                    writeCallback: (_, val) => sysevtIntT1Value = (uint)val,
                    valueProviderCallback: _ => sysevtIntT1Value);

            // 0x118 VW_SYSEVT_INT_T2
            regs[0x118] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT_INT_T2",
                    writeCallback: (_, val) => sysevtIntT2Value = (uint)val,
                    valueProviderCallback: _ => sysevtIntT2Value);

            // 0x11C VW_SYSEVT_INT_STS (W1C)
            regs[0x11C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT_INT_STS",
                    writeCallback: (_, val) =>
                    {
                        sysevtIntStsValue &= ~(uint)val;
                        if(sysevtIntStsValue == 0)
                            intStsValue &= ~IntVwSysevt;
                        UpdateIrq();
                    },
                    valueProviderCallback: _ => sysevtIntStsValue);

            // 0x120 VW_SYSEVT1_INT_T0
            regs[0x120] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1_INT_T0",
                    writeCallback: (_, val) => sysevt1IntT0Value = (uint)val,
                    valueProviderCallback: _ => sysevt1IntT0Value);

            // 0x124 VW_SYSEVT1_INT_T1
            regs[0x124] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1_INT_T1",
                    writeCallback: (_, val) => sysevt1IntT1Value = (uint)val,
                    valueProviderCallback: _ => sysevt1IntT1Value);

            // 0x128 VW_SYSEVT1_INT_T2
            regs[0x128] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1_INT_T2",
                    writeCallback: (_, val) => sysevt1IntT2Value = (uint)val,
                    valueProviderCallback: _ => sysevt1IntT2Value);

            // 0x12C VW_SYSEVT1_INT_STS (W1C)
            regs[0x12C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "VW_SYSEVT1_INT_STS",
                    writeCallback: (_, val) =>
                    {
                        sysevt1IntStsValue &= ~(uint)val;
                        if(sysevt1IntStsValue == 0)
                            intStsValue &= ~IntVwSysevt1;
                        UpdateIrq();
                    },
                    valueProviderCallback: _ => sysevt1IntStsValue);

            // 0x800 MMBI_CTRL
            regs[0x800] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "MMBI_CTRL",
                    writeCallback: (_, val) => mmbiCtrlValue = (uint)val,
                    valueProviderCallback: _ => mmbiCtrlValue);

            // 0x808 MMBI_INT_STS (W1C)
            regs[0x808] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "MMBI_INT_STS",
                    writeCallback: (_, val) => mmbiIntStsValue &= ~(uint)val,
                    valueProviderCallback: _ => mmbiIntStsValue);

            // 0x80C MMBI_INT_EN
            regs[0x80C] = new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "MMBI_INT_EN",
                    writeCallback: (_, val) => mmbiIntEnValue = (uint)val,
                    valueProviderCallback: _ => mmbiIntEnValue);

            // 0x810-0x848 MMBI_HOST_RWP (8 instances, 8 bytes apart)
            for(int i = 0; i < MmbiMaxInst; i++)
            {
                long off = 0x810 + (i * 8);
                int idx = i;
                regs[off] = new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: $"MMBI_HOST_RWP{idx}",
                        writeCallback: (_, val) => mmbiHostRwp[idx] = (uint)val,
                        valueProviderCallback: _ => mmbiHostRwp[idx]);
            }

            return new DoubleWordRegisterCollection(this, regs);
        }

        private void HandleCtrlWrite(uint data)
        {
            // SW reset bits are self-clearing
            if((data & CtrlPerifPcRxSwRst) != 0) ResetPerifPcRx();
            if((data & CtrlPerifPcTxSwRst) != 0) ResetPerifPcTx();
            if((data & CtrlPerifNpTxSwRst) != 0) ResetPerifNpTx();
            if((data & CtrlOobRxSwRst) != 0) ResetOobRx();
            if((data & CtrlOobTxSwRst) != 0) ResetOobTx();
            if((data & CtrlFlashRxSwRst) != 0) ResetFlashRx();
            if((data & CtrlFlashTxSwRst) != 0) ResetFlashTx();

            // Store value with reset bits cleared
            ctrlValue = data & ~SwResetMask;
        }

        private void CompletePcTx()
        {
            pcTxCtrlValue &= ~TrigPend;
            pcTxLen = 0;
            intStsValue |= IntPerifPcTxCmplt;
            UpdateIrq();
        }

        private void CompleteNpTx()
        {
            npTxCtrlValue &= ~TrigPend;
            npTxLen = 0;
            intStsValue |= IntPerifNpTxCmplt;
            UpdateIrq();
        }

        private void CompleteOobTx()
        {
            oobTxCtrlValue &= ~TrigPend;
            oobTxLen = 0;
            intStsValue |= IntOobTxCmplt;
            UpdateIrq();
        }

        private void CompleteFlashTx()
        {
            flashTxCtrlValue &= ~TrigPend;
            flashTxLen = 0;
            intStsValue |= IntFlashTxCmplt;
            UpdateIrq();
        }

        private void NotifySysevtChange(uint oldVal, uint newVal)
        {
            uint changed = oldVal ^ newVal;
            uint enabled = sysevtIntEnValue;

            if((changed & enabled) != 0)
            {
                sysevtIntStsValue |= (changed & enabled);
                intStsValue |= IntVwSysevt;
                UpdateIrq();
            }
        }

        private void UpdateIrq()
        {
            bool active = (intStsValue & intEnValue) != 0;
            IRQ.Set(active);
        }

        private void ResetPerifPcRx()
        {
            Array.Clear(pcRxBuf, 0, pcRxBuf.Length);
            pcRxLen = 0;
            pcRxPos = 0;
            pcRxCtrlValue = 0;
        }

        private void ResetPerifPcTx()
        {
            Array.Clear(pcTxBuf, 0, pcTxBuf.Length);
            pcTxLen = 0;
            pcTxCtrlValue = 0;
        }

        private void ResetPerifNpTx()
        {
            Array.Clear(npTxBuf, 0, npTxBuf.Length);
            npTxLen = 0;
            npTxCtrlValue = 0;
        }

        private void ResetOobRx()
        {
            Array.Clear(oobRxBuf, 0, oobRxBuf.Length);
            oobRxLen = 0;
            oobRxPos = 0;
            oobRxCtrlValue = 0;
        }

        private void ResetOobTx()
        {
            Array.Clear(oobTxBuf, 0, oobTxBuf.Length);
            oobTxLen = 0;
            oobTxCtrlValue = 0;
        }

        private void ResetFlashRx()
        {
            Array.Clear(flashRxBuf, 0, flashRxBuf.Length);
            flashRxLen = 0;
            flashRxPos = 0;
            flashRxCtrlValue = 0;
        }

        private void ResetFlashTx()
        {
            Array.Clear(flashTxBuf, 0, flashTxBuf.Length);
            flashTxLen = 0;
            flashTxCtrlValue = 0;
        }

        private static uint PackCtrl(byte cyc, byte tag, uint len)
        {
            return ((len & 0xFFF) << 12) | (uint)((tag & 0xF) << 8) | cyc;
        }

        // --- Constants (matching QEMU defines) ---

        private const int FifoSize = 256;
        private const int MmbiMaxInst = 8;

        // CTRL register bits
        private const uint CtrlFlashTxSwRst     = 1u << 31;
        private const uint CtrlFlashRxSwRst     = 1u << 30;
        private const uint CtrlOobTxSwRst       = 1u << 29;
        private const uint CtrlOobRxSwRst       = 1u << 28;
        private const uint CtrlPerifNpTxSwRst   = 1u << 27;
        private const uint CtrlPerifNpRxSwRst   = 1u << 26;
        private const uint CtrlPerifPcTxSwRst   = 1u << 25;
        private const uint CtrlPerifPcRxSwRst   = 1u << 24;
        private const uint CtrlFlashTxDmaEn     = 1u << 23;
        private const uint CtrlFlashRxDmaEn     = 1u << 22;
        private const uint CtrlOobTxDmaEn       = 1u << 21;
        private const uint CtrlOobRxDmaEn       = 1u << 20;
        private const uint CtrlPerifNpTxDmaEn   = 1u << 19;
        private const uint CtrlPerifPcTxDmaEn   = 1u << 17;
        private const uint CtrlPerifPcRxDmaEn   = 1u << 16;

        private const uint SwResetMask =
            CtrlFlashTxSwRst | CtrlFlashRxSwRst |
            CtrlOobTxSwRst | CtrlOobRxSwRst |
            CtrlPerifNpTxSwRst | CtrlPerifNpRxSwRst |
            CtrlPerifPcTxSwRst | CtrlPerifPcRxSwRst;

        // INT_STS / INT_EN bits
        private const uint IntRstDeassert       = 1u << 31;
        private const uint IntOobRxTmout        = 1u << 23;
        private const uint IntVwSysevt1         = 1u << 22;
        private const uint IntFlashTxErr        = 1u << 21;
        private const uint IntOobTxErr          = 1u << 20;
        private const uint IntFlashTxAbt        = 1u << 19;
        private const uint IntOobTxAbt          = 1u << 18;
        private const uint IntPerifNpTxAbt      = 1u << 17;
        private const uint IntPerifPcTxAbt      = 1u << 16;
        private const uint IntFlashRxAbt        = 1u << 15;
        private const uint IntOobRxAbt          = 1u << 14;
        private const uint IntPerifNpRxAbt      = 1u << 13;
        private const uint IntPerifPcRxAbt      = 1u << 12;
        private const uint IntPerifNpTxErr      = 1u << 11;
        private const uint IntPerifPcTxErr      = 1u << 10;
        private const uint IntVwGpio            = 1u << 9;
        private const uint IntVwSysevt          = 1u << 8;
        private const uint IntFlashTxCmplt      = 1u << 7;
        private const uint IntFlashRxCmplt      = 1u << 6;
        private const uint IntOobTxCmplt        = 1u << 5;
        private const uint IntOobRxCmplt        = 1u << 4;
        private const uint IntPerifNpTxCmplt    = 1u << 3;
        private const uint IntPerifPcTxCmplt    = 1u << 1;
        private const uint IntPerifPcRxCmplt    = 1u << 0;

        // CTRL register field bits
        private const uint ServPend = 1u << 31;
        private const uint TrigPend = 1u << 31;

        // SYSEVT host-driven bits
        private const uint SysevtHostRstWarn = 1u << 8;
        private const uint SysevtOobRstWarn  = 1u << 6;
        private const uint SysevtPltrst      = 1u << 5;
        private const uint SysevtSuspend     = 1u << 4;
        private const uint SysevtS5Sleep     = 1u << 2;
        private const uint SysevtS4Sleep     = 1u << 1;
        private const uint SysevtS3Sleep     = 1u << 0;

        private const uint SysevtHostDrivenMask =
            SysevtHostRstWarn | SysevtOobRstWarn | SysevtPltrst |
            SysevtSuspend | SysevtS5Sleep | SysevtS4Sleep | SysevtS3Sleep;

        // SYSEVT slave-driven bits
        private const uint SysevtHostRstAck   = 1u << 27;
        private const uint SysevtRstCpuInit   = 1u << 26;
        private const uint SysevtSlvBootSts   = 1u << 23;
        private const uint SysevtNonFatalErr  = 1u << 22;
        private const uint SysevtFatalErr     = 1u << 21;
        private const uint SysevtSlvBootDone  = 1u << 20;
        private const uint SysevtOobRstAck    = 1u << 16;
        private const uint SysevtNmiOut       = 1u << 10;
        private const uint SysevtSmiOut       = 1u << 9;

        private const uint SysevtSlaveDrivenMask =
            SysevtHostRstAck | SysevtRstCpuInit | SysevtSlvBootSts |
            SysevtNonFatalErr | SysevtFatalErr | SysevtSlvBootDone |
            SysevtOobRstAck | SysevtNmiOut | SysevtSmiOut;

        // SYSEVT1 bits
        private const uint Sysevt1SuspendAck  = 1u << 20;
        private const uint Sysevt1SuspendWarn = 1u << 0;

        // CTRL2 bits
        private const uint Ctrl2McycRdDis = 1u << 6;
        private const uint Ctrl2McycWrDis = 1u << 4;

        // Capability reset values (from QEMU)
        private const uint GenCapReset   = 0x0000F759;
        private const uint Ch0CapReset   = 0x00000073;
        private const uint Ch1CapReset   = 0x00000033;
        private const uint Ch2CapReset   = 0x00000033;
        private const uint Ch3CapReset   = 0x00000003;
        private const uint Ch3Cap2Reset  = 0x00000000;

        // --- State ---

        private readonly IMachine machine;
        private readonly DoubleWordRegisterCollection registers;

        // FIFO buffers
        private readonly byte[] pcRxBuf, pcTxBuf, npTxBuf;
        private readonly byte[] oobRxBuf, oobTxBuf;
        private readonly byte[] flashRxBuf, flashTxBuf;

        // FIFO positions/lengths
        private uint pcRxLen, pcRxPos, pcTxLen, npTxLen;
        private uint oobRxLen, oobRxPos, oobTxLen;
        private uint flashRxLen, flashRxPos, flashTxLen;

        // Register backing fields
        private uint ctrlValue, stsValue, intStsValue, intEnValue;
        private uint ctrl2Value;
        private uint pcRxCtrlValue, pcTxCtrlValue, npTxCtrlValue;
        private uint oobRxCtrlValue, oobTxCtrlValue;
        private uint flashRxCtrlValue, flashTxCtrlValue;
        private uint pcRxDmaAddr, pcTxDmaAddr, npTxDmaAddr;
        private uint oobRxDmaAddr, oobTxDmaAddr;
        private uint flashRxDmaAddr, flashTxDmaAddr;
        private uint mcycSaddrValue, mcycTaddrValue, mcycMaskValue;
        private uint flashSafsTaddrValue;
        private uint sysevtValue, sysevtIntEnValue, sysevtIntStsValue;
        private uint sysevt1Value, sysevt1IntEnValue, sysevt1IntStsValue;
        private uint sysevtIntT0Value, sysevtIntT1Value, sysevtIntT2Value;
        private uint sysevt1IntT0Value, sysevt1IntT1Value, sysevt1IntT2Value;
        private uint vwGpioValue, vwGpioDirValue, vwGpioGrpValue;
        private uint genCapValue, ch0CapValue, ch1CapValue;
        private uint ch2CapValue, ch3CapValue, ch3Cap2Value;
        private uint mmbiCtrlValue, mmbiIntStsValue, mmbiIntEnValue;
        private uint[] mmbiHostRwp = new uint[MmbiMaxInst];
    }
}
