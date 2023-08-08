//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Sound
{
    // this model lack support for concatenated channels
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public abstract class LiteX_I2S : BasicDoubleWordPeripheral, IKnownSize
    {
        protected LiteX_I2S(IMachine machine, DataFormat format, uint sampleWidth, uint samplingRate, uint fifoIrqThreshold = 256) : base(machine)
        {
            if(format != DataFormat.Standard)
            {
                throw new ConstructionException("Only Standard format is supported at the moment");
            }

            if(fifoIrqThreshold > fifoDepth)
            {
                throw new ConstructionException($"Wrong fifo IRQ threshold value: {fifoIrqThreshold}. Should be in range 0 - {fifoDepth}");
            }

            this.fifoIrqThreshold = fifoIrqThreshold;
            this.dataFormat = format;
            this.samplingRate = samplingRate;
            this.sampleWidth = sampleWidth;

            buffer = new Queue<uint>();
            IRQ = new GPIO();
            DefineRegisters();
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            buffer.Clear();

            UpdateInterrupts();
        }

        [ConnectionRegion("buffer")]
        public void WriteToBuffer(long offset, uint value)
        {
            // HW allows to access FIFO from any offset
            TryEnqueueSample(value);
        }

        [ConnectionRegion("buffer")]
        public uint ReadFromBuffer(long offset)
        {
            // HW allows to access FIFO from any offset
            if(!TryDequeueSample(out var res))
            {
                this.Log(LogLevel.Warning, "Tried to read from an empty FIFO");
            }

            return res;
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        protected bool TryEnqueueSample(uint sample)
        {
            this.Log(LogLevel.Noisy, "Trying to enqueue a sample: 0x{0:X}", sample);

            lock(buffer)
            {
                if(buffer.Count >= fifoDepth)
                {
                    this.Log(LogLevel.Warning, "FIFO overflow, a sample will be lost");
                    
                    errorEventPending.Value = true;
                    UpdateInterrupts();
                    
                    return false;
                }

                EnqueueSampleInner(sample);
                UpdateInterrupts();

                return true;
            }
        }

        protected bool TryDequeueSample(out uint sample)
        {
            this.Log(LogLevel.Noisy, "Trying to dequeue a sample");
            lock(buffer)
            {
                if(buffer.Count == 0)
                {
                    this.Log(LogLevel.Noisy, "Tried to read from an empty FIFO, returned 0 by default");
                    sample = 0;

                    errorEventPending.Value = true;
                    UpdateInterrupts();

                    return false;
                }

                sample = buffer.Dequeue();
                this.Log(LogLevel.Noisy, "Dequeued 0x{0:X}, buffer is now {1} bytes long", sample, buffer.Count);

                UpdateInterrupts();

                return true;
            }
        }

        protected virtual bool IsReady()
        {
            return false;
        }

        protected virtual void EnqueueSampleInner(uint sample)
        {
            buffer.Enqueue(sample);
            this.Log(LogLevel.Noisy, "Sample enqueued, buffer is now {0} bytes long", buffer.Count);
        }

        protected void UpdateInterrupts()
        {
            lock(buffer)
            {
                var state = false;

                readyEventPending.Value = IsReady();

                state |= readyEventEnabled.Value && readyEventPending.Value;
                state |= errorEventEnabled.Value && errorEventPending.Value;

                state &= enabled.Value;

                this.Log(LogLevel.Noisy, "Setting IRQ to {0}", state);
                IRQ.Set(state);
            }
        }

        protected virtual void HandleEnable(bool enabled)
        {
            // by default do nothing
        }

        protected readonly uint fifoIrqThreshold;
        protected readonly Queue<uint> buffer;

        private void DefineRegisters()
        {
            Registers.EventPending.Define32(this)
                .WithFlag(0, out readyEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "ready")
                .WithFlag(1, out errorEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "error")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.EventEnable.Define(this)
                .WithFlag(0, out readyEventEnabled, name: "ready")
                .WithFlag(1, out errorEventEnabled, name: "error")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Control.Define(this)
                .WithFlag(0, out enabled, name: "enable", writeCallback: (_, v) => HandleEnable(v))
                .WithFlag(1, FieldMode.Write, name: "fifo_reset", writeCallback: (_, v) =>
                {
                    if(!v)
                    {
                        return;
                    }

                    this.Log(LogLevel.Noisy, "Flushing FIFO");
                    buffer.Clear();
                    UpdateInterrupts();
                })
                .WithReservedBits(2, 30)
            ;

            Registers.Config0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(samplingRate >> 16), name: "sampling_rate")
            ;

            Registers.Config1.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(samplingRate >> 8), name: "sampling_rate")
            ;

            Registers.Config2.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(samplingRate >> 0), name: "sampling_rate")
            ;

            Registers.Config3.Define(this)
                .WithEnumField<DoubleWordRegister, DataFormat>(0, 2, FieldMode.Read, valueProviderCallback: _ => dataFormat, name: "format")
                .WithValueField(2, 6, FieldMode.Read, valueProviderCallback: _ => sampleWidth, name: "sample_width")
            ;
        }

        private IFlagRegisterField enabled;
        private IFlagRegisterField readyEventEnabled;
        private IFlagRegisterField errorEventEnabled;
        private IFlagRegisterField readyEventPending;
        private IFlagRegisterField errorEventPending;

        private readonly DataFormat dataFormat;
        private readonly uint sampleWidth;
        private readonly uint samplingRate;

        private const int fifoDepth = 512;

        public enum DataFormat
        {
            Standard = 1,
            LeftJustified = 2
        }

        protected enum Registers
        {
            EventStatus  = 0x000,
            EventPending = 0x004,
            EventEnable  = 0x008,
            Control      = 0x00C,
            Status       = 0x010,

            // this is a single 32-bit LiteX CSR
            // scattered accross 4 regular 32-bit registers
            Config0      = 0x020,
            Config1      = 0x020 + 0x4,
            Config2      = 0x020 + 0x8,
            Config3      = 0x020 + 0xC
        }
    }
}
