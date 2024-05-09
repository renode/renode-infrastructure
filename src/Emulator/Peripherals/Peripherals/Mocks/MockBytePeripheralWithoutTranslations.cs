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
    public class MockBytePeripheralWithoutTranslations : IBytePeripheral
    {
        public MockBytePeripheralWithoutTranslations()
        {
            registers = new ByteRegisterCollection(this, new Dictionary<long, ByteRegister>{
                { 0x0, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x1, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x2, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x3, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x4, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x5, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x6, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x7, new ByteRegister(this, 0x0).WithValueField(0, 8) },
            });
            regionRegisters = new ByteRegisterCollection(this, new Dictionary<long, ByteRegister>{
                { 0x0, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x1, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x2, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x3, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x4, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x5, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x6, new ByteRegister(this, 0x0).WithValueField(0, 8) },
                { 0x7, new ByteRegister(this, 0x0).WithValueField(0, 8) },
            });
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            regionRegisters.Reset();
        }

        public virtual byte ReadByte(long offset)
        {
            return registers.Read(offset);
        }

        public virtual void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegion("region")]
        public byte ReadByteFromRegion(long offset)
        {
            return regionRegisters.Read(offset);
        }

        [ConnectionRegion("region")]
        public void WriteByteToRegion(long offset, byte value)
        {
            regionRegisters.Write(offset, value);
        }

        private readonly ByteRegisterCollection registers;
        private readonly ByteRegisterCollection regionRegisters;
    }
}
