//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public class SecureEngineMailbox_1 : IDoubleWordPeripheral, IKnownSize
    {
        public SecureEngineMailbox_1(Machine machine)
        {
            this.machine = machine;

            fifo = new Queue<uint>();
            
            RxIRQ = new GPIO();
            TxIRQ = new GPIO();
            
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            fifo.Clear();
            wordsLeftToBeReceived = 0;
            rxHeader = ResponseCode.InternalError;
        }

        public uint ReadDoubleWord(long offset)
        {
            var result = 0U;

            try
            {
                if(registersCollection.TryRead(offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", offset, (Registers)offset, result);
            }

            this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", offset, (Registers)offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
            if(!registersCollection.TryWrite(offset, value))
            {
                this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
                return;
            }
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => !FifoIsAlmostFull, name: "TXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => FifoIsFull, name: "TXFULL")
                    .WithReservedBits(22, 1)
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "TXERROR")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.RxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, out rxInterrupt, FieldMode.Read, name: "RXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXEMPTY")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXHDR")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXERROR")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.TxProtection, new DoubleWordRegister(this)
                    .WithReservedBits(0, 21)
                    .WithTaggedFlag("UNPROTECTED", 21)
                    .WithTaggedFlag("PRIVILGED", 22)
                    .WithTaggedFlag("NONSECURE", 23)
                    .WithTag("USER", 24, 8)
                },
                {(long)Registers.RxProtection, new DoubleWordRegister(this)
                    .WithReservedBits(0, 21)
                    .WithTaggedFlag("UNPROTECTED", 21)
                    .WithTaggedFlag("PRIVILGED", 22)
                    .WithTaggedFlag("NONSECURE", 23)
                    .WithTag("USER", 24, 8)
                },
                {(long)Registers.TxHeader, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => { TxHeader = (uint)value; }, name: "TXHEADER")
                },
                {(long)Registers.RxHeader, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)RxHeader, name: "RXHEADER")
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithFlag(0, out txInterruptEnable, name: "TXINTEN")
                    .WithFlag(1, out rxInterruptEnable, name: "RXINTEN")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)Registers.Fifo0;
            var blockSize = (long)Registers.Fifo1 - (long)Registers.Fifo0;

            for(var index = 0; index < FifoWordSize; index++)
            {
                var i = index;
                
                registerDictionary.Add(startOffset + blockSize*i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => FifoDequeue(), writeCallback: (_, value) => { FifoEnqueue((uint)value); }, name: $"FIFO{i}")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x7F;
        public GPIO RxIRQ { get; }
        public GPIO TxIRQ { get; }

        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;

#region fields
        private const uint FifoWordSize = 16;
        // TODO: according to the design book, TXSTATUS.TXINT field: "Interrupt status (same value as interrupt signal). 
        // High when TX FIFO is not almost-full (enough available space to start sending a message)."
        // As of now I don't know what "enough available space to send a message" means, so for now I assume a message
        // needs the whole FIFO.
        private const uint FifoAlmostFullThreshold = 1;
        private const uint DmaDescriptorTerminator = 0x00000001;
        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private Queue<uint> fifo;
        private int FifoWordsCount => fifo.Count;
        private bool FifoIsAlmostFull => (FifoWordsCount >= FifoAlmostFullThreshold);
        private bool FifoIsFull => (FifoWordsCount == FifoWordSize);
        private bool FifoIsEmpty => (FifoWordsCount == 0);
        private ResponseCode rxHeader = ResponseCode.InternalError;
        private uint wordsLeftToBeReceived = 0;
        private IFlagRegisterField txInterruptEnable;
        private IFlagRegisterField rxInterruptEnable;
        private IFlagRegisterField rxInterrupt;

        private uint TxHeader
        {
            set
            {
                // Setting the TX header starts a new "transaction".
                // The TX header as per HOST code, appears to be simply the number of 
                // bytes (multiple of 4) that constitute the message.
                // At minimum a message contains:
                // - The header itself
                // - The command ID
                // - Address in memory of command data input
                // - Address in memory of command data output
                // - Zero or more parameters
                wordsLeftToBeReceived = value / 4;

                // The TX header is also placed in the FIFO
                FifoEnqueue(value);
            }
        }

        private ResponseCode RxHeader
        {
            set
            {
                rxHeader = value;
            }
            get
            {
                var retValue = rxHeader;
                // After reading the rxHeader, we set it back to some non-success status and clear RXINT.
                rxHeader = ResponseCode.InternalError;
                rxInterrupt.Value = false;
                UpdateInterrupts();
                return retValue;
            }
        }
#endregion

#region system methods
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                // TXINT: Interrupt status (same value as interrupt signal). 
                // High when TX FIFO is not almost-full (enough available space to start sending a message).
                var irq = txInterruptEnable.Value && !FifoIsAlmostFull;
                if (irq)
                {
                    this.Log(LogLevel.Info, "IRQ TX set");
                }
                TxIRQ.Set(irq);

                // RXINT: Interrupt status (same value as interrupt signal). High when RX FIFO is not almost-empty 
                // or when the end of the message is ready in the FIFO (enough data available to start reading).
                irq = rxInterruptEnable.Value && /* TODO */ false;
                if (irq)
                {
                    this.Log(LogLevel.Info, "IRQ RX set");
                }
                RxIRQ.Set(irq);
            });
        }

        private void FifoEnqueue(uint value)
        {
            if (!FifoIsFull)
            {
                fifo.Enqueue(value);

                wordsLeftToBeReceived--;
                if (wordsLeftToBeReceived == 0)
                {
                    ProcessCommand();
                }

                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Error, "FifoEnqueue(): queue is FULL!");
            }
        }

        private uint FifoDequeue()
        {
            uint ret = 0;

            if (!FifoIsEmpty)
            {
                ret = fifo.Dequeue();
                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Error, "FifoDequeue(): queue is EMPTY!");
            }

            return ret;
        }

        private void ProcessCommand()
        {
            if (FifoIsEmpty)
            {
                this.Log(LogLevel.Error, "ProcessCommand(): Queue is EMPTY!");
                WriteResponse(ResponseCode.InvalidParameter);
                return;
            }

            uint header = FifoDequeue();

            // First 2 bytes of the header is the number of bytes in the message (header included)
            var wordsCount = (header & 0xFFFF)/4;
            
            if (FifoWordsCount < wordsCount - 1)
            {
                this.Log(LogLevel.Error, "ProcessCommand(): Not enough words FifoSize={0}, expectedWords={1}", FifoWordsCount, wordsCount - 1);
                WriteResponse(ResponseCode.InvalidParameter);
                return;

            }

            var commandOptions = FifoDequeue();
            var commandId = (CommandId)(commandOptions >> 16);
            commandOptions &= 0xFFFF;
            var inputDmaDescriptorPtr = FifoDequeue();
            var outputDmaDescriptorPtr = FifoDequeue();

            var commandParamsCount = wordsCount - 4;
            uint[] commandParams = new uint[13];            
            for(var i = 0; i < commandParamsCount; i ++)
            {
                commandParams[i] = FifoDequeue();
            }

            this.Log(LogLevel.Info, "ProcessCommand(): command ID={0} command Options=0x{1:X} command params count={2}", commandId, commandOptions, commandParamsCount);

            ResponseCode responseCode;

            switch(commandId)
            {
                case CommandId.ImportKey:
                    responseCode = HandleImportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.Hash:
                    responseCode = HandleHashCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.HashUpdate:
                    responseCode = HandleHashUpdateCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.HashFinish:
                    responseCode = HandleHashFinishCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.Encrypt:
                    responseCode = HandleEncryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.CcmEncrypt:
                    responseCode = HandleCcmEncryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.CcmDecrypt:
                    responseCode = HandleCcmDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.Random:
                    responseCode = HandleRandomCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                default:
                    responseCode = ResponseCode.InvalidCommand;
                    this.Log(LogLevel.Error, "ProcessCommand(): Command ID 0x{0:X} not handled!", (uint)commandId);
                    break;
            }

            if (responseCode != ResponseCode.Ok)
            {
                this.Log(LogLevel.Error, "ProcessCommand(): Response code {0}", responseCode);
            }
            
            // TODO: for now we write the response only if the command was successfully handled, so that we catch
            // non-implemented commands (the HOST MCU waiting for the RxHeader to be written).
            if (responseCode == ResponseCode.Ok)
            {
                WriteResponse(responseCode);
            }
        }

        private void WriteResponse(ResponseCode code)
        {
            RxHeader = code;
            rxInterrupt.Value = true;
            UpdateInterrupts();
        }
#endregion

#region command handlers
        private ResponseCode HandleImportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 1)
            {
                this.Log(LogLevel.Error, "HandleImportKeyCommand(): invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            
            this.Log(LogLevel.Noisy, "HandleImportKeyCommand(): keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            // First input DMA descriptor contains the plaintext key
            uint plaintextKeyPtr;
            uint keyLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out plaintextKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleImportKeyCommand(): keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     plaintextKeyPtr, keyLength, transferOptions, nextDescriptorPtr);

            // Input DMA has actually a second descriptor containing 8 bytes all set to zeros. I assume that is the auth_data[8] field,
            // TODO: For now we don't use the auth_data to do anything so we just ignore it.

            // TODO: for now we don't handle "volatile" (internal storage) mode.
            if (keyMode == KeyMode.Volatile)
            {
                return ResponseCode.InvalidParameter;
            }

            uint outputDataPtr;
            uint outputDataLength;
            DmaTranferOptions outputTransferOptions;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr);

            // Output DMA is expected to be structured as follows:
            // - 12 bytes: random IV assigned at time of wrapping
            // - var bytes: encrypted key data
            // - 16 bytes: AESGCM authentication tag
            if (outputDataLength != (12 + keyLength + 16))
            {
                return ResponseCode.InvalidParameter;
            }

            // All other key modes write a wrapped-up flavor of the key to the output DMA.
            // TODO: for now we simply copy the plaintext key to the output DMA. Random IV and AESGCM tags are zeroed out.
            uint offset = 0;
            machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0);
            machine.SystemBus.WriteDoubleWord(outputDataPtr + 4, 0);
            machine.SystemBus.WriteDoubleWord(outputDataPtr + 8, 0);
            // random IV
            for(uint i = 0; i < 3; i++)
            {
                machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0);
                offset += 4;
            }
            // "encrypted" key data
            for(uint i = 0; i < keyLength/4; i++)
            {
                uint keyWord = (uint)machine.SystemBus.ReadDoubleWord(plaintextKeyPtr + i*4);
                machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, keyWord);
                offset += 4;
            }
            // AESGCM authentication tag
            for(uint i = 0; i < 4; i++)
            {
                machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0);
                offset += 4;
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            // TODO

            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashUpdateCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            // TODO

            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashFinishCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            // TODO
            
            return ResponseCode.Ok;
        }

        private ResponseCode HandleEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 2)
            {
                this.Log(LogLevel.Error, "HandleEncryptCommand(): invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            CryptoMode cryptoMode = (CryptoMode)((commandOptions & 0xF00) >> 8);
            ContextMode contextMode = (ContextMode)(commandOptions & 0xF);

            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): cryptoMode={0} contextMode={1}", cryptoMode, contextMode);

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint dataSize = commandParams[1];
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): dataSize={0}", dataSize);

            // Check that the length is compatible with the crypto mode
            if (!IsDataLengthValid(dataSize, cryptoMode))
            {
                return ResponseCode.InvalidParameter;
            }
            
            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            
            // Third input DMA descriptor contains the IV
            // The input IV size is 0 for ECB mode and 16 for all other modes
            uint inputIvPtr = 0;
            uint inputIvLength = 0;
            if (cryptoMode != CryptoMode.Ecb)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out inputIvPtr, out inputIvLength, out transferOptions, out nextDescriptorPtr);
                this.Log(LogLevel.Noisy, "HandleEncryptCommand(): INPUT2: inputIvPtr=0x{0:X} inputIvLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputIvPtr, inputIvLength, transferOptions, nextDescriptorPtr);
            }

            // Fourth input DMA descriptor contains plain data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): INPUT3: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // First output DMA descriptor contains encrypted data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleEncryptCommand(): OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the IV
            // The output IV size is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            uint outputIvPtr = 0;
            uint outputIvLength = 0;
            if (cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.WholeMessage && contextMode != ContextMode.EndOfMessage)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out outputIvPtr, out outputIvLength, out transferOptions, out nextDescriptorPtr);
                this.Log(LogLevel.Noisy, "HandleEncryptCommand(): OUTPUT1: outputIvPtr=0x{0:X} outputIvLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputIvPtr, outputIvLength, transferOptions, nextDescriptorPtr);
            }

            // TODO: for now we copy the plaintext to the destination without actually encrypting it
            for (uint i = 0; i < dataSize; i += 4)
            {
                uint word = (uint)machine.SystemBus.ReadDoubleWord(inputDataPtr);
                machine.SystemBus.WriteDoubleWord(outputDataPtr + i, word);
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleCcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 4)
            {
                this.Log(LogLevel.Error, "HandleCcmEncryptCommand(): invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint associatedAuthenticatedDataSize = commandParams[2];
            uint inputDataSize = commandParams[3];
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): Other Command Params: tagSize={0} nonceSize={1} aadSize={2} inputDataSize={3}",
                     tagSize, nonceSize, associatedAuthenticatedDataSize, inputDataSize);

            if (!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            
            // Third input DMA descriptor contains the nonce data
            uint noncePtr;
            uint nonceLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out noncePtr, out nonceLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): INPUT2: noncePtr=0x{0:X} nonceLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     noncePtr, nonceLength, transferOptions, nextDescriptorPtr);

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     aadPtr, aadLength, transferOptions, nextDescriptorPtr);

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // First output DMA descriptor contains the encrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the output tag
            uint outputTagPtr;
            uint outputTagLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputTagPtr, out outputTagLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmEncryptCommand(): OUTPUT1: outputTagPtr=0x{0:X} outputTagLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputTagPtr, outputTagLength, transferOptions, nextDescriptorPtr);
            

            // TODO: for now we copy the plaintext to the output data buffer as is and set the tag to all 0s.
            for(uint i = 0; i < inputDataSize; i++)
            {
                byte b = (byte)machine.SystemBus.ReadByte(inputDataPtr + i);
                machine.SystemBus.WriteByte(outputDataPtr + i, b);
            } 

            for(uint i = 0; i < tagSize; i++)
            {
                machine.SystemBus.WriteByte(outputTagPtr + i, 0x00);
            } 

            return ResponseCode.Ok;
        }

        private ResponseCode HandleCcmDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 4)
            {
                this.Log(LogLevel.Error, "HandleCcmDecryptCommand(): invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint associatedAuthenticatedDataSize = commandParams[2];
            uint inputDataSize = commandParams[3];
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): Other Command Params: tagSize={0} nonceSize={1} aadSize={2} inputDataSize={3}",
                     tagSize, nonceSize, associatedAuthenticatedDataSize, inputDataSize);

            if (!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            
            // Third input DMA descriptor contains the nonce data
            uint noncePtr;
            uint nonceLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out noncePtr, out nonceLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): INPUT2: noncePtr=0x{0:X} nonceLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     noncePtr, nonceLength, transferOptions, nextDescriptorPtr);

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     aadPtr, aadLength, transferOptions, nextDescriptorPtr);

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // First output DMA descriptor contains the encrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleCcmDecryptCommand(): OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);            

            // TODO: for now we copy the "encrpyted" data to the output data buffer as is (it was never encrypted in first place)
            for(uint i = 0; i < inputDataSize; i++)
            {
                byte b = (byte)machine.SystemBus.ReadByte(inputDataPtr + i);
                machine.SystemBus.WriteByte(outputDataPtr + i, b);
            } 

            return ResponseCode.Ok;
        }

        private ResponseCode HandleRandomCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 1)
            {
                this.Log(LogLevel.Error, "HandleRandomCommand(): invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint randomLength = commandParams[0];

            this.Log(LogLevel.Noisy, "HandleRandomCommand(): random length = {0}", randomLength);

            // First output DMA descriptor contains the random data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr);
            this.Log(LogLevel.Noisy, "HandleRandomCommand(): OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            for(uint i = 0; i < randomLength; i++)
            {
                byte b = (byte)random.Next();
                machine.SystemBus.WriteByte(outputDataPtr + i, b);
            }
            
            return ResponseCode.Ok;                     
        }
#endregion

#region command utility methods
        private void UnpackDmaDescriptor(uint dmaPtr, out uint dataPtr, out uint dataSize, out DmaTranferOptions options, out uint nextDescriptorPtr)
        {
            dataPtr = (uint)machine.SystemBus.ReadDoubleWord(dmaPtr);
            nextDescriptorPtr = (uint)machine.SystemBus.ReadDoubleWord(dmaPtr + 4);
            var word2 = (uint)machine.SystemBus.ReadDoubleWord(dmaPtr + 8);
            dataSize = word2 & 0xFFFFFFF;
            options = (DmaTranferOptions)(word2 >> 28);
            
            this.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): dataPtr=0x{0:X} dataSize={1} options={2} nextDescriptorPtr=0x{3:X}",
                     dataPtr, dataSize, options, nextDescriptorPtr);
            for(uint i = 0; i < dataSize; i+=4)
            {
                this.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): DATA word{0}=0x{1:X}", i/4, machine.SystemBus.ReadDoubleWord(dataPtr+i));
            }
        }

        private void UnpackKeyMetadata(uint keyMetadata, out uint index, out KeyType type, out KeyMode mode, out KeyRestriction restriction)
        {
            // [31:28]
            type = (KeyType)((keyMetadata >> 28) & 0xF);
            // [27:26]
            mode = (KeyMode)((keyMetadata >> 26) & 0x3);
            // [25:24]
            restriction = (KeyRestriction)((keyMetadata >> 24) & 0x3);
            // [23:16]
            index = ((keyMetadata >> 16) & 0xFF);

            this.Log(LogLevel.Noisy, "UnpackKeyMetadata(): keyMetadata=0x{0:X} index={1} type={2} mode={3} restriction={4}",
                     keyMetadata, index, type, mode, restriction);
        }

        bool IsTagSizeValid(uint tagSize)
        {
            // Tag size must be 0, 4, 6, 8, 10, 12, 14 or 16 bytes
            return (tagSize == 0 || tagSize == 4 || tagSize == 6 || tagSize == 8 || tagSize == 10 || tagSize == 12 || tagSize == 14 || tagSize == 16);
        }

        bool IsNonceSizeValid(uint nonceSize)
        {
            return (nonceSize >= 8 && nonceSize <= 13);
        }

        bool IsDataLengthValid(uint dataSize, CryptoMode cryptoMode)
        {
            bool lengthOk = false;
            switch(cryptoMode)
            {
                case CryptoMode.Ecb:
                case CryptoMode.Cfb:
                case CryptoMode.Ofb:
                    // Must be > 0 and multiple of 16
                    lengthOk = ((dataSize > 0) && ((dataSize & 0xF) == 0));
                    break;
                case CryptoMode.Cbc:
                    // Must be > 16
                    lengthOk = (dataSize > 16);
                    break;
                case CryptoMode.Ctr:
                    // Must be > 0
                    lengthOk = (dataSize > 0);
                    break;                
            }

            return lengthOk;
        }
#endregion

#region enums
        private enum Registers
        {
            Fifo0           = 0x00,
            Fifo1           = 0x04,
            Fifo2           = 0x08,
            Fifo3           = 0x0C,
            Fifo4           = 0x10,
            Fifo5           = 0x14,
            Fifo6           = 0x18,
            Fifo7           = 0x1C,
            Fifo8           = 0x20,
            Fifo9           = 0x24,
            Fifo10          = 0x28,
            Fifo11          = 0x2C,
            Fifo12          = 0x30,
            Fifo13          = 0x34,
            Fifo14          = 0x38,
            Fifo15          = 0x3C,
            TxStatus        = 0x40,
            RxStatus        = 0x44,
            TxProtection    = 0x48,
            RxProtection    = 0x4C,
            TxHeader        = 0x50,
            RxHeader        = 0x54,
            Config          = 0x58,
        }

        private enum CommandId
        {
            ImportKey            = 0x0100,
            Hash                 = 0x0300,
            HashUpdate           = 0x0301,
            HashFinish           = 0x0303,
            Encrypt              = 0x0400,
            CcmEncrypt           = 0x0405,
            CcmDecrypt           = 0x0406,
            Random               = 0x0700,
        }

        private enum ResponseCode
        {
            Ok                 = 0x00000000,
            InvalidCommand     = 0x00010000,
            AuthorizationError = 0x00020000,
            InvalidSignature   = 0x00030000,
            BusError           = 0x00040000,
            InternalError      = 0x00050000,
            CryptoError        = 0x00060000,
            InvalidParameter   = 0x00070000,
            SecureBootError    = 0x00090000,
            SelfTestError      = 0x000A0000,
            NotInitialized     = 0x000B0000,
            MailboxInvalid     = 0x00FE0000,
            Abort              = 0x00FF0000,
        }

        private enum KeyType
        {
            Raw                         = 0x0,
            EccWeirstrabPrimeFieldCurve = 0x8,
            EccMontgomeryCurve          = 0xB,
            Ed25519                     = 0xC,
        }

        private enum KeyMode
        {
            Unprotected        = 0x0,
            Volatile           = 0x1,
            Wrapped            = 0x2,
            WrappedAntiReplay  = 0x3,
        }

        private enum KeyRestriction
        {
            Unlocked   = 0x0,
            Locked     = 0x1,
            Internal   = 0x2,
            Restricted = 0x3,
        }

        private enum DmaTranferOptions
        {
            Register       = 0x1,
            MemoryRealign  = 0x2,
            Discard        = 0x4,
        }
        private enum ContextMode
        {
            WholeMessage    = 0x0,
            StartOfMessage  = 0x1,
            EndOfMessage    = 0x2,
            MiddleOfMessage = 0x3,
        }

        private enum CryptoMode
        {
            Ecb = 0x1,
            Cbc = 0x2,
            Ctr = 0x3,
            Cfb = 0x4,
            Ofb = 0x5,
        }
#endregion        
    }
}
