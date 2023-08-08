//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class AthenaX5200 : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AthenaX5200(IMachine machine)
        {
            memoryManager = new InternalMemoryManager();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            rsaServiceProvider = new RSAServiceProvider(memoryManager);
            aesServiceProvider = new AESServiceProvider(memoryManager, machine.GetSystemBus(this));
            msgAuthServiceProvider = new MessageAuthenticationServiceProvider(memoryManager, machine.GetSystemBus(this));
            dsaServiceProvider = new DSAServiceProvider(memoryManager, machine.GetSystemBus(this));

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
                { JumpTable.PrecomputeValueRSA, EmptyHandler },
                { JumpTable.InstantiateDRBG, InstantiateDRBG },
                { JumpTable.GenerateBlocksFromDRBG, GenerateBlocksWithDRBG },
                { JumpTable.UninstantiateDRBG, UninstantiateDRBG },
                { JumpTable.ModularExponentationRSA, rsaServiceProvider.ModularExponentation },
                { JumpTable.ModularReductionRSA, rsaServiceProvider.ModularReduction },
                { JumpTable.DecryptCipherRSA, rsaServiceProvider.DecryptData },
                { JumpTable.RunAES, aesServiceProvider.PerformAESOperation },
                { JumpTable.RunAES_DMA, aesServiceProvider.PerformAESOperationDMA },
                { JumpTable.RunGCM, msgAuthServiceProvider.PerformGCMMessageAuthentication },
                { JumpTable.RunGCMNew, msgAuthServiceProvider.PerformGCMMessageAuthentication },
                { JumpTable.RunSHA, msgAuthServiceProvider.PerformSHA },
                { JumpTable.RunSHADMA, msgAuthServiceProvider.PerformSHADMA },
                { JumpTable.RunHMACSHA, msgAuthServiceProvider.PerformHMACSHA },
                { JumpTable.RunDSA_Sign, dsaServiceProvider.SignDMA },
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
            msgAuthServiceProvider.Reset();
            rsaServiceProvider.Reset();
            aesServiceProvider.Reset();
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
        private readonly MessageAuthenticationServiceProvider msgAuthServiceProvider;
        private readonly DSAServiceProvider dsaServiceProvider;
        
        private enum JumpTable
        {
            // gaps in addressing - only a few commands are implemented
            PrecomputeValueRSA = 0x0,
            ModularExponentationRSA = 0x2,
            ModularReductionRSA = 0x12,
            RunSHA = 0x1E,
            RunAES = 0x20,
            RunGCM = 0x24,
            InstantiateDRBG = 0x2C,
            GenerateBlocksFromDRBG = 0x30,
            RunAES_DMA = 0x38,
            UninstantiateDRBG = 0x32,
            RunHMACSHA = 0x36,
            RunSHADMA = 0x3C,
            RunDSA_Sign = 0x40,
            DecryptCipherRSA = 0x4E,
            RunGCMNew = 0x5A
        }

        private enum Registers : uint
        {
            CSR = 0x7F80
        }
    }
}
