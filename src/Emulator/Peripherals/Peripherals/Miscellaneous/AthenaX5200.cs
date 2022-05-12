//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class AthenaX5200 : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AthenaX5200(Machine machine)
        {
            memoryManager = new InternalMemoryManager();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            rsaServiceProvider = new RSAServiceProvider(memoryManager);
            aesServiceProvider = new AESServiceProvider(memoryManager, machine.SystemBus);

            Registers.CSR.Define(this)
                .WithFlag(0,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            Reset();
                        }
                        coreReset = val;
                    },
                    valueProviderCallback: _ => coreReset, name: "RESET")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            isCompleted = false;
                        }
                    }, name: "CLEAR_COMPLETE")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => isCompleted, name: "COMPLETE")
                .WithTag("BUSY", 3, 1)
                .WithFlag(4, out coreExecuteCommand, name: "EXECUTE_COMMAND")
                .WithReservedBits(5, 3)
                .WithEnumField(8, 8, out operation)
                .WithReservedBits(16, 3)
                .WithTag("PKX_OFFSET", 19, 1)
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, __) =>
                {
                    if(!coreExecuteCommand.Value)
                    {
                        return;
                    }
                    if(!commands.TryGetValue(operation.Value, out var command))
                    {
                        this.Log(LogLevel.Error, "Unknown command: [{0}].", operation.Value);
                        return;
                    }
                    this.Log(LogLevel.Noisy, "Executing command: [{0}]", operation.Value);
                    command();
                    isCompleted = true;
                    coreExecuteCommand.Value = false;
                });

            commands = new Dictionary<JumpTable, Action>
            {
                { JumpTable.DetectFirmwareVersion, EmptyHandler },
                { JumpTable.PrecomputeValueRSA, EmptyHandler },
                { JumpTable.InstantiateDRBG, InstantiateDRBG },
                { JumpTable.GenerateBlocksFromDRBG, GenerateBlocksWithDRBG },
                { JumpTable.UninstantiateDRBG, UninstantiateDRBG },
                { JumpTable.ModularExponentationRSA, rsaServiceProvider.ModularExponentation },
                { JumpTable.ModularReductionRSA, rsaServiceProvider.ModularReduction },
                { JumpTable.DecryptCipherRSA, rsaServiceProvider.DecryptData },
                { JumpTable.RunAES, aesServiceProvider.PerformAESOperation },
                { JumpTable.RunAES_DMA, aesServiceProvider.PerformAESOperationDMA },
            };

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return memoryManager.TryReadDoubleWord(offset, out uint result)
                ? result
                : RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(!memoryManager.TryWriteDoubleWord(offset, value))
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public void Reset()
        {
            memoryManager.ResetMemories();
            RegistersCollection.Reset();
            randomGenerator?.Reset();
            isCompleted = false;
            coreReset = false;
        }

        public long Size => 0x1000000;
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void EmptyHandler()
        {
            // There are commands in which no processing is needed,
            // but we want to report in the Status Register that it has completed.
        }

        private void InstantiateDRBG()
        {
            if(randomGenerator != null)
            {
                this.Log(LogLevel.Error, "RNG subsystem already instantiated. Aborting!");
                return;
            }
            randomGenerator = new PseudorandomBitGenerator(memoryManager);
        }

        private void GenerateBlocksWithDRBG()
        {
            if(randomGenerator == null)
            {
                this.Log(LogLevel.Error, "RNG subsystem is not instantiated. Aborting!");
                return;
            }
            randomGenerator.Generate();
        }

        private void UninstantiateDRBG()
        {
            if(randomGenerator == null)
            {
                this.Log(LogLevel.Error, "RNG subsystem is not instantiated. Aborting!");
                return;
            }
            randomGenerator = null;
        }

        private bool coreReset;
        private bool isCompleted;
        private PseudorandomBitGenerator randomGenerator;

        private readonly InternalMemoryManager memoryManager;
        private readonly Dictionary<JumpTable, Action> commands;
        private readonly IEnumRegisterField<JumpTable> operation;
        private readonly IFlagRegisterField coreExecuteCommand;
        private readonly RSAServiceProvider rsaServiceProvider;
        private readonly AESServiceProvider aesServiceProvider;

        private class InternalMemoryManager
        {
            public InternalMemoryManager()
            {
                coreMemories = new Dictionary<long, InternalMemoryAccessor>
                {
                    { 0x0, new InternalMemoryAccessor(BERLength, "BER_BE", Endianness.BigEndian) },
                    { 0x1, new InternalMemoryAccessor(MMRLength, "MMR_BE", Endianness.BigEndian) },
                    { 0x2, new InternalMemoryAccessor(TSRLength, "TSR_BE", Endianness.BigEndian) },
                    { 0x3, new InternalMemoryAccessor(FPRLength, "FPR_BE", Endianness.BigEndian) },
                    { 0x8, new InternalMemoryAccessor(BERLength, "BER_LE", Endianness.LittleEndian) },
                    { 0x9, new InternalMemoryAccessor(MMRLength, "MMR_LE", Endianness.LittleEndian) },
                    { 0xA, new InternalMemoryAccessor(TSRLength, "TSR_LE", Endianness.LittleEndian) },
                    { 0xB, new InternalMemoryAccessor(FPRLength, "FPR_LE", Endianness.LittleEndian) }
                };
            }

            public void ResetMemories()
            {
                foreach(KeyValuePair<long, InternalMemoryAccessor> memory in coreMemories)
                {
                    memory.Value.Reset();
                }
            }

            public bool TryReadDoubleWord(long offset, out uint result)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    result = 0;
                    return false;
                }
                result = mem.ReadDoubleWord(internalOffset);
                return true;
            }

            public bool TryReadBytes(long offset, int count, out byte[] result)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    result = new byte[0];
                    return false;
                }
                result = mem.ReadBytes(internalOffset, count).ToArray();
                return true;
            }

            public bool TryWriteDoubleWord(long offset, uint value)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    return false;
                }
                mem.WriteDoubleWord(internalOffset, value);
                return true;
            }

            public bool TryWriteBytes(long offset, byte[] bytes)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    return false;
                }
                mem.WriteBytes(internalOffset, bytes);
                return true;
            }

            private bool TryAddressInternalMemory(long offset, out InternalMemoryAccessor mem, out long internalMemoryOffset)
            {
                var offsetMask = offset >> OffsetShift;
                if(!coreMemories.TryGetValue(offsetMask, out mem))
                {
                    internalMemoryOffset = 0;
                    Logger.Log(LogLevel.Noisy, "Could not write to internal memory at address 0x{0:X}", offset);
                    return false;
                }
                internalMemoryOffset = offset - (offsetMask << OffsetShift);
                return true;
            }

            private readonly Dictionary<long, InternalMemoryAccessor> coreMemories;

            private const int BERLength = 0x1000;
            private const int MMRLength = 0x1000;
            private const int TSRLength = 0x1000;
            private const int FPRLength = 0x1000;
            private const int OffsetShift = 12;
        }

        private class InternalMemoryAccessor
        {
            public InternalMemoryAccessor(uint size, string name, Endianness endianness)
            {
                this.endianness = endianness;
                internalMemory = new byte[size];
                Name = name;
            }

            public uint ReadDoubleWord(long offset)
            {
                if(offset < 0 || (offset + 4) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to read outside of {0} internal memory, at offset 0x{1:X}", Name, offset);
                    return 0;
                }
                var result = BitHelper.ToUInt32(internalMemory, (int)offset, 4, endianness == Endianness.LittleEndian);
                Logger.Log(LogLevel.Debug, "Read value 0x{0:X} from memory {1} at offset 0x{2:X}", result, Name, offset);
                return result;
            }

            public IEnumerable<byte> ReadBytes(long offset, int count)
            {
                if(offset < 0 || (offset + count) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to read {0} bytes outside of {1} internal memory, at offset 0x{2:X}", count, Name, offset);
                    yield return 0;
                }
                for(var i = 0; i < count; ++i)
                {
                    yield return internalMemory[offset + i];
                }
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                if(offset < 0 || (offset + 4) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to write value 0x{0:X} outside of {1} internal memory, at offset 0x{2:X}", value, Name, offset);
                    return;
                }
                Logger.Log(LogLevel.Debug, "Writing value 0x{0:X} to memory {1} at offset 0x{2:X}", value, Name, offset);

                foreach(var b in BitHelper.GetBytesFromValue(value, sizeof(uint), false))
                {
                    internalMemory[offset] = b;
                    ++offset;
                }
            }

            public void WriteBytes(long offset, byte[] bytes)
            {
                if(offset < 0 || (offset + bytes.Length) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to write {0] bytes outside of {1} internal memory, at offset 0x{2:X}", bytes.Length, Name, offset);
                    return;
                }
                foreach(var b in bytes)
                {
                    internalMemory[offset] = b;
                    ++offset;
                }
            }

            public void Reset()
            {
                for(var i = 0; i < internalMemory.Length; ++i)
                {
                    internalMemory[i] = 0;
                }
            }

            public string Name { get; }

            private readonly Endianness endianness;
            private readonly byte[] internalMemory;
        }

        // This class exposes the functionality of the DRBG core (short for "Deterministic Random Bit Generator"),
        // but because it is simplified, the name is also changed to be more adequate.
        private class PseudorandomBitGenerator
        {
            public PseudorandomBitGenerator(InternalMemoryManager manager)
            {
                this.manager = manager;
                Reset();
            }

            public void Generate()
            {
                manager.TryReadDoubleWord((long)DRBGRegisters.EntropyFactor, out reqLen);
                manager.TryReadDoubleWord((long)DRBGRegisters.ReseedLimit, out var reseedLimit);
                if(reseedLimit == 1 || reseedCounter == 0)
                {
                    Reseed(reseedLimit);
                }
                for(var i = 0; i < reqLen * 4; ++i)
                {
                    manager.TryWriteDoubleWord(
                        (long)DRBGRegisters.ResponseDataAddress + (i * 4),
                        (uint)EmulationManager.Instance.CurrentEmulation.RandomGenerator.Next()
                    );
                }
                if(reseedCounter >= 0)
                {
                    reseedCounter--;
                }
            }

            public void Reset()
            {
                reseedCounter = 0;
                reqLen = 0;
            }

            private void Reseed(uint limit)
            {
                Logger.Log(LogLevel.Noisy, "Requested seed reset.");
                // As a simplification, and to ensure execution determinism, we increment the existing seed by one.
                EmulationManager.Instance.CurrentEmulation.RandomGenerator.ResetSeed(
                    EmulationManager.Instance.CurrentEmulation.RandomGenerator.GetCurrentSeed() + 1
                );
                reseedCounter = limit;
            }

            private uint reseedCounter;
            private uint reqLen;

            private readonly InternalMemoryManager manager;

            private enum DRBGRegisters
            {
                EntropyFactor = 0x8,
                KeyOrdinal = 0xC,
                ReseedLimit = 0x10,
                IsTestInstantiation = 0x14,
                AdditionalInputDataLength = 0x18,
                PersonalizationStringAddress = 0x1C,
                ContextAddress = 0x20,
                AdditionalInputDataAddress = 0x188,
                ResponseDataAddress = 0x8090,
            }
        }

        private class RSAServiceProvider
        {
            public RSAServiceProvider(InternalMemoryManager manager)
            {
                this.manager = manager;
            }

            public void ModularExponentation()
            {
                manager.TryReadDoubleWord((long)RSARegisters.ExponentationModulusLength, out var modulusLength);
                manager.TryReadDoubleWord((long)RSARegisters.ExponentLength, out var exponentLength);
                var baseAddress = (long)RSARegisters.BaseAddress + exponentLength * 4;

                var n = CreateBigInteger(RSARegisters.Modulus, modulusLength);
                var e = CreateBigInteger(RSARegisters.Exponent, exponentLength);

                var operand = CreateBigInteger((RSARegisters)baseAddress, modulusLength);
                var result = BigInteger.ModPow(operand, e, n);

                var resultBytes = Helpers.ChangeEndianness(result.ToByteArray());
                manager.TryWriteBytes(baseAddress, resultBytes);
            }

            public void ModularReduction()
            {
                manager.TryReadDoubleWord((long)RSARegisters.ReductionModulusLength, out var modulusLength);
                manager.TryReadDoubleWord((long)RSARegisters.ReductionOperandLength, out var operandLength);

                var n = CreateBigInteger(RSARegisters.Modulus, modulusLength);
                var a = CreateBigInteger(RSARegisters.Operand, operandLength);
                var result = a % n;

                var resultBytes = Helpers.ChangeEndianness(result.ToByteArray());
                manager.TryWriteBytes((long)RSARegisters.Operand, resultBytes);
            }

            public void DecryptData()
            {
                manager.TryReadDoubleWord((long)RSARegisters.DecryptionModulusLength, out var modulusLength);

                var n = CreateBigInteger(RSARegisters.N, modulusLength * 2);

                // Step 1:
                // m1 = c^dp mod p
                var c = CreateBigInteger(RSARegisters.Cipher, modulusLength * 2);
                var dp = CreateBigInteger(RSARegisters.DP, modulusLength);
                var p = CreateBigInteger(RSARegisters.P, modulusLength);
                var m1 = BigInteger.ModPow(c, dp, p);

                // Step 2:
                // m2 = c^dq mod q
                var dq = CreateBigInteger(RSARegisters.DQ, modulusLength);
                var q = CreateBigInteger(RSARegisters.Q, modulusLength);
                var m2 = BigInteger.ModPow(c, dq, q);

                // Step 3:
                // h = (qInv * (m1 - m2)) mod p
                var qInv = CreateBigInteger(RSARegisters.QInverted, modulusLength);
                var x = m1 - m2;
                // We add in an extra p here to keep x positive
                // example: https://www.di-mgt.com.au/crt_rsa.html
                while(x < 0)
                {
                    x += p;
                }
                var h = (qInv * x) % p;

                // Step 4:
                // m = m2 + h * q
                var m = m2 + h * q;

                var mBytes = Helpers.ChangeEndianness(m.ToByteArray());
                manager.TryWriteBytes((long)RSARegisters.BaseAddress, mBytes);
            }

            private BigInteger CreateBigInteger(RSARegisters register, uint wordCount)
            {
                var j = wordCount - 1;
                manager.TryReadBytes((long)register + ((wordCount - 1) * 4), 1, out var b);
                // All calculations require positive values, so we are verifying the sign here:
                // if the highest bit of the first byte is set, we effectively add a zero at
                // the beginning of the array, so the data can be interpreted as a positive value.
                var wordBytesLength = b[0] >= 0x80 ? wordCount * 4 + 1 : wordCount * 4;
                var wordBytes = new byte[wordBytesLength];
                for(var i = (int)wordCount - 1; i >= 0; --i)
                {
                    manager.TryReadBytes((long)register + (i * 4), 4, out var bytes);
                    wordBytes[j * 4 + 3] = bytes[0];
                    wordBytes[j * 4 + 2] = bytes[1];
                    wordBytes[j * 4 + 1] = bytes[2];
                    wordBytes[j * 4] = bytes[3];
                    --j;
                }
                return new BigInteger(wordBytes);
            }            

            private readonly InternalMemoryManager manager;

            private enum RSARegisters
            {
                ReductionOperandLength = 0x8,
                ReductionModulusLength = 0xC,
                ExponentationModulusLength = 0x10,
                Operand = 0x10,
                ExponentLength = 0x14,
                Exponent = 0x18,
                BaseAddress = 0x18,
                DecryptionModulusLength = 0x30,
                Cipher = 0x1e4,
                DP = 0x4f0,
                DQ = 0x5b0,
                QInverted = 0x670,
                Modulus = 0x1000,
                P = 0x1348,
                Q = 0x14cc,
                N = 0x1650,
            }
        }

        private class AESServiceProvider
        {
            public AESServiceProvider(InternalMemoryManager manager, SystemBus bus)
            {
                this.manager = manager;
                this.bus = bus;
            }

            public void PerformAESOperation()
            {
                manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes);
                manager.TryReadBytes((long)AESRegisters.InitVector, InitializationVectorByteCount, out var ivBytes);
                manager.TryReadBytes((long)AESRegisters.InputData, InputDataByteCount, out var inputBytes);
                manager.TryReadDoubleWord((long)AESRegisters.Config, out var config);
                var operation = BitHelper.IsBitSet(config, 0)
                       ? Operation.Decryption
                       : Operation.Encryption;

                // The memory in which Key bytes are stored is little-endian, but both Encryptor and Decryptor require big-endian data
                keyBytes = Helpers.ChangeEndianness(keyBytes);
                var result = GetResultBytes(keyBytes, ivBytes, inputBytes, operation);

                manager.TryWriteBytes((long)AESRegisters.Cipher, result);
            }

            public void PerformAESOperationDMA()
            {
                manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes);
                manager.TryReadBytes((long)AESRegisters.InitVectorDMA, InitializationVectorByteCount, out var ivBytes);
                manager.TryReadDoubleWord((long)AESRegisters.InputDataAddrDMA, out var inputDataAddr);
                manager.TryReadDoubleWord((long)AESRegisters.ResultDataAddrDMA, out var resultDataAddr);
                manager.TryReadDoubleWord((long)AESRegisters.ConfigDMA, out var config);
                var operation = BitHelper.IsBitSet(config, 0)
                       ? Operation.Decryption
                       : Operation.Encryption;

                // The memory in which Key bytes are stored is little-endian, but both Encryptor and Decryptor require big-endian data
                keyBytes = Helpers.ChangeEndianness(keyBytes);
                bus.WriteBytes(GetResultBytes(keyBytes, ivBytes, bus.ReadBytes(inputDataAddr, CipherByteCount), operation), resultDataAddr);
            }

            private byte[] GetResultBytes(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
            {
                return operation == Operation.Encryption
                    ? Encryption(keyBytes, ivBytes, inputBytes)
                    : Decryption(keyBytes, ivBytes, inputBytes);
            }

            private byte[] Encryption(byte[] key, byte[] iv, byte[] input)
            {                
                using(var aes = Aes.Create())
                using(var encryptor = aes.CreateEncryptor(key, iv))
                {
                    return encryptor.TransformFinalBlock(input, 0, input.Length);
                }
            }

            private byte[] Decryption(byte[] key, byte[] iv, byte[] input)
            {
                using(var aes = Aes.Create())
                {
                    aes.Padding = PaddingMode.None;
                    using(var encryptor = aes.CreateDecryptor(key, iv))
                    {
                        return encryptor.TransformFinalBlock(input, 0, input.Length);
                    }
                }
            }

            private readonly SystemBus bus;
            private readonly InternalMemoryManager manager;

            // These values are taken directly from the driver because they are not written to the internal memories
            private const int KeyLengthByteCount = 32;
            private const int InitializationVectorByteCount = 16;
            private const int InputDataByteCount = 16;
            private const int CipherByteCount = 16;

            private enum Operation
            {
                Encryption,
                Decryption
            }

            private enum AESRegisters
            {
                Config = 0x20,
                InputDataAddrDMA = 0x28,
                ResultDataAddrDMA = 0x2C,
                ConfigDMA = 0x3C,
                InitVector = 0x8024,
                InitVectorDMA = 0x8040,
                Cipher = 0x8048,
                InputData = 0x8058,
                Key = 0x9000,
            }
        }

        private static class Helpers
        {
            public static byte[] ChangeEndianness(byte[] bytes)
            {
                DebugHelper.Assert(bytes.Length % 4 == 0);

                for(var i = 0; i < bytes.Length / 4; ++i)
                {
                    var temp = bytes[(i * 4) + 3];
                    bytes[(i * 4) + 3] = bytes[(i * 4) + 0];
                    bytes[(i * 4) + 0] = temp;

                    temp = bytes[(i * 4) + 2];
                    bytes[(i * 4) + 2] = bytes[(i * 4) + 1];
                    bytes[(i * 4) + 1] = temp;
                }
                return bytes;
            }
        }

        private enum Endianness
        {
            BigEndian,
            LittleEndian
        }
        
        private enum JumpTable
        {
            // gaps in addressing - only a few commands are implemented
            PrecomputeValueRSA = 0x0,
            ModularExponentationRSA = 0x2,
            ModularReductionRSA = 0x12,
            RunAES = 0x20,
            InstantiateDRBG = 0x2C,
            GenerateBlocksFromDRBG = 0x30,
            RunAES_DMA = 0x38,
            UninstantiateDRBG = 0x32,
            DecryptCipherRSA = 0x4E,
            DetectFirmwareVersion = 0x5A
        }

        private enum Registers : uint
        {
            CSR = 0x7F80
        }
    }
}
