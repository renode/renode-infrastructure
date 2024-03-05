//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    // NOTE: DMA interface is not implemented, supports only AES-GCM mode
    public class STM32H7_CRYPTO : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H7_CRYPTO(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            inputFIFO.Clear();
            outputFIFO.Clear();
            algorithmState = null;
            // `currentMode` doesn't have to be reset

            base.Reset();
            // Needs to be called after the RegisterCollection is reset
            UpdateInterrupt();
        }

        public long Size => 0x400;

        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, out algorithmDirection, name: "Algorithm direction")
                .WithValueField(3, 3, out algorithmModeLow, name: "Algorithm mode [0:2]")
                .WithEnumField(6, 2, out dataType, name: "Data type selection")
                .WithEnumField(8, 2, out keySize, name: "Key size (AES mode only)")
                .WithReservedBits(10, 4)
                .WithFlag(14, FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        lock(executeLock)
                        {
                            inputFIFO.Clear();
                            outputFIFO.Clear();
                        }
                    },
                    name: "FIFO flush")
                .WithFlag(15, out enabled,
                    writeCallback: (_, __) =>
                    {
                        lock(executeLock)
                        {
                            EnableOrDisable();
                            // If there is any pending data in the FIFO, try sending it now
                            TryFeedPhase();
                            UpdateInterrupt();
                        }
                    },
                    name: "CRYPTO Enable")
                .WithEnumField(16, 2, out phaseGCMOrCCM, name: "GCM or CCM Phase")
                .WithReservedBits(18, 1)
                .WithValueField(19, 1, out algorithmModeHigh, name: "Algorithm mode [3]")
                .WithReservedBits(20, 12);

            Registers.Status.Define(this)
                // The queues don't have fixed lengths for us, but they still report limits, as provided in the docs
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => inputFIFO.Count == 0, name: "Input FIFO empty (IFEM)")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => inputFIFO.Count < MaximumFifoDepth, name: "Input FIFO not full (IFNF)")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => outputFIFO.Count > 0, name: "Output FIFO not empty (OFNE)")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => outputFIFO.Count >= MaximumFifoDepth, name: "Output FIFO full (OFFU)")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "CRYPTO is Busy") // Always false - operations are instant for us
                .WithReservedBits(5, 27);

            Registers.DataInput.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) =>
                    {
                        lock(executeLock)
                        {
                            // Casts to `uint` are safe, since the field is exactly 32 bits wide
                            inputFIFO.Enqueue(DoByteSwap((uint)value));
                            TryFeedPhase();
                            UpdateInterrupt();
                        }
                    },
                    valueProviderCallback: _ =>
                    {
                        lock(executeLock)
                        {
                            if(enabled.Value)
                            {
                                this.Log(LogLevel.Warning, "DataInput should not be read from, while CRYPTO is enabled");
                            }

                            if(!inputFIFO.TryDequeue(out var result))
                            {
                                this.Log(LogLevel.Warning, "Input FIFO is empty, returning 0");
                                return 0;
                            }
                            UpdateInterrupt();
                            return result;
                        }
                    },
                    name: "Data input"
                );

            Registers.DataOutput.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        lock(executeLock)
                        {
                            if(!outputFIFO.TryDequeue(out var result))
                            {
                                this.Log(LogLevel.Warning, "Output FIFO is empty, returning 0");
                                return 0;
                            }
                            UpdateInterrupt();
                            return DoByteSwap(result);
                        }
                    },
                    name: "Data output"
                );

            Registers.InterruptMaskSetClear.Define(this)
                .WithFlag(0, out inputFifoIrqMask, name: "Input FIFO service interrupt mask (INIM)")
                .WithFlag(1, out outputFifoIrqMask, name: "Output FIFO service interrupt mask (OUTIM)")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Registers.RawInterruptStatus.Define(this)
                .WithFlag(0, out inputFifoIrqRaw, FieldMode.Read, name: "Input FIFO service raw interrupt status (INRIS)")
                .WithFlag(1, out outputFifoIrqRaw, FieldMode.Read, name: "Output FIFO service raw interrupt status (OUTRIS)")
                .WithReservedBits(2, 30);

            Registers.MaskedInterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => inputFifoIrqRaw.Value && inputFifoIrqMask.Value, name: "Input FIFO service masked interrupt status (INMIS)")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => outputFifoIrqRaw.Value && outputFifoIrqMask.Value, name: "Output FIFO service masked interrupt status (OUTMIS)")
                .WithReservedBits(2, 30);

            Registers.Key0L.DefineMany(this, 8, (register, registerIndex) =>
                    register.WithValueField(0, 32, out keys[registerIndex], name: $"Key{registerIndex / 2}{(registerIndex % 2 > 0 ? "R" : "L")}")
                );

            Registers.InitializationVector0L.DefineMany(this, 4, (register, registerIndex) =>
                    register.WithValueField(0, 32, out initialVectors[registerIndex], name: $"IV{registerIndex / 2}{(registerIndex % 2 > 0 ? "R" : "L")}")
                );
        }

        private void TryFeedPhase()
        {
            if(algorithmState != null)
            {
                while(inputFIFO.TryDequeue(out var result))
                {
                    algorithmState.FeedThePhase(result);
                }
            }
        }

        private uint DoByteSwap(uint value)
        {
            switch(dataType.Value)
            {
                case DataType.Bit32:
                    // No byte swapping
                    return value;
                case DataType.Bit16:
                    return BitHelper.ReverseWords(value);
                case DataType.Bit8:
                    return BitHelper.ReverseBytes(value);
                case DataType.Bit1:
                    return BitHelper.ReverseBits(value);
                default:
                    throw new InvalidOperationException($"Invalid byte swap option selected: {dataType.Value}");
            }
        }

        private void UpdateInterrupt()
        {
            outputFifoIrqRaw.Value = outputFIFO.Any();
            inputFifoIrqRaw.Value = inputFIFO.Count < MaximumFifoDepth;

            bool status = (outputFifoIrqRaw.Value && outputFifoIrqMask.Value)
                          || (inputFifoIrqRaw.Value && inputFifoIrqMask.Value);
            this.Log(LogLevel.Noisy, "IRQ set to {0}", status);
            IRQ.Set(status);
        }

        private void EnableOrDisable()
        {
            if(!enabled.Value)
            {
                // If we are not enabling the CRYPTO, there is no need to update the status.
                // In fact, the current status (e.g. execution phase) needs to be preserved, when the peripheral is re-enabled again.
                // So just exit immediately on a disabled peripheral.
                return;
            }

            var algorithmMode = (AlgorithmMode)((algorithmModeHigh.Value << 3) | algorithmModeLow.Value);
            // Switch algorithm, but only if not executing workaround
            if((algorithmMode != currentMode || algorithmState == null) && !DetectGCMWorkaround(algorithmMode))
            {
                if(algorithmMode != AlgorithmMode.AES_GCM)
                {
                    this.Log(LogLevel.Error, "This model implements only: {0} mode but tried to configure it to {1}. Ignoring the operation", nameof(AlgorithmMode.AES_GCM), algorithmMode);
                    return;
                }
                algorithmState = new RSA_GCM_State(this);
                currentMode = algorithmMode;
            }

            try
            {
                this.Log(LogLevel.Debug, "Switching to GCM phase {0}", phaseGCMOrCCM.Value);
                switch(phaseGCMOrCCM.Value)
                {
                    case GCMOrCCMPhase.Initialization:
                        algorithmState.InitializeInitializationPhase(
                            keys.Select(ks => (uint)ks.Value).Skip(keySizeToAesSkip[keySize.Value]).Reverse().SelectMany(e => BitConverter.GetBytes(e)).Reverse().ToArray(),
                            initialVectors.Select(ks => (uint)ks.Value).Reverse().SelectMany(e => BitConverter.GetBytes(e)).Reverse().ToArray()
                        );
                        // According to the docs:
                        // "This bit is automatically cleared by hardware when the key preparation process ends (ALGOMODE = 0111) or after GCM/GMAC or CCM Initialization phase."
                        enabled.Value = false;
                        return;
                    case GCMOrCCMPhase.Header:
                        algorithmState.InitializeHeaderPhase();
                        return;
                    case GCMOrCCMPhase.Payload:
                        algorithmState.InitializePayloadPhase();
                        return;
                    case GCMOrCCMPhase.Final:
                        algorithmState.InitializeFinalPhase();
                        return;
                    default:
                        throw new InvalidOperationException($"Invalid GCM phase: {phaseGCMOrCCM.Value}");
                }
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Error, "Cryptography backend failed with exception: {0}", e);
                enabled.Value = false;
                algorithmState = null;
            }
        }

        private bool DetectGCMWorkaround(AlgorithmMode newMode)
        {
            // This is a workaround for calculating correct MAC for GCM, if the last block size if less than 128 bit
            // described in 35.4.8 (CRYP stealing and data padding)
            if(!(algorithmState is RSA_GCM_State s) || newMode != AlgorithmMode.AES_CTR || phaseGCMOrCCM.Value != GCMOrCCMPhase.Payload)
            {
                return false;
            }
            if(s.ExecutingWorkaround)
            {
                return true;
            }
            this.Log(LogLevel.Debug, "Executing GCM workaround");
            // Workaround detected - don't switch to newMode, but we need to handle the bytes in a specific way.
            // BouncyCastle can handle partial (< 128 bit) last block, but in this case, it will emit both the ciphertext and tag, as output from `DoFinal`
            // the ciphertext from `ProcessBytes` will be invalid and needs to be discarded.
            // Since the model should return valid data block-for-block we have to run the cipher two times:
            // first to obtain the ciphertext, and second to obtain the MAC.
            // This could be optimized, by extracting MAC calculation outside of the lib, if performance becomes critical.
            s.ExecuteWorkaround();
            return true;
        }

        private readonly Queue<uint> inputFIFO = new Queue<uint>();
        private readonly Queue<uint> outputFIFO = new Queue<uint>();

        private IFlagRegisterField outputFifoIrqMask;
        private IFlagRegisterField inputFifoIrqMask;
        private IFlagRegisterField outputFifoIrqRaw;
        private IFlagRegisterField inputFifoIrqRaw;

        private IFlagRegisterField algorithmDirection;
        private IFlagRegisterField enabled;
        private IValueRegisterField algorithmModeLow;
        private IValueRegisterField algorithmModeHigh;
        private IEnumRegisterField<DataType> dataType;
        private IEnumRegisterField<KeySize> keySize;
        private IEnumRegisterField<GCMOrCCMPhase> phaseGCMOrCCM;

        // These are expected to be stored in big-endian format
        private readonly IValueRegisterField[] keys = new IValueRegisterField[8];
        private readonly IValueRegisterField[] initialVectors = new IValueRegisterField[4];

        private FourPhaseState algorithmState;
        private AlgorithmMode currentMode;
        private readonly object executeLock = new object();

        private const int MaximumFifoDepth = 8;

        private readonly Dictionary<KeySize, int> keySizeToAesSkip = new Dictionary<KeySize, int>()
        {
            {KeySize.Bit256, 0},
            {KeySize.Bit192, 2},
            {KeySize.Bit128, 4},
        };

        private abstract class FourPhaseState
        {
            abstract public void InitializeInitializationPhase(byte[] key, byte[] iv);
            abstract public void InitializeHeaderPhase();
            abstract public void InitializePayloadPhase();
            abstract public void InitializeFinalPhase();

            // Feed data from input FIFO
            abstract public void FeedThePhase(uint value);

            protected STM32H7_CRYPTO parent;
        }

        private class RSA_GCM_State : FourPhaseState
        {
            public RSA_GCM_State(STM32H7_CRYPTO parent)
            {
                this.parent = parent;
            }

            public bool ExecutingWorkaround { get; private set; }

            public override void FeedThePhase(uint value)
            {
                var currentPhase = parent.phaseGCMOrCCM.Value;
                if(!CheckIfInitialized())
                {
                    return;
                }
                byte[] bytes = BitConverter.GetBytes(value).Reverse().ToArray();

                switch(currentPhase)
                {
                    case GCMOrCCMPhase.Header:
                        ProcessHeader(bytes);
                        break;
                    case GCMOrCCMPhase.Payload:
                        ProcessPayload(bytes);
                        break;
                    case GCMOrCCMPhase.Final:
                        ProcessFinal(value);
                        break;
                }
            }

            public override void InitializeInitializationPhase(byte[] key, byte[] iv)
            {
                parent.Log(LogLevel.Debug, "Initializing {0} with key: {1}, iv: {2}", nameof(RSA_GCM_State), Misc.PrettyPrintCollectionHex(key), Misc.PrettyPrintCollectionHex(iv));

                gcm = new GcmBlockCipher(new AesEngine());
                payload = new List<byte>();
                aad = new List<byte>();
                final = new List<uint>();
                ExecutingWorkaround = false;
                payloadCounter = 0;

                keyParameters = new KeyParameter(key);
                this.iv = iv;
                isInitialized = true;
            }

            public override void InitializeHeaderPhase()
            {
                if(!CheckIfInitialized())
                {
                    return;
                }
                if(BitHelper.ToUInt32(iv, 12, 4, false) != 0x2)
                {
                    // The docs suggest to put 0x2 here. This is likely a counter block, but the crypto backend doesn't work well when supplying custom counter value
                    // So this is ignored with the assert, since it should be unlikely, that a different value will be used
                    parent.Log(LogLevel.Error, "Last block of IV is not equal to 0x2. This will be ignored, and calculation might be unreliable");
                }
                iv = iv.Take(12).ToArray();
                // Associated data will be fed by the input FIFO later
                finalParameters = new AeadParameters(keyParameters, MacSizeInBytes * 8, iv);
                // It's a symmetric cipher, it's stuck in encryption mode
                // for BouncyCastle decryption will try to compare MAC and throw errors, this is not what we want here
                gcm.Init(true, finalParameters);
            }

            public override void InitializePayloadPhase()
            {
                // Intentionally blank - the payload will be fed through input FIFO
            }

            public override void InitializeFinalPhase()
            {
                // The model is expecting to get the block describing the length of the header and payload
                // And only afterwards will it return the computed MAC
                // So this method is intentionally blank - will be fed through input FIFO
            }

            public void ExecuteWorkaround()
            {
                ExecutingWorkaround = true;
            }

            private void ProcessHeader(byte[] bytes)
            {
                aad.AddRange(bytes);
                gcm.ProcessAadBytes(bytes, 0, bytes.Length);
            }

            private void ProcessPayload(byte[] bytes)
            {
                byte[] output = new byte[BlockSizeInBytes];
                var length = bytes.Length;

                if(IsEncryption)
                {
                    payload.AddRange(bytes);
                }
                gcm.ProcessBytes(bytes, 0, length, output, 0);
                if((++payloadCounter % (BlockSizeInBytes / sizeof(uint))) != 0)
                {
                    // Block size is 128 bits in AES-GCM, so we need to first process 4 uints worth of data
                    // before returning any data from the FIFO
                    return;
                }

                if(!IsEncryption)
                {
                    payload.AddRange(output);
                }
                parent.Log(LogLevel.Debug, "Got cipher block: {0}", Misc.PrettyPrintCollectionHex(output));
                parent.outputFIFO.EnqueueRange(BytesToUIntAndSwapEndianness(output));
            }

            private void ProcessFinal(uint value)
            {
                final.Add(value);

                if(ExecutingWorkaround)
                {
                    if(final.Count != 4)
                    {
                        // First 4 uints supplied to Final, when executing workaround, have to be discarded by us
                        // since they don't contain the header and payload length
                        // note, that they most likely have some meaning for the silicon, so you shouldn't just write bogus here on real HW
                        // but not for us, because of how our crypto backend operates
                        return;
                    }
                    final.Clear();
                    // Return some bogus data - the driver has to discard it, according to the docs
                    parent.outputFIFO.EnqueueRange(new uint[] { 0xDEADBEEF, 0xBAADBEEF, 0xFEEDC0DE, 0xDEADC0DE });
                    ExecutingWorkaround = false;
                    return;
                }

                if(final.Count != 4)
                {
                    // We don't have the lengths provided just yet
                    return;
                }
                // Length is provided in bits, but BouncyCastle needs bytes
                uint headerLen = final[1] / 8;
                uint payloadLen = final[3] / 8;
                final.Clear();
                parent.Log(LogLevel.Debug, "Calculating MAC for given final parameters: headerLen={0}, payloadLen={1}", headerLen, payloadLen);

                var gcmTag = new GcmBlockCipher(new AesEngine());
                gcmTag.Init(true, finalParameters);
                gcmTag.ProcessAadBytes(aad.ToArray(), 0, (int)headerLen);

                // This is discarded by the model, but the crypto backend needs the space to performs calculations
                var output = new byte[payload.Count];
                // This will contain both the MAC and the last cipherblock
                var mac = new byte[BlockSizeInBytes + MacSizeInBytes];

                // Payload is either ciphertext or plaintext, depending on the selected mode
                gcmTag.ProcessBytes(payload.ToArray(), 0, (int)payloadLen, output, 0);
                gcmTag.DoFinal(mac, 0);
                parent.outputFIFO.EnqueueRange(BytesToUIntAndSwapEndianness(gcmTag.GetMac()));
            }

            private IEnumerable<uint> BytesToUIntAndSwapEndianness(byte[] bytes)
            {
                if(bytes.Length % 4 != 0)
                {
                    // This should never happen, since AES blocks are 128 bits, but let's be sure
                    throw new InvalidOperationException($"{nameof(RSA_GCM_State)} cipher block is not a multiple of 4!");
                }

                for(int i = 0; i < bytes.Length; i += 4)
                {
                    // Swap endianness here - and since output is "uint" - take 4 bytes
                    yield return BitHelper.ToUInt32(bytes, i, 4, false);
                }
            }

            private bool CheckIfInitialized()
            {
                if(isInitialized == false)
                {
                    parent.Log(LogLevel.Error, "Initialization Phase has not been executed. Aborting");
                    return false;
                }
                return true;
            }

            private bool IsEncryption => parent.algorithmDirection.Value == false;

            private const int BlockSizeInBytes = 128 / 8;
            private const int MacSizeInBytes = BlockSizeInBytes;

            private bool isInitialized;
            private int payloadCounter;
            private GcmBlockCipher gcm;
            private List<byte> payload;
            private List<uint> final;
            private List<byte> aad;
            private byte[] iv;
            private KeyParameter keyParameters;
            private AeadParameters finalParameters;
        }

        private enum GCMOrCCMPhase
        {
            Initialization = 0b00,
            Header = 0b01,
            Payload = 0b10,
            Final = 0b11
        }

        private enum DataType
        {
            Bit32 = 0b00,
            Bit16 = 0b01,
            Bit8 = 0b10,
            Bit1 = 0b11,
        }

        private enum KeySize
        {
            Bit128 = 0b00,
            Bit192 = 0b01,
            Bit256 = 0b10,
            Reserved = 0b11,
        }

        private enum AlgorithmMode
        {
            TDES_ECB = 0b0000,
            TDES_CBC = 0b0001,
            DES_ECB = 0b0010,
            DES_CBC = 0b0011,
            AES_ECB = 0b0100,
            AES_CBC = 0b0101,
            AES_CTR = 0b0110,
            AES_key_prepare_EBC_CBC = 0b0111,
            AES_GCM = 0b1000,
            AES_CCM = 0b1001,
        }

        private enum Registers
        {
            Control = 0x0,
            Status = 0x4,
            DataInput = 0x8,
            DataOutput = 0xC,
            // NOTE: DMA interface is not supported
            DMAControl = 0x10,
            InterruptMaskSetClear = 0x14,
            RawInterruptStatus = 0x18,
            MaskedInterruptStatus = 0x1C,
            Key0L = 0x20,
            Key0R = 0x24,
            Key1L = 0x28,
            Key1R = 0x2C,
            Key2L = 0x30,
            Key2R = 0x34,
            Key3L = 0x38,
            Key3R = 0x3C,
            InitializationVector0L = 0x40,
            InitializationVector0R = 0x44,
            InitializationVector1L = 0x48,
            InitializationVector1R = 0x4C,
            // NOTE: Pre-emptive Context Switching is not supported
            ContentSwapGCM_CCM0 = 0x50,
            ContentSwapGCM_CCM1 = 0x54,
            ContentSwapGCM_CCM2 = 0x58,
            ContentSwapGCM_CCM3 = 0x5C,
            ContentSwapGCM_CCM4 = 0x60,
            ContentSwapGCM_CCM5 = 0x64,
            ContentSwapGCM_CCM6 = 0x68,
            ContentSwapGCM_CCM7 = 0x6C,
            ContentSwapGCM0 = 0x70,
            ContentSwapGCM1 = 0x74,
            ContentSwapGCM2 = 0x78,
            ContentSwapGCM3 = 0x7C,
            ContentSwapGCM4 = 0x80,
            ContentSwapGCM5 = 0x84,
            ContentSwapGCM6 = 0x88,
            ContentSwapGCM7 = 0x8C,
        }
    }
}
