//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32G0_ADC : STM32_ADC_Common
    {
        public STM32G0_ADC(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null)
            : base(
                machine,
                referenceVoltage,
                externalEventFrequency,
                dmaChannel,
                dmaPeripheral,
                // Base class configuration
                watchdogCount: 1,
                hasCalibration: false,
                channelCount: 19,
                hasPrescaler: false,
                hasVbatPin: true,
                hasChannelSelect: true,
                hasChannelSequence: true,
                hasPowerRegister: false
            )
        {}
    }
}
