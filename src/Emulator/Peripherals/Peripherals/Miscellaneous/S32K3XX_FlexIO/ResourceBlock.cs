//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public interface IResourceBlockOwner : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IEmulationElement { }

    public abstract class ResourceBlock
    {
        public ResourceBlock(IResourceBlockOwner owner, uint identifier)
        {
            this.owner = owner;
            Identifier = identifier;
        }

        public virtual void Reset()
        {
            foreach(var interrupt in Interrupts)
            {
                interrupt.Reset();
            }
        }

        public uint Identifier { get; }
        public abstract string Name { get; }
        public abstract IEnumerable<Interrupt> Interrupts { get; }

        public event Action<bool> AnyInterruptChanged;

        protected void OnInterruptChange(bool value)
        {
            AnyInterruptChanged?.Invoke(value);
        }

        protected readonly IResourceBlockOwner owner;
    }
}
