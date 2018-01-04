//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public sealed class CortexA7 : Arm
    {
        public CortexA7(Machine machine, GIC gic, ulong genericTimerCompareValue, Endianess endianness = Endianess.LittleEndian) : base("cortex-a15", machine, endianness)
        {
            genericTimer = new CortexAGenericTimer(machine, gic, genericTimerCompareValue);
        }

        protected override void Write32CP15Inner(uint instruction, uint value)
        {
            var coprocessorRegisterN = (instruction >> 16) & 0xf;

            switch(coprocessorRegisterN)
            {
            case 14:
                genericTimer.WriteRegister(instruction, value);
                break;
            default:
                base.Write32CP15Inner(instruction, value);
                break;
            }
        }

        protected override uint Read32CP15Inner(uint instruction)
        {
            var coprocessorRegisterN = (instruction >> 16) & 0xf;

            switch(coprocessorRegisterN)
            {
            case 14: // Timer
                return (uint)genericTimer.ReadRegister(instruction);
            default:
                return base.Read32CP15Inner(instruction);
            }
        }

        protected override ulong Read64CP15Inner(uint instruction)
        {
            var coprocessorRegisterM = instruction & 0xf;

            switch(coprocessorRegisterM)
            {
            case 14:
                var result = genericTimer.ReadRegister(instruction);
                return result;
            default:
                return base.Read64CP15Inner(instruction);
            }
        }

        protected override void Write64CP15Inner(uint instruction, ulong value)
        {
            var coprocessorRegisterM = instruction & 0xf;

            switch(coprocessorRegisterM)
            {
            case 14:
                genericTimer.WriteRegister(instruction, value);
                break;
            default:
                base.Write64CP15Inner(instruction, value);
                break;
            }
        }

        private readonly CortexAGenericTimer genericTimer;
    }
}