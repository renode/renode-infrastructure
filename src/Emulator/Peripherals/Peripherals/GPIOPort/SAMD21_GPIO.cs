//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class SAMD21_GPIO : BaseGPIOPort, IBytePeripheral, IDoubleWordPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public SAMD21_GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            configurationRegister = new DoubleWordRegister(this);

            pinIsOutput = new bool[NumberOfPins];
            pinInputEnabled = new bool[NumberOfPins];

            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset != (long)Registers.WriteConfiguration)
            {
                return this.ReadDoubleWordUsingByte(offset);
            }
            // NOTE: We are using additional DoubleWordRegister for WriteConfiguration (0x28)
            //       to simplify implementation of transactions
            return configurationRegister.Read();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset != (long)Registers.WriteConfiguration)
            {
                this.WriteDoubleWordUsingByte(offset, value);
                return;
            }
            // NOTE: We are using additional DoubleWordRegister for WriteConfiguration (0x28)
            //       to simplify implementation of transactions
            configurationRegister.Write(0, value);
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public long Size => 0x80;

        private void DefineRegisters()
        {
            Registers.Direction.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"DIR[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => pinIsOutput[index * 8 + j],
                    writeCallback: (j, _, value) => pinIsOutput[index * 8 + j] = value);
            });

            Registers.DirectionClear.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"DIRCLR[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => pinIsOutput[index * 8 + j],
                    writeCallback: (j, _, value) => { if(value) pinIsOutput[index * 8 + j] = false; });
            });

            Registers.DirectionSet.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"DIRSET[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => pinIsOutput[index * 8 + j],
                    writeCallback: (j, _, value) => { if(value) pinIsOutput[index * 8 + j] = true; });
            });

            Registers.DirectionToggle.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"DIRTGL[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => pinIsOutput[index * 8 + j],
                    writeCallback: (j, _, value) => { if(value) pinIsOutput[index * 8 + j] ^= true; });
            });

            Registers.Output.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"OUT[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => Connections[index * 8 + j].IsSet,
                    writeCallback: (j, _, value) => Connections[index * 8 + j].Set(value));
            });

            Registers.OutputClear.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"OUTCLR[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => Connections[index * 8 + j].IsSet,
                    writeCallback: (j, _, value) => { if(value) Connections[index * 8 + j].Unset(); });
            });

            Registers.OutputSet.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"OUTSET[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => Connections[index * 8 + j].IsSet,
                    writeCallback: (j, _, value) => { if(value) Connections[index * 8 + j].Set(); });
            });

            Registers.OutputToggle.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, name: $"OUTTGL[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => Connections[index * 8 + j].IsSet,
                    writeCallback: (j, _, value) => { if(value) Connections[index * 8 + j].Toggle(); });
            });

            Registers.Input.DefineMany(this, 4, (register, index) =>
            {
                register.WithFlags(0, 8, FieldMode.Read, name: $"IN[{(index+1)*8-1}:{index*8}]",
                    valueProviderCallback: (j, _) => pinInputEnabled[index * 8 + j] && State[index * 8 + j]);
            });

            configurationRegister
                .WithValueField(0, 16, out var pinMask, name: "PINMASK")
                .WithTaggedFlag("PMUXEN", 16)
                .WithFlag(17, out var pinInputBuffer, name: "INEN")
                .WithTaggedFlag("PULLEN", 18)
                .WithReservedBits(19, 3)
                .WithTaggedFlag("DRVSTR", 22)
                .WithReservedBits(23, 1)
                .WithTag("PMUX", 24, 4)
                .WithTaggedFlag("WRPMUX", 28)
                .WithReservedBits(29, 1)
                .WithFlag(30, out var writePinConfig, name: "WRPINCFG")
                .WithFlag(31, out var halfWordSelect, name: "HWSEL")
                .WithWriteCallback((_, __) =>
                {
                    if(!writePinConfig.Value)
                    {
                        return;
                    }

                    var mask = pinMask.Value << (halfWordSelect.Value ? 16 : 0);
                    BitHelper.ForeachActiveBit(mask, index =>
                    {
                        pinInputEnabled[index] = pinInputBuffer.Value;
                    });
                });

            Registers.PinConfiguration.DefineMany(this, NumberOfPins, (register, index) =>
            {
                register
                    .WithTaggedFlag("PMUXEN", 0)
                    .WithFlag(1, name: "INEN",
                        valueProviderCallback: _ => pinInputEnabled[index],
                        writeCallback: (_, value) => pinInputEnabled[index] = value)
                    .WithTaggedFlag("PULLEN", 2)
                    .WithReservedBits(3, 3)
                    .WithTaggedFlag("DRVSTR", 6)
                    .WithReservedBits(7, 1)
                ;
            });
        }

        public const int NumberOfPins = 32;

        private bool[] pinIsOutput;
        private bool[] pinInputEnabled;
        private readonly DoubleWordRegister configurationRegister;

        private enum Registers
        {
            Direction = 0x00,
            DirectionClear = 0x04,
            DirectionSet = 0x08,
            DirectionToggle = 0xC,
            Output = 0x10,
            OutputClear = 0x14,
            OutputSet = 0x18,
            OutputToggle = 0x1C,
            Input = 0x20,
            Control = 0x24,
            WriteConfiguration = 0x28,
            PeripheralMultiplexing = 0x2C,
            PinConfiguration = 0x40,
        }
    }
}
