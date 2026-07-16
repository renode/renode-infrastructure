//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class AD4011_ADC : ISPIPeripheral, IADC
    {
        public AD4011_ADC(IMachine machine, decimal referenceVoltage)
        {
            if(referenceVoltage < ReferenceVoltageMin || referenceVoltage > ReferenceVoltageMax)
            {
                throw new ConstructionException($"{nameof(referenceVoltage)} ({referenceVoltage}V) is out of range of [{ReferenceVoltageMin}V; {ReferenceVoltageMax}V]");
            }
            ReferenceVoltage = referenceVoltage;
            toTransmit = new Queue<byte>();

            ADCContainer = new SimpleContainerHelper<IRESDSampleSource<VoltageSample>>(machine, this);
            configRegister = new ByteRegister(this, 0x1)
                .WithFlag(0, out overvoltageClamp, FieldMode.ReadToSet, name: "OV clamp")
                .WithTaggedFlag("Turbo mode enable", 1)
                .WithTaggedFlag("High-Z model enable", 2)
                .WithFlag(3, out spanCompression, name: "Span compression enable")
                .WithFlag(4, out statusBitsEnable, name: "Status bits enable")
                .WithReservedBits(5, 3);

            Reset();
        }

        public void Reset()
        {
            FinishTransmission();
            configRegister.Reset();
        }

        public byte Transmit(byte data)
        {
            switch(currentState)
            {
            case State.Idle:
                if((data & ConfigAccessPattern) == ConfigAccessPattern)
                {
                    ChangeState(BitHelper.IsBitSet(data, ConfigReadWriteBit) ? State.ReadConfig : State.WriteConfig);
                    return 0x0;
                }

                EnqueueMeasurement();
                ChangeState(State.TransmitData);
                goto case State.TransmitData;
            case State.ReadConfig:
                ChangeState(State.Idle);
                return configRegister.Read();
            case State.WriteConfig:
                ChangeState(State.Idle);
                configRegister.Write(0x0, data);
                return 0x0;
            case State.TransmitData:
                toTransmit.TryDequeue(out var b);
                if(toTransmit.Count == 0)
                {
                    ChangeState(State.Idle);
                }
                return b;
            default:
                throw new UnreachableException();
            }
        }

        public void FinishTransmission()
        {
            ChangeState(State.Idle);
            toTransmit.Clear();
        }

        public void SetADCValue(int channel, uint value)
        {
            throw new RecoverableException("SetADCValue is deprecated and should not be used in new models. Use a ADCChannelSource instead");
        }

        public uint GetADCValue(int channel)
        {
            throw new RecoverableException("GetADCValue is deprecated and should not be used in new models. Use a ADCChannelSource instead");
        }

        public decimal ReferenceVoltage
        {
            get => referenceVoltage;
            set
            {
                if(value < ReferenceVoltageMin || value > ReferenceVoltageMax)
                {
                    throw new RecoverableException($"Reference voltage ({value}V) is out of range of [{ReferenceVoltageMin}V; {ReferenceVoltageMax}V]");
                }
                referenceVoltage = value;
            }
        }

        public int ADCChannelCount => 1;

        public SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; }

        private uint GetConvertedSample()
        {
            if(!ADCContainer.TryGetByAddress(0, out var provider))
            {
                this.WarningLog("Could not find a voltage provider, falling back to 0V");
                return 0x0;
            }

            return Convert(provider.Sample.Voltage / 1e6m);
        }

        private uint Convert(decimal input)
        {
            // With span compression enabled, the maximum value is 0.8 * V_ref
            var maxVolts = ReferenceVoltage * (spanCompression.Value ? 0.8m : 1m);
            var lsb = maxVolts / (1 << (MeasurementResolutionBits - 1));

            var diff = input - ReferenceVoltage;
            if(Math.Abs(diff) > maxVolts)
            {
                overvoltageClamp.Value = false; // This is active low
                diff = Math.Sign(diff) * maxVolts;
            }

            var result = (uint)((int)(diff / lsb)) & ((1u << MeasurementResolutionBits) - 1);

            // Since the output is in 2's complement it is impossible to encode +V_ref
            if(result > MaxRawPositiveMeasurement && Math.Sign(diff) > 0)
            {
                result = MaxRawPositiveMeasurement;
            }
            return result;
        }

        private void EnqueueMeasurement()
        {
            var measurement = GetConvertedSample();
            byte[] bytes;
            int toSend;
            if(statusBitsEnable.Value)
            {
                measurement <<= StatusBitCount;
                BitHelper.SetBit(ref measurement, 5, overvoltageClamp.Value);
                BitHelper.SetBit(ref measurement, 4, spanCompression.Value);
                // Read to trigger side-effects (e.g. clear the OV flag)
                configRegister.Read();

                toSend = (MeasurementResolutionBits + StatusBitCount).DivCeil(8);
                bytes = BitConverter.GetBytes(measurement);
            }
            else
            {
                toSend = MeasurementResolutionBits.DivCeil(8);
                bytes = BitConverter.GetBytes(measurement);
            }

            // Bytes are transmitted MSB first
            if(BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            toTransmit.EnqueueRange(bytes.Skip(bytes.Length - toSend).Take(toSend));
        }

        private void ChangeState(State to)
        {
            if(to != currentState)
            {
                this.DebugLog("State changed: {0} -> {1}", currentState, to);
                currentState = to;
            }
        }

        private State currentState;
        private decimal referenceVoltage;

        private readonly Queue<byte> toTransmit;
        private readonly ByteRegister configRegister;
        private readonly IFlagRegisterField overvoltageClamp;
        private readonly IFlagRegisterField spanCompression;
        private readonly IFlagRegisterField statusBitsEnable;

        private const int MeasurementResolutionBits = 18;
        private const uint MaxRawPositiveMeasurement = (1u << (MeasurementResolutionBits - 1)) - 1;
        private const byte StatusBitCount = 6;

        private const byte ConfigAccessPattern = 0b00010100;
        private const byte ConfigReadWriteBit = 6;
        private const decimal ReferenceVoltageMin = 2.4m;
        private const decimal ReferenceVoltageMax = 5.1m;

        private enum State
        {
            Idle,
            ReadConfig,
            WriteConfig,
            TransmitData,
        }
    }
}
