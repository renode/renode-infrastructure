//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class PULP_InterruptController : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize, IIRQController, IPeripheralContainer<PULP_EventController, NullRegistrationPoint>
    {
        public PULP_InterruptController(IMachine machine) : base(machine)
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
            interrupt.Value |= 1u << number;
            Update();
        }

        public void OnEvent(int number, bool value)
        {
            if(!value)
            {
                return;
            }
            events.Enqueue((uint)number);
            interrupt.Value |= 1u << SoCEvent;
            Update();
        }

        public void Register(PULP_EventController peripheral, NullRegistrationPoint registrationPoint)
        {
            if(eventController != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            eventController = peripheral;
        }

        public void Unregister(PULP_EventController peripheral)
        {
            if(eventController == null || eventController != peripheral)
            {
                throw new RegistrationException("The specified peripheral was never registered.");
            }
            machine.UnregisterAsAChildOf(this, peripheral);
            eventController = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(PULP_EventController peripheral)
        {
            return eventController != null ?
                new [] { NullRegistrationPoint.Instance } :
                Enumerable.Empty<NullRegistrationPoint>();
        }

        public IEnumerable<IRegistered<PULP_EventController, NullRegistrationPoint>> Children
        {
            get
            {
                return eventController != null ?
                    new [] { Registered.Create(eventController, NullRegistrationPoint.Instance) } :
                    Enumerable.Empty<IRegistered<PULP_EventController, NullRegistrationPoint>>();
            }
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

        private PULP_EventController eventController;

        private readonly Queue<uint> events = new Queue<uint>();
        private readonly IValueRegisterField mask;
        private readonly IValueRegisterField interrupt;

        private const int NumberOfOutgoingInterrupts = 32; //guess
        private const int SoCEvent = 26;

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
