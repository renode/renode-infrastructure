//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Reflection;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public partial class ARM_SMMUv3
    {
        private class WrappingQueue<T>
        {
            public WrappingQueue(ARM_SMMUv3 smmu, IBusController sysbus, Role smmuRole,
                IValueRegisterField baseAddr, IValueRegisterField sizeShift,
                IValueRegisterField produce, IValueRegisterField consume,
                Func<IList<byte>, Type> typeSelector = null)
            {
                if(typeof(T).GetCustomAttribute<WidthAttribute>() == null)
                {
                    throw new ArgumentException($"Queue element type {typeof(T)} must have {nameof(WidthAttribute)} defined");
                }

                this.smmu = smmu;
                this.sysbus = sysbus;
                this.smmuRole = smmuRole;
                this.baseAddr = baseAddr;
                this.sizeShift = sizeShift;
                this.produce = produce;
                this.consume = consume;
                this.typeSelector = typeSelector ?? (_ => typeof(T));
                this.elementSize = (ulong)Packet.CalculateLength<T>();
            }

            public bool TryPeek(out T element)
            {
                if(smmuRole == Role.Producer)
                {
                    throw new InvalidOperationException("Cannot peek element when SMMU role is Producer");
                }

                element = default(T);
                if(IsEmpty)
                {
                    return false;
                }

                var address = BaseAddress + ConsumerIndex * elementSize;
                element = smmu.ReadSubclass<T>(address, typeSelector);
                return true;
            }

            public void AdvanceConsumerIndex()
            {
                if(smmuRole == Role.Producer)
                {
                    throw new InvalidOperationException("Cannot advance consumption when SMMU role is Producer");
                }

                if(IsEmpty)
                {
                    return;
                }

                var newIndex = (ConsumerIndex + 1) & Mask;
                var newWrap = ConsumerWrap;
                if(newIndex == 0)
                {
                    newWrap = !newWrap;
                }
                consume.Value = newIndex | (newWrap ? Size : 0);
            }

            public bool TryEnqueue(T element)
            {
                if(smmuRole == Role.Consumer)
                {
                    throw new InvalidOperationException("Cannot enqueue an element when SMMU role is Consumer");
                }

                if(IsFull)
                {
                    smmu.WarningLog("{0} queue is full, cannot enqueue {1}", typeof(T).Name, element);
                    return false;
                }

                var producerIndex = ProducerIndex;
                var address = BaseAddress + producerIndex * elementSize;
                sysbus.WriteBytes(Packet.Encode(element), address, context: smmu.Context);

                var newIndex = (producerIndex + 1) & Mask;
                var newWrap = ProducerWrap;
                if(newIndex == 0)
                {
                    newWrap = !newWrap;
                }
                produce.Value = newIndex | (newWrap ? Size : 0);
                return true;
            }

            public bool IsEmpty => ProducerIndex == ConsumerIndex && ProducerWrap == ConsumerWrap;

            public bool IsFull => ProducerIndex == ConsumerIndex && ProducerWrap != ConsumerWrap;

            private ulong BaseAddress => baseAddr.Value << 5;

            private uint Size => 1u << (int)sizeShift.Value; // Also a mask for the wrap bit

            private uint Mask => (uint)Size - 1;

            private uint ProducerIndex => (uint)produce.Value & Mask;

            private bool ProducerWrap => ((uint)produce.Value & Size) != 0;

            private uint ConsumerIndex => (uint)consume.Value & Mask;

            private bool ConsumerWrap => ((uint)consume.Value & Size) != 0;

            private readonly ARM_SMMUv3 smmu;
            private readonly IBusController sysbus;
            private readonly Role smmuRole;
            private readonly Func<IList<byte>, Type> typeSelector;
            private readonly ulong elementSize;

            private readonly IValueRegisterField baseAddr;
            private readonly IValueRegisterField sizeShift;
            private readonly IValueRegisterField produce;
            private readonly IValueRegisterField consume;

            public enum Role
            {
                Producer,
                Consumer,
            }
        }
    }
}