//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;

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
    }

    public interface IADC : ISensor
    {
        // ADC value should be treated as RESD VoltageSample value
        void SetADCValue(int channel, uint value);

        uint GetADCValue(int channel);

        int ADCChannelCount { get; }
    }
}