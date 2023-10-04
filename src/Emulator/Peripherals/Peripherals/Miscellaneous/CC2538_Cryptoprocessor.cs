//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Crypto;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class CC2538_Cryptoprocessor : IDoubleWordPeripheral, IKnownSize
    {
        public CC2538_Cryptoprocessor(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);
            Interrupt = new GPIO();

            var keyStoreWrittenRegister = new DoubleWordRegister(this);
            var keyStoreWriteAreaRegister = new DoubleWordRegister(this);
            for(var i = 0; i < NumberOfKeys; i++)
            {
                var j = i;
                keyStoreWrittenRegister.DefineFlagField(i, writeCallback: (_, value) => { if(value) keys[j] = null; }, valueProviderCallback: _ => keys[j] != null, name: "RAM_AREA_WRITTEN" + i);
                keyStoreWriteAreaRegister.DefineFlagField(i, writeCallback: (_, value) => keyStoreWriteArea[j] = value, valueProviderCallback: _ => keyStoreWriteArea[j], name: "RAM_AREA" + i);
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.DmaChannel0Control, new DoubleWordRegister(this)
                    .WithFlag(0, out dmaInputChannelEnabled, name: "EN")
                    .WithFlag(1, name: "PRIO") // priority is not handled
                },
                {(long)Registers.DmaChannel0ExternalAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out dmaInputAddress)
                },
                {(long)Registers.DmaChannel0Length, new DoubleWordRegister(this)
                    .WithValueField(0, 15, writeCallback: (_, value) => DoInputTransfer((int)value), valueProviderCallback: _ => 0)
                },
                {(long)Registers.DmaChannel1Control, new DoubleWordRegister(this)
                    .WithFlag(0, out dmaOutputChannelEnabled, name: "EN")
                    .WithFlag(1, name: "PRIO") // priority is not handled
                },
                {(long)Registers.DmaChannel1ExternalAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out dmaOutputAddress)
                },
                {(long)Registers.DmaChannel1Length, new DoubleWordRegister(this)
                    .WithValueField(0, 15, writeCallback: (_, value) => DoOutputTransfer((int)value), valueProviderCallback: _ => 0)
                },
                {(long)Registers.KeyStoreWriteArea, keyStoreWriteAreaRegister},
                {(long)Registers.KeyStoreWrittenArea, keyStoreWrittenRegister},
                {(long)Registers.KeyStoreSize, new DoubleWordRegister(this)
                    .WithEnumField(0, 2, out keySize)
                },
                {(long)Registers.KeyStoreReadArea, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out selectedKey)
                    .WithFlag(31, FieldMode.Read, name: "BUSY", valueProviderCallback: _ => false)
                },
                {(long)Registers.AesControl, new DoubleWordRegister(this)
                    .WithEnumField(2, 1, out direction)
                    .WithFlag(5, out cbcEnabled)
                    .WithFlag(6, out ctrEnabled)
                    .WithEnumField(7, 2, out counterWidth)
                    .WithFlag(15, out cbcMacEnabled)
                    .WithValueField(16, 2, out gcmEnabled, name: "GCM")
                    .WithFlag(18, out ccmEnabled)
                    .WithValueField(19, 3, out ccmLengthField, name: "CCM_L")
                    .WithValueField(22, 3, out ccmLengthOfAuthenticationField, name: "CCM_M")
                    .WithFlag(29, out saveContext)
                    .WithFlag(30, out savedContextReady, mode: FieldMode.Read | FieldMode.WriteOneToClear)
                },
                {(long)Registers.AesCryptoLength0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => aesOperationLength = checked((int)value))
                },
                {(long)Registers.AesCryptoLength1, new DoubleWordRegister(this)
                    .WithValueField(0, 29, FieldMode.Write, writeCallback: (_, value) => { if(value != 0) this.Log(LogLevel.Error, "Unsupported crypto length that spans more than one register."); })
                },
                {(long)Registers.AlgorithmSelection, new DoubleWordRegister(this)
                    .WithEnumField(0, 3, out dmaDestination, name: "KEY-STORE AES HASH")
                    .WithFlag(31, name: "TAG")
                },
                {(long)Registers.InterruptConfiguration, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptIsLevel)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out resultInterruptEnabled)
                    .WithFlag(1, out dmaDoneInterruptEnabled)
                },
                {(long)Registers.InterruptClear, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, value) => { if(value) { resultInterrupt = false; RefreshInterrupts(); } }, valueProviderCallback: _ => false )
                    .WithFlag(1, writeCallback: (_, value) => { if(value) { dmaDoneInterrupt = false; RefreshInterrupts(); } }, valueProviderCallback: _ => false )
                    .WithFlag(29, FieldMode.Read, name: "KEY_ST_RD_ERR")
                    .WithFlag(30, writeCallback: (_, value) => { if(value) { keyStoreWriteErrorInterrupt = false; RefreshInterrupts(); } }, valueProviderCallback: _ => false, name: "KEY_ST_WR_ERR")
                    .WithFlag(31, FieldMode.Read, name: "DMA_BUS_ERR")
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => resultInterrupt, name: "RESULT_AV")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => dmaDoneInterrupt, name: "DMA_IN_DONE")
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => keyStoreWriteErrorInterrupt, name: "KEY_ST_WR_ERR")
                },
                {(long)Registers.AesAuthLength, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out aesAuthLength)
                }
            };

            for(var i = 0; i < 4; i++)
            {
                var j = i;
                var ivRegister = new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => BitConverter.GetBytes((uint)value).CopyTo(inputVector, j * 4),
                                            valueProviderCallback: _ => BitConverter.ToUInt32(inputVector, j * 4));
                registersMap.Add((long)Registers.AesInputVector + 4 * i, ivRegister);

                var tagRegister = new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => BitConverter.GetBytes((uint)value).CopyTo(tag, j * 4),
                                            valueProviderCallback: _ => BitConverter.ToUInt32(tag, j * 4));
                registersMap.Add((long)Registers.AesTagOut + 4 * i, tagRegister);
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
            Reset();
        }

        public void Reset()
        {
            registers.Reset();

            aesOperationLength = 0;

            keyStoreWriteErrorInterrupt = false;
            dmaDoneInterrupt = false;
            resultInterrupt = false;
            RefreshInterrupts();

            keys = new byte[NumberOfKeys][];
            keyStoreWriteArea = new bool[NumberOfKeys];
            inputVector = new byte[AesBlockSizeInBytes];
            tag = new byte[AesBlockSizeInBytes];
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size
        {
            get
            {
                return 0x800;
            }
        }

        public GPIO Interrupt { get; private set; }

        private void RefreshInterrupts()
        {
            var value = (resultInterruptEnabled.Value && resultInterrupt) || (dmaDoneInterruptEnabled.Value && dmaDoneInterrupt) || keyStoreWriteErrorInterrupt;
            this.Log(LogLevel.Debug, "Setting Interrupt to {0}.", value);
            Interrupt.Set(value);
            if(!interruptIsLevel.Value)
            {
                keyStoreWriteErrorInterrupt = false;
                dmaDoneInterrupt = false;
                resultInterrupt = false;
                Interrupt.Unset();
            }
        }

        private void ProcessDataInMemory(uint inputAddress, uint? outputAddress, int length, Action<Block> processor, Block data = null)
        {
            SysbusWriter writer = null;
            var reader = new SysbusReader(sysbus, inputAddress, length);
            if(outputAddress.HasValue)
            {
                writer = new SysbusWriter(sysbus, outputAddress.Value, length);
            }

            if(data == null)
            {
                data = Block.OfSize(AesBlockSizeInBytes);
            }
            while(!reader.IsFinished)
            {
                reader.Read(data);
                data.PadSpaceLeft(0);
                processor(data);
                if(writer != null)
                {
                    writer.Write(data.Buffer);
                }
                data.Index = 0;
            }
        }

        private void DoInputTransfer(int length)
        {
            if(!dmaInputChannelEnabled.Value)
            {
                this.Log(LogLevel.Warning, "DMA input transfer detected, but input channel is not enabled. Ignoring it.");
                return;
            }
            if(length == 0)
            {
                this.Log(LogLevel.Warning, "DMA input transfer of length 0 detected. Ignoring it.");
                return;
            }

            switch(dmaDestination.Value)
            {
            case DmaDestination.KeyStore:
                HandleKeyTransfer(length);
                break;
            case DmaDestination.HashEngine:
                this.Log(LogLevel.Error, "Hash engine is not supported.");
                return;
            case DmaDestination.Aes:
                if(!HandleInputAesTransfer(length))
                {
                    return;
                }
                break;
            default:
                var isKeyStoreSelected = (((int)dmaDestination.Value) & (int)DmaDestination.KeyStore) != 0;
                var isAesSelected = (((int)dmaDestination.Value) & (int)DmaDestination.Aes) != 0;
                var isHashSelected = (((int)dmaDestination.Value) & (int)DmaDestination.HashEngine) != 0;
                throw new InvalidOperationException(string.Format("Invalid combination of algorithm selection flags: [KEY STORE: {0}, AES: {1}, HASH: {2}]",
                    isKeyStoreSelected, isAesSelected, isHashSelected));
            }

            resultInterrupt = true;
            dmaDoneInterrupt = true;
            RefreshInterrupts();
        }

        private void DoOutputTransfer(int length)
        {
            if(!dmaOutputChannelEnabled.Value)
            {
                this.Log(LogLevel.Warning, "DMA output transfer detected, but output channel is not enabled. Ignoring it.");
                return;
            }
            if(dmaDestination.Value != DmaDestination.Aes)
            {
                this.Log(LogLevel.Error, "Output transfer is implemented for AES destination only, but not for {0}. Ignoring the transfer.", dmaDestination.Value);
                return;
            }
            if(length != aesOperationLength)
            {
                this.Log(LogLevel.Error, "AES operation in which dma length is different than aes length is not supported. Ignoring the transfer.");
                return;
            }

            // here the real cipher operation begins
            if(cbcEnabled.Value)
            {
                HandleCbc(length);
            }
            else if(ccmEnabled.Value)
            {
                if(direction.Value == Direction.Encryption)
                {
                    HandleCcmEncryption(length);
                }
                else
                {
                    HandleCcmDecryption(length);
                    if(saveContext.Value)
                    {
                        savedContextReady.Value = true;
                    }
                }
            }
            else if(ctrEnabled.Value)
            {
                HandleCtr(length);
            }
            else if(gcmEnabled.Value != 0)
            {
                this.Log(LogLevel.Error, "GCM mode is not supported");
            }
            else if(!cbcEnabled.Value && !ctrEnabled.Value && (counterWidth.Value == 0) && !cbcMacEnabled.Value && (gcmEnabled.Value == 0) && !ccmEnabled.Value && (ccmLengthField.Value == 0) && (ccmLengthOfAuthenticationField.Value == 0))
            {
                // ECB mode is selected only if bits [28:5] in AesControl register are set to 0.
                HandleEcb(length);
            }
            else
            {
                this.Log(LogLevel.Error, "No supported cipher mode selected: CCM, CBC-MAC, CBC, CTR, ECB");
            }

            dmaDoneInterrupt = true;
            resultInterrupt = true;
            RefreshInterrupts();
        }

        private bool HandleInputAesTransfer(int length)
        {
            if(ccmEnabled.Value)
            {
                HandleCcmAuthentication(length);
            }
            else if(cbcMacEnabled.Value)
            {
                HandleCbcMac(length);
            }
            else if(cbcEnabled.Value || ctrEnabled.Value)
            {
                return false; // the real crypto operation will start on output transfer
            }
            else if(!cbcEnabled.Value && !ctrEnabled.Value && (counterWidth.Value == 0) && !cbcMacEnabled.Value && (gcmEnabled.Value == 0) && !ccmEnabled.Value && (ccmLengthField.Value == 0) && (ccmLengthOfAuthenticationField.Value == 0))

            {
                // ECB mode is selected only if bits [28:5] in AesControl register are set to 0.
                return false;
            }
            else
            {
                this.Log(LogLevel.Error, "No supported cipher mode selected: CCM, CBC-MAC, CBC, CTR, ECB");
            }

            if(saveContext.Value)
            {
                savedContextReady.Value = true;
            }
            return true;
        }

        private void HandleKeyTransfer(int length)
        {
            var totalNumberOfActivatedSlots = keyStoreWriteArea.Sum(x => x ? 1 : 0);
            var keyWriteSlotIndex = keyStoreWriteArea.IndexOf(x => true);
            var numberOfConsecutiveSlots = keyStoreWriteArea.Skip(keyWriteSlotIndex).TakeWhile(x => x == true).Count();

            if(totalNumberOfActivatedSlots != numberOfConsecutiveSlots)
            {
                this.Log(LogLevel.Warning, "Bits in key store write area are not set consecutively: {0}, ignoring transfer.", BitHelper.GetSetBitsPretty(BitHelper.GetValueFromBitsArray(keyStoreWriteArea)));
                keyStoreWriteErrorInterrupt = true;
                RefreshInterrupts();
                return;
            }

            if(length != numberOfConsecutiveSlots * KeyEntrySizeInBytes)
            {
                this.Log(LogLevel.Warning, "Transfer length {0}B is not consistent with the number selected slots: {1} (each of size {2}B). Ignoring transfer", length, numberOfConsecutiveSlots, KeyEntrySizeInBytes);
                keyStoreWriteErrorInterrupt = true;
                RefreshInterrupts();
                return;
            }

            var nonEmptyKeyStoreSlots = keys.Skip(keyWriteSlotIndex).Take(numberOfConsecutiveSlots).Select((v, i) => new { v, i }).Where(x => x.v != null).Select(x => x.i.ToString()).ToList();
            if(nonEmptyKeyStoreSlots.Count > 0)
            {
                this.Log(LogLevel.Warning, "Trying to write a key to a non empty key store: {0}, ignoring transfer.", string.Join(", ", nonEmptyKeyStoreSlots));
                keyStoreWriteErrorInterrupt = true;
                RefreshInterrupts();
                return;
            }

            for(var i = keyWriteSlotIndex; i < numberOfConsecutiveSlots; i++)
            {
                keys[i] = sysbus.ReadBytes(dmaInputAddress.Value, KeyEntrySizeInBytes);
                dmaInputAddress.Value += KeyEntrySizeInBytes;
            }
        }

        private void HandleCbcMac(int length)
        {
            if(inputVector.Any(x => x != 0))
            {
                this.Log(LogLevel.Warning, "Input vector in CBC-MAC mode should be set to all zeros!. Ignoring this transfer");
                return;
            }

            using(var aes = AesProvider.GetCbcMacProvider(GetSelectedKey()))
            {
                ProcessDataInMemory((uint)dmaInputAddress.Value, null, length, aes.EncryptBlockInSitu);
                aes.LastBlock.CopyTo(tag);
            }
        }

        private static void IncrementCounter(byte[] buffer, int counterWidth)
        {
            // This is just a manual increment of integer value stored in `counterWidth` LSB bytes of a buffer.
            // It must be ensured that in case of an overflow the rest of a buffer is not modified.
            for(int i = 0; i < (counterWidth + 1) * 4; i++)
            {
                if(unchecked(++buffer[buffer.Length - i - 1]) != 0)
                {
                    break;
                }
            }
        }

        private void HandleCtr(int length)
        {
            var ivBlock = Block.UsingBytes(inputVector);
            var encryptedNonceCounterBlock = Block.OfSize(AesBlockSizeInBytes);
            using(var aes = AesProvider.GetEcbProvider(GetSelectedKey()))
            {
                ProcessDataInMemory((uint)dmaInputAddress.Value, (uint)dmaOutputAddress.Value, length, b =>
                {
                    aes.EncryptBlock(ivBlock, encryptedNonceCounterBlock);
                    b.XorWith(encryptedNonceCounterBlock);
                    IncrementCounter(ivBlock.Buffer, (int)counterWidth.Value);
                });
            }
        }

        private void HandleEcb(int length)
        {
            using(var aes = AesProvider.GetEcbProvider(GetSelectedKey()))
            {
                var processor = direction.Value == Direction.Encryption
                    ? (Action<Block>)aes.EncryptBlockInSitu
                    : aes.DecryptBlockInSitu;

                ProcessDataInMemory((uint)dmaInputAddress.Value, (uint)dmaOutputAddress.Value, length, processor);
            }
        }

        private void HandleCbc(int length)
        {
            using(var aes = AesProvider.GetCbcProvider(GetSelectedKey(), inputVector))
            {
                var processor = direction.Value == Direction.Encryption
                    ? (Action<Block>)aes.EncryptBlockInSitu
                    : aes.DecryptBlockInSitu;

                ProcessDataInMemory((uint)dmaInputAddress.Value, (uint)dmaOutputAddress.Value, length, processor);
            }
        }

        private void HandleCcmAuthentication(int length)
        {
            if(ccmLengthOfAuthenticationField.Value == 0 && aesOperationLength == 0)
            {
                // no authentication header is calculated
                return;
            }

            var adataPresent = false;
            if(ccmCbcMacAesProvider == null)
            {
                // this is a first ccm dma transfer;
                // if it uses adata there will be a second one;

                // CCM mode uses CBC-MAC for authentication;
                ccmCbcMacAesProvider = AesProvider.GetCbcMacProvider(GetSelectedKey());

                ccmCbcMacAesProvider.EncryptBlockInSitu(GenerateB0Block());
                var adataBlock = GenerateFirstAdataBlock();
                if(adataBlock != null)
                {
                    adataPresent = true;
                    // there is adata
                    ProcessDataInMemory((uint)dmaInputAddress.Value, null, length, ccmCbcMacAesProvider.EncryptBlockInSitu, adataBlock);
                    if(aesOperationLength > 0)
                    {
                        // message data will be sent in a second dma transfer
                        return;
                    }
                }
            }

            if(!adataPresent)
            {
                if(aesOperationLength != length)
                {
                    this.Log(LogLevel.Warning, "Message data detected, but aes operation length ({0}) is different than this transfer length ({1}). Aborting the transfer.", aesOperationLength, length);
                    ccmCbcMacAesProvider.Dispose();
                    ccmCbcMacAesProvider = null;
                    return;
                }

                if(direction.Value == Direction.Decryption)
                {
                    // we must first decrypt the message data before calculating the tag
                    return;
                }

                // this is the second transfer with message data
                ProcessDataInMemory((uint)dmaInputAddress.Value, null, length, ccmCbcMacAesProvider.EncryptBlockInSitu);
            }

            // calculate tag
            GenerateS0Block().XorWith(ccmCbcMacAesProvider.LastBlock).CopyTo(tag);

            ccmCbcMacAesProvider.Dispose();
            ccmCbcMacAesProvider = null;
        }

        private void HandleCcmEncryption(int length)
        {
            // first, we increment a counter
            IncrementCounter(inputVector, (int)counterWidth.Value);
            HandleCtr(length);

            if(ccmCbcMacAesProvider != null)
            {
                ccmCbcMacAesProvider.Dispose();
                ccmCbcMacAesProvider = null;
            }
        }

        private void HandleCcmDecryption(int length)
        {
            // calculate s0 block before changing the counter (and input vector)
            var s0Block = GenerateS0Block();
            // first, increment a counter
            IncrementCounter(inputVector, (int)counterWidth.Value);
            // decrypt in CTR mode
            HandleCtr(length);

            if(ccmCbcMacAesProvider != null)
            {
                // calculate authentication from decrypted data
                ProcessDataInMemory((uint)dmaInputAddress.Value, null, length, ccmCbcMacAesProvider.EncryptBlockInSitu);
                // calculate tag
                s0Block.XorWith(ccmCbcMacAesProvider.LastBlock).CopyTo(tag);

                ccmCbcMacAesProvider.Dispose();
                ccmCbcMacAesProvider = null;
            }
        }

        private Block GenerateB0Block()
        {
            const int aesAuthLengthOffset = 6;
            const int ccmLengthOfAuthenticationFieldOffset = 3;

            var result = Block.OfSize(AesBlockSizeInBytes);
            // flags
            var flags = (byte)(((aesAuthLength.Value > 0 ? 1u : 0u) << aesAuthLengthOffset)
                + ((uint)ccmLengthOfAuthenticationField.Value << ccmLengthOfAuthenticationFieldOffset)
                + (uint)ccmLengthField.Value);
            result.UpdateByte(flags);
            // nonce
            var nonceLength = 15 - (int)(ccmLengthField.Value + 1);
            result.UpdateBytes(inputVector, 1, nonceLength);
            // l(m) - fill LSB with aes operation length
            while(result.SpaceLeft > 0)
            {
                result.UpdateByte(result.SpaceLeft > 4
                    ? (byte)0
                    : (byte)((aesOperationLength >> ((result.SpaceLeft - 1) * 8)) & 0xff));
            }
            return result;
        }

        private Block GenerateFirstAdataBlock()
        {
            const int twoOctetsThreshold = (1 << 16) - (1 << 8);

            var adataLength = (int)aesAuthLength.Value;
            if(adataLength == 0)
            {
                return null;
            }

            var result = Block.OfSize(AesBlockSizeInBytes);

            // encode a length
            if(aesAuthLength.Value < twoOctetsThreshold)
            {
                // use two LSB of adataLength
                result.UpdateByte((byte)(adataLength >> 8));
                result.UpdateByte((byte)adataLength);
            }
            else
            {
                // those are just magic numbers required by RFC 3610
                result.UpdateByte(0xff);
                result.UpdateByte(0xfe);

                // use four LSB of adataLength
                result.UpdateByte((byte)(adataLength >> 24));
                result.UpdateByte((byte)(adataLength >> 16));
                result.UpdateByte((byte)(adataLength >> 8));
                result.UpdateByte((byte)adataLength);
            }
            // standard allows for longer Adata fields, but we cannot express
            // them using 32-bit register architecture

            return result;
        }

        private Block GenerateS0Block()
        {
            var resultBlock = Block.WithCopiedBytes(inputVector);
            using(var aesEcb = new AesProvider(CipherMode.ECB, PaddingMode.None, GetSelectedKey()))
            {
                aesEcb.EncryptBlockInSitu(resultBlock);
            }
            return resultBlock;
        }

        private byte[] GetSelectedKey()
        {
            byte[] result;

            switch(keySize.Value)
            {
            case KeySize.Bits128:
                return keys[selectedKey.Value];
            case KeySize.Bits192:
                result = new byte[24];
                Array.Copy(keys[selectedKey.Value + 1], 0, result, 16, 8);
                break;
            case KeySize.Bits256:
                result = new byte[32];
                keys[selectedKey.Value + 1].CopyTo(result, 16);
                break;
            default:
                this.Log(LogLevel.Error, "Reserved key size value ({0}) used instead of the proper one. Using key consiting of 16 zeroed bytes.", keySize.Value);
                return new byte[16];
            }
            Array.Copy(keys[selectedKey.Value], result, 16);
            return result;
        }

        private AesProvider ccmCbcMacAesProvider;
        private bool dmaDoneInterrupt;
        private bool resultInterrupt;
        private bool keyStoreWriteErrorInterrupt;
        private int aesOperationLength;
        private byte[] inputVector;
        private byte[] tag;
        private bool[] keyStoreWriteArea;
        private byte[][] keys;
        private readonly IFlagRegisterField saveContext;
        private readonly IFlagRegisterField savedContextReady;
        private readonly IFlagRegisterField cbcEnabled;
        private readonly IFlagRegisterField ctrEnabled;
        private readonly IEnumRegisterField<CounterWidth> counterWidth;
        private readonly IFlagRegisterField cbcMacEnabled;
        private readonly IValueRegisterField gcmEnabled;
        private readonly IFlagRegisterField ccmEnabled;
        private readonly IValueRegisterField ccmLengthField;
        private readonly IValueRegisterField ccmLengthOfAuthenticationField;
        private readonly IEnumRegisterField<Direction> direction;
        private readonly IValueRegisterField aesAuthLength;
        private readonly IValueRegisterField dmaInputAddress;
        private readonly IValueRegisterField dmaOutputAddress;
        private readonly IValueRegisterField selectedKey;
        private readonly IFlagRegisterField dmaInputChannelEnabled;
        private readonly IFlagRegisterField dmaOutputChannelEnabled;
        private readonly IEnumRegisterField<KeySize> keySize;
        private readonly IEnumRegisterField<DmaDestination> dmaDestination;
        private readonly IFlagRegisterField interruptIsLevel;
        private readonly IFlagRegisterField resultInterruptEnabled;
        private readonly IFlagRegisterField dmaDoneInterruptEnabled;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IBusController sysbus;

        private const int NumberOfKeys = 8;
        private const int KeyEntrySizeInBytes = 16;
        private const int AesBlockSizeInBytes = 16;

        private enum Registers : uint
        {
            DmaChannel0Control = 0x0, // DMAC_CH0_CTRL
            DmaChannel0ExternalAddress = 0x4, // DMAC_CH0_EXTADDR
            DmaChannel0Length = 0xC, // DMAC_CH0_DMALENGTH
            DmaChannel1Control = 0x20, // DMAC_CH1_CTRL
            DmaChannel1ExternalAddress = 0x24, // DMAC_CH1_EXTADDR
            DmaChannel1Length = 0x2C, // DMAC_CH1_DMALENGTH
            KeyStoreWriteArea = 0x400, // AES_KEY_STORE_WRITE_AREA
            KeyStoreWrittenArea = 0x404, // AES_KEY_STORE_WRITTEN_AREA
            KeyStoreSize = 0x408, // AES_KEY_STORE_SIZE
            KeyStoreReadArea = 0x40C, // AES_KEY_STORE_READ_AREA
            AesInputVector = 0x540, // AES_AES_IV_0
            AesControl = 0x550, // AES_AES_CTRL
            AesCryptoLength0 = 0x554, // AES_AES_C_LENGTH_0
            AesCryptoLength1 = 0x558, // AES_AES_C_LENGTH_1
            AesAuthLength = 0x55C, // AES_AES_AUTH_LENGTH
            AesTagOut = 0x570, // AES_TAG_OUT_0
            AlgorithmSelection = 0x700, // AES_CTRL_ALG_SEL
            InterruptConfiguration = 0x780, // AES_CTRL_INT_CFG
            InterruptEnable = 0x784, // AES_CTRL_INT_EN
            InterruptClear = 0x788, // AES_CTRL_INT_CLR
            InterruptStatus = 0x790, // AES_CTRL_INT_STAT
        }

        private enum DmaDestination
        {
            KeyStore = 1,
            Aes = 2,
            HashEngine = 4
        }

        private enum KeySize
        {
            Bits128 = 1,
            Bits192 = 2,
            Bits256 = 3
        }

        private enum Direction
        {
            Decryption,
            Encryption
        }

        private enum CounterWidth
        {
            Bits32,
            Bits64,
            Bits96,
            Bits128
        }

        private abstract class SysbusReaderWriterBase
        {
            protected SysbusReaderWriterBase(IBusController bus, ulong startAddress, int length)
            {
                this.bus = bus;
                currentAddress = startAddress;
                bytesLeft = length;
            }

            public bool IsFinished { get { return bytesLeft == 0; } }

            protected ulong currentAddress;
            protected int bytesLeft;
            protected readonly IBusController bus;
        }

        private class SysbusReader : SysbusReaderWriterBase
        {
            public SysbusReader(IBusController bus, ulong startAddress, int length) : base(bus, startAddress, length)
            {
            }

            public int Read(Block destination)
            {
                var bytesToRead = Math.Min(bytesLeft, destination.SpaceLeft);
                bus.ReadBytes(currentAddress, bytesToRead, destination.Buffer, destination.Index);
                destination.Index += bytesToRead;
                currentAddress += (ulong)bytesToRead;
                bytesLeft -= bytesToRead;
                return bytesToRead;
            }
        }

        private class SysbusWriter : SysbusReaderWriterBase
        {
            public SysbusWriter(IBusController bus, ulong startAddress, int length) : base(bus, startAddress, length)
            {
            }

            public void Write(byte[] bytes)
            {
                var length = Math.Min(bytesLeft, bytes.Length);
                bus.WriteBytes(bytes, currentAddress, length);
                currentAddress += (ulong)length;
                bytesLeft -= length;
            }
        }
    }
}
