//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class INA228 : II2CPeripheral, ISensor, ITemperatureSensor, IUnderstandRESD, IProvidesRegisterCollection<MultibyteRegisterCollection>
    {
        public INA228(IMachine machine)
        {
            this.machine = machine;
            RegistersCollection = new MultibyteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            registerAddress = null;
            writeOffset = null;
            readOffset = 0;
            RegistersCollection.Reset();
            ResetAccumulators();
            UpdateFeederThreadPeriod();
        }

        public void FeedShuntVoltageSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            shuntVoltageStream = this.CreateRESDStream<VoltageSample, decimal>(filePath, channelId, s => s.Voltage / 1e6m, sampleOffsetType, sampleOffsetTime);
            StartAdcSamplingThread();
            this.Log(LogLevel.Noisy, "Shunt voltage RESD stream set to {0}", filePath);
        }

        public void FeedBusVoltageSamplesFromRESD(ReadFilePath filePath, uint channelId = 1,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            busVoltageStream = this.CreateRESDStream<VoltageSample, decimal>(filePath, channelId, s => s.Voltage / 1e6m, sampleOffsetType, sampleOffsetTime);
            StartAdcSamplingThread();
            this.Log(LogLevel.Noisy, "Bus voltage RESD stream set to {0}", filePath);
        }

        public void FeedTemperatureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            temperatureStream = this.CreateRESDStream<TemperatureSample, decimal>(filePath, channelId, s => s.Temperature / 1e3m, sampleOffsetType, sampleOffsetTime);
            StartAdcSamplingThread();
            this.Log(LogLevel.Noisy, "Temperature RESD stream set to {0}", filePath);
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Write with no data, ignoring");
                return;
            }

            if(!writeOffset.HasValue)
            {
                registerAddress = data[0];
                writeOffset = 0;
                readOffset = 0;
                data = data.Skip(1).ToArray();
            }

            RegistersCollection.WriteWithOffset(registerAddress.Value, writeOffset.Value, data);
            writeOffset += data.Length;
        }

        public byte[] Read(int count = 1)
        {
            FinishWriteTransfer();

            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Warning, "Read attempted before any register pointer was set");
                return Array.Empty<byte>();
            }

            var result = RegistersCollection.ReadWithOffset(registerAddress.Value, readOffset, count);
            readOffset += result.Length;
            return result;
        }

        public void FinishTransmission()
        {
            FinishWriteTransfer();
            readOffset = 0;
        }

        public decimal ShuntVoltage
        {
            get => shuntVoltage;
            set
            {
                if(shuntVoltageStream != null)
                {
                    throw new RecoverableException("Cannot set sensor value while a RESD stream is feeding it");
                }
                AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                shuntVoltage = value;
            }
        }

        public decimal BusVoltage
        {
            get => busVoltage;
            set
            {
                if(busVoltageStream != null)
                {
                    throw new RecoverableException("Cannot set sensor value while a RESD stream is feeding it");
                }
                AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                busVoltage = value;
            }
        }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(temperatureStream != null)
                {
                    throw new RecoverableException("Cannot set sensor value while a RESD stream is feeding it");
                }
                AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                temperature = value;
            }
        }

        public MultibyteRegisterCollection RegistersCollection { get; }

        private static decimal WrapToRange(decimal value, decimal min, decimal max)
        {
            var range = max - min + 1m;
            var wrapped = (value - min) % range;
            if(wrapped < 0m)
            {
                wrapped += range;
            }
            return wrapped + min;
        }

        private static long ClampToSigned(decimal counts, int bits)
        {
            var min = (decimal)BitHelper.MinSignedValue(bits);
            var max = (decimal)BitHelper.MaxSignedValue(bits);
            return (long)System.Math.Round(counts.Clamp(min, max));
        }

        private static ulong ClampToUnsigned(decimal counts, int bits)
        {
            var max = (decimal)BitHelper.MaxUnsignedValue(bits);
            return (ulong)System.Math.Round(counts.Clamp(0m, max));
        }

        private void StartAdcSamplingThread()
        {
            if(adcSamplingThread != null)
            {
                return;
            }
            adcSamplingThread = machine.ObtainManagedThread(SampleAllChannels, ComputeContinuousFeedPeriod(), "INA228 ADC sampling thread", this);
            adcSamplingThread.Start();
        }

        private void SampleAllChannels()
        {
            if(shuntVoltageStream != null)
            {
                var status = shuntVoltageStream.TryGetCurrentSample(this, out var shunt, out var shuntTimestamp);
                if(status != RESDStreamStatus.BeforeStream)
                {
                    AccumulateElapsedInterval(shuntTimestamp);
                    shuntVoltage = shunt;
                }
                if(status == RESDStreamStatus.AfterStream)
                {
                    shuntVoltageStream.Dispose();
                    shuntVoltageStream = null;
                }
            }
            if(busVoltageStream != null)
            {
                var status = busVoltageStream.TryGetCurrentSample(this, out var bus, out var busTimestamp);
                if(status != RESDStreamStatus.BeforeStream)
                {
                    AccumulateElapsedInterval(busTimestamp);
                    busVoltage = bus;
                }
                if(status == RESDStreamStatus.AfterStream)
                {
                    busVoltageStream.Dispose();
                    busVoltageStream = null;
                }
            }
            if(temperatureStream != null)
            {
                var status = temperatureStream.TryGetCurrentSample(this, out var temp, out var _);
                if(status != RESDStreamStatus.BeforeStream)
                {
                    temperature = temp;
                }
                if(status == RESDStreamStatus.AfterStream)
                {
                    temperatureStream.Dispose();
                    temperatureStream = null;
                }
            }
        }

        private void UpdateFeederThreadPeriod()
        {
            if(adcSamplingThread != null)
            {
                adcSamplingThread.Period = ComputeContinuousFeedPeriod();
            }
        }

        private TimeInterval ComputeContinuousFeedPeriod()
        {
            var mode = operatingMode.Value;
            var busTime = ConversionTimeMicroseconds[(int)busConversionTime.Value];
            var shuntTime = ConversionTimeMicroseconds[(int)shuntConversionTime.Value];
            var tempTime = ConversionTimeMicroseconds[(int)temperatureConversionTime.Value];
            var avgMultiplier = AveragingCounts[(int)averagingCount.Value];

            var busEnabled = mode.HasFlag(AdcMode.Bus);
            var shuntEnabled = mode.HasFlag(AdcMode.Shunt);
            var tempEnabled = mode.HasFlag(AdcMode.Temperature);
            var isContinuous = mode.HasFlag(AdcMode.Continuous);

            var totalMicroseconds = (busEnabled ? busTime : 0) + (shuntEnabled ? shuntTime : 0) + (tempEnabled ? tempTime : 0);

            if(!isContinuous || totalMicroseconds == 0)
            {
                totalMicroseconds = busTime + shuntTime + tempTime;
            }

            totalMicroseconds *= avgMultiplier;
            return TimeInterval.FromMicroseconds((ulong)totalMicroseconds);
        }

        private void FinishWriteTransfer()
        {
            writeOffset = null;
        }

        private void AccumulateElapsedInterval(TimeInterval now)
        {
            var elapsedSeconds = (decimal)System.Math.Max(0.0, (now - lastAccumulationTime).TotalSeconds);
            accumulatedEnergyCounts += GetPowerCounts() * elapsedSeconds / EnergyLsbDivisor;
            accumulatedChargeCounts += GetCurrentCounts() * elapsedSeconds;

            var energyMax = (decimal)BitHelper.MaxUnsignedValue(EnergyChargeBits);
            if(accumulatedEnergyCounts > energyMax || accumulatedEnergyCounts < 0m)
            {
                energyOverflowed = true;
                accumulatedEnergyCounts = WrapToRange(accumulatedEnergyCounts, 0m, energyMax);
            }

            var chargeMin = (decimal)BitHelper.MinSignedValue(EnergyChargeBits);
            var chargeMax = (decimal)BitHelper.MaxSignedValue(EnergyChargeBits);
            if(accumulatedChargeCounts > chargeMax || accumulatedChargeCounts < chargeMin)
            {
                chargeOverflowed = true;
                accumulatedChargeCounts = WrapToRange(accumulatedChargeCounts, chargeMin, chargeMax);
            }

            lastAccumulationTime = now;
        }

        private void ResetAccumulators()
        {
            accumulatedEnergyCounts = 0m;
            accumulatedChargeCounts = 0m;
            energyOverflowed = false;
            chargeOverflowed = false;
            lastAccumulationTime = machine.LocalTimeSource.ElapsedVirtualTime;
        }

        private long GetVShuntCounts()
        {
            return ClampToSigned(ShuntVoltage / ShuntVoltageLsb, 20);
        }

        private long GetVBusCounts()
        {
            return ClampToSigned(BusVoltage / BusVoltageLsb, 20);
        }

        private long GetDieTempCounts()
        {
            return ClampToSigned(Temperature / TemperatureLsb, 16);
        }

        private long GetCurrentCounts()
        {
            if(shuntCalibration.Value == 0)
            {
                return 0;
            }
            var adcRangeMultiplier = adcRange.Value ? 4m : 1m;
            var counts = ShuntVoltage * ShuntCalScalingConstant * adcRangeMultiplier / shuntCalibration.Value;
            if(temperatureCompensation.Value)
            {
                var tempCoPpm = (decimal)shuntTemperatureCoefficient.Value;
                counts /= 1m + (Temperature - 25m) * tempCoPpm * 1e-6m;
            }
            return ClampToSigned(counts, 20);
        }

        private long GetPowerCounts()
        {
            var counts = GetCurrentCounts() * BusVoltage / PowerLsbScalingConstant;
            return (long)ClampToUnsigned(counts, 24);
        }

        private void DefineRegisters()
        {
            Registers.Config.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithReservedBits(0, 4)
                        .WithFlag(4, out adcRange, name: "ADCRANGE")
                        .WithFlag(5, out temperatureCompensation, name: "TEMPCOMP")
                        .WithTag("CONVDLY", 6, 8)
                        .WithFlag(14, FieldMode.Write, writeCallback: (_, value) => { if(value) ResetAccumulators(); }, name: "RSTACC")
                        .WithFlag(15, FieldMode.Write, writeCallback: (_, value) => { if(value) Reset(); }, name: "RST"))
            ;

            Registers.ADCConfig.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 3, out averagingCount, name: "AVG")
                        .WithValueField(3, 3, out temperatureConversionTime, name: "VTCT")
                        .WithValueField(6, 3, out shuntConversionTime, name: "VSHCT")
                        .WithValueField(9, 3, out busConversionTime, name: "VBUSCT")
                        .WithEnumField(12, 4, out operatingMode, name: "MODE")
                        .WithWriteCallback((_, __) => UpdateFeederThreadPeriod()),
                    resetValue: 0xFB68)
            ;

            Registers.ShuntCalibration.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 15, out shuntCalibration, name: "SHUNT_CAL")
                        .WithReservedBits(15, 1),
                    resetValue: 0x1000)
            ;

            Registers.ShuntTemperatureCoefficient.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 14, out shuntTemperatureCoefficient, name: "TEMPCO")
                        .WithReservedBits(14, 2))
            ;

            Registers.ShuntVoltageMeasurement.DefineMultibyte(this, 3)
                .WithRegister<DoubleWordRegister>(0, register =>
                    register
                        .WithReservedBits(0, 4)
                        .WithValueField(4, 20, FieldMode.Read, valueProviderCallback: _ => BitHelper.SignTruncate(GetVShuntCounts(), 20), name: "VSHUNT"))
            ;

            Registers.BusVoltageMeasurement.DefineMultibyte(this, 3)
                .WithRegister<DoubleWordRegister>(0, register =>
                    register
                        .WithReservedBits(0, 4)
                        .WithValueField(4, 20, FieldMode.Read, valueProviderCallback: _ => BitHelper.SignTruncate(GetVBusCounts(), 20), name: "VBUS"))
            ;

            Registers.TemperatureMeasurement.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => BitHelper.SignTruncate(GetDieTempCounts(), 16), name: "DIETEMP"))
            ;

            Registers.CurrentResult.DefineMultibyte(this, 3)
                .WithRegister<DoubleWordRegister>(0, register =>
                    register
                        .WithReservedBits(0, 4)
                        .WithValueField(4, 20, FieldMode.Read, valueProviderCallback: _ => BitHelper.SignTruncate(GetCurrentCounts(), 20), name: "CURRENT"))
            ;

            Registers.PowerResult.DefineMultibyte(this, 3)
                .WithRegister<DoubleWordRegister>(0, register =>
                    register
                        .WithValueField(0, 24, FieldMode.Read, valueProviderCallback: _ => ClampToUnsigned(GetPowerCounts(), 24), name: "POWER"))
            ;

            Registers.EnergyResult.DefineMultibyte(this, 5)
                .WithRegister<QuadWordRegister>(0, register =>
                    register
                        .WithValueField(0, EnergyChargeBits, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                            energyOverflowed = false;
                            return (ulong)System.Math.Round(accumulatedEnergyCounts);
                        }, name: "ENERGY"))
            ;

            Registers.ChargeResult.DefineMultibyte(this, 5)
                .WithRegister<QuadWordRegister>(0, register =>
                    register
                        .WithValueField(0, EnergyChargeBits, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                            chargeOverflowed = false;
                            return BitHelper.SignTruncate((long)System.Math.Round(accumulatedChargeCounts), EnergyChargeBits);
                        }, name: "CHARGE"))
            ;

            Registers.DiagnosticFlagsAndAlert.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "MEMSTAT")
                        .WithTaggedFlag("CNVRF", 1)
                        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => GetPowerCounts() > (long)powerOverLimit.Value * PowerThresholdLsbRatio, name: "POL")
                        .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => GetVBusCounts() < (long)busUndervoltageLimit.Value * ThresholdLsbRatio, name: "BUSUL")
                        .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => GetVBusCounts() > (long)busOvervoltageLimit.Value * ThresholdLsbRatio, name: "BUSOL")
                        .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => GetVShuntCounts() < (short)shuntUndervoltageLimit.Value * ThresholdLsbRatio, name: "SHNTUL")
                        .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => GetVShuntCounts() > (short)shuntOvervoltageLimit.Value * ThresholdLsbRatio, name: "SHNTOL")
                        .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => GetDieTempCounts() > (short)temperatureOverLimit.Value, name: "TMPOL")
                        .WithReservedBits(8, 1)
                        .WithTaggedFlag("MATHOF", 9)
                        .WithFlag(10, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                            return chargeOverflowed;
                        }, name: "CHARGEOF")
                        .WithFlag(11, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            AccumulateElapsedInterval(machine.LocalTimeSource.ElapsedVirtualTime);
                            return energyOverflowed;
                        }, name: "ENERGYOF")
                        .WithFlag(12, name: "APOL")
                        .WithFlag(13, name: "SLOWALERT")
                        .WithFlag(14, name: "CNVR")
                        .WithFlag(15, name: "ALATCH"))
            ;

            Registers.ShuntOvervoltageThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, out shuntOvervoltageLimit, name: "SOVL"),
                    resetValue: 0x7FFF)
            ;

            Registers.ShuntUndervoltageThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, out shuntUndervoltageLimit, name: "SUVL"),
                    resetValue: 0x8000)
            ;

            Registers.BusOvervoltageThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 15, out busOvervoltageLimit, name: "BOVL")
                        .WithReservedBits(15, 1),
                    resetValue: 0x7FFF)
            ;

            Registers.BusUndervoltageThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 15, out busUndervoltageLimit, name: "BUVL")
                        .WithReservedBits(15, 1))
            ;

            Registers.TemperatureOverLimitThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, out temperatureOverLimit, name: "TEMP_LIMIT"),
                    resetValue: 0x7FFF)
            ;

            Registers.PowerOverLimitThreshold.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, out powerOverLimit, name: "PWR_LIMIT"),
                    resetValue: 0xFFFF)
            ;

            Registers.ManufacturerID.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 16, FieldMode.Read, name: "MANFID"),
                    resetValue: 0x5449)
            ;

            Registers.DeviceID.DefineMultibyte(this, 2)
                .WithRegister<WordRegister>(0, register =>
                    register
                        .WithValueField(0, 4, FieldMode.Read, name: "REV_ID")
                        .WithValueField(4, 12, FieldMode.Read, name: "DIEID"),
                    resetValue: 0x2281)
            ;
        }

        private decimal ShuntVoltageLsb => adcRange.Value ? ShuntLsbHighPrecision : ShuntLsbNormalRange;

        private decimal accumulatedChargeCounts;
        private bool energyOverflowed;
        private bool chargeOverflowed;
        private TimeInterval lastAccumulationTime;

        private decimal accumulatedEnergyCounts;
        private decimal busVoltage;
        private decimal temperature;

        private RESDStream<VoltageSample, decimal> shuntVoltageStream;
        private RESDStream<VoltageSample, decimal> busVoltageStream;
        private RESDStream<TemperatureSample, decimal> temperatureStream;
        private IManagedThread adcSamplingThread;

        private decimal shuntVoltage;
        private byte? registerAddress;
        private int? writeOffset;
        private int readOffset;

        private IValueRegisterField shuntCalibration;
        private IValueRegisterField shuntTemperatureCoefficient;
        private IValueRegisterField shuntOvervoltageLimit;
        private IValueRegisterField shuntUndervoltageLimit;
        private IValueRegisterField busOvervoltageLimit;
        private IValueRegisterField busUndervoltageLimit;
        private IValueRegisterField temperatureOverLimit;
        private IValueRegisterField powerOverLimit;

        private IValueRegisterField averagingCount;
        private IValueRegisterField temperatureConversionTime;
        private IValueRegisterField shuntConversionTime;
        private IValueRegisterField busConversionTime;
        private IEnumRegisterField<AdcMode> operatingMode;
        private IFlagRegisterField temperatureCompensation;
        private IFlagRegisterField adcRange;

        private readonly IMachine machine;
        private const long ThresholdLsbRatio = 16;
        private const int EnergyChargeBits = 40;
        private const decimal EnergyLsbDivisor = 16m;
        private const decimal PowerLsbScalingConstant = 3.2m;
        private const decimal BusVoltageLsb = 195.3125e-6m;
        private const decimal TemperatureLsb = 7.8125e-3m;
        private const decimal ShuntLsbHighPrecision = 78.125e-9m;

        private const decimal ShuntLsbNormalRange = 312.5e-9m;
        private const long PowerThresholdLsbRatio = 256;
        private const decimal ShuntCalScalingConstant = 13107.2e6m;

        [Flags]
        private enum AdcMode : byte
        {
            Bus = (1 << 0),
            Shunt = (1 << 1),
            Temperature = (1 << 2),
            Continuous = (1 << 3),
        }

        private static readonly int[] ConversionTimeMicroseconds = { 50, 84, 150, 280, 540, 1052, 2074, 4120 };
        private static readonly int[] AveragingCounts = { 1, 4, 16, 64, 128, 256, 512, 1024 };

        private enum Registers : byte
        {
            Config = 0x00,
            ADCConfig = 0x01,
            ShuntCalibration = 0x02,
            ShuntTemperatureCoefficient = 0x03,
            ShuntVoltageMeasurement = 0x04,
            BusVoltageMeasurement = 0x05,
            TemperatureMeasurement = 0x06,
            CurrentResult = 0x07,
            PowerResult = 0x08,
            EnergyResult = 0x09,
            ChargeResult = 0x0A,
            DiagnosticFlagsAndAlert = 0x0B,
            ShuntOvervoltageThreshold = 0x0C,
            ShuntUndervoltageThreshold = 0x0D,
            BusOvervoltageThreshold = 0x0E,
            BusUndervoltageThreshold = 0x0F,
            TemperatureOverLimitThreshold = 0x10,
            PowerOverLimitThreshold = 0x11,
            ManufacturerID = 0x3E,
            DeviceID = 0x3F,
        }
    }
}
