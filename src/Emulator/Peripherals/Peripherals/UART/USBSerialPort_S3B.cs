//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class USBSerialPort_S3B : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public USBSerialPort_S3B(Machine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.DeviceId, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => DeviceId)
                },
                {(long)Registers.UsbPid, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => UsbPid)
                },
                {(long)Registers.RevisionNumber, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => RevisionNumber)
                },
                {(long)Registers.UsbToM4FifoFlags, new DoubleWordRegister(this)
                    // this is a simplification - as along as queue is not empty we say it has exactly one byte
                    .WithEnumField<DoubleWordRegister, FifoPopFlags>(0, 4, FieldMode.Read, valueProviderCallback: _ => Count != 0 ? FifoPopFlags.EntryCout1 : FifoPopFlags.Empty)
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.UsbToM4FifoReadData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                            return 0;
                        }
                        return character;
                    })
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.M4ToUsbFifoFlags, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, FifoPushFlags>(0, 4, FieldMode.Read, valueProviderCallback: _ => FifoPushFlags.Empty) // tx is always empty
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.M4ToUsbFifoWriteData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => this.TransmitCharacter((byte)value))
                    .WithReservedBits(8, 24)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x100;

        private uint DeviceId => 0xA5BD;
        private uint RevisionNumber => 0x0200;
        private uint UsbPid => 0x6141;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum FifoPopFlags
        {
            Empty = 0b0000,
            EntryCout1 = 0b0001,
            AtLeast2Entries = 0b0010,
            AtLeast4Entries = 0b0011,
            AtLeast8Entries = 0b0100,
            AtLeast16Entries = 0b0101,
            AtLeast32Entries = 0b0110,
            LessThan1_4To64Entries = 0b1000,
            Fill1_4OrMore = 0b1101,
            Fill1_2OrMore = 0b1110,
            Full = 0b1111,
            // others - reserved
        }

        private enum FifoPushFlags
        {
            Full = 0b0000,
            Empty = 0b0001,
            RoomForMoreThan1_2 = 0b0010,
            RoomForMoreThan1_4 = 0b0011,
            RoomForLessThan1_4To64 = 0b0100,
            RoomFor32To63 = 0b1010,
            RoomFor16To31 = 0b1011,
            RoomFor8To15 = 0b1100,
            RoomFor4to7 = 0b1101,
            RoomForAtLeast2 = 0b1110,
            RoomForAtLeast1 = 0b1111,
            // others -reserved
        }

        private enum Registers : long
        {
            DeviceId = 0x00,
            RevisionNumber = 0x04,
            UsbPid = 0x10,
            UsbToM4FifoFlags = 0x40,
            UsbToM4FifoReadData = 0x44,
            M4ToUsbFifoFlags = 0x80,
            M4ToUsbFifoWriteData = 0x84,
        }
    }
}
