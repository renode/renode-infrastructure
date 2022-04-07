//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Numerics;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class AthenaX5200 : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AthenaX5200(Machine machine)
        {
            memoryManager = new InternalMemoryManager();
            RegistersCollection = new DoubleWordRegisterCollection(this);

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
                { JumpTable.InstantiateDRBG, InstantiateDRBG },
                { JumpTable.GenerateBlocksFromDRBG, GenerateBlocksWithDRBG },
                { JumpTable.UninstantiateDRBG, UninstantiateDRBG },
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

        private class InternalMemoryManager
        {
            public InternalMemoryManager()
            {
                coreMemories = new Dictionary<long, InternalMemoryAccessor>
                {
                    { 0x0, new InternalMemoryAccessor(BERLength, "BER_BE", isLittleEndian: false) },
                    { 0x1, new InternalMemoryAccessor(MMRLength, "MMR_BE", isLittleEndian: false) },
                    { 0x2, new InternalMemoryAccessor(TSRLength, "TSR_BE", isLittleEndian: false) },
                    { 0x3, new InternalMemoryAccessor(FPRLength, "FPR_BE", isLittleEndian: false) },
                    { 0x8, new InternalMemoryAccessor(BERLength, "BER_LE") },
                    { 0x9, new InternalMemoryAccessor(MMRLength, "MMR_LE") },
                    { 0xA, new InternalMemoryAccessor(TSRLength, "TSR_LE") },
                    { 0xB, new InternalMemoryAccessor(FPRLength, "FPR_LE") }
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
            public InternalMemoryAccessor(uint size, string name, bool isLittleEndian = true)
            {
                this.isLittleEndian = isLittleEndian;
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
                var result = BitHelper.ToUInt32(internalMemory, (int)offset, 4, false);
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
                foreach(var b in BitHelper.GetBytesFromValue(value, sizeof(uint), isLittleEndian))
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
                if(!isLittleEndian)
                {
                    bytes = ChangeEndianness(bytes);
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

            private byte[] ChangeEndianness(byte[] bytes)
            {
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

            private readonly bool isLittleEndian;
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

        private enum JumpTable
        {
            // gaps in addressing - only a few commands are implemented
            InstantiateDRBG = 0x2C,
            GenerateBlocksFromDRBG = 0x30,
            UninstantiateDRBG = 0x32,
            DetectFirmwareVersion = 0x5A
        }

        private enum Registers : uint
        {
            CSR = 0x7F80
        }
    }
}
