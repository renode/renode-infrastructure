//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService : IDoubleWordPeripheral
    {
        public virtual uint ReadDoubleWord(long offset)
        {
            return Read<RegistersMacAndMmc>(macAndMmcRegisters, "MAC and MMC", offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
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

        [UiAccessible]
        public string[,] GetCoutersInfo()
        {
            var table = new Table();
            table.AddRow("Name", "Value");
            table.AddRow(nameof(txByteCounter), Convert.ToString(txByteCounter.Value));
            table.AddRow(nameof(txPacketCounter), Convert.ToString(txPacketCounter.Value));
            table.AddRow(nameof(txUnicastPacketCounter), Convert.ToString(txUnicastPacketCounter.Value));
            table.AddRow(nameof(txMulticastPacketCounter), Convert.ToString(txMulticastPacketCounter.Value));
            table.AddRow(nameof(txBroadcastPacketCounter), Convert.ToString(txBroadcastPacketCounter.Value));
            table.AddRow(nameof(txGoodByteCounter), Convert.ToString(txGoodByteCounter.Value));
            table.AddRow(nameof(txGoodPacketCounter), Convert.ToString(txGoodPacketCounter.Value));
            table.AddRow(nameof(rxPacketCounter), Convert.ToString(rxPacketCounter.Value));
            table.AddRow(nameof(rxByteCounter), Convert.ToString(rxByteCounter.Value));
            table.AddRow(nameof(rxGoodByteCounter), Convert.ToString(rxGoodByteCounter.Value));
            table.AddRow(nameof(rxBroadcastPacketCounter), Convert.ToString(rxBroadcastPacketCounter.Value));
            table.AddRow(nameof(rxMulticastPacketCounter), Convert.ToString(rxMulticastPacketCounter.Value));
            table.AddRow(nameof(rxCrcErrorPacketCounter), Convert.ToString(rxCrcErrorPacketCounter.Value));
            table.AddRow(nameof(rxUnicastPacketCounter), Convert.ToString(rxUnicastPacketCounter.Value));
            table.AddRow(nameof(rxFifoPacketCounter), Convert.ToString(rxFifoPacketCounter.Value));
            for(var i = 0; i < NumberOfIpcCounters; i++)
            {
                var name = (IpcCounter)i;
                table.AddRow($"IPC {name} Packets", Convert.ToString(rxIpcPacketCounter[i].Value));
                table.AddRow($"IPC {name} Bytes", Convert.ToString(rxIpcByteCounter[i].Value));
            }
            return table.ToArray();
        }

        // This property works similar to `sysbus LogPeripheralAccess` command,
        // but supports regions. Setting this to `null` disables logging.
        public LogLevel PeripheralAccessLogLevel { get; set; } = null;

        protected IEnumRegisterField<DMAChannelInterruptMode> dmaInterruptMode;

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
                    .WithFlag(12, out loopbackEnabled, name: "MACCR.LM (LM)")
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
                    .WithEnumField<DoubleWordRegister, RegisterSourceAddressOperation>(28, 3, out sourceAddressOperation, name: "MACCR.SARC (SARC)")
                    .WithTaggedFlag("MACCR.ARPEN (ARPEN)", 31)
                },
                {(long)RegistersMacAndMmc.ExtendedOperatingModeConfiguration, new DoubleWordRegister(this)
                    .WithTag("MACECR.GPSL (GPSL)", 0, 14)
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out crcCheckDisable, name: "MACECR.DCRCC (DCRCC)")
                    .WithTaggedFlag("MACECR.SPEN (SPEN)", 17)
                    .WithTaggedFlag("MACECR.USP (USP)", 18)
                    .If(dmaChannels.Length > 1)
                        .Then(r => r.WithFlag(19, out packetDuplicationControl, name: "MACECR.PDC (PDC)"))
                        .Else(r => r.WithReservedBits(19, 1))
                    .WithReservedBits(20, 4)
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
                    .WithFlag(4, out ptpMessageTypeInterrupt, FieldMode.Read, name: "MACISR.PMTIS (PMTIS)")
                    .WithFlag(5, out lowPowerIdleInterrupt, FieldMode.Read, name: "MACISR.LPIIS (LPIIS)")
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("MACISR.MMCIS (MMCIS)", 8)
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => MMCRxInterruptStatus, name: "MACISR.MMCRXIS (MMCRXIS)")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => MMCTxInterruptStatus, name: "MACISR.MMCTXIS (MMCTXIS)")
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
                    .If(dmaChannels.Length > 1)
                        .Then(r => r
                            .WithValueField(16, dmaChannels.Length, out dmaChannelSelect, name: "MACA0HR.DCS (DCS)")
                            .WithReservedBits(16 + dmaChannels.Length, 15 - dmaChannels.Length))
                        .Else(r => r.WithReservedBits(16, 15))
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
                    .WithValueField(0, 8, name: "MACA1HR.ADDRHI (ADDRHI) [MAC1.E]",
                        valueProviderCallback: _ => MAC1.E,
                        writeCallback: (_, value) => MAC1 = MAC1.WithNewOctets(e: (byte)value))
                    .WithValueField(8, 8, name: "MACA1HR.ADDRHI (ADDRHI) [MAC1.F]",
                        valueProviderCallback: _ => MAC1.F,
                        writeCallback: (_, value) => MAC1 = MAC1.WithNewOctets(f: (byte)value))
                    .WithReservedBits(16, 8)
                    .WithTag("MACA1HR.MBC (MBC)", 24, 6)
                    .WithTaggedFlag("MACA1HR.SA (SA)", 30)
                    .WithTaggedFlag("MACA1HR.AE (AE)", 31)
                },
                {(long)RegistersMacAndMmc.MACAddress1Low, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 8, name: "MACA1LR.ADDRLO (ADDRLO) [MAC.A]",
                        valueProviderCallback: _ => MAC1.A,
                        writeCallback: (_, value) => MAC1 = MAC1.WithNewOctets(a: (byte)value))
                    .WithValueField(8, 8, name: "MACA1LR.ADDRLO (ADDRLO) [MAC1.B]",
                        valueProviderCallback: _ => MAC1.B,
                        writeCallback: (_, value) => MAC = MAC.WithNewOctets(b: (byte)value))
                    .WithValueField(16, 8, name: "MACA1LR.ADDRLO (ADDRLO) [MAC1.C]",
                        valueProviderCallback: _ => MAC1.C,
                        writeCallback: (_, value) => MAC1 = MAC1.WithNewOctets(c: (byte)value))
                    .WithValueField(24, 8, name: "MACA1LR.ADDRLO (ADDRLO) [MAC1.D]",
                        valueProviderCallback: _ => MAC1.D,
                        writeCallback: (_, value) => MAC1 = MAC1.WithNewOctets(d: (byte)value))
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
                    .WithFlag(0, out rxPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXGBPKTIS (MMC Receive Good Bad Packet Counter Interrupt Status)")
                    .WithFlag(1, out rxByteCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXGBOCTIS (MMC Receive Good Bad Octet Counter Interrupt Status)")
                    .WithFlag(2, out rxGoodByteCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXGOCTIS (MMC Receive Good Octet Counter Interrupt Status)")
                    .WithFlag(3, out rxBroadcastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXBCGPIS (MMC Receive Broadcast Good Packet Counter Interrupt Status)")
                    .WithFlag(4, out rxMulticastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXMCGPIS (MMC Receive Multicast Good Packet Counter Interrupt Status)")
                    .WithFlag(5, out rxCrcErrorPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXCRCERPIS (MMC Receive CRC Error Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXALGNERPIS (MMC Receive Alignment Error Packet Counter Interrupt Status)", 6)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXRUNTPIS (MMC Receive Runt Packet Counter Interrupt Status)", 7)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXJABERPIS (MMC Receive Jabber Error Packet Counter Interrupt Status)", 8)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXUSIZEGPIS (MMC Receive Undersize Good Packet Counter Interrupt Status)", 9)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXOSIZEGPIS (MMC Receive Oversize Good Packet Counter Interrupt Status)", 10)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX64OCTGBPIS (MMC Receive 64 Octet Good Bad Packet Counter Interrupt Status)", 11)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX65T127OCTGBPIS (MMC Receive Broadcast Good Bad Packet Counter Interrupt Status)", 12)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX128T255OCTGBPIS (MMC Receive 128 to 255 Octet Good Bad Packet Counter Interrupt Status)", 13)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX256T511OCTGBPIS (MMC Receive 256 to 511 Octet Good Bad Packet Counter Interrupt Status)", 14)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX512T1023OCTGBPIS (MMC Receive 512 to 1023 Octet Good Bad Packet Counter Interrupt Status)", 15)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RX1024TMAXOCTGBPIS (MMC Receive 1024 to Maximum Octet Good Bad Packet Counter Interrupt Status)", 16)
                    .WithFlag(17, out rxUnicastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXUCGPIS (MMC Receive Unicast Good Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXLENERPIS (MMC Receive Length Error Packet Counter Interrupt Status)", 18)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXORANGEPIS (MMC Receive Out Of Range Error Packet Counter Interrupt Status)", 19)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXPAUSPIS (MMC Receive Pause Packet Counter Interrupt Status)", 20)
                    .WithFlag(21, out rxFifoPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_RX_INTERRUPT.RXFOVPIS (MMC Receive FIFO Overflow Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXVLANGBPIS (MMC Receive VLAN Good Bad Packet Counter Interrupt Status)", 22)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXWDOGPIS (MMC Receive Watchdog Error Packet Counter Interrupt Status)", 23)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXRCVERRPIS (MMC Receive Error Packet Counter Interrupt Status)", 24)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXCTRLPIS (MMC Receive Control Packet Counter Interrupt Status)", 25)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXLPIUSCIS (MMC Receive LPI Microsecond Counter Interrupt Status)", 26)
                    .WithTaggedFlag("MMC_RX_INTERRUPT.RXLPITRCIS (MMC Receive LPI Transition Counter Interrupt Status)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.MMCTxInterrupt, new DoubleWordRegister(this)
                    .WithFlag(0, out txByteCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXGBOCTIS (MMC Transmit Good Bad Octet Counter Interrupt Status)")
                    .WithFlag(1, out txPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXGBPKTIS (MMC Transmit Good Bad Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXBCGPIS (MMC Transmit Broadcast Good Packet Counter Interrupt Status)", 2)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXMCGPIS (MMC Transmit Multicast Good Packet Counter Interrupt Status)", 3)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX64OCTGBPIS (MMC Transmit 64 Octet Good Bad Packet Counter Interrupt Status)", 4)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX65T127OCTGBPIS (MMC Transmit 65 to 127 Octet Good Bad Packet Counter Interrupt Status)", 5)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX128T255OCTGBPIS (MMC Transmit 128 to 255 Octet Good Bad Packet Counter Interrupt Status)", 6)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX256T511OCTGBPIS (MMC Transmit 256 to 511 Octet Good Bad Packet Counter Interrupt Status)", 7)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX512T1023OCTGBPIS (MMC Transmit 512 to 1023 Octet Good Bad Packet Counter Interrupt Status)", 8)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TX1024TMAXOCTGBPIS (MMC Transmit 1024 to Maximum Octet Good Bad Packet Counter Interrupt Status)", 9)
                    .WithFlag(10, out txUnicastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXUCGBPIS (MMC Transmit Unicast Good Bad Packet Counter Interrupt Status)")
                    .WithFlag(11, out txMulticastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXMCGBPIS (MMC Transmit Multicast Good Bad Packet Counter Interrupt Status)")
                    .WithFlag(12, out txBroadcastPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXBCGBPIS (MMC Transmit Broadcast Good Bad Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXUFLOWERPIS (MMC Transmit Underflow Error Packet Counter Interrupt Status)", 13)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXSCOLGPIS (MMC Transmit Single Collision Good Packet Counter Interrupt Status)", 14)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXMCOLGPIS (MMC Transmit Multiple Collision Good Packet Counter Interrupt Status)", 15)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXDEFPIS (MMC Transmit Deferred Packet Counter Interrupt Status)", 16)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXLATCOLPIS (MMC Transmit Late Collision Packet Counter Interrupt Status)", 17)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXEXCOLPIS (MMC Transmit Excessive Collision Packet Counter Interrupt Status)", 18)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXCARERPIS (MMC Transmit Carrier Error Packet Counter Interrupt Status)", 19)
                    .WithFlag(20, out txGoodByteCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXGOCTIS (MMC Transmit Good Octet Counter Interrupt Status)")
                    .WithFlag(21, out txGoodPacketCounterInterrupt, FieldMode.ReadToClear, name: "MMC_TX_INTERRUPT.TXGPKTIS (MMC Transmit Good Packet Counter Interrupt Status)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXEXDEFPIS (MMC Transmit Excessive Deferral Packet Counter Interrupt Status)", 22)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXPAUSPIS (MMC Transmit Pause Packet Counter Interrupt Status)", 23)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXVLANGPIS (MMC Transmit VLAN Good Packet Counter Interrupt Status)", 24)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXOSIZEGPIS (MMC Transmit Oversize Good Packet Counter Interrupt Status)", 25)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXLPIUSCIS (MMC Transmit LPI Microsecond Counter Interrupt Status)", 26)
                    .WithTaggedFlag("MMC_TX_INTERRUPT.TXLPITRCIS (MMC Transmit LPI Transition Counter Interrupt Status)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.MMCRxInterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out rxPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXGBPKTIM (MMC Receive Good Bad Packet Counter Interrupt Mask)")
                    .WithFlag(1, out rxByteCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXGBOCTIM (MMC Receive Good Bad Octet Counter Interrupt Mask)")
                    .WithFlag(2, out rxGoodByteCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXGOCTIM (MMC Receive Good Octet Counter Interrupt Mask)")
                    .WithFlag(3, out rxBroadcastPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXBCGPIM (MMC Receive Broadcast Good Packet Counter Interrupt Mask)")
                    .WithFlag(4, out rxMulticastPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXMCGPIM (MMC Receive Multicast Good Packet Counter Interrupt Mask)")
                    .WithFlag(5, out rxCrcErrorPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXCRCERPIM (MMC Receive CRC Error Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXALGNERPIM (MMC Receive Alignment Error Packet Counter Interrupt Mask)", 6)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXRUNTPIM (MMC Receive Runt Packet Counter Interrupt Mask)", 7)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXJABERPIM (MMC Receive Jabber Error Packet Counter Interrupt Mask)", 8)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXUSIZEGPIM (MMC Receive Undersize Good Packet Counter Interrupt Mask)", 9)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXOSIZEGPIM (MMC Receive Oversize Good Packet Counter Interrupt Mask)", 10)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX64OCTGBPIM (MMC Receive 64 Octet Good Bad Packet Counter Interrupt Mask)", 11)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX65T127OCTGBPIM (MMC Receive Broadcast Good Bad Packet Counter Interrupt Mask)", 12)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX128T255OCTGBPIM (MMC Receive 128 to 255 Octet Good Bad Packet Counter Interrupt Mask)", 13)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX256T511OCTGBPIM (MMC Receive 256 to 511 Octet Good Bad Packet Counter Interrupt Mask)", 14)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX512T1023OCTGBPIM (MMC Receive 512 to 1023 Octet Good Bad Packet Counter Interrupt Mask)", 15)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RX1024TMAXOCTGBPIM (MMC Receive 1024 to Maximum Octet Good Bad Packet Counter Interrupt Mask)", 16)
                    .WithFlag(17, out rxUnicastPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXUCGPIM (MMC Receive Unicast Good Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXLENERPIM (MMC Receive Length Error Packet Counter Interrupt Mask)", 18)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXORANGEPIM (MMC Receive Out Of Range Error Packet Counter Interrupt Mask)", 19)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXPAUSPIM (MMC Receive Pause Packet Counter Interrupt Mask)", 20)
                    .WithFlag(21, out rxFifoPacketCounterInterruptEnable, name: "MMC_RX_INTERRUPT_MASK.RXFOVPIM (MMC Receive FIFO Overflow Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXVLANGBPIM (MMC Receive VLAN Good Bad Packet Counter Interrupt Mask)", 22)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXWDOGPIM (MMC Receive Watchdog Error Packet Counter Interrupt Mask)", 23)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXRCVERRPIM (MMC Receive Error Packet Counter Interrupt Mask)", 24)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXCTRLPIM (MMC Receive Control Packet Counter Interrupt Mask)", 25)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXLPIUSCIM (MMC Receive LPI Microsecond Counter Interrupt Mask)", 26)
                    .WithTaggedFlag("MMC_RX_INTERRUPT_MASK.RXLPITRCIM (MMC Receive LPI Transition Counter Interrupt Mask)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.MMCTxInterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out txByteCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXGBOCTIM (MMC Transmit Good Bad Octet Counter Interrupt Mask)")
                    .WithFlag(1, out txPacketCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXGBPKTIM (MMC Transmit Good Bad Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXBCGPIM (MMC Transmit Broadcast Good Packet Counter Interrupt Mask)", 2)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXMCGPIM (MMC Transmit Multicast Good Packet Counter Interrupt Mask)", 3)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX64OCTGBPIM (MMC Transmit 64 Octet Good Bad Packet Counter Interrupt Mask)", 4)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX65T127OCTGBPIM (MMC Transmit 65 to 127 Octet Good Bad Packet Counter Interrupt Mask)", 5)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX128T255OCTGBPIM (MMC Transmit 128 to 255 Octet Good Bad Packet Counter Interrupt Mask)", 6)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX256T511OCTGBPIM (MMC Transmit 256 to 511 Octet Good Bad Packet Counter Interrupt Mask)", 7)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX512T1023OCTGBPIM (MMC Transmit 512 to 1023 Octet Good Bad Packet Counter Interrupt Mask)", 8)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TX1024TMAXOCTGBPIM (MMC Transmit 1024 to Maximum Octet Good Bad Packet Counter Interrupt Mask)", 9)
                    .WithFlag(10, out txUnicastPacketCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXUCGBPIM (MMC Transmit Unicast Good Bad Packet Counter Interrupt Mask)")
                    .WithFlag(11, out txMulticastPacketCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXMCGBPIM (MMC Transmit Multicast Good Bad Packet Counter Interrupt Mask)")
                    .WithFlag(12, out txBroadcastPacketCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXBCGBPIM (MMC Transmit Broadcast Good Bad Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXUFLOWERPIM (MMC Transmit Underflow Error Packet Counter Interrupt Mask)", 13)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXSCOLGPIM (MMC Transmit Single Collision Good Packet Counter Interrupt Mask)", 14)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXMCOLGPIM (MMC Transmit Multiple Collision Good Packet Counter Interrupt Mask)", 15)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXDEFPIM (MMC Transmit Deferred Packet Counter Interrupt Mask)", 16)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXLATCOLPIM (MMC Transmit Late Collision Packet Counter Interrupt Mask)", 17)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXEXCOLPIM (MMC Transmit Excessive Collision Packet Counter Interrupt Mask)", 18)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXCARERPIM (MMC Transmit Carrier Error Packet Counter Interrupt Mask)", 19)
                    .WithFlag(20, out txGoodByteCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXGOCTIM (MMC Transmit Good Octet Counter Interrupt Mask)")
                    .WithFlag(21, out txGoodPacketCounterInterruptEnable, name: "MMC_TX_INTERRUPT_MASK.TXGPKTIM (MMC Transmit Good Packet Counter Interrupt Mask)")
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXEXDEFPIM (MMC Transmit Excessive Deferral Packet Counter Interrupt Mask)", 22)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXPAUSPIM (MMC Transmit Pause Packet Counter Interrupt Mask)", 23)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXVLANGPIM (MMC Transmit VLAN Good Packet Counter Interrupt Mask)", 24)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXOSIZEGPIM (MMC Transmit Oversize Good Packet Counter Interrupt Mask)", 25)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXLPIUSCIM (MMC Transmit LPI Microsecond Counter Interrupt Mask)", 26)
                    .WithTaggedFlag("MMC_TX_INTERRUPT_MASK.TXLPITRCIM (MMC Transmit LPI Transition Counter Interrupt Mask)", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersMacAndMmc.TxOctetCountGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txByteCounter, FieldMode.Read, name: "TX_OCTET_COUNT_GOOD_BAD.TXOCTGB (TXOCTGB)")
                },
                {(long)RegistersMacAndMmc.TxPacketCountGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txPacketCounter, FieldMode.Read, name: "TX_PACKET_COUNT_GOOD_BAD.TXPKTGB (TXPKTGB)")
                },
                {(long)RegistersMacAndMmc.TxUnicastPacketsGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txUnicastPacketCounter, FieldMode.Read, name: "TX_UNICAST_PACKETS_GOOD_BAD.TXUCASTGB (TXUCASTGB)")
                },
                {(long)RegistersMacAndMmc.TxMulticastPacketsGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txMulticastPacketCounter, FieldMode.Read, name: "TX_MULTICAST_PACKETS_GOOD_BAD.TXMCASTGB (TXMCASTGB)")
                },
                {(long)RegistersMacAndMmc.TxBroadcastPacketsGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txBroadcastPacketCounter, FieldMode.Read, name: "TX_BROADCAST_PACKETS_GOOD_BAD.TXBCASTGB (TXBCASTGB)")
                },
                {(long)RegistersMacAndMmc.TxSingleCollisionGoodPackets, new DoubleWordRegister(this)
                    // collisions are impossible
                    .WithValueField(0, 32, FieldMode.Read, name: "TX_SINGLE_COLLISION_GOOD_PACKETS.TXSNGLCOLG (TXSNGLCOLG)")
                },
                {(long)RegistersMacAndMmc.TxMultipleCollisionGoodPackets, new DoubleWordRegister(this)
                    // collisions are impossible
                    .WithValueField(0, 32, FieldMode.Read, name: "TX_MULTIPLE_COLLISION_GOOD_PACKETS.TXMULTCOLG (TXMULTCOLG)")
                },
                {(long)RegistersMacAndMmc.TxOctetCountGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txGoodByteCounter, FieldMode.Read, name: "TX_OCTET_COUNT_GOOD.TXOCTG (TXOCTG)")
                },
                {(long)RegistersMacAndMmc.TxPacketCountGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txGoodPacketCounter, FieldMode.Read, name: "TX_PACKET_COUNT_GOOD.TXPKTG (TXPKTG)")
                },
                {(long)RegistersMacAndMmc.RxPacketsCountGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxPacketCounter, FieldMode.Read, name: "RX_PACKETS_COUNT_GOOD_BAD.RXPKTGB (RXPKTGB)")
                },
                {(long)RegistersMacAndMmc.RxOctetCountGoodBad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxByteCounter, FieldMode.Read, name: "RX_OCTET_COUNT_GOOD_BAD.RXOCTGB (RXOCTGB)")
                },
                {(long)RegistersMacAndMmc.RxOctetCountGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxGoodByteCounter, FieldMode.Read, name: "RX_OCTET_COUNT_GOOD.RXOCTG (RXOCTG)")
                },
                {(long)RegistersMacAndMmc.RxBroadcastPacketsGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxBroadcastPacketCounter, FieldMode.Read, name: "RX_BROADCAST_PACKETS_GOOD.RXBCASTG (RXBCASTG)")
                },
                {(long)RegistersMacAndMmc.RxMulticastPacketsGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxMulticastPacketCounter, FieldMode.Read, name: "RX_MULTICAST_PACKETS_GOOD.RXMCASTG (RXMCASTG)")
                },
                {(long)RegistersMacAndMmc.RxCRCErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxCrcErrorPacketCounter, FieldMode.Read, name: "RX_CRC_ERROR_PACKETS.RXCRCERR (RXCRCERR)")
                },
                {(long)RegistersMacAndMmc.RxAlignmentErrorPackets, new DoubleWordRegister(this)
                    .WithTag("RX_ALIGNMENT_ERROR_PACKETS.RXALGNERR (RXALGNERR)", 0, 32)
                },
                {(long)RegistersMacAndMmc.RxUnicastPacketsGood, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxUnicastPacketCounter, FieldMode.Read, name: "RX_UNICAST_PACKETS_GOOD.RXUCASTG (RXUCASTG)")
                },
                {(long)RegistersMacAndMmc.RxFifoOverflowPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxFifoPacketCounter, name: "Rx_FIFO_Overflow_Packets.RXFIFOOVFL (RXFIFOOVFL)")
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
                {(long)RegistersMacAndMmc.MmcIpcRxInterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV4Good], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4GPIM (RXIPV4GPIM)")
                    .WithFlag(1, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV4HeaderError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4HERPIM (RXIPV4HERPIM)")
                    .WithFlag(2, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV4NoPayload], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4NOPAYPIM (RXIPV4NOPAYPIM)")
                    .WithFlag(3, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV4Fragmented], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4FRAGPIM (RXIPV4FRAGPIM)")
                    .WithFlag(4, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV4UDPChecksumDisabled], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4UDSBLPIM (RXIPV4UDSBLPIM)")
                    .WithFlag(5, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV6Good], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6GPIM (RXIPV6GPIM)")
                    .WithFlag(6, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV6HeaderError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6HERPIM (RXIPV6HERPIM)")
                    .WithFlag(7, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IpV6NoPayload], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6NOPAYPIM (RXIPV6NOPAYPIM)")
                    .WithFlag(8, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.UdpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXUDPGPIM (RXUDPGPIM)")
                    .WithFlag(9, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.UdpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXUDPERPIM (RXUDPERPIM)")
                    .WithFlag(10, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.TcpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXTCPGPIM (RXTCPGPIM)")
                    .WithFlag(11, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.TcpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXTCPERPIM (RXTCPERPIM)")
                    .WithFlag(12, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IcmpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXICMPGPIM (RXICMPGPIM)")
                    .WithFlag(13, out rxIpcPacketCounterInterruptEnable[(int)IpcCounter.IcmpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXICMPERPIM (RXICMPERPIM)")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV4Good], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4GOIM (RXIPV4GOIM)")
                    .WithFlag(17, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV4HeaderError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4HEROIM (RXIPV4HEROIM)")
                    .WithFlag(18, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV4NoPayload], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4NOPAYOIM (RXIPV4NOPAYOIM)")
                    .WithFlag(19, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV4Fragmented], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4FRAGOIM (RXIPV4FRAGOIM)")
                    .WithFlag(20, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV4UDPChecksumDisabled], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV4UDSBLOIM (RXIPV4UDSBLOIM)")
                    .WithFlag(21, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV6Good], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6GOIM (RXIPV6GOIM)")
                    .WithFlag(22, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV6HeaderError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6HEROIM (RXIPV6HEROIM)")
                    .WithFlag(23, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IpV6NoPayload], name: "MMC_IPC_RX_INTERRUPT_MASK.RXIPV6NOPAYOIM (RXIPV6NOPAYOIM)")
                    .WithFlag(24, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.UdpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXUDPGOIM (RXUDPGOIM)")
                    .WithFlag(25, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.UdpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXUDPEROIM (RXUDPEROIM)")
                    .WithFlag(26, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.TcpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXTCPGOIM (RXTCPGOIM)")
                    .WithFlag(27, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.TcpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXTCPEROIM (RXTCPEROIM)")
                    .WithFlag(28, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IcmpGood], name: "MMC_IPC_RX_INTERRUPT_MASK.RXICMPGOIM (RXICMPGOIM)")
                    .WithFlag(29, out rxIpcByteCounterInterruptEnable[(int)IpcCounter.IcmpError], name: "MMC_IPC_RX_INTERRUPT_MASK.RXICMPEROIM (RXICMPEROIM)")
                    .WithReservedBits(30, 2)
                },
                {(long)RegistersMacAndMmc.MmcIpcRxInterrupt, new DoubleWordRegister(this)
                    .WithFlag(0, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV4Good], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4GPIS (RXIPV4GPIS)")
                    .WithFlag(1, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV4HeaderError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4HERPIS (RXIPV4HERPIS)")
                    .WithFlag(2, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV4NoPayload], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4NOPAYPIS (RXIPV4NOPAYPIS)")
                    .WithFlag(3, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV4Fragmented], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4FRAGPIS (RXIPV4FRAGPIS)")
                    .WithFlag(4, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV4UDPChecksumDisabled], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4UDSBLPIS (RXIPV4UDSBLPIS)")
                    .WithFlag(5, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV6Good], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6GPIS (RXIPV6GPIS)")
                    .WithFlag(6, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV6HeaderError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6HERPIS (RXIPV6HERPIS)")
                    .WithFlag(7, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IpV6NoPayload], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6NOPAYPIS (RXIPV6NOPAYPIS)")
                    .WithFlag(8, out rxIpcPacketCounterInterrupt[(int)IpcCounter.UdpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXUDPGPIS (RXUDPGPIS)")
                    .WithFlag(9, out rxIpcPacketCounterInterrupt[(int)IpcCounter.UdpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXUDPERPIS (RXUDPERPIS)")
                    .WithFlag(10, out rxIpcPacketCounterInterrupt[(int)IpcCounter.TcpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXTCPGPIS (RXTCPGPIS)")
                    .WithFlag(11, out rxIpcPacketCounterInterrupt[(int)IpcCounter.TcpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXTCPERPIS (RXTCPERPIS)")
                    .WithFlag(12, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IcmpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXICMPGPIS (RXICMPGPIS)")
                    .WithFlag(13, out rxIpcPacketCounterInterrupt[(int)IpcCounter.IcmpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXICMPERPIS (RXICMPERPIS)")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV4Good], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4GOIS (RXIPV4GOIS)")
                    .WithFlag(17, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV4HeaderError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4HEROIS (RXIPV4HEROIS)")
                    .WithFlag(18, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV4NoPayload], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4NOPAYOIS (RXIPV4NOPAYOIS)")
                    .WithFlag(19, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV4Fragmented], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4FRAGOIS (RXIPV4FRAGOIS)")
                    .WithFlag(20, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV4UDPChecksumDisabled], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV4UDSBLOIS (RXIPV4UDSBLOIS)")
                    .WithFlag(21, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV6Good], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6GOIS (RXIPV6GOIS)")
                    .WithFlag(22, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV6HeaderError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6HEROIS (RXIPV6HEROIS)")
                    .WithFlag(23, out rxIpcByteCounterInterrupt[(int)IpcCounter.IpV6NoPayload], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXIPV6NOPAYOIS (RXIPV6NOPAYOIS)")
                    .WithFlag(24, out rxIpcByteCounterInterrupt[(int)IpcCounter.UdpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXUDPGOIS (RXUDPGOIS)")
                    .WithFlag(25, out rxIpcByteCounterInterrupt[(int)IpcCounter.UdpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXUDPEROIS (RXUDPEROIS)")
                    .WithFlag(26, out rxIpcByteCounterInterrupt[(int)IpcCounter.TcpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXTCPGOIS (RXTCPGOIS)")
                    .WithFlag(27, out rxIpcByteCounterInterrupt[(int)IpcCounter.TcpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXTCPEROIS (RXTCPEROIS)")
                    .WithFlag(28, out rxIpcByteCounterInterrupt[(int)IpcCounter.IcmpGood], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXICMPGOIS (RXICMPGOIS)")
                    .WithFlag(29, out rxIpcByteCounterInterrupt[(int)IpcCounter.IcmpError], FieldMode.ReadToClear, name: "MMC_IPC_RX_INTERRUPT.RXICMPEROIS (RXICMPEROIS)")
                    .WithReservedBits(30, 2)
                },
                {(long)RegistersMacAndMmc.RxIpv4GoodPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV4Good], FieldMode.Read, name: "RXIPV4_GOOD_PACKETS.RXIPV4GDPKT (RXIPV4GDPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4HeaderErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV4HeaderError], FieldMode.Read, name: "RXIPV4_HEADER_ERROR_PACKETS.RXIPV4HDRERRPKT (RXIPV4HDRERRPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4NoPayloadPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV4NoPayload], FieldMode.Read, name: "RXIPV4_NO_PAYLOAD_PACKETS.RXIPV4NOPAYPKT (RXIPV4NOPAYPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4FragmentedPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV4Fragmented], FieldMode.Read, name: "RXIPV4_FRAGMENTED_PACKETS.RXIPV4FRAGPKT (RXIPV4FRAGPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4UdpChecksumDisabledPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV4UDPChecksumDisabled], FieldMode.Read, name: "RXIPV4_UDP_CHECKSUM_DISABLED_PACKETS.RXIPV4UDSBLPKT (RXIPV4UDSBLPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6GoodPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV6Good], FieldMode.Read, name: "RXIPV6_GOOD_PACKETS.RXIPV6GDPKT (RXIPV6GDPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6HeaderErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV6HeaderError], FieldMode.Read, name: "RXIPV6_HEADER_ERROR_PACKETS.RXIPV6HDRERRPKT (RXIPV6HDRERRPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6NoPayloadPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IpV6NoPayload], FieldMode.Read, name: "RXIPV6_NO_PAYLOAD_PACKETS.RXIPV6NOPAYPKT (RXIPV6NOPAYPKT)")
                },
                {(long)RegistersMacAndMmc.RxUdpGoodPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.UdpGood], FieldMode.Read, name: "RXUDP_GOOD_PACKETS.RXUDPGDPKT (RXUDPGDPKT)")
                },
                {(long)RegistersMacAndMmc.RxUdpErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.UdpError], FieldMode.Read, name: "RXUDP_ERROR_PACKETS.RXUDPERRPKT (RXUDPERRPKT)")
                },
                {(long)RegistersMacAndMmc.RxTcpGoodPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.TcpGood], FieldMode.Read, name: "RXTCP_GOOD_PACKETS.RXTCPGDPKT (RXTCPGDPKT)")
                },
                {(long)RegistersMacAndMmc.RxTcpErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.TcpError], FieldMode.Read, name: "RXTCP_ERROR_PACKETS.RXTCPERRPKT (RXTCPERRPKT)")
                },
                {(long)RegistersMacAndMmc.RxIcmpGoodPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IcmpGood], FieldMode.Read, name: "RXICMP_GOOD_PACKETS.RXICMPGDPKT (RXICMPGDPKT)")
                },
                {(long)RegistersMacAndMmc.RxIcmpErrorPackets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcPacketCounter[(int)IpcCounter.IcmpError], FieldMode.Read, name: "RXICMP_ERROR_PACKETS.RXICMPERRPKT (RXICMPERRPKT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4GoodOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV4Good], FieldMode.Read, name: "RXIPV4_GOOD_OCTETS.RXIPV4GDOCT (RXIPV4GDOCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4HeaderErrorOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV4HeaderError], FieldMode.Read, name: "RXIPV4_HEADER_ERROR_OCTETS.RXIPV4HDRERROCT (RXIPV4HDRERROCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4NoPayloadOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV4NoPayload], FieldMode.Read, name: "RXIPV4_NO_PAYLOAD_OCTETS.RXIPV4NOPAYOCT (RXIPV4NOPAYOCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4FragmentedOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV4Fragmented], FieldMode.Read, name: "RXIPV4_FRAGMENTED_OCTETS.RXIPV4FRAGOCT (RXIPV4FRAGOCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv4UdpChecksumDisableOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV4UDPChecksumDisabled], FieldMode.Read, name: "RXIPV4_UDP_CHECKSUM_DISABLE_OCTETS.RXIPV4UDSBLOCT (RXIPV4UDSBLOCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6GoodOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV6Good], FieldMode.Read, name: "RXIPV6_GOOD_OCTETS.RXIPV6GDOCT (RXIPV6GDOCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6HeaderErrorOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV6HeaderError], FieldMode.Read, name: "RXIPV6_HEADER_ERROR_OCTETS.RXIPV6HDRERROCT (RXIPV6HDRERROCT)")
                },
                {(long)RegistersMacAndMmc.RxIpv6NoPayloadOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IpV6NoPayload], FieldMode.Read, name: "RXIPV6_NO_PAYLOAD_OCTETS.RXIPV6NOPAYOCT (RXIPV6NOPAYOCT)")
                },
                {(long)RegistersMacAndMmc.RxUdpGoodOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.UdpGood], FieldMode.Read, name: "RXUDP_GOOD_OCTETS.RXUDPGDOCT (RXUDPGDOCT)")
                },
                {(long)RegistersMacAndMmc.RxUdpErrorOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.UdpError], FieldMode.Read, name: "RXUDP_ERROR_OCTETS.RXUDPERROCT (RXUDPERROCT)")
                },
                {(long)RegistersMacAndMmc.RxTcpGoodOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.TcpGood], FieldMode.Read, name: "RXTCP_GOOD_OCTETS.RXTCPGDOCT (RXTCPGDOCT)")
                },
                {(long)RegistersMacAndMmc.RxTcpErrorOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.TcpError], FieldMode.Read, name: "RXTCP_ERROR_OCTETS.RXTCPERROCT (RXTCPERROCT)")
                },
                {(long)RegistersMacAndMmc.RxIcmpGoodOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IcmpGood], FieldMode.Read, name: "RXICMP_GOOD_OCTETS.RXICMPGDOCT (RXICMPGDOCT)")
                },
                {(long)RegistersMacAndMmc.RxIcmpErrorOctets, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxIpcByteCounter[(int)IpcCounter.IcmpError], FieldMode.Read, name: "RXICMP_ERROR_OCTETS.RXICMPERROCT (RXICMPERROCT)")
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
                    // values are intentionally empty because we send packets immediately
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "MTLTxQDR.TXQPAUSED (TXQPAUSED)")
                    .WithEnumField<DoubleWordRegister, MTLTxQueueReadControllerStatus>(1, 2, FieldMode.Read,
                        valueProviderCallback: _ => MTLTxQueueReadControllerStatus.Idle, name: "MTLTxQDR.TRCSTS (TRCSTS)")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "MTLTxQDR.TWCSTS (TWCSTS)")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "MTLTxQDR.TXQSTS (TXQSTS)")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "MTLTxQDR.TXSTSFSTS (TXSTSFSTS)")
                    .WithReservedBits(6, 10)
                    .WithValueField(16, 3, FieldMode.Read, valueProviderCallback: _ => 0, name: "MTLTxQDR.PTXQ (PTXQ)")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 3, FieldMode.Read, valueProviderCallback: _ => 0, name: "MTLTxQDR.STXSTSF (STXSTSF)")
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
                {(long)RegistersMTL.RxQueueOperatingMode, new DoubleWordRegister(this)
                    .WithTag("MTLRxQOMR.RTC (RTC)", 0, 2)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("MTLRxQOMR.FUP (FUP)", 3)
                    .WithTaggedFlag("MTLRxQOMR.FEP (FEP)", 4)
                    .WithTaggedFlag("MTLRxQOMR.RSF (RSF)", 5)
                    .WithTaggedFlag("MTLRxQOMR.DIS_TCP_EF (DIS_TCP_EF)", 6)
                    .WithTaggedFlag("MTLRxQOMR.EHFC (EHFC)", 7)
                    .WithTag("MTLRxQOMR.RFA (RFA)", 8, 4)
                    .WithReservedBits(12, 2)
                    .WithTag("MTLRxQOMR.RFD (RFD)", 14, 4)
                    .WithReservedBits(18, 2)
                    .WithValueField(20, 5, FieldMode.Read, valueProviderCallback: _ => 0b11111, name: "MTLRxQOMR.RQS (RQS)")
                    .WithReservedBits(25, 7)
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
            var map = new Dictionary<long, DoubleWordRegister>()
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
                    .WithEnumField(16, 2, out dmaInterruptMode, name: "DMAMR.INTM (Interrupt Mode)",
                        changeCallback: (previous, current) =>
                        {
                            if(current == DMAChannelInterruptMode.Reserved)
                            {
                                this.Log(LogLevel.Warning, "Attempted to set DMA interrupt mode to a reserved value. Reverting back to the previous value {0}", previous);
                                dmaInterruptMode.Value = previous;
                                return;
                            }

                            UpdateInterrupts();
                        })
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
                    .WithFlags(0, dmaChannels.Length, name: "DMAISR.DCxIS (DMA Channel Interrupt Status)",
                        valueProviderCallback: (i, _) => dmaChannels[i].Interrupts)
                    .WithReservedBits(dmaChannels.Length, 16 - dmaChannels.Length)
                    .WithTaggedFlag("DMAISR.MTLIS (MTL Interrupt Status)", 16)
                    .WithTaggedFlag("DMAISR.MACIS (MAC Interrupt Status)", 17)
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RegistersDMA.DebugStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("DMADSR.AXWHSTS (AHB Master Write Channel)", 0)
                    .WithReservedBits(1, 7)
                    .For((r, i) =>
                    {
                        var offset = i * 8;
                        r.WithEnumField<DoubleWordRegister, DMARxProcessState>(8 + offset, 4, FieldMode.Read,
                            valueProviderCallback: _ => dmaChannels[i].DmaRxState, name: $"DMADSR.RPS{i} (DMA Channel {i} Receive Process State)")
                        .WithEnumField<DoubleWordRegister, DMATxProcessState>(12 + offset, 4, FieldMode.Read,
                            valueProviderCallback: _ => dmaChannels[i].DmaTxState, name: $"DMADSR.TPS{i} (DMA Channel {i} Transmit Process State)");
                    }, 0, dmaChannels.Length)
                },
            };

            foreach(var channel in dmaChannels)
            {
                channel.DefineChannelRegisters(ref map);
            }

            return map;
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
        private IFlagRegisterField loopbackEnabled;
        private IFlagRegisterField checksumOffloadEnable;
        private IFlagRegisterField crcCheckDisable;
        private IFlagRegisterField packetDuplicationControl;
        private IFlagRegisterField ptpMessageTypeInterrupt;
        private IFlagRegisterField lowPowerIdleInterrupt;
        private IFlagRegisterField timestampInterrupt;
        private IFlagRegisterField ptpMessageTypeInterruptEnable;
        private IFlagRegisterField lowPowerIdleInterruptEnable;
        private IFlagRegisterField timestampInterruptEnable;
        private IFlagRegisterField miiBusy;
        private IFlagRegisterField clause45PhyEnable;
        private IEnumRegisterField<MIIOperation> miiOperation;
        private IEnumRegisterField<RegisterSourceAddressOperation> sourceAddressOperation;
        private IValueRegisterField miiRegisterOrDeviceAddress;
        private IValueRegisterField miiPhy;
        private IValueRegisterField miiData;
        private IValueRegisterField miiAddress;
        private IValueRegisterField dmaChannelSelect;
        private IFlagRegisterField rxUnicastPacketCounterInterrupt;
        private IFlagRegisterField rxFifoPacketCounterInterrupt;
        private IFlagRegisterField rxCrcErrorPacketCounterInterrupt;
        private IFlagRegisterField rxMulticastPacketCounterInterrupt;
        private IFlagRegisterField rxBroadcastPacketCounterInterrupt;
        private IFlagRegisterField rxGoodByteCounterInterrupt;
        private IFlagRegisterField rxByteCounterInterrupt;
        private IFlagRegisterField rxPacketCounterInterrupt;
        private IFlagRegisterField txGoodPacketCounterInterrupt;
        private IFlagRegisterField txGoodByteCounterInterrupt;
        private IFlagRegisterField txBroadcastPacketCounterInterrupt;
        private IFlagRegisterField txMulticastPacketCounterInterrupt;
        private IFlagRegisterField txUnicastPacketCounterInterrupt;
        private IFlagRegisterField txPacketCounterInterrupt;
        private IFlagRegisterField txByteCounterInterrupt;
        private IFlagRegisterField rxUnicastPacketCounterInterruptEnable;
        private IFlagRegisterField rxFifoPacketCounterInterruptEnable;
        private IFlagRegisterField rxCrcErrorPacketCounterInterruptEnable;
        private IFlagRegisterField rxMulticastPacketCounterInterruptEnable;
        private IFlagRegisterField rxBroadcastPacketCounterInterruptEnable;
        private IFlagRegisterField rxGoodByteCounterInterruptEnable;
        private IFlagRegisterField rxByteCounterInterruptEnable;
        private IFlagRegisterField rxPacketCounterInterruptEnable;
        private IFlagRegisterField txGoodPacketCounterInterruptEnable;
        private IFlagRegisterField txGoodByteCounterInterruptEnable;
        private IFlagRegisterField txBroadcastPacketCounterInterruptEnable;
        private IFlagRegisterField txMulticastPacketCounterInterruptEnable;
        private IFlagRegisterField txUnicastPacketCounterInterruptEnable;
        private IFlagRegisterField txPacketCounterInterruptEnable;
        private IFlagRegisterField txByteCounterInterruptEnable;
        private IValueRegisterField txByteCounter;
        private IValueRegisterField txPacketCounter;
        private IValueRegisterField txUnicastPacketCounter;
        private IValueRegisterField txMulticastPacketCounter;
        private IValueRegisterField txBroadcastPacketCounter;
        private IValueRegisterField txGoodByteCounter;
        private IValueRegisterField txGoodPacketCounter;
        private IValueRegisterField rxPacketCounter;
        private IValueRegisterField rxByteCounter;
        private IValueRegisterField rxGoodByteCounter;
        private IValueRegisterField rxBroadcastPacketCounter;
        private IValueRegisterField rxMulticastPacketCounter;
        private IValueRegisterField rxCrcErrorPacketCounter;
        private IValueRegisterField rxUnicastPacketCounter;
        private IValueRegisterField rxFifoPacketCounter;
        private IFlagRegisterField enableTimestamp;
        private IFlagRegisterField enableTimestampForAll;

        private readonly IFlagRegisterField[] rxIpcPacketCounterInterruptEnable;
        private readonly IFlagRegisterField[] rxIpcByteCounterInterruptEnable;
        private readonly IFlagRegisterField[] rxIpcPacketCounterInterrupt;
        private readonly IFlagRegisterField[] rxIpcByteCounterInterrupt;
        private readonly IValueRegisterField[] rxIpcPacketCounter;
        private readonly IValueRegisterField[] rxIpcByteCounter;

        private readonly DoubleWordRegisterCollection macAndMmcRegisters;
        // MAC Transaction Layer
        private readonly DoubleWordRegisterCollection mtlRegisters;
        private readonly DoubleWordRegisterCollection dmaRegisters;

        private readonly int NumberOfIpcCounters = Enum.GetNames(typeof(IpcCounter)).Length;

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
            TxOctetCountGoodBad = 0x714,
            TxPacketCountGoodBad = 0x718,
            TxUnicastPacketsGoodBad = 0x73C,
            TxMulticastPacketsGoodBad = 0x740,
            TxBroadcastPacketsGoodBad = 0x744,
            TxSingleCollisionGoodPackets = 0x74C,
            TxMultipleCollisionGoodPackets = 0x750,
            TxOctetCountGood = 0x764,
            TxPacketCountGood = 0x768,
            RxPacketsCountGoodBad = 0x780,
            RxOctetCountGoodBad = 0x784,
            RxOctetCountGood = 0x788,
            RxBroadcastPacketsGood = 0x78C,
            RxMulticastPacketsGood = 0x790,
            RxCRCErrorPackets = 0x794,
            RxAlignmentErrorPackets = 0x798,
            RxUnicastPacketsGood = 0x7C4,
            RxFifoOverflowPackets = 0x7D4,
            TxLPIMicrodecondTimer = 0x7EC,
            TxLPITransitionCounter = 0x7F0,
            RxLPIMicrosecondCounter = 0x7F4,
            RxLPITransitionCounter = 0x7F8,
            MmcIpcRxInterruptMask = 0x800,
            MmcIpcRxInterrupt = 0x808,
            RxIpv4GoodPackets = 0x810,
            RxIpv4HeaderErrorPackets = 0x814,
            RxIpv4NoPayloadPackets = 0x818,
            RxIpv4FragmentedPackets = 0x81C,
            RxIpv4UdpChecksumDisabledPackets = 0x820,
            RxIpv6GoodPackets = 0x824,
            RxIpv6HeaderErrorPackets = 0x828,
            RxIpv6NoPayloadPackets = 0x82C,
            RxUdpGoodPackets = 0x830,
            RxUdpErrorPackets = 0x834,
            RxTcpGoodPackets = 0x838,
            RxTcpErrorPackets = 0x83C,
            RxIcmpGoodPackets = 0x840,
            RxIcmpErrorPackets = 0x844,
            RxIpv4GoodOctets = 0x850,
            RxIpv4HeaderErrorOctets = 0x854,
            RxIpv4NoPayloadOctets = 0x858,
            RxIpv4FragmentedOctets = 0x85C,
            RxIpv4UdpChecksumDisableOctets = 0x860,
            RxIpv6GoodOctets = 0x864,
            RxIpv6HeaderErrorOctets = 0x868,
            RxIpv6NoPayloadOctets = 0x86C,
            RxUdpGoodOctets = 0x870,
            RxUdpErrorOctets = 0x874,
            RxTcpGoodOctets = 0x878,
            RxTcpErrorOctets = 0x87C,
            RxIcmpGoodOctets = 0x880,
            RxIcmpErrorOctets = 0x884,
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
        }

        public enum RegistersDMAChannel : long
        {
            Control = 0x00,
            TransmitControl = 0x04,
            ReceiveControl = 0x08,
            TxDescriptorListAddress = 0x14,
            RxDescriptorListAddress = 0x1C,
            TxDescriptorTailPointer = 0x20,
            RxDescriptorTailPointer = 0x28,
            TxDescriptorRingLength = 0x2C,
            RxDescriptorRingLength = 0x30,
            InterruptEnable = 0x34,
            RxInterruptWatchdogTimer = 0x38,
            CurrentApplicationTransmitDescriptor = 0x44,
            CurrentApplicationReceiveDescriptor = 0x4C,
            CurrentApplicationTransmitBuffer = 0x54,
            CurrentApplicationReceiveBuffer = 0x5C,
            Status = 0x60,
            MissedFrameCount = 0x6C,
        }

        public enum RegisterSourceAddressOperation : byte
        {
            MACAddressRegisterReserved0 = 0b000,
            MACAddressRegisterReserved1 = 0b001,
            MACAddressRegister0Insert   = 0b010,
            MACAddressRegister0Replace  = 0b011,
            MACAddressRegisterReserved2 = 0b100,
            MACAddressRegisterReserved3 = 0b101,
            MACAddressRegister1Insert   = 0b110,
            MACAddressRegister1Replace  = 0b111,
        }

        protected enum DMATxProcessState
        {
            Stopped             = 0b000, // Reset or Stop Receive Command issued
            FetchingDescriptor  = 0b001, // Fetching Tx Transfer Descriptor
            WaitingForStatus    = 0b010, // Waiting for status
            FetchingBufferData  = 0b011, // Reading Data from system memory buffer and queuing it to the Tx buffer (Tx FIFO)
            TimestampWriteState = 0b100, // Timestamp write state
            // Reserved         = 0b101, // Reserved for future use
            Suspended           = 0b110, // Tx Descriptor Unavailable or Tx Buffer Underflow
            ClosingDescriptor   = 0b111, // Closing Tx Descriptor
        }

        protected enum DMARxProcessState
        {
            Stopped             = 0b000, // Reset or Stop Receive Command issued
            FetchingDescriptor  = 0b001, // Fetching Rx Transfer Descriptor
            // Reserved         = 0b010, // Reserved for future use
            WaitingForPacket    = 0b011, // Waiting for Rx packet
            Suspended           = 0b100, // Rx Descriptor Unavailable
            ClosingDescriptor   = 0b101, // Closing the Rx Descriptor
            TimestampWriteState = 0b110, // Timestamp write state
            WritingBufferData   = 0b111, // Transferring the received packet data from the Rx buffer to the system memory
        }

        private enum MIIOperation : byte
        {
            Write = 0b01,
            PostReadAddressIncrement = 0b10,
            Read = 0b11,
        }

        private enum IpcCounter : int
        {
            IpV4Good,
            IpV4HeaderError,
            IpV4NoPayload,
            IpV4Fragmented,
            IpV4UDPChecksumDisabled,
            IpV6Good,
            IpV6HeaderError,
            IpV6NoPayload,
            UdpGood,
            UdpError,
            TcpGood,
            TcpError,
            IcmpGood,
            IcmpError,
        }

        private enum MTLTxQueueReadControllerStatus
        {
            Idle = 0b00,
            Read = 0b01,
            Waiting = 0b10,
            Flushing = 0b11,
        }
    }
}
