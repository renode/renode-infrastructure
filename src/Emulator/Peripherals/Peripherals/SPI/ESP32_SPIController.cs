//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2024 Sean "xobs" Cross
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    // The ESP32 drives flash from SPI1, the general purpose controller; the ESP32-S2 and ESP32-S3
    // use SPIMEM1. Only the flash path is modelled, not the master and slave modes.
    public class ESP32_SPIController : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IHasMappedRegisters, IKnownSize
    {
        public ESP32_SPIController(IMachine machine, ESP32Generation generation = ESP32Generation.ESP32) : base(machine)
        {
            this.generation = generation;
            registersType = generation switch
            {
                ESP32Generation.ESP32 => typeof(Registers),
                ESP32Generation.ESP32S2 => typeof(RegistersS2),
                ESP32Generation.ESP32S3 => typeof(RegistersS3),
                _ => throw new ConstructionException($"{generation} is not supported by this model"),
            };
            registerMapper = new RegisterMapper(registersType);
            dataBuffer = new IValueRegisterField[DataBufferWords];
            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            TagRemainingRegisters(registersType);
            Reset();
        }

        public override void Reset()
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

        public string OffsetToString(long offset) => registerMapper.ToString(offset);

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x400;

        private static int BitsToBytes(ulong bitLengthMinusOne) => (int)((bitLengthMinusOne + 1) / 8);

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var command = new DoubleWordRegister(this)
                .WithReservedBits(0, 16)
                .WithTaggedFlag(IsSpiMemory ? "RESERVED16" : "FLASH_PER", 16)
                .WithTaggedFlag(IsSpiMemory ? "FLASH_PE" : "FLASH_PES", 17)
                .WithFlag(18, valueProviderCallback: _ => false, writeCallback: (_, value) => { if(value) { PerformUserTransfer(); } }, name: "USR")
                .WithTaggedFlag("FLASH_HPM", 19)
                .WithTaggedFlag("FLASH_RES", 20)
                .WithTaggedFlag("FLASH_DP", 21)
                .WithTaggedFlag("FLASH_CE", 22)
                .WithTaggedFlag("FLASH_BE", 23)
                .WithTaggedFlag("FLASH_SE", 24)
                .WithTaggedFlag("FLASH_PP", 25)
                .WithTaggedFlag("FLASH_WRSR", 26)
                .WithFlag(27, valueProviderCallback: _ => false, writeCallback: (_, value) => { if(value) { PerformFlashCommand(ReadStatusCommand, 1, useAddress: false); } }, name: "FLASH_RDSR")
                .WithFlag(28, valueProviderCallback: _ => false, writeCallback: (_, value) => { if(value) { PerformFlashCommand(ReadIdCommand, JedecIdBytes, useAddress: false); } }, name: "FLASH_RDID")
                .WithTaggedFlag("FLASH_WRDI", 29)
                .WithTaggedFlag("FLASH_WREN", 30)
                .WithFlag(31, valueProviderCallback: _ => false, writeCallback: (_, value) => { if(value) { PerformFlashCommand(ReadCommand, BitsToBytes(misoBitLength.Value), useAddress: true); } }, name: "FLASH_READ");

            var address = new DoubleWordRegister(this)
                .WithValueField(0, 32, out addressValue, name: "USR_ADDR_VALUE");

            var control = new DoubleWordRegister(this)
                .WithTag("COMMAND_FORMAT", 0, 10)
                .WithTaggedFlag("FCS_CRC_EN", 10)
                .WithTaggedFlag("TX_CRC_EN", 11)
                .WithTaggedFlag("WAIT_FLASH_IDLE_EN", 12)
                .WithTaggedFlag("FASTRD_MODE", 13)
                .WithTaggedFlag("FREAD_DUAL", 14)
                .WithTaggedFlag("RESANDRES", 15)
                .WithTag("DATA_POLARITY", 16, 4)
                .WithTaggedFlag("FREAD_QUAD", 20)
                .WithTaggedFlag("WP_REG", 21)
                .WithTaggedFlag("WRSR_2B", 22)
                .WithTaggedFlag("FREAD_DIO", 23)
                .WithTaggedFlag("FREAD_QIO", 24)
                .WithTag("BIT_ORDER", 25, 2)
                .WithReservedBits(27, 5);

            var clock = new DoubleWordRegister(this)
                .WithTag("CLOCK_DIVIDER", 0, 31)
                .WithTaggedFlag("CLK_EQU_SYSCLK", 31);

            var readStatus = new DoubleWordRegister(this);
            if(IsSpiMemory)
            {
                readStatus.WithValueField(0, 16, out readStatusValue, name: "STATUS")
                          .WithValueField(16, 8, name: "WB_MODE")
                          .WithReservedBits(24, 8);
            }
            else
            {
                readStatus.WithValueField(0, 32, out readStatusValue, name: "STATUS");
            }

            var user = new DoubleWordRegister(this, 0x8000_0000)
                .WithTag("CS_TIMING", 0, 12)
                .WithTaggedFlag("FWRITE_DUAL", 12)
                .WithTaggedFlag("FWRITE_QUAD", 13)
                .WithTaggedFlag("FWRITE_DIO", 14)
                .WithTaggedFlag("FWRITE_QIO", 15)
                .WithTag("HOLD_CONTROL", 16, 8)
                .WithTaggedFlag("USR_MISO_HIGHPART", 24)
                .WithTaggedFlag("USR_MOSI_HIGHPART", 25)
                .WithTaggedFlag("USR_DUMMY_IDLE", 26)
                .WithFlag(27, out userMosiPhase, name: "USR_MOSI")
                .WithFlag(28, out userMisoPhase, name: "USR_MISO")
                .WithFlag(29, out userDummyPhase, name: "USR_DUMMY")
                .WithFlag(30, out userAddressPhase, name: "USR_ADDR")
                .WithFlag(31, out userCommandPhase, name: "USR_COMMAND");

            var user1 = new DoubleWordRegister(this, 0x5C00_0007)
                .WithValueField(0, DummyCycleLengthBits, name: "USR_DUMMY_CYCLELEN")
                .WithReservedBits(DummyCycleLengthBits, 26 - DummyCycleLengthBits)
                .WithValueField(26, 6, out addressBitLength, name: "USR_ADDR_BITLEN");

            var user2 = new DoubleWordRegister(this, 0x7000_0000)
                .WithValueField(0, 16, out commandValue, name: "USR_COMMAND_VALUE")
                .WithReservedBits(16, 12)
                .WithValueField(28, 4, out commandBitLength, name: "USR_COMMAND_BITLEN");

            var mosiDLen = new DoubleWordRegister(this)
                .WithValueField(0, DataBitLengthBits, out mosiBitLength, name: "USR_MOSI_DBITLEN")
                .WithReservedBits(DataBitLengthBits, 32 - DataBitLengthBits);

            var misoDLen = new DoubleWordRegister(this)
                .WithValueField(0, DataBitLengthBits, out misoBitLength, name: "USR_MISO_DBITLEN")
                .WithReservedBits(DataBitLengthBits, 32 - DataBitLengthBits);

            var map = new Dictionary<long, DoubleWordRegister>
            {
                {GenerationOffset(Registers.Cmd, RegistersS2.Cmd, RegistersS3.Cmd), command},
                {GenerationOffset(Registers.Address, RegistersS2.Address, RegistersS3.Address), address},
                {GenerationOffset(Registers.Control, RegistersS2.Control, RegistersS3.Control), control},
                {GenerationOffset(Registers.Clock, RegistersS2.Clock, RegistersS3.Clock), clock},
                {GenerationOffset(Registers.RdStatus, RegistersS2.RdStatus, RegistersS3.RdStatus), readStatus},
                {GenerationOffset(Registers.User, RegistersS2.User, RegistersS3.User), user},
                {GenerationOffset(Registers.User1, RegistersS2.User1, RegistersS3.User1), user1},
                {GenerationOffset(Registers.User2, RegistersS2.User2, RegistersS3.User2), user2},
                {GenerationOffset(Registers.MosiDLen, RegistersS2.MosiDLen, RegistersS3.MosiDLen), mosiDLen},
                {GenerationOffset(Registers.MisoDLen, RegistersS2.MisoDLen, RegistersS3.MisoDLen), misoDLen},
            };

            var dataBufferBase = GenerationOffset(Registers.W0, RegistersS2.W0, RegistersS3.W0);
            for(var i = 0; i < DataBufferWords; i++)
            {
                map[dataBufferBase + i * BytesPerWord] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, out dataBuffer[i], name: $"BUF{i}");
            }
            return map;
        }

        private long GenerationOffset(Registers esp32, RegistersS2 esp32s2, RegistersS3 esp32s3)
        {
            switch(generation)
            {
            case ESP32Generation.ESP32:
                return (long)esp32;
            case ESP32Generation.ESP32S2:
                return (long)esp32s2;
            default:
                return (long)esp32s3;
            }
        }

        private void TagRemainingRegisters(Type registers)
        {
            foreach(var register in Enum.GetValues(registers))
            {
                var offset = Convert.ToInt64(register);
                if(!RegistersCollection.HasRegisterAtOffset(offset))
                {
                    RegistersCollection.DefineRegister(offset).WithTag(register.ToString(), 0, 32);
                }
            }
        }

        private void PerformUserTransfer()
        {
            var command = userCommandPhase.Value || (IsSpiMemory && commandBitLength.Value != 0)
                ? new[] { (byte)commandValue.Value }
                : [];

            uint address = 0;
            var addressBytes = 0;
            if(userAddressPhase.Value)
            {
                addressBytes = BitsToBytes(addressBitLength.Value);
                // SPIMEM keeps USR_ADDR LSB-aligned, the ESP32 MSB-aligned and padded with ones.
                address = (uint)addressValue.Value;
                if(!IsSpiMemory && addressBytes > 0 && addressBytes < 4)
                {
                    address >>= 8 * (4 - addressBytes);
                }
            }

            var dummyBytes = userDummyPhase.Value ? 1 : 0;
            var mosi = userMosiPhase.Value ? ReadDataBuffer(BitsToBytes(mosiBitLength.Value)) : new byte[0];
            var misoBytes = userMisoPhase.Value ? BitsToBytes(misoBitLength.Value) : 0;

            var miso = Transmit(new Command(command, address, addressBytes, dummyBytes, mosi).GetBytes(), misoBytes);
            StoreResult(miso);
        }

        private void PerformFlashCommand(byte command, int misoBytes, bool useAddress)
        {
            this.Log(LogLevel.Debug, "Flash command 0x{0:X2} requested from CMD", command);
            var address = useAddress ? (uint)(addressValue.Value & AddressMask) : 0u;
            var miso = Transmit(new Command(new[] { command }, address, useAddress ? AddressBytes : 0, 0, new byte[0]).GetBytes(), misoBytes);
            StoreResult(miso);
        }

        // One transfer per go bit: unlike the byte at a time models, every phase is configured up front.
        private byte[] Transmit(byte[] command, int misoBytes)
        {
            var miso = new byte[misoBytes];
            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "Transfer requested, but no SPI peripheral is attached");
                return miso;
            }

            this.Log(LogLevel.Debug, "Transfer: {0} out, {1} in", Misc.PrettyPrintCollectionHex(command), misoBytes);

            foreach(var b in command)
            {
                RegisteredPeripheral.Transmit(b);
            }
            for(var i = 0; i < miso.Length; i++)
            {
                miso[i] = RegisteredPeripheral.Transmit(DummyByte);
            }
            RegisteredPeripheral.FinishTransmission();
            if(miso.Length > 0)
            {
                this.Log(LogLevel.Debug, "Transfer returned {0}", Misc.PrettyPrintCollectionHex(miso));
            }
            return miso;
        }

        private byte[] ReadDataBuffer(int count)
        {
            var data = new byte[count];
            for(var offset = 0; offset < count; offset += BytesPerWord)
            {
                var width = Math.Min(BytesPerWord, count - offset);
                BitHelper.GetBytesFromValue(data, offset, dataBuffer[offset / BytesPerWord].Value, width, reverse: true);
            }
            return data;
        }

        // The first word also lands in RD_STATUS, where FLASH_RDSR and FLASH_RDID leave their result.
        private void StoreResult(byte[] data)
        {
            if(data.Length == 0)
            {
                return;
            }

            var padded = new byte[DataBufferWords * BytesPerWord];
            Array.Copy(data, padded, Math.Min(data.Length, padded.Length));
            for(var word = 0; word < DataBufferWords; word++)
            {
                dataBuffer[word].Value = BitHelper.ToUInt32(padded, word * BytesPerWord, BytesPerWord, reverse: true);
            }
            readStatusValue.Value = BitHelper.ToUInt32(padded, 0, StatusBytes, reverse: true);
        }

        private bool IsSpiMemory => generation != ESP32Generation.ESP32;

        private int DummyCycleLengthBits => generation == ESP32Generation.ESP32S3 ? 6 : 8;

        private int DataBitLengthBits => generation switch
        {
            ESP32Generation.ESP32 => 24,
            ESP32Generation.ESP32S2 => 11,
            _ => 10,
        };

        // width of RD_STATUS.STATUS
        private int StatusBytes => IsSpiMemory ? 2 : 4;

        private IFlagRegisterField userCommandPhase;
        private IFlagRegisterField userAddressPhase;
        private IFlagRegisterField userDummyPhase;
        private IFlagRegisterField userMosiPhase;
        private IFlagRegisterField userMisoPhase;
        private IValueRegisterField addressValue;
        private IValueRegisterField readStatusValue;
        private IValueRegisterField commandValue;
        private IValueRegisterField commandBitLength;
        private IValueRegisterField addressBitLength;
        private IValueRegisterField mosiBitLength;
        private IValueRegisterField misoBitLength;

        private readonly ESP32Generation generation;
        private readonly Type registersType;
        private readonly IValueRegisterField[] dataBuffer;
        private readonly RegisterMapper registerMapper;

        private const int BytesPerWord = 4;
        private const byte DummyByte = 0;
        private const int DataBufferWords = 16;
        private const int AddressBytes = 3;
        private const int JedecIdBytes = 3;
        private const uint AddressMask = 0xFFFFFF;

        private const byte ReadCommand = 0x03;
        private const byte ReadStatusCommand = 0x05;
        private const byte ReadIdCommand = 0x9F;

        private class Command
        {
            public Command(byte[] command, uint address, int addressBytes, int dummyBytes, byte[] mosi)
            {
                bytes = new List<byte>();
                bytes.AddRange(command);
                bytes.AddRange(BitHelper.GetBytesFromValue(address, addressBytes));
                bytes.AddRange(Enumerable.Repeat(DummyByte, dummyBytes));
                bytes.AddRange(mosi);
            }

            public byte[] GetBytes()
            {
                return bytes.ToArray();
            }

            private readonly List<byte> bytes;
        }

        private enum Registers : long
        {
            Cmd                 = 0x000, // SPI_CMD
            Address             = 0x004, // SPI_ADDR
            Control             = 0x008, // SPI_CTRL
            Ctrl1               = 0x00C, // SPI_CTRL1
            RdStatus            = 0x010, // SPI_RD_STATUS
            Ctrl2               = 0x014, // SPI_CTRL2
            Clock               = 0x018, // SPI_CLOCK
            User                = 0x01C, // SPI_USER
            User1               = 0x020, // SPI_USER1
            User2               = 0x024, // SPI_USER2
            MosiDLen            = 0x028, // SPI_MOSI_DLEN
            MisoDLen            = 0x02C, // SPI_MISO_DLEN
            SlvWrStatus         = 0x030, // SPI_SLV_WR_STATUS
            Pin                 = 0x034, // SPI_PIN
            Slave               = 0x038, // SPI_SLAVE
            Slave1              = 0x03C, // SPI_SLAVE1
            Slave2              = 0x040, // SPI_SLAVE2
            Slave3              = 0x044, // SPI_SLAVE3
            SlvWrbufDLen        = 0x048, // SPI_SLV_WRBUF_DLEN
            SlvRdbufDLen        = 0x04C, // SPI_SLV_RDBUF_DLEN
            CacheFctrl          = 0x050, // SPI_CACHE_FCTRL
            CacheSctrl          = 0x054, // SPI_CACHE_SCTRL
            SramCmd             = 0x058, // SPI_SRAM_CMD
            SramDrdCmd          = 0x05C, // SPI_SRAM_DRD_CMD
            SramDwrCmd          = 0x060, // SPI_SRAM_DWR_CMD
            SlvRdBit            = 0x064, // SPI_SLV_RD_BIT
            W0                  = 0x080, // SPI_W0
            W1                  = 0x084, // SPI_W1
            W2                  = 0x088, // SPI_W2
            W3                  = 0x08C, // SPI_W3
            W4                  = 0x090, // SPI_W4
            W5                  = 0x094, // SPI_W5
            W6                  = 0x098, // SPI_W6
            W7                  = 0x09C, // SPI_W7
            W8                  = 0x0A0, // SPI_W8
            W9                  = 0x0A4, // SPI_W9
            W10                 = 0x0A8, // SPI_W10
            W11                 = 0x0AC, // SPI_W11
            W12                 = 0x0B0, // SPI_W12
            W13                 = 0x0B4, // SPI_W13
            W14                 = 0x0B8, // SPI_W14
            W15                 = 0x0BC, // SPI_W15
            TxCrc               = 0x0C0, // SPI_TX_CRC
            Ext0                = 0x0F0, // SPI_EXT0
            Ext1                = 0x0F4, // SPI_EXT1
            Ext2                = 0x0F8, // SPI_EXT2
            Ext3                = 0x0FC, // SPI_EXT3
            DmaConfig           = 0x100, // SPI_DMA_CONF
            DmaOutLink          = 0x104, // SPI_DMA_OUT_LINK
            DmaInLink           = 0x108, // SPI_DMA_IN_LINK
            DmaStatus           = 0x10C, // SPI_DMA_STATUS
            DmaIntEnable        = 0x110, // SPI_DMA_INT_ENA
            DmaIntRaw           = 0x114, // SPI_DMA_INT_RAW
            DmaIntStatus        = 0x118, // SPI_DMA_INT_ST
            DmaIntClear         = 0x11C, // SPI_DMA_INT_CLR
            InErrEofDesAddress  = 0x120, // SPI_IN_ERR_EOF_DES_ADDR
            InSucEofDesAddress  = 0x124, // SPI_IN_SUC_EOF_DES_ADDR
            InlinkDscr          = 0x128, // SPI_INLINK_DSCR
            InlinkDscrBf0       = 0x12C, // SPI_INLINK_DSCR_BF0
            InlinkDscrBf1       = 0x130, // SPI_INLINK_DSCR_BF1
            OutEofBfrDesAddress = 0x134, // SPI_OUT_EOF_BFR_DES_ADDR
            OutEofDesAddress    = 0x138, // SPI_OUT_EOF_DES_ADDR
            OutlinkDscr         = 0x13C, // SPI_OUTLINK_DSCR
            OutlinkDscrBf0      = 0x140, // SPI_OUTLINK_DSCR_BF0
            OutlinkDscrBf1      = 0x144, // SPI_OUTLINK_DSCR_BF1
            DmaRstatus          = 0x148, // SPI_DMA_RSTATUS
            DmaTstatus          = 0x14C, // SPI_DMA_TSTATUS
            Date                = 0x3FC, // SPI_DATE
        }

        private enum RegistersS2 : long
        {
            Cmd               = 0x000, // SPI_MEM_CMD
            Address           = 0x004, // SPI_MEM_ADDR
            Control           = 0x008, // SPI_MEM_CTRL
            Ctrl1             = 0x00C, // SPI_MEM_CTRL1
            Ctrl2             = 0x010, // SPI_MEM_CTRL2
            Clock             = 0x014, // SPI_MEM_CLOCK
            User              = 0x018, // SPI_MEM_USER
            User1             = 0x01C, // SPI_MEM_USER1
            User2             = 0x020, // SPI_MEM_USER2
            MosiDLen          = 0x024, // SPI_MEM_MOSI_DLEN
            MisoDLen          = 0x028, // SPI_MEM_MISO_DLEN
            RdStatus          = 0x02C, // SPI_MEM_RD_STATUS
            ExtAddress        = 0x030, // SPI_MEM_EXT_ADDR
            Misc              = 0x034, // SPI_MEM_MISC
            TxCrc             = 0x038, // SPI_MEM_TX_CRC
            CacheFctrl        = 0x03C, // SPI_MEM_CACHE_FCTRL
            CacheSctrl        = 0x040, // SPI_MEM_CACHE_SCTRL
            SramCmd           = 0x044, // SPI_MEM_SRAM_CMD
            SramDrdCmd        = 0x048, // SPI_MEM_SRAM_DRD_CMD
            SramDwrCmd        = 0x04C, // SPI_MEM_SRAM_DWR_CMD
            SramClk           = 0x050, // SPI_MEM_SRAM_CLK
            Fsm               = 0x054, // SPI_MEM_FSM
            W0                = 0x058, // SPI_MEM_W0
            W1                = 0x05C, // SPI_MEM_W1
            W2                = 0x060, // SPI_MEM_W2
            W3                = 0x064, // SPI_MEM_W3
            W4                = 0x068, // SPI_MEM_W4
            W5                = 0x06C, // SPI_MEM_W5
            W6                = 0x070, // SPI_MEM_W6
            W7                = 0x074, // SPI_MEM_W7
            W8                = 0x078, // SPI_MEM_W8
            W9                = 0x07C, // SPI_MEM_W9
            W10               = 0x080, // SPI_MEM_W10
            W11               = 0x084, // SPI_MEM_W11
            W12               = 0x088, // SPI_MEM_W12
            W13               = 0x08C, // SPI_MEM_W13
            W14               = 0x090, // SPI_MEM_W14
            W15               = 0x094, // SPI_MEM_W15
            FlashWaitiControl = 0x098, // SPI_MEM_FLASH_WAITI_CTRL
            FlashSusCmd       = 0x09C, // SPI_MEM_FLASH_SUS_CMD
            FlashSusControl   = 0x0A0, // SPI_MEM_FLASH_SUS_CTRL
            SusStatus         = 0x0A4, // SPI_MEM_SUS_STATUS
            TimingCali        = 0x0A8, // SPI_MEM_TIMING_CALI
            DinMode           = 0x0AC, // SPI_MEM_DIN_MODE
            DinNum            = 0x0B0, // SPI_MEM_DIN_NUM
            DoutMode          = 0x0B4, // SPI_MEM_DOUT_MODE
            DoutNum           = 0x0B8, // SPI_MEM_DOUT_NUM
            SmemTimingCali    = 0x0BC, // SPI_MEM_SPI_SMEM_TIMING_CALI
            SmemDinMode       = 0x0C0, // SPI_MEM_SPI_SMEM_DIN_MODE
            SmemDinNum        = 0x0C4, // SPI_MEM_SPI_SMEM_DIN_NUM
            SmemDoutMode      = 0x0C8, // SPI_MEM_SPI_SMEM_DOUT_MODE
            SmemDoutNum       = 0x0CC, // SPI_MEM_SPI_SMEM_DOUT_NUM
            SmemAc            = 0x0D0, // SPI_MEM_SPI_SMEM_AC
            Ddr               = 0x0D4, // SPI_MEM_DDR
            SmemDdr           = 0x0D8, // SPI_MEM_SPI_SMEM_DDR
            ClockGate         = 0x0DC, // SPI_MEM_CLOCK_GATE
            Date              = 0x3FC, // SPI_MEM_DATE
        }

        private enum RegistersS3 : long
        {
            Cmd               = 0x000, // SPI_MEM_CMD
            Address           = 0x004, // SPI_MEM_ADDR
            Control           = 0x008, // SPI_MEM_CTRL
            Ctrl1             = 0x00C, // SPI_MEM_CTRL1
            Ctrl2             = 0x010, // SPI_MEM_CTRL2
            Clock             = 0x014, // SPI_MEM_CLOCK
            User              = 0x018, // SPI_MEM_USER
            User1             = 0x01C, // SPI_MEM_USER1
            User2             = 0x020, // SPI_MEM_USER2
            MosiDLen          = 0x024, // SPI_MEM_MOSI_DLEN
            MisoDLen          = 0x028, // SPI_MEM_MISO_DLEN
            RdStatus          = 0x02C, // SPI_MEM_RD_STATUS
            ExtAddress        = 0x030, // SPI_MEM_EXT_ADDR
            Misc              = 0x034, // SPI_MEM_MISC
            TxCrc             = 0x038, // SPI_MEM_TX_CRC
            CacheFctrl        = 0x03C, // SPI_MEM_CACHE_FCTRL
            CacheSctrl        = 0x040, // SPI_MEM_CACHE_SCTRL
            SramCmd           = 0x044, // SPI_MEM_SRAM_CMD
            SramDrdCmd        = 0x048, // SPI_MEM_SRAM_DRD_CMD
            SramDwrCmd        = 0x04C, // SPI_MEM_SRAM_DWR_CMD
            SramClk           = 0x050, // SPI_MEM_SRAM_CLK
            Fsm               = 0x054, // SPI_MEM_FSM
            W0                = 0x058, // SPI_MEM_W0
            W1                = 0x05C, // SPI_MEM_W1
            W2                = 0x060, // SPI_MEM_W2
            W3                = 0x064, // SPI_MEM_W3
            W4                = 0x068, // SPI_MEM_W4
            W5                = 0x06C, // SPI_MEM_W5
            W6                = 0x070, // SPI_MEM_W6
            W7                = 0x074, // SPI_MEM_W7
            W8                = 0x078, // SPI_MEM_W8
            W9                = 0x07C, // SPI_MEM_W9
            W10               = 0x080, // SPI_MEM_W10
            W11               = 0x084, // SPI_MEM_W11
            W12               = 0x088, // SPI_MEM_W12
            W13               = 0x08C, // SPI_MEM_W13
            W14               = 0x090, // SPI_MEM_W14
            W15               = 0x094, // SPI_MEM_W15
            FlashWaitiControl = 0x098, // SPI_MEM_FLASH_WAITI_CTRL
            FlashSusCmd       = 0x09C, // SPI_MEM_FLASH_SUS_CMD
            FlashSusControl   = 0x0A0, // SPI_MEM_FLASH_SUS_CTRL
            SusStatus         = 0x0A4, // SPI_MEM_SUS_STATUS
            TimingCali        = 0x0A8, // SPI_MEM_TIMING_CALI
            DinMode           = 0x0AC, // SPI_MEM_DIN_MODE
            DinNum            = 0x0B0, // SPI_MEM_DIN_NUM
            DoutMode          = 0x0B4, // SPI_MEM_DOUT_MODE
            SmemTimingCali    = 0x0BC, // SPI_MEM_SPI_SMEM_TIMING_CALI
            SmemDinMode       = 0x0C0, // SPI_MEM_SPI_SMEM_DIN_MODE
            SmemDinNum        = 0x0C4, // SPI_MEM_SPI_SMEM_DIN_NUM
            SmemDoutMode      = 0x0C8, // SPI_MEM_SPI_SMEM_DOUT_MODE
            EccControl        = 0x0CC, // SPI_MEM_ECC_CTRL
            EccErrorAddress   = 0x0D0, // SPI_MEM_ECC_ERR_ADDR
            EccErrorBit       = 0x0D4, // SPI_MEM_ECC_ERR_BIT
            SmemAc            = 0x0DC, // SPI_MEM_SPI_SMEM_AC
            Ddr               = 0x0E0, // SPI_MEM_DDR
            SmemDdr           = 0x0E4, // SPI_MEM_SPI_SMEM_DDR
            ClockGate         = 0x0E8, // SPI_MEM_CLOCK_GATE
            CoreClockSelect   = 0x0EC, // SPI_MEM_CORE_CLK_SEL
            IntEnable         = 0x0F0, // SPI_MEM_INT_ENA
            IntClear          = 0x0F4, // SPI_MEM_INT_CLR
            IntRaw            = 0x0F8, // SPI_MEM_INT_RAW
            IntStatus         = 0x0FC, // SPI_MEM_INT_ST
            Date              = 0x3FC, // SPI_MEM_DATE
        }
    }
}
