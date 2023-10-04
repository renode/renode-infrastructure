//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Sound;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class NRF52840_PDM: BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_PDM(IMachine machine) : base(machine)
        {
            CreateRegisters();
            IRQ = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            decoderLeft?.Reset();
            decoderRight?.Reset();
            sampleThread?.Dispose();
            sampleThread = null;
            inputFileLeft = "";
            inputFileRight = "";
            multiplierL = 1.0;
            multiplierR = 1.0;
            numberOfChannels = 2;
            sampleRatio = 64;
            clockFrequency = 32000000 / 31;
            SetSampleFrequency();
        }

        public void SetInputFile(string fileName, Channel channel = Channel.Left, int repeat = 1)
        {
            switch(channel)
            {
                case Channel.Left:
                {
                    if(decoderLeft == null)
                    {
                        decoderLeft = new PCMDecoder(16, sampleFrequency, 1, false, this);
                    }

                    for(var i = 0; i < repeat; i++)
                    {
                        decoderLeft.LoadFile(fileName);
                    }
                    inputFileLeft = fileName;
                }
                break;

                case Channel.Right:
                {
                    if(decoderRight == null)
                    {
                        decoderRight = new PCMDecoder(16, sampleFrequency, 1, false, this);
                    }

                    for(var i = 0; i < repeat; i++)
                    {
                        decoderRight.LoadFile(fileName);
                    }
                    inputFileRight = fileName;
                }
                break;
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public enum Channel
        {
            Left = 0,
            Right = 1,
        }

        private void UpdateInterrupts()
        {
            var stopped = eventStopped.Value && intenStopped.Value;
            var started = eventStarted.Value && intenStarted.Value;
            var end = eventEnd.Value && intenEnd.Value;
            IRQ.Set(stopped || started || end);
        }

        private void Start()
        {
            if(!enablePDM.Value)
            {
                this.Log(LogLevel.Warning, "Trying to start samples aquisition before enabling peripheral. Will not start");
                return;
            }

            if(inputFileLeft == "" || (numberOfChannels == 2 && inputFileRight == ""))
            {
                this.Log(LogLevel.Error, "Trying to start reception with not enough input files - please set input using `SetinputFile`. Aborting.");
                return;
            }
            
            StartPDMThread();
        }

        private void Stop()
        {
            eventStopped.Value = true;
            UpdateInterrupts();
            this.Log(LogLevel.Debug, "Event Stopped");
        }

        private bool StartPDMThread()
        {
            StopPDMThread();

            if(maxSamplesCount.Value == 0)
            {
                // Crate stub ManagedThread just to make clear that it was started, but just send
                // eventStarted Interrupt - software might configure proper value in the IRQ handler
                sampleThread = machine.ObtainManagedThread(InputSamples, 1);
                eventStarted.Value = true;
                UpdateInterrupts();
                return false;
            }
            // Since we handle all samples in one go we have to calculate how often should we do it
            var eventFrequency = (sampleFrequency / (int)(maxSamplesCount.Value)) * numberOfChannels;
            sampleThread = machine.ObtainManagedThread(InputSamples, (uint)eventFrequency);
            sampleThread.Start();
            return true;
        }

        private bool StopPDMThread()
        {
           if(sampleThread == null)
           {
               return false;
           }
           sampleThread.Stop();
           sampleThread.Dispose();
           sampleThread = null;
           return true;
        }

        private void InputSamples()
        {
            var currentPointer = samplePtr.Value;
            eventStarted.Value = true;
            UpdateInterrupts();

            var samplesCount = (uint)maxSamplesCount.Value;
            var doubleWordsCount = samplesCount / 2;
            var preparedDoubleWords = new uint[doubleWordsCount];

            switch(numberOfChannels)
            {
                case 1:
                    var samples = decoderLeft.GetSamplesByCount(samplesCount);

                    var index = 1u;
                    ushort prev = 0;
                    
                    foreach(ushort sample in samples)
                    {
                        if(index % 2 != 0)
                        {
                            prev = sample;
                        }
                        else
                        {
                            // Assuming input file format of s16le
                            preparedDoubleWords[(index / 2) - 1] = (uint)((Misc.SwapBytesUShort(sample) << 16) | Misc.SwapBytesUShort(prev));
                        }
                        index++;
                    }
                    
                    if(index % 2 == 0)
                    {
                        // One sample left
                        preparedDoubleWords[(index / 2) - 1] = prev;
                    }
                    
                    break;
                case 2:
                    var samplesLeft  = decoderLeft.GetSamplesByCount(samplesCount / 2).ToArray();
                    var samplesRight = decoderRight.GetSamplesByCount(samplesCount / 2).ToArray();
                                                   
                    if(samplesLeft.Length != samplesRight.Length)
                    {
                        // Make sure arrays have equal size
                        var neededSize = Math.Max(samplesLeft.Length, samplesRight.Length);
                        Array.Resize(ref samplesLeft, neededSize);
                        Array.Resize(ref samplesRight, neededSize);
                    }

                    if(invertChannels.Value)
                    {
                        Misc.Swap(ref samplesLeft, ref samplesRight);
                    }

                    for(var i = 0; i < samplesLeft.Length; i++)
                    {
                        var right = (uint)Misc.SwapBytesUShort((ushort)samplesRight[i]);
                        var left  = (uint)Misc.SwapBytesUShort((ushort)samplesLeft[i]);

                        preparedDoubleWords[i] = (right << 16) | left;
                    }
                    break;
            }

            foreach(uint i in preparedDoubleWords)
            {
                sysbus.WriteDoubleWord(currentPointer, i);
                currentPointer += 4;
            }

            eventEnd.Value = true;
            UpdateInterrupts();
        }

        private void SetGain(ulong val, Channel channel)
        {
            if(val > 80)
            {
                this.Log(LogLevel.Error, "Trying to set GAIN.{0} value higher than 80. Setting gain to default value.", channel);
                val = 40;
            }
            var gain = ((int)val - 40) / 2;
            this.Log(LogLevel.Debug, "{0} channel gain set to {1}db", channel,  gain);
            // Convert dB of amplitude to multiplier
            var multiplier = Math.Pow(10, gain / 20.0);
            switch(channel)
            {
                case Channel.Left:
                    multiplierL = multiplier;
                    break;
                case Channel.Right:
                    multiplierR = multiplier;
                    break;
            }
        }

        private void SetClockFrequency(ClockFrequency frequency)
        {
            switch(frequency)
            {
                case ClockFrequency.f1000K:
                    clockFrequency = 32000000 / 32;
                    break;
                case ClockFrequency.Default:
                    clockFrequency = 32000000 / 31;
                    break;
                case ClockFrequency.f1067K:
                    clockFrequency = 32000000 / 30;
                    break;
                case ClockFrequency.f1231K:
                    clockFrequency = 32000000 / 26;
                    break;
                case ClockFrequency.f1280K:
                    clockFrequency = 32000000 / 25;
                    break;
                case ClockFrequency.f1333K:
                    clockFrequency = 32000000 / 24;
                    break;
                default:
                    this.Log(LogLevel.Error, "Wrong PDMCLKCTRL value, settting to default value");
                    goto case ClockFrequency.Default;
            }
            SetSampleFrequency();
        }

        private void SetSampleFrequency()
        {
            sampleFrequency = clockFrequency / sampleRatio;
            this.Log(LogLevel.Debug, "Clock frequency = {0}kHz; Sample ratio = {1}; Sample frequency set to {2}kHz", clockFrequency / 1000.0, sampleRatio, sampleFrequency / 1000.0);
        }

        private void CreateRegisters()
        {
            Registers.TasksStart.Define(this, 0x0)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) Start(); }, name: "TASKS_START")
                .WithReservedBits(1, 31);
            Registers.TasksStop.Define(this, 0x0)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) Stop(); }, name: "TASKS_STOP")
                .WithReservedBits(1, 31);
            Registers.EventsStarted.Define(this, 0x0)
                .WithFlag(0, out eventStarted, changeCallback: (_, __) => UpdateInterrupts(), name: "EVENTS_STARTED")
                .WithReservedBits(1, 31);
            Registers.EventsStopped.Define(this, 0x0)
                .WithFlag(0, out eventStopped, changeCallback: (_, __) => UpdateInterrupts(), name: "EVENTS_STOPPED")
                .WithReservedBits(1, 31);
            Registers.EventsEnd.Define(this, 0x0)
                .WithFlag(0, out eventEnd, changeCallback: (_, __) => UpdateInterrupts(), name: "EVENTS_END")
                .WithReservedBits(1, 31);
            Registers.InterruptEnable.Define(this, 0x0)
                .WithFlag(0, out intenStarted, name: "STARTED")
                .WithFlag(1, out intenStopped, name: "STOPPED")
                .WithFlag(2, out intenEnd, name: "END")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.InterruptEnableSet.Define(this, 0x0)
                .WithFlag(0,
                    writeCallback: (_, val) => { intenStarted.Value |= val; },
                    valueProviderCallback: (_) => { return intenStarted.Value; },
                    name: "STARTED")
                .WithFlag(1,
                    writeCallback: (_, val) => { intenStopped.Value |= val; },
                    valueProviderCallback: (_) => { return intenStopped.Value; },
                    name: "STOPPED")
                .WithFlag(2,
                    writeCallback: (_, val) => { intenEnd.Value |= val; },
                    valueProviderCallback: (_) => { return intenEnd.Value; },
                    name: "END")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.InterruptEnableClear.Define(this, 0x0)
                .WithFlag(0,
                    writeCallback: (_, val) => { intenStarted.Value &= !val; },
                    valueProviderCallback: (_) => { return intenStarted.Value; },
                    name: "STARTED")
                .WithFlag(1,
                    writeCallback: (_, val) => { intenStopped.Value &= !val; },
                    valueProviderCallback: (_) => { return intenStopped.Value; },
                    name: "STOPPED")
                .WithFlag(2,
                    writeCallback: (_, val) => { intenEnd.Value &= !val; },
                    valueProviderCallback: (_) => { return intenEnd.Value; },
                    name: "END")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.Enable.Define(this, 0x0)
                .WithFlag(0, out enablePDM, name: "ENABLE")
                .WithReservedBits(1, 31);
            Registers.PdmClockControl.Define(this, 0x08400000)
                .WithValueField(0, 32,
                    writeCallback: (_, val) => SetClockFrequency((ClockFrequency)val), name: "FREQ");
            Registers.Mode.Define(this, 0x0)
                .WithValueField(0, 1, out operationMode,
                    writeCallback: (_, val) =>
                        {
                            switch((OperationMode)val)
                            {
                                case OperationMode.Stereo:
                                    numberOfChannels = 2;
                                    break;
                                case OperationMode.Mono:
                                    numberOfChannels = 1;
                                    break;
                            }
                            this.Log(LogLevel.Debug, "MODE.OPERATION set to {0}", (OperationMode)val);
                        },
                    name: "OPERATION")
                .WithFlag(1, out invertChannels, name:"EDGE")
                .WithReservedBits(2, 30);
            Registers.Ratio.Define(this, 0x0)
                .WithValueField(0, 1,
                     writeCallback: (_, val) =>
                     {
                         switch((Ratio)val)
                         {
                             case Ratio.x64:
                                 sampleRatio = 64;
                                 break;
                             case Ratio.x80:
                                 sampleRatio = 80;
                                 break;
                         }
                         SetSampleFrequency();
                     },
                     name:"RATIO")
                .WithReservedBits(1, 31);
            Registers.GainLeft.Define(this, 0x28)
                .WithValueField(0, 7,
                    writeCallback: (_, val) => SetGain(val, Channel.Left), name: "GAINL")
                .WithReservedBits(8, 24);
            Registers.GainRigth.Define(this, 0x28)
                .WithValueField(0, 7, writeCallback: (_, val) => SetGain(val, Channel.Right), name: "GAINR")
                .WithReservedBits(8, 24);
            Registers.SamplePointer.Define(this, 0x0)
                .WithValueField(0, 32, out samplePtr, name: "PTR");
            Registers.SampleBufferSize.Define(this, 0x0)
                .WithValueField(0, 15, out maxSamplesCount,
                    writeCallback: (oldval, val) =>
                    {
                        if(oldval != val && sampleThread != null)
                        {
                            // Need to restart thread to change how often it fires
                            this.Log(LogLevel.Debug, "Setting MaxSampleCount to {0}", val);
                            StartPDMThread();
                        }
                    },
                    name: "MAXCNT")
                .WithReservedBits(15, 17);
            Registers.PinSelectClk.Define(this, 0xFFFFFFFF)
                .WithTaggedFlag("PIN", 0)
                .WithTaggedFlag("PORT", 5)
                .WithReservedBits(6, 25)
                .WithTaggedFlag("CONNECT", 31);
            Registers.PinSelectDin.Define(this, 0xFFFFFFFF)
                .WithTaggedFlag("PIN", 0)
                .WithTaggedFlag("PORT", 5)
                .WithReservedBits(6, 25)
                .WithTaggedFlag("CONNECT", 31);
        }

        private uint clockFrequency;
        private uint numberOfChannels;
        private uint sampleFrequency;
        private uint sampleRatio;
        private double multiplierL;
        private double multiplierR;
        private string inputFileLeft;
        private string inputFileRight;

        private PCMDecoder decoderLeft;
        private PCMDecoder decoderRight;
        private IManagedThread sampleThread;

        private IFlagRegisterField enablePDM;
        private IFlagRegisterField eventEnd;
        private IFlagRegisterField eventStarted;
        private IFlagRegisterField eventStopped;
        private IFlagRegisterField intenEnd;
        private IFlagRegisterField intenStarted;
        private IFlagRegisterField intenStopped;
        private IFlagRegisterField invertChannels;
        private IValueRegisterField maxSamplesCount;
        private IValueRegisterField operationMode;
        private IValueRegisterField samplePtr;

        private enum Registers : long
        {
            TasksStart           = 0x000, // Starts continuous PDM transfer
            TasksStop            = 0x004, // Stops PDM transfer
            EventsStarted        = 0x100, // PDM transfer has started
            EventsStopped        = 0x104, // PDM transfer has finished
            EventsEnd            = 0x108, // The PDM has written the last sample specified by Sample_MAXCNT (or the last sample after aSTOP task has been received) to Data RAM.
            InterruptEnable      = 0x300, // Enable or disable interrupt
            InterruptEnableSet   = 0x304, // Enable interrupt
            InterruptEnableClear = 0x308, // Disable interrupt
            Enable               = 0x500, // PDM module enable register
            PdmClockControl      = 0x504, // PDM clock generator control
            Mode                 = 0x508, // Defines the routing of the connected PDM microphones' signals
            GainLeft             = 0x518, // Left output gain adjustment
            GainRigth            = 0x51C, // Right output gain adjustment
            Ratio                = 0x520, // Selects the ratio between PDM_CLK and output sample rate. Change PDMCLKCTRL accordingly.
            PinSelectClk         = 0x540, // Pin number configuration for PDM CLK signal
            PinSelectDin         = 0x544, // Pin number configuration for PDM DIN signal
            SamplePointer        = 0x560, // RAM address pointer to write samples to with EasyDMA
            SampleBufferSize     = 0x564, // Number of samples to allocate memory for in EasyDMA mode
        }

        private enum ClockFrequency
        {
            f1000K  = 0x08000000, // PDM_CLK = 32 MHz / 32 = 1.000 MHz
            Default = 0x08400000, // PDM_CLK = 32 MHz / 31 = 1.032 MHz. Nominal clock forRATIO=Ratio64.
            f1067K  = 0x08800000, // PDM_CLK = 32 MHz / 30 = 1.067 MHz
            f1231K  = 0x09800000, // PDM_CLK = 32 MHz / 26 = 1.231 MHz
            f1280K  = 0x0A000000, // PDM_CLK = 32 MHz / 25 = 1.280 MHz. Nominal clock forRATIO=Ratio80.
            f1333K  = 0x0A800000, // PDM_CLK = 32 MHz / 24 = 1.333 MHz
        }

        private enum OperationMode
        {
            Stereo = 0,
            Mono   = 1,
        }

        private enum Ratio
        {
            x64 = 0,
            x80 = 1,
        }
    }
}
