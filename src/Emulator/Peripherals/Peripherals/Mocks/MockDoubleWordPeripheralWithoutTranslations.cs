//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class MockDoubleWordPeripheralWithoutTranslations : IDoubleWordPeripheral
    {
        public MockDoubleWordPeripheralWithoutTranslations()
        {
            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>{
                { 0x0, new DoubleWordRegister(this, 0x0).WithValueField(0, 32) },
                { 0x4, new DoubleWordRegister(this, 0x0).WithValueField(0, 32) },
            });
            regionRegisters = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>{
                { 0x0, new DoubleWordRegister(this, 0x0).WithValueField(0, 32) },
                { 0x4, new DoubleWordRegister(this, 0x0).WithValueField(0, 32) },
            });
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            regionRegisters.Reset();
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegion("region")]
        public uint ReadDoubleWordFromRegion(long offset)
        {
            return regionRegisters.Read(offset);
        }

        [ConnectionRegion("region")]
        public void WriteDoubleWordToRegion(long offset, uint value)
        {
            regionRegisters.Write(offset, value);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly DoubleWordRegisterCollection regionRegisters;
    }
}
