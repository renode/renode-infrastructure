//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class Silabs_SecureElement
    {
        public Silabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3)
            : this(machine, parent, txFifo, rxFifo, series3, 0, 0, 0, 0, 0, 0)
        {
        }

        public Silabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3, 
                            uint flashSize, uint flashPageSize, uint flashRegionSize, uint flashCodeRegionStart, uint flashCodeRegionEnd=0, uint flashDataRegionStart=0)
        {
            this.machine = machine;
            this.parent = parent;
            this.txFifo = txFifo;
            this.rxFifo = rxFifo;
            this.series3 = series3;
            this.flashSize = flashSize;
            this.flashPageSize = flashPageSize;
            this.flashRegionSize = flashRegionSize;
            this.flashCodeRegionStart = flashCodeRegionStart;
            this.flashCodeRegionEnd = flashCodeRegionEnd;
            this.flashDataRegionStart = flashDataRegionStart;
            
            // TODO: the SE adds an internal key at slot 246 used for NVM3 encryption operations. 
            // We just add an arbitrary key here to faciliate all NVM3 operations.
            byte[] key = {0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF};
            volatileKeys[246] = key;
        }

#region fields
        private readonly Machine machine;
        private readonly IDoubleWordPeripheral parent;
        private const uint NullDescriptor = 1;
        private readonly bool series3;
        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private Queue<uint> txFifo;
        private Queue<uint> rxFifo;
        private uint wordsLeftToBeReceived;
        // TODO: This is a HACK: the Bouncy Castle hash function implementation does not allow to
        // set the state, which is needed for HASH_UPDATE commands.
        // The solution is to keep around the hash engine until the HASH_FINAL command is called,
        // but this requires all these commands to happen sequentially, hence the hack.
        private IDigest currentHashEngine = null;
        // Series3 specific value.
        // Related to PSEC-5391, once that gets resolved, we might need to update this accordingly.
        private const uint QspiFlashHostBase = 0x1000000;
        private readonly uint flashSize;
        private readonly uint flashPageSize;
        private readonly uint flashRegionSize;
        private readonly uint flashCodeRegionStart;
        private readonly uint flashCodeRegionEnd;
        private readonly uint flashDataRegionStart;
        private Dictionary<uint, byte[]> volatileKeys = new Dictionary<uint, byte[]>();
#endregion

#region public methods
        public void Reset()
        {
            wordsLeftToBeReceived = 0;
        }

        public uint GetDefaultErrorStatus()
        {
            return (uint)ResponseCode.InternalError;
        }

        public void TxHeaderSetCallback(uint header)
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
            wordsLeftToBeReceived = header / 4;
        }

        public bool TxFifoEnqueueCallback(uint word)
        {
            if (wordsLeftToBeReceived == 0)
            {
                parent.Log(LogLevel.Error, "TxFifoEnqueueCallback: 0 words left");
                return false;
            }
            
            wordsLeftToBeReceived--;
            
            if (wordsLeftToBeReceived == 0)
            {
                ProcessCommand();
                return true;
            }

            return false;
        }
#endregion

#region private methods
        private void ProcessCommand()
        {
            uint commandHandle = 0;

            if (txFifo.Count == 0)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Queue is EMPTY!");                
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }

            uint header = txFifo.Dequeue();
            // First 2 bytes of the header is the number of bytes in the message (header included)
            uint wordsCount = (header & 0xFFFF)/4;
            
            if (txFifo.Count < wordsCount - 1)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Not enough words FifoSize={0}, expectedWords={1}", txFifo.Count, wordsCount - 1);
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }
            
            if (series3)
            {
                commandHandle = txFifo.Dequeue();
            }

            uint commandOptions = txFifo.Dequeue();
            var commandId = (CommandId)(commandOptions >> 16);
            commandOptions &= 0xFFFF;
            uint inputDmaDescriptorPtr = txFifo.Dequeue();
            uint outputDmaDescriptorPtr = txFifo.Dequeue();
            uint commandParamsCount = wordsCount - (series3 ? 5U : 4U);
            uint[] commandParams = new uint[13];            
            for(var i = 0; i < commandParamsCount; i ++)
            {
                commandParams[i] = txFifo.Dequeue();
            }

            parent.Log(LogLevel.Info, "ProcessCommand(): command ID={0} command Options=0x{1:X} command params count={2}", commandId, commandOptions, commandParamsCount);

            ResponseCode responseCode;

            switch(commandId)
            {
                case CommandId.ImportKey:
                    responseCode = HandleImportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.ExportKey:
                    responseCode = HandleExportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, series3);
                    break;
                case CommandId.Hash:
                    responseCode = HandleHashCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.HashUpdate:
                case CommandId.HashFinish:
                    responseCode = HandleHashUpdateOrFinishCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, (commandId == CommandId.HashFinish));
                    break;
                case CommandId.AesEncrypt:
                case CommandId.AesDecrypt:
                    responseCode = HandleAesEncryptOrDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, (commandId == CommandId.AesEncrypt));
                    break;
                case CommandId.AesCmac:
                    responseCode = HandleAesCmacCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.AesCcmEncrypt:
                    responseCode = HandleAesCcmEncryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.AesCcmDecrypt:
                    responseCode = HandleAesCcmDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.AesGcmEncrypt:
                    responseCode = HandleAesGcmEncryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.AesGcmDecrypt:
                    responseCode = HandleAesGcmDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.Random:
                    responseCode = HandleRandomCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                    break;
                case CommandId.ReadDeviceData:
                    responseCode = HandleReadDeviceDataCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.FlashEraseDataRegion:
                    responseCode = HandleFlashEraseDataRegionCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.FlashWriteDataRegion:
                    responseCode = HandleFlashWriteDataRegionCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.FlashGetDataRegionLocation:
                    responseCode = HandleFlashGetDataRegionLocationCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.FlashGetCodeRegionConfig:
                    responseCode = HandleFlashGetCodeRegionConfigCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                case CommandId.ConfigureQspiRefClock:
                    responseCode = HandleConfigureQspiRefClockCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                    break;
                default:
                    responseCode = ResponseCode.InvalidCommand;
                    parent.Log(LogLevel.Error, "ProcessCommand(): Command ID 0x{0:X} not handled!", (uint)commandId);
                    break;
            }

            if (responseCode != ResponseCode.Ok)
            {
                parent.Log(LogLevel.Info, "ProcessCommand(): Response code {0}", responseCode);
            }

            WriteResponse(responseCode, commandHandle);
        }

        private void WriteResponse(ResponseCode code, uint commandHandle)
        {
            rxFifo.Enqueue((uint)code);
            if (series3)
            {
                rxFifo.Enqueue(commandHandle);
            }
        }
#endregion

#region command handlers
        private ResponseCode HandleImportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "IMPORT_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            
            parent.Log(LogLevel.Noisy, "IMPORT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            if (keyMode == KeyMode.Unprotected || keyMode == KeyMode.WrappedAntiReplay)
            {
                parent.Log(LogLevel.Error, "HandleImportKeyCommand: invalid key mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor contains the plaintext key
            uint plaintextKeyPtr;
            uint keyLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out plaintextKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "IMPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     plaintextKeyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = new byte[keyLength];
            FetchFromRam(plaintextKeyPtr, key, 0, keyLength);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            
            // Input DMA has actually a second descriptor containing 8 bytes all set to zeros. I assume that is the auth_data[8] field,
            // TODO: For now we don't use the auth_data to do anything so we just ignore it.
            if (keyMode == KeyMode.Volatile)
            {
                // if keyMode == KeyMode.Volatile, we store the key in a dictionary, 
                // since no data will be returned and the key will be transferred into an internal slot or KSU.
                // in our case, dictionary within SE model.
                // TODO: Double check if when doing FetchFromRam we need to account for offset.
                parent.Log(LogLevel.Noisy, "Assigning VOLATILE key to index {0}", keyIndex);
                volatileKeys[keyIndex] = key;
                
                return ResponseCode.Ok;
            }
            else if (keyMode == KeyMode.Wrapped)
            {
                uint outputDataPtr;
                uint outputDataLength;
                DmaTranferOptions outputTransferOptions;
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);

                // Output DMA is expected to be structured as follows:
                // - 12 bytes: random IV assigned at time of wrapping
                // - var bytes: encrypted key data
                // - 16 bytes: AESGCM authentication tag
                if (outputDataLength != (12 + keyLength + 16))
                {
                    return ResponseCode.InvalidParameter;
                }

                // All other key modes write a wrapped-up flavor of the key to the output DMA.
                // TODO: for now we simply copy the plaintext key to the output DMA. 
                // Random IV and AESGCM tags are assigned to special markers.
                uint offset = 0;
                machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0);
                machine.SystemBus.WriteDoubleWord(outputDataPtr + 4, 0);
                machine.SystemBus.WriteDoubleWord(outputDataPtr + 8, 0);
                // random IV
                for(uint i = 0; i < 3; i++)
                {
                    machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0x15151515);
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
                    machine.SystemBus.WriteDoubleWord(outputDataPtr + offset, 0x5C5D5E5F);
                    offset += 4;
                }
                
                return ResponseCode.Ok;
            }

            return ResponseCode.InvalidParameter;
        }

        protected virtual ResponseCode HandleExportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, bool series3)
        {
            if (commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }            

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            
            parent.Log(LogLevel.Noisy, "EXPORT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                       keyIndex, keyType, keyMode, keyRestriction);

            if (keyMode == KeyMode.Unprotected || keyMode == KeyMode.WrappedAntiReplay)
            {
                parent.Log(LogLevel.Error, "HandleExportKeyCommand: invalid key mode");
                return ResponseCode.Abort;
            }

            if (keyMode == KeyMode.Volatile)
            {
                // key must be present in the dictionary to be exported.
                if (!volatileKeys.ContainsKey(keyIndex))
                {
                    return ResponseCode.InvalidParameter;
                }

                byte[] key = volatileKeys[keyIndex];
                uint outputDataPtr;
                uint outputDataLength;
                DmaTranferOptions transferOptions;
                uint nextDescriptorPtr;
                // No data input is required when exporting a VOLATILE key, only output DMA is used.
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "EXPORT_VOLATILE_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, key.Length, transferOptions, nextDescriptorPtr);

                if (outputDataLength != key.Length)
                {
                    return ResponseCode.InvalidParameter;
                }

                for (uint i = 0; i < key.Length / 4; i++)
                {
                    machine.SystemBus.WriteDoubleWord(outputDataPtr + i * 4, BitConverter.ToUInt32(key, (int)i * 4));
                }

                return ResponseCode.Ok;
            }
            else if (keyMode == KeyMode.Wrapped)
            {
                // Only "unlocked" keys can be exported.
                if (keyRestriction != KeyRestriction.Unlocked)
                {
                    return ResponseCode.AuthorizationError;
                }

                // First input DMA descriptor contains the wrapped key, expected to be structured as follows:
                // - 8 bytes: authorization data
                // - 12 bytes: random IV assigned at time of wrapping
                // - var bytes: encrypted key data
                // - 16 bytes: AESGCM authentication tag
                uint wrappedKeyPtr;
                uint keyLength;
                uint authDataPtr;
                uint authDataLength;            
                DmaTranferOptions transferOptions;
                uint nextDescriptorPtr;

                UnpackDmaDescriptor(inputDma, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
                UnpackDmaDescriptor(nextDescriptorPtr, out wrappedKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
                uint ivLength = 12;
                uint tagLength = 16;
                keyLength -= (ivLength + tagLength);
                parent.Log(LogLevel.Noisy, "EXPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        wrappedKeyPtr, keyLength, transferOptions, nextDescriptorPtr);
                byte[] key = new byte[keyLength];
                FetchFromRam(wrappedKeyPtr + ivLength, key, 0, keyLength);
                parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));

                uint outputDataPtr;
                uint outputDataLength;
                DmaTranferOptions outputTransferOptions;
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);

                // Output DMA is expected to contain the plaintext raw key
                if (outputDataLength != keyLength)
                {
                    return ResponseCode.InvalidParameter;
                }

                // Since for now we store the decrypted key "as is" in the ImportKey command, we can simply copy that as "decrypted" key data.
                for(uint i = 0; i < keyLength/4; i++)
                {   
                    uint keyWord = (uint)machine.SystemBus.ReadDoubleWord(wrappedKeyPtr + 12 + i*4);
                    machine.SystemBus.WriteDoubleWord(outputDataPtr + i*4, keyWord);
                }
                
                return ResponseCode.Ok;
            }

            return ResponseCode.InvalidParameter;
        }

        private ResponseCode HandleHashCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "HASH: invalid parameter count");
                return ResponseCode.Abort;
            }

            ShaMode hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];

            parent.Log(LogLevel.Noisy, "HASH: mode={0} dataSize={1}", hashMode, dataSize); 

            DmaTranferOptions transferOptions;
            uint inputPayloadDescriptorPtr;
            uint inputPayloadLength;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputPayloadDescriptorPtr, out inputPayloadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "HASH: INPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputPayloadDescriptorPtr, inputPayloadLength, transferOptions, nextDescriptorPtr);
            byte[] payload = new byte[inputPayloadLength];
            FetchFromRam(inputPayloadDescriptorPtr, payload, 0, inputPayloadLength);
            parent.Log(LogLevel.Noisy, "Payload=[{0}]", BitConverter.ToString(payload));

            uint outputDigestDescriptorPtr;
            uint outputDigestLength;
            UnpackDmaDescriptor(outputDma, out outputDigestDescriptorPtr, out outputDigestLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "HASH: OUTPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDigestDescriptorPtr, outputDigestLength, transferOptions, nextDescriptorPtr);
            byte[] digest = new byte[outputDigestLength];

            IDigest hashEngine = CreateHashEngine(hashMode);

            if(hashEngine == null)
            {
                parent.Log(LogLevel.Error, "HASH: unable to create hashing engine");
                return ResponseCode.Abort;
            }

            if (hashEngine.GetDigestSize() != outputDigestLength)
            {
                parent.Log(LogLevel.Error, "HASH: digest size mismatch");
                return ResponseCode.Abort;
            }

            hashEngine.BlockUpdate(payload, 0, (int)inputPayloadLength);
            hashEngine.DoFinal(digest, 0);

            parent.Log(LogLevel.Noisy, "Digest=[{0}]", BitConverter.ToString(digest));
            WriteToRam(digest, 0, outputDigestDescriptorPtr, outputDigestLength);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashUpdateOrFinishCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool doFinish)
        {
            if (commandParamsCount != 1 && commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: invalid parameter count");
                return ResponseCode.Abort;
            }

            ShaMode hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];
            uint counter = (commandParamsCount == 2) ? commandParams[1] : 0;

            parent.Log(LogLevel.Noisy, "HASH_UPDATE/FINISH: mode={0} dataSize={1} counter={2}", hashMode, dataSize, counter); 

            DmaTranferOptions transferOptions;
            uint inputStateDescriptorPtr;
            uint inputStateLength;
            uint nextDescriptorPtr;
            // TODO: we don't use the input state, we just keep the hashing engine object around 
            // until the HASH_FINISH command is called.
            UnpackDmaDescriptor(inputDma, out inputStateDescriptorPtr, out inputStateLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "HASH_UPDATE/FINISH: INPUT0: statePtr=0x{0:X} inputStateLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputStateDescriptorPtr, inputStateLength, transferOptions, nextDescriptorPtr);

            uint inputPayloadDescriptorPtr;
            uint inputPayloadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputPayloadDescriptorPtr, out inputPayloadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "HASH_UPDATE/FINISH: INPUT1: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputPayloadDescriptorPtr, inputPayloadLength, transferOptions, nextDescriptorPtr);
            byte[] payload = new byte[inputPayloadLength];
            FetchFromRam(inputPayloadDescriptorPtr, payload, 0, inputPayloadLength);
            parent.Log(LogLevel.Noisy, "Payload=[{0}]", BitConverter.ToString(payload));

            uint outputDescriptorPtr;
            uint outputLength;
            UnpackDmaDescriptor(outputDma, out outputDescriptorPtr, out outputLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "HASH_UPDATE/FINISH: OUTPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDescriptorPtr, outputLength, transferOptions, nextDescriptorPtr);
            byte[] output = new byte[outputLength];
            
            if (currentHashEngine == null)
            {
                currentHashEngine = CreateHashEngine(hashMode);
                if(currentHashEngine == null)
                {
                    parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: unable to create hashing engine");
                    return ResponseCode.Abort;
                }
            }
            else if (!CheckHashEngine(currentHashEngine, hashMode))
            {
                // We make sure the current engine is the one we expect. 
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: current hashing engine mismatch");
                return ResponseCode.Abort;
            }
            
            currentHashEngine.BlockUpdate(payload, 0, (int)inputPayloadLength);

            // TODO: we don't use the input state, we just keep the hashing engine object around 
            // until the HASH_FINISH command is called. So we don't write the output state either.

            if (doFinish)
            {
                if (currentHashEngine.GetDigestSize() != outputLength)
                {
                    parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: digest size mismatch");
                    return ResponseCode.Abort;
                }
                
                currentHashEngine.DoFinal(output, 0);
                currentHashEngine = null;
                
                parent.Log(LogLevel.Noisy, "Digest=[{0}]", BitConverter.ToString(output));
                WriteToRam(output, 0, outputDescriptorPtr, outputLength);
            }
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesEncryptOrDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool encrypt)
        {
            if (commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "AES_ENCRYPT/DECRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            CryptoMode cryptoMode = (CryptoMode)((commandOptions & 0xF00) >> 8);
            ContextMode contextMode = (ContextMode)(commandOptions & 0xF);

            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: cryptoMode={0} contextMode={1}", cryptoMode, contextMode);

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint dataSize = commandParams[1];
            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: dataSize={0}", dataSize);

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
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);
            
            // Third input DMA descriptor contains the IV (for whole or start of message) or context (for middle/end)
            // The input IV/Context size is 0 for ECB mode and 16 for all other modes
            ParametersWithIV parametersWithIV = null;
            if (cryptoMode != CryptoMode.Ecb)
            {
                uint inputIvPtr = 0;
                uint inputIvLength = 0;
                UnpackDmaDescriptor(nextDescriptorPtr, out inputIvPtr, out inputIvLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: INPUT2: inputIvPtr=0x{0:X} inputIvLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputIvPtr, inputIvLength, transferOptions, nextDescriptorPtr);
                byte[] inputIv = new byte[inputIvLength];
                FetchFromRam(inputIvPtr, inputIv, 0, inputIvLength);
                parent.Log(LogLevel.Noisy, "InputIv=[{0}]", BitConverter.ToString(inputIv));
                parametersWithIV = new ParametersWithIV(keyParameter, inputIv);
            }

            // Fourth input DMA descriptor(s) contains input data
            uint inputDataPtr;
            uint inputDataLength;
            byte[] inputData = new byte[dataSize];
            uint inputDataOffset = 0;

            while (inputDataOffset < dataSize)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: INPUT3: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
                if (transferOptions != DmaTranferOptions.Discard)
                {
                    FetchFromRam(inputDataPtr, inputData, inputDataOffset, inputDataLength);
                    parent.Log(LogLevel.Noisy, "InputData=[{0}]", BitConverter.ToString(inputData));
                    inputDataOffset += inputDataLength;
                }
            }

            // First output DMA descriptor(s) contains output data
            byte[] outputData = new byte[dataSize];
            uint outputDataOffset = 0;
            nextDescriptorPtr = outputDma;
            
            while (outputDataOffset < dataSize)
            {
                uint tempOutputDataPtr;
                uint tempOutputDataLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tempOutputDataPtr, out tempOutputDataLength, out transferOptions, out nextDescriptorPtr, machine);            
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        tempOutputDataPtr, tempOutputDataLength, transferOptions, nextDescriptorPtr);
                outputDataOffset += tempOutputDataLength;
            }

            // Second output DMA descriptor contains the context of block encryption for passing to next encryption/dceryption call.
            // The output context length is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            uint outputContextPtr = 0;
            uint outputContextLength = 0;
            if (cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.WholeMessage && contextMode != ContextMode.EndOfMessage)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);
            }

            IBlockCipher cipher;
            switch(cryptoMode)
            {
                case CryptoMode.Cbc:
                    cipher = new CbcBlockCipher(new AesEngine());
                    cipher.Init(encrypt, parametersWithIV);
                break;
                case CryptoMode.Ecb: 
                    cipher = new AesEngine();
                    // The input IV length is always 0 when using ECB
                    cipher.Init(encrypt, keyParameter);
                break;
                case CryptoMode.Ctr: // TODOs
                case CryptoMode.Cfb:
                case CryptoMode.Ofb:
                default:
                    parent.Log(LogLevel.Error, "AES_ENCRYPT/DECRYPT: invalid crypto mode");
                    return ResponseCode.Abort;
            }
            
            for(int i = 0; i < dataSize; i += 16)
            {
                cipher.ProcessBlock(inputData, i, outputData, i);
            }

            outputDataOffset = 0;
            nextDescriptorPtr = outputDma;

            while (outputDataOffset < dataSize)
            {
                uint tempOutputDataPtr;
                uint tempOutputDataLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tempOutputDataPtr, out tempOutputDataLength, out transferOptions, out nextDescriptorPtr, machine);            
                if (transferOptions != DmaTranferOptions.Discard)
                {
                    WriteToRam(outputData, outputDataOffset, tempOutputDataPtr, tempOutputDataLength);
                    parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Writing output data: length={0} offset={1} at location {2:X}",
                            tempOutputDataLength, outputDataOffset, tempOutputDataPtr);
                    outputDataOffset += tempOutputDataLength;
                }
            }

            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Output data=[{0}]", BitConverter.ToString(outputData));

            // The output iv_size is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            if (cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.EndOfMessage && contextMode != ContextMode.WholeMessage)
            {
                // TODO: Write the output IV
                // - RENODE-46: For CBC mode, the output IV is the cbcV
                
                // For now we just do a +1 on the input IV
                byte[] iv = parametersWithIV.GetIV();
                for (uint i=0; i<outputContextLength; i++)
                {
                    iv[i] = (byte)(iv[i] + 1);
                }
                WriteToRam(iv, 0, outputContextPtr, outputContextLength);        
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Output context=[{0}]", BitConverter.ToString(iv));
            }
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesCmacCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 2)
            {
               parent.Log(LogLevel.Error, "AES_CMAC: invalid parameter count");
               return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_CMAC: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint inputDataSize = commandParams[1];
            parent.Log(LogLevel.Noisy, "AES_CMAC: dataSize={0}", inputDataSize);

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CMAC: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key. 
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CMAC: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);                     
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CMAC: INPUT2: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            byte[] inputData = new byte[inputDataLength];
            FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
            parent.Log(LogLevel.Noisy, "InputData=[{0}]", BitConverter.ToString(inputData));

            // First output DMA descriptor contains the output MAC
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CMAC: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            byte[] outputData = new byte[outputDataLength];

            CMac cmac = new CMac(new AesEngine());
            cmac.Init(keyParameter);
            cmac.BlockUpdate(inputData, 0, (int)inputDataLength);
            cmac.DoFinal(outputData, 0);
            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Noisy, "AES_CMAC: Output MAC=[{0}]", BitConverter.ToString(outputData));
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesCcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 4)
            {
               parent.Log(LogLevel.Error, "AES_CCM_ENCRYPT: invalid parameter count");
               return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint associatedAuthenticatedDataSize = commandParams[2];
            uint inputDataSize = commandParams[3];
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: Other Command Params: tagSize={0} nonceSize={1} aadSize={2} inputDataSize={3}",
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
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);                     
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);
            
            // Third input DMA descriptor contains the nonce data
            uint noncePtr;
            uint nonceLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out noncePtr, out nonceLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: INPUT2: noncePtr=0x{0:X} nonceLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     noncePtr, nonceLength, transferOptions, nextDescriptorPtr);
            byte[] nonce = new byte[nonceLength];
            FetchFromRam(noncePtr, nonce, 0, nonceLength);
            parent.Log(LogLevel.Noisy, "Nonce=[{0}]", BitConverter.ToString(nonce));

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Noisy, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            byte[] inputData = new byte[inputDataLength];
            FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
            parent.Log(LogLevel.Noisy, "InputData=[{0}]", BitConverter.ToString(inputData));

            // First output DMA descriptor contains the encrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the output tag
            uint outputTagPtr;
            uint outputTagLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputTagPtr, out outputTagLength, out transferOptions, out nextDescriptorPtr, machine); 
            parent.Log(LogLevel.Noisy, "AES_CCM_ENCRYPT: OUTPUT1: outputTagPtr=0x{0:X} outputTagLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputTagPtr, outputTagLength, transferOptions, nextDescriptorPtr);

            CcmBlockCipher cipher = new CcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(keyParameter, (int)outputTagLength*8, nonce, aad);
            cipher.Init(true, parameters);

            byte[] outputDataAndTag = new byte[outputDataLength + outputTagLength];
            cipher.ProcessPacket(inputData, 0, (int)inputDataLength, outputDataAndTag, 0);            
            parent.Log(LogLevel.Noisy, "EncryptedDataAndTag=[{0}]", BitConverter.ToString(outputDataAndTag));

            WriteToRam(outputDataAndTag, 0, outputDataPtr, outputDataLength);
            WriteToRam(outputDataAndTag, outputDataLength, outputTagPtr, outputTagLength);  
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesCcmDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 4)
            {
                parent.Log(LogLevel.Error, "AES_CCM_DECRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint associatedAuthenticatedDataSize = commandParams[2];
            uint inputDataSize = commandParams[3];
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: Other Command Params: tagSize={0} nonceSize={1} aadSize={2} inputDataSize={3}",
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
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);
            
            // Third input DMA descriptor contains the nonce data
            uint noncePtr;
            uint nonceLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out noncePtr, out nonceLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT2: noncePtr=0x{0:X} nonceLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     noncePtr, nonceLength, transferOptions, nextDescriptorPtr);
            byte[] nonce = new byte[nonceLength];
            FetchFromRam(noncePtr, nonce, 0, nonceLength);
            parent.Log(LogLevel.Noisy, "Nonce=[{0}]", BitConverter.ToString(nonce));

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];         
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Noisy, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // Sixth input DMA descriptor contains the tag to be verified
            uint tagPtr;
            uint tagLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out tagPtr, out tagLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: INPUT5: tagPtr=0x{0:X} tagLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     tagPtr, tagLength, transferOptions, nextDescriptorPtr);

            byte[] inputDataAndTag = new byte[inputDataLength + tagLength];
            FetchFromRam(inputDataPtr, inputDataAndTag, 0, inputDataLength);
            FetchFromRam(tagPtr, inputDataAndTag, inputDataLength, tagLength);
            parent.Log(LogLevel.Noisy, "InputDataAndTag=[{0}]", BitConverter.ToString(inputDataAndTag));

            // First output DMA descriptor contains the decrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_CCM_DECRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            byte[] outputData = new byte[outputDataLength];

            CcmBlockCipher cipher = new CcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(keyParameter, (int)tagLength*8, nonce, aad);
            cipher.Init(false, parameters);
            bool tagMatch = true;

            try
            {
                cipher.ProcessPacket(inputDataAndTag, 0, (int)(inputDataLength+tagLength), outputData, 0);
            }
            catch (Exception e)
            {
                if (e is InvalidCipherTextException)
                {
                    tagMatch = false;
                }
                else
                {
                    throw e;
                }
            }

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Noisy, "Decrypted data=[{0}] tagMatch={1}", BitConverter.ToString(outputData), tagMatch);
            
            return tagMatch ? ResponseCode.Ok : ResponseCode.CryptoError;            
        }

        private ResponseCode HandleAesGcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                    keyIndex, keyType, keyMode, keyRestriction);
            uint associatedAuthenticatedDataSize = commandParams[1];
            uint inputDataSize = commandParams[2];

            // Extract Command Options
            uint tagLength = GetTagLength((commandOptions >> 8) & 0xFF);
            ContextMode contextMode = (ContextMode)(commandOptions & 0xFF);

            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: Command Params: aadSize={0} inputDataSize={1} Command Options: contextMode={2} tagLength={3}",
                    associatedAuthenticatedDataSize, inputDataSize, contextMode, tagLength);

            if (contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the IV (for Whole or Start) or context data (for Middle or End)
            uint ivPtr;
            uint ivLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out ivPtr, out ivLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT2: ivPtr=0x{0:X} ivLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    ivPtr, ivLength, transferOptions, nextDescriptorPtr);
            byte[] iv = new byte[ivLength];
            FetchFromRam(ivPtr, iv, 0, ivLength);
            parent.Log(LogLevel.Noisy, "IV/Context=[{0}]", BitConverter.ToString(iv));

            // The iv_size is 12 for Whole and Start of message (= initial IV) and 32 for Middle/End of message (= context input).
            // For now we only support Whole, so iv_size should always be 12.
            if (ivLength != 12)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: invalid IV length");
                return ResponseCode.Abort;
            }

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Noisy, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            byte[] inputData = new byte[inputDataLength];
            FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
            parent.Log(LogLevel.Noisy, "InputData=[{0}]", BitConverter.ToString(inputData));

            // Sixth input DMA descriptor contains the len(A)||len(C) (if applicable)
            uint lenAlenCPtr = 0;
            uint lenAlenCLength = 0;
            if (nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT5: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if (lenAlenCLength > 0)
            {
                FetchFromRam(lenAlenCPtr, lenAlenC, 0, lenAlenCLength);
                parent.Log(LogLevel.Noisy, "LenAlenC=[{0}]", BitConverter.ToString(lenAlenC));
            }

            // First output DMA descriptor contains the encrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the context or tag.
            uint outputContextPtr;
            uint outputContextLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);

            GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(keyParameter, (int)tagLength*8, iv, aad);
            cipher.Init(true, parameters);

            byte[] outputDataAndTag = new byte[outputDataLength + tagLength];
            int len = cipher.ProcessBytes(inputData, 0, (int)inputDataLength, outputDataAndTag, 0);
            len += cipher.DoFinal(outputDataAndTag, len);
            parent.Log(LogLevel.Noisy, "EncryptedDataAndTag=[{0}]", BitConverter.ToString(outputDataAndTag));
            WriteToRam(outputDataAndTag, 0, outputDataPtr, outputDataLength);

            // For End/Whole mode, the output context is the MAC.
            parent.Log(LogLevel.Noisy, "MAC=[{0}]", BitConverter.ToString(cipher.GetMac()));
            WriteToRam(cipher.GetMac(), 0, outputContextPtr, outputContextLength);

            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesGcmDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                    keyIndex, keyType, keyMode, keyRestriction);
            uint associatedAuthenticatedDataSize = commandParams[1];
            uint inputDataSize = commandParams[2];

            // Extract Command Options
            uint tagLength = GetTagLength((commandOptions >> 8) & 0xFF);
            ContextMode contextMode = (ContextMode)(commandOptions & 0xFF);

            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: Command Params: aadSize={0} inputDataSize={1} Command Options: contextMode={2} tagLength={3}",
                    associatedAuthenticatedDataSize, inputDataSize, contextMode, tagLength);

            if (contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = CheckAndRetrieveKey(keyType, keyMode, keyIndex, keyPtr);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));
            KeyParameter keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the IV (for Whole or Start) or context data (for Middle or End)
            uint ivPtr;
            uint ivLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out ivPtr, out ivLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT2: ivPtr=0x{0:X} ivLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    ivPtr, ivLength, transferOptions, nextDescriptorPtr);
            byte[] iv = new byte[ivLength];
            FetchFromRam(ivPtr, iv, 0, ivLength);
            parent.Log(LogLevel.Noisy, "IV/Context=[{0}]", BitConverter.ToString(iv));

            // The iv_size is 12 for Whole and Start of message (= initial IV) and 32 for Middle/End of message (= context input).
            // For now we only support Whole, so iv_size should always be 12.
            if (ivLength != 12)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: invalid IV length");
                return ResponseCode.Abort;
            }

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Noisy, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the ciphertext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // Sixth input DMA descriptor contains the MAC (End/Whole only)
            uint macPtr = 0;
            uint macLength = 0;
            if (nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out macPtr, out macLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT5: macPtr=0x{0:X} macLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        macPtr, macLength, transferOptions, nextDescriptorPtr);
            }

            // Seventh input DMA descriptor contains the len(A)||len(C) (End only)
            uint lenAlenCPtr = 0;
            uint lenAlenCLength = 0;
            if (nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT6: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if (lenAlenCLength > 0)
            {
                FetchFromRam(lenAlenCPtr, lenAlenC, 0, lenAlenCLength);
                parent.Log(LogLevel.Noisy, "LenAlenC=[{0}]", BitConverter.ToString(lenAlenC));
            }

            byte[] inputDataAndTag = new byte[inputDataLength + tagLength];
            FetchFromRam(inputDataPtr, inputDataAndTag, 0, inputDataLength);
            FetchFromRam(macPtr, inputDataAndTag, inputDataLength, (uint)tagLength);
            parent.Log(LogLevel.Noisy, "InputDataAndTag=[{0}]", BitConverter.ToString(inputDataAndTag));

            // First output DMA descriptor contains the decrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            byte[] outputData = new byte[outputDataLength];

            // Second output DMA descriptor contains the context
            uint outputContextPtr = 0;
            uint outputContextLength = 0;
            if (nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);
            }

            GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(keyParameter, (int)tagLength*8, iv, aad);
            cipher.Init(false, parameters);
            
            bool tagMatch = true;
            int len = cipher.ProcessBytes(inputDataAndTag, 0, (int)(inputDataLength+tagLength), outputData, 0);

            try
            {
                len += cipher.DoFinal(outputData, len);
            }
            catch (Exception e)
            {
                if (e is InvalidCipherTextException)
                {
                    tagMatch = false;
                }
                else
                {
                    throw e;
                }
            }

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Noisy, "DecryptedData=[{0}] tagMatch={1}", BitConverter.ToString(outputData), tagMatch);

            // TODO: write the output context here when we implement Start/Middle mode.
            
            return tagMatch ? ResponseCode.Ok : ResponseCode.CryptoError;
        }

        private ResponseCode HandleRandomCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if (commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "RANDOM: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint randomLength = commandParams[0];

            parent.Log(LogLevel.Noisy, "RANDOM: random length = {0}", randomLength);

            // First output DMA descriptor contains the random data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "RANDOM: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            for(uint i = 0; i < randomLength; i++)
            {
                byte b = (byte)random.Next();
                machine.SystemBus.WriteByte(outputDataPtr + i, b);
            }
            return ResponseCode.Ok;                     
        }

        private ResponseCode HandleReadDeviceDataCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount < 1 || commandParamsCount > 2)
            {
                parent.Log(LogLevel.Error, "ReadDeviceData: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint readLocation = commandOptions >> 12;
            ReadDeviceDataLocationType readLocationType = (ReadDeviceDataLocationType)(readLocation >> 4);

            switch(readLocationType)
            {
                case ReadDeviceDataLocationType.CC:
                    //update location offset to CC
                    break;
                case ReadDeviceDataLocationType.DI:
                    //update location offset to DI
                    break;
                case ReadDeviceDataLocationType.WaferProbe:
                    //update location offset to WaferProbe
                    break;
                default:
                    return ResponseCode.InvalidParameter;
            }  

            DeviceDataReadSize readSize = (DeviceDataReadSize)(commandOptions & 0xFF);
            uint output = 1;

            switch(readSize)
            {
                case DeviceDataReadSize.WholeElement:
                    //Get whole element data, size will depend on the readLocationType
                    break;
                case DeviceDataReadSize.ChunkOfElement:
                    if (commandParamsCount != 2)
                    {
                        parent.Log(LogLevel.Error, "ReadDeviceData: invalid param count for ChunkOfElement read size");
                        return ResponseCode.Abort;
                    }
                    //Get chunk of element, need to check if we remain in bounds
                    break;
                case DeviceDataReadSize.OneWord:
                    //Get one word from specified offset
                    break;
                case DeviceDataReadSize.GetSize:
                    //Get size of element
                    break;
                case DeviceDataReadSize.GetValue:
                    if (readLocation != 0)
                    {
                        return ResponseCode.InvalidParameter;
                    }
                    //Get value corresponding to address (only for CC section)
                    break;
                default:
                    return ResponseCode.InvalidParameter;
            }

            // First output DMA descriptor contains the location to which we send the data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "ReadDeviceData: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // For now, return 1 regardless of the location/option selected
            machine.SystemBus.WriteDoubleWord(outputDataPtr, output);
            return ResponseCode.Ok;                     
        }   

        private ResponseCode HandleFlashEraseDataRegionCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashEraseDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // check that flash is properly initialized
            if (series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            // For erase operations, the address may be any within the page to be erased.
            uint startAddress = commandParams[0] & ~(flashPageSize - 1);
            uint sectorsCount = commandParams[1];

            parent.Log(LogLevel.Noisy, "FLASH_ERASE_DATA_REGION: startAddress=0x{0:X} sectorNumber={1}", startAddress, sectorsCount);

            machine.ClockSource.ExecuteInLock(delegate {
                for (uint i = 0; i < sectorsCount; i++)
                {
                    for(uint addr = startAddress + i*flashPageSize; addr < startAddress + (i+1)*flashPageSize; addr += 4)
                    {
                        machine.SystemBus.WriteDoubleWord(addr, 0xFFFFFFFF);
                    }
                }
            });
            return ResponseCode.Ok;
        }

        private ResponseCode HandleFlashWriteDataRegionCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashWriteDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }
            // check that flash is properly initialized
            if (series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            uint startAddress = commandParams[0];
            uint writeLength = commandParams[1];

            parent.Log(LogLevel.Noisy, "FLASH_WRITE_DATA: startAddress=0x{0:X} length={1}", startAddress, writeLength);

            // First input DMA descriptor contains the data to be written
            uint inputDataPtr;
            uint inputDataLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "FLASH_WRITE_DATA: INPUT0: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            
            machine.ClockSource.ExecuteInLock(delegate {
                for(uint i = 0; i<writeLength; i+=4)
                {
                    uint word = machine.SystemBus.ReadDoubleWord(inputDataPtr + i);
                    machine.SystemBus.WriteDoubleWord(startAddress + i, word);
                }
            });
            return ResponseCode.Ok;
        }

        private ResponseCode HandleFlashGetDataRegionLocationCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "FlashGetDataRegionLocation: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // First output DMA descriptor contains the data region address
            uint outputDataRegionAddressPtr;
            uint outputDataRegionAddressLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataRegionAddressPtr, out outputDataRegionAddressLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "FLASH_GET_DATA_REGION_LOC: OUTPUT0: outputDataRegionAddressPtr=0x{0:X} outputDataRegionAddressLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataRegionAddressPtr, outputDataRegionAddressLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the data region length
            uint outputDataRegionLengthPtr;
            uint outputDataRegionLengthLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataRegionLengthPtr, out outputDataRegionLengthLength, out transferOptions, out nextDescriptorPtr, machine); 
            parent.Log(LogLevel.Noisy, "FLASH_GET_DATA_REGION_LOC: OUTPUT1: outputDataRegionLengthPtr=0x{0:X} outputDataRegionLengthLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataRegionLengthPtr, outputDataRegionLengthLength, transferOptions, nextDescriptorPtr);
                        
                        
            // check that flash is properly initialized
            if (series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }
            uint flashDataSpaceLength = GetInitialDataSpaceLength;
            machine.SystemBus.WriteDoubleWord(outputDataRegionAddressPtr, QspiFlashHostBase + (flashSize - flashDataSpaceLength));
            machine.SystemBus.WriteDoubleWord(outputDataRegionLengthPtr, flashDataSpaceLength);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleFlashGetCodeRegionConfigCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if (commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "HandleFlashGetCodeRegionConfigCommand: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // Output DMA descriptor contains the region config array
            uint outputCodeRegionConfigAddressPtr;
            uint outputCodeRegionConfigAddressLength;
            DmaTranferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputCodeRegionConfigAddressPtr, out outputCodeRegionConfigAddressLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "FLASH_GET_CODE_REGION_CONFIG: OUTPUT0: outputCodeRegionConfigAddressPtr=0x{0:X} outputCodeRegionConfigAddressLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputCodeRegionConfigAddressPtr, outputCodeRegionConfigAddressLength, transferOptions, nextDescriptorPtr);
                        
            // check that flash is properly initialized
            if (series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            uint regionSize = (outputCodeRegionConfigAddressPtr & 0xFFF) * 32 * 1024; // Bits 0:11
            uint protectionMode = (outputCodeRegionConfigAddressPtr >> 12) & 0x3; // Bits 12:13
            bool bankSwappingEnabled = ((outputCodeRegionConfigAddressPtr >> 14) & 0x1) == 1; // Bit 14
            bool regionClosed = ((outputCodeRegionConfigAddressPtr >> 15) & 0x1) == 1; // Bit 15

            uint data = (regionSize / (32 * 1024)) & 0xFFF; // Bits 0:11
            data |= (protectionMode & 0x3) << 12; // Bits 12:13
            data |= (bankSwappingEnabled ? 1u : 0u) << 14; // Bit 14
            data |= (regionClosed ? 1u : 0u) << 15; // Bit 15

            machine.SystemBus.WriteDoubleWord(outputCodeRegionConfigAddressPtr, data);

            return ResponseCode.Ok;
        }  

        private ResponseCode HandleConfigureQspiRefClockCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            switch (commandOptions)
            {
                case 0x01:
                case 0x0100:
                    // Set FSRCO as the QSPI controller clock source
                    if (commandParamsCount != 0)
                    {
                        parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x01", commandParamsCount);
                        return ResponseCode.InvalidParameter;
                    }
                    parent.Log(LogLevel.Noisy, "QSPI Clock Source set to FSRCO");
                    break;

                case 0x02:
                case 0x0200:
                    // Set FLPLL as the QSPI controller clock source
                    if (commandParamsCount != 4)
                    {
                        parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x02", commandParamsCount);
                        return ResponseCode.InvalidParameter;
                    }

                    uint flpllRefClock = commandParams[0];
                    uint intDiv = (commandParams[1] >> 16) & 0xFFFF;
                    uint fracDiv = commandParams[1] & 0xFFFF;
                    uint flpllFreqRange = commandParams[2] & 0xF;
                    uint qspiClockPrescaler = (commandParams[2] >> 4) & 0xF;

                    if (flpllRefClock != 0x0 && flpllRefClock != 0x2)
                    {
                        parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid FLPLL reference clock {0}", flpllRefClock);
                        return ResponseCode.InvalidParameter;
                    }
                    if (fracDiv >= 2048 || intDiv <= 5 || intDiv >= 12)
                    {
                        parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid INT_DIV {0} or FRAC_DIV {1}", intDiv, fracDiv);
                        return ResponseCode.InvalidParameter;
                    }

                    parent.Log(LogLevel.Noisy, "QSPI Clock Source set to FLPLL");
                    parent.Log(LogLevel.Noisy, "FLPLL Ref Clock: {0}, INT_DIV: {1}, FRAC_DIV: {2}, Freq Range: {3}, Prescaler: {4}",
                        flpllRefClock, intDiv, fracDiv, flpllFreqRange, qspiClockPrescaler);
                    break;

                default:
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid command option {0}", commandOptions);
                    return ResponseCode.InvalidParameter;
            }

            return ResponseCode.Ok;
        }
#endregion

#region command utility methods
        private void FetchFromRam(uint sourcePointer, byte[] destination, uint destinationOffset, uint length)
        {
            for (uint i=0; i<length; i++)
            {
                destination[destinationOffset + i] = (byte)machine.SystemBus.ReadByte(sourcePointer + i);
            }
        }

        private void WriteToRam(byte[] source, uint sourceOffset, uint destinationPointer, uint length)
        {
            for (uint i=0; i<length; i++)
            {
                machine.SystemBus.WriteByte(destinationPointer + i, source[sourceOffset + i]);
            }
        }

        private void UnpackDmaDescriptor(uint dmaPointer, out uint dataPointer, out uint dataSize, out DmaTranferOptions options, out uint nextDescriptorPtr, Machine machine)
        {
            dataPointer = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer);
            nextDescriptorPtr = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 4);
            var word2 = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 8);
            dataSize = word2 & 0xFFFFFFF;
            options = (DmaTranferOptions)(word2 >> 28);
            
            parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): dataPointer=0x{0:X} dataSize={1} options={2} nextDescriptorPtr=0x{3:X}",
                     dataPointer, dataSize, options, nextDescriptorPtr);
            for(uint i = 0; i < dataSize; i+=4)
            {
                parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): DATA word{0}=0x{1:X}", i/4, machine.SystemBus.ReadDoubleWord(dataPointer+i));
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

            parent.Log(LogLevel.Noisy, "UnpackKeyMetadata(): keyMetadata=0x{0:X} index={1} type={2} mode={3} restriction={4}",
                     keyMetadata, index, type, mode, restriction);
        }

        IDigest CreateHashEngine(ShaMode hashMode)
        {
            IDigest ret = null;

            switch(hashMode)
            {
                case ShaMode.Sha1:
                    ret = new Sha1Digest();
                    break;
                case ShaMode.Sha224:
                    ret = new Sha224Digest();
                    break;
                case ShaMode.Sha256:
                    ret = new Sha256Digest();
                    break;
                case ShaMode.Sha384:
                    ret = new Sha384Digest();
                    break;
                case ShaMode.Sha512:
                    ret = new Sha512Digest();
                    break;
                default:
                    parent.Log(LogLevel.Error, "CreateHashEngine(): invalid hash mode");
                    break;
            }
            return ret;
        }

        bool CheckHashEngine(IDigest hashEngine, ShaMode hashMode)
        {
            bool ret = false;

            switch(hashMode)
            {
                case ShaMode.Sha1:
                    if (hashEngine is Sha1Digest)
                    {
                        ret = true;
                    }
                    break;
                case ShaMode.Sha224:
                    if (hashEngine is Sha224Digest)
                    {
                        ret = true;
                    }
                    break;
                case ShaMode.Sha256:
                    if (hashEngine is Sha256Digest)
                    {
                        ret = true;
                    }
                    break;
                case ShaMode.Sha384:
                    if (hashEngine is Sha384Digest)
                    {
                        ret = true;
                    }
                    break;
                case ShaMode.Sha512:
                    if (hashEngine is Sha512Digest)
                    {
                        ret = true;
                    }                    
                    break;
                default:
                    parent.Log(LogLevel.Error, "CheckHashEngine(): invalid hash mode");
                    break;
            }
            return ret;
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
                    lengthOk = (dataSize >= 16);
                    break;
                case CryptoMode.Ctr:
                    // Must be > 0
                    lengthOk = (dataSize > 0);
                    break;                
            }
            return lengthOk;
        }

        uint GetTagLength(uint tagLengthOption)
        {
            uint tagLength;
            switch (tagLengthOption)
            {
                case 0x4:
                    tagLength = 4;
                    break;
                case 0x8:
                    tagLength = 8;
                    break;
                case 0xC:
                    tagLength = 12;
                    break;
                case 0xD:
                    tagLength = 13;
                    break;
                case 0xE:
                    tagLength = 14;
                    break;
                case 0xF:
                    tagLength = 15;
                    break;
                default:
                    tagLength = 16;
                    break;
            }
            return tagLength;
        }

        private byte[] CheckAndRetrieveKey(KeyType keyType, KeyMode keyMode, uint keyIndex, uint keyPointer)
        {
            byte[] key = new byte[16];

            // TODO: for now we only support "raw" key type.
            if (keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "Key TYPE not supported");
                return key;
            }
            
            // TODO: for now we don't support "WrappedAntiReplay" key mode.
            if (keyMode == KeyMode.WrappedAntiReplay)
            {
                parent.Log(LogLevel.Error, "Key MODE WrappedAntiReplay not supported");
                return key;
            }

            if (keyMode == KeyMode.Unprotected || keyMode == KeyMode.Wrapped)
            {
                // IV - 12 bytes
                // Actual key
                // AESGCM tag - 16 bytes
                uint keyOffset = 12;                
                FetchFromRam(keyPointer + keyOffset, key, 0, 16);
            }
            else if (keyMode == KeyMode.Volatile)
            {
                if (!volatileKeys.ContainsKey(keyIndex))
                {
                    parent.Log(LogLevel.Error, "Volatile key not found");
                    return key;
                }

                key = volatileKeys[keyIndex];
                parent.Log(LogLevel.Noisy, "CheckAndRetrieveKey (volatile): keyIndex={0} key=[{1}]", keyIndex, BitConverter.ToString(key));
            }

            return key;
        }

        private uint GetInitialDataSpaceLength
        {
            get
            {
                return flashSize - flashDataRegionStart;
            }
        }
#endregion

#region enums
        private enum CommandId
        {
            ImportKey                       = 0x0100,
            ExportKey                       = 0x0102,
            DeleteKey                       = 0x0105,
            TransferKey                     = 0x0106,
            InstallKey                      = 0x0107,
            CreateKey                       = 0x0200,
            ReadPublicKey                   = 0x0201,
            DeriveKey                       = 0x0202,
            Hash                            = 0x0300,
            HashUpdate                      = 0x0301,
            HashHmac                        = 0x0302,
            HashFinish                      = 0x0303,
            AesEncrypt                      = 0x0400,
            AesDecrypt                      = 0x0401,
            AesGcmEncrypt                   = 0x0402,
            AesGcmDecrypt                   = 0x0403,
            AesCmac                         = 0x0404,
            AesCcmEncrypt                   = 0x0405,
            AesCcmDecrypt                   = 0x0406,
            ReadDeviceData                  = 0x4330,
            Random                          = 0x0700,
            ConfigureQspiRefClock           = 0xFF15,
            FlashGetCodeRegionConfig        = 0xFF53,
            FlashEraseDataRegion            = 0xFF62,
            FlashWriteDataRegion            = 0xFF63,
            FlashGetDataRegionLocation      = 0xFF64,
        }

        private enum ShaMode
        {
            Sha1    = 0x2,
            Sha224  = 0x3,
            Sha256  = 0x4,
            Sha384  = 0x5,
            Sha512  = 0x6,
        }

        private enum CryptoMode
        {
            Ecb = 0x1,
            Cbc = 0x2,
            Ctr = 0x3,
            Cfb = 0x4,
            Ofb = 0x5,
        }

        private enum ContextMode
        {
            WholeMessage    = 0x0,
            StartOfMessage  = 0x1,
            EndOfMessage    = 0x2,
            MiddleOfMessage = 0x3,
        }     

        private enum DmaTranferOptions
        {
            Register       = 0x1,
            MemoryRealign  = 0x2,
            Discard        = 0x4,
        }   

        private enum KeyRestriction
        {
            Unlocked   = 0x0,
            Locked     = 0x1,
            Internal   = 0x2,
            Restricted = 0x3,
        }

        private enum KeyMode
        {
            Unprotected        = 0x0,
            Volatile           = 0x1,
            Wrapped            = 0x2,
            WrappedAntiReplay  = 0x3,
        }

        private enum KeyType
        {
            Raw                         = 0x0,
            EccWeirstrabPrimeFieldCurve = 0x8,
            EccMontgomeryCurve          = 0xB,
            Ed25519                     = 0xC,
        }

        public enum ResponseCode
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

        private enum ReadDeviceDataLocationType
        {
            CC         = 0x0,
            DI         = 0x1,
            WaferProbe = 0x2, 
        }

        private enum DeviceDataReadSize
        {
            WholeElement   = 0x00,
            ChunkOfElement = 0x01,
            OneWord        = 0x02,
            GetSize        = 0x03,
            GetValue       = 0x04,
        }
#endregion
    }
}