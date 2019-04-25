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
    public partial class VexRiscv : RiscV32
    {
        public VexRiscv(Core.Machine machine, uint hartId = 0) : base(null, "rv32im", machine, hartId, PrivilegeArchitecture.Priv1_09, Endianess.LittleEndian)
        {
            TlibSetCsrValidation(0);

            RegisterCSR((ulong)CSRs.IrqMask, () => (ulong)irqMask, value => { irqMask = (uint)value; Update(); });
            RegisterCSR((ulong)CSRs.IrqPending, () => (ulong)irqPending, value => { irqPending = (uint)value; Update(); });
            RegisterCSR((ulong)CSRs.DCacheInfo, () => (ulong)dCacheInfo, value => dCacheInfo = (uint)value);
        }

        public override void OnGPIO(int number, bool value)
        {
            BitHelper.SetBit(ref irqPending, (byte)number, value);
            Update();
        }

        private void Update()
        {
            //We support only Machine mode, with external interrupts. An additional interrupt controller would be required to have more advanced handling.
            base.OnGPIO((int)IrqType.MachineExternalInterrupt, (irqPending & irqMask) != 0);
        }

        private uint irqMask;
        private uint irqPending;
        private uint dCacheInfo;

        private enum CSRs
        {
            IrqMask = 0xBC0,
            IrqPending = 0xFC0,
            DCacheInfo = 0xCC0
        }
    }
}
