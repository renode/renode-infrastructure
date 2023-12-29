//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2023 OS Systems
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32C0_ADC : STM32_ADC_Common
    {
        public STM32C0_ADC(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null)
            : base(
                machine,
                referenceVoltage,
                externalEventFrequency,
                dmaChannel,
                dmaPeripheral,
                // Base class configuration
                watchdogCount: 3,
                hasCalibration: true,
                hasHighCalAddress: false,
                channelCount: 23,
                hasPrescaler: false,
                hasVbatPin: false,
                hasChannelSequence: true,
                hasPowerRegister: false
            )
        {}
    }
}
