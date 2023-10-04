//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    class MAX32650_TPU : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_TPU(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            DefineRegisters();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void CalculateCrc32()
        {
            try
            {
                byte[] data;
                data = sysbus.ReadBytes((ulong)dmaSourceAddress.Value, (int)dmaDataLength.Value, onlyMemory: true);
                crcEngine.Update(data);
                crcValue.Value = crcEngine.Value;
                dmaFinished.Value = true;
                crcFinished.Value = true;
            }
            catch(RecoverableException exception)
            {
                this.Log(LogLevel.Warning, "Error occured when reading memory to calculate CRC: {0}", exception.Message);
                errorOccured.Value = true;
            }

            operationFinished.Value |= crcFinished.Value;
            UpdateInterrupts();
        }

        private void UpdateCRCEngine()
        {
            var polynomial = BitHelper.ReverseBits((uint)crcPolynomial.Value);
            crcEngine = new CRCEngine(polynomial, 32, init: (uint)crcValue.Value);
        }

        private void UpdateInterrupts()
        {
            if(interruptEnabled.Value)
            {
                IRQ.Set(operationFinished.Value | errorOccured.Value);
            }
            else
            {
                IRQ.Unset();
            }
        }

        private void ResetKnownRegisters()
        {
            dmaSourceAddress.Value = 0x00000000;
            dmaDataLength.Value = 0x00000000;
            crcPolynomial.Value = DefaultCRCPolynomial;
            crcValue.Value = DefaultCRCSeed;

            UpdateCRCEngine();
        }

        private void DefineRegisters()
        {
            Registers.CryptoControl.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "CRYPTO_CTRL.rst",
                    // Writing '1' to this field causes peripheral to clear all its
                    // internal cryptographic states as well as related registers.
                    // We are not supposed to clear configuration, thus
                    // *_CTRL registers are omitted.
                    writeCallback: (_, value) => { if(value) ResetKnownRegisters(); })
                .WithFlag(1, out interruptEnabled, name: "CRYPTO_CTRL.int",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithTaggedFlag("CRYPTO_CTRL.src", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("CRYPTO_CTRL.bso", 4)
                .WithTaggedFlag("CRYPTO_CTRL.bsi", 5)
                .WithTaggedFlag("CRYPTO_CTRL.wait_end", 6)
                .WithTaggedFlag("CRYPTO_CTRL.wait_pol", 7)
                .WithTag("CRYPTO_CTRL.wrsrc", 8, 2)
                .WithEnumField<DoubleWordRegister, ReadFIFOSource>(10, 2, name: "CRYPTO_CTRL.rdsrc",
                    changeCallback: (_, value) =>
                    {
                        if(value != ReadFIFOSource.DMAorAPB)
                        {
                            this.Log(LogLevel.Warning, "Tried to change FIFO source to {0}, but only DMA is currently supported; ignored", value);
                        }
                    })
                .WithReservedBits(12, 2)
                .WithTaggedFlag("CRYPTO_CTRL.flag_mode", 14)
                .WithTaggedFlag("CRYPTO_CTRL.dmadnemsk", 15)
                .WithReservedBits(16, 8)
                .WithFlag(24, out dmaFinished, name: "CRYPTO_CTRL.dma_done")
                .WithFlag(25, out crcFinished, name: "CRYPTO_CTRL.gls_done",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithTaggedFlag("CRYPTO_CTRL.hsh_done", 26)
                .WithTaggedFlag("CRYPTO_CTRL.cph_done", 27)
                .WithTaggedFlag("CRYPTO_CTRL.maa_done", 28)
                .WithFlag(29, out errorOccured, FieldMode.Read, name: "CRYPTO_CTRL.err")
                .WithFlag(30, FieldMode.Read, name: "CRYPTO_CTRL.rdy",
                    valueProviderCallback: _ => true)
                .WithFlag(31, out operationFinished, name: "CRYPTO_CTRL.done")
            ;

            Registers.CRCControl.Define(this)
                .WithFlag(0, out crcEnabled, name: "CRC_CTRL.crc")
                .WithFlag(1, name: "CRC_CTRL.msb",
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.Log(LogLevel.Warning, "CRC mode changed to MSB, but only LSB is supported; ignoring");
                        }
                    })
                .WithTaggedFlag("CRC_CTRL.prng", 2)
                .WithTaggedFlag("CRC_CTRL.ent", 3)
                .WithTaggedFlag("CRC_CTRL.ham", 4)
                .WithTaggedFlag("CRC_CTRL.hrst", 5)
                .WithReservedBits(6, 26)
            ;

            Registers.DMASource.Define(this)
                .WithValueField(0, 32, out dmaSourceAddress, name: "DMA_SRC.addr")
            ;

            Registers.DMACount.Define(this)
                .WithValueField(0, 32, out dmaDataLength, name: "DMA_CNT.addr")
                .WithWriteCallback((_, __) => CalculateCrc32());
                // According to documentation and HAL implementation, the CRC
                // value is calculated after DMA transaction is done. As writing
                // to this register starts DMA transaction, we can use it
                // as trigger to calculate CRC value.
            ;

            Registers.CRCPolynomial.Define(this, DefaultCRCPolynomial)
                .WithValueField(0, 32, out crcPolynomial, FieldMode.Write, name: "CRC_POLY.data",
                    changeCallback: (_, __) => UpdateCRCEngine())
            ;

            Registers.CRCValue.Define(this, DefaultCRCSeed)
                .WithValueField(0, 32, out crcValue, name: "CRC_VAL.val",
                    changeCallback: (_, __) => UpdateCRCEngine())
            ;
        }

        private IFlagRegisterField crcEnabled;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField dmaFinished;
        private IFlagRegisterField crcFinished;
        private IFlagRegisterField errorOccured;
        private IFlagRegisterField operationFinished;

        private IValueRegisterField dmaSourceAddress;
        private IValueRegisterField dmaDataLength;

        private IValueRegisterField crcPolynomial;
        private IValueRegisterField crcValue;

        private CRCEngine crcEngine;

        private const uint DefaultCRCPolynomial = 0xEDB88320;
        private const uint DefaultCRCSeed = 0xFFFFFFFF;

        private enum ReadFIFOSource : byte
        {
            DMADisabled = 0,
            DMAorAPB,
            RNG,
            Reserved,
        }

        private enum Registers
        {
            CryptoControl = 0x00,
            CipherControl = 0x04,
            HashControl = 0x08,
            CRCControl = 0x0C,
            DMASource = 0x10,
            DMADestination = 0x14,
            DMACount = 0x18,
            MAAControl = 0x1C,
            DataIn0 = 0x20,
            DataIn1 = 0x24,
            DataIn2 = 0x28,
            DataIn3 = 0x2C,
            DataOut0 = 0x30,
            DataOut1 = 0x34,
            DataOut2 = 0x38,
            DataOut3 = 0x3C,
            CRCPolynomial = 0x40,
            CRCValue = 0x44,
            PRNG = 0x48,
            HammingECC = 0x4C,
            CipherInitialVector0 = 0x50,
            CipherInitialVector1 = 0x54,
            CipherInitialVector2 = 0x58,
            CipherInitialVector3 = 0x5C,
            CipherKey0 = 0x60,
            CipherKey1 = 0x64,
            CipherKey2 = 0x68,
            CipherKey3 = 0x6C,
            CipherKey4 = 0x70,
            CipherKey5 = 0x74,
            CipherKey6 = 0x78,
            CipherKey7 = 0x7C,
            HashMessageDigest0 = 0x80,
            HashMessageDigest1 = 0x84,
            HashMessageDigest2 = 0x88,
            HashMessageDigest3 = 0x8C,
            HashMessageDigest4 = 0x90,
            HashMessageDigest5 = 0x94,
            HashMessageDigest6 = 0x98,
            HashMessageDigest7 = 0x9C,
            HashMessageDigest8 = 0xA0,
            HashMessageDigest9 = 0xA4,
            HashMessageDigest10 = 0xA8,
            HashMessageDigest11 = 0xAC,
            HashMessageDigest12 = 0xB0,
            HashMessageDigest13 = 0xB4,
            HashMessageDigest14 = 0xB8,
            HashMessageDigest15 = 0xBC,
            HashMessageSize0 = 0xC0,
            HashMessageSize1 = 0xC4,
            HashMessageSize2 = 0xC8,
            HashMessageSize3 = 0xCC,
            MAAWordSize = 0xD0,
        }
    }
}

