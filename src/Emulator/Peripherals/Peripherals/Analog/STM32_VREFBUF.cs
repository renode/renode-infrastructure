//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32_VREFBUF : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        private bool isReady;

        public STM32_VREFBUF(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "ENVR", changeCallback: (_, val) => isReady = val)
                    .WithFlag(1, name: "HIZ")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => isReady, name: "VRR")
                    .WithValueField(4, 3, name: "VRS"),
                [0x04] = new DoubleWordRegister(this)
                    .WithValueField(0, 6, name: "TRIM")
            };
            registers = new DoubleWordRegisterCollection(this, map);
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            isReady = false;
            registers.Reset();
        }

        public long Size => 0x400;
    }
}
