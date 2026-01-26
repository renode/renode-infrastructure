//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class FocalTechFT9001_Reset : IDoubleWordPeripheral, IBytePeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public FocalTechFT9001_Reset(IMachine machine)
        {
            this.machine = machine;
            ByteRegisters = new ByteRegisterCollection(this);
            DoubleWordRegisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();

            powerOnResetCause.Value = true;
        }

        public void Reset()
        {
            DoubleWordRegisters.Reset();
            ByteRegisters.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return DoubleWordRegisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            DoubleWordRegisters.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return ByteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            ByteRegisters.Write(offset, value);
        }

        public long Size => 0x1000;

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => DoubleWordRegisters;

        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => ByteRegisters;

        private void RequestReset()
        {
            machine.RequestReset();
        }

        private void DefineRegisters()
        {
            var this_dword = (IProvidesRegisterCollection<DoubleWordRegisterCollection>)this;
            var this_byte = (IProvidesRegisterCollection<ByteRegisterCollection>)this;

            Registers.ResetControlRegister.Define32(this_dword)
                .WithFlag(31, FieldMode.Write, name: "SOFTRST", writeCallback: (_, val) => { if(val) RequestReset(); });
            Registers.ResetStatusRegister.Define8(this_byte)
                .WithFlag(3, out powerOnResetCause, name: "RSTCAUSE_POR");
        }

        private ByteRegisterCollection ByteRegisters { get; }

        private DoubleWordRegisterCollection DoubleWordRegisters { get; }

        private IFlagRegisterField powerOnResetCause;

        private readonly IMachine machine;

        private enum Registers
        {
            ResetControlRegister = 0x0,
            LVDCR = 0x4,
            HVDCR = 0x5,
            RTR = 0x6,
            ResetStatusRegister = 0x7
        }
    }
}
