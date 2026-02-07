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

namespace Antmicro.Renode.Peripherals.DMA
{
    public class S32K3XX_DMAMUX : BasicBytePeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
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

        public void OnGPIO(int slot, bool value)
        {
            if(slot < 0 || slot >= numberOfSlots)
            {
                this.WarningLog("Slot {0} is outside of allowed range 0-{1}", slot, numberOfSlots - 1);
            }
            slotState[slot] = value;
            for(var i = 0; i < numberOfChannels; ++i)
            {
                var channelId = CalculateChannelId(i);
                if(source[channelId].Value == (ulong)slot)
                {
                    if(value)
                    {
                        this.DebugLog("Slot #{0} triggered channel #{1}", slot, channelId);
                    }
                    SetChannelState(channelId, value);
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

        public long Size => 0x10;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        protected override void DefineRegisters()
        {
            // ChannelConfiguration3 is the register with the lowest offset
            Registers.ChannelConfiguration3.DefineMany(this, numberOfChannels, (register, i) =>
            {
                var channelId = CalculateChannelId(i);
                register
                    .WithValueField(0, 6, out source[channelId], name: "SOURCE - DMA Channel Source (Slot)")
                    .If(i < numberOfChannelsWithEnable)
                        .Then(r => r
                            .WithFlag(6, out enable[channelId], name: "TRIG - DMA Channel Enable")
                        )
                        .Else(r => r
                            .WithReservedBits(6, 1)
                        )
                    .WithFlag(7, out triggerEnable[channelId], name: "ENBL - DMA Channel Trigger Enable")
                    .WithChangeCallback((_, __) =>
                    {
                        this.DebugLog("Channel #{1} configured be triggered by slot {0}", source[channelId].Value, channelId);
                        SetChannelState(channelId, slotState[source[channelId].Value]);
                    })
                ;
            });
        }

        private void SetChannelState(int channel, bool value)
        {
            Connections[channel].Set(value);
        }

        private int CalculateChannelId(int index)
        {
            // Reverse order in windows of 4
            return index + 3 - 2 * (index % 4);
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
            ChannelConfiguration3  = 0x0,
            ChannelConfiguration2  = 0x1,
            ChannelConfiguration1  = 0x2,
            ChannelConfiguration0  = 0x3,
            ChannelConfiguration7  = 0x4,
            ChannelConfiguration6  = 0x5,
            ChannelConfiguration5  = 0x6,
            ChannelConfiguration4  = 0x7,
            ChannelConfiguration11 = 0x8,
            ChannelConfiguration10 = 0x9,
            ChannelConfiguration9  = 0xA,
            ChannelConfiguration8  = 0xB,
            ChannelConfiguration15 = 0xC,
            ChannelConfiguration14 = 0xD,
            ChannelConfiguration13 = 0xE,
            ChannelConfiguration12 = 0xF,
        }
    }
}
