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
    public class MockRegister : IPeripheralRegister<ICPU, NullRegistrationPoint>, IDoubleWordPeripheral, IKnownSize
    {
        public MockRegister(IMachine machine)
        {
            this.machine = machine;
        }

        public void Register(ICPU peripheral, NullRegistrationPoint registrationPoint)
        {
            if(isRegistered)
            {
                throw new ArgumentException("Child is already registered.");
            }
            else
            {
                isRegistered = true;
                machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            }
        }

        public void Unregister(ICPU peripheral)
        {
            isRegistered = false;
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

        public long Size
        {
            get 
            {
                return 0x4;
            }
        }

        private bool isRegistered;
        private IMachine machine;
    }
}
