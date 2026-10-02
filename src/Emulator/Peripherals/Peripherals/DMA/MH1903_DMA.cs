using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class MH1903_DMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_DMA(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x400;

        public GPIO IRQ { get; set; } = new GPIO();

        private void DefineRegisters()
        {
            // DMA Channels 0-7 (each channel is 0x58 bytes = 22 DWORDs)
            for(int ch = 0; ch < 8; ch++)
            {
                var baseOffset = (Registers)((long)Registers.Channel0SourceAddressLow + (ch * 0x58));

                // Source Address Register Low/High
                ((Registers)((long)baseOffset + 0x00)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceAddressLow");
                ((Registers)((long)baseOffset + 0x04)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceAddressHigh");

                // Destination Address Register Low/High
                ((Registers)((long)baseOffset + 0x08)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationAddressLow");
                ((Registers)((long)baseOffset + 0x0C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationAddressHigh");

                // Linked List Pointer Low/High
                ((Registers)((long)baseOffset + 0x10)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}LinkedListPointerLow");
                ((Registers)((long)baseOffset + 0x14)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}LinkedListPointerHigh");

                // Control Register Low/High
                ((Registers)((long)baseOffset + 0x18)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}ControlLow");
                ((Registers)((long)baseOffset + 0x1C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}ControlHigh");

                // Source Status Low/High
                ((Registers)((long)baseOffset + 0x20)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceStatusLow");
                ((Registers)((long)baseOffset + 0x24)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceStatusHigh");

                // Destination Status Low/High
                ((Registers)((long)baseOffset + 0x28)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationStatusLow");
                ((Registers)((long)baseOffset + 0x2C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationStatusHigh");

                // Source Status Address Low/High
                ((Registers)((long)baseOffset + 0x30)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceStatusAddressLow");
                ((Registers)((long)baseOffset + 0x34)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceStatusAddressHigh");

                // Destination Status Address Low/High
                ((Registers)((long)baseOffset + 0x38)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationStatusAddressLow");
                ((Registers)((long)baseOffset + 0x3C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationStatusAddressHigh");

                // Configuration Register Low/High
                ((Registers)((long)baseOffset + 0x40)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}ConfigurationLow");
                ((Registers)((long)baseOffset + 0x44)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}ConfigurationHigh");

                // Source Gather Register Low/High
                ((Registers)((long)baseOffset + 0x48)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceGatherLow");
                ((Registers)((long)baseOffset + 0x4C)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}SourceGatherHigh");

                // Destination Scatter Register Low/High
                ((Registers)((long)baseOffset + 0x50)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationScatterLow");
                ((Registers)((long)baseOffset + 0x54)).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"Channel{ch}DestinationScatterHigh");
            }

            // Interrupt Registers - Raw Status
            Registers.RawTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawTransferLow");
            Registers.RawTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawTransferHigh");
            Registers.RawBlockLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawBlockLow");
            Registers.RawBlockHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawBlockHigh");
            Registers.RawSourceTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawSourceTransferLow");
            Registers.RawSourceTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawSourceTransferHigh");
            Registers.RawDestinationTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawDestinationTransferLow");
            Registers.RawDestinationTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawDestinationTransferHigh");
            Registers.RawErrorLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawErrorLow");
            Registers.RawErrorHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RawErrorHigh");

            // Interrupt Registers - Status
            Registers.StatusTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusTransferLow");
            Registers.StatusTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusTransferHigh");
            Registers.StatusBlockLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusBlockLow");
            Registers.StatusBlockHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusBlockHigh");
            Registers.StatusSourceTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusSourceTransferLow");
            Registers.StatusSourceTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusSourceTransferHigh");
            Registers.StatusDestinationTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusDestinationTransferLow");
            Registers.StatusDestinationTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusDestinationTransferHigh");
            Registers.StatusErrorLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusErrorLow");
            Registers.StatusErrorHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusErrorHigh");

            // Interrupt Registers - Mask
            Registers.MaskTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskTransferLow");
            Registers.MaskTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskTransferHigh");
            Registers.MaskBlockLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskBlockLow");
            Registers.MaskBlockHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskBlockHigh");
            Registers.MaskSourceTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskSourceTransferLow");
            Registers.MaskSourceTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskSourceTransferHigh");
            Registers.MaskDestinationTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskDestinationTransferLow");
            Registers.MaskDestinationTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskDestinationTransferHigh");
            Registers.MaskErrorLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskErrorLow");
            Registers.MaskErrorHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MaskErrorHigh");

            // Interrupt Registers - Clear
            Registers.ClearTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearTransferLow");
            Registers.ClearTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearTransferHigh");
            Registers.ClearBlockLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearBlockLow");
            Registers.ClearBlockHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearBlockHigh");
            Registers.ClearSourceTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearSourceTransferLow");
            Registers.ClearSourceTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearSourceTransferHigh");
            Registers.ClearDestinationTransferLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearDestinationTransferLow");
            Registers.ClearDestinationTransferHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearDestinationTransferHigh");
            Registers.ClearErrorLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearErrorLow");
            Registers.ClearErrorHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ClearErrorHigh");

            // Combined Interrupt Status
            Registers.StatusInterruptLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusInterruptLow");
            Registers.StatusInterruptHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "StatusInterruptHigh");

            // Software handshaking
            Registers.RequestSourceRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RequestSourceRegisterLow");
            Registers.RequestSourceRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RequestSourceRegisterHigh");
            Registers.RequestDestinationRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RequestDestinationRegisterLow");
            Registers.RequestDestinationRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RequestDestinationRegisterHigh");
            Registers.SingleRequestSourceRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SingleRequestSourceRegisterLow");
            Registers.SingleRequestSourceRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SingleRequestSourceRegisterHigh");
            Registers.SingleRequestDestinationRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SingleRequestDestinationRegisterLow");
            Registers.SingleRequestDestinationRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SingleRequestDestinationRegisterHigh");
            Registers.LastSourceRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "LastSourceRegisterLow");
            Registers.LastSourceRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "LastSourceRegisterHigh");
            Registers.LastDestinationRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "LastDestinationRegisterLow");
            Registers.LastDestinationRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "LastDestinationRegisterHigh");

            // DMA Configuration registers
            Registers.ConfigurationRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ConfigurationRegisterLow");
            Registers.ConfigurationRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ConfigurationRegisterHigh");
            Registers.ChannelEnableRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ChannelEnableRegisterLow");
            Registers.ChannelEnableRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ChannelEnableRegisterHigh");
            Registers.DmaIdRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "DmaIdRegisterLow");
            Registers.DmaIdRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "DmaIdRegisterHigh");
            Registers.DmaTestRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "DmaTestRegisterLow");
            Registers.DmaTestRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "DmaTestRegisterHigh");

            // Reserved 0x3B8-0x3BF
            Registers.Reserved3B8.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved3BC.Define(this)
                .WithReservedBits(0, 32);

            // Component parameter registers (read-only)
            Registers.ComponentParameters6Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters6Low");
            Registers.ComponentParameters6High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters6High");
            Registers.ComponentParameters5Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters5Low");
            Registers.ComponentParameters5High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters5High");
            Registers.ComponentParameters4Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters4Low");
            Registers.ComponentParameters4High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters4High");
            Registers.ComponentParameters3Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters3Low");
            Registers.ComponentParameters3High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters3High");
            Registers.ComponentParameters2Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters2Low");
            Registers.ComponentParameters2High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters2High");
            Registers.ComponentParameters1Low.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters1Low");
            Registers.ComponentParameters1High.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentParameters1High");
            Registers.ComponentIdRegisterLow.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentIdRegisterLow");
            Registers.ComponentIdRegisterHigh.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ComponentIdRegisterHigh");
        }

        private enum Registers : long
        {
            // Channel 0 registers (0x000 - 0x057)
            Channel0SourceAddressLow = 0x000,
            Channel0SourceAddressHigh = 0x004,
            Channel0DestinationAddressLow = 0x008,
            Channel0DestinationAddressHigh = 0x00C,
            Channel0LinkedListPointerLow = 0x010,
            Channel0LinkedListPointerHigh = 0x014,
            Channel0ControlLow = 0x018,
            Channel0ControlHigh = 0x01C,
            Channel0SourceStatusLow = 0x020,
            Channel0SourceStatusHigh = 0x024,
            Channel0DestinationStatusLow = 0x028,
            Channel0DestinationStatusHigh = 0x02C,
            Channel0SourceStatusAddressLow = 0x030,
            Channel0SourceStatusAddressHigh = 0x034,
            Channel0DestinationStatusAddressLow = 0x038,
            Channel0DestinationStatusAddressHigh = 0x03C,
            Channel0ConfigurationLow = 0x040,
            Channel0ConfigurationHigh = 0x044,
            Channel0SourceGatherLow = 0x048,
            Channel0SourceGatherHigh = 0x04C,
            Channel0DestinationScatterLow = 0x050,
            Channel0DestinationScatterHigh = 0x054,

            // Channels 1-7 follow same pattern at offsets 0x58, 0xB0, 0x108, 0x160, 0x1B8, 0x210, 0x268

            // Interrupt raw status registers (0x2C0)
            RawTransferLow = 0x2C0,
            RawTransferHigh = 0x2C4,
            RawBlockLow = 0x2C8,
            RawBlockHigh = 0x2CC,
            RawSourceTransferLow = 0x2D0,
            RawSourceTransferHigh = 0x2D4,
            RawDestinationTransferLow = 0x2D8,
            RawDestinationTransferHigh = 0x2DC,
            RawErrorLow = 0x2E0,
            RawErrorHigh = 0x2E4,

            // Interrupt status registers (0x2E8)
            StatusTransferLow = 0x2E8,
            StatusTransferHigh = 0x2EC,
            StatusBlockLow = 0x2F0,
            StatusBlockHigh = 0x2F4,
            StatusSourceTransferLow = 0x2F8,
            StatusSourceTransferHigh = 0x2FC,
            StatusDestinationTransferLow = 0x300,
            StatusDestinationTransferHigh = 0x304,
            StatusErrorLow = 0x308,
            StatusErrorHigh = 0x30C,

            // Interrupt mask registers (0x310)
            MaskTransferLow = 0x310,
            MaskTransferHigh = 0x314,
            MaskBlockLow = 0x318,
            MaskBlockHigh = 0x31C,
            MaskSourceTransferLow = 0x320,
            MaskSourceTransferHigh = 0x324,
            MaskDestinationTransferLow = 0x328,
            MaskDestinationTransferHigh = 0x32C,
            MaskErrorLow = 0x330,
            MaskErrorHigh = 0x334,

            // Interrupt clear registers (0x338)
            ClearTransferLow = 0x338,
            ClearTransferHigh = 0x33C,
            ClearBlockLow = 0x340,
            ClearBlockHigh = 0x344,
            ClearSourceTransferLow = 0x348,
            ClearSourceTransferHigh = 0x34C,
            ClearDestinationTransferLow = 0x350,
            ClearDestinationTransferHigh = 0x354,
            ClearErrorLow = 0x358,
            ClearErrorHigh = 0x35C,

            // Combined interrupt status (0x360)
            StatusInterruptLow = 0x360,
            StatusInterruptHigh = 0x364,

            // Software handshaking (0x368)
            RequestSourceRegisterLow = 0x368,
            RequestSourceRegisterHigh = 0x36C,
            RequestDestinationRegisterLow = 0x370,
            RequestDestinationRegisterHigh = 0x374,
            SingleRequestSourceRegisterLow = 0x378,
            SingleRequestSourceRegisterHigh = 0x37C,
            SingleRequestDestinationRegisterLow = 0x380,
            SingleRequestDestinationRegisterHigh = 0x384,
            LastSourceRegisterLow = 0x388,
            LastSourceRegisterHigh = 0x38C,
            LastDestinationRegisterLow = 0x390,
            LastDestinationRegisterHigh = 0x394,

            // DMA Configuration (0x398)
            ConfigurationRegisterLow = 0x398,
            ConfigurationRegisterHigh = 0x39C,
            ChannelEnableRegisterLow = 0x3A0,
            ChannelEnableRegisterHigh = 0x3A4,
            DmaIdRegisterLow = 0x3A8,
            DmaIdRegisterHigh = 0x3AC,
            DmaTestRegisterLow = 0x3B0,
            DmaTestRegisterHigh = 0x3B4,

            // Reserved (0x3B8)
            Reserved3B8 = 0x3B8,
            Reserved3BC = 0x3BC,

            // Component parameters (0x3C0)
            ComponentParameters6Low = 0x3C0,
            ComponentParameters6High = 0x3C4,
            ComponentParameters5Low = 0x3C8,
            ComponentParameters5High = 0x3CC,
            ComponentParameters4Low = 0x3D0,
            ComponentParameters4High = 0x3D4,
            ComponentParameters3Low = 0x3D8,
            ComponentParameters3High = 0x3DC,
            ComponentParameters2Low = 0x3E0,
            ComponentParameters2High = 0x3E4,
            ComponentParameters1Low = 0x3E8,
            ComponentParameters1High = 0x3EC,
            ComponentIdRegisterLow = 0x3F0,
            ComponentIdRegisterHigh = 0x3F4,
        }
    }
}
