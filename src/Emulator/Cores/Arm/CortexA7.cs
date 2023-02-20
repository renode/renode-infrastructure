//
// Copyright (c) 2010-2023 Antmicro
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
        public CortexA7(Machine machine, ARM_GenericTimer genericTimer, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian)
            : base("cortex-a15", machine, cpuId, endianness)
        {
            GenericTimer = genericTimer;
        }

        public override void Reset()
        {
            base.Reset();
        }

        public ARM_GenericTimer GenericTimer { get; }

        protected override void Write32CP15Inner(uint instruction, uint value)
        {
            var coprocessorRegisterN = (instruction >> 16) & 0xf;

            switch(coprocessorRegisterN)
            {
                case 14:
                    GenericTimer.WriteDoubleWordRegisterAArch32(instruction & GenericTimerDoubleWordRegisterMask, value);
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
                case 14:
                    return GenericTimer.ReadDoubleWordRegisterAArch32(instruction & GenericTimerDoubleWordRegisterMask);
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
                    return GenericTimer.ReadQuadWordRegisterAArch32(instruction & GenericTimerQuadWordRegisterMask);
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
                    GenericTimer.WriteQuadWordRegisterAArch32(instruction & GenericTimerQuadWordRegisterMask, value);
                    break;
                default:
                    base.Write64CP15Inner(instruction, value);
                    break;
            }
        }

        // Mask all cooprocessor instruction fields expect for opc1, CRn, opc2 and CRm
        private const uint GenericTimerDoubleWordRegisterMask = 0xef00ef;
        // Mask all cooprocessor instruction fields expect for opc1 and CRm
        private const uint GenericTimerQuadWordRegisterMask = 0x000000ff;
    }
}
