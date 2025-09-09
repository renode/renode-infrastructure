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
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32_DMAMUX : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public STM32_DMAMUX(Machine machine, int numberOfOutputRequestChannels, int numberOfRequestGeneratorChannels) : base(machine)
        {
            nrOfOutputRequestChannels = numberOfOutputRequestChannels;
            nrOfRequestGeneratorChannels = numberOfRequestGeneratorChannels;

            Connections = Enumerable
                .Range(0, nrOfOutputRequestChannels)
                .ToDictionary(idx => idx, _ => (IGPIO)new GPIO());

            requestId = new IValueRegisterField[nrOfOutputRequestChannels];

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var gpio in Connections.Values)
            {
                gpio.Unset();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            for(var channelId = 0; channelId < nrOfOutputRequestChannels; channelId++)
            {
                if(requestId[channelId].Value == (ulong)number)
                {
                    this.NoisyLog("Set IRQ {0}: {1}", number, value);
                    Connections[channelId].Set(value);
                }
            }
        }

        public long Size => 0x400;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            Registers.RequestLineMultiplexerChannelConfiguration.DefineMany(this, (uint)nrOfOutputRequestChannels,
                (reg, id) => reg
                    .WithValueField(0, 7, out requestId[id], name: "DMAREQ_ID")
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("SOIE", 8)
                    .WithTaggedFlag("EGE", 9)
                    .WithReservedBits(10, 6)
                    .WithTaggedFlag("SE", 16)
                    .WithTag("SPOL", 17, 2)
                    .WithTag("NBREQ", 19, 5)
                    .WithTag("SYNC_ID", 24, 2)
                    .WithReservedBits(26, 6)
            );

            Registers.RequestLineMultiplexerInterruptStatus.Define(this)
                .WithFlags(0, nrOfOutputRequestChannels, out var requestLineMuxIrqStatus, FieldMode.Read, name: "SOF")
                .WithReservedBits(nrOfOutputRequestChannels, 32 - nrOfOutputRequestChannels);

            Registers.RequestLineMultiplexerInterruptClear.Define(this)
                .WithFlags(0, nrOfOutputRequestChannels, FieldMode.Set, name: "CSOF",
                    writeCallback: (id, _, val) => { if(val) requestLineMuxIrqStatus[id].Value = false; })
                .WithReservedBits(nrOfOutputRequestChannels, 32 - nrOfOutputRequestChannels);

            Registers.RequestGeneratorChannelConfiguration.DefineMany(this, (uint)nrOfRequestGeneratorChannels,
                (reg, _) => reg
                    .WithTag("SIG_ID", 0, 3)
                    .WithReservedBits(3, 5)
                    .WithTaggedFlag("OIE", 8)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag("GE", 16)
                    .WithTag("GPOL", 17, 2)
                    .WithTag("GNBREQ", 19, 5)
                    .WithReservedBits(24, 8)
            );

            Registers.RequestGeneratorInterruptStatus.Define(this)
                .WithTaggedFlags("OF", 0, nrOfRequestGeneratorChannels)
                .WithReservedBits(nrOfRequestGeneratorChannels, 32 - nrOfRequestGeneratorChannels);

            Registers.RequestGeneratorInterruptClear.Define(this)
                .WithTaggedFlags("COF", 0, nrOfRequestGeneratorChannels)
                .WithReservedBits(nrOfRequestGeneratorChannels, 32 - nrOfRequestGeneratorChannels);
        }

        private readonly IValueRegisterField[] requestId;

        private readonly int nrOfOutputRequestChannels;
        private readonly int nrOfRequestGeneratorChannels;

        private enum Registers
        {
            RequestLineMultiplexerChannelConfiguration      = 0x0,      // CxCR
            RequestLineMultiplexerInterruptStatus           = 0x80,     // CSR
            RequestLineMultiplexerInterruptClear            = 0x84,     // CFR
            RequestGeneratorChannelConfiguration            = 0x100,    // RGxCR
            RequestGeneratorInterruptStatus                 = 0x140,    // RGSR
            RequestGeneratorInterruptClear                  = 0x144,    // RGCFR
        }
    }
}