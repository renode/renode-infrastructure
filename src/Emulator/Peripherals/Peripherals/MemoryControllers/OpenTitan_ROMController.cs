//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class OpenTitan_ROMController : IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_ROMController(MappedMemory rom, ulong nonce, ulong keyLow, ulong keyHigh)
        {
            if(rom.Size <= 0)
            {
                throw new ConstructionException("Provided rom's size has to be greater than zero");
            }
            if(rom.Size % 4 != 0)
            {
                throw new ConstructionException("Provided rom's size has to be divisible by word size (4)");
            }

            this.rom = rom;
            this.keyLow = keyLow;
            this.keyHigh = keyHigh;
            romIndexWidth = BitHelper.GetMostSignificantSetBitIndex((ulong)rom.Size / 4 - 1) + 1;
            addressKey = nonce >> (64 - romIndexWidth);
            dataNonce = nonce << romIndexWidth;
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

        public void Load(string fileName)
        {
            try
            {
                using(var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    var size = (ulong)rom.Size / 4;
                    var buffer = new byte[8];
                    var read = 0;
                    for(var scrambledIndex = 0UL; scrambledIndex < size; ++scrambledIndex)
                    {
                        read = reader.Read(buffer, 0, WordSizeWithECC);
                        if(read == 0)
                        {
                            throw new RecoverableException($"Error while loading file {fileName}: file is too small");
                        }
                        if(read != WordSizeWithECC)
                        {
                            throw new RecoverableException($"Error while loading file {fileName}: the number of bytes in a file must be a multiple of {WordSizeWithECC}");
                        }

                        var data = BitConverter.ToUInt64(buffer, 0);
                        var index = PRESENTCipher.Descramble(scrambledIndex, addressKey, romIndexWidth, NumberOfScramblingRounds);
                        if(index >= size)
                        {
                            throw new RecoverableException($"Error while loading file {fileName}: decoded offset (0x{(index * 4):X}) is out of bounds [0;0x{rom.Size:X})");
                        }
                        if(index >= size - 8)
                        {
                            // top 8 (by logical addressing) words are 256-bit expected hash, stored not scrambled.
                            expectedDigest[index - size + 8].Value = (uint)data;
                        }

                        // data's width is 32 bits of proper data and 7 bits of ECC
                        var dataPresent = PRESENTCipher.Descramble(data, 0, 32 + 7, NumberOfScramblingRounds);
                        var dataPrince = PRINCECipher.Scramble(index | dataNonce, keyLow, keyHigh, rounds: 6);
                        var descrabled = (uint)(dataPresent ^ dataPrince);
                        rom.WriteDoubleWord((long)index * 4, descrabled);
                    }
                    read = reader.Read(buffer, 0, buffer.Length);
                    if(read != 0)
                    {
                        throw new RecoverableException($"Error while loading file {fileName}: file is too big");
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException(string.Format("Exception while loading file {0}: {1}", fileName, e.Message));
            }
        }

        public void Reset()
        {
            // do not clear ROM
            registers.Reset();
            fatalTriggered = false;
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

        private readonly ulong addressKey;
        private readonly ulong dataNonce;
        private readonly ulong keyLow;
        private readonly ulong keyHigh;
        private readonly int romIndexWidth;
        private readonly MappedMemory rom;
        private readonly DoubleWordRegisterCollection registers;

        private readonly IValueRegisterField[] expectedDigest;

        private bool fatalTriggered;

        private const int NumberOfDigestRegisters = 8;
        private const int NumberOfScramblingRounds = 2;
        private const int WordSizeWithECC = 5;

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
