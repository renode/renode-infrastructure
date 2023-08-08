//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensors;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class IMXRT_ADC : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public IMXRT_ADC(IMachine machine) : base(machine)
        {
            samplesFifos = new SensorSamplesFifo<ScalarSample>[NumberOfChannels];
            for(var i = 0; i < samplesFifos.Length; i++)
            {
                samplesFifos[i] = new SensorSamplesFifo<ScalarSample>();
            }

            conversionCompleteInterruptEnable = new IFlagRegisterField[NumberOfHardwareTriggers];
            inputChannel = new IValueRegisterField[NumberOfHardwareTriggers];
            cdata = new IValueRegisterField[NumberOfHardwareTriggers];

            IRQ = new GPIO();
            DefineRegisters();
        }

        public GPIO IRQ { get; }

        public long Size => 0x5C;

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= NumberOfHardwareTriggers)
            {
                this.Log(LogLevel.Warning, "Unsupported external hardware trigger #{0} (supported are 0-{1})", number, NumberOfHardwareTriggers - 1);
                return;
            }

            if(value)
            {
                HandleTrigger(number, true);
            }
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        public void FeedSample(byte sample, int channel)
        {
            if(channel < 0 || channel >= NumberOfChannels)
            {
                throw new RecoverableException($"This model supports channels 0-{(NumberOfChannels- 1)} inclusive");
            }

            samplesFifos[channel].FeedSample(new ScalarSample(sample));
        }

        public void FeedSamplesFromFile(string path, int channel)
        {
            if(channel < 0 || channel >= NumberOfChannels)
            {
                throw new RecoverableException($"This model supports channels 0-{(NumberOfChannels- 1)} inclusive");
            }

            samplesFifos[channel].FeedSamplesFromFile(path);
        }

        public void TriggerConversion(uint channel)
        {
            if(channel >= NumberOfChannels)
            {
                throw new RecoverableException($"This model supports channels 0-{(NumberOfChannels - 1)} inclusive");
            }

            this.Log(LogLevel.Debug, "Starting conversion on channel #{0}", channel);
            samplesFifos[channel].TryDequeueNewSample();
            cdata[channel].Value = (uint)samplesFifos[channel].Sample.Value;

            conversionComplete.Value = true;
            UpdateInterrupts();
        }

        private void HandleTrigger(int triggerId, bool isHW)
        {
            this.Log(LogLevel.Debug, "Trigger #{0} fired", triggerId);

            var channel = (uint)inputChannel[triggerId].Value;
            if(channel == ConversionDisabledMask)
            {
                this.Log(LogLevel.Warning, "Trigger #{0} fired, but the conversion is disabled", triggerId);
                return;
            }
            if(channel == AdcEtcMask)
            {
                this.Log(LogLevel.Warning, "External channel selection is not supported for trigger #{0}", triggerId);
                return;
            }
            if(channel > 15)
            {
                this.Log(LogLevel.Warning, "Unsupported channel #{0} selected for trigger #{1}", channel, triggerId);
                return;
            }

            if(isHW && !hardwareTriggerSelect.Value)
            {
                this.Log(LogLevel.Warning, "Trigger #{0} fired, but hardware trigger select is not set", triggerId);
                return;
            }

            TriggerConversion(channel);
        }

        private void DefineRegisters()
        {
            Registers.HardwareTriggersControl0.DefineMany(this, NumberOfHardwareTriggers, (register, idx) =>
            {
                var j = idx;
                register
                    .WithValueField(0, 5, out inputChannel[j], name: "ADCH - Input Channel Select")
                    .WithReservedBits(5, 2)
                    .WithFlag(7, out conversionCompleteInterruptEnable[j], name: "AIEN - Conversion Complete Interrupt Enable/Disable Control")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) =>
                    {
                        if(j == 0)
                        {
                           // only trigger 0 can be fired by SW
                           HandleTrigger(0, false);
                        }
                    });
            });

            // NOTE: the documentation says there is only one bit here,
            // but shouldn't there be more?
            Registers.HardwareTriggersStatus.Define(this)
                .WithFlag(0, out conversionComplete, FieldMode.Read, name: "COCO0 - Conversion Complete Flag")
                .WithReservedBits(1, 31);

            Registers.HardwareTriggersDataResult0.DefineMany(this, NumberOfHardwareTriggers, (register, idx) =>
            {
              register
                .WithValueField(0, 12, out cdata[idx], FieldMode.Read, name: "CDATA - Data")
                .WithReservedBits(12, 20)
                .WithReadCallback((_, __) => 
                {
                    conversionComplete.Value = false; // Set this to clear COCOn and turn off IRQ after read
                    UpdateInterrupts();
                });
            });

            Registers.Configuration.Define(this)
                .WithTag("ADICLK - Input Clock Select", 0, 2)
                .WithTag("MODE - Conversion Mode Selection", 2, 2)
                .WithTaggedFlag("ADLSMP - Long Sample Time Configuration", 4)
                .WithTag("ADIV - Clock Divide Select", 5, 2)
                .WithTaggedFlag("ADLPC - Low-Power Configuration", 7)
                .WithTag("ADSTS - Total Sample Duration In Number Of Full Cycles", 8, 2)
                .WithTaggedFlag("ADHSC - High Speed Configuration", 10)
                .WithTag("REFSEL - Voltage Reference Selection", 11, 2)
                .WithFlag(13, out hardwareTriggerSelect, name: "ADTRG - Conversion Trigger Select")
                .WithTag("AVGS - Hardware Avarage select", 14, 2)
                .WithTaggedFlag("OVWREN - Data Overwrite Enable", 16)
                .WithReservedBits(17, 15);

            Registers.GeneralControl.Define(this)
                .WithTaggedFlag("ADACKEN - Asynchronous clock output enable", 0)
                .WithTaggedFlag("DMAEN - DMA Enable", 1)
                .WithTaggedFlag("ACREN - Compare Function Range Enable", 2)
                .WithTaggedFlag("ACFGT - Compare Function Enable", 3)
                .WithTaggedFlag("ACFE - Compare Function Enable", 4)
                .WithTaggedFlag("AVGE - Hardware avarage enable", 5)
                .WithTaggedFlag("ADCO - Continouous Conversion Enable", 6)
                .WithFlag(7, name: "CAL - Calibration", valueProviderCallback: _ => false) // calibration ends right away
                .WithReservedBits(8, 24);

            Registers.GeneralStatus.Define(this)
                .WithTaggedFlag("ADACT - Conversion Active", 0)
                .WithTaggedFlag("CALF - Calibration Failed Flag", 1)
                .WithTaggedFlag("AWKST - Asynchronous wakeup interrupt status", 2)
                .WithReservedBits(3, 29);

            Registers.CompareValue.Define(this)
                .WithTag("CV1 - Compare Value 1", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("CV2 - Compare Value 2", 16, 12)
                .WithReservedBits(28, 4);

            Registers.OffsetCorrectionValue.Define(this)
                .WithTag("OFS - Offset value", 0, 12)
                .WithTaggedFlag("SIGN - Sign bit", 12)
                .WithReservedBits(13, 19);

            Registers.CalibrationValue.Define(this)
                .WithTag("CAL_CODE - Calibration Result Value", 0, 4)
                .WithReservedBits(4, 28);
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= conversionComplete.Value && conversionCompleteInterruptEnable.Any(x => x.Value);

            this.Log(LogLevel.Info, "Setting IRQ to {0}", flag);
            IRQ.Set(flag);
        }

        private IFlagRegisterField hardwareTriggerSelect;
        private IFlagRegisterField conversionComplete;

        private readonly IFlagRegisterField[] conversionCompleteInterruptEnable;
        private readonly IValueRegisterField[] inputChannel;
        private readonly IValueRegisterField[] cdata;
        private readonly SensorSamplesFifo<ScalarSample>[] samplesFifos;

        private const int NumberOfHardwareTriggers = 8;
        private const int NumberOfChannels = 16;

        private const int AdcEtcMask = 0b10000;
        private const int ConversionDisabledMask = 0b11111;

        private enum Registers
        {
            HardwareTriggersControl0 = 0x00,
            HardwareTriggersControl1 = 0x04,
            HardwareTriggersControl2 = 0x08,
            HardwareTriggersControl3 = 0x0C,
            HardwareTriggersControl4 = 0x10,
            HardwareTriggersControl5 = 0x14,
            HardwareTriggersControl6 = 0x18,
            HardwareTriggersControl7 = 0x1C,

            HardwareTriggersStatus = 0x20,

            HardwareTriggersDataResult0 = 0x24,
            HardwareTriggersDataResult1 = 0x28,
            HardwareTriggersDataResult2 = 0x2C,
            HardwareTriggersDataResult3 = 0x30,
            HardwareTriggersDataResult4 = 0x34,
            HardwareTriggersDataResult5 = 0x38,
            HardwareTriggersDataResult6 = 0x3C,
            HardwareTriggersDataResult7 = 0x40,

            Configuration = 0x44,
            GeneralControl = 0x48,
            GeneralStatus = 0x4C,
            CompareValue = 0x50,
            OffsetCorrectionValue = 0x54,
            CalibrationValue = 0x58,
        }
    }
}
