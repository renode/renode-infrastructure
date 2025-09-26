//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public partial class IMXRT700_eDMA : BasicDoubleWordPeripheral, IWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public IMXRT700_eDMA(IMachine machine, int numberOfChannels) : base(machine)
        {
            if(numberOfChannels < MinimumNumberOfChannels || numberOfChannels > MaximumNumberOfChannels)
            {
                throw new ConstructionException($"The number of channels {numberOfChannels} isn't in the allowed range {MinimumNumberOfChannels}-{MaximumNumberOfChannels}");
            }

            NumberOfChannels = numberOfChannels;
            dmaEngine = new DmaEngine(sysbus);

            var connections = new Dictionary<int, IGPIO>();
            channels = new Channel[NumberOfChannels];
            for(int i = 0; i < numberOfChannels; i++)
            {
                channels[i] = new Channel(this, i);
                connections[i] = channels[i].IRQ;
            }
            Connections = connections;
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var ch in channels)
            {
                ch.Reset();
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            var channelIndex = offset / BaseSize - 1;
            if(channelIndex == BaseModuleChannelIndex)
            {
                return base.ReadDoubleWord(offset);
            }
            if(channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to read double word from the protected offset {0:X}", offset);
                return 0;
            }
            return channels[(int)channelIndex].ReadDoubleWord(offset % BaseSize);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var channelIndex = offset / BaseSize - 1;
            if(channelIndex == BaseModuleChannelIndex)
            {
                base.WriteDoubleWord(offset, value);
                return;
            }
            if(channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to write double word to the protected offset {0:X}", offset);
                return;
            }
            channels[(int)channelIndex].WriteDoubleWord(offset % BaseSize, value);
        }

        public ushort ReadWord(long offset)
        {
            var channelIndex = offset / BaseSize - 1;
            if(channelIndex == BaseModuleChannelIndex || channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to read word from the protected offset {0:X}", offset);
                return 0;
            }
            return channels[(int)channelIndex].ReadWord(offset % BaseSize);
        }

        public void WriteWord(long offset, ushort value)
        {
            var channelIndex = offset / BaseSize - 1;
            if(channelIndex == BaseModuleChannelIndex || channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to write word to the protected offset {0:X}", offset);
                return;
            }
            channels[(int)channelIndex].WriteWord(offset % BaseSize, value);
        }

        public void HandlePeripheralRequest(int slot)
        {
            var channel = channels.FirstOrDefault(x => x.ServiceRequestSource == slot);
            if(channel == null)
            {
                this.WarningLog("No channel configured for peripheral slot {0}", slot);
                return;
            }

            channel.HardwareServiceRequest();
        }

        public long Size => (NumberOfChannels + 1) * BaseSize;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public int NumberOfChannels { get; }

        public bool Halt
        {
            set => halt.Value = value;
        }

        private bool IsTransferAllowed()
        {
            if(enableDebug.Value)
            {
                // eDMA doesn't transfer data
                return false;
            }

            if(halt.Value)
            {
                return false;
            }

            return true;
        }

        private void ReportErrorOnChannel(int channelNumber)
        {
            if(haltAfterError.Value)
            {
                halt.Value = true;
            }
            if(channelNumber >= NumberOfChannels)
            {
                this.WarningLog("Cannot report error on a nonexistent channel {0} - allowed channel numbers are in the range 0-{1}", channelNumber, NumberOfChannels - 1);
                return;
            }
            errorChannelNumber.Value = (ulong)channelNumber;
            // Error indicators are sticky and cannot be cleared.
            // They show the last recorded error until the DMA is reset.
            channelError.Value = channels[channelNumber].Errors;
        }

        private void LinkChannel(int initiatorChannelNumber, int linkedChannelNumber)
        {
            if(linkedChannelNumber < 0 || linkedChannelNumber >= NumberOfChannels)
            {
                this.WarningLog("Unable to link to a nonexistent channel {0} from channel {1}", linkedChannelNumber, initiatorChannelNumber);
                return;
            }
            this.DebugLog("Channel linking from CH{0} to CH{1}", initiatorChannelNumber, linkedChannelNumber);
            channels[linkedChannelNumber].ChannelLinkInternalRequest();
        }

        private bool IsPeripheralSlotOccupied(int askingChannelNumber, int slot, out int occupiedChannelNumber)
        {
            occupiedChannelNumber = -1;
            // SRC in CHn_MUX must be unique across channels (with the exception of SRC=0)
            var channel = channels.FirstOrDefault(x => x.ChannelNumber != askingChannelNumber && x.ServiceRequestSource == slot);
            if(channel != null)
            {
                occupiedChannelNumber = channel.ChannelNumber;
            }
            return channel != null;
        }

        private void DefineRegisters()
        {
            // Channel preemption and arbitration configuration is ignored, because all DMA transfers finish immediately during emulation.
            Registers.ManagementPageControl.Define(this, 0x00310000, name: "MP_CSR")
                .WithReservedBits(0, 1)
                .WithFlag(1, out enableDebug, name: "EDBG")
                .WithFlag(2, name: "ERCA") // No impact, channel arbitration doesn't matter during emulation.
                .WithReservedBits(3, 1)
                .WithFlag(4, out haltAfterError, name: "HAE")
                .WithFlag(5, out halt, name: "HALT")
                .WithFlag(6, out globalChannelLinkingControl, name: "GCLC")
                .WithTaggedFlag("GMRC", 7)
                .WithTaggedFlag("ECX", 8) // Minor loops are atomic during emulation, so cancellation is immediate. 
                .WithTaggedFlag("CX", 9) // Same as above.
                .WithReservedBits(10, 6)
                .WithReservedBits(16, 8)
                .WithTag("ACTIVE_ID", 24, 4) // Software never observes ACTIVE bit 1 during emulation. 
                .WithReservedBits(28, 3)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ =>
                {
                    // Transfers are immediate so eDMA is always idle from the software perspective.
                    return false;
                }, name: "ACTIVE");

            // During emulation channel errors can occur only due to an illegal setting in the transfer control descriptor.
            // Bus and memory errors are not possible.
            // Error flags mirror flags in the corresponding channel specified by ERRCHN.
            Registers.ManagementPageErrorStatus.Define(this, name: "MP_ES")
                .WithEnumField<DoubleWordRegister, ChannelError>(0, 8, out channelError, FieldMode.Read, name: "DBE|SBE|SGE|NCE|DOE|DAE|SOE|SAE")
                .WithTaggedFlag("ECX", 8)
                .WithReservedBits(9, 7)
                .WithReservedBits(16, 8)
                .WithValueField(24, 4, out errorChannelNumber, FieldMode.Read, name: "ERRCHN")
                .WithReservedBits(28, 3)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return channels[(int)errorChannelNumber.Value].Errors != ChannelError.NoError;
                }, name: "VLD");

            Registers.ManagementPageInterruptRequestStatus.Define(this, name: "MP_INT")
                .WithFlags(0, NumberOfChannels, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].IRQ.IsSet, name: "INT")
                .WithReservedBits(NumberOfChannels, 32 - NumberOfChannels);

            // During emulation transfers are executed immediately on peripheral requests,
            // so for software there are no active hardware service requests at any time.
            Registers.ManagementPageHardwareRequestStatus.Define(this, name: "MP_HRS")
                .WithFlags(0, NumberOfChannels, FieldMode.Read, valueProviderCallback: (i, _) => false, name: "HRS")
                .WithReservedBits(NumberOfChannels, 32 - NumberOfChannels);

            // No impact, channel arbitration doesn't matter during emulation, because all DMA requests are handled immediately.
            Registers.ChannelArbitrationGroup.DefineMany(this, (uint)NumberOfChannels, (register, channel) =>
            {
                register
                    .WithValueField(0, 5, name: "GRPRI")
                    .WithReservedBits(5, 27);
            }, name: "CHn_GRPRI");
        }

        private IFlagRegisterField enableDebug;
        private IFlagRegisterField haltAfterError;
        private IFlagRegisterField halt;
        private IFlagRegisterField globalChannelLinkingControl;
        private IEnumRegisterField<ChannelError> channelError;
        private IValueRegisterField errorChannelNumber;

        private readonly DmaEngine dmaEngine;
        private readonly Channel[] channels;
        private const int BaseSize = 0x1000;
        // The base module is used for a global channel management.
        // It's not really the index of a channel, but it's used in an address arithmetic to redirect access to a particular channel.
        // This value means, there is no redirection at all and the base module is accessed.
        private const int BaseModuleChannelIndex = -1;
        private const int MinimumNumberOfChannels = 1;
        private const int MaximumNumberOfChannels = 32;

        private enum Registers
        {
            ManagementPageControl = 0x00,
            ManagementPageErrorStatus = 0x04,
            ManagementPageInterruptRequestStatus = 0x08,
            ManagementPageHardwareRequestStatus = 0x0C,
            ChannelArbitrationGroup = 0x100,

            // Registers for each channel are inlined to support logging for either 8-channel or 16-channel instance.
            ChannelControlAndStatus0 = 0x1000,
            ChannelErrorStatus0 = 0x1004,
            ChannelInterruptStatus0 = 0x1008,
            ChannelSystemBus0 = 0x100C,
            ChannelPriority0 = 0x1010,
            ChannelMultiplexorConfiguration0 = 0x1014,
            TCDSourceAddress0 = 0x1020,
            TCDSignedSourceAddressOffset0 = 0x1024,
            TCDTransferAttributes0 = 0x1026,
            TCDTransferSize0 = 0x1028,
            TCDLastSourceAddressAdjustment0 = 0x102C,
            TCDDestinationAddress0 = 0x1030,
            TCDSignedDestinationAddressOffset0 = 0x1034,
            TCDCurrentMajorLoopCount0 = 0x1036,
            TCDLastDestinationAddressAdjustment0 = 0x1038,
            TCDControlAndStatus0 = 0x103C,
            TCDBeginningMajorLoopCount0 = 0x103E,

            ChannelControlAndStatus1 = 0x2000,
            ChannelErrorStatus1 = 0x2004,
            ChannelInterruptStatus1 = 0x2008,
            ChannelSystemBus1 = 0x200C,
            ChannelPriority1 = 0x2010,
            ChannelMultiplexorConfiguration1 = 0x2014,
            TCDSourceAddress1 = 0x2020,
            TCDSignedSourceAddressOffset1 = 0x2024,
            TCDTransferAttributes1 = 0x2026,
            TCDTransferSize1 = 0x2028,
            TCDLastSourceAddressAdjustment1 = 0x202C,
            TCDDestinationAddress1 = 0x2030,
            TCDSignedDestinationAddressOffset1 = 0x2034,
            TCDCurrentMajorLoopCount1 = 0x2036,
            TCDLastDestinationAddressAdjustment1 = 0x2038,
            TCDControlAndStatus1 = 0x203C,
            TCDBeginningMajorLoopCount1 = 0x203E,

            ChannelControlAndStatus2 = 0x3000,
            ChannelErrorStatus2 = 0x3004,
            ChannelInterruptStatus2 = 0x3008,
            ChannelSystemBus2 = 0x300C,
            ChannelPriority2 = 0x3010,
            ChannelMultiplexorConfiguration2 = 0x3014,
            TCDSourceAddress2 = 0x3020,
            TCDSignedSourceAddressOffset2 = 0x3024,
            TCDTransferAttributes2 = 0x3026,
            TCDTransferSize2 = 0x3028,
            TCDLastSourceAddressAdjustment2 = 0x302C,
            TCDDestinationAddress2 = 0x3030,
            TCDSignedDestinationAddressOffset2 = 0x3034,
            TCDCurrentMajorLoopCount2 = 0x3036,
            TCDLastDestinationAddressAdjustment2 = 0x3038,
            TCDControlAndStatus2 = 0x303C,
            TCDBeginningMajorLoopCount2 = 0x303E,

            ChannelControlAndStatus3 = 0x4000,
            ChannelErrorStatus3 = 0x4004,
            ChannelInterruptStatus3 = 0x4008,
            ChannelSystemBus3 = 0x400C,
            ChannelPriority3 = 0x4010,
            ChannelMultiplexorConfiguration3 = 0x4014,
            TCDSourceAddress3 = 0x4020,
            TCDSignedSourceAddressOffset3 = 0x4024,
            TCDTransferAttributes3 = 0x4026,
            TCDTransferSize3 = 0x4028,
            TCDLastSourceAddressAdjustment3 = 0x402C,
            TCDDestinationAddress3 = 0x4030,
            TCDSignedDestinationAddressOffset3 = 0x4034,
            TCDCurrentMajorLoopCount3 = 0x4036,
            TCDLastDestinationAddressAdjustment3 = 0x4038,
            TCDControlAndStatus3 = 0x403C,
            TCDBeginningMajorLoopCount3 = 0x403E,

            ChannelControlAndStatus4 = 0x5000,
            ChannelErrorStatus4 = 0x5004,
            ChannelInterruptStatus4 = 0x5008,
            ChannelSystemBus4 = 0x500C,
            ChannelPriority4 = 0x5010,
            ChannelMultiplexorConfiguration4 = 0x5014,
            TCDSourceAddress4 = 0x5020,
            TCDSignedSourceAddressOffset4 = 0x5024,
            TCDTransferAttributes4 = 0x5026,
            TCDTransferSize4 = 0x5028,
            TCDLastSourceAddressAdjustment4 = 0x502C,
            TCDDestinationAddress4 = 0x5030,
            TCDSignedDestinationAddressOffset4 = 0x5034,
            TCDCurrentMajorLoopCount4 = 0x5036,
            TCDLastDestinationAddressAdjustment4 = 0x5038,
            TCDControlAndStatus4 = 0x503C,
            TCDBeginningMajorLoopCount4 = 0x503E,

            ChannelControlAndStatus5 = 0x6000,
            ChannelErrorStatus5 = 0x6004,
            ChannelInterruptStatus5 = 0x6008,
            ChannelSystemBus5 = 0x600C,
            ChannelPriority5 = 0x6010,
            ChannelMultiplexorConfiguration5 = 0x6014,
            TCDSourceAddress5 = 0x6020,
            TCDSignedSourceAddressOffset5 = 0x6024,
            TCDTransferAttributes5 = 0x6026,
            TCDTransferSize5 = 0x6028,
            TCDLastSourceAddressAdjustment5 = 0x602C,
            TCDDestinationAddress5 = 0x6030,
            TCDSignedDestinationAddressOffset5 = 0x6034,
            TCDCurrentMajorLoopCount5 = 0x6036,
            TCDLastDestinationAddressAdjustment5 = 0x6038,
            TCDControlAndStatus5 = 0x603C,
            TCDBeginningMajorLoopCount5 = 0x603E,

            ChannelControlAndStatus6 = 0x7000,
            ChannelErrorStatus6 = 0x7004,
            ChannelInterruptStatus6 = 0x7008,
            ChannelSystemBus6 = 0x700C,
            ChannelPriority6 = 0x7010,
            ChannelMultiplexorConfiguration6 = 0x7014,
            TCDSourceAddress6 = 0x7020,
            TCDSignedSourceAddressOffset6 = 0x7024,
            TCDTransferAttributes6 = 0x7026,
            TCDTransferSize6 = 0x7028,
            TCDLastSourceAddressAdjustment6 = 0x702C,
            TCDDestinationAddress6 = 0x7030,
            TCDSignedDestinationAddressOffset6 = 0x7034,
            TCDCurrentMajorLoopCount6 = 0x7036,
            TCDLastDestinationAddressAdjustment6 = 0x7038,
            TCDControlAndStatus6 = 0x703C,
            TCDBeginningMajorLoopCount6 = 0x703E,

            ChannelControlAndStatus7 = 0x8000,
            ChannelErrorStatus7 = 0x8004,
            ChannelInterruptStatus7 = 0x8008,
            ChannelSystemBus7 = 0x800C,
            ChannelPriority7 = 0x8010,
            ChannelMultiplexorConfiguration7 = 0x8014,
            TCDSourceAddress7 = 0x8020,
            TCDSignedSourceAddressOffset7 = 0x8024,
            TCDTransferAttributes7 = 0x8026,
            TCDTransferSize7 = 0x8028,
            TCDLastSourceAddressAdjustment7 = 0x802C,
            TCDDestinationAddress7 = 0x8030,
            TCDSignedDestinationAddressOffset7 = 0x8034,
            TCDCurrentMajorLoopCount7 = 0x8036,
            TCDLastDestinationAddressAdjustment7 = 0x8038,
            TCDControlAndStatus7 = 0x803C,
            TCDBeginningMajorLoopCount7 = 0x803E,

            ChannelControlAndStatus8 = 0x9000,
            ChannelErrorStatus8 = 0x9004,
            ChannelInterruptStatus8 = 0x9008,
            ChannelSystemBus8 = 0x900C,
            ChannelPriority8 = 0x9010,
            ChannelMultiplexorConfiguration8 = 0x9014,
            TCDSourceAddress8 = 0x9020,
            TCDSignedSourceAddressOffset8 = 0x9024,
            TCDTransferAttributes8 = 0x9026,
            TCDTransferSize8 = 0x9028,
            TCDLastSourceAddressAdjustment8 = 0x902C,
            TCDDestinationAddress8 = 0x9030,
            TCDSignedDestinationAddressOffset8 = 0x9034,
            TCDCurrentMajorLoopCount8 = 0x9036,
            TCDLastDestinationAddressAdjustment8 = 0x9038,
            TCDControlAndStatus8 = 0x903C,
            TCDBeginningMajorLoopCount8 = 0x903E,

            ChannelControlAndStatus9 = 0xA000,
            ChannelErrorStatus9 = 0xA004,
            ChannelInterruptStatus9 = 0xA008,
            ChannelSystemBus9 = 0xA00C,
            ChannelPriority9 = 0xA010,
            ChannelMultiplexorConfiguration9 = 0xA014,
            TCDSourceAddress9 = 0xA020,
            TCDSignedSourceAddressOffset9 = 0xA024,
            TCDTransferAttributes9 = 0xA026,
            TCDTransferSize9 = 0xA028,
            TCDLastSourceAddressAdjustment9 = 0xA02C,
            TCDDestinationAddress9 = 0xA030,
            TCDSignedDestinationAddressOffset9 = 0xA034,
            TCDCurrentMajorLoopCount9 = 0xA036,
            TCDLastDestinationAddressAdjustment9 = 0xA038,
            TCDControlAndStatus9 = 0xA03C,
            TCDBeginningMajorLoopCount9 = 0xA03E,

            ChannelControlAndStatus10 = 0xB000,
            ChannelErrorStatus10 = 0xB004,
            ChannelInterruptStatus10 = 0xB008,
            ChannelSystemBus10 = 0xB00C,
            ChannelPriority10 = 0xB010,
            ChannelMultiplexorConfiguration10 = 0xB014,
            TCDSourceAddress10 = 0xB020,
            TCDSignedSourceAddressOffset10 = 0xB024,
            TCDTransferAttributes10 = 0xB026,
            TCDTransferSize10 = 0xB028,
            TCDLastSourceAddressAdjustment10 = 0xB02C,
            TCDDestinationAddress10 = 0xB030,
            TCDSignedDestinationAddressOffset10 = 0xB034,
            TCDCurrentMajorLoopCount10 = 0xB036,
            TCDLastDestinationAddressAdjustment10 = 0xB038,
            TCDControlAndStatus10 = 0xB03C,
            TCDBeginningMajorLoopCount10 = 0xB03E,

            ChannelControlAndStatus11 = 0xC000,
            ChannelErrorStatus11 = 0xC004,
            ChannelInterruptStatus11 = 0xC008,
            ChannelSystemBus11 = 0xC00C,
            ChannelPriority11 = 0xC010,
            ChannelMultiplexorConfiguration11 = 0xC014,
            TCDSourceAddress11 = 0xC020,
            TCDSignedSourceAddressOffset11 = 0xC024,
            TCDTransferAttributes11 = 0xC026,
            TCDTransferSize11 = 0xC028,
            TCDLastSourceAddressAdjustment11 = 0xC02C,
            TCDDestinationAddress11 = 0xC030,
            TCDSignedDestinationAddressOffset11 = 0xC034,
            TCDCurrentMajorLoopCount11 = 0xC036,
            TCDLastDestinationAddressAdjustment11 = 0xC038,
            TCDControlAndStatus11 = 0xC03C,
            TCDBeginningMajorLoopCount11 = 0xC03E,

            ChannelControlAndStatus12 = 0xD000,
            ChannelErrorStatus12 = 0xD004,
            ChannelInterruptStatus12 = 0xD008,
            ChannelSystemBus12 = 0xD00C,
            ChannelPriority12 = 0xD010,
            ChannelMultiplexorConfiguration12 = 0xD014,
            TCDSourceAddress12 = 0xD020,
            TCDSignedSourceAddressOffset12 = 0xD024,
            TCDTransferAttributes12 = 0xD026,
            TCDTransferSize12 = 0xD028,
            TCDLastSourceAddressAdjustment12 = 0xD02C,
            TCDDestinationAddress12 = 0xD030,
            TCDSignedDestinationAddressOffset12 = 0xD034,
            TCDCurrentMajorLoopCount12 = 0xD036,
            TCDLastDestinationAddressAdjustment12 = 0xD038,
            TCDControlAndStatus12 = 0xD03C,
            TCDBeginningMajorLoopCount12 = 0xD03E,

            ChannelControlAndStatus13 = 0xE000,
            ChannelErrorStatus13 = 0xE004,
            ChannelInterruptStatus13 = 0xE008,
            ChannelSystemBus13 = 0xE00C,
            ChannelPriority13 = 0xE010,
            ChannelMultiplexorConfiguration13 = 0xE014,
            TCDSourceAddress13 = 0xE020,
            TCDSignedSourceAddressOffset13 = 0xE024,
            TCDTransferAttributes13 = 0xE026,
            TCDTransferSize13 = 0xE028,
            TCDLastSourceAddressAdjustment13 = 0xE02C,
            TCDDestinationAddress13 = 0xE030,
            TCDSignedDestinationAddressOffset13 = 0xE034,
            TCDCurrentMajorLoopCount13 = 0xE036,
            TCDLastDestinationAddressAdjustment13 = 0xE038,
            TCDControlAndStatus13 = 0xE03C,
            TCDBeginningMajorLoopCount13 = 0xE03E,

            ChannelControlAndStatus14 = 0xF000,
            ChannelErrorStatus14 = 0xF004,
            ChannelInterruptStatus14 = 0xF008,
            ChannelSystemBus14 = 0xF00C,
            ChannelPriority14 = 0xF010,
            ChannelMultiplexorConfiguration14 = 0xF014,
            TCDSourceAddress14 = 0xF020,
            TCDSignedSourceAddressOffset14 = 0xF024,
            TCDTransferAttributes14 = 0xF026,
            TCDTransferSize14 = 0xF028,
            TCDLastSourceAddressAdjustment14 = 0xF02C,
            TCDDestinationAddress14 = 0xF030,
            TCDSignedDestinationAddressOffset14 = 0xF034,
            TCDCurrentMajorLoopCount14 = 0xF036,
            TCDLastDestinationAddressAdjustment14 = 0xF038,
            TCDControlAndStatus14 = 0xF03C,
            TCDBeginningMajorLoopCount14 = 0xF03E,

            ChannelControlAndStatus15 = 0x10000,
            ChannelErrorStatus15 = 0x10004,
            ChannelInterruptStatus15 = 0x10008,
            ChannelSystemBus15 = 0x1000C,
            ChannelPriority15 = 0x10010,
            ChannelMultiplexorConfiguration15 = 0x10014,
            TCDSourceAddress15 = 0x10020,
            TCDSignedSourceAddressOffset15 = 0x10024,
            TCDTransferAttributes15 = 0x10026,
            TCDTransferSize15 = 0x10028,
            TCDLastSourceAddressAdjustment15 = 0x1002C,
            TCDDestinationAddress15 = 0x10030,
            TCDSignedDestinationAddressOffset15 = 0x10034,
            TCDCurrentMajorLoopCount15 = 0x10036,
            TCDLastDestinationAddressAdjustment15 = 0x10038,
            TCDControlAndStatus15 = 0x1003C,
            TCDBeginningMajorLoopCount15 = 0x1003E,
        }
    }
}