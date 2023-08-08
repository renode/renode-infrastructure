//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class NullRegister : IPeripheralRegister<ICPU, NullRegistrationPoint>, IDoubleWordPeripheral
    {
        public NullRegister(IMachine machine)
        {
            this.machine = machine;
        }

        public void Register(ICPU peripheral, NullRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(ICPU peripheral)
        {
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public void Reset()
        {
            
        }

        public uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            
        }

        private readonly IMachine machine;
    }
}
