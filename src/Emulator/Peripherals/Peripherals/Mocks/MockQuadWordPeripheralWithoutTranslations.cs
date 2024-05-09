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
    public class MockQuadWordPeripheralWithoutTranslations : IQuadWordPeripheral
    {
        public MockQuadWordPeripheralWithoutTranslations()
        {
            registers = new QuadWordRegisterCollection(this, new Dictionary<long, QuadWordRegister>{
                { 0x0, new QuadWordRegister(this, 0x0).WithValueField(0, 64) },
            });
            regionRegisters = new QuadWordRegisterCollection(this, new Dictionary<long, QuadWordRegister>{
                { 0x0, new QuadWordRegister(this, 0x0).WithValueField(0, 64) },
            });
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            regionRegisters.Reset();
        }

        public virtual ulong ReadQuadWord(long offset)
        {
            return registers.Read(offset);
        }

        public virtual void WriteQuadWord(long offset, ulong value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegion("region")]
        public ulong ReadQuadWordFromRegion(long offset)
        {
            return regionRegisters.Read(offset);
        }

        [ConnectionRegion("region")]
        public void WriteQuadWordToRegion(long offset, ulong value)
        {
            regionRegisters.Write(offset, value);
        }

        private readonly QuadWordRegisterCollection registers;
        private readonly QuadWordRegisterCollection regionRegisters;
    }
}
