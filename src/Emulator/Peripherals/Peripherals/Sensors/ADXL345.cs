//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// uncomment the following line to get warnings about
// accessing unhandled registers; it's disabled by default
// as it generates a lot of noise in the log and might
// hide some important messages
// 
// #define WARN_UNHANDLED_REGISTERS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ADXL345 : II2CPeripheral
    {
        public ADXL345()
        {
            samplesFifo = new Queue<Sample>();

            MaxFifoDepth = 32;
        }

        public void Reset()
        {
            range = Range.G2;
            fullResolution = false;
            lastRegister = 0;
            bufferedSamples = null;
            currentSample = new Sample(0, 0, 0);

            lock(samplesFifo)
            {
                samplesFifoEmptied = null;
                samplesFifo.Clear();
            }
        }

        public void FinishTransmission()
        {
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            this.Log(LogLevel.Noisy, "Write with {0} bytes of data", data.Length);

            lastRegister = (Registers)data[0];
            this.Log(LogLevel.Noisy, "Setting register ID to 0x{0:X} - {0}", lastRegister);

            if(data.Length > 1)
            {
                this.Log(LogLevel.Noisy, "Handling register write");
                // skip the first byte as it contains register address
                foreach(var b in data.Skip(1))
                {
                    WriteCurrentRegister(b);
                    AdvanceRegister();
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Handling register read");
                if(lastRegister == Registers.Xdata0)
                {
                    lock(samplesFifo)
                    {
                        if(!samplesFifo.TryDequeue(out currentSample))
                        {
                            currentSample = new Sample(0, 0, 0);
                            this.Log(LogLevel.Warning, "Reading from Xdata0 register, but there are no samples");
                            return;
                        }

                        if(samplesFifo.Count == 0 && samplesFifoEmptied != null)
                        {
                            samplesFifoEmptied();
                        }
                    }
                }
            }
        }

        public void FeedSample(short x, short y, short z, int repeat = 1)
        {
            lock(samplesFifo)
            {
                if(repeat < 0)
                {
                    SamplesFifoEmptied = () => FeedSampleInner(x, y, z);
                }
                else
                {
                    FeedSampleInner(x, y, z, repeat);

                    SamplesFifoEmptied = null;
                }
            }
        }

        public void FeedSample(string path, int repeat = 1)
        {
            bufferedSamples = ParseSamplesFile(path);

            lock(samplesFifo)
            {
                if(repeat < 0)
                {
                    SamplesFifoEmptied = () => FeedSampleInner(bufferedSamples);
                }
                else
                {
                    FeedSampleInner(bufferedSamples, repeat);

                    bufferedSamples = null;
                    SamplesFifoEmptied = null;
                }
            }
        }

        public byte[] Read(int count = 1)
        {
            this.Log(LogLevel.Noisy, "Reading {0} bytes from register 0x{1:X} - {1}", count, lastRegister);

            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = ReadCurrentRegister();
                AdvanceRegister();
            }

            this.Log(LogLevel.Noisy, "Read result: {0}", Misc.PrettyPrintCollection(result));
            return result;
        }

        public int MaxFifoDepth { get; set; }

        private byte ReadCurrentRegister()
        {
            switch(lastRegister)
            {
                case Registers.DeviceID:
                    return DevID;
                case Registers.Xdata0:
                    return currentSample.X.GetLowByte(fullResolution, range);
                case Registers.Xdata1:
                    return currentSample.X.GetHighByte(fullResolution, range);
                case Registers.Ydata0:
                    return currentSample.Y.GetLowByte(fullResolution, range);
                case Registers.Ydata1:
                    return currentSample.Y.GetHighByte(fullResolution, range);
                case Registers.Zdata0:
                    return currentSample.Z.GetLowByte(fullResolution, range);
                case Registers.Zdata1:
                    return currentSample.Z.GetHighByte(fullResolution, range);
                case Registers.FifoStatus:
                    return (byte)Math.Min(samplesFifo.Count, MaxFifoDepth);

                default:
#if WARN_UNHANDLED_REGISTERS
                    // this generates a lot of noise in the logs so it's disabled by default
                    this.Log(LogLevel.Warning, "Reading from an unsupported or not-yet-implemented register: 0x{0:X} - {0}", lastRegister);
#endif
                    return 0;
            }
        }

        private void WriteCurrentRegister(byte value)
        {
            this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to register {1}", value, lastRegister);

            switch(lastRegister)
            {
                case Registers.DataFormatControl:
                    range = (Range)(value & 0x3);
                    fullResolution = (value >> 3) != 0;
                    break;

                default:
#if WARN_UNHANDLED_REGISTERS
                    // this generates a lot of noise in the logs so it's disabled by default
                    this.Log(LogLevel.Warning, "Writing to an unsupported or not-yet-implemented register: 0x{0:X} - {0}", lastRegister);
#endif
                    break;
            }
        }

        private void AdvanceRegister()
        {
            lastRegister = (Registers)((int)lastRegister + 1);
            this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", lastRegister);
        }

        private IEnumerable<Sample> ParseSamplesFile(string path)
        {
            var localQueue = new Queue<Sample>();
            var lineNumber = 0;

            try
            {
                using(var reader = File.OpenText(path))
                {
                    var line = "";
                    while((line = reader.ReadLine()) != null)
                    {
                        ++lineNumber;
                        var numbers = line.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToArray();

                        if(numbers.Length != 3
                                || !short.TryParse(numbers[0], out var x)
                                || !short.TryParse(numbers[1], out var y)
                                || !short.TryParse(numbers[2], out var z))
                        {
                            throw new RecoverableException($"Wrong data file format at line {lineNumber}: {line}");
                        }
                        localQueue.Enqueue(new Sample(x, y, z));
                    }
                }
            }
            catch(Exception e)
            {
                if(e is RecoverableException)
                {
                    throw;
                }

                throw new RecoverableException($"There was a problem when reading samples file: {e.Message}");
            }

            return localQueue;
        }

        private void FeedSampleInner(short x, short y, short z, int repeat = 1)
        {
            lock(samplesFifo)
            {
                var sample = new Sample(x, y, z);
                for(var i = 0; i < repeat; i++)
                {
                    samplesFifo.Enqueue(sample);
                }
            }
        }

        private void FeedSampleInner(IEnumerable<Sample> samples, int repeat = 1)
        {
            lock(samplesFifo)
            {
                for(var i = 0; i < repeat; i++)
                {
                    samplesFifo.EnqueueRange(samples);
                }
            }
        }

        private Action SamplesFifoEmptied
        {
            get => samplesFifoEmptied;

            set
            {
                lock(samplesFifo)
                {
                    samplesFifoEmptied = value;
                    if(samplesFifoEmptied != null && samplesFifo.Count == 0)
                    {
                        samplesFifoEmptied();
                    }
                }
            }
        }

        private Registers lastRegister;
        private Range range;
        private bool fullResolution;
        private IEnumerable<Sample> bufferedSamples;
        private Action samplesFifoEmptied;
        private Sample currentSample;

        private readonly Queue<Sample> samplesFifo;

        private const byte DevID = 0xe5;

        private struct Sample
        {
            public Sample(short x, short y, short z)
            {
                X = new SubSample(x);
                Y = new SubSample(y);
                Z = new SubSample(z);
            }

            public override string ToString()
            {
                return $"[X: {X.RawValue}, Y: {Y.RawValue}, Z: {Z.RawValue}]";
            }

            public SubSample X;
            public SubSample Y;
            public SubSample Z;
        }

        private struct SubSample
        {
            public SubSample(short v)
            {
                RawValue = v;
            }

            public byte GetLowByte(bool fullRes, Range range)
            {
                return (byte)(RawValue >> GetShifter(fullRes, range));
            }

            public byte GetHighByte(bool fullRes, Range range)
            {
                return (byte)(RawValue >> (GetShifter(fullRes, range) + 8));
            }

            public short RawValue;

            private int GetShifter(bool fullRes, Range range)
            {
                var shifter = 2;

                if(!fullRes)
                {
                    shifter = (int)range + 2;
                }

                return shifter;
            }
        }

        private enum Range
        {
            G2 = 0,
            G4 = 1,
            G8 = 2,
            G16 = 3
        }

        private enum Registers : byte
        {
            DeviceID = 0x00,
            // 0x01 to 0x1C are reserved
            TapThreshold = 0x1D,
            Xoffset = 0x1E,
            Yoffset = 0x1F,
            Zoffset = 0x20,
            TapDuration = 0x21,
            TapLatency = 0x22,
            TapWindow = 0x23,
            ActivityThreshold = 0x24,
            InactivityThreshold = 0x25,
            InactivityTime = 0x26,
            AxisEnableControlForActivityAndInactivityDetection = 0x27,
            FreeFallThreshold = 0x28,
            FreeFallTime = 0x29,
            AxisControlForSingleTapDoubleTap = 0x2A,
            SourceOfSingleTapDoubleTap = 0x2B,
            DataRateAndPowerModeControl = 0x2C,
            PowerSavingFeaturesControl = 0x2D,
            InterruptEnableControl = 0x2E,
            InterruptMappingControl = 0x2F,
            SourceOfInterrupts = 0x30,
            DataFormatControl = 0x31,
            Xdata0 = 0x32,
            Xdata1 = 0x33,
            Ydata0 = 0x34,
            Ydata1 = 0x35,
            Zdata0 = 0x36,
            Zdata1 = 0x37,
            FifoControl = 0x38,
            FifoStatus = 0x39
        }
    }
}
