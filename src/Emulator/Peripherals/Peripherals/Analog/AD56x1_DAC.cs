//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class AD56x1_DAC : IFloatingVoltageSource, ISPIPeripheral
    {
        public AD56x1_DAC(double referenceVoltage, byte resolution)
        {
            if(!(resolution == 8 || resolution == 10 || resolution == 12))
            {
                // Only 8, 10, and 12-bit versions exsist
                throw new ConstructionException($"Invalid resolution {resolution} provided, valid values are 8, 10, and 12");
            }
            if(referenceVoltage < 2.7 || referenceVoltage > 5.5)
            {
                throw new ConstructionException($"Invalid reference voltage {referenceVoltage} provided. Value must be in the range [2.7, 5.5] volt");
            }
            Max = (uint)(referenceVoltage * 1e6);
            buffer = new byte[RegisterSize];
            Reset();
            Resolution = resolution;
        }

        public void Reset()
        {
            // Output is always 0V after power-up
            buffer[0] = 0;
            buffer[1] = 0;
            index = 0;
            Sample = new VoltageSample(0);
            IsFloating = false;
            NewSample?.Invoke(Sample);
        }

        public byte Transmit(byte data)
        {
            if(index >= RegisterSize)
            {
                this.WarningLog("More than 2 bytes ({0}) sent in a single transaction, ignoring extra writes", index);
            }
            buffer[index] = data;
            index++;
            // The device does not have a output line, so always return 0
            return 0;
        }

        public void FinishTransmission()
        {
            if(index < RegisterSize)
            {
                this.WarningLog("Recieved only {0} bytes, 2 expected. Output not updated", index);
                index = 0;
                return;
            }
            // The 2 MSB of the first byte encodes the mode
            var mode = (Mode)BitHelper.GetValue(buffer[0], 6, 2);
            if(mode == Mode.Normal)
            {
                // The register value is split across the two bytes, 6 MSB are in the first byte, the rest in the second byte
                uint reg = 0;
                reg |= (uint)(BitHelper.GetValue(buffer[0], 0, 6) << 2);
                var remaningBits = Resolution - 6;
                reg |= (uint)BitHelper.GetValue(buffer[1], 8 - remaningBits, remaningBits);
                this.DebugLog("Register value: 0x{0:X}", reg);

                // Datasheet specifies V_out = V_dd * (reg / 2**n) where n is the resolution
                // Note that this means that V_out can never be = to V_dd, as max reg value is (2**n)-1
                var output = (uint)(Max * ((double)reg / (Math.Pow(2, Resolution))));
                Sample = new VoltageSample(output);
                IsFloating = false;
                this.DebugLog("Analog output: {0} ({1} μV)", Sample, Sample.Voltage);
            }
            else
            {
                // In Renode all the power down modes are treated as 0V out
                this.DebugLog("Power down mode, setting output to 0");
                Sample = new VoltageSample(0);
                // In other modes DAC is connected through a resistor to ground
                IsFloating = mode == Mode.ThreeState;
            }
            index = 0;
            NewSample?.Invoke(Sample);
        }

        /// <summary>
        /// This is a workaround for properties with RESDSample types are not visible in monitor
        /// </summary>
        public VoltageSample Voltage()
        {
            return Sample;
        }

        public VoltageSample Sample { get; private set; }

        public bool IsFloating { get; private set; }

        public event Action<VoltageSample> NewSample;

        // The supply voltage, which is used as the max output value, in microvolts

        private uint Max { get; }

        private byte Resolution { get; }

        private uint index;
        private readonly byte[] buffer;
        private const int RegisterSize = 2;

        private enum Mode
        {
            Normal = 0,
            OneKToGnd = 0b01,
            OneHundredKToGnd = 0b10,
            ThreeState = 0b11,
        }
    }
}
