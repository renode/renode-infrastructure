//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    // This class exposes the functionality of the DRBG core (short for "Deterministic Random Bit Generator"),
    // but because it is simplified, the name is also changed to be more adequate.
    public class PseudorandomBitGenerator
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
}
