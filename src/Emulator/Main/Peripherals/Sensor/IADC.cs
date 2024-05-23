//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Sensor
{
    public interface IADC : ISensor
    {
        // ADC value should be treated as RESD VoltageSample value
        void SetADCValue(int channel, uint value);
        uint GetADCValue(int channel);

        int ADCChannelCount { get; }
    }
}
