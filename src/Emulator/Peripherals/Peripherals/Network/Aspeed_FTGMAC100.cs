// Copyright (c) 2026 Microsoft
// Licensed under the MIT license.
//
// Aspeed AST2600 FTGMAC100 Ethernet MAC Controller
// Full network emulation with DMA descriptor TX/RX.
// Register semantics from QEMU hw/net/ftgmac100.c and Linux ftgmac100 driver.

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Aspeed_FTGMAC100 : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        public Aspeed_FTGMAC100(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            IRQ = new GPIO();
            phyRegs = new uint[32];
            Reset();
        }

        public override void Reset()
        {
            isr = 0;
            ier = 0;
            maccr = 0;
            dblac = 0x00022F00;
            rbsr = 0x640;
            itc = 0;
            aptc = 0;
            fcr = 0;
            revr = 0;
            fear = 0;
            tpafcr = 0;
            dmafifos = 0;
            maht0 = 0;
            maht1 = 0;
            nptxrBadr = 0;
            rxrBadr = 0;
            hptxrBadr = 0;
            txDescAddr = 0;
            rxDescAddr = 0;
            phycr = 0;
            phydata = 0;

            Array.Clear(phyRegs, 0, phyRegs.Length);
            phyRegs[MII_BMCR] = 0x1000;
            phyRegs[MII_BMSR] = 0x796D;
            phyRegs[MII_PHYID1] = 0x001C;
            phyRegs[MII_PHYID2] = 0xC916;
            phyRegs[MII_ANAR] = 0x01E1;
            phyRegs[MII_ANLPAR] = 0x45E1;
            phyRegs[MII_1000BTCR] = 0x0300;
            phyRegs[MII_1000BTSR] = 0x7C00;

            lock(receiveLock)
            {
                queue.Clear();
            }

            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
                case REG_ISR:
                    return isr;
                case REG_IER:
                    return ier;
                case REG_MAC_MADR:
                    return (uint)((MAC.A << 8) | MAC.B);
                case REG_MAC_LADR:
                    return (uint)((MAC.C << 24) | (MAC.D << 16) | (MAC.E << 8) | MAC.F);
                case REG_MAHT0:
                    return maht0;
                case REG_MAHT1:
                    return maht1;
                case REG_NPTXR_BADR:
                    return nptxrBadr;
                case REG_RXR_BADR:
                    return rxrBadr;
                case REG_HPTXR_BADR:
                    return hptxrBadr;
                case REG_ITC:
                    return itc;
                case REG_APTC:
                    return aptc;
                case REG_DBLAC:
                    return dblac;
                case REG_DMAFIFOS:
                    return dmafifos;
                case REG_REVR:
                    return revr;
                case REG_FEAR:
                    return fear;
                case REG_TPAFCR:
                    return tpafcr;
                case REG_RBSR:
                    return rbsr;
                case REG_MACCR:
                    return maccr;
                case REG_MACSR:
                    return 0;
                case REG_PHYCR:
                    return phycr;
                case REG_PHYDATA:
                    return phydata;
                case REG_FCR:
                    return fcr;
                default:
                    this.Log(LogLevel.Warning, "Unhandled read at 0x{0:X}", offset);
                    return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
                case REG_ISR:
                    isr &= ~value;
                    UpdateIRQ();
                    TryDequeueFrame();
                    break;
                case REG_IER:
                    ier = value;
                    UpdateIRQ();
                    break;
                case REG_MAC_MADR:
                    MAC = MAC.WithNewOctets(a: (byte)((value >> 8) & 0xFF), b: (byte)(value & 0xFF));
                    break;
                case REG_MAC_LADR:
                    MAC = MAC.WithNewOctets(
                        c: (byte)((value >> 24) & 0xFF),
                        d: (byte)((value >> 16) & 0xFF),
                        e: (byte)((value >> 8) & 0xFF),
                        f: (byte)(value & 0xFF));
                    break;
                case REG_MAHT0:
                    maht0 = value;
                    break;
                case REG_MAHT1:
                    maht1 = value;
                    break;
                case REG_NPTXPD:
                    if(TxEnabled)
                    {
                        SendFrames();
                    }
                    break;
                case REG_RXPD:
                    TryDequeueFrame();
                    break;
                case REG_NPTXR_BADR:
                    nptxrBadr = value;
                    txDescAddr = value;
                    break;
                case REG_RXR_BADR:
                    rxrBadr = value;
                    rxDescAddr = value;
                    break;
                case REG_HPTXR_BADR:
                    hptxrBadr = value;
                    break;
                case REG_ITC:
                    itc = value;
                    break;
                case REG_APTC:
                    aptc = value;
                    break;
                case REG_DBLAC:
                    dblac = value;
                    break;
                case REG_RBSR:
                    rbsr = value & 0x3FFF;
                    break;
                case REG_MACCR:
                    HandleMACCR(value);
                    break;
                case REG_PHYCR:
                    phycr = value;
                    HandlePHYCR(value);
                    break;
                case REG_PHYDATA:
                    phydata = value;
                    break;
                case REG_FCR:
                    fcr = value;
                    break;
                case REG_TPAFCR:
                    tpafcr = value;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled write at 0x{0:X} value=0x{1:X}", offset, value);
                    break;
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(receiveLock)
            {
                if(!RxEnabled)
                {
                    return;
                }

                if(frame.Bytes.Length < 14)
                {
                    this.Log(LogLevel.Warning, "Dropping runt frame ({0} bytes)", frame.Bytes.Length);
                    return;
                }

                if(!ShouldReceiveFrame(frame.DestinationMAC))
                {
                    this.Log(LogLevel.Noisy, "MAC filter dropped frame for {0}", frame.DestinationMAC);
                    return;
                }

                if(!DeliverFrame(frame))
                {
                    queue.Enqueue(frame);
                }
            }
        }

        public MACAddress MAC { get; set; }

        public event Action<EthernetFrame> FrameReady;

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        // --- TX path ---

        private void SendFrames()
        {
            var addr = txDescAddr;
            var desSize = TxDescriptorSize;
            var packetData = new List<byte>();

            while(true)
            {
                var des0 = sysbus.ReadDoubleWord(addr);

                if((des0 & TXDES0_TXDMA_OWN) == 0)
                {
                    break;
                }

                var des3 = sysbus.ReadDoubleWord(addr + 12);
                var bufSize = (int)(des0 & 0x3FFF);

                if(bufSize > 0 && des3 != 0)
                {
                    packetData.AddRange(sysbus.ReadBytes(des3, bufSize));
                }

                var isLastSegment = (des0 & TXDES0_LTS) != 0;
                var isEndOfRing = (des0 & TXDES0_EDOTR_ASPEED) != 0;

                des0 &= ~TXDES0_TXDMA_OWN;
                sysbus.WriteDoubleWord(addr, des0);

                if(isLastSegment && packetData.Count > 0)
                {
                    if(Misc.TryCreateFrameOrLogWarning(this, packetData.ToArray(), out var frame, addCrc: true))
                    {
                        this.Log(LogLevel.Debug, "TX: {0} bytes", packetData.Count);
                        FrameReady?.Invoke(frame);
                    }
                    isr |= INT_XPKT_ETH;
                    packetData.Clear();
                }

                if(isEndOfRing)
                {
                    addr = nptxrBadr;
                }
                else
                {
                    addr += (uint)desSize;
                }
            }

            txDescAddr = addr;
            isr |= INT_NO_NPTXBUF;
            UpdateIRQ();
        }

        // --- RX path ---

        private bool DeliverFrame(EthernetFrame frame)
        {
            var data = frame.Bytes;
            var written = 0;
            var first = true;
            var addr = rxDescAddr;
            var desSize = RxDescriptorSize;
            var maxBuf = (int)(rbsr & 0x3FFF);
            if(maxBuf == 0)
            {
                maxBuf = 0x640;
            }

            while(written < data.Length)
            {
                var des0 = sysbus.ReadDoubleWord(addr);

                if((des0 & RXDES0_RXPKT_RDY) != 0)
                {
                    isr |= INT_NO_RXBUF;
                    UpdateIRQ();
                    return false;
                }

                var des3 = sysbus.ReadDoubleWord(addr + 12);
                var isEndOfRing = (des0 & RXDES0_EDORR_ASPEED) != 0;
                var bufSize = Math.Min(maxBuf, data.Length - written);

                var chunk = new byte[bufSize];
                Array.Copy(data, written, chunk, 0, bufSize);
                sysbus.WriteBytes(chunk, des3);

                des0 = RXDES0_RXPKT_RDY | (uint)(bufSize & 0x3FFF);
                if(isEndOfRing)
                {
                    des0 |= RXDES0_EDORR_ASPEED;
                }

                if(first)
                {
                    des0 |= RXDES0_FRS;
                    first = false;
                }

                written += bufSize;
                if(written >= data.Length)
                {
                    des0 |= RXDES0_LRS;
                }

                if(frame.DestinationMAC.IsBroadcast)
                {
                    des0 |= RXDES0_BROADCAST;
                }
                else if((frame.DestinationMAC.A & 1) != 0)
                {
                    des0 |= RXDES0_MULTICAST;
                }

                sysbus.WriteDoubleWord(addr, des0);

                if(isEndOfRing)
                {
                    addr = rxrBadr;
                }
                else
                {
                    addr += (uint)desSize;
                }
            }

            rxDescAddr = addr;
            isr |= INT_RPKT_BUF;
            UpdateIRQ();
            this.Log(LogLevel.Debug, "RX: {0} bytes delivered", data.Length);
            return true;
        }

        private bool ShouldReceiveFrame(MACAddress dest)
        {
            if((maccr & MACCR_RX_ALL) != 0)
            {
                return true;
            }

            if(dest.IsBroadcast)
            {
                return (maccr & MACCR_RX_BROADPKT) != 0;
            }

            if((dest.A & 1) != 0)
            {
                return (maccr & MACCR_RX_MULTIPKT) != 0;
            }

            return dest.Equals(MAC);
        }

        private void TryDequeueFrame()
        {
            lock(receiveLock)
            {
                while(queue.Count > 0)
                {
                    var frame = queue.Peek();
                    if(DeliverFrame(frame))
                    {
                        queue.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        // --- Register helpers ---

        private void HandleMACCR(uint value)
        {
            if((value & MACCR_SW_RST) != 0)
            {
                var preserved = maccr & (MACCR_GIGA_MODE | MACCR_FAST_MODE);
                Reset();
                maccr = preserved;
            }
            else
            {
                maccr = value;
            }
        }

        private void HandlePHYCR(uint value)
        {
            var phyReg = (value >> 21) & 0x1F;

            if((value & PHYCR_MIIWR) != 0)
            {
                var writeData = phydata & 0xFFFF;
                if(phyReg < 32)
                {
                    phyRegs[phyReg] = writeData;
                    phyRegs[MII_BMSR] |= BMSR_LINK_ST;
                }
                phycr &= ~PHYCR_MIIWR;
            }
            else if((value & PHYCR_MIIRD) != 0)
            {
                uint readData = 0;
                if(phyReg < 32)
                {
                    readData = phyRegs[phyReg];
                }
                phydata = (readData << 16) | (phydata & 0xFFFF);
                phycr &= ~PHYCR_MIIRD;
            }
        }

        private void UpdateIRQ()
        {
            IRQ.Set((isr & ier) != 0);
        }

        private bool TxEnabled => (maccr & MACCR_TXDMA_EN) != 0 && (maccr & MACCR_TXMAC_EN) != 0;
        private bool RxEnabled => (maccr & MACCR_RXDMA_EN) != 0 && (maccr & MACCR_RXMAC_EN) != 0;

        private int TxDescriptorSize
        {
            get
            {
                var size = (int)(((dblac >> 16) & 0xF) * 8);
                return size < 16 ? 16 : size;
            }
        }

        private int RxDescriptorSize
        {
            get
            {
                var size = (int)(((dblac >> 12) & 0xF) * 8);
                return size < 16 ? 16 : size;
            }
        }

        // --- State ---

        private uint isr, ier, maccr;
        private uint dblac, rbsr, itc, aptc, fcr;
        private uint revr, fear, tpafcr, dmafifos;
        private uint maht0, maht1;
        private uint nptxrBadr, rxrBadr, hptxrBadr;
        private uint txDescAddr, rxDescAddr;
        private uint phycr, phydata;
        private readonly uint[] phyRegs;
        private readonly IBusController sysbus;
        private readonly object receiveLock = new object();
        private readonly Queue<EthernetFrame> queue = new Queue<EthernetFrame>();

        // --- Register offsets ---

        private const long REG_ISR          = 0x00;
        private const long REG_IER          = 0x04;
        private const long REG_MAC_MADR     = 0x08;
        private const long REG_MAC_LADR     = 0x0C;
        private const long REG_MAHT0        = 0x10;
        private const long REG_MAHT1        = 0x14;
        private const long REG_NPTXPD       = 0x18;
        private const long REG_RXPD         = 0x1C;
        private const long REG_NPTXR_BADR   = 0x20;
        private const long REG_RXR_BADR     = 0x24;
        private const long REG_HPTXPD       = 0x28;
        private const long REG_HPTXR_BADR   = 0x2C;
        private const long REG_ITC          = 0x30;
        private const long REG_APTC         = 0x34;
        private const long REG_DBLAC        = 0x38;
        private const long REG_DMAFIFOS     = 0x3C;
        private const long REG_REVR         = 0x40;
        private const long REG_FEAR         = 0x44;
        private const long REG_TPAFCR       = 0x48;
        private const long REG_RBSR         = 0x4C;
        private const long REG_MACCR        = 0x50;
        private const long REG_MACSR        = 0x54;
        private const long REG_PHYCR        = 0x60;
        private const long REG_PHYDATA      = 0x64;
        private const long REG_FCR          = 0x68;

        // --- TX descriptor des0 bits ---

        private const uint TXDES0_TXDMA_OWN     = 1u << 31;
        private const uint TXDES0_EDOTR_ASPEED  = 1u << 30;
        private const uint TXDES0_FTS           = 1u << 29;
        private const uint TXDES0_LTS           = 1u << 28;

        // --- RX descriptor des0 bits ---

        private const uint RXDES0_RXPKT_RDY     = 1u << 31;
        private const uint RXDES0_EDORR_ASPEED  = 1u << 30;
        private const uint RXDES0_FRS           = 1u << 29;
        private const uint RXDES0_LRS           = 1u << 28;
        private const uint RXDES0_BROADCAST     = 1u << 17;
        private const uint RXDES0_MULTICAST     = 1u << 16;

        // --- ISR/IER interrupt bits ---

        private const uint INT_RPKT_BUF     = 1u << 0;
        private const uint INT_RPKT_FIFO    = 1u << 1;
        private const uint INT_NO_RXBUF     = 1u << 2;
        private const uint INT_RPKT_LOST    = 1u << 3;
        private const uint INT_XPKT_ETH     = 1u << 4;
        private const uint INT_XPKT_FIFO    = 1u << 5;
        private const uint INT_NO_NPTXBUF   = 1u << 6;
        private const uint INT_PHYSTS_CHG   = 1u << 9;

        // --- MACCR bits ---

        private const uint MACCR_TXDMA_EN   = 1u << 0;
        private const uint MACCR_RXDMA_EN   = 1u << 1;
        private const uint MACCR_TXMAC_EN   = 1u << 2;
        private const uint MACCR_RXMAC_EN   = 1u << 3;
        private const uint MACCR_GIGA_MODE  = 1u << 9;
        private const uint MACCR_RX_ALL     = 1u << 14;
        private const uint MACCR_RX_MULTIPKT = 1u << 16;
        private const uint MACCR_RX_BROADPKT = 1u << 17;
        private const uint MACCR_FAST_MODE  = 1u << 19;
        private const uint MACCR_SW_RST     = 1u << 31;

        // --- PHYCR bits ---

        private const uint PHYCR_MIIRD = 1u << 26;
        private const uint PHYCR_MIIWR = 1u << 27;

        // --- PHY register indices ---

        private const int MII_BMCR      = 0;
        private const int MII_BMSR      = 1;
        private const int MII_PHYID1    = 2;
        private const int MII_PHYID2    = 3;
        private const int MII_ANAR      = 4;
        private const int MII_ANLPAR    = 5;
        private const int MII_1000BTCR  = 9;
        private const int MII_1000BTSR  = 10;
        private const uint BMSR_LINK_ST = 1u << 2;
    }
}
