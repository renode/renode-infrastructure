//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class VEML7700 : II2CPeripheral, ISensor, IIlluminanceSensor, IProvidesRegisterCollection<WordRegisterCollection>, IUnderstandRESD
    {
        public VEML7700()
        {
            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public byte[] Read(int count)
        {
            if(addressToRead == -1)
            {
                this.WarningLog("Tried to read without addressing");
                return new byte[] { };
            }
            var result = BitHelper.GetBytesFromValue(
                RegistersCollection.Read(addressToRead),
                sizeof(ushort),
                true
            );
            addressToRead = -1;
            return result;
        }

        public void Write(byte[] data)
        {
            if(data.Length == 1)
            {
                addressToRead = data[0];
                return;
            }
            addressToRead = -1;
            if(data.Length != 3)
            {
                this.WarningLog("Written {0} bytes when expecting 3", data.Length);
                if(data.Length == 0) return;
            }
            var value = BitHelper.ToUInt16(data, 0, true);
            RegistersCollection.Write(data[0], value);
        }

        public void FinishTransmission()
        {
            addressToRead = -1;
        }

        public void Reset()
        {
            FinishTransmission();
            RegistersCollection.Reset();
        }

        public void FeedIlluminanceSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            illuminanceStream?.Dispose();
            illuminanceStream = this.CreateRESDStream<IlluminanceSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
            this.Log(LogLevel.Noisy, "RESD stream set to {0}", filePath);
        }

        public WordRegisterCollection RegistersCollection { get; }

        public decimal Illuminance
        {
            get
            {
                if(illuminanceStream == null)
                {
                    return illuminance;
                }
                illuminanceStream.TryGetCurrentSample(this, out var sample, out var _);
                if(sample == null)
                {
                    this.WarningLog("Failed to get sample value, using default");
                    return 0;
                }
                return sample.Illuminance / 1e3m;
            }

            set
            {
                if(illuminanceStream == null)
                {
                    illuminance = value;
                    return;
                }
                throw new RecoverableException("Cannot set sensor value when using RESD stream");
            }
        }

        // VEML7700 supports a "white" channel which has a completely different response curve compared to normal illuminance
        // There's no documentation as to how gain and integration time affects whiteness, so let's assume it follows the same numbers as plain illuminance
        public decimal Whiteness { get; set; }

        // Whether to report an alternate device ID on command 7
        public bool AlternateDeviceId { get; set; }

        private void DefineRegisters()
        {
            Registers.Configuration.Define(this, 1)
                .WithFlag(0, out shutdownRegister, changeCallback: (_, v) =>
                {
                    if(v)
                    {
                        illuminanceLatched = Illuminance;
                        whitenessLatched = Whiteness;
                    }
                }, name: "Shutdown (ALS_SD)")
                .WithFlag(1, out interruptRegister, name: "Interrupt register enable (ALS_INT_EN)")
                .WithReservedBits(2, 2)
                .WithTag("Interrupt persistence (ALS_PERS)", 4, 2)
                .WithEnumField<WordRegister, IntegrationTime>(6, 4, out integrationTimeRegister, name: "Integration time (ALS_IT)")
                .WithReservedBits(10, 1)
                .WithEnumField<WordRegister, Gain>(11, 2, out gainRegister, name: "Gain (ALS_GAIN)")
                .WithReservedBits(13, 2);

            Registers.ThresholdHigh.Define(this)
                .WithValueField(0, 16, out highThresholdRegister, name: "High threshold (ALS_WH)");

            Registers.ThresholdLow.Define(this)
                .WithValueField(0, 16, out lowThresholdRegister, name: "Low threshold (ALS_WL)");

            Registers.PowerSaving.Define(this)
                .WithTaggedFlag("Power saving enable (PSM_EN)", 0)
                .WithTag("Power saving mode (PSM)", 1, 2)
                .WithReservedBits(3, 13);

            Registers.Illuminance.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, valueProviderCallback: _ => ScaleSample(IlluminanceLatched), name: "Illuminance (ALS)");

            Registers.Whiteness.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, valueProviderCallback: _ => ScaleSample(WhitenessLatched), name: "Whiteness (WHITE)");

            Registers.InterruptState.Define(this)
                .WithReservedBits(0, 14)
                .WithFlag(14, mode: FieldMode.Read, valueProviderCallback: _ => interruptRegister.Value && ScaleSample(IlluminanceLatched) > highThresholdRegister.Value, name: "Interrupt high triggered (int_th_high)")
                .WithFlag(15, mode: FieldMode.Read, valueProviderCallback: _ => interruptRegister.Value && ScaleSample(IlluminanceLatched) > lowThresholdRegister.Value, name: "Interrupt low trigerred (int_th_low)");

            Registers.ID.Define(this)
                .WithValueField(0, 8, mode: FieldMode.Read, valueProviderCallback: _ => DeviceID, name: "Device ID (ID)")
                .WithValueField(8, 8, mode: FieldMode.Read, valueProviderCallback: _ => AlternateDeviceId ? SlaveCodeAlt : SlaveCode, name: "Slave code (SLAVE_CODE)");
        }

        private ushort ScaleSample(decimal value)
        {
            var coefficient = luxCoefficients[(int)gainRegister.Value][(int)integrationTimeRegister.Value];
            var result = Math.Round(value / coefficient);
            return (ushort)Math.Min(result, ushort.MaxValue);
        }

        // Taken directly from Zephyr
        // https://github.com/zephyrproject-rtos/zephyr/blob/0214260fb5124ed6d810d177bc4ba13351826544/drivers/sensor/vishay/veml7700/veml7700.c#L69
        private static readonly decimal[][] luxCoefficients = new[] {
        	       /* 25ms    50ms    100ms   200ms   400ms   800ms  Integration Time */
        	new [] {0.2304m, 0.1152m, 0.0576m, 0.0288m, 0.0144m, 0.0072m}, /* Gain 1 */
        	new [] {0.1152m, 0.0576m, 0.0288m, 0.0144m, 0.0072m, 0.0036m}, /* Gain 2 */
        	new [] {1.8432m, 0.9216m, 0.4608m, 0.2304m, 0.1152m, 0.0576m}, /* Gain 1/8 */
        	new [] {0.9216m, 0.4608m, 0.2304m, 0.1152m, 0.0576m, 0.0288m}, /* Gain 1/4 */
        };

        private decimal IlluminanceLatched => shutdownRegister.Value ? illuminanceLatched : Illuminance;

        private decimal WhitenessLatched => shutdownRegister.Value ? whitenessLatched : Whiteness;

        private decimal illuminance;

        private decimal illuminanceLatched;
        private decimal whitenessLatched;

        private IFlagRegisterField shutdownRegister;
        private IFlagRegisterField interruptRegister;
        private IValueRegisterField lowThresholdRegister;
        private IValueRegisterField highThresholdRegister;
        private IEnumRegisterField<Gain> gainRegister;
        private IEnumRegisterField<IntegrationTime> integrationTimeRegister;

        private RESDStream<IlluminanceSample> illuminanceStream;

        private int addressToRead;

        private const byte SlaveCode = 0xc4;
        private const byte SlaveCodeAlt = 0xd4;
        private const byte DeviceID = 0x81;

        public enum Gain
        {
            Gain1 = 0,
            Gain2 = 1,
            Gain1_8 = 2, // 1/8
            Gain1_4 = 3  // 1/4
        }

        public enum IntegrationTime
        {
            Time25ms = 0b1100,
            Time50ms = 0b1000,
            Time100ms = 0b0000,
            Time200ms = 0b0001,
            Time400ms = 0b0010,
            Time800ms = 0b0011
        }

        private enum Registers
        {
            Configuration = 0,  // ALS_CONF_0
            ThresholdHigh = 1,  // ALS_WH
            ThresholdLow = 2,   // ALS_WL
            PowerSaving = 3,
            Illuminance = 4,    // ALS
            Whiteness = 5,      // WHITE
            InterruptState = 6, // ALS_INT
            ID = 7
        }
    }
}
