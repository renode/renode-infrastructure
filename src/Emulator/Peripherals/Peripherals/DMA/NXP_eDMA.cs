//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

using Channel = Antmicro.Renode.Peripherals.DMA.NXP_eDMA_Channels.Channel;

namespace Antmicro.Renode.Peripherals.DMA
{
    public partial class NXP_eDMA : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IHasOwnLife, IGPIOReceiver
    {
        public NXP_eDMA(IMachine machine, int numberOfChannels) : base(machine)
        {
            if(numberOfChannels < MinimumNumberOfChannels || numberOfChannels > MaximumNumberOfChannels)
            {
                throw new ConstructionException($"The number of channels {numberOfChannels} isn't in the allowed range {MinimumNumberOfChannels}-{MaximumNumberOfChannels}");
            }

            NumberOfChannels = numberOfChannels;

            connections = new Dictionary<int, IGPIO>();
            channels = new Channel[NumberOfChannels];

            DefineRegisters();
        }

        public void Start()
        {
            AssertChannels();
        }

        public void Pause()
        {
            // Intentionally left empty
        }

        public void Resume()
        {
            // Intentionally left empty
        }

        public bool IsPaused => false;

        public void OnGPIO(int channel, bool value)
        {
            if(channel < 0 || channel >= MaximumNumberOfChannels)
            {
                this.WarningLog("Channel {0} is outside of allowed range  0-{1}", channel, MaximumNumberOfChannels - 1);
                return;
            }
            if(!value)
            {
                return;
            }
            channels[channel].HardwareServiceRequest();
        }

        public bool TryGetChannelBySlot(int slot, out int channelNumber)
        {
            var channel = channels.FirstOrDefault(x => x.ServiceRequestSource == slot)?.ChannelNumber;
            channelNumber = channel ?? default(int);
            return channel.HasValue;
        }

        public void SetChannel(int channelNumber, Channel channel)
        {
            if(channelNumber < 0 || channelNumber >= MaximumNumberOfChannels)
            {
                throw new ConstructionException($"Peripheral is configured for {MaximumNumberOfChannels} channel, attempted to register channel {channelNumber}");
            }
            channels[channelNumber] = channel;
            connections[channelNumber] = channels[channelNumber].IRQ;
        }

        public bool IsTransferAllowed()
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

        public void ReportErrorOnChannel(int channelNumber)
        {
            if(channelNumber >= NumberOfChannels)
            {
                throw new ArgumentException($"Cannot report error on a nonexistent channel {channelNumber} - allowed channel numbers are in the range 0-{NumberOfChannels - 1}");
            }
            if(haltAfterError.Value)
            {
                halt.Value = true;
            }
            errorChannelNumber.Value = (ulong)channelNumber;
            // Error indicators are sticky and cannot be cleared.
            // They show the last recorded error until the DMA is reset.
            channelError.Value = channels[channelNumber].Errors;
        }

        public void LinkChannel(int initiatorChannelNumber, int linkedChannelNumber)
        {
            if(linkedChannelNumber < 0 || linkedChannelNumber >= NumberOfChannels)
            {
                this.WarningLog("Unable to link to a nonexistent channel {0} from channel {1}", linkedChannelNumber, initiatorChannelNumber);
                return;
            }
            this.DebugLog("Channel linking from CH{0} to CH{1}", initiatorChannelNumber, linkedChannelNumber);
            channels[linkedChannelNumber].ChannelLinkInternalRequest();
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
            get
            {
                AssertChannels();
                return connections;
            }
        }

        public bool ProvidesWithMuxingConfiguration
        {
            get
            {
                AssertChannels();
                return channels[0].ServiceRequestSource != null;
            }
        }

        public int NumberOfChannels { get; }

        public bool Halt
        {
            set => halt.Value = value;
        }

        private void AssertChannels()
        {
            if(channelsChecked)
            {
                return;
            }

            var channelsWithMuxing = channels[0]?.ServiceRequestSource.HasValue ?? false;

            for(var i = 0; i < NumberOfChannels; ++i)
            {
                if(channels[i] == null)
                {
                    throw new RecoverableException($"Channel {i} not registered");
                }
                if(channelsWithMuxing != channels[i].ServiceRequestSource.HasValue)
                {
                    throw new RecoverableException($"Mixed multiplexing detected, channel {i} does not match with previous channels");
                }
            }

            channelsChecked = true;
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
                .WithTag("ACTIVE_ID", 24, 5) // Software never observes ACTIVE bit 1 during emulation.
                .WithReservedBits(29, 2)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ =>
                {
                    // Transfers are immediate so eDMA is always idle from the software perspective.
                    return false;
                }, name: "ACTIVE");

            // During emulation channel errors can occur only due to an illegal setting in the transfer control descriptor.
            // Bus and memory errors are not possible.
            // Error flags mirror flags in the corresponding channel specified by ERRCHN.
            Registers.ManagementPageErrorStatus.Define(this, name: "MP_ES")
                .WithEnumField<DoubleWordRegister, Channel.ErrorFlags>(0, 8, out channelError, FieldMode.Read, name: "DBE|SBE|SGE|NCE|DOE|DAE|SOE|SAE")
                .WithTaggedFlag("ECX", 8)
                .WithTaggedFlag("UCE", 9)
                .WithReservedBits(10, 6)
                .WithReservedBits(16, 8)
                .WithValueField(24, 5, out errorChannelNumber, FieldMode.Read, name: "ERRCHN")
                .WithReservedBits(29, 2)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return channels[(int)errorChannelNumber.Value].Errors != Channel.ErrorFlags.NoError;
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
        private IEnumRegisterField<Channel.ErrorFlags> channelError;
        private IValueRegisterField errorChannelNumber;

        private bool channelsChecked;
        private readonly Channel[] channels;
        private readonly Dictionary<int, IGPIO> connections;

        private const int MinimumNumberOfChannels = 1;
        private const int MaximumNumberOfChannels = 32;

        private enum Registers
        {
            ManagementPageControl = 0x00,
            ManagementPageErrorStatus = 0x04,
            ManagementPageInterruptRequestStatus = 0x08,
            ManagementPageHardwareRequestStatus = 0x0C,
            ChannelArbitrationGroup = 0x100,
        }
    }
}
