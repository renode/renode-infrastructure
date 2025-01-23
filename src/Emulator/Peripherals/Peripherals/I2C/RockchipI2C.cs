//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

// minimal model compliant with u-boot rk_i2c driver
// does nothing except facilitating probe
namespace Antmicro.Renode.Peripherals.I2C
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RockchipI2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public RockchipI2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public long Size => 0x1000;
        [IrqProvider]
        public GPIO IRQ { get; private set; }

        private void DefineRegisters()
        {
            Registers.Con.Define(this)
                .WithTaggedFlag("i2c_en", 0)
                .WithTag("i2c_mode", 1, 2)
                .WithFlag(3, out startTransmission, name: "start")
                .WithFlag(4, out stopTransmission, name: "stop")
                .WithTaggedFlag("ack", 5)
                .WithTaggedFlag("act2nak", 6)
                .WithReservedBits(7, 25)
            ;

            Registers.MrxAddr.Define(this)
                .WithTaggedFlag("Read/Write", 0)
                .WithTag("Master address register", 1, 23)
                .WithTaggedFlag("Address low byte valid (addlvld)", 24)
                .WithTaggedFlag("address middle byte valid (addmvld)", 25)
                .WithTaggedFlag("address high byte valid (addhvld)", 26)
                .WithReservedBits(27, 5)
            ;

            Registers.ClkDiv.Define(this)
                .WithTag("SCL low level clock count (CLKDIVL)", 0, 16)
                .WithTag("SCL high level clock count (CLKDIVH)", 16, 16)
            ;

            Registers.Ien.Define(this)
                .WithTaggedFlag("Byte trasmit finished interrupt enable (btfien)", 0)
                .WithTaggedFlag("Byte receive finished interrupt enable (brfien)", 1)
                .WithTaggedFlag("MTXCNT data transmit finished interrupt enable (mbtfien)", 2)
                .WithTaggedFlag("MRXCNT data received finished interrupt enable (mbrfien)", 3)
                .WithTaggedFlag("Start operation finished interrupt enable (startien)", 4)
                .WithTaggedFlag("Stop operation finished interrupt enable (stopien)", 5)
                .WithTaggedFlag("NAK handshake received interrupt enable (nakrcvien)", 6)
                .WithReservedBits(7, 25)
            ;

            Registers.Ipd.Define(this)
                .WithTaggedFlag("Byte trasmit finished interrupt pending bit (btfipd)", 0)
                .WithTaggedFlag("Byte receive finished interrupt pending bit (brfipd)", 1)
                .WithFlag(2, name: "MTXCNT data transmit finished interrupt pending bit (mbtfipd)", valueProviderCallback: _ => true)
                .WithFlag(3, name: "MRXCNT data received finished interrupt pending bit (mbrfipd)", valueProviderCallback: _ => true)
                .WithFlag(4, name: "Start operation finished interrupt pending bit (startipd)", valueProviderCallback: _ => startTransmission.Value)
                .WithFlag(5, name: "Stop operation finished interrupt pending bit (stopipd)", valueProviderCallback: _ => stopTransmission.Value)
                .WithTaggedFlag("NAK handshake received interrupt pending bit (nakrcvipd)", 6)
                .WithReservedBits(7, 25)
            ;
        }

        private IFlagRegisterField stopTransmission;
        private IFlagRegisterField startTransmission;
        private enum Registers
        {
            Con = 0x0, // config
            ClkDiv = 0x04, // clock divisor
            MrxAddr = 0x08, // slave address accessed
            Ien = 0x18, // interrupt enabled
            Ipd = 0x1C, // interrupt pending
            TxData0 = 0x100,
        }
    }
}
