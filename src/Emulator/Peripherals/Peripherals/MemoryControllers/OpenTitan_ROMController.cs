//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class OpenTitan_ROMController : IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_ROMController(MappedMemory rom)
        {
            this.rom = rom;
            expectedDigest = new IValueRegisterField[NumberOfDigestRegisters];
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            fatalTriggered = false;
            InitializeDigestRegisters();
        }

        public long Size => 0x1000;

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.AlertTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { fatalTriggered |= value; }, name: "fatal")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.FatalAlertCause, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => fatalTriggered, name: "checker_error")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => fatalTriggered, name: "integrity_error")
                    .WithReservedBits(2, 30)
                },
            };

            for(var i = 0; i < NumberOfDigestRegisters; ++i)
            {
                registersDictionary.Add((long)Registers.Digest0 + i * 4, new DoubleWordRegister(this)
                    .WithTag($"DIGEST_{i}", 0, 32));
                registersDictionary.Add((long)Registers.ExpectedDigest0 + i * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out expectedDigest[i], FieldMode.Read, name: $"EXP_DIGEST_{i}"));
            }

            return registersDictionary;
        }

        private void InitializeDigestRegisters()
        {
            for(var i = 0; i < numberOfDigestRegisters; ++i)
            {
                expectedDigest[i].Value = rom.ReadDoubleWord(rom.Size + (i - numberOfDigestRegisters) * 4);
            }
        }

        private readonly MappedMemory rom;
        private readonly DoubleWordRegisterCollection registers;

        private readonly IValueRegisterField[] expectedDigest;

        private bool fatalTriggered;

        private const int numberOfDigestRegisters = 8;

        private enum Registers : long
        {
            AlertTest       = 0x00,
            FatalAlertCause = 0x04,
            Digest0         = 0x08,
            Digest1         = 0x0C,
            Digest2         = 0x10,
            Digest3         = 0x14,
            Digest4         = 0x18,
            Digest5         = 0x1C,
            Digest6         = 0x20,
            Digest7         = 0x24,
            ExpectedDigest0 = 0x28,
            ExpectedDigest1 = 0x2C,
            ExpectedDigest2 = 0x30,
            ExpectedDigest3 = 0x34,
            ExpectedDigest4 = 0x38,
            ExpectedDigest5 = 0x3C,
            ExpectedDigest6 = 0x40,
            ExpectedDigest7 = 0x44,
        }
    }
}
