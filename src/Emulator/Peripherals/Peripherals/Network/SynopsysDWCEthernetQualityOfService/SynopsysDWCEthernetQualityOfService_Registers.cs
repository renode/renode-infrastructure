//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService : IDoubleWordPeripheral
    {
        public uint ReadDoubleWord(long offset)
        {
            return Read<RegistersMacAndMmc>(macAndMmcRegisters, "MAC and MMC", offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            Write<RegistersMacAndMmc>(macAndMmcRegisters, "MAC and MMC", offset, value);
        }

        [ConnectionRegionAttribute("mtl")]
        public uint ReadDoubleWordFromMTL(long offset)
        {
            return Read<RegistersMTL>(mtlRegisters, "MTL", offset);
        }

        [ConnectionRegionAttribute("mtl")]
        public void WriteDoubleWordToMTL(long offset, uint value)
        {
            Write<RegistersMTL>(mtlRegisters, "MTL", offset, value);
        }

        [ConnectionRegionAttribute("dma")]
        public uint ReadDoubleWordFromDMA(long offset)
        {
            return Read<RegistersDMA>(dmaRegisters, "DMA", offset);
        }

        [ConnectionRegionAttribute("dma")]
        public void WriteDoubleWordToDMA(long offset, uint value)
        {
            Write<RegistersDMA>(dmaRegisters, "DMA", offset, value);
        }

        // This property works similar to `sysbus LogPeripheralAccess` command,
        // but supports regions. Setting this to `null` disables logging.
        public LogLevel PeripheralAccessLogLevel { get; set; } = null;

        private void ResetRegisters()
        {
            macAndMmcRegisters.Reset();
            mtlRegisters.Reset();
            dmaRegisters.Reset();
        }

        private IDictionary<long, DoubleWordRegister> CreateRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>()
            {
                {(long)RegistersMacAndMmc.OperatingModeConfiguration, new DoubleWordRegister(this)
                    .WithFlag(0, out rxEnable, name: "MACCR.RE (Receiver Enable)")
                    .WithFlag(1, out txEnable, name: "MACCR.TE (TE)")
                    .WithTag("MACCR.PRELEN (PRELEN)", 2, 2)
                    .WithTaggedFlag("MACCR.DC (DC)", 4)
                    .WithTag("MACCR.BL (BL)", 5, 2)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("MACCR.DR (DR)", 8)
                    .WithTaggedFlag("MACCR.DCRS (DCRS)", 9)
                    .WithTaggedFlag("MACCR.DO (DO)", 10)
                    .WithTaggedFlag("MACCR.ECRSFD (ECRSFD)", 11)
                    .WithTaggedFlag("MACCR.LM (LM)", 12)
                    .WithTaggedFlag("MACCR.DM (DM)", 13)
                    .WithTaggedFlag("MACCR.FES (FES)", 14)
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("MACCR.JD (JD)", 17)
                    .WithTaggedFlag("MACCR.JE (JE)", 16)
                    .WithReservedBits(18, 1)
                    .WithTaggedFlag("MACCR.WD (WD)", 19)
                    .WithTaggedFlag("MACCR.ACS (ACS)", 20)
                    .WithTaggedFlag("MACCR.CST (CST)", 21)
                    .WithTaggedFlag("MACCR.S2KP (S2KP)", 22)
                    .WithTaggedFlag("MACCR.GPSLCE (GPSLCE)", 23)
                    .WithTag("MACCR.IPG (IPG)", 24, 3)
                    .WithFlag(27, out checksumOffloadEnable, name: "MACCR.IPC (IPC)")
                    .WithTag("MACCR.SARC (SARC)", 28, 3)
                    .WithTaggedFlag("MACCR.ARPEN (ARPEN)", 31)
                },
                {(long)RegistersMacAndMmc.ExtendedOperatingModeConfiguration, new DoubleWordRegister(this)
                    .WithTag("MACECR.GPSL (GPSL)", 0, 14)
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out crcCheckDisable, name: "MACECR.DCRCC (DCRCC)")
                    .WithTaggedFlag("MACECR.SPEN (SPEN)", 17)
                    .WithTaggedFlag("MACECR.USP (USP)", 18)
                    .WithReservedBits(19, 5)
                    .WithTaggedFlag("MACECR.EIPGEN (EIPGEN)", 24)
                    .WithTag("MACECR.EIPG (EIPG)", 25, 5)
                    .WithReservedBits(30, 2)
                },
                {(long)RegistersMacAndMmc.PacketFilteringControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACPFR.PR (PR)", 0)
                    .WithTaggedFlag("MACPFR.HUC (HUC)", 1)
                    .WithTaggedFlag("MACPFR.HMC (HMC)", 2)
                    .WithTaggedFlag("MACPFR.DAIF (DAIF)", 3)
                    .WithTaggedFlag("MACPFR.PM (PM)", 4)
                    .WithTaggedFlag("MACPFR.DBF (DBF)", 5)
                    .WithTag("MACPFR.PCF (PCF)", 6, 2)
                    .WithTaggedFlag("MACPFR.SAIF (SAIF)", 8)
                    .WithTaggedFlag("MACPFR.SAF (SAF)", 9)
                    .WithTaggedFlag("MACPFR.HPF (HPF)", 10)
                    .WithReservedBits(11, 5)
                    .WithTaggedFlag("MACPFR.VTFE (VTFE)", 16)
                    .WithReservedBits(17, 3)
                    .WithTaggedFlag("MACPFR.IPFE (IPFE)", 20)
                    .WithTaggedFlag("MACPFR.DNTU (DNTU)", 21)
                    .WithReservedBits(22, 9)
                    .WithTaggedFlag("MACPFR.RA (RA)", 31)
                },
                {(long)RegistersMacAndMmc.WatchdogTimeout, new DoubleWordRegister(this)
                    .WithTag("MACWTR.WTO (WTO)", 0, 4)
                    .WithReservedBits(4, 4)
                    .WithTaggedFlag("MACWTR.PWE (PWE)", 8)
                    .WithReservedBits(9, 23)
                },
                {(long)RegistersMacAndMmc.HashTable0, new DoubleWordRegister(this)
                    .WithTag("MACHT0R.HT31T0 (HT31T0)", 0, 32)
                },
                {(long)RegistersMacAndMmc.HashTable1, new DoubleWordRegister(this)
                    .WithTag("MACHT1R.HT63T32 (HT63T32)", 0, 32)
                },
                {(long)RegistersMacAndMmc.VLANTag, new DoubleWordRegister(this)
                    .WithTag("MACVTR.VL (VL)", 0, 16)
                    .WithTaggedFlag("MACVTR.ETV (ETV)", 16)
                    .WithTaggedFlag("MACVTR.VTIM (VTIM)", 17)
                    .WithTaggedFlag("MACVTR.ESVL (ESVL)", 18)
                    .WithTaggedFlag("MACVTR.ERSVLM (ERSVLM)", 19)
                    .WithTaggedFlag("MACVTR.DOVLTC (DOVLTC)", 20)
                    .WithTag("MACVTR.EVLS (EVLS)", 21, 2)
                    .WithReservedBits(23, 1)
                    .WithTaggedFlag("MACVTR.EVLRXS (EVLRXS)", 24)
                    .WithTaggedFlag("MACVTR.VTHM (VTHM)", 25)
                    .WithTaggedFlag("MACVTR.EDVLP (EDVLP)", 26)
                    .WithTaggedFlag("MACVTR.ERIVLT (ERIVLT)", 27)
                    .WithTag("MACVTR.EIVLS (EIVLS)", 28, 2)
                    .WithReservedBits(30, 1)
                    .WithTaggedFlag("MACVTR.EIVLRXS (EIVLRXS)", 31)
                },
                {(long)RegistersMacAndMmc.VLANHashTable, new DoubleWordRegister(this)
                    .WithTag("MACVHTR.VLHT (VLHT)", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersMacAndMmc.VLANInclusion, new DoubleWordRegister(this)
                    .WithTag("MACVIR.VLT (VLT)", 0, 16)
                    .WithTag("MACVIR.VLC (VLC)", 16, 2)
                    .WithTaggedFlag("MACVIR.VLP (VLP)", 18)
                    .WithTaggedFlag("MACVIR.CSVL (CSVL)", 19)
                    .WithTaggedFlag("MACVIR.VLTI (VLTI)", 20)
                    .WithReservedBits(21, 11)
                },
                {(long)RegistersMacAndMmc.InnerVLANInclusion, new DoubleWordRegister(this)
                    .WithTag("MACIVIR.VLT (VLT)", 0, 16)
                    .WithTag("MACIVIR.VLC (VLC)", 16, 2)
                    .WithTaggedFlag("MACIVIR.VLP (VLP)", 18)
                    .WithTaggedFlag("MACIVIR.CSVL (CSVL)", 19)
                    .WithTaggedFlag("MACIVIR.VLTI (VLTI)", 20)
                    .WithReservedBits(21, 11)
                },
                {(long)RegistersMacAndMmc.TxQueueFlowControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACQTxFCR.FCB_BPA (FCB_BPA)", 0)
                    .WithTaggedFlag("MACQTxFCR.TFE (TFE)", 1)
                    .WithReservedBits(2, 2)
                    .WithTag("MACQTxFCR.PLT (PLT)", 4, 3)
                    .WithTaggedFlag("MACQTxFCR.DZPQ (DZPQ)", 7)
                    .WithReservedBits(8, 8)
                    .WithTag("MACQTxFCR.PT (PT)", 16, 16)
                },
                {(long)RegistersMacAndMmc.RxFlowControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACRxFCR.RFE (RFE)", 0)
                    .WithTaggedFlag("MACRxFCR.UP (UP)", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)RegistersMacAndMmc.InterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(0, 3)
                    .WithTaggedFlag("MACISR.PHYIS (PHYIS)", 3)
                    .WithFlag(4, out ptpMessageTypeInterrupt, name: "MACISR.PMTIS (PMTIS)")
                    .WithFlag(5, out lowPowerIdleInterrupt, name: "MACISR.LPIIS (LPIIS)")
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("MACISR.MMCIS (MMCIS)", 8)
                    .WithTaggedFlag("MACISR.MMCRXIS (MMCRXIS)", 9)
                    .WithFlag(10, valueProviderCallback: _ => MMCTxInterruptStatus, name: "MACISR.MMCTXIS (MMCTXIS)")
                    .WithReservedBits(11, 1)
                    .WithFlag(12, out timestampInterrupt, FieldMode.ReadToClear, name: "MACISR.TSIS (TSIS)")
                    .WithTaggedFlag("MACISR.TXSTSIS (TXSTSIS)", 13)
                    .WithTaggedFlag("MACISR.RXSTSIS (RXSTSIS)", 14)
                    .WithReservedBits(15, 17)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.InterruptEnable, new DoubleWordRegister(this)
                    .WithReservedBits(0, 3)
                    .WithTaggedFlag("MACIER.PHYIE (PHYIE)", 3)
                    .WithFlag(4, out ptpMessageTypeInterruptEnable, name: "MACIER.PMTIE (PMTIE)")
                    .WithFlag(5, out lowPowerIdleInterruptEnable, name: "MACIER.LPIIE (LPIIE)")
                    .WithReservedBits(6, 6)
                    .WithFlag(12, out timestampInterruptEnable, name: "MACIER.TSIE (TSIE)")
                    .WithTaggedFlag("MACIER.TXSTSIE (TXSTSIE)", 13)
                    .WithTaggedFlag("MACIER.RXSTSIE (RXSTSIE)", 14)
                    .WithReservedBits(15, 17)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.RxTxStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACRxTxSR.TJT (TJT)", 0)
                    .WithTaggedFlag("MACRxTxSR.NCARR (NCARR)", 1)
                    .WithTaggedFlag("MACRxTxSR.LCARR (LCARR)", 2)
                    .WithTaggedFlag("MACRxTxSR.EXDEF (EXDEF)", 3)
                    .WithTaggedFlag("MACRxTxSR.LCOL (LCOL)", 4)
                    .WithTaggedFlag("MACRxTxSR.EXCOL (LCOL)", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("MACRxTxSR.RWT (RWT)", 8)
                    .WithReservedBits(9, 23)
                },
                {(long)RegistersMacAndMmc.PMTControlStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACPCSR.PWRDWN (PWRDWN)", 0)
                    .WithTaggedFlag("MACPCSR.MGKPKTEN (MGKPKTEN)", 1)
                    .WithTaggedFlag("MACPCSR.RWKPKTEN (RWKPKTEN)", 2)
                    .WithReservedBits(3, 2)
                    .WithTaggedFlag("MACPCSR.MGKPRCVD (MGKPRCVD)", 5)
                    .WithTaggedFlag("MACPCSR.RWKPRCVD (RWKPRCVD)", 6)
                    .WithReservedBits(7, 2)
                    .WithTaggedFlag("MACPCSR.GLBLUCAST (GLBLUCAST)", 9)
                    .WithTaggedFlag("MACPCSR.RWKPFE (RWKPFE)", 10)
                    .WithReservedBits(11, 13)
                    .WithTag("MACPCSR.RWKPTR (RWKPTR)", 24, 5)
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("MACPCSR.RWKFILTRST (RWKFILTRST)", 31)
                },
                {(long)RegistersMacAndMmc.RemoteWakeUpPacketFilter, new DoubleWordRegister(this)
                    .WithTag("MACRWKPFR.MACRWKPFR (MACRWKPFR)", 0, 32)
                },
                {(long)RegistersMacAndMmc.LPIControlAndStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACLCSR.TLPIEN (TLPIEN)", 0)
                    .WithTaggedFlag("MACLCSR.TLPIEX (TLPIEX)", 1)
                    .WithTaggedFlag("MACLCSR.RLPIEN (RLPIEN)", 2)
                    .WithTaggedFlag("MACLCSR.RLPIEX (RLPIEX)", 3)
                    .WithReservedBits(4, 4)
                    .WithTaggedFlag("MACLCSR.TLPIST (TLPIST)", 8)
                    .WithTaggedFlag("MACLCSR.RLPIST (RLPIST)", 9)
                    .WithReservedBits(10, 6)
                    .WithTaggedFlag("MACLCSR.LPIEN (LPIEN)", 16)
                    .WithTaggedFlag("MACLCSR.PLS (PLS)", 17)
                    .WithTaggedFlag("MACLCSR.PLSEN (PLSEN)", 18)
                    .WithTaggedFlag("MACLCSR.LPITXA (LPITXA)", 19)
                    .WithTaggedFlag("MACLCSR.LPITE (LPITE)", 20)
                    .WithReservedBits(21, 11)
                },
                {(long)RegistersMacAndMmc.LPITimersControl, new DoubleWordRegister(this, 0x3E80000)
                    .WithTag("MACLTCR.TWT (TWT)", 0, 16)
                    .WithTag("MACLTCR.LST (LST)", 16, 10)
                    .WithReservedBits(26, 6)
                },
                {(long)RegistersMacAndMmc.LPIEntryTimer, new DoubleWordRegister(this)
                    .WithTag("MACLETR.LPIET (LPIET)", 0, 17)
                    .WithReservedBits(17, 15)
                },
                {(long)RegistersMacAndMmc.OneMicrosecondTickCounter, new DoubleWordRegister(this)
                    .WithTag("MAC1USTCR.TIC_1US_CNTR (TIC_1US_CNTR)", 0, 12)
                    .WithReservedBits(12, 20)
                },
                {(long)RegistersMacAndMmc.Version, new DoubleWordRegister(this, 0x3142)
                    .WithTag("MACVR.SNPSVER (SNPSVER)", 0, 8)
                    .WithTag("MACVR.USERVER (USERVER)", 8, 8)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersMacAndMmc.Debug, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACDR.RPESTS (RPESTS)", 0)
                    .WithTag("MACDR.RFCFCSTS (RFCFCSTS)", 1, 2)
                    .WithReservedBits(3, 13)
                    .WithTaggedFlag("MACDR.TPESTS (TPESTS)", 16)
                    .WithTag("MACDR.TFCSTS (TFCSTS)", 17, 2)
                    .WithReservedBits(19, 13)
                },
                {(long)RegistersMacAndMmc.HardwareFeature0, new DoubleWordRegister(this, 0x0A0D_73F7)
                    .WithTaggedFlag("MIISEL", 0)
                    .WithTaggedFlag("GMIISEL", 1)
                    .WithTaggedFlag("HDSEL", 2)
                    .WithTaggedFlag("PCSSEL", 3)
                    .WithTaggedFlag("VLHASH", 4)
                    .WithTaggedFlag("SMASEL", 5)
                    .WithTaggedFlag("RWKSEL", 6)
                    .WithTaggedFlag("MGKSEL", 7)
                    .WithTaggedFlag("MMCSEL", 8)
                    .WithTaggedFlag("ARPOFFSEL", 9)
                    .WithReservedBits(10, 2)
                    .WithTaggedFlag("TSSEL", 12)
                    .WithTaggedFlag("EEESEL", 13)
                    .WithTaggedFlag("TXCOESEL", 14)
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("RXCOESEL", 16)
                    .WithReservedBits(17, 1)
                    .WithTag("ADDMACADRSEL", 18, 5)
                    .WithTaggedFlag("MACADR32SEL", 23)
                    .WithTaggedFlag("MACADR64SEL", 24)
                    .WithTag("TSSTSSEL", 25, 2)
                    .WithTaggedFlag("SAVLANINS", 27)
                    .WithTag("ACTPHYSEL", 28, 3)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.HardwareFeature1, new DoubleWordRegister(this, 0x1104_1904)
                    .WithTag("MACHWF1R.RXFIFOSIZE (RXFIFOSIZE)", 0, 5)
                    .WithReservedBits(5, 1)
                    .WithTag("MACHWF1R.TXFIFOSIZE (TXFIFOSIZE)", 6, 5)
                    .WithTaggedFlag("MACHWF1R.OSTEN (OSTEN)", 11)
                    .WithTaggedFlag("MACHWF1R.PTOEN (PTOEN)", 12)
                    .WithTaggedFlag("MACHWF1R.ADVTHWORD (ADVTHWORD)", 13)
                    .WithTag("MACHWF1R.ADDR64 (ADDR64)", 14, 2)
                    .WithTaggedFlag("MACHWF1R.DCBEN (DCBEN)", 16)
                    .WithTaggedFlag("MACHWF1R.SPHEN (SPHEN)", 17)
                    .WithTaggedFlag("MACHWF1R.TSOEN (TSOEN)", 18)
                    .WithTaggedFlag("MACHWF1R.DBGMEMA (DBGMEMA)", 19)
                    .WithTaggedFlag("MACHWF1R.AVSEL (AVSEL)", 20)
                    .WithReservedBits(21, 3)
                    .WithTag("MACHWF1R.HASHTBLSZ (HASHTBLSZ)", 24, 2)
                    .WithReservedBits(26, 1)
                    .WithTag("MACHWF1R.L3L4FNUM (L3L4FNUM)", 27, 4)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.HardwareFeature2, new DoubleWordRegister(this, 0x4100_0000)
                    .WithTag("MACHWF2R.RXQCNT (RXQCNT)", 0, 4)
                    .WithReservedBits(4, 2)
                    .WithTag("MACHWF2R.TXQCNT (TXQCNT)", 6, 4)
                    .WithReservedBits(10, 2)
                    .WithTag("MACHWF2R.RXCHCNT (RXCHCNT)", 12, 4)
                    .WithReservedBits(16, 2)
                    .WithTag("MACHWF2R.TXCHCNT (TXCHCNT)", 18, 4)
                    .WithReservedBits(22, 2)
                    .WithTag("MACHWF2R.PPSOUTNUM (PPSOUTNUM)", 24, 3)
                    .WithReservedBits(27, 1)
                    .WithTag("MACHWF2R.AUXSNAPNUM (AUXSNAPNUM)", 28, 3)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.HardwareFeature3, new DoubleWordRegister(this, 0x20)
                    .WithTag("NRVF", 0, 3)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("CBTISEL", 4)
                    .WithTaggedFlag("DVLAN", 5)
                    .WithReservedBits(6, 26)
                },
                {(long)RegistersMacAndMmc.MDIOAddress, new DoubleWordRegister(this)
                    .WithFlag(0, out miiBusy, name: "MACMDIOAR.MB (MB)")
                    .WithFlag(1, out clause45PhyEnable, name: "MACMDIOAR.C45E (C45E)")
                    .WithEnumField<DoubleWordRegister, MIIOperation>(2, 2, out miiOperation, name: "MACMDIOAR.GOC (GOC)")
                    .WithTaggedFlag("MACMDIOAR.SKAP (SKAP)", 4)
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 4, changeCallback: (_, value) => { if(value >= 0b0110 && value <= 0b0111) this.Log(LogLevel.Warning, "Reserved CSR Clock Range (0x{0:X}).", value); }, name: "MACMDIOAR.CR (CR)")
                    .WithTag("MACMDIOAR.NTC (NTC)", 12, 3)
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 5, out miiRegisterOrDeviceAddress, name: "MACMDIOAR.RDA (RDA)")
                    .WithValueField(21, 5, out miiPhy, name: "MACMDIOAR.PA (PA)")
                    .WithTaggedFlag("MACMDIOAR.BTB (BTB)", 26)
                    .WithTaggedFlag("MACMDIOAR.PSE (PSE)", 27)
                    .WithReservedBits(28, 4)
                    .WithWriteCallback((_, __) => ExecuteMIIOperation())
                },
                {(long)RegistersMacAndMmc.MDIOData, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out miiData, name: "MACMDIODR.MD (MD)")
                    .WithValueField(16, 16, out miiAddress, name: "MACMDIODR.RA (RA)")
                },
                {(long)RegistersMacAndMmc.ARPAddress, new DoubleWordRegister(this)
                    .WithTag("MACARPAR.ARPPA (ARPPA)", 0, 32)
                },
                {(long)RegistersMacAndMmc.CSRSoftwareControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("RCW", 0)
                    .WithReservedBits(1, 7)
                    .WithTaggedFlag("SEEN", 8)
                    .WithReservedBits(9, 23)
                },
                {(long)RegistersMacAndMmc.MACAddress0High, new DoubleWordRegister(this, 0x8000FFFF)
                    .WithValueField(0, 8, name: "MACA0HR.ADDRHI (ADDRHI) [MAC.E]",
                        valueProviderCallback: _ => MAC.E,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(e: (byte)value))
                    .WithValueField(8, 8, name: "MACA0HR.ADDRHI (ADDRHI) [MAC.F]",
                        valueProviderCallback: _ => MAC.F,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(f: (byte)value))
                    .WithReservedBits(16, 15)
                    .WithFlag(31, FieldMode.Read, name: "MACA0HR.AE (AE)",
                        valueProviderCallback: _ => true)
                },
                {(long)RegistersMacAndMmc.MACAddress0Low, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 8, name: "MACA0LR.ADDRLO (ADDRLO) [MAC.A]",
                        valueProviderCallback: _ => MAC.A,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(a: (byte)value))
                    .WithValueField(8, 8, name: "MACA0LR.ADDRLO (ADDRLO) [MAC.B]",
                        valueProviderCallback: _ => MAC.B,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(b: (byte)value))
                    .WithValueField(16, 8, name: "MACA0LR.ADDRLO (ADDRLO) [MAC.C]",
                        valueProviderCallback: _ => MAC.C,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(c: (byte)value))
                    .WithValueField(24, 8, name: "MACA0LR.ADDRLO (ADDRLO) [MAC.D]",
                        valueProviderCallback: _ => MAC.D,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(d: (byte)value))
                },
                {(long)RegistersMacAndMmc.MACAddress1High, new DoubleWordRegister(this, 0xFFFF)
                    .WithTag("MACA1HR.ADDRHI (ADDRHI)", 0, 16)
                    .WithReservedBits(16, 8)
                    .WithTag("MACA1HR.MBC (MBC)", 24, 6)
                    .WithTaggedFlag("MACA1HR.SA (SA)", 30)
                    .WithTaggedFlag("MACA1HR.AE (AE)", 31)
                },
                {(long)RegistersMacAndMmc.MACAddress1Low, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("MACA1LR.ADDRLO (ADDRLO)", 0, 32)
                },
                {(long)RegistersMacAndMmc.MACAddress2High, new DoubleWordRegister(this, 0xFFFF)
                    .WithTag("MACA2HR.ADDRHI (ADDRHI)", 0, 16)
                    .WithReservedBits(16, 8)
                    .WithTag("MACA2HR.MBC (MBC)", 24, 6)
                    .WithTaggedFlag("MACA2HR.SA (SA)", 30)
                    .WithTaggedFlag("MACA2HR.AE (AE)", 31)
                },
                {(long)RegistersMacAndMmc.MACAddress2Low, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("MACA2LR.ADDRLO (ADDRLO)", 0, 32)
                },
                {(long)RegistersMacAndMmc.MACAddress3High, new DoubleWordRegister(this, 0xFFFF)
                    .WithTag("MACA3HR.ADDRHI (ADDRHI)", 0, 16)
                    .WithReservedBits(16, 8)
                    .WithTag("MACA3HR.MBC (MBC)", 24, 6)
                    .WithTaggedFlag("MACA3HR.SA (SA)", 30)
                    .WithTaggedFlag("MACA3HR.AE (AE)", 31)
                },
                {(long)RegistersMacAndMmc.MACAddress3Low, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("MACA3LR.ADDRLO (ADDRLO)", 0, 32)
                },
                {(long)RegistersMacAndMmc.MMCControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MMC_CONTROL.CNTRST (CNTRST)", 0)
                    .WithTaggedFlag("MMC_CONTROL.CNTSTOPRO (CNTSTOPRO)", 1)
                    .WithTaggedFlag("MMC_CONTROL.RSTONRD (RSTONRD)", 2)
                    .WithTaggedFlag("MMC_CONTROL.CNTFREEZ (CNTFREEZ)", 3)
                    .WithTaggedFlag("MMC_CONTROL.CNTPRST (CNTPRST)", 4)
                    .WithTaggedFlag("MMC_CONTROL.CNTPRSTLVL (CNTPRSTLVL)", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("MMC_CONTROL.UCDBC (UCDBC)", 8)
                    .WithReservedBits(9, 23)
                },
                {(long)RegistersMacAndMmc.MMCRxInterrupt, new DoubleWordRegister(this)
                    .WithReservedBits(0, 5)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXCRCERPIS (RXCRCERPIS)", 5)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXALGNERPIS (RXALGNERPIS)", 6)
                    .WithReservedBits(7, 10)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXUCGPIS (RXUCGPIS)", 17)
                    .WithReservedBits(18, 8)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXLPIUSCIS (RXLPIUSCIS)", 26)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXLPITRCIS (RXLPITRCIS)", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)RegistersMacAndMmc.MMCTxInterrupt, new DoubleWordRegister(this)
                    .WithReservedBits(0, 14)
                    .WithFlag(14, out txGoodPacketCounterThresholdInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXSCOLGPIS (TXSCOLGPIS)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXMCOLGPIS (TXMCOLGPIS)", 15)
                    .WithReservedBits(16, 5)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXGPKTIS (TXGPKTIS)", 21)
                    .WithReservedBits(22, 4)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXLPIUSCIS (TXLPIUSCIS)", 26)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXLPITRCIS (TXLPITRCIS)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.MMCRxInterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(0, 5)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXCRCERPIM (RXCRCERPIM)", 5)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXALGNERPIM (RXALGNERPIM)", 6)
                    .WithReservedBits(7, 10)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXUCGPIM (RXUCGPIM)", 17)
                    .WithReservedBits(18, 8)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXLPIUSCIM (RXLPIUSCIM)", 26)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXLPITRCIM (RXLPITRCIM)", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)RegistersMacAndMmc.MMCTxInterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(0, 14)
                    .WithFlag(14, out txGoodPacketCounterThresholdInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXSCOLGPIM (TXSCOLGPIM)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXMCOLGPIM (TXMCOLGPIM)", 15)
                    .WithReservedBits(16, 5)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXGPKTIM (TXGPKTIM)", 21)
                    .WithReservedBits(22, 4)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXLPIUSCIM (TXLPIUSCIM)", 26)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXLPITRCIM (TXLPITRCIM)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.TxSingleCollisionGoodPackets, new DoubleWordRegister(this)
                    .WithTag("TX_SINGLE_COLLISION_GOOD_PACKETS.TXSNGLCOLG (TXSNGLCOLG)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TxMultipleCollisionGoodPackets, new DoubleWordRegister(this)
                    .WithTag("TX_MULTIPLE_COLLISION_GOOD_PACKETS.TXMULTCOLG (TXMULTCOLG)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TxPacketCountGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txGoodPacketCounter, FieldMode.Read, name: "TX_PACKET_COUNT_GOOD.TXPKTG (TXPKTG)")
                },
                {(long)RegistersMacAndMmc.RxCRCErrorPackets, new DoubleWordRegister(this)
                    .WithTag("RX_CRC_ERROR_PACKETS.RXCRCERR (RXCRCERR)", 0, 32)
                },
                {(long)RegistersMacAndMmc.RxAlignmentErrorPackets, new DoubleWordRegister(this)
                    .WithTag("RX_ALIGNMENT_ERROR_PACKETS.RXALGNERR (RXALGNERR)", 0, 32)
                },
                {(long)RegistersMacAndMmc.RxUnicastPacketsGood, new DoubleWordRegister(this)
                    .WithTag("RX_UNICAST_PACKETS_GOOD.RXUCASTG (RXUCASTG)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TxLPIMicrodecondTimer, new DoubleWordRegister(this)
                    .WithTag("TX_LPI_USEC_CNTR.TXLPIUSC (TXLPIUSC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TxLPITransitionCounter, new DoubleWordRegister(this)
                    .WithTag("TX_LPI_TRAN_CNTR.TXLPITRC (TXLPITRC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.RxLPIMicrosecondCounter, new DoubleWordRegister(this)
                    .WithTag("RX_LPI_USEC_CNTR.RXLPIUSC (RXLPIUSC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.RxLPITransitionCounter, new DoubleWordRegister(this)
                    .WithTag("RX_LPI_TRAN_CNTR.RXLPITRC (RXLPITRC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3And4Control0, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACL3L4C0R.L3PEN0 (L3PEN0)", 0)
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("MACL3L4C0R.L3SAM0 (L3SAM0)", 2)
                    .WithTaggedFlag("MACL3L4C0R.L3SAIM0 (L3SAIM0)", 3)
                    .WithTaggedFlag("MACL3L4C0R.L3DAM0 (L3DAM0)", 4)
                    .WithTaggedFlag("MACL3L4C0R.L3DAIM0 (L3DAIM0)", 5)
                    .WithTag("MACL3L4C0R.L3HSBM0 (L3HSBM0)", 6, 5)
                    .WithTag("MACL3L4C0R.L3HDBM0 (L3HDBM0)", 11, 5)
                    .WithTaggedFlag("MACL3L4C0R.L4PEN0 (L4PEN0)", 16)
                    .WithReservedBits(17, 1)
                    .WithTaggedFlag("MACL3L4C0R.L4SPM0 (L4SPM0)", 18)
                    .WithTaggedFlag("MACL3L4C0R.L4SPIM0 (L4SPIM0)", 19)
                    .WithTaggedFlag("MACL3L4C0R.L4DPM0 (L4DPM0)", 20)
                    .WithTaggedFlag("MACL3L4C0R.L4DPIM0 (L4DPIM0)", 21)
                    .WithReservedBits(22, 10)
                },
                {(long)RegistersMacAndMmc.Layer4AddressFilter0, new DoubleWordRegister(this)
                    .WithTag("MACL4A0R.L4SP0 (L4SP0)", 0, 16)
                    .WithTag("MACL4A0R.L4DP0 (L4DP0)", 16, 16)
                },
                {(long)RegistersMacAndMmc.Layer3Address0Filter0, new DoubleWordRegister(this)
                    .WithTag("MACL3A00R.L3A00 (L3A00)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address1Filter0, new DoubleWordRegister(this)
                    .WithTag("MACL3A10R.L3A10 (L3A10)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address2Filter0, new DoubleWordRegister(this)
                    .WithTag("MACL3A20.L3A20 (L3A20)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address3Filter0, new DoubleWordRegister(this)
                    .WithTag("MACL3A30.L3A30 (L3A30)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3And4Control1, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACL3L4C1R.L3PEN1 (L3PEN1)", 0)
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("MACL3L4C1R.L3SAM1 (L3SAM1)", 2)
                    .WithTaggedFlag("MACL3L4C1R.L3SAIM1 (L3SAIM1)", 3)
                    .WithTaggedFlag("MACL3L4C1R.L3DAM1 (L3DAM1)", 4)
                    .WithTaggedFlag("MACL3L4C1R.L3DAIM1 (L3DAIM1)", 5)
                    .WithTag("MACL3L4C1R.L3HSBM1 (L3HSBM1)", 6, 5)
                    .WithTag("MACL3L4C1R.L3HDBM1 (L3HDBM1)", 11, 5)
                    .WithTaggedFlag("MACL3L4C1R.L4PEN1 (L4PEN1)", 16)
                    .WithReservedBits(17, 1)
                    .WithTaggedFlag("MACL3L4C1R.L4SPM1 (L4SPM1)", 18)
                    .WithTaggedFlag("MACL3L4C1R.L4SPIM1 (L4SPIM1)", 19)
                    .WithTaggedFlag("MACL3L4C1R.L4DPM1 (L4DPM1)", 20)
                    .WithTaggedFlag("MACL3L4C1R.L4DPIM1 (L4DPIM1)", 21)
                    .WithReservedBits(22, 10)
                },
                {(long)RegistersMacAndMmc.Layer4AddressFilter1, new DoubleWordRegister(this)
                    .WithTag("MACL4A1R.L4SP1 (L4SP1)", 0, 16)
                    .WithTag("MACL4A1R.L4DP1 (L4DP1)", 16, 16)
                },
                {(long)RegistersMacAndMmc.Layer3Address0Filter1, new DoubleWordRegister(this)
                    .WithTag("MACL3A01R.L3A01 (L3A01)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address1Filter1, new DoubleWordRegister(this)
                    .WithTag("MACL3A11R.L3A11 (L3A11)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address2Filter1, new DoubleWordRegister(this)
                    .WithTag("MACL3A21R.L3A21 (L3A21)", 0, 32)
                },
                {(long)RegistersMacAndMmc.Layer3Address3Filter1, new DoubleWordRegister(this)
                    .WithTag("MACL3A31R.L3A31 (L3A31)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampControl, new DoubleWordRegister(this, 0x0200)
                    .WithFlag(0, out enableTimestamp, name: "MACTSCR.TSENA (TSENA)")
                    .WithTaggedFlag("MACTSCR.TSCFUPDT (TSCFUPDT)", 1)
                    .WithTaggedFlag("MACTSCR.TSINIT (TSINIT)", 2)
                    .WithTaggedFlag("MACTSCR.TSUPDT (TSUPDT)", 3)
                    .WithReservedBits(4, 1)
                    .WithTaggedFlag("MACTSCR.TSADDREG (TSADDREG)", 5)
                    .WithReservedBits(6, 2)
                    .WithFlag(8, out enableTimestampForAll, name: "MACTSCR.TSENALL (TSENALL)")
                    .WithTaggedFlag("MACTSCR.TSCTRLSSR (TSCTRLSSR)", 9)
                    .WithTaggedFlag("MACTSCR.TSVER2ENA (TSVER2ENA)", 10)
                    .WithTaggedFlag("MACTSCR.TSIPENA (TSIPENA)", 11)
                    .WithTaggedFlag("MACTSCR.TSIPV6ENA (TSIPV6ENA)", 12)
                    .WithTaggedFlag("MACTSCR.TSIPV4ENA (TSIPV4ENA)", 13)
                    .WithTaggedFlag("MACTSCR.TSEVNTENA (TSEVNTENA)", 14)
                    .WithTaggedFlag("MACTSCR.TSMSTRENA (TSMSTRENA)", 15)
                    .WithTag("MACTSCR.SNAPTYPSEL (SNAPTYPSEL)", 16, 2)
                    .WithTaggedFlag("MACTSCR.TSENMACADDR (TSENMACADDR)", 18)
                    .WithTaggedFlag("MACTSCR.CSC (CSC)", 19)
                    .WithReservedBits(20, 4)
                    .WithTaggedFlag("MACTSCR.TXTSSTSM (TXTSSTSM)", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)RegistersMacAndMmc.SubsecondIncrement, new DoubleWordRegister(this)
                    .WithReservedBits(0, 8)
                    .WithTag("MACSSIR.SNSINC (SNSINC)", 8, 8)
                    .WithTag("MACSSIR.SSINC (SSINC)", 16, 8)
                    .WithReservedBits(24, 8)
                },
                {(long)RegistersMacAndMmc.SystemTimeSeconds, new DoubleWordRegister(this)
                    .WithTag("MACSTSR.TSS (TSS)", 0, 32)
                },
                {(long)RegistersMacAndMmc.SystemTimeNanoseconds, new DoubleWordRegister(this)
                    .WithTag("MACSTNR.TSSS (TSSS)", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.SystemTimeSecondsUpdate, new DoubleWordRegister(this)
                    .WithTag("MACSTSUR.TSS (TSS)", 0, 32)
                },
                {(long)RegistersMacAndMmc.SystemTimeNanosecondsUpdate, new DoubleWordRegister(this)
                    .WithTag("MACSTNUR.TSSS (TSSS)", 0, 31)
                    .WithTaggedFlag("MACSTNUR.ADDSUB (ADDSUB)", 31)
                },
                {(long)RegistersMacAndMmc.TimestampAddend, new DoubleWordRegister(this)
                    .WithTag("MACTSAR.TSAR (TSAR)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACTSSR.TSSOVF (TSSOVF)", 0)
                    .WithTaggedFlag("MACTSSR.TSTARGT0 (TSTARGT0)", 1)
                    .WithTaggedFlag("MACTSSR.AUXTSTRIG (AUXTSTRIG)", 2)
                    .WithTaggedFlag("MACTSSR.TSTRGTERR0 (TSTRGTERR0)", 3)
                    .WithReservedBits(4, 11)
                    .WithTaggedFlag("MACTSSR.TXTSSIS (TXTSSIS)", 15)
                    .WithTag("MACTSSR.ATSSTN (ATSSTN)", 16, 4)
                    .WithReservedBits(20, 4)
                    .WithTaggedFlag("MACTSSR.ATSSTM (ATSSTM)", 24)
                    .WithTag("MACTSSR.ATSNS (ATSNS)", 25, 5)
                    .WithReservedBits(30, 2)
                },
                {(long)RegistersMacAndMmc.TxTimestampStatusNanoseconds, new DoubleWordRegister(this)
                    .WithTag("MACTxTSSNR.TXTSSLO (TXTSSLO)", 0, 31)
                    .WithTaggedFlag("MACTxTSSNR.TXTSSMIS (TXTSSMIS)", 31)
                },
                {(long)RegistersMacAndMmc.TxTimestampStatusSeconds, new DoubleWordRegister(this)
                    .WithTag("MACTxTSSSR.TXTSSHI (TXTSSHI)", 0, 32)
                },
                {(long)RegistersMacAndMmc.AuxiliaryControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACACR.ATSFC (ATSFC)", 0)
                    .WithReservedBits(1, 3)
                    .WithTaggedFlag("MACACR.ATSEN0 (ATSEN0)", 4)
                    .WithTaggedFlag("MACACR.ATSEN1 (ATSEN1)", 5)
                    .WithTaggedFlag("MACACR.ATSEN2 (ATSEN2)", 6)
                    .WithTaggedFlag("MACACR.ATSEN3 (ATSEN3)", 7)
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersMacAndMmc.AuxiliaryTimestampNanoseconds, new DoubleWordRegister(this)
                    .WithTag("MACATSNR.AUXTSLO (AUXTSLO)", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.AuxiliaryTimestampSeconds, new DoubleWordRegister(this)
                    .WithTag("MACATSSR.AUXTSHI (AUXTSHI)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampIngressAsymmetricCorrection, new DoubleWordRegister(this)
                    .WithTag("MACTSIACR.OSTIAC (OSTIAC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampEgressAsymmetricCorrection, new DoubleWordRegister(this)
                    .WithTag("MACTSEACR.OSTEAC (OSTEAC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampIngressCorrectionNanosecond, new DoubleWordRegister(this)
                    .WithTag("MACTSICNR.TSIC (TSIC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.TimestampEgressCorrectionNanosecond, new DoubleWordRegister(this)
                    .WithTag("MACTSECNR.TSEC (TSEC)", 0, 32)
                },
                {(long)RegistersMacAndMmc.PPSControl, new DoubleWordRegister(this)
                    .WithTag("MACPPSCR.PPSCTRL/MACPPSCR.PPSCMD (PPSCTRL/PPSCMD)", 0, 4)
                    .WithTaggedFlag("MACPPSCR.PPSEN0 (PPSEN0)", 4)
                    .WithTag("MACPPSCR.TRGTMODSEL0 (TRGTMODSEL0)", 5, 2)
                    .WithReservedBits(7, 25)
                },
                {(long)RegistersMacAndMmc.PPSTargetTimeSeconds, new DoubleWordRegister(this)
                    .WithTag("MACPPSTTSR.TSTRH0 (TSTRH0)", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)RegistersMacAndMmc.PPSTargetTimeNanoseconds, new DoubleWordRegister(this)
                    .WithTag("MACPPSTTNR.TTSL0 (TTSL0)", 0, 31)
                    .WithTaggedFlag("MACPPSTTNR.TRGTBUSY0 (TRGTBUSY0)", 31)
                },
                {(long)RegistersMacAndMmc.PPSInterval, new DoubleWordRegister(this)
                    .WithTag("MACPPSIR.PPSINT0 (PPSINT0)", 0, 32)
                },
                {(long)RegistersMacAndMmc.PPSWidth, new DoubleWordRegister(this)
                    .WithTag("MACPPSWR.PPSWIDTH0 (PPSWIDTH0)", 0, 32)
                },
                {(long)RegistersMacAndMmc.PTPOffloadControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("MACPOCR.PTOEN (PTOEN)", 0)
                    .WithTaggedFlag("MACPOCR.ASYNCEN (ASYNCEN)", 1)
                    .WithTaggedFlag("MACPOCR.APDREQEN (APDREQEN)", 2)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("MACPOCR.ASYNCTRIG (ASYNCTRIG)", 4)
                    .WithTaggedFlag("MACPOCR.APDREQTRIG (APDREQTRIG)", 5)
                    .WithTaggedFlag("MACPOCR.DRRDIS (DRRDIS)", 6)
                    .WithReservedBits(7, 1)
                    .WithTag("MACPOCR.DN (DN)", 8, 8)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersMacAndMmc.PTPSourcePortIdentity0, new DoubleWordRegister(this)
                    .WithTag("MACSPI0R.SPI0 (SPI0)", 0, 32)
                },
                {(long)RegistersMacAndMmc.PTPSourcePortIdentity1, new DoubleWordRegister(this)
                    .WithTag("MACSPI1R.SPI1 (SPI1)", 0, 32)
                },
                {(long)RegistersMacAndMmc.PTPSourcePortIdentity2, new DoubleWordRegister(this)
                    .WithTag("MACSPI2R.SPI2 (SPI2)", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersMacAndMmc.LogMessageInterval, new DoubleWordRegister(this)
                    .WithTag("MACLMIR.LSI (LSI)", 0, 8)
                    .WithTag("MACLMIR.DRSYNCR (DRSYNCR)", 8, 3)
                    .WithReservedBits(11, 13)
                    .WithTag("MACLMIR.LMPDRI (LMPDRI)", 24, 8)
                }
            };
        }

        private IDictionary<long, DoubleWordRegister> CreateMTLRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>()
            {
                {(long)RegistersMTL.OperatingMode, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("MTLOMR.DTXSTS (DTXSTS)", 1)
                    .WithReservedBits(2, 6)
                    .WithTaggedFlag("MTLOMR.CNTPRST (CNTPRST)", 8)
                    .WithTaggedFlag("MTLOMR.CNTCLR (CNTCLR)", 9)
                    .WithReservedBits(10, 22)
                },
                {(long)RegistersMTL.InterruptStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MTLISR.Q0IS (Queue interrupt status)", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMTL.TxQueueOperating, new DoubleWordRegister(this, 0x70008)
                    .WithTaggedFlag("MTLTxQOMR.FTQ (Flush Transmit Queue)", 0)
                    .WithTaggedFlag("MTLTxQOMR.TSF (Transmit Store and Forward)", 1)
                    .WithTag("MTLTxQOMR.TXQEN (Transmit Queue Enable)", 2, 2)
                    .WithTag("MTLTxQOMR.TTC (Transmit Threshold Control)", 4, 3)
                    .WithReservedBits(7, 9)
                    .WithTag("MTLTxQOMR.TQS (Transmit Queue Size)", 16, 3)
                    .WithReservedBits(19, 13)
                },
                {(long)RegistersMTL.TxQueueUnderflow, new DoubleWordRegister(this)
                    .WithTag("MTLTxQUR.UFFRMCNT (Underflow Packet Counter)", 0, 11)
                    .WithTaggedFlag("MTLTxQUR.UFCNTOVF (UFCNTOVF)", 11)
                    .WithReservedBits(12, 20)
                },
                {(long)RegistersMTL.TxQueueDebug, new DoubleWordRegister(this)
                    .WithTaggedFlag("MTLTxQDR.TXQPAUSED (TXQPAUSED)", 0)
                    .WithTag("MTLTxQDR.TRCSTS (TRCSTS)", 1, 2)
                    .WithTaggedFlag("MTLTxQDR.TWCSTS (TWCSTS)", 3)
                    .WithTaggedFlag("MTLTxQDR.TXQSTS (TXQSTS)", 4)
                    .WithTaggedFlag("MTLTxQDR.TXSTSFSTS (TXSTSFSTS)", 5)
                    .WithReservedBits(6, 10)
                    .WithTag("MTLTxQDR.PTXQ (PTXQ)", 16, 3)
                    .WithReservedBits(19, 1)
                    .WithTag("MTLTxQDR.STXSTSF (STXSTSF)", 20, 3)
                    .WithReservedBits(23, 9)
                },
                {(long)RegistersMTL.QueueInterruptControlStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("MTLQICSR.TXUNFIS (TXUNFIS)", 0)
                    .WithReservedBits(1, 7)
                    .WithTaggedFlag("MTLQICSR.TXUIE (TXUIE)", 8)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag("MTLQICSR.RXOVFIS (RXOVFIS)", 16)
                    .WithReservedBits(17, 7)
                    .WithTaggedFlag("MTLQICSR.RXOIE (RXOIE)", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)RegistersMTL.RxQueueOperatingMode, new DoubleWordRegister(this, 0x700000)
                    .WithTag("MTLRxQOMR.RTC (RTC)", 0, 2)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("MTLRxQOMR.FUP (FUP)", 3)
                    .WithTaggedFlag("MTLRxQOMR.FEP (FEP)", 4)
                    .WithTaggedFlag("MTLRxQOMR.RSF (RSF)", 5)
                    .WithTaggedFlag("MTLRxQOMR.DIS_TCP_EF (DIS_TCP_EF)", 6)
                    .WithReservedBits(7, 13)
                    .WithValueField(20, 3, FieldMode.Read, name: "MTLRxQOMR.RQS (RQS)")
                    .WithReservedBits(23, 9)
                },
                {(long)RegistersMTL.RxQueueMissedPacketAndOverflowCounter, new DoubleWordRegister(this)
                    .WithTag("MTLRxQMPOCR.OVFPKTCNT (OVFPKTCNT)", 0, 11)
                    .WithTaggedFlag("MTLRxQMPOCR.OVFCNTOVF (OVFCNTOVF)", 11)
                    .WithReservedBits(12, 4)
                    .WithTag("MTLRxQMPOCR.MISPKTCNT (MISPKTCNT)", 16, 11)
                    .WithTaggedFlag("MTLRxQMPOCR.MISCNTOVF (MISCNTOVF)", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)RegistersMTL.RxQueueDebug, new DoubleWordRegister(this)
                    .WithTaggedFlag("MTLRxQDR.RWCSTS (RWCSTS)", 0)
                    .WithTag("MTLRxQDR.RRCSTS (RRCSTS)", 1, 2)
                    .WithReservedBits(3, 1)
                    .WithTag("MTLRxQDR.RXQSTS (RXQSTS)", 4, 2)
                    .WithReservedBits(6, 10)
                    .WithTag("MTLRxQDR.PRXQ (PRXQ)", 16, 14)
                    .WithReservedBits(30, 2)
                }
            };
        }

        private IDictionary<long, DoubleWordRegister> CreateDMARegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>()
            {
                {(long)RegistersDMA.DMAMode, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.Log(LogLevel.Info, "Software reset.");
                            Reset();
                        }
                    },
                    valueProviderCallback: _ => false, name: "DMAMR.SWR (Software Reset)")
                    .WithTaggedFlag("DMAMR.DA (DMA Tx or Rx Arbitration Scheme)", 1)
                    .WithReservedBits(2, 9)
                    .WithTaggedFlag("DMAMR.TXPR (Transmit priority)", 11)
                    .WithTag("DMAMR.PR (Priority ratio)", 12, 3)
                    .WithReservedBits(15, 1)
                    .WithTag("DMAMR.INTM (Interrupt Mode)", 16, 2)
                    .WithReservedBits(18, 14)
                },
                {(long)RegistersDMA.SystemBusMode, new DoubleWordRegister(this)
                    .WithTaggedFlag("DMASBMR.FB (Fixed Burst Length)", 0)
                    .WithReservedBits(1, 11)
                    .WithFlag(12, name: "DMASBMR.AAL (Address-Aligned Beats)")
                    .WithReservedBits(13, 1)
                    .WithTaggedFlag("DMASBMR.MB (Mixed Burst)", 14)
                    .WithTaggedFlag("DMASBMR.RB (Rebuild INCRx Burst)", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersDMA.InterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => DMAInterrupts, name: "DMAISR.DC0IS (DMA Channel Interrupt Status)")
                    .WithReservedBits(1, 15)
                    .WithTaggedFlag("DMAISR.MTLIS (MTL Interrupt Status)", 16)
                    .WithTaggedFlag("DMAISR.MACIS (MAC Interrupt Status)", 17)
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersDMA.DebugStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("DMADSR.AXWHSTS (AHB Master Write Channel)", 0)
                    .WithReservedBits(1, 7)
                    .WithTag("DMADSR.RPS0 (DMA Channel Receive Process State)", 8, 4)
                    .WithTag("DMADSR.TPS0 (DMA Channel Transmit Process State)", 12, 4)
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersDMA.ChannelControl, new DoubleWordRegister(this)
                    .WithValueField(0, 14, out maximumSegmentSize, name: "DMACCR.MSS (Maximum Segment Size)")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out programableBurstLengthTimes8, name: "DMACCR.PBLX8 (8xPBL mode)")
                    .WithReservedBits(17, 1)
                    .WithValueField(18, 3, out descriptorSkipLength, name: "DMACCR.DSL (Descriptor Skip Length)")
                    .WithReservedBits(21, 11)
                },
                {(long)RegistersDMA.ChannelTransmitControl, new DoubleWordRegister(this)
                    .WithFlag(0, out startTx, changeCallback: (_, __) =>
                    {
                        if(startTx.Value)
                        {
                            txDescriptorRingCurrent.Value = txDescriptorRingStart.Value;
                            txFinishedRing = false;
                            StartTxDMA();
                        }
                    },
                    name: "DMACTxCR.ST (Start or Stop Transmission Command)")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, out operateOnSecondPacket, name: "DMACTxCR.OSF (Operate on Second Packet)")
                    .WithReservedBits(5, 7)
                    .WithFlag(12, out tcpSegmentationEnable, name: "DMACTxCR.TSE (TCP Segmentation Enabled)")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 6, out txProgramableBurstLength, name: "DMACTxCR.TXPBL (Transmit Programmable Burst Length)")
                    .WithReservedBits(22, 10)
                },
                {(long)RegistersDMA.ChannelReceiveControl, new DoubleWordRegister(this)
                    .WithFlag(0, out startRx, changeCallback: (_, __) =>
                    {
                        if(startRx.Value)
                        {
                            rxDescriptorRingCurrent.Value = rxDescriptorRingStart.Value;
                            rxFinishedRing = false;
                            StartRxDMA();
                        }
                    },
                    name: "DMACRxCR.SR (Start or Stop Receive Command)")
                    .WithValueField(1, 14, out rxBufferSize, writeCallback: (_, __) =>
                    {
                        if(rxBufferSize.Value % 4 != 0)
                        {
                            this.Log(LogLevel.Warning, "Receive buffer size must be a multiple of 4. Ignoring LSBs, but this behavior is undefined.");
                            rxBufferSize.Value &= ~0x3UL;
                        }
                    },
                    name: "DMACRxCR.RBSZ (Receive Buffer size)")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 6, out rxProgramableBurstLength, name: "DMACRxCR.RXPBL (RXPBL)")
                    .WithReservedBits(22, 9)
                    .WithTaggedFlag("DMACRxCR.RPF (DMA Rx Channel Packet Flush)", 31)
                },
                {(long)RegistersDMA.ChannelTxDescriptorListAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txDescriptorRingStart, name: "DMACTxDLAR.TDESLA (Start of Transmit List)")
                },
                {(long)RegistersDMA.ChannelRxDescriptorListAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxDescriptorRingStart, name: "DMACRxDLAR.RDESLA (Start of Receive List)")
                },
                {(long)RegistersDMA.ChannelTxDescriptorTailPointer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txDescriptorRingTail, changeCallback: (previousValue, _) =>
                    {
                        var clearTxFinishedRing = txDescriptorRingTail.Value != txDescriptorRingCurrent.Value;
                        if((txState & DMAState.Suspended) != 0 || (txFinishedRing && clearTxFinishedRing))
                        {
                            txFinishedRing &= !clearTxFinishedRing;
                            StartTxDMA();
                        }
                    }, name: "DMACTxDTPR.TDT (Transmit Descriptor Tail Pointer)")
                },
                {(long)RegistersDMA.ChannelRxDescriptorTailPointer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxDescriptorRingTail, changeCallback: (previousValue, _) =>
                    {
                        var clearRxFinishedRing = rxDescriptorRingTail.Value != rxDescriptorRingCurrent.Value;
                        if((rxState & DMAState.Suspended) != 0 || (rxFinishedRing && clearRxFinishedRing))
                        {
                            rxFinishedRing &= !clearRxFinishedRing;
                            StartRxDMA();
                        }
                    }, name: "DMACRxDTPR.RDT (Receive Descriptor Tail Pointer)")
                },
                {(long)RegistersDMA.ChannelTxDescriptorRingLength, new DoubleWordRegister(this)
                    .WithValueField(0, 10, out txDescriptorRingLength, name: "DMACTxRLR.TDRL (Transmit Descriptor Ring Length)")
                    .WithReservedBits(10, 22)
                },
                {(long)RegistersDMA.ChannelRxDescriptorRingLength, new DoubleWordRegister(this)
                    .WithValueField(0, 10, out rxDescriptorRingLength, name: "DMACRxRLR.RDRL (Receive Descriptor Ring Length)")
                    .WithReservedBits(10, 6)
                    .WithValueField(16, 8, out alternateRxBufferSize, name: "DMACRxRLR.ARBS (Alternate Receive Buffer Size)")
                    .WithReservedBits(24, 8)
                },
                {(long)RegistersDMA.ChannelInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txInterruptEnable, name: "DMACIER.TIE (Transmit Interrupt Enable)")
                    .WithFlag(1, out txProcessStoppedEnable, name: "DMACIER.TXSE (Transmit Stopped Enable)")
                    .WithFlag(2, out txBufferUnavailableEnable, name: "DMACIER.TBUE (Transmit Buffer Unavailable Enable)")
                    .WithReservedBits(3, 3)
                    .WithFlag(6, out rxInterruptEnable, name: "DMACIER.RIE (Receive Interrupt Enable)")
                    .WithFlag(7, out rxBufferUnavailableEnable, name: "DMACIER.RBUE (Receive Buffer Unavailable Enable)")
                    .WithFlag(8, out rxProcessStoppedEnable, name: "DMACIER.RSE (Receive Stopped Enable)")
                    .WithFlag(9, out rxWatchdogTimeoutEnable, name: "DMACIER.RWTE (Receive Watchdog Timeout Enable)")
                    .WithFlag(10, out earlyTxInterruptEnable, name: "DMACIER.ETIE (Early Transmit Interrupt Enable)")
                    .WithFlag(11, out earlyRxInterruptEnable, name: "DMACIER.ERIE (Early Receive Interrupt Enable)")
                    .WithFlag(12, out fatalBusErrorEnable, name: "DMACIER.FBEE (Fatal Bus Error Enable)")
                    .WithFlag(13, out contextDescriptorErrorEnable, name: "DMACIER.CDEE (Context Descriptor Error Enable)")
                    .WithFlag(14, out abnormalInterruptSummaryEnable, name: "DMACIER.AIE (Abnormal Interrupt Summary Enable)")
                    .WithFlag(15, out normalInterruptSummaryEnable, name: "DMACIER.NIE (Normal Interrupt Summary Enable)")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersDMA.ChannelRxInterruptWatchdogTimer, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out rxWatchdogCounter, changeCallback: (_, __) =>
                    {
                        rxWatchdog.Limit = rxWatchdogCounter.Value;
                    },
                    name: "DMACRxIWTR.RWT (Receive Interrupt Watchdog Timer Count)")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 2, out rxWatchdogCounterUnit, changeCallback: (_, __) =>
                    {
                        rxWatchdog.Divider = RxWatchdogDivider << (byte)rxWatchdogCounterUnit.Value;
                    },
                    name: "DMACRxIWTR.RWTU (Receive Interrupt Watchdog Timer Count Units)")
                    .WithReservedBits(18, 14)
                },
                {(long)RegistersDMA.ChannelCurrentApplicationTransmitDescriptor, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txDescriptorRingCurrent, FieldMode.Read, name: "DMACCATxDR.CURTDESAPTR (Application Transmit Descriptor Address Pointer)")
                },
                {(long)RegistersDMA.ChannelCurrentApplicationReceiveDescriptor, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxDescriptorRingCurrent, FieldMode.Read, name: "DMACCARxDR.CURRDESAPTR (Application Receive Descriptor Address Pointer)")
                },
                {(long)RegistersDMA.ChannelCurrentApplicationTransmitBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txCurrentBuffer, FieldMode.Read, name: "DMACCATxBR.CURTBUFAPTR (Application Transmit Buffer Address Pointer)")
                },
                {(long)RegistersDMA.ChannelCurrentApplicationReceiveBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxCurrentBuffer, FieldMode.Read, name: "DMACCARxBR.CURRBUFAPTR (Application Receive Buffer Address Pointer)")
                },
                {(long)RegistersDMA.ChannelStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out txInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TI (Transmit Interrupt)")
                    .WithFlag(1, out txProcessStopped, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TPS (Transmit Process Stopped)")
                    .WithFlag(2, out txBufferUnavailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TBU (Transmit Buffer Unavailable)")
                    .WithReservedBits(3, 3)
                    .WithFlag(6, out rxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RI (Receive Interrupt)")
                    .WithFlag(7, out rxBufferUnavailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RBU (Receive Buffer Unavailable)")
                    .WithFlag(8, out rxProcessStopped, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RPS (Receive Process Stopped)")
                    .WithFlag(9, out rxWatchdogTimeout, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RWT (Receive Watchdog Timeout)")
                    .WithFlag(10, out earlyTxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.ET (Early Transmit Interrupt)")
                    .WithFlag(11, out earlyRxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.ER (Early Receive Interrupt)")
                    .WithFlag(12, out fatalBusError, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.FBE (Fatal Bus Error)")
                    .WithFlag(13, out contextDescriptorError, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.CDE (Context Descriptor Error)")
                    .WithFlag(14, out abnormalInterruptSummary, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.AIS (Abnormal Interrupt Summary)")
                    .WithFlag(15, out normalInterruptSummary, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.NIS (Normal Interrupt Summary)")
                    .WithTag("DMACSR.TEB (Tx DMA Error Bits)", 16, 3)
                    .WithTag("DMACSR.REB (Rx DMA Error Bits)", 19, 3)
                    .WithReservedBits(22, 10)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersDMA.ChannelMissedFrameCount, new DoubleWordRegister(this)
                    .WithTag("DMACMFCR.MFC (Dropped Packet Counters)", 0, 11)
                    .WithReservedBits(11, 4)
                    .WithTaggedFlag("DMACMFCR.MFCO (Overflow status of the MFC Counter)", 15)
                    .WithReservedBits(16, 16)
                }
            };
        }

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            try
            {
                if(registersCollection.TryRead(offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if(PeripheralAccessLogLevel != null)
                {
                    this.Log(PeripheralAccessLogLevel, "Read from {0} at offset 0x{1:X} ({2}), returned 0x{3:X}.", regionName, offset, Enum.Format(typeof(T), offset, "G"), result);
                }
            }
            this.Log(LogLevel.Warning, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, offset, Enum.Format(typeof(T), offset, "G"));
            return result;
        }

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            if(PeripheralAccessLogLevel != null)
            {
                this.Log(PeripheralAccessLogLevel, "Write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, offset, Enum.Format(typeof(T), offset, "G"), value);
            }
            if(!registersCollection.TryWrite(offset, value))
            {
                this.Log(LogLevel.Warning, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, offset, Enum.Format(typeof(T), offset, "G"), value);
                return;
            }
        }

        private void ExecuteMIIOperation()
        {
            if(!miiBusy.Value)
            {
                return;
            }
            miiBusy.Value = false;

            switch(miiOperation.Value)
            {
                case MIIOperation.Read:
                    if(clause45PhyEnable.Value)
                    {
                        if(!TryGetPhy<Clause45Address, ushort>((uint)miiPhy.Value, out var phy))
                        {
                            this.Log(LogLevel.Debug, "Read access to unknown phy {0} via Clause 45.", miiPhy.Value);
                            break;
                        }
                        var c45Address = new Clause45Address((byte)miiRegisterOrDeviceAddress.Value, (ushort)miiAddress.Value);
                        miiData.Value = phy.Read(c45Address);
                        this.Log(LogLevel.Noisy, "Read ({1}, 0x{2:X}) access to phy {0} via Clause 45.", miiPhy.Value, c45Address, miiData.Value);
                    }
                    else
                    {
                        if(!TryGetPhy<ushort>((uint)miiPhy.Value, out var phy))
                        {
                            this.Log(LogLevel.Debug, "Read access to unknown phy {0} via Clause 22.", miiPhy.Value);
                            break;
                        }
                        miiData.Value = phy.Read((ushort)miiRegisterOrDeviceAddress.Value);
                        this.Log(LogLevel.Noisy, "Read ({1}, 0x{2:X}) access to phy {0} via Clause 22.", miiPhy.Value, miiRegisterOrDeviceAddress.Value, miiData.Value);
                    }
                    break;
                case MIIOperation.Write:
                    if(clause45PhyEnable.Value)
                    {
                        if(!TryGetPhy<Clause45Address, ushort>((uint)miiPhy.Value, out var phy))
                        {
                            this.Log(LogLevel.Debug, "Write access to unknown phy {0} via Clause 45.", miiPhy.Value);
                            break;
                        }
                        var c45Address = new Clause45Address((byte)miiRegisterOrDeviceAddress.Value, (ushort)miiAddress.Value);
                        this.Log(LogLevel.Noisy, "Write ({1}, 0x{2:X}) access to phy {0} via Clause 45.", miiPhy.Value, c45Address, miiData.Value);
                        phy.Write(c45Address, (ushort)miiData.Value);
                    }
                    else
                    {
                        if(!TryGetPhy<ushort>((uint)miiPhy.Value, out var phy))
                        {
                            this.Log(LogLevel.Debug, "Write access to unknown phy {0} via Clause 22.", miiPhy.Value);
                            break;
                        }
                        this.Log(LogLevel.Noisy, "Write ({1}, 0x{2:X}) access to phy {0} via Clause 22.", miiPhy.Value, miiRegisterOrDeviceAddress.Value, miiData.Value);
                        phy.Write((ushort)miiRegisterOrDeviceAddress.Value, (ushort)miiData.Value);
                    }
                    break;
                case MIIOperation.PostReadAddressIncrement:
                    if(!clause45PhyEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "Invalid MII Command: Post Read Increment Address is valid only for Clause 45 PHY.");
                        break;
                    }
                    this.Log(LogLevel.Warning, "Unimplemented Command: Post Read Increment Address.");
                    break;
                default:
                    this.Log(LogLevel.Warning, "Invalid MII Command: Reserved Operation (0x{0:X}).", miiOperation.Value);
                    break;
            }
        }

        private IFlagRegisterField rxEnable;
        private IFlagRegisterField txEnable;
        private IFlagRegisterField checksumOffloadEnable;
        private IFlagRegisterField crcCheckDisable;
        private IFlagRegisterField ptpMessageTypeInterrupt;
        private IFlagRegisterField lowPowerIdleInterrupt;
        private IFlagRegisterField timestampInterrupt;
        private IFlagRegisterField ptpMessageTypeInterruptEnable;
        private IFlagRegisterField lowPowerIdleInterruptEnable;
        private IFlagRegisterField timestampInterruptEnable;
        private IFlagRegisterField miiBusy;
        private IFlagRegisterField clause45PhyEnable;
        private IEnumRegisterField<MIIOperation> miiOperation;
        private IValueRegisterField miiRegisterOrDeviceAddress;
        private IValueRegisterField miiPhy;
        private IValueRegisterField miiData;
        private IValueRegisterField miiAddress;
        private IFlagRegisterField txGoodPacketCounterThresholdInterrupt;
        private IFlagRegisterField txGoodPacketCounterThresholdInterruptEnable;
        private IValueRegisterField txGoodPacketCounter;
        private IFlagRegisterField enableTimestamp;
        private IFlagRegisterField enableTimestampForAll;

        private IValueRegisterField maximumSegmentSize;
        private IFlagRegisterField programableBurstLengthTimes8;
        private IValueRegisterField descriptorSkipLength;
        private IFlagRegisterField startTx;
        private IFlagRegisterField operateOnSecondPacket;
        private IFlagRegisterField tcpSegmentationEnable;
        private IValueRegisterField txProgramableBurstLength;
        private IFlagRegisterField startRx;
        private IValueRegisterField rxBufferSize;
        private IValueRegisterField rxProgramableBurstLength;
        private IValueRegisterField txDescriptorRingStart;
        private IValueRegisterField rxDescriptorRingStart;
        private IValueRegisterField txDescriptorRingTail;
        private IValueRegisterField rxDescriptorRingTail;
        private IValueRegisterField txDescriptorRingLength;
        private IValueRegisterField rxDescriptorRingLength;
        private IValueRegisterField alternateRxBufferSize;
        private IValueRegisterField txDescriptorRingCurrent;
        private IValueRegisterField rxDescriptorRingCurrent;
        private IValueRegisterField txCurrentBuffer;
        private IValueRegisterField rxCurrentBuffer;

        private IFlagRegisterField txInterruptEnable;
        private IFlagRegisterField txProcessStoppedEnable;
        private IFlagRegisterField txBufferUnavailableEnable;
        private IFlagRegisterField rxInterruptEnable;
        private IFlagRegisterField rxBufferUnavailableEnable;
        private IFlagRegisterField rxProcessStoppedEnable;
        private IFlagRegisterField rxWatchdogTimeoutEnable;
        private IFlagRegisterField earlyTxInterruptEnable;
        private IFlagRegisterField earlyRxInterruptEnable;
        private IFlagRegisterField fatalBusErrorEnable;
        private IFlagRegisterField contextDescriptorErrorEnable;
        private IFlagRegisterField abnormalInterruptSummaryEnable;
        private IValueRegisterField rxWatchdogCounter;
        private IValueRegisterField rxWatchdogCounterUnit;
        private IFlagRegisterField normalInterruptSummaryEnable;
        private IFlagRegisterField txInterrupt;
        private IFlagRegisterField txProcessStopped;
        private IFlagRegisterField txBufferUnavailable;
        private IFlagRegisterField rxInterrupt;
        private IFlagRegisterField rxBufferUnavailable;
        private IFlagRegisterField rxProcessStopped;
        private IFlagRegisterField rxWatchdogTimeout;
        private IFlagRegisterField earlyTxInterrupt;
        private IFlagRegisterField earlyRxInterrupt;
        private IFlagRegisterField fatalBusError;
        private IFlagRegisterField contextDescriptorError;
        private IFlagRegisterField abnormalInterruptSummary;
        private IFlagRegisterField normalInterruptSummary;

        private readonly DoubleWordRegisterCollection macAndMmcRegisters;
        // MAC Transaction Layer
        private readonly DoubleWordRegisterCollection mtlRegisters;
        private readonly DoubleWordRegisterCollection dmaRegisters;

        public enum RegistersMacAndMmc : long
        {
            OperatingModeConfiguration = 0x000,
            ExtendedOperatingModeConfiguration = 0x004,
            PacketFilteringControl = 0x008,
            WatchdogTimeout = 0x00C,
            HashTable0 = 0x010,
            HashTable1 = 0x014,
            VLANTag = 0x050,
            VLANHashTable = 0x058,
            VLANInclusion = 0x060,
            InnerVLANInclusion = 0x064,
            TxQueueFlowControl = 0x070,
            RxFlowControl = 0x090,
            InterruptStatus = 0x0B0,
            InterruptEnable = 0x0B4,
            RxTxStatus = 0x0B8,
            PMTControlStatus = 0x0C0,
            RemoteWakeUpPacketFilter = 0x0C4,
            LPIControlAndStatus = 0x0D0,
            LPITimersControl = 0x0D4,
            LPIEntryTimer = 0x0D8,
            OneMicrosecondTickCounter = 0x0DC,
            Version = 0x110,
            Debug = 0x114,
            HardwareFeature0 = 0x11C,
            HardwareFeature1 = 0x120,
            HardwareFeature2 = 0x124,
            HardwareFeature3 = 0x128,
            MDIOAddress = 0x200,
            MDIOData = 0x204,
            ARPAddress = 0x210,
            CSRSoftwareControl = 0x230,
            MACAddress0High = 0x300,
            MACAddress0Low = 0x304,
            MACAddress1Low = 0x30C,
            MACAddress2Low = 0x314,
            MACAddress1High = 0x308,
            MACAddress2High = 0x310,
            MACAddress3High = 0x318,
            MACAddress3Low = 0x31C,
            MMCControl = 0x700,
            MMCRxInterrupt = 0x704,
            MMCTxInterrupt = 0x708,
            MMCRxInterruptMask = 0x70C,
            MMCTxInterruptMask = 0x710,
            TxSingleCollisionGoodPackets = 0x74C,
            TxMultipleCollisionGoodPackets = 0x750,
            TxPacketCountGood = 0x768,
            RxCRCErrorPackets = 0x794,
            RxAlignmentErrorPackets = 0x798,
            RxUnicastPacketsGood = 0x7C4,
            TxLPIMicrodecondTimer = 0x7EC,
            TxLPITransitionCounter = 0x7F0,
            RxLPIMicrosecondCounter = 0x7F4,
            RxLPITransitionCounter = 0x7F8,
            Layer3And4Control0 = 0x900,
            Layer4AddressFilter0 = 0x904,
            Layer3Address0Filter0 = 0x910,
            Layer3Address1Filter0 = 0x914,
            Layer3Address2Filter0 = 0x918,
            Layer3Address3Filter0 = 0x91C,
            Layer3And4Control1 = 0x930,
            Layer4AddressFilter1 = 0x934,
            Layer3Address0Filter1 = 0x940,
            Layer3Address1Filter1 = 0x944,
            Layer3Address2Filter1 = 0x948,
            Layer3Address3Filter1 = 0x94C,
            TimestampControl = 0xB00,
            SubsecondIncrement = 0xB04,
            SystemTimeSeconds = 0xB08,
            SystemTimeNanoseconds = 0xB0C,
            SystemTimeSecondsUpdate = 0xB10,
            SystemTimeNanosecondsUpdate = 0xB14,
            TimestampAddend = 0xB18,
            TimestampStatus = 0xB20,
            TxTimestampStatusNanoseconds = 0xB30,
            TxTimestampStatusSeconds = 0xB34,
            AuxiliaryControl = 0xB40,
            AuxiliaryTimestampNanoseconds = 0xB48,
            AuxiliaryTimestampSeconds = 0xB4C,
            TimestampIngressAsymmetricCorrection = 0xB50,
            TimestampEgressAsymmetricCorrection = 0xB54,
            TimestampIngressCorrectionNanosecond = 0xB58,
            TimestampEgressCorrectionNanosecond = 0xB5C,
            PPSControl = 0xB70,
            PPSTargetTimeSeconds = 0xB80,
            PPSTargetTimeNanoseconds = 0xB84,
            PPSInterval = 0xB88,
            PPSWidth = 0xB8C,
            PTPOffloadControl = 0xBC0,
            PTPSourcePortIdentity0 = 0xBC4,
            PTPSourcePortIdentity1 = 0xBC8,
            PTPSourcePortIdentity2 = 0xBCC,
            LogMessageInterval = 0xBD0,
        }

        public enum RegistersMTL : long
        {
            OperatingMode = 0x000,
            InterruptStatus = 0x020,
            TxQueueOperating = 0x100,
            TxQueueUnderflow = 0x104,
            TxQueueDebug = 0x108,
            QueueInterruptControlStatus = 0x12C,
            RxQueueOperatingMode = 0x130,
            RxQueueMissedPacketAndOverflowCounter = 0x134,
            RxQueueDebug = 0x138,
        }

        public enum RegistersDMA : long
        {
            DMAMode = 0x000,
            SystemBusMode = 0x004,
            InterruptStatus = 0x008,
            DebugStatus = 0x00C,
            ChannelControl = 0x100,
            ChannelTransmitControl = 0x104,
            ChannelReceiveControl = 0x108,
            ChannelTxDescriptorListAddress = 0x114,
            ChannelRxDescriptorListAddress = 0x11C,
            ChannelTxDescriptorTailPointer = 0x120,
            ChannelRxDescriptorTailPointer = 0x128,
            ChannelTxDescriptorRingLength = 0x12C,
            ChannelRxDescriptorRingLength = 0x130,
            ChannelInterruptEnable = 0x134,
            ChannelRxInterruptWatchdogTimer = 0x138,
            ChannelCurrentApplicationTransmitDescriptor = 0x144,
            ChannelCurrentApplicationReceiveDescriptor = 0x14C,
            ChannelCurrentApplicationTransmitBuffer = 0x154,
            ChannelCurrentApplicationReceiveBuffer = 0x15C,
            ChannelStatus = 0x160,
            ChannelMissedFrameCount = 0x16C,
        }
    }
}
