//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class ESP32_USBSerialJTAG : UARTBase, IDoubleWordPeripheral, IBytePeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IHasMappedRegisters, IKnownSize
    {
        public ESP32_USBSerialJTAG(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
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

        // Byte accesses are handled here rather than through AllowedTranslation.ByteToDoubleWord,
        // because that translation implements a byte write as a read-modify-write of the whole
        // word - which on EP1 would pop a character off the receive FIFO for every character sent.
        public byte ReadByte(long offset)
        {
            if(offset % 4 != 0)
            {
                this.Log(LogLevel.Warning, "Unhandled byte read from offset 0x{0:X}; only word-aligned byte accesses are supported", offset);
                return 0;
            }
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset % 4 != 0)
            {
                this.Log(LogLevel.Warning, "Unhandled byte write to offset 0x{0:X}; only word-aligned byte accesses are supported", offset);
                return;
            }
            WriteDoubleWord(offset, value);
        }

        public string OffsetToString(long offset) => registerMapper.ToString(offset);

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x100;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
        }

        protected override void QueueEmptied()
        {
        }

        private void DefineRegisters()
        {
            Registers.Ep1.Define(this)
                .WithValueField(0, 8, name: "RDWR_BYTE",
                    writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                    valueProviderCallback: _ => TryGetCharacter(out var character) ? character : (byte)0)
                .WithReservedBits(8, 24);

            Registers.Ep1Config.Define(this, 0x2)
                // Transmission is instant, so there is nothing left to flush.
                .WithFlag(0, name: "WR_DONE")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "SERIAL_IN_EP_DATA_FREE")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => Count > 0, name: "SERIAL_OUT_EP_DATA_AVAIL")
                .WithReservedBits(3, 29);
        }

        private readonly RegisterMapper registerMapper = new RegisterMapper(typeof(Registers));

        private enum Registers : long
        {
            Ep1          = 0x000, // USB_SERIAL_JTAG_EP1
            Ep1Config    = 0x004, // USB_SERIAL_JTAG_EP1_CONF
            IntRaw       = 0x008, // USB_SERIAL_JTAG_INT_RAW
            IntStatus    = 0x00C, // USB_SERIAL_JTAG_INT_ST
            IntEnable    = 0x010, // USB_SERIAL_JTAG_INT_ENA
            IntClear     = 0x014, // USB_SERIAL_JTAG_INT_CLR
            Conf0        = 0x018, // USB_SERIAL_JTAG_CONF0
            Test         = 0x01C, // USB_SERIAL_JTAG_TEST
            JfifoStatus  = 0x020, // USB_SERIAL_JTAG_JFIFO_ST
            FrameNum     = 0x024, // USB_SERIAL_JTAG_FRAM_NUM
            InEp0Status  = 0x028, // USB_SERIAL_JTAG_IN_EP0_ST
            InEp1Status  = 0x02C, // USB_SERIAL_JTAG_IN_EP1_ST
            InEp2Status  = 0x030, // USB_SERIAL_JTAG_IN_EP2_ST
            InEp3Status  = 0x034, // USB_SERIAL_JTAG_IN_EP3_ST
            OutEp0Status = 0x038, // USB_SERIAL_JTAG_OUT_EP0_ST
            OutEp1Status = 0x03C, // USB_SERIAL_JTAG_OUT_EP1_ST
            OutEp2Status = 0x040, // USB_SERIAL_JTAG_OUT_EP2_ST
            MiscConfig   = 0x044, // USB_SERIAL_JTAG_MISC_CONF
            MemConfig    = 0x048, // USB_SERIAL_JTAG_MEM_CONF
            Date         = 0x080, // USB_SERIAL_JTAG_DATE
        }
    }
}
