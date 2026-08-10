//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class EOSS3_ADC : BasicDoubleWordPeripheral, IKnownSize, IADC
    {
        public EOSS3_ADC(IMachine machine) : base(machine)
        {
            ADCContainer = new SimpleContainerHelper<IRESDSampleSource<VoltageSample>>(machine, this);
            DefineRegisters();
            this.RegisterDefaultChildren(machine);
        }

        public override void Reset()
        {
            base.Reset();
            state = State.Idle;
        }

        public long Size => 0x10;

        public int ADCChannelCount => 2;

        public SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; }

        private void DefineRegisters()
        {
            Registers.Out.Define(this)
                .WithValueField(0, 12, FieldMode.Read, name: "out", valueProviderCallback: _ => GetSample())
                .WithReservedBits(12, 20)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "eoc", valueProviderCallback: _ =>
                {
                    switch(state)
                    {
                    case State.Idle:
                        return true;

                    case State.ConversionStarted:
                        state = State.SampleReady;
                        return false;

                    case State.SampleReady:
                        state = State.Idle;
                        return true;

                    default:
                        throw new ArgumentException($"Unexpected state: {state}");
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Registers.Control.Define(this)
                .WithFlag(0, name: "soc", writeCallback: (_, val) => StartConversion(val))
                .WithFlag(1, out selectedChannel1, name: "sel")
                .WithFlag(2, name: "meas_en")
                .WithReservedBits(3, 28)
            ;
        }

        private void StartConversion(bool flag)
        {
            state = flag
                ? State.ConversionStarted
                : State.Idle;
        }

        private uint GetSample()
        {
            int channelIndex = selectedChannel1.Value ? 1 : 0;
            if(ADCContainer.TryGetByAddress(channelIndex, out var source))
            {
                return source.Sample.ToADCRawValue(ReferenceVoltage, ResolutionInBits);
            }

            this.WarningLog("No ADC source connected to channel {0}, returning 0", channelIndex);
            return 0;
        }

        private State state;
        private IFlagRegisterField selectedChannel1;

        private const ushort ResolutionInBits = 12;
        private const decimal ReferenceVoltage = 1.4m; // From QL EOS S3 Ultra Low Power multicore MCU datasheet v3.3f.

        private enum State
        {
            Idle,
            ConversionStarted,
            SampleReady
        }

        private enum Registers
        {
            Out = 0x0,
            Status = 0x4,
            Control = 0x8
        }
    }
}