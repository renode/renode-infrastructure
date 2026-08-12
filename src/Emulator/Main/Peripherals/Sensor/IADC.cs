//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Analog;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensor
{
    public static class IADCExtensions
    {
        public static void SetVoltage(this IADC @this, uint voltage, int channel)
        {
            if(!@this.ADCContainer.TryGetByAddress(channel, out var sampleSource))
            {
                throw new RecoverableException($"Invalid channel {channel}");
            }

            if(sampleSource is ADCChannelSource)
            {
                var channelSource = sampleSource as ADCChannelSource;
                channelSource.Sample = new VoltageSample(voltage);
            }
            else if(sampleSource is NamedDiscreteValues)
            {
                var namedDiscreteValues = sampleSource as NamedDiscreteValues;
                var state = namedDiscreteValues.FirstOrDefault(x => x.Value.Voltage == voltage).Key;
                if(state == null)
                {
                    throw new RecoverableException($"Invalid voltage value {voltage}");
                }
                namedDiscreteValues.CurrentState = state;
            }
            else if(sampleSource is Potentiometer)
            {
                var potentiometer = sampleSource as Potentiometer;
                (var min, var max) = potentiometer.Bounds;
                var percentage = Misc.RemapNumber(voltage, min, max, 0, 100);
                if(!percentage.HasValue)
                {
                    throw new RecoverableException($"Invalid voltage value {voltage} not in [{min}; {max}]");
                }
                potentiometer.Percentage = percentage.Value;
            }
            else
            {
                throw new RecoverableException($"Unknown channel source type");
            }
        }

        public static uint GetVoltage(this IADC @this, int channel)
        {
            if(!@this.ADCContainer.TryGetByAddress(channel, out var sampleSource))
            {
                throw new RecoverableException($"Invalid channel {channel}");
            }

            return sampleSource.Sample.Voltage;
        }

        public static void AssertChannel(this IADC @this, int channel)
        {
            if(channel < 0 || channel >= @this.ADCChannelCount)
            {
                throw new RecoverableException($"'{nameof(channel)}' is not in [0, {@this.ADCChannelCount - 1}] range");
            }
        }

        public static void RegisterDefaultChildren(this IADC @this, IMachine machine)
        {
            machine.PeripheralsChanged += (machine, ev) =>
            {
                /* We need to create default children as soon as this ADC peripheral exists.
                 * However, the channel name must be unique at the machine level so the ADC name is
                 * prefixed. The creation driver first register the ADC device and then sets its
                 * local name. So the default children are created on the
                 * PeripheralChangeType.NamedChanged event instead of PeripheralChangeType.Addition.
                 */
                if(ev.Peripheral == @this && ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.NameChanged)
                {
                    @this.DoRegisterDefaultChildren(machine);
                }
            };
        }

        private static void DoRegisterDefaultChildren(this IADC @this, IMachine machine)
        {
            machine.TryGetLocalName(@this, out var adcName);

            for(var i = 0; i < @this.ADCChannelCount; i++)
            {
                IRESDSampleSource<VoltageSample> channelSource = new ADCDefaultChannelSource();
                @this.Register(channelSource, new NumberRegistrationPoint<int>(i));
                machine.SetLocalName(channelSource, $"{adcName}-{@this.GetDefaultChannelName(i)}");
            }
        }
    }

    public interface IADC : ISensor, IPeripheralContainer<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>
    {
        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Register(IRESDSampleSource<VoltageSample> peripheral, NumberRegistrationPoint<int> channel)
        {
            IRESDSampleSource<VoltageSample> sampleSource;

            this.AssertChannel(channel.Address);

            // Allow to register a new source over the default child.
            if(ADCContainer.TryGetByAddress(channel.Address, out sampleSource) && sampleSource is ADCDefaultChannelSource)
            {
                ADCContainer.Unregister(sampleSource);
            }

            ADCContainer.Register(peripheral, channel);
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Unregister(IRESDSampleSource<VoltageSample> peripheral)
        {
            ADCContainer.Unregister(peripheral);
        }

        string GetDefaultChannelName(int channel)
        {
            return $"channel{channel}";
        }

        int ADCChannelCount { get; }

        SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; }

        IEnumerable<IRegistered<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>> IPeripheralContainer<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Children => ADCContainer.Children;

        IEnumerable<NumberRegistrationPoint<int>> IPeripheralContainer<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.GetRegistrationPoints(IRESDSampleSource<VoltageSample> peripheral) => ADCContainer.GetRegistrationPoints(peripheral);
    }
}