//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class S32K3XX_DMAMUX : BasicBytePeripheral, IKnownSize, INumberedGPIOOutput
    {
        public S32K3XX_DMAMUX(IMachine machine, uint numberOfSlots = 64, uint numberOfChannels = 16, uint numberOfChannelsWithEnable = 4)
            : base(machine)
        {
            this.numberOfSlots = numberOfSlots;
            this.numberOfChannels = numberOfChannels;
            this.numberOfChannelsWithEnable = numberOfChannelsWithEnable;

            source = new IValueRegisterField[numberOfChannels];
            enable = new IFlagRegisterField[numberOfChannelsWithEnable];
            triggerEnable = new IFlagRegisterField[numberOfChannels];
            slotState = new bool[numberOfSlots];

            Connections = Enumerable.Range(0, (int)numberOfChannels).ToDictionary(i => i, _ => (IGPIO)new GPIO());

            DefineRegisters();
        }

        public long Size => 0x10;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public void OnGPIO(int slot, bool value)
        {
            if(slot < 0 || slot >= numberOfSlots)
            {
                this.WarningLog("Slot {0} is outside of allowed range 0-{1}", slot, numberOfSlots - 1);
            }
            slotState[slot] = value;
            for(var i = 0; i < numberOfChannels; ++i)
            {
                if(source[i].Value == (ulong)slot)
                {
                    SetChannelState(i, value);
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < numberOfSlots; ++i)
            {
                slotState[i] = false;
            }
            for(var i = 0; i < numberOfChannels; ++i)
            {
                SetChannelState(i, false);
            }
        }

        protected override void DefineRegisters()
        {
            Registers.ChannelConfiguration0.DefineMany(this, numberOfChannels, (register, i) =>
            {
                // Reverse order in windows of 4
                var channelId = i + 4 - 2 * (i % 4);
                register
                    .WithValueField(0, 6, out source[i], name: "SOURCE - DMA Channel Source (Slot)")
                    .If(i < numberOfChannelsWithEnable)
                        .Then(r => r
                            .WithFlag(6, out enable[i], name: "TRIG - DMA Channel Enable")
                        )
                        .Else(r => r
                            .WithReservedBits(6, 1)
                        )
                    .WithFlag(7, out triggerEnable[i], name: "ENBL - DMA Channel Trigger Enable")
                    .WithChangeCallback((_, __) => SetChannelState(channelId, slotState[source[i].Value]))
                ;
            });
        }

        private void SetChannelState(int channel, bool value)
        {
            Connections[channel].Set(value);
        }

        private readonly uint numberOfChannelsWithEnable;
        private readonly uint numberOfChannels;
        private readonly uint numberOfSlots;

        private readonly bool[] slotState;
        private readonly IValueRegisterField[] source;
        private readonly IFlagRegisterField[] enable;
        private readonly IFlagRegisterField[] triggerEnable;

        public enum Registers
        {
            ChannelConfiguration0  = 0x0,
            ChannelConfiguration1  = 0x1,
            ChannelConfiguration2  = 0x2,
            ChannelConfiguration3  = 0x3,
            ChannelConfiguration4  = 0x4,
            ChannelConfiguration5  = 0x5,
            ChannelConfiguration6  = 0x6,
            ChannelConfiguration7  = 0x7,
            ChannelConfiguration8  = 0x8,
            ChannelConfiguration9  = 0x9,
            ChannelConfiguration10 = 0xA,
            ChannelConfiguration11 = 0xB,
            ChannelConfiguration12 = 0xC,
            ChannelConfiguration13 = 0xD,
            ChannelConfiguration14 = 0xE,
            ChannelConfiguration15 = 0xF,
        }
    }
}
