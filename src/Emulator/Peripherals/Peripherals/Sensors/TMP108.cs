//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

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
    public class TMP108 : II2CPeripheral, ITemperatureSensor, IDisposable, IUnderstandRESD
    {
        public TMP108(IMachine machine)
        {
            registers = new WordRegisterCollection(this);
            DefineRegisters();
            UpdateInterrupts();

            continuousConversionThread = machine.ObtainManagedThread(
                UpdateTemperature,
                ContinuousConversionPeriod,
                $"{nameof(TMP108)} continuous conversion",
                this,
                () => mode.Value is not (Mode.ContinuousConversion or Mode.ContinuousConversion2)
            );
            continuousConversionThread.Start();
            // TMP108 reads its first sample at bootup, emulate this by
            // scheduling the first update to happen after sync state. We can't
            // just call `UpdateTemperature` here since the user will set the
            // correct `Temperature` after this constructor finishes
            machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => UpdateTemperature());
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.NoisyLog("Write with no data. Ignoring.");
                return;
            }

            registerAddress = (Registers)((data[0] % 4) * 2);

            var count = data.Length - 1;

            if(count > MaxAccessSize)
            {
                this.WarningLog("A maximum of {0} bytes is allowed for a register write, ignoring {1} bytes", 2, count - 2);
                count = MaxAccessSize;
            }

            for(var idx = 0; idx < count; idx += 1)
            {
                registers.WriteWithOffset((byte)registerAddress, idx, data[idx + 1], msbFirst: true);
            }
        }

        public byte[] Read(int count)
        {
            if(count > MaxAccessSize)
            {
                this.WarningLog("A maximum of {0} bytes is allowed for a read, ignoring {1} bytes", MaxAccessSize, count - MaxAccessSize);
                count = MaxAccessSize;
            }
            var result = new byte[count];
            for(var idx = 0; idx < count; idx += 1)
            {
                result[idx] = registers.ReadWithOffset((byte)registerAddress, idx, msbFirst: true);
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = default(Registers);
        }

        public void Reset()
        {
            registers.Reset();
            FinishTransmission();
            defaultTemperature = 0;
            temperatureLatched = 0;
        }

        public void Dispose()
        {
            continuousConversionThread.Dispose();
            temperatureStream?.Dispose();
        }

        public void FeedTemperatureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            temperatureStream?.Dispose();
            temperatureStream = this.CreateRESDStream<TemperatureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
            this.NoisyLog("RESD stream set to {0}", filePath);
        }

        public decimal Temperature
        {
            get
            {
                if(temperatureStream == null)
                {
                    return defaultTemperature;
                }
                temperatureStream.TryGetCurrentSample(this, out var sample, out var _);
                if(sample == null)
                {
                    this.NoisyLog("Failed to get sample value, using default");
                    return defaultTemperature;
                }
                return sample.Temperature / 1e3m;
            }

            set
            {
                if(temperatureStream != null)
                {
                    throw new RecoverableException("Cannot set sensor value when using RESD stream");
                }
                if(value > MaxPossibleTemperature)
                {
                    value = MaxPossibleTemperature;
                    this.WarningLog("{0} is higher than maximum of {1} and has been clamped", value, MaxPossibleTemperature);
                }
                else if(value < MinPossibleTemperature)
                {
                    value = MinPossibleTemperature;
                    this.WarningLog("{0} is lower than minimum of {1} and has been clamped", value, MinPossibleTemperature);
                }
                defaultTemperature = value;
            }
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        protected virtual int MaxAccessSize => 2;

        protected virtual bool HysteresisEnabled => true;

        private static short SignExtend12Bit(short num) => (short)(((num & 0x0800) != 0 ? 0xf000 : 0x0000) | (num & 0x0fff));

        private static byte HysteresysToDegrees(Hysteresis hysteresis) => hysteresis switch
        {
            Hysteresis._0 => 0,
            Hysteresis._1 => 1,
            Hysteresis._2 => 2,
            Hysteresis._4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(hysteresis))
        };

        private bool CompareWatchdogValues(short lesserValue, short greaterValue, bool currentState)
        {
            if(thermostatFlag.Value)
            {
                return currentState || lesserValue < greaterValue;
            }
            if(lesserValue < greaterValue)
            {
                return true;
            }
            if(lesserValue >= greaterValue + HysteresisInUnits)
            {
                return false;
            }
            return currentState;
        }

        private void UpdateInterrupts()
        {
            var value = TemperatureLatchedInUnits;
            temperatureLowWatchdog.Value = TemperatureLow;
            temperatureHighWatchdog.Value = TemperatureHigh;
            var state = (temperatureLowWatchdog.Value || temperatureHighWatchdog.Value) == polarityFlag.Value;
            if(state != IRQ.IsSet)
            {
                this.DebugLog("IRQ set to {0}", state);
            }
            IRQ.Set(state);
        }

        private void DefineRegisters()
        {
            Registers.Temperature.Define(registers)
                .WithReservedBits(0, 4)
                .WithValueField(4, 12, FieldMode.Read, valueProviderCallback: _ => (ulong)TemperatureLatchedInUnits, name: "Temperature");

            Registers.Configuration.Define(registers, 0x2210)
                .WithReservedBits(0, 4)
                .WithEnumField<WordRegister, Hysteresis>(4, 2, out hysteresis, name: "Hysteresis Control (HYS)")
                .WithReservedBits(6, 1)
                .WithFlag(7, out polarityFlag, changeCallback: (_, __) => UpdateInterrupts(), name: "Polarity (POL)")
                .WithEnumField<WordRegister, Mode>(8, 2, out mode, name: "Mode (M)",
                    changeCallback: (_, value) =>
                    {
                        switch(value)
                        {
                        case Mode.OneShot:
                            mode.Value = Mode.Shutdown;
                            UpdateTemperature();
                            break;
                        case Mode.ContinuousConversion:
                        case Mode.ContinuousConversion2:
                            continuousConversionThread.Start();
                            break;
                        default:
                            break;
                        }
                    }
                )
                .WithFlag(10, out thermostatFlag, name: "Thermostat mode (TM)")
                .WithFlag(11, out temperatureLowWatchdog, FieldMode.ReadToClear | FieldMode.Write, name: "Temperature low watchdog (FL)")
                .WithFlag(12, out temperatureHighWatchdog, FieldMode.ReadToClear | FieldMode.Write, name: "Temperature high watchdog (FH)")
                .WithValueField(13, 2, out convertionRate, changeCallback: (_, __) => continuousConversionThread.Period = ContinuousConversionPeriod, name: "Conversion Rate (CR)")
                .WithTaggedFlag("ID", 15);

            Registers.TemperatureLow.Define(registers, 0x80_00)
                .WithReservedBits(0, 4)
                .WithValueField(4, 12, out temperatureLowThreshold, changeCallback: (_, __) => UpdateInterrupts(), name: "Temperature threshold low (T_low)");

            Registers.TemperatureHigh.Define(registers, 0x7F_F0)
                .WithReservedBits(0, 4)
                .WithValueField(4, 12, out temperatureHighThreshold, changeCallback: (_, __) => UpdateInterrupts(), name: "Temperature threshold high (T_high)");
        }

        private void UpdateTemperature()
        {
            temperatureLatched = Temperature;
            UpdateInterrupts();
        }

        private short TemperatureLatchedInUnits => (short)(temperatureLatched * UnitsPerDegreeCelsius);

        private TimeInterval ContinuousConversionPeriod => TimeInterval.FromMicroseconds((ulong)(4_000_000 >> (int)(2 * convertionRate.Value)));

        private short HysteresisInUnits
        {
            get
            {
                if(!HysteresisEnabled)
                {
                    return 0;
                }

                return (short)(HysteresysToDegrees(hysteresis.Value) * UnitsPerDegreeCelsius);
            }
        }

        private bool TemperatureLow => CompareWatchdogValues(TemperatureLatchedInUnits, SignExtend12Bit((short)temperatureLowThreshold.Value), temperatureLowWatchdog.Value);

        private bool TemperatureHigh => CompareWatchdogValues(SignExtend12Bit((short)temperatureHighThreshold.Value), TemperatureLatchedInUnits, temperatureHighWatchdog.Value);

        private RESDStream<TemperatureSample> temperatureStream;

        private decimal defaultTemperature;
        private decimal temperatureLatched;

        private Registers registerAddress;

        private IFlagRegisterField thermostatFlag;
        private IFlagRegisterField temperatureLowWatchdog;
        private IFlagRegisterField temperatureHighWatchdog;
        private IFlagRegisterField polarityFlag;
        private IValueRegisterField convertionRate;
        private IValueRegisterField temperatureLowThreshold;
        private IValueRegisterField temperatureHighThreshold;
        private IEnumRegisterField<Hysteresis> hysteresis;
        private IEnumRegisterField<Mode> mode;

        private readonly WordRegisterCollection registers;
        private readonly IManagedThread continuousConversionThread;

        private const decimal MinPossibleTemperature = -40;
        private const decimal MaxPossibleTemperature = 125;

        private const byte UnitsPerDegreeCelsius = 16;

        private enum Mode
        {
            Shutdown = 0x0,
            OneShot = 0x1,
            ContinuousConversion = 0x2,
            ContinuousConversion2 = 0x3,
        }

        private enum Hysteresis : byte
        {
            _0 = 0,
            _1 = 1,
            _2 = 2,
            _4 = 3
        }

        private enum Registers : byte
        {
            Temperature = 0x0,
            Configuration = 0x2,
            TemperatureLow = 0x4,
            TemperatureHigh = 0x6,
        }
    }
}
