//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;
using ELFSharp.ELF;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class PicoRV32 : RiscV32
    {
        public PicoRV32(Core.Machine machine, string cpuType, uint hartId = 0) : base(null, cpuType, machine, hartId, PrivilegeArchitecture.Priv1_09, Endianess.LittleEndian)
        {
        }

        public override void OnGPIO(int number, bool value)
        {
            // not implemented at the moment
        }
    }
}
