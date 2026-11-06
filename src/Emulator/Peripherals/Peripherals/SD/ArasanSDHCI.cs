//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SD
{
    // Models the Arasan SD3.0/SDIO3.0 Host Controller as found on Xilinx/AMD ZynqMP
    // (SD0/SD1 @ 0xff160000/0xff170000). Register layout follows the SD Host Controller
    // Standard Specification (SDHCI) verbatim - Arasan/ZynqMP add no MMIO-visible quirks
    // for the paths exercised here (ZynqMP tap-delay tuning goes through PMU firmware SMC
    // calls, not memory-mapped registers). Only SDMA is advertised/implemented; ADMA and
    // UHS/1.8V signaling are left unadvertised so Linux/U-Boot stay on the legacy/high-speed,
    // single-transfer-per-command path this model actually supports.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class ArasanSDHCI : NullRegistrationPointPeripheralContainer<SDCard>,
        IDoubleWordPeripheral, IWordPeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>,
        IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public ArasanSDHCI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            sysbus = machine.GetSystemBus(this);
            irqManager = new InterruptManager<Interrupts>(this);
            RegistersCollection = new DoubleWordRegisterCollection(this);
            ByteRegistersCollection = new ByteRegisterCollection(this);
            InitializeRegisters();
        }

        // TransferModeCommand (0x0C-0x0F) is special-cased through ByteRegistersCollection - see
        // the comment on that register's definition below for why. Everything else stays on the
        // plain DoubleWordRegisterCollection path, same as before.
        public uint ReadDoubleWord(long offset)
        {
            if(ByteRegistersCollection.TryRead(offset, out var b0))
            {
                var b1 = ByteRegistersCollection.Read(offset + 1);
                var b2 = ByteRegistersCollection.Read(offset + 2);
                var b3 = ByteRegistersCollection.Read(offset + 3);
                return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
            }
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(ByteRegistersCollection.TryWrite(offset, (byte)value))
            {
                ByteRegistersCollection.Write(offset + 1, (byte)(value >> 8));
                ByteRegistersCollection.Write(offset + 2, (byte)(value >> 16));
                ByteRegistersCollection.Write(offset + 3, (byte)(value >> 24));
                return;
            }
            RegistersCollection.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(ByteRegistersCollection.TryRead(offset, out var low))
            {
                var high = ByteRegistersCollection.Read(offset + 1);
                return (ushort)(low | (high << 8));
            }
            return this.ReadWordUsingDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(ByteRegistersCollection.TryWrite(offset, (byte)value))
            {
                ByteRegistersCollection.Write(offset + 1, (byte)(value >> 8));
                return;
            }
            this.WriteWordUsingDoubleWord(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            ByteRegistersCollection.Reset();
            irqManager.Reset();
            RegisteredPeripheral?.Reset();
        }

        [IrqProvider]
        public GPIO IRQ { get; private set; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public ByteRegisterCollection ByteRegistersCollection { get; }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => RegistersCollection;

        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => ByteRegistersCollection;

        public long Size => 0x1000;

        private void InitializeRegisters()
        {
            Registers.SdmaAddress.Define32(this)
                .WithValueField(0, 32, out sdmaAddress, name: "SDMA System Address / Argument2");

            Registers.BlockSizeCount.Define32(this)
                .WithValueField(0, 12, out transferBlockSize, name: "BLOCK_SIZE")
                .WithReservedBits(12, 4)
                .WithValueField(16, 16, out transferBlockCount, name: "BLOCK_COUNT");

            Registers.Argument.Define32(this)
                .WithValueField(0, 32, out commandArgument, name: "ARGUMENT");

            // TRANSFER_MODE (low 16 bits) and COMMAND (high 16 bits) share one dword per the SDHCI
            // spec, but real hardware - and Linux's generic sdhci.c - always write them as two
            // separate 16-bit accesses, Transfer Mode first, Command second. Modeling this as a
            // single DoubleWordRegister with one WithWriteCallback (as before) meant the
            // Transfer-Mode-only write ALSO fired the callback, via AllowedTranslations upconverting
            // it into a full-dword read-modify-write - except CMD_INDEX in that merged value was
            // still whatever the *previous* command left behind, since the real Command half hadn't
            // been written yet. That spurious execution then stomped on state (RESPONSE0,
            // TreatNextCommandAsAppCommand) right before the real command ran, which is what broke
            // ACMD41 (SD OCR init) and made every card enumerate as a bare, 0-byte MMC device
            // instead of the real SD card. Splitting into four byte registers - matching
            // MPFS_SDController.cs's CommandTransferMode_SRS03_0..3 pattern for this same SDHCI
            // quirk - means only a write that actually touches the CMD_INDEX byte (byte 3, 0x0F)
            // triggers a command, regardless of which access width the driver uses to get there.
            ByteRegisters.TransferModeCommand0.Define8(this)
                .WithFlag(0, name: "DMAE") // DMA is always used for data commands in this model
                .WithFlag(1, name: "BCE")
                .WithValueField(2, 2, name: "ACMDEN")
                .WithFlag(4, out dataTransferDirectionSelect, name: "DTDS")
                .WithFlag(5, out multiBlockSelect, name: "MSBS")
                .WithReservedBits(6, 2);

            ByteRegisters.TransferModeCommand1.Define8(this)
                .WithReservedBits(0, 8);

            ByteRegisters.TransferModeCommand2.Define8(this)
                .WithValueField(0, 2, out responseType, name: "RESP_TYPE_SELECT")
                .WithReservedBits(2, 1)
                .WithFlag(3, name: "CMD_CRC_CHK_EN")
                .WithFlag(4, name: "CMD_IDX_CHK_EN")
                .WithFlag(5, out dataPresentSelect, name: "DATA_PRESENT_SEL")
                .WithValueField(6, 2, name: "CMD_TYPE");

            ByteRegisters.TransferModeCommand3.Define8(this)
                .WithValueField(0, 6, out commandIndex, name: "CMD_INDEX")
                .WithReservedBits(6, 2)
                .WithWriteCallback((_, __) => ExecuteCommand());

            Registers.Response0.Define32(this)
                .WithValueField(0, 32, out responseFields[0], FieldMode.Read, name: "RESPONSE0");
            Registers.Response1.Define32(this)
                .WithValueField(0, 32, out responseFields[1], FieldMode.Read, name: "RESPONSE1");
            Registers.Response2.Define32(this)
                .WithValueField(0, 32, out responseFields[2], FieldMode.Read, name: "RESPONSE2");
            Registers.Response3.Define32(this)
                .WithValueField(0, 32, out responseFields[3], FieldMode.Read, name: "RESPONSE3");

            Registers.BufferDataPort.Define32(this)
                .WithValueField(0, 32, name: "BUFFER_DATA_PORT"); // PIO transfers are not implemented; SDMA is used instead

            Registers.PresentState.Define32(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "CMD_INHIBIT")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "CMD_INHIBIT_DAT")
                .WithReservedBits(2, 6)
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => false, name: "DAT_ACTIVE_WRITE")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => false, name: "DAT_ACTIVE_READ")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => true, name: "BUFFER_WRITE_ENABLE")
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "BUFFER_READ_ENABLE")
                .WithReservedBits(12, 4)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "CARD_INSERTED")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "CARD_STATE_STABLE")
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "CARD_DETECT_PIN_LEVEL")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => true, name: "WRITE_PROTECT_PIN_LEVEL")
                .WithValueField(20, 4, FieldMode.Read, valueProviderCallback: _ => 0xF, name: "DAT_LINE_LEVEL")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "CMD_LINE_LEVEL")
                .WithReservedBits(25, 7);

            Registers.HostControlPowerBgapWakeup.Define32(this)
                .WithFlag(0, name: "LED_CTRL")
                .WithFlag(1, name: "DATA_XFER_WIDTH_4BIT")
                .WithFlag(2, name: "HIGH_SPEED_EN")
                .WithValueField(3, 2, name: "DMA_SEL")
                .WithFlag(5, name: "EXT_DATA_XFER_WIDTH_8BIT")
                .WithFlag(6, name: "CARD_DETECT_TEST")
                .WithFlag(7, name: "CARD_DETECT_SIGNAL_SEL")
                .WithFlag(8, name: "SD_BUS_POWER")
                .WithValueField(9, 3, name: "SD_BUS_VOLTAGE_SEL")
                .WithFlag(12, name: "HW_RESET_EN")
                .WithReservedBits(13, 3)
                .WithTag("BLOCK_GAP_CONTROL", 16, 8)
                .WithTag("WAKE_UP_CONTROL", 24, 8);

            Registers.ClockTimeoutSoftwareReset.Define32(this)
                .WithFlag(0, out internalClockEnable, name: "INTERNAL_CLOCK_EN")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => internalClockEnable.Value, name: "INTERNAL_CLOCK_STABLE")
                .WithFlag(2, name: "SD_CLOCK_EN")
                .WithReservedBits(3, 3)
                .WithValueField(6, 10, name: "SDCLK_FREQ_SEL")
                .WithTag("DATA_TIMEOUT_COUNTER", 16, 4)
                .WithReservedBits(20, 4)
                .WithFlag(24, out resetAll, FieldMode.Read | FieldMode.Write, writeCallback: (_, val) => { if(val) SoftwareReset(); }, name: "RESET_ALL")
                // Real hardware clears these almost instantly once the CMD/DATA line settles; this
                // model has no line-timing to wait on, so just report them as already-cleared like
                // RESET_ALL. Without this, Linux's sdhci core polls forever ("Reset 0x2/0x4 never
                // completed") since nothing ever clears the bit it just wrote.
                .WithFlag(25, FieldMode.Read | FieldMode.Write, valueProviderCallback: _ => false, name: "RESET_CMD")
                .WithFlag(26, FieldMode.Read | FieldMode.Write, valueProviderCallback: _ => false, name: "RESET_DATA")
                .WithReservedBits(27, 5);

            Registers.NormalErrorIntStatus.Bind(this, irqManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => irqManager.IsSet(irq),
                writeCallback: (irq, prev, curr) => { if(curr) irqManager.ClearInterrupt(irq); }));

            Registers.NormalErrorIntStatusEnable.Bind(this, irqManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => irqManager.IsEnabled(irq),
                writeCallback: (irq, _, curr) =>
                {
                    if(curr)
                    {
                        irqManager.EnableInterrupt(irq);
                    }
                    else
                    {
                        irqManager.DisableInterrupt(irq);
                    }
                }));

            Registers.NormalErrorIntSignalEnable.Define32(this)
                .WithValueField(0, 32, name: "SIGNAL_ENABLE"); // status enable already gates the IRQ line in this model

            Registers.AutoCmdErrorHostControl2.Define32(this)
                .WithTag("AUTO_CMD_ERROR_STATUS", 0, 16)
                .WithReservedBits(16, 16);

            Registers.Capabilities.Define32(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => 4, name: "TIMEOUT_CLK_FREQ")
                .WithReservedBits(6, 1)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "TIMEOUT_CLK_UNIT")
                // Linux's sdhci-of-arasan ignores this and reads the clock framework
                // instead, so this field being 0 was invisible there. But U-Boot's
                // zynq_sdhci.c clk_get_rate() call resolves via a ZynqMP firmware SMC
                // call Renode doesn't implement, silently returning 0 (not an error) -
                // sdhci_setup_cfg() then falls back to this exact field, and 0 here
                // means EINVAL with no card ever probed. 200 (MHz) matches this board's
                // real clock-frequency DT property (0xbebc1f1 Hz = 200 MHz).
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => 200, name: "BASE_CLK_FREQ")
                .WithValueField(16, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "MAX_BLOCK_LEN")
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN_DO_8BIT")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_DO_ADMA2")
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_DO_ADMA1")
                .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN_DO_HISPD")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN_DO_SDMA")
                .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_DO_SUSPEND")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN_VDD_330")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN_VDD_300")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_VDD_180")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_64BIT_V4")
                .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => false, name: "CAN_64BIT")
                .WithReservedBits(29, 3);

            Registers.Capabilities1.Define32(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0, name: "CAPABILITIES1"); // no UHS/HS200/HS400 support advertised

            Registers.MaxCurrent.Define32(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0, name: "MAX_CURRENT");

            Registers.ForceEvent.Define32(this)
                .WithTag("FORCE_EVENT", 0, 32);

            Registers.AdmaErrorStatus.Define32(this)
                .WithTag("ADMA_ERROR_STATUS", 0, 8)
                .WithReservedBits(8, 24);

            Registers.AdmaSystemAddressLow.Define32(this)
                .WithTag("ADMA_SYSTEM_ADDRESS_LOW", 0, 32);

            Registers.AdmaSystemAddressHigh.Define32(this)
                .WithTag("ADMA_SYSTEM_ADDRESS_HIGH", 0, 32);

            Registers.VendorRegister.Define32(this)
                .WithTag("VENDOR_REGISTER", 0, 32);

            Registers.SlotIntStatusHostVersion.Define32(this)
                .WithTag("SLOT_INTERRUPT_STATUS", 0, 8)
                .WithReservedBits(8, 8)
                .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => 0x02, name: "SPEC_VERSION") // SDHCI v3
                .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => 0x00, name: "VENDOR_VERSION");
        }

        private void ExecuteCommand()
        {
            var commandIndex = (uint)this.commandIndex.Value;
            var commandArgument = (uint)this.commandArgument.Value;
            var responseType = (ResponseType)this.responseType.Value;

            var sdCard = RegisteredPeripheral;
            if(sdCard == null)
            {
                this.WarningLog("Command {0} issued but no SD card attached", commandIndex);
                irqManager.SetInterrupt(Interrupts.CommandTimeoutError);
                return;
            }

            var response = sdCard.HandleCommand(commandIndex, commandArgument);

            switch(responseType)
            {
            case ResponseType.Response136:
                PackR2Response(response);
                break;
            case ResponseType.Response48:
            case ResponseType.Response48CheckBusy:
                responseFields[0].Value = response.AsUInt32(0);
                break;
            case ResponseType.NoResponse:
                break;
            }

            irqManager.SetInterrupt(Interrupts.CommandComplete);

            if(dataPresentSelect.Value)
            {
                ProcessDataCommand(sdCard, dataTransferDirectionSelect.Value);
            }
        }

        // The SDHCI spec's Response Register for R2 holds CID/CSD[127:8] (its own trailing
        // CRC7+end-bit dropped), right-shifted by 8 bits across RESPONSE3:RESPONSE2:RESPONSE1:RESPONSE0.
        // Verified against the generic Linux driver's sdhci_read_rsp_136(): resp[i] = (RESPONSEn << 8) |
        // (RESPONSEn-1 >> 24) - the top byte of RESPONSE3 is always shifted out, so its value here is
        // irrelevant to what the driver reconstructs.
        private void PackR2Response(BitStream response)
        {
            var c3 = response.AsUInt32(96);  // CID/CSD[127:96]
            var c2 = response.AsUInt32(64);  // CID/CSD[95:64]
            var c1 = response.AsUInt32(32);  // CID/CSD[63:32]
            var c0 = response.AsUInt32(0);   // CID/CSD[31:0]
            responseFields[3].Value = c3 >> 8;
            responseFields[2].Value = (c3 << 24) | (c2 >> 8);
            responseFields[1].Value = (c2 << 24) | (c1 >> 8);
            responseFields[0].Value = (c1 << 24) | (c0 >> 8);
        }

        // Driven by DTDS (direction) and MSBS (single vs. multi block), not by command number: real
        // hardware doesn't know or care which SD command triggered a data phase, it just moves
        // BlockSize*BlockCount bytes (or just BlockSize, for a single-block transfer) in whichever
        // direction the register says. Keying this off a hardcoded CMD17/18/24/25 whitelist (as
        // before) silently dropped every other data-bearing command - e.g. ACMD51 (SEND_SCR), which
        // has DATA_PRESENT_SEL set but isn't a block read/write - so no DMA interrupt ever fired for
        // it and Linux hung waiting for hardware interrupt during SD card enumeration.
        private void ProcessDataCommand(SDCard sdCard, bool isRead)
        {
            var size = multiBlockSelect.Value ? BlockSize * BlockCount : BlockSize;
            var dest = (ulong)sdmaAddress.Value;

            if(isRead)
            {
                DmaRead(sdCard, dest, size);
            }
            else
            {
                DmaWrite(sdCard, dest, size);
            }

            irqManager.SetInterrupt(Interrupts.DmaInterrupt);
            irqManager.SetInterrupt(Interrupts.TransferComplete);
        }

        private void DmaRead(SDCard sdCard, ulong dest, uint size)
        {
            var data = sdCard.ReadData(size);
            sysbus.WriteBytes(data, dest);
        }

        private void DmaWrite(SDCard sdCard, ulong dest, uint size)
        {
            var buf = new byte[size];
            sysbus.ReadBytes(dest, (int)size, buf, 0);
            sdCard.WriteData(buf);
        }

        private void SoftwareReset()
        {
            irqManager.Reset();
            resetAll.Value = false;
        }

        private uint BlockSize => System.Math.Max(1u, (uint)transferBlockSize.Value);

        private uint BlockCount => System.Math.Max(1u, (uint)transferBlockCount.Value);

        private IValueRegisterField sdmaAddress;
        private IValueRegisterField transferBlockSize;
        private IValueRegisterField transferBlockCount;
        private IValueRegisterField commandArgument;
        private IValueRegisterField responseType;
        private IFlagRegisterField dataPresentSelect;
        private IFlagRegisterField dataTransferDirectionSelect;
        private IFlagRegisterField multiBlockSelect;
        private IValueRegisterField commandIndex;
        private IFlagRegisterField internalClockEnable;
        private IFlagRegisterField resetAll;

        private readonly IBusController sysbus;
        private readonly InterruptManager<Interrupts> irqManager;
        private readonly IValueRegisterField[] responseFields = new IValueRegisterField[4];

        private enum ResponseType
        {
            NoResponse          = 0,
            Response136         = 1, // R2
            Response48          = 2, // R1 / R3 / R4 / R6 / R7
            Response48CheckBusy = 3, // R1b
        }

        private enum Interrupts
        {
            CommandComplete      = 0,
            TransferComplete     = 1,
            BlockGapEvent        = 2,
            DmaInterrupt         = 3,
            BufferWriteReady     = 4,
            BufferReadReady      = 5,
            CardInsertion        = 6,
            CardRemoval          = 7,
            CardInterrupt        = 8,
            // [11:9] Reserved
            Retune               = 12,
            // [13] Reserved
            CommandQueuing       = 14,
            ErrorInterrupt       = 15,
            CommandTimeoutError  = 16,
            CommandCrcError      = 17,
            CommandEndBitError   = 18,
            CommandIndexError    = 19,
            DataTimeoutError     = 20,
            DataCrcError         = 21,
            DataEndBitError      = 22,
            PowerError           = 23,
            AutoCmdError         = 24,
            AdmaError            = 25,
            TuningError          = 26,
            // [31:27] Reserved
        }

        private enum Registers
        {
            SdmaAddress                = 0x00, // SDMA_ADDRESS / ARGUMENT2
            BlockSizeCount             = 0x04, // BLOCK_SIZE / BLOCK_COUNT
            Argument                   = 0x08, // ARGUMENT
            // 0x0C: TRANSFER_MODE / COMMAND - see ByteRegisters below, not defined here
            Response0                  = 0x10, // RESPONSE0
            Response1                  = 0x14, // RESPONSE1
            Response2                  = 0x18, // RESPONSE2
            Response3                  = 0x1C, // RESPONSE3
            BufferDataPort             = 0x20, // BUFFER_DATA_PORT
            PresentState               = 0x24, // PRESENT_STATE
            HostControlPowerBgapWakeup = 0x28, // HOST_CONTROL / POWER_CONTROL / BLOCK_GAP_CONTROL / WAKE_UP_CONTROL
            ClockTimeoutSoftwareReset  = 0x2C, // CLOCK_CONTROL / TIMEOUT_CONTROL / SOFTWARE_RESET
            NormalErrorIntStatus       = 0x30, // INT_STATUS
            NormalErrorIntStatusEnable = 0x34, // INT_ENABLE
            NormalErrorIntSignalEnable = 0x38, // SIGNAL_ENABLE
            AutoCmdErrorHostControl2   = 0x3C, // AUTO_CMD_STATUS / HOST_CONTROL2
            Capabilities               = 0x40, // CAPABILITIES
            Capabilities1              = 0x44, // CAPABILITIES_1
            MaxCurrent                 = 0x48, // MAX_CURRENT
            ForceEvent                 = 0x50, // SET_ACMD12_ERROR / SET_INT_ERROR
            AdmaErrorStatus            = 0x54, // ADMA_ERROR
            AdmaSystemAddressLow       = 0x58, // ADMA_ADDRESS
            AdmaSystemAddressHigh      = 0x5C, // ADMA_ADDRESS_HI
            VendorRegister             = 0x78, // Arasan SDHCI_ARASAN_VENDOR_REGISTER (HS400 enhanced strobe, etc.)
            SlotIntStatusHostVersion   = 0xFC, // SLOT_INT_STATUS / HOST_VERSION
        }

        private enum ByteRegisters
        {
            TransferModeCommand0 = 0x0C, // TRANSFER_MODE[7:0]
            TransferModeCommand1 = 0x0D, // TRANSFER_MODE[15:8] (unused bits, reserved)
            TransferModeCommand2 = 0x0E, // COMMAND[7:0]
            TransferModeCommand3 = 0x0F, // COMMAND[15:8] - CMD_INDEX lives here; writing it executes the command
        }
    }
}
