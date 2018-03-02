//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals
{
    public class InterruptHandler<TRegister, TFlag>
    {
        public InterruptHandler(IGPIO gpio)
        {
            irqs = new Dictionary<TRegister, IrqState>();
            flagToRegister = new Dictionary<TFlag, FlagState>();
            this.gpio = gpio;
        }

        public void Reset()
        {
            foreach(var irq in irqs)
            {
                irq.Value.Value = 0;
                irq.Value.Mask = 0;
            }
            gpio.Unset();
        }

        public void RegisterInterrupt(TRegister register, TFlag flag, byte position, bool masked = false)
        {
            if(!irqs.ContainsKey(register))
            {
                irqs[register] = new IrqState();
            }

            flagToRegister.Add(flag, new FlagState { Position = position, Register = register });
            if(masked)
            {
                BitHelper.SetBit(ref irqs[register].Mask, position, true);
            }
        }

        public void RequestInterrupt(TFlag flag)
        {
            var reg = flagToRegister[flag];
            BitHelper.SetBit(ref irqs[reg.Register].Value, reg.Position, true);
            Refresh();
        }

        public uint GetRegisterValue(TRegister register)
        {
            return irqs[register].Value;
        }

        public void SetRegisterValue(TRegister register, uint value)
        {
            irqs[register].Value = value;
            Refresh();
        }

        public uint GetRegisterMask(TRegister register)
        {
            return irqs[register].Mask;
        }

        public void SetRegisterMask(TRegister register, uint value)
        {
            irqs[register].Mask = value;
            Refresh();
        }

        public void Refresh()
        {
            foreach(var flag in flagToRegister)
            {
                if(BitHelper.IsBitSet(irqs[flag.Value.Register].EffectiveValue, flag.Value.Position))
                {
                    gpio.Set(true);
                    return;
                }
            }
            gpio.Set(false);
        }

        private readonly Dictionary<TRegister, IrqState> irqs;
        private readonly Dictionary<TFlag, FlagState> flagToRegister;
        private readonly IGPIO gpio;

        private class IrqState
        {
            public uint Value;
            public uint Mask;

            public uint EffectiveValue { get { return Value & Mask; } }
        }

        private class FlagState
        {
            public TRegister Register;
            public byte Position;
        }
    }
}
