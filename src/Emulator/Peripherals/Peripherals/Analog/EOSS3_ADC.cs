//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class EOSS3_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EOSS3_ADC(IMachine machine) : base(machine)
        {
            channels = new Channel[] { new Channel(this, 0), new Channel(this, 1) };
            DefineRegisters();
        }

        public void FeedSample(uint value, uint channelNo, int repeat = 1)
        {
            if(channelNo > 1)
            {
                throw new RecoverableException("Only channels 0/1 are supported");
            }

            var sample = new Sample { Value = value };
            channels[channelNo].FeedSample(sample, repeat);
        }

        public void FeedSample(string path, uint channelNo, int repeat = 1)
        {
            if(channelNo > 1)
            {
                throw new RecoverableException("Only channels 0/1 are supported");
            }

            var parsedSamples = ParseSamplesFile(path);
            channels[channelNo].FeedSample(parsedSamples, repeat);
        }

        public override void Reset()
        {
            base.Reset();
            state = State.Idle;

            foreach(var c in channels)
            {
                c.Reset();
            }
        }

        public long Size => 0x10;

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
                        if(!uint.TryParse(line.Trim(), out var sample))
                        {
                            throw new RecoverableException($"Wrong data file format at line {lineNumber}. Expected an unsigned integer number, but got '{line}'");
                        }

                        localQueue.Enqueue(new Sample { Value = sample });
                    }
                }
            }
            catch(Exception e)
            {
                if(e is RecoverableException)
                {
                    throw;
                }

                // this is to nicely handle IO errors in monitor
                throw new RecoverableException(e.Message);
            }

            return localQueue;
        }

        private void DefineRegisters()
        {
            Registers.Out.Define(this)
                .WithValueField(0, 12, FieldMode.Read, name: "out", valueProviderCallback: _ => channels[selectedChannel1.Value ? 1 : 0].GetSample())
                .WithReservedBits(12, 20)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "eoc", valueProviderCallback: _ =>
                {
                    switch(state)
                    {
                        case State.Idle:
                            return true;

                        case State.ConversionStarted:
                            channels[selectedChannel1.Value ? 1 : 0].PrepareSample();
                            state = State.SampleReady;
                            return false;

                        case State.SampleReady:
                            state = State.Idle;
                            return true;

                        default:
                            throw new ArgumentException($"Unexpected state: {state}");
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Registers.Control.Define(this)
                .WithFlag(0, name: "soc", writeCallback: (_, val) => StartConversion(val))
                .WithFlag(1, out selectedChannel1, name: "sel")
                .WithFlag(2, name: "meas_en")
                .WithReservedBits(3, 28)
            ;
        }

        private void StartConversion(bool flag)
        {
            state = flag
                ? State.ConversionStarted
                : State.Idle;
        }
       
        private State state;
        private IFlagRegisterField selectedChannel1;

        private readonly Channel[] channels;

        private class Channel
        {
            public Channel(EOSS3_ADC parent, int id)
            {
                this.id = id;
                this.parent = parent;
                samples = new Queue<Sample>();
            }

            public void PrepareSample()
            {
                lock(samples)
                {
                    if(samples.Count == 0 && BufferedSamples != null)
                    {
                        samples.EnqueueRange(BufferedSamples);
                    }

                    if(!samples.TryDequeue(out currentSample))
                    {
                        currentSample = new Sample();
                        parent.Log(LogLevel.Warning, "No more samples available in channel {0}", id);
                    }
                }
            }

            public uint GetSample()
            {
                return currentSample.Value;
            }

            public void FeedSample(Sample sample, int repeat = 1)
            {
                lock(samples)
                {
                    if(repeat == -1)
                    {
                        BufferedSamples = new Sample[] { sample };
                    }
                    else
                    {
                        BufferedSamples = null;
                        for(var i = 0; i < repeat; i++)
                        {
                            samples.Enqueue(sample);
                        }
                    }
                }
            }

            public void FeedSample(IEnumerable<Sample> samplesCollection, int repeat = 1)
            {
                lock(samples)
                {
                    if(repeat == -1)
                    {
                        BufferedSamples = samplesCollection;
                    }
                    else
                    {
                        BufferedSamples = null;
                        for(var i = 0; i < repeat; i++)
                        {
                            samples.EnqueueRange(samplesCollection);
                        }
                    }
                }
            }

            public void Reset()
            {
                lock(samples)
                {
                    BufferedSamples = null;
                    samples.Clear();
                    currentSample = new Sample();
                }
            }

            public IEnumerable<Sample> BufferedSamples { get; private set; }

            private Sample currentSample;

            private readonly Queue<Sample> samples;
            private readonly int id;
            private readonly EOSS3_ADC parent;
        }

        private struct Sample
        {
            public uint Value;
        }

        private enum State
        {
            Idle,
            ConversionStarted,
            SampleReady
        }

        private enum Registers
        {
            Out = 0x0,
            Status = 0x4,
            Control = 0x8
        }
    }
}
