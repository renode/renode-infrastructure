//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.SPI
{
    // Aspeed AST2600 Firmware Memory Controller (FMC)
    // Reference: QEMU hw/ssi/aspeed_smc.c (ast2600 FMC variant)
    //
    // The FMC provides SPI flash access through two bus regions:
    //   1. "registers" at 0x1E620000 — control/status/DMA registers
    //   2. "flash" at 0x20000000 — memory-mapped flash window
    //
    // The flash window supports two modes controlled by CE0 Control Register:
    //   - Normal mode (type=0): reads/writes go to backing MappedMemory
    //   - User mode (type=3): reads/writes send SPI bytes to GenericSpiFlash
    //
    // User mode is used by the Linux spi-aspeed-smc driver to send JEDEC
    // Read ID (0x9F) and other SPI commands to identify the flash chip.
    //
    // The DMA engine reads/writes through the system bus so it can access
    // both the flash window (at 0x20000000) and DRAM (at 0x80000000).
    //
    // AST2600-specific features:
    //   - DMA grant handshake (0xAEED0000 request / 0xDEEA0000 clear)
    //   - WDT2 alternate boot control
    //   - Read timing calibration with failure injection
    //
    // For boot: u-boot SPL runs from the flash window. During init it
    // uses DMA checksum for SPI timing calibration, then loads the next
    // stage from flash to DRAM via DMA.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_FMC : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public Aspeed_FMC(IMachine machine, MappedMemory flashMemory) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            this.flashMemory = flashMemory;
            this.spiFlash = new GenericSpiFlash(flashMemory,
                manufacturerId: 0xEF, memoryType: 0x40, capacityCode: 0x20);

            var dict = new Dictionary<int, IGPIO>();
            dict[0] = new GPIO();  // DMA completion IRQ
            Connections = new ReadOnlyDictionary<int, IGPIO>(dict);

            dmaCtrl = 0;
            dmaFlashAddr = 0;
            dmaDramAddr = 0;
            dmaLen = 0;
            dmaChecksum = 0;

            // CE0 defaults: normal mode (type=0), CE_STOP_ACTIVE
            ceType = DefaultCECtrl & 0x3;
            ceStopActive = (DefaultCECtrl & 0x4) != 0;

            DefineRegisters();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x200;

        public override void Reset()
        {
            base.Reset();
            spiFlash.Reset();
            dmaCtrl = 0;
            dmaFlashAddr = 0;
            dmaDramAddr = 0;
            dmaLen = 0;
            dmaChecksum = 0;
            ceType = DefaultCECtrl & 0x3;
            ceStopActive = (DefaultCECtrl & 0x4) != 0;
            Connections[0].Unset();
        }

        // --- Register region (0x1E620000) ---

        [ConnectionRegion("registers")]
        public override uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [ConnectionRegion("registers")]
        public override void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        // --- Flash region (0x20000000) ---

        [ConnectionRegion("flash")]
        public byte FlashReadByte(long offset)
        {
            if(IsUserMode())
            {
                return spiFlash.Transmit(0);
            }
            return flashMemory.ReadByte(offset);
        }

        [ConnectionRegion("flash")]
        public void FlashWriteByte(long offset, byte value)
        {
            if(IsUserMode())
            {
                spiFlash.Transmit(value);
            }
            else
            {
                flashMemory.WriteByte(offset, value);
            }
        }

        [ConnectionRegion("flash")]
        public uint FlashReadDoubleWord(long offset)
        {
            if(IsUserMode())
            {
                uint result = 0;
                for(int i = 0; i < 4; i++)
                {
                    result |= (uint)spiFlash.Transmit(0) << (i * 8);
                }
                return result;
            }
            return flashMemory.ReadDoubleWord(offset);
        }

        [ConnectionRegion("flash")]
        public void FlashWriteDoubleWord(long offset, uint value)
        {
            if(IsUserMode())
            {
                for(int i = 0; i < 4; i++)
                {
                    spiFlash.Transmit((byte)(value >> (i * 8)));
                }
            }
            else
            {
                flashMemory.WriteDoubleWord(offset, value);
            }
        }

        // --- CE0 mode tracking ---

        private void UpdateCEMode(uint value)
        {
            var newType = value & 0x3;
            var stopActive = (value & 0x4) != 0;

            if(!ceStopActive && stopActive)
            {
                // CE deasserted — end SPI transaction
                spiFlash.FinishTransmission();
            }

            ceType = newType;
            ceStopActive = stopActive;
        }

        private bool IsUserMode()
        {
            return ceType == UserModeType && !ceStopActive;
        }

        private void DefineRegisters()
        {
            // 0x00 — CE Type Setting Register
            // AST2600 default: CE0 = SPI (0x2), write-enabled
            Registers.Configuration.Define(this, DefaultConf)
                .WithValueField(0, 32, name: "FMC_CONF");

            // 0x04 — CE Control Register (4-byte addr mode per CE)
            Registers.CEControl.Define(this, 0x0)
                .WithValueField(0, 32, name: "CE_CTRL");

            // 0x08 — Interrupt Control and Status
            Registers.InterruptControl.Define(this, 0x0)
                .WithValueField(0, 32, name: "INTR_CTRL",
                    writeCallback: (_, value) =>
                    {
                        // Store the full value; bits[2:0] = enables, bits[11:9] = status
                        // DMA status bit (11) is read-only, set by DMA completion
                    });

            // 0x0C — Command Control Register
            Registers.CommandControl.Define(this, 0x0)
                .WithValueField(0, 8, name: "CE_CMD_CTRL");

            // 0x10-0x20 — CE0-CE2 Control Registers
            Registers.CE0Control.Define(this, DefaultCECtrl)
                .WithValueField(0, 32, name: "CE0_CTRL",
                    writeCallback: (_, value) => UpdateCEMode((uint)value));
            Registers.CE1Control.Define(this, DefaultCECtrl)
                .WithValueField(0, 32, name: "CE1_CTRL");
            Registers.CE2Control.Define(this, DefaultCECtrl)
                .WithValueField(0, 32, name: "CE2_CTRL");

            // 0x30-0x38 — Segment Address Registers
            Registers.SegmentAddr0.Define(this, DefaultSeg0)
                .WithValueField(0, 32, name: "SEG_ADDR0");
            Registers.SegmentAddr1.Define(this, DefaultSeg1)
                .WithValueField(0, 32, name: "SEG_ADDR1");
            Registers.SegmentAddr2.Define(this, 0x0)
                .WithValueField(0, 32, name: "SEG_ADDR2");

            // 0x50 — Misc Control #1
            Registers.MiscControl1.Define(this, 0x0)
                .WithValueField(0, 32, name: "MISC_CTRL1");

            // 0x54 — Dummy Data
            Registers.DummyData.Define(this, 0x0)
                .WithValueField(0, 8, name: "DUMMY_DATA");

            // 0x64 — FMC WDT2 Control (alternate boot)
            Registers.WDT2Control.Define(this, 0x0)
                .WithFlag(0, name: "WDT2_EN")
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "BOOT_SOURCE")
                .WithFlag(5, name: "SINGLE_BOOT")
                .WithFlag(6, name: "ALT_BOOT_MODE")
                .WithReservedBits(7, 25);

            // 0x80 — DMA Control/Status
            Registers.DMAControl.Define(this, 0x0)
                .WithValueField(0, 32, name: "DMA_CTRL",
                    valueProviderCallback: _ => dmaCtrl,
                    writeCallback: (_, value) => HandleDmaCtrlWrite((uint)value));

            // 0x84 — DMA Flash Side Address
            Registers.DMAFlashAddr.Define(this, 0x0)
                .WithValueField(0, 32, name: "DMA_FLASH_ADDR",
                    valueProviderCallback: _ => dmaFlashAddr,
                    writeCallback: (_, value) =>
                    {
                        if(IsDmaGranted())
                        {
                            dmaFlashAddr = (uint)(value & DmaFlashMask);
                        }
                    });

            // 0x88 — DMA DRAM Side Address
            Registers.DMADramAddr.Define(this, 0x0)
                .WithValueField(0, 32, name: "DMA_DRAM_ADDR",
                    valueProviderCallback: _ => dmaDramAddr,
                    writeCallback: (_, value) =>
                    {
                        if(IsDmaGranted())
                        {
                            dmaDramAddr = (uint)(value & DmaDramMask);
                        }
                    });

            // 0x8C — DMA Length
            Registers.DMALength.Define(this, 0x0)
                .WithValueField(0, 32, name: "DMA_LEN",
                    valueProviderCallback: _ => dmaLen,
                    writeCallback: (_, value) =>
                    {
                        if(IsDmaGranted())
                        {
                            dmaLen = (uint)(value & DmaLenMask);
                        }
                    });

            // 0x90 — DMA Checksum (read-only result)
            Registers.DMAChecksum.Define(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "DMA_CHECKSUM",
                    valueProviderCallback: _ => dmaChecksum);

            // 0x94 — Read Timing Compensation
            Registers.Timings.Define(this, 0x0)
                .WithValueField(0, 32, name: "TIMINGS",
                    valueProviderCallback: _ => timingsReg,
                    writeCallback: (_, value) => { timingsReg = (uint)value; });
        }

        // --- DMA Engine ---

        private void HandleDmaCtrlWrite(uint value)
        {
            // AST2600 DMA grant handshake
            // Preserve existing request/grant bits
            value |= (dmaCtrl & (DmaCtrlRequest | DmaCtrlGrant));

            if(value == DmaGrantRequestMagic)
            {
                // Automatically grant request
                dmaCtrl |= (DmaCtrlRequest | DmaCtrlGrant);
                this.Log(LogLevel.Debug, "DMA grant requested and auto-granted");
                return;
            }

            if(value == DmaGrantClearMagic)
            {
                // Clear request/grant
                dmaCtrl &= ~(DmaCtrlRequest | DmaCtrlGrant);
                this.Log(LogLevel.Debug, "DMA grant cleared");
                return;
            }

            if(!IsDmaGranted())
            {
                this.Log(LogLevel.Warning, "DMA operation without grant");
                return;
            }

            if((value & DmaCtrlEnable) == 0)
            {
                // Disable DMA
                dmaCtrl = value;
                DmaStop();
                return;
            }

            // Check if DMA already in progress (completion bit set means done)
            if((dmaCtrl & DmaCtrlEnable) != 0 && (intrCtrlValue & IntrDmaStatus) == 0)
            {
                this.Log(LogLevel.Warning, "DMA already in progress");
                return;
            }

            dmaCtrl = value;

            if((dmaCtrl & DmaCtrlCksum) != 0)
            {
                if((dmaCtrl & DmaCtrlCalib) != 0)
                {
                    DmaCalibration();
                }
                DmaChecksum();
            }
            else
            {
                DmaReadWrite();
            }

            DmaDone();

            // Clear grant after operation
            dmaCtrl &= ~(DmaCtrlRequest | DmaCtrlGrant);
        }

        private void DmaChecksum()
        {
            if((dmaCtrl & DmaCtrlWrite) != 0)
            {
                this.Log(LogLevel.Warning, "Invalid DMA direction for checksum");
                return;
            }

            uint len = AlignedDmaLen();
            dmaChecksum = 0;

            this.Log(LogLevel.Debug, "DMA checksum: flash=0x{0:X8} len=0x{1:X}", dmaFlashAddr, len);

            while(len > 0)
            {
                uint data;
                try
                {
                    data = sysbus.ReadDoubleWord(dmaFlashAddr);
                }
                catch
                {
                    this.Log(LogLevel.Warning, "DMA flash read failed at 0x{0:X8}", dmaFlashAddr);
                    return;
                }

                dmaChecksum += data;
                dmaFlashAddr += 4;
                len -= 4;
                dmaLen = len;
            }

            // Inject read failure for high-speed timing calibration
            if(ShouldInjectFailure())
            {
                dmaChecksum = 0xBADC0DE;
                this.Log(LogLevel.Debug, "DMA checksum: injecting failure for calibration");
            }
        }

        private void DmaReadWrite()
        {
            uint len = AlignedDmaLen();
            bool isWrite = (dmaCtrl & DmaCtrlWrite) != 0;

            this.Log(LogLevel.Debug, "DMA {0}: flash=0x{1:X8} dram=0x{2:X8} len=0x{3:X}",
                isWrite ? "write" : "read", dmaFlashAddr, dmaDramAddr, len);

            while(len > 0)
            {
                try
                {
                    if(isWrite)
                    {
                        // DRAM → Flash
                        uint data = sysbus.ReadDoubleWord(dmaDramAddr);
                        sysbus.WriteDoubleWord(dmaFlashAddr, data);
                    }
                    else
                    {
                        // Flash → DRAM
                        uint data = sysbus.ReadDoubleWord(dmaFlashAddr);
                        sysbus.WriteDoubleWord(dmaDramAddr, data);
                    }
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Warning, "DMA transfer failed: {0}", e.Message);
                    return;
                }

                dmaChecksum += sysbus.ReadDoubleWord(dmaFlashAddr);
                dmaDramAddr += 4;
                dmaFlashAddr += 4;
                len -= 4;
                dmaLen = len;
            }
        }

        private void DmaCalibration()
        {
            // Extract calibration parameters from DMA_CTRL
            uint delay = (dmaCtrl >> DmaCtrlDelayShift) & 0xF;
            uint hclkMask = (dmaCtrl >> DmaCtrlFreqShift) & 0xF;
            uint hclkDiv = HclkDivisor(hclkMask);
            uint hclkShift = (hclkDiv - 1) * 4;

            // Update timing register
            if(hclkDiv > 0 && hclkDiv < 6)
            {
                timingsReg &= ~(0xFu << (int)hclkShift);
                timingsReg |= (delay << (int)hclkShift);
            }

            // Update CE0 clock frequency
            uint ce0Ctrl = RegistersCollection.Read((long)Registers.CE0Control);
            ce0Ctrl &= ~(0xFu << CeCtrlClockFreqShift);
            ce0Ctrl |= ((hclkDiv & 0xF) << CeCtrlClockFreqShift);
            RegistersCollection.Write((long)Registers.CE0Control, ce0Ctrl);

            this.Log(LogLevel.Debug, "DMA calibration: hclk_div={0} delay={1}", hclkDiv, delay);
        }

        private bool ShouldInjectFailure()
        {
            if((dmaCtrl & DmaCtrlCalib) == 0)
            {
                return false;
            }

            uint delay = (dmaCtrl >> DmaCtrlDelayShift) & 0xF;
            uint hclkMask = (dmaCtrl >> DmaCtrlFreqShift) & 0xF;
            uint hclkDiv = HclkDivisor(hclkMask);

            switch(hclkDiv)
            {
                case 4: case 5: case 6: case 7: case 8:
                case 9: case 10: case 11: case 12: case 13:
                case 14: case 15: case 16:
                    return false;
                case 3:
                    return (delay & 0x7) < 1;
                case 2:
                    return (delay & 0x7) < 2;
                case 1:
                    return true; // > 100MHz, always fail
                default:
                    return false;
            }
        }

        private static uint HclkDivisor(uint mask)
        {
            // HCLK/1 .. HCLK/16 lookup table (from QEMU)
            byte[] divisors = { 15, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 0 };
            for(int i = 0; i < divisors.Length; i++)
            {
                if(mask == divisors[i])
                {
                    return (uint)(i + 1);
                }
            }
            return 1;
        }

        private void DmaDone()
        {
            // Set DMA status bit in interrupt register
            intrCtrlValue |= IntrDmaStatus;
            RegistersCollection.Write((long)Registers.InterruptControl, intrCtrlValue);

            // Raise IRQ if DMA interrupt enabled
            if((intrCtrlValue & IntrDmaEn) != 0)
            {
                Connections[0].Set();
            }
        }

        private void DmaStop()
        {
            intrCtrlValue &= ~IntrDmaStatus;
            RegistersCollection.Write((long)Registers.InterruptControl, intrCtrlValue);
            dmaChecksum = 0;
            Connections[0].Unset();
        }

        private bool IsDmaGranted()
        {
            return (dmaCtrl & DmaCtrlGrant) != 0;
        }

        private uint AlignedDmaLen()
        {
            // AST2600: DMA length starts at 1 byte, align up to 4
            uint len = dmaLen + 1; // dma_start_length = 1
            return (len + 3) & ~3u;
        }

        // --- Fields ---

        private new readonly IBusController sysbus;
        private readonly MappedMemory flashMemory;
        private readonly GenericSpiFlash spiFlash;

        private uint dmaCtrl;
        private uint dmaFlashAddr;
        private uint dmaDramAddr;
        private uint dmaLen;
        private uint dmaChecksum;
        private uint timingsReg;
        private uint intrCtrlValue;
        private uint ceType;
        private bool ceStopActive;

        // --- Constants ---

        // CE0 default: SPI type, write enabled
        private const uint DefaultConf = (0x2u << 0) | (1u << 16);
        // CE default control: read mode, READ cmd (0x03), CE_STOP_ACTIVE
        private const uint DefaultCECtrl = (0x03u << 16) | (1u << 2);
        // Segment 0: 0-128MB (in 8MB units: end=0x08, start=0x00)
        private const uint DefaultSeg0 = (0x08u << 24) | (0x00u << 16);
        // Segment 1: 128-256MB
        private const uint DefaultSeg1 = (0x10u << 24) | (0x08u << 16);

        // User mode type value
        private const uint UserModeType = 3;

        // DMA control bits
        private const uint DmaCtrlRequest = (1u << 31);
        private const uint DmaCtrlGrant   = (1u << 30);
        private const uint DmaCtrlCalib   = (1u << 3);
        private const uint DmaCtrlCksum   = (1u << 2);
        private const uint DmaCtrlWrite   = (1u << 1);
        private const uint DmaCtrlEnable  = (1u << 0);
        private const int  DmaCtrlDelayShift = 8;
        private const int  DmaCtrlFreqShift  = 4;

        // DMA magic values (AST2600)
        private const uint DmaGrantRequestMagic = 0xAEED0000;
        private const uint DmaGrantClearMagic   = 0xDEEA0000;

        // DMA address masks
        private const uint DmaFlashMask = 0xFFFFFFFC;
        private const uint DmaDramMask  = 0xFFFFFFFC;
        private const uint DmaLenMask   = 0x01FFFFFF;

        // Interrupt control bits
        private const uint IntrDmaStatus = (1u << 11);
        private const uint IntrDmaEn     = (1u << 3);

        // CE control
        private const int CeCtrlClockFreqShift = 8;

        private enum Registers
        {
            Configuration    = 0x00,
            CEControl        = 0x04,
            InterruptControl = 0x08,
            CommandControl   = 0x0C,
            CE0Control       = 0x10,
            CE1Control       = 0x14,
            CE2Control       = 0x18,
            SegmentAddr0     = 0x30,
            SegmentAddr1     = 0x34,
            SegmentAddr2     = 0x38,
            MiscControl1     = 0x50,
            DummyData        = 0x54,
            WDT2Control      = 0x64,
            DMAControl       = 0x80,
            DMAFlashAddr     = 0x84,
            DMADramAddr      = 0x88,
            DMALength        = 0x8C,
            DMAChecksum      = 0x90,
            Timings          = 0x94,
        }
    }
}
