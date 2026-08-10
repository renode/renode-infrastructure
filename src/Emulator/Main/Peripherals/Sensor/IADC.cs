//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Analog;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensor
{
    public static class IADCExtensions
    {
        public static void SetVoltage(this IADC @this, decimal value, int channel)
        {
            @this.AssertChannel(channel);
            @this.SetADCValue(channel, (uint)(value * 1e6m));
        }

        public static decimal GetVoltage(this IADC @this, int channel)
        {
            @this.AssertChannel(channel);
            return (decimal)@this.GetADCValue(channel) / 1e6m;
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
        // ADC value should be treated as RESD VoltageSample value
        void SetADCValue(int channel, uint value)
        {
            // These functions are deprecated, so the default implementation is a throwing
            throw new RecoverableException("SetADCValue is deprecated and should not be used in new models. Use a ADCChannelSource instead");
        }

        uint GetADCValue(int channel)
        {
            throw new RecoverableException("GetADCValue is deprecated and should not be used in new models. Use a ADCChannelSource instead");
        }

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