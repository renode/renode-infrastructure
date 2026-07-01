//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32H7_ADC : STM32_ADC_Common
    {
        public STM32H7_ADC(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null)
            : base(
                machine,
                referenceVoltage,
                externalEventFrequency,
                dmaChannel,
                dmaPeripheral,
                // Base class configuration
                watchdogCount: 3,
                hasCalibration: true,
                channelCount: 19,
                hasPrescaler: true,
                hasVbatPin: true,
                hasChannelSelect: false,
                hasChannelSequence: true,
                hasPowerRegister: false,
                hasOffset: true,
                hasDifferentialMode: true,
                samplingTime: SamplingTime.PerChannel,
                dualMode: true,
                hasLinearityCalibration: true,
                hasChannelInjection: true,
                hasSeparateThresholdRegisters: true,
                resolutionRange: ResolutionRange.Bits8_16,
                hasChannelPreselection: true,
                hasScanDirection: false
            )
        { }
    }
}
