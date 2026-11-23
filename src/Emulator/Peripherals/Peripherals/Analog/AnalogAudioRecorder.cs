
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class AnalogAudioRecorder : IVoltageSink, IPeripheral
    {
        public AnalogAudioRecorder(IMachine machine)
        {
            this.machine = machine;
            samples = new List<AudioSample>();
        }

        public int SampleCount => samples.Count;

        public void SetVoltage(double voltage)
        {
            var timestamp =
                (ulong)machine.LocalTimeSource.ElapsedVirtualTime.TotalNanoseconds;
            
            lock(samples)
            {
                samples.Add(new AudioSample
                {
                    Timestamp = timestamp,
                    Voltage = voltage
                });
            }
        }

        public void Reset()
        {
            lock(samples)
            {
                samples.Clear();
            }
        }

        public void SaveWav(string path)
        {
            List<AudioSample> captured;

            lock(samples)
            {
                captured = new List<AudioSample>(samples);
            }

            if(captured.Count == 0)
            {
                throw new InvalidOperationException(
                    "No audio samples recorded.");
            }

            var endTimestamp =
                (ulong)machine.LocalTimeSource.ElapsedVirtualTime.TotalNanoseconds;

            var startTimestamp = captured[0].Timestamp;

            if(endTimestamp <= startTimestamp)
            {
                throw new InvalidOperationException(
                    "Recording duration is zero.");
            }

            var duration = endTimestamp - startTimestamp;

            var frameCount = (int)Math.Ceiling(
                duration * SampleRate / 1000000000.0
            );

            var dataSize = frameCount * 2;

            using(var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);

                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                var sampleIndex = 0;
                var currentVoltage = 0.0;

                for(var i = 0; i < frameCount; i++)
                {
                    var timestamp = startTimestamp +
                        (ulong)((double)i * 1000000000.0 / SampleRate);

                    while(sampleIndex < captured.Count &&
                          captured[sampleIndex].Timestamp <= timestamp)
                    {
                        currentVoltage =
                            captured[sampleIndex].Voltage;

                        sampleIndex++;
                    }

                    var normalized =
                        (currentVoltage / ReferenceVoltage) * 2.0 - 1.0;

                    normalized = Math.Max(-1.0,
                        Math.Min(1.0, normalized));

                    var pcm = (short)Math.Round(
                        normalized * 32767.0);

                    writer.Write(pcm);
                }
            }
        }

        private readonly IMachine machine;
        private readonly List<AudioSample> samples;

        private const int SampleRate = 44100;
        private const double ReferenceVoltage = 3.3;

        private class AudioSample
        {
            public ulong Timestamp;
            public double Voltage;
        }
    }
}
