//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class EOSS3_PacketFIFO : BasicDoubleWordPeripheral, IKnownSize
    {
        public EOSS3_PacketFIFO(IMachine machine) : base(machine)
        {
            queueNames = new Dictionary<long, string>()
            {
                {0, "PacketFifo0"},
                {1, "PacketFifo1"},
                {2, "PacketFifo2"},
                {3, "PacketFifo8k"}
            };

            packetFifos = new PacketFifoBase[NumberOfQueues];

            for(var i = 0; i < NumberOfRegularQueues; i ++)
            {
                packetFifos[i] = new PacketFifo(this, queueNames[i], fifoSizes[i]);
            }

            packetFifos[NumberOfQueues - 1] = new PacketFifoSwitchable(this, queueNames[NumberOfQueues - 1], fifoSizes[NumberOfQueues - 1]);

            DefineRegisters();
        }

        public long Size => 0x2000;

        /*
                Collision is not to be implemented
                Ram is always awake.
        */
        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            var loopHelper = Registers.FifoControl.Define(this);
            for(var i = 0; i < 31; i += 8)
            {
                loopHelper
                    .WithFlag(i, out enable[i / 8], writeCallback: WarnUnsupportedMux, name: queueNames[i / 8] + "_en")
                    .WithFlag(i + 0x1, out pushMux[i / 8], writeCallback: WarnUnsupportedMux, name: queueNames[i / 8] + "_push_mux")
                    .WithFlag(i + 0x2, out popMux[i / 8], writeCallback: WarnUnsupportedMux, name: queueNames[i / 8] + "_pop_mux")
                    .WithFlag(i + 0x3, out pushIntMux[i / 8], writeCallback: WarnUnsupportedMux, name: queueNames[i / 8] + "_push_int_mux")
                    .WithFlag(i + 0x4, out popIntMux[i / 8], writeCallback: WarnUnsupportedMux, name: queueNames[i / 8] + "_pop_int_mux")
                    .WithTaggedFlag(queueNames[i/8] + "_ffe_sel", i + 0x5);
            }
            loopHelper.WithReservedBits(30, 2);

            loopHelper = Registers.FifoStatus.Define(this);
            for(var i = 0; i < 31; i += 8)
            {
                loopHelper
                    .WithValueField(i + 0x0, 2, valueProviderCallback: _ => 0x0, name: queueNames[i / 8] + "_sram_sleep" ) //will never sleep
                    .WithFlag(i + 0x2, out packetFifos[i / 8].Overflow, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: UpdateIRQWrapper, name: queueNames[i / 8] + "_push_int_over")
                    .WithFlag(i + 0x3, out packetFifos[i / 8].PushThreshold, FieldMode.Read, name:  queueNames[i / 8] + "_push_int_thresh")
                    .WithFlag(i + 0x4, out packetFifos[i / 8].PushOnSleep, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: UpdateIRQWrapper, name: queueNames[i / 8] + "_push_int_sleep")
                    .WithFlag(i + 0x5, out packetFifos[i / 8].Underflow, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: UpdateIRQWrapper, name: queueNames[i / 8] + "_pop_int_under")
                    .WithFlag(i + 0x6, out packetFifos[i / 8].PopThreshold, FieldMode.Read, name:  queueNames[i / 8] + "_pop_int_thresh")
                    .WithFlag(i + 0x7, out packetFifos[i / 8].PopOnSleep, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: UpdateIRQWrapper, name: queueNames[i / 8] + "_pop_int_sleep");
            }

            Registers.PushControlPacketFifo0.DefineMany(this, NumberOfQueues, (reg, idx) =>
                reg.WithTaggedFlag(queueNames[idx] + "_push_sleep_en", 0x0)
                .WithTaggedFlag(queueNames[idx] + "_push_sleep_type", 0x1)
                .WithFlag(0x2, out packetFifos[idx].PushOverMask, writeCallback: UpdateIRQWrapper, name: queueNames[idx] + "_push_int_en_over")
                .WithFlag(0x3, out packetFifos[idx].PushThresholdMask, writeCallback: UpdateIRQWrapper, name: queueNames[idx] + "_push_int_en_thresh")
                .WithTaggedFlag(queueNames[idx] + "_push_int_en_sram_sleep", 0x4)
                .WithValueField(0x10, 9, out packetFifos[idx].PushThresholdLevel, name: queueNames[idx] + "_push_thresh")
                .WithReservedBits(25, 7)
            , 0x10);

            Registers.PopControlPacketFifo0.DefineMany(this, NumberOfRegularQueues, (reg, idx) =>
                reg.WithTaggedFlag(queueNames[idx] + "_pop_sleep_en", 0x0)
                .WithTaggedFlag(queueNames[idx] + "_pop_sleep_type", 0x1)
                .WithFlag(0x2, flagField: out packetFifos[idx].PopUnderMask, writeCallback: UpdateIRQWrapper, name: queueNames[idx] + "_pop_int_en_under")
                .WithFlag(0x3, flagField: out packetFifos[idx].PopThresholdMask, writeCallback: UpdateIRQWrapper, name: queueNames[idx] + "_pop_int_en_thresh")
                .WithTaggedFlag(queueNames[idx] + "_push_int_en_sram_sleep", 0x4)
                .WithValueField(0x10, 9, out packetFifos[idx].PopThresholdLevel, name: queueNames[idx] + "_pop_thresh")
                .WithReservedBits(25, 7)
            , 0x10);

            Registers.PopControlPacketFifo8k.Define(this)
                .WithTaggedFlag(queueNames[3] + "_pop_sleep_en", 0x0)
                .WithTaggedFlag(queueNames[3] + "_pop_sleep_type", 0x1)
                .WithFlag(0x2, flagField: out packetFifos[3].PopUnderMask, writeCallback: UpdateIRQWrapper, name: queueNames[3] + "_pop_int_en_under")
                .WithFlag(0x3, flagField: out packetFifos[3].PopThresholdMask, writeCallback: UpdateIRQWrapper, name: queueNames[3] + "_pop_int_en_thresh")
                .WithFlag(0x5, writeCallback: ((PacketFifoSwitchable) packetFifos[3]).EnableFifoMode, valueProviderCallback: _ => ((PacketFifoSwitchable) packetFifos[3]).pf8kFifoMode, name: queueNames[3] + "_fifo_pkt_mode")
                .WithFlag(0x6, writeCallback: ((PacketFifoSwitchable) packetFifos[3]).EnableRingMode, valueProviderCallback: _ => ((PacketFifoSwitchable) packetFifos[3]).pf8kRingBuffMode, name: queueNames[3] + "_fifo_ring_buff_mode")
                .WithValueField(0x10, 13, out packetFifos[3].PopThresholdLevel, name: queueNames[3] + "_pop_thresh")
                .WithReservedBits(29, 3);

            Registers.CountPacketFifo0.Define(this)
                .WithValueField(0x0, 9, FieldMode.Read, valueProviderCallback: _ => (uint)(packetFifos[0].Count), name: queueNames[0] + "_pop_cnt")
                .WithFlag(0xF, FieldMode.Read, valueProviderCallback: _ => (packetFifos[0].Count == 0), name: queueNames[0] + "_empty")
                .WithValueField(0x10, 9, FieldMode.Read, valueProviderCallback: _ => (uint)(fifoSizes[0] - packetFifos[0].Count), name: queueNames[0] + "_push_cnt")
                .WithFlag(0x1F, FieldMode.Read, valueProviderCallback: _ => (packetFifos[0].Count >= fifoSizes[0]), name: queueNames[0] + "_full");

            Registers.CountPacketFifo1.DefineMany(this, 3, (reg, idx) =>
                reg.WithValueField(0x0, 8, FieldMode.Read, valueProviderCallback: _ => (uint)(packetFifos[idx].Count), name: queueNames[idx] + "_pop_cnt")
                .WithFlag(0xF, FieldMode.Read, valueProviderCallback: _ => (packetFifos[idx].Count == 0), name: queueNames[idx] + "_empty")
                .WithValueField(0x10, 8, FieldMode.Read, valueProviderCallback: _ => (uint)(fifoSizes[idx] - packetFifos[idx].Count), name: queueNames[idx] + "_push_cnt")
                .WithFlag(0x1F, FieldMode.Read, valueProviderCallback: _ => (packetFifos[idx].Count >= fifoSizes[idx]), name: queueNames[idx] + "_full")
            , 0x10);

            Registers.DataPacketFifo0.DefineMany(this, NumberOfRegularQueues, (reg, idx) =>
                reg.WithValueField(0x0, 32, writeCallback: (prevVal, val) => packetFifos[idx].EnqueueCallback((uint)prevVal, (uint)val), valueProviderCallback: (prevVal) => packetFifos[idx].DequeueCallback((uint)prevVal), name: queueNames[idx] + "_data_reg")
            , 0x10);

            Registers.DataPacketFifo8k.Define(this)
                .WithValueField(0x0, 17, writeCallback: (prevVal, val) => packetFifos[3].EnqueueCallback((uint)prevVal, (uint)val), valueProviderCallback: (prevVal) => packetFifos[3].DequeueCallback((uint)prevVal), name: queueNames[3] + "_data_reg")
                .WithFlag(0x11, name: queueNames[3] + "_push_eop")
                .WithReservedBits(18, 14);
        }


        private void WarnUnsupportedMux(bool _, bool value)
        {
            if(value)
            {
                this.Log(LogLevel.Warning, "This target is unsupported.", value);
            }
        }

        private void UpdateIRQWrapper(bool _, bool __)
        {
            UpdateIRQ();
        }

        private void UpdateIRQ()
        {
            foreach(var queue in packetFifos)
            {
                if(queue.PushOverMask.Value && queue.Overflow.Value
                    || queue.PushThresholdMask.Value && queue.PushThreshold.Value
                    || queue.PopUnderMask.Value && queue.Underflow.Value
                    || queue.PopThresholdMask.Value && queue.PopThreshold.Value)
                    {
                        IRQ.Set(true);
                        return;
                    }
            }
            IRQ.Unset();
        }

        private PacketFifoBase[] packetFifos;

        private readonly int[] fifoSizes = {256, 128, 128, 4096};

        private Dictionary<long, string> queueNames;

        private IFlagRegisterField[] enable = new IFlagRegisterField[4];
        private IFlagRegisterField[] pushMux = new IFlagRegisterField[4];
        private IFlagRegisterField[] popMux = new IFlagRegisterField[4];
        private IFlagRegisterField[] pushIntMux = new IFlagRegisterField[4];
        private IFlagRegisterField[] popIntMux = new IFlagRegisterField[4];

        private const int NumberOfQueues = 4;
        private const int NumberOfRegularQueues = 3;

        private abstract class PacketFifoBase
        {
            public PacketFifoBase(EOSS3_PacketFIFO parent, string name, int size)
            {
                this.parent = parent;
                this.name = name;
                this.size = size;
            }

            public abstract void EnqueueCallback(uint _, uint value);
            public abstract uint DequeueCallback(uint _);

            public abstract int Count { get; protected set;}

            public IFlagRegisterField PushOverMask; //mask on 0, enable on 1
            public IFlagRegisterField PushThresholdMask;
            public IValueRegisterField PushThresholdLevel;
            public IFlagRegisterField PopUnderMask;
            public IFlagRegisterField PopThresholdMask;
            public IValueRegisterField PopThresholdLevel;

            public IFlagRegisterField Overflow;
            public IFlagRegisterField Underflow;
            public IFlagRegisterField PushThreshold;
            public IFlagRegisterField PushOnSleep;
            public IFlagRegisterField PopThreshold;
            public IFlagRegisterField PopOnSleep; //no collision, as it is not simulated

            protected String name;
            protected int size;
            protected EOSS3_PacketFIFO parent;
        }

        private class PacketFifo : PacketFifoBase
        {
            public PacketFifo(EOSS3_PacketFIFO parent, string name, int size) : base(parent, name, size)
            {
                fifo = new Queue<UInt32>();
            }

            public override void EnqueueCallback(uint _, uint value)
            {
                PopThreshold.Value = false;

                fifo.Enqueue(value);
                if(fifo.Count >= (int)PushThresholdLevel.Value)
                {
                    PushThreshold.Value = true;
                    parent.Log(LogLevel.Noisy, "Push treshold in {0}.", name);
                }
                if(fifo.Count > size)
                {
                    Overflow.Value = true;
                    parent.Log(LogLevel.Warning, "Cannot push {0}: maximum size exceeded.", name);
                }
                else
                {
                    Overflow.Value = false;
                }
                parent.UpdateIRQ();
            }

            public override uint DequeueCallback(uint _)
            {
                PushThreshold.Value = false;

                if(fifo.Count == 0)
                {
                    Underflow.Value = true;
                    parent.Log(LogLevel.Warning, "Cannot pop: {0} is empty.", name);
                    parent.UpdateIRQ();
                    return 0;
                }
                else
                {
                    Underflow.Value = false;
                }
                if(fifo.Count - 1 <= (int)PopThresholdLevel.Value)
                {
                    PopThreshold.Value = true;
                    parent.Log(LogLevel.Noisy, "Pop treshold in {0}.", name);
                }
                parent.UpdateIRQ();
                return fifo.Dequeue();
            }

            public override int Count
            {
                get {return fifo.Count;}
                protected set {}
            }

            private Queue<UInt32> fifo;
        }

        private class PacketFifoSwitchable : PacketFifoBase
        {
            public PacketFifoSwitchable(EOSS3_PacketFIFO pf, string name, int size) : base(pf, name, size)
            {
                pf3BufferMode = new CircularBuffer<UInt16>(size);
                pf3QueueMode = new Queue<UInt16>(size);
            }

            public void EnableFifoMode(bool _, bool value)
            {
                pf8kFifoMode = value;
                if(value && !pf8kRingBuffMode)
                {
                    pf3QueueMode = new Queue<UInt16> (pf3BufferMode);

                    parent.Log(LogLevel.Noisy, "Switched {0} to FIFO mode", name);
                }
            }

            public void EnableRingMode(bool _, bool value)
            {
                pf8kRingBuffMode = value;
                if(value && !pf8kFifoMode)
                {
                    var size = Math.Max(pf3QueueMode.Count, 4096);
                    pf3BufferMode = new CircularBuffer<UInt16> (size);

                    foreach(var element in pf3QueueMode)
                    {
                        pf3BufferMode.Enqueue(element);
                    }

                    parent.Log(LogLevel.Noisy, "Switched {0} to ring buffer mode", name);
                }
            }

            public override void EnqueueCallback(uint _, uint value)
            {
                PopThreshold.Value = false;

                if(pf8kRingBuffMode && !pf8kFifoMode)
                {
                    pf3BufferMode.Enqueue((ushort)value);
                    if(pf3BufferMode.Count >= (int)PushThresholdLevel.Value)
                    {
                        PushThreshold.Value = true;
                        parent.Log(LogLevel.Noisy, "Push treshold in {0}.", name);
                    }
                    if(pf3BufferMode.Count > size)
                    {
                        parent.Log(LogLevel.Warning, "Cannot push {0}: maximum size exceeded.", name);
                    }
                }
                else if(!pf8kRingBuffMode && pf8kFifoMode)
                {
                    pf3QueueMode.Enqueue((ushort)value);
                    if(pf3QueueMode.Count >= (int)PushThresholdLevel.Value)
                    {
                        PushThreshold.Value = true;
                        parent.Log(LogLevel.Noisy, "Push treshold in {0}", name);
                    }
                    if(pf3QueueMode.Count > size)
                    {
                        Overflow.Value = true;
                        parent.Log(LogLevel.Warning, "Cannot push {0}: maximum size exceeded.", name);
                    }
                    else
                    {
                        Overflow.Value = false;
                    }
                }
                else
                {
                    parent.Log(LogLevel.Warning, "No valid mode for {0} selected", name);
                }
                parent.UpdateIRQ();
            }

            public override uint DequeueCallback(uint _)
            {
                PushThreshold.Value = false;

                if(pf8kRingBuffMode && !pf8kFifoMode)
                {
                    if(pf3BufferMode.Count == 0)
                    {
                        Underflow.Value = true;
                        parent.Log(LogLevel.Warning, "Cannot pop: {0} is empty.", name);
                        parent.UpdateIRQ();
                        return 0;
                    }
                    else
                    {
                        Underflow.Value = false;
                    }
                    if(pf3BufferMode.Count - 1 <= (int)PopThresholdLevel.Value)
                    {
                        PopThreshold.Value = true;
                        parent.Log(LogLevel.Noisy, "Pop treshold in {0}.", name);
                    }
                    parent.UpdateIRQ();
                    return pf3BufferMode.TryDequeue(out var ret) ? ret : 0u;
                }
                else if(!pf8kRingBuffMode && pf8kFifoMode)
                {
                    if(pf3QueueMode.Count == 0)
                    {
                        Underflow.Value = true;
                        parent.Log(LogLevel.Warning, "Cannot pop: {0} is empty.", name);
                        parent.UpdateIRQ();
                        return 0;
                    }
                    else
                    {
                        Underflow.Value = false;
                    }
                    if(pf3QueueMode.Count - 1 <= (int)PopThresholdLevel.Value)
                    {
                        PopThreshold.Value = true;
                        parent.Log(LogLevel.Noisy, "Pop treshold in {0}.", name);
                    }
                    parent.UpdateIRQ();
                    return pf3QueueMode.TryDequeue(out var ret) ? ret : 0u;
                }
                else
                {
                    parent.Log(LogLevel.Warning, "No valid mode for {0} selected", name);
                    parent.UpdateIRQ();
                    return 0;
                }
            }

            public override int Count
            {
                get
                {
                    if(pf8kFifoMode)
                    {
                        return pf3QueueMode.Count;
                    }
                    if(pf8kRingBuffMode)
                    {
                        return pf3BufferMode.Count;
                    }
                    return 0;
                }
                protected set {}
            }

            public bool pf8kFifoMode;
            public bool pf8kRingBuffMode;
            private Queue<UInt16> pf3QueueMode;
            private CircularBuffer<UInt16> pf3BufferMode;
        }

        private enum EPushMux
        {
            M4 = 0x0, //Memory
            FFE = 0x1 //FFE
        }

        private enum EOthersMux
        {
            M4 = 0x0, //Memory
            AP = 0x1, //Applications Processor
        }

        private enum Registers
        {
            FifoControl = 0x0,
            SramControl0 = 0x004,
            SramControl1 = 0x008,
            FifoStatus = 0x00C,
            PushControlPacketFifo0 = 0x010,
            PopControlPacketFifo0 = 0x014,
            CountPacketFifo0 = 0x018,
            DataPacketFifo0 = 0x01C,
            PushControlPacketFifo1 = 0x020,
            PopControlPacketFifo1 = 0x024,
            CountPacketFifo1 = 0x028,
            DataPacketFifo1 = 0x02C,
            PushControlPacketFifo2 = 0x030,
            PopControlPacketFifo2 = 0x034,
            CountPacketFifo2 = 0x038,
            DataPacketFifo2 = 0x03C,
            PushControlPacketFifo8k = 0x040,
            PopControlPacketFifo8k = 0x044,
            CountPacketFifo8k = 0x048,
            DataPacketFifo8k = 0x04C,
            FifoCollisionInterrupts = 0x050,
            FifoCollisionMasks = 0x54
        }
    }
}
