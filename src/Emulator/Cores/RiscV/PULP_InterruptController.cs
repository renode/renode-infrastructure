//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class PULP_InterruptController : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize, IIRQController
    {
        public PULP_InterruptController(Machine machine) : base(machine)
        {
            var irqs = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfOutgoingInterrupts; i++)
            {
                irqs[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);

            Registers.Mask.Define(this)
                .WithValueField(0, 32, writeCallback: (_, value) => mask.Value = value, valueProviderCallback: _ => mask.Value, name: "MASK")
            ;

            Registers.MaskSet.Define(this)
                .WithValueField(0, 32, out mask, FieldMode.Read | FieldMode.Set, name: "MASK_SET")
            ;

            Registers.MaskClear.Define(this)
                .WithValueField(0, 32, writeCallback: (_, value) => mask.Value &= ~value, valueProviderCallback: _ => mask.Value, name: "MASK_CLEAR")
            ;

            Registers.Interrupt.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => interrupt.Value, name: "INT")
            ;
            Registers.InterruptSet.Define(this)
                .WithValueField(0, 32, out interrupt, FieldMode.Read | FieldMode.Set, name: "INT_SET")
            ;

            Registers.InterruptClear.Define(this)
                .WithValueField(0, 32, writeCallback: (_, value) => interrupt.Value &= ~value, valueProviderCallback: _ => interrupt.Value, name: "INT_CLEAR")
            ;

            Registers.FIFO.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => TryGetEvent(), name: "FIFO")
            ;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            base.WriteDoubleWord(offset, value);
            Update();
        }

        public override void Reset()
        {
            base.Reset();
            Update();
            events.Clear();
        }

        public void OnGPIO(int number, bool value)
        {
            if(!value)
            {
                return;
            }
            // Temporarily treat low-number signals as events.
            // This is an approximation, subject to further investigation.
            if(number < EventBoundary)
            {
                events.Enqueue((uint)number);
                number = SoCEvent;
            }
            interrupt.Value |= 1u << number;
            Update();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public long Size => 0x800;

        private void Update()
        {
            var effectiveInterrupt = interrupt.Value & mask.Value;
            for(byte i = 0; i < NumberOfOutgoingInterrupts; i++)
            {
                Connections[i].Set(BitHelper.IsBitSet(effectiveInterrupt, i));
            }
        }

        private uint TryGetEvent()
        {
            if(!events.TryDequeue(out var result))
            {
                this.Log(LogLevel.Debug, "Trying to dequeue from an empty event list");
            }
            if(events.Count == 0)
            {
                interrupt.Value &= ~(1u << SoCEvent);
                Update();
            }
            return result;
        }

        private readonly Queue<uint> events = new Queue<uint>();
        private readonly IValueRegisterField mask;
        private readonly IValueRegisterField interrupt;

        private const int NumberOfOutgoingInterrupts = 32; //guess
        private const int SoCEvent = 26;
        private const int EventBoundary = 8;

        private enum Registers : long
        {
            Mask = 0x0,
            MaskSet = 0x4,
            MaskClear = 0x8,
            Interrupt = 0xC,
            InterruptSet = 0x10,
            InterruptClear = 0x14,
            Acknowledge = 0x18,
            AcknowledgeSet = 0x1C,
            AcknowledgeClear = 0x20,
            FIFO = 0x24
        }
    }
}
