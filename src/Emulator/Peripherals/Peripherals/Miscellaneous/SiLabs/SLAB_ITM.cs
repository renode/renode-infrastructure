using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SLAB_ITM : UARTBase, IDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public SLAB_ITM(Machine machine) : base(machine)
        {
        }

        public override Parity ParityBit { get { return Parity.None; } }

        public override Bits StopBits { get { return Bits.One; } }

        public override uint BaudRate { get { return 115200; } }

        public long Size { get { return 0x4000; } }

        public uint ReadDoubleWord(long offset)
        {
            switch ((Registers)offset)
            {
                case Registers.STIM0:
                    return 1;   // The stimulus port is ready to accept one piece of data.
                case Registers.TER0:
                    return 1;   // Stimulus port0 enabled
                case Registers.TCR:
                    return 1;   // ITM enabled
                default:
                    this.Log(LogLevel.Warning, "This read access is not implemented.");
                    return 0;
            }
        }

        public void WriteByte(long offset, byte value)
        {
            switch ((Registers)offset)
            {
                case Registers.STIM0:
                    TransmitCharacter(value);
                    break;
                case Registers.TER0:
                case Registers.TCR:
                    break;
                default:
                    this.Log(LogLevel.Warning, "This byte write access is not implemented 0x{0:X}.", offset);
                    break;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            //this.Log(LogLevel.Warning, "writing doubleWord to non existing offset {0:X}, case : {1:X}", offset, offset & 0xF000);
            switch ((Registers)offset)
            {
                case Registers.STIM0:
                    TransmitCharacter((byte)value);
                    break;
                case Registers.TER0:
                case Registers.TCR:
                    break;
                default:
                    this.Log(LogLevel.Warning, "This double word write access is not implemented 0x{0:X}.", offset);
                    break;
            }
        }

        protected override void CharWritten()
        {
            // do nothing
        }

        protected override void QueueEmptied()
        {
            // do nothing
        }

        byte IBytePeripheral.ReadByte(long offset)
        {
            throw new NotImplementedException();
        }

        private enum Registers
        {
            STIM0 = 0x000,
            TER0 = 0xE00,
            TCR = 0xE80,
        }
    }
}
