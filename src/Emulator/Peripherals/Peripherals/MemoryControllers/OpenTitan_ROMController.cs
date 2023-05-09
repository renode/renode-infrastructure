//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class OpenTitan_ROMController: IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_ROMController(MappedMemory rom, string nonce, string key)
        {
            this.rom = rom;
            romLengthInWords = (ulong)rom.Size / 4;
            romIndexWidth = BitHelper.GetMostSignificantSetBitIndex(romLengthInWords - 1) + 1;
            if(romLengthInWords <= 8)
            {
                throw new ConstructionException("Provided rom's size has to be greater than 8 words (32 bytes)");
            }
            if(rom.Size % 4 != 0)
            {
                throw new ConstructionException("Provided rom's size has to be divisible by word size (4)");
            }

            Key = key;
            Nonce = nonce;
            FatalAlert = new GPIO();

            digest = new byte[NumberOfDigestRegisters * 4];
            expectedDigest = new byte[NumberOfDigestRegisters * 4];
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

        public void LoadVmem(string fileName)
        {
            var digestData = new ulong[romLengthInWords - 8];
            try
            {
                var reader = new VmemReader(fileName);
                foreach(var touple in reader.GetIndexDataPairs())
                {
                    var scrambledIndex = (ulong)touple.Item1;
                    var data = touple.Item2;
                    LoadWord(scrambledIndex, data, digestData);
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException(string.Format("Exception while loading file {0}: {1}", fileName, e.Message));
            }
            CalculateDigest(digestData);
        }

        public void Reset()
        {
            // do not clear ROM
            registers.Reset();
            CheckDigest();
            FatalAlert.Unset();
        }

        public long Size => 0x1000;

        public GPIO FatalAlert { get; }

        public IEnumerable<byte> Digest => digest;

        public string Nonce
        {
            set
            {
                ulong[] temp;
                if(!Misc.TryParseHexString(value, out temp, sizeof(ulong), endiannessSwap: true))
                {
                    throw new RecoverableException("Unable to parse value. Incorrect Length");
                }

                addressKey = temp[0] >> (64 - romIndexWidth);
                dataNonce = temp[0] << romIndexWidth;
            }
            get
            {
                return "{0:X16}".FormatWith((dataNonce >> romIndexWidth) | (addressKey << (64 - romIndexWidth)));
            }
        }

        public string Key
        {
            set
            {
                if(!Misc.TryParseHexString(value, out key, sizeof(ulong), endiannessSwap: true))
                {
                    throw new RecoverableException("Unable to parse value. Incorrect Length");
                }
            }
            get
            {
                return key.Select(x => "{0:X16}".FormatWith(x)).Stringify();
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.AlertTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                        {
                            checkerError.Value |= value;
                            integrityError.Value |= value;
                            if(value)
                            {
                                FatalAlert.Blink();
                            }
                        }, name: "fatal")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.FatalAlertCause, new DoubleWordRegister(this)
                    .WithFlag(0, out checkerError, FieldMode.Read, name: "checker_error")
                    .WithFlag(1, out integrityError, FieldMode.Read, name: "integrity_error")
                    .WithReservedBits(2, 30)
                },
            };

            for(var i = 0; i < NumberOfDigestRegisters; ++i)
            {
                registersDictionary.Add((long)Registers.Digest0 + i * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)BitConverter.ToInt32(digest, i * 4), name: $"DIGEST_{i}"));
                registersDictionary.Add((long)Registers.ExpectedDigest0 + i * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)BitConverter.ToInt32(expectedDigest, i * 4), name: $"EXP_DIGEST_{i}"));
            }

            return registersDictionary;
        }

        private void LoadWord(ulong scrambledIndex, ulong data, ulong[] digestData)
        {
            var index = PRESENTCipher.Descramble(scrambledIndex, addressKey, romIndexWidth, NumberOfScramblingRounds);
            if(index >= romLengthInWords)
            {
                throw new RecoverableException($"Error while loading: decoded offset (0x{(index * 4):X}) is out of bounds [0;0x{rom.Size:X})");
            }
            if(index >= romLengthInWords - 8)
            {
                // top 8 (by logical addressing) words are 256-bit expected hash, stored not scrambled.
                expectedDigest.SetBytesFromValue((uint)data, (int)(index + 8 - romLengthInWords) * 4);
            }
            else
            {
                digestData[index] = data;
            }

            // data's width is 32 bits of proper data and 7 bits of ECC
            var dataPresent = PRESENTCipher.Descramble(data, 0, 32 + 7, NumberOfScramblingRounds);
            var dataPrince = PRINCECipher.Scramble(index | dataNonce, key[1], key[0], rounds: 6);
            var descrabled = (dataPresent ^ dataPrince) & ((1UL << 39) - 1);
            if(index < romLengthInWords - 8 && !ECCHsiao.CheckECC(descrabled))
            {
                this.Log(LogLevel.Warning, "ECC error at logical index 0x{0:X}", index);
            }
            rom.WriteDoubleWord((long)index * 4, (uint)descrabled);
        }

        private void CalculateDigest(ulong[] words)
        {
            var hasher = new CShakeDigest(256, null, Encoding.ASCII.GetBytes("ROM_CTRL"));
            foreach(var word in words)
            {
                hasher.BlockUpdate(BitConverter.GetBytes(word), 0, 8);
            }

            hasher.DoFinal(digest, 0, digest.Length);
            CheckDigest();
        }

        private void CheckDigest()
        {
            integrityError.Value = expectedDigest.Zip(digest, (b0, b1) => b0 != b1).Any(b => b);
        }

        private ulong addressKey;
        private ulong dataNonce;
        private readonly ulong romLengthInWords;
        private readonly int romIndexWidth;
        private readonly MappedMemory rom;
        private readonly DoubleWordRegisterCollection registers;

        private readonly byte[] digest;
        private readonly byte[] expectedDigest;
        private ulong[] key;

        private IFlagRegisterField checkerError;
        private IFlagRegisterField integrityError;

        private const int NumberOfDigestRegisters = 8;
        private const int NumberOfScramblingRounds = 2;

        private class ECCHsiao
        {
            // A variation of/optimization over Hamming Code (39,32)
            // For explenation on parity-check matrix construction see:
            // https://www.ysu.am/files/11-1549527438-.pdf
            public static ulong AddECC(uint word)
            {
                var code = 0ul;
                for(byte i = 0; i < 7; ++i)
                {
                    var inverted = i % 2 == 1;
                    var bit = BitHelper.CalculateParity(word & bitmask[i]);
                    BitHelper.SetBit(ref code, i, bit ^ inverted);
                }
                return (ulong)word | (code << 32);
            }

            public static bool CheckECC(ulong word)
            {
                return word == AddECC((uint)word);
            }

            // generated with opentitan's secded_gen.py
            private static readonly uint[] bitmask = new uint[]
            {
                0x2606bd25, 0xdeba8050, 0x413d89aa, 0x31234ed1,
                0xc2c1323b, 0x2dcc624c, 0x98505586
            };
        }
        #pragma warning disable format
        private enum Registers: long
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
        #pragma warning restore format
    }
}
