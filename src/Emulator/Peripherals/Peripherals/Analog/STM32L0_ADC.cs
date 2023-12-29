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
    public class STM32L0_ADC : STM32_ADC_Common
    {
        public STM32L0_ADC(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null)
            : base(
                machine,
                referenceVoltage,
                externalEventFrequency,
                dmaChannel,
                dmaPeripheral,
                // Base class configuration
                watchdogCount: 1,
                hasCalibration: true,
                hasHighCalAddress: false,
                channelCount: 18,
                hasPrescaler: true,
                hasVbatPin: false,
                hasChannelSequence: false,
                hasPowerRegister: true
            )
        {}
    }
}
