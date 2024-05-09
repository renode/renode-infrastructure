//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class MockWordPeripheralWithoutTranslations : IWordPeripheral
    {
        public MockWordPeripheralWithoutTranslations()
        {
            registers = new WordRegisterCollection(this, new Dictionary<long, WordRegister>{
                { 0x0, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x2, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x4, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x6, new WordRegister(this, 0x0).WithValueField(0, 16) },
            });
            regionRegisters = new WordRegisterCollection(this, new Dictionary<long, WordRegister>{
                { 0x0, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x2, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x4, new WordRegister(this, 0x0).WithValueField(0, 16) },
                { 0x6, new WordRegister(this, 0x0).WithValueField(0, 16) },
            });
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            regionRegisters.Reset();
        }

        public virtual ushort ReadWord(long offset)
        {
            return registers.Read(offset);
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegion("region")]
        public ushort ReadWordFromRegion(long offset)
        {
            return regionRegisters.Read(offset);
        }

        [ConnectionRegion("region")]
        public void WriteWordToRegion(long offset, ushort value)
        {
            regionRegisters.Write(offset, value);
        }

        private readonly WordRegisterCollection registers;
        private readonly WordRegisterCollection regionRegisters;
    }
}
