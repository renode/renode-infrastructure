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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public abstract class LiteX_I2S : BasicDoubleWordPeripheral, IKnownSize
    {
        protected LiteX_I2S(Machine machine, DataFormat format, uint sampleWidth, uint samplingRate, uint channelsConcatenatedBit, uint fifoIrqThreshold = 256) : base(machine)
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
            DefineRegisters(channelsConcatenatedBit);
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            buffer.Clear();
        }

        [ConnectionRegion("buffer")]
        public void WriteToBuffer(long offset, uint value)
        {
            // HW allows to access FIFO from any offset
            if(!TryEnqueueSample(value))
            {
                this.Log(LogLevel.Warning, "FIFO overflow, some data will be lost");
            }
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
                    this.Log(LogLevel.Noisy, "FIFO is full, sorry");
                    return false;
                }

                buffer.Enqueue(sample);
                this.Log(LogLevel.Noisy, "Sample enqueued, buffer is now {0} bytes long", buffer.Count);

                QueueFilled();
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
                    this.Log(LogLevel.Noisy, "FIFO is empty, sorry");
                    sample = 0;
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

        protected virtual void QueueFilled()
        {
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

        protected readonly uint fifoIrqThreshold;
        protected readonly Queue<uint> buffer;

        private void DefineRegisters(uint channelsConcatenatedBit)
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
                .WithFlag(0, out enabled, name: "enable")
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

            Registers.Status3.Define(this)
                // TODO: implement concatenated channels handling
                .WithFlag((int)channelsConcatenatedBit, FieldMode.Read, name: "channels_concatenated")
            ;

            Registers.Config3.Define(this)
                .WithEnumField<DoubleWordRegister, DataFormat>(0, 2, FieldMode.Read, name: "format", valueProviderCallback: _ => dataFormat)
                .WithValueField(2, 6, FieldMode.Read, name: "sample_width", valueProviderCallback: _ => sampleWidth)
                .WithValueField(8, 24, FieldMode.Read, name: "sampling_rate", valueProviderCallback: _ => samplingRate)
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
        private readonly int fifoDepth = 512;

        protected enum Registers
        {
            EventStatus  = 0x000,
            EventPending = 0x004,
            EventEnable  = 0x008,
            Control      = 0x00C,

            Status0      = 0x010,
            Status3      = 0x010 + 0xC,

            Config0      = 0x020,
            Config3      = 0x020 + 0xC
        }    
    }

    public enum DataFormat
    {
        Standard = 1,
        LeftJustified = 2
    }
}
