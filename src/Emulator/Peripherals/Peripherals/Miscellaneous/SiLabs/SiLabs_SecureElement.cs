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

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_SecureElement
    {
        public SiLabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3)
            : this(machine, parent, txFifo, rxFifo, series3, 0, 0, 0, 0, 0, 0, null)
        {
        }

        public SiLabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3,
                                    uint flashSize, uint flashPageSize, uint flashRegionSize, uint flashCodeRegionStart, uint flashCodeRegionEnd = 0, uint flashDataRegionStart = 0, SiLabs_IKeyStorage ksuStorage = null)
        {
            this.machine = machine;
            this.parent = parent;
            this.ksuStorage = ksuStorage;
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

        public bool TxFifoEnqueueCallback(uint word)
        {
            if(wordsLeftToBeReceived == 0)
            {
                parent.Log(LogLevel.Error, "TxFifoEnqueueCallback: 0 words left");
                return false;
            }

            wordsLeftToBeReceived--;

            if(wordsLeftToBeReceived == 0)
            {
                ProcessCommand();
                return true;
            }

            return false;
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

        public uint GetDefaultErrorStatus()
        {
            return (uint)ResponseCode.InternalError;
        }

        public void Reset()
        {
            wordsLeftToBeReceived = 0;
            currentHashEngine = null;

            // Reset and re-initialize volatile keys with the NVM3 encryption key
            volatileKeys.Clear();
            byte[] key = {0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF};
            volatileKeys[246] = key;
        }

        private void ProcessCommand()
        {
            uint commandHandle = 0;

            if(txFifo.Count == 0)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Queue is EMPTY!");
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }

            uint header = txFifo.Dequeue();
            // First 2 bytes of the header is the number of bytes in the message (header included)
            uint wordsCount = (header & 0xFFFF)/4;

            if(txFifo.Count < wordsCount - 1)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Not enough words FifoSize={0}, expectedWords={1}", txFifo.Count, wordsCount - 1);
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }

            if(series3)
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
            for(var i = 0; i < commandParamsCount; i++)
            {
                commandParams[i] = txFifo.Dequeue();
            }

            parent.Log(LogLevel.Info, "ProcessCommand(): command ID={0} command Options=0x{1:X} command params count={2}", commandId, commandOptions, commandParamsCount);

            ResponseCode responseCode;

            switch(commandId)
            {
            case CommandId.CreateKey:
                responseCode = HandleCreateKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ReadPublicKey:
                responseCode = HandleReadPublicKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ImportKey:
                responseCode = HandleImportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ExportKey:
                responseCode = HandleExportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, series3);
                break;
            case CommandId.TransferKey:
                responseCode = HandleTransferKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
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
            case CommandId.DiffieHellman:
                responseCode = HandleDiffieHellmanCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.JPakeRound1:
                responseCode = HandleJPakeRound1Command(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.JPakeRound2:
                responseCode = HandleJPakeRound2Command(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.JPakeGenerateSessionKey:
                responseCode = HandleJPakeGenerateSessionKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
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

            if(responseCode != ResponseCode.Ok)
            {
                parent.Log(LogLevel.Info, "ProcessCommand(): Response code {0}", responseCode);
            }

            WriteResponse(responseCode, commandHandle);
        }

        private void WriteResponse(ResponseCode code, uint commandHandle)
        {
            rxFifo.Enqueue((uint)code);
            if(series3)
            {
                rxFifo.Enqueue(commandHandle);
            }
        }

        private ResponseCode HandleAesEncryptOrDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool encrypt)
        {
            if(commandParamsCount != 2)
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
            if(!IsDataLengthValid(dataSize, cryptoMode))
            {
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
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
            if(cryptoMode != CryptoMode.Ecb)
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

            while(inputDataOffset < dataSize)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: INPUT3: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
                if(transferOptions != DmaTransferOptions.Discard)
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

            while(outputDataOffset < dataSize)
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
            if(cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.WholeMessage && contextMode != ContextMode.EndOfMessage)
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

            while(outputDataOffset < dataSize)
            {
                uint tempOutputDataPtr;
                uint tempOutputDataLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tempOutputDataPtr, out tempOutputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                if(transferOptions != DmaTransferOptions.Discard)
                {
                    WriteToRam(outputData, outputDataOffset, tempOutputDataPtr, tempOutputDataLength);
                    parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Writing output data: length={0} offset={1} at location {2:X}",
                            tempOutputDataLength, outputDataOffset, tempOutputDataPtr);
                    outputDataOffset += tempOutputDataLength;
                }
            }

            parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Output data=[{0}]", BitConverter.ToString(outputData));

            // The output iv_size is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            if(cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.EndOfMessage && contextMode != ContextMode.WholeMessage)
            {
                // TODO: Write the output IV
                // - RENODE-46: For CBC mode, the output IV is the cbcV

                // For now we just do a +1 on the input IV
                byte[] iv = parametersWithIV.GetIV();
                for(uint i = 0; i < outputContextLength; i++)
                {
                    iv[i] = (byte)(iv[i] + 1);
                }
                WriteToRam(iv, 0, outputContextPtr, outputContextLength);
                parent.Log(LogLevel.Noisy, "AES_ENCRYPT/DECRYPT: Output context=[{0}]", BitConverter.ToString(iv));
            }
            return ResponseCode.Ok;
        }

        private ResponseCode HandleTransferKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
            {
                throw new NotSupportedException("TRANSFER_KEY: invalid parameter count");
            }

            KeyMode srcKeyMode;
            KeyType srcKeyType;
            KeyRestriction srcKeyRestriction;
            uint srcKeyIndex;

            UnpackKeyMetadata(commandParams[0], out srcKeyIndex, out srcKeyType, out srcKeyMode, out srcKeyRestriction);

            // Extract the update mode and index from keyUpdateMetadata
            uint keyUpdateMetadata = commandParams[1];
            KeyMode dstKeyMode = (KeyMode)((keyUpdateMetadata >> 8) & 0x3);
            uint dstKeyIndex = keyUpdateMetadata & 0xFF;

            parent.Log(LogLevel.Noisy, "TRANSFER_KEY: SRC Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                       srcKeyIndex, srcKeyType, srcKeyMode, srcKeyRestriction);
            parent.Log(LogLevel.Noisy, "TRANSFER_KEY: DST Key Metadata: keyIndex={0} keyMode={1}",
                       dstKeyIndex, dstKeyMode);

            // First input DMA descriptor contains the input key data
            uint inputKeyDataPtr;
            uint inputKeyDataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputKeyDataPtr, out inputKeyDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "TRANSFER_KEY: INPUT0: inputKeyDataPtr=0x{0:X} inputKeyDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputKeyDataPtr, inputKeyDataLength, transferOptions, nextDescriptorPtr);

            // Fetch the input key data
            byte[] inputKeyData = new byte[inputKeyDataLength];
            FetchFromRam(inputKeyDataPtr, inputKeyData, 0, inputKeyDataLength);
            parent.Log(LogLevel.Noisy, "InputKeyData=[{0}]", BitConverter.ToString(inputKeyData));

            // Second input DMA descriptor contains the new auth data
            uint newAuthDataPtr;
            uint newAuthDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out newAuthDataPtr, out newAuthDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "TRANSFER_KEY: INPUT1: newAuthDataPtr=0x{0:X} newAuthDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     newAuthDataPtr, newAuthDataLength, transferOptions, nextDescriptorPtr);

            // Fetch the new auth data
            byte[] newAuthData = new byte[newAuthDataLength];
            FetchFromRam(newAuthDataPtr, newAuthData, 0, newAuthDataLength);
            parent.Log(LogLevel.Noisy, "NewAuthData=[{0}]", BitConverter.ToString(newAuthData));

            // Retrieve the source key
            byte[] sourceKey = null;
            switch(srcKeyMode)
            {
            case KeyMode.Unprotected:
            {
                sourceKey = inputKeyData;
                break;
            }
            case KeyMode.Wrapped:
            {
                ResponseCode result = ReadWrappedKeyFromDescriptor(inputKeyDataPtr, inputKeyDataLength, out sourceKey);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                if(!volatileKeys.ContainsKey(srcKeyIndex))
                {
                    parent.Log(LogLevel.Error, "TRANSFER_KEY: Source Volatile key not found, slot={0}", srcKeyIndex);
                    return ResponseCode.InvalidParameter;
                }
                sourceKey = volatileKeys[srcKeyIndex];
                break;
            }
            case KeyMode.Ksu:
            {
                if(!ksuStorage.ContainsKey(srcKeyIndex))
                {
                    sourceKey = ksuStorage.GetKey(srcKeyIndex);
                }
                break;
            }
            default:
                parent.Log(LogLevel.Error, "TRANSFER_KEY: Invalid source key mode={0}", srcKeyMode);
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Noisy, "TRANSFER_KEY: SourceKey=[{0}]", BitConverter.ToString(sourceKey));

            // Write the key to destination and possibly output it to the output descriptor (only for unprotected/wrapped destination)
            byte[] outputKey = null;
            switch(dstKeyMode)
            {
            case KeyMode.Unprotected:
            {
                outputKey = sourceKey;
                break;
            }
            case KeyMode.Wrapped:
            {
                uint offset = 0;
                outputKey = new byte[sourceKey.Length + 12 + 16];

                // - 12 bytes: random IV assigned at time of wrapping
                // - var bytes: encrypted key data
                // - 16 bytes: AESGCM authentication tag
                // As of now, we leave the key stored as plain text, so we just grab that.
                FillArray(outputKey, 0, 12, WrappedKeyRandomIVMarker);
                offset += 12;
                Array.Copy(sourceKey, 0, outputKey, (int)offset, sourceKey.Length);
                offset += (uint)sourceKey.Length;
                FillArray(outputKey, offset, 16, WrappedKeyAuthenticationTagMarker);
                break;
            }
            case KeyMode.Volatile:
            {
                volatileKeys[dstKeyIndex] = sourceKey;
                break;
            }
            case KeyMode.Ksu:
            {
                ksuStorage.AddKey(dstKeyIndex, sourceKey);
                break;
            }
            default:
                parent.Log(LogLevel.Error, "TRANSFER_KEY: Invalid destination key mode={0}", dstKeyMode);
                return ResponseCode.InvalidParameter;
            }

            if(outputKey != null)
            {
                // Output DMA descriptor contains the new key data
                uint outputKeyDataPtr;
                uint outputKeyDataLength;
                UnpackDmaDescriptor(outputDma, out outputKeyDataPtr, out outputKeyDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "TRANSFER_KEY: OUTPUT0: outputKeyDataPtr=0x{0:X} outputKeyDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                           outputKeyDataPtr, outputKeyDataLength, transferOptions, nextDescriptorPtr);

                // Write the new key data to the output DMA
                WriteToRam(outputKey, 0, outputKeyDataPtr, outputKeyDataLength);
                parent.Log(LogLevel.Noisy, "TRANSFER_KEY: OutputDescLength={0} outputKeyLength={1} OutputKeyData=[{2}]", outputKeyDataLength, outputKey.Length, BitConverter.ToString(outputKey));
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "HASH: invalid parameter count");
                return ResponseCode.Abort;
            }

            ShaMode hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];

            parent.Log(LogLevel.Noisy, "HASH: mode={0} dataSize={1}", hashMode, dataSize);

            DmaTransferOptions transferOptions;
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

            if(hashEngine.GetDigestSize() != outputDigestLength)
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
            if(commandParamsCount != 1 && commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: invalid parameter count");
                return ResponseCode.Abort;
            }

            ShaMode hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];
            uint counter = (commandParamsCount == 2) ? commandParams[1] : 0;

            parent.Log(LogLevel.Noisy, "HASH_UPDATE/FINISH: mode={0} dataSize={1} counter={2}", hashMode, dataSize, counter);

            DmaTransferOptions transferOptions;
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

            if(currentHashEngine == null)
            {
                currentHashEngine = CreateHashEngine(hashMode);
                if(currentHashEngine == null)
                {
                    parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: unable to create hashing engine");
                    return ResponseCode.Abort;
                }
            }
            else if(!CheckHashEngine(currentHashEngine, hashMode))
            {
                // We make sure the current engine is the one we expect. 
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: current hashing engine mismatch");
                return ResponseCode.Abort;
            }

            currentHashEngine.BlockUpdate(payload, 0, (int)inputPayloadLength);

            // TODO: we don't use the input state, we just keep the hashing engine object around 
            // until the HASH_FINISH command is called. So we don't write the output state either.

            if(doFinish)
            {
                if(currentHashEngine.GetDigestSize() != outputLength)
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

        private ResponseCode HandleAesCmacCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
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
            DmaTransferOptions transferOptions;
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

        private ResponseCode HandleExportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, bool series3)
        {
            if(commandParamsCount != 1)
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

            if(keyMode == KeyMode.Unprotected)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: invalid key mode");
                return ResponseCode.Abort;
            }

            if(keyMode == KeyMode.Volatile)
            {
                // key must be present in the dictionary to be exported.
                if(!volatileKeys.ContainsKey(keyIndex))
                {
                    return ResponseCode.InvalidParameter;
                }

                byte[] key = volatileKeys[keyIndex];
                uint outputDataPtr;
                uint outputDataLength;
                DmaTransferOptions transferOptions;
                uint nextDescriptorPtr;
                // No data input is required when exporting a VOLATILE key, only output DMA is used.
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "EXPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, key.Length, transferOptions, nextDescriptorPtr);

                if(outputDataLength != key.Length)
                {
                    return ResponseCode.InvalidParameter;
                }

                WriteToRam(key, 0, outputDataPtr, (uint)key.Length);
                return ResponseCode.Ok;
            }
            else if(keyMode == KeyMode.Wrapped)
            {
                // Only "unlocked" keys can be exported.
                if(keyRestriction != KeyRestriction.Unlocked)
                {
                    return ResponseCode.AuthorizationError;
                }

                uint outputDataPtr;
                uint outputDataLength;
                DmaTransferOptions outputTransferOptions;
                uint nextDescriptorPtr;
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);

                uint wrappedKeyPtr;
                uint keyLength;
                uint authDataPtr;
                uint authDataLength;
                DmaTransferOptions transferOptions;
                UnpackDmaDescriptor(inputDma, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
                UnpackDmaDescriptor(nextDescriptorPtr, out wrappedKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "EXPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                           wrappedKeyPtr, keyLength, transferOptions, nextDescriptorPtr);

                byte[] key = null;
                ResponseCode result = ReadWrappedKeyFromDescriptor(wrappedKeyPtr, keyLength, out key);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));

                // Since for now we store the decrypted key "as is" in the ImportKey command, we can simply copy that as "decrypted" key data.
                WriteToRam(key, 0, outputDataPtr, outputDataLength);
                return ResponseCode.Ok;
            }

            return ResponseCode.InvalidParameter;
        }

        private ResponseCode HandleAesCcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 4)
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

            if(!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
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

        private ResponseCode HandleAesGcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 3)
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

            if(contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
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
            if(ivLength != 12)
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
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_ENCRYPT: INPUT5: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if(lenAlenCLength > 0)
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
            if(commandParamsCount != 3)
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

            if(contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
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
            if(ivLength != 12)
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
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out macPtr, out macLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT5: macPtr=0x{0:X} macLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        macPtr, macLength, transferOptions, nextDescriptorPtr);
            }

            // Seventh input DMA descriptor contains the len(A)||len(C) (End only)
            uint lenAlenCPtr = 0;
            uint lenAlenCLength = 0;
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Noisy, "AES_GCM_DECRYPT: INPUT6: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if(lenAlenCLength > 0)
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
            if(nextDescriptorPtr != NullDescriptor)
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
            catch(Exception e)
            {
                if(e is InvalidCipherTextException)
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
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "RANDOM: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint randomLength = commandParams[0];

            parent.Log(LogLevel.Noisy, "RANDOM: random length = {0}", randomLength);

            // First output DMA descriptor contains the random data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions transferOptions;
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
            if(commandParamsCount < 1 || commandParamsCount > 2)
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
                if(commandParamsCount != 2)
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
                if(readLocation != 0)
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
            DmaTransferOptions transferOptions;
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
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashEraseDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            // For erase operations, the address may be any within the page to be erased.
            uint startAddress = commandParams[0] & ~(flashPageSize - 1);
            uint sectorsCount = commandParams[1];

            parent.Log(LogLevel.Noisy, "FLASH_ERASE_DATA_REGION: startAddress=0x{0:X} sectorNumber={1}", startAddress, sectorsCount);

            machine.ClockSource.ExecuteInLock(delegate
            {
                for(uint i = 0; i < sectorsCount; i++)
                {
                    for(uint addr = startAddress + i * flashPageSize; addr < startAddress + (i + 1) * flashPageSize; addr += 4)
                    {
                        machine.SystemBus.WriteDoubleWord(addr, 0xFFFFFFFF);
                    }
                }
            });
            return ResponseCode.Ok;
        }

        private ResponseCode HandleFlashWriteDataRegionCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashWriteDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }
            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
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
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "FLASH_WRITE_DATA: INPUT0: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            machine.ClockSource.ExecuteInLock(delegate
            {
                for(uint i = 0; i < writeLength; i += 4)
                {
                    uint word = machine.SystemBus.ReadDoubleWord(inputDataPtr + i);
                    machine.SystemBus.WriteDoubleWord(startAddress + i, word);
                }
            });
            return ResponseCode.Ok;
        }

        private ResponseCode HandleFlashGetDataRegionLocationCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "FlashGetDataRegionLocation: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // First output DMA descriptor contains the data region address
            uint outputDataRegionAddressPtr;
            uint outputDataRegionAddressLength;
            DmaTransferOptions transferOptions;
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
            if(series3 && (flashSize == 0 || flashPageSize == 0))
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
            if(commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "HandleFlashGetCodeRegionConfigCommand: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // Output DMA descriptor contains the region config array
            uint outputCodeRegionConfigAddressPtr;
            uint outputCodeRegionConfigAddressLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputCodeRegionConfigAddressPtr, out outputCodeRegionConfigAddressLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "FLASH_GET_CODE_REGION_CONFIG: OUTPUT0: outputCodeRegionConfigAddressPtr=0x{0:X} outputCodeRegionConfigAddressLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputCodeRegionConfigAddressPtr, outputCodeRegionConfigAddressLength, transferOptions, nextDescriptorPtr);

            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
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
            switch(commandOptions)
            {
            case 0x01:
            case 0x0100:
                // Set FSRCO as the QSPI controller clock source
                if(commandParamsCount != 0)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x01", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }
                parent.Log(LogLevel.Noisy, "QSPI Clock Source set to FSRCO");
                break;

            case 0x02:
            case 0x0200:
                // Set FLPLL as the QSPI controller clock source
                if(commandParamsCount != 4)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x02", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }

                uint flpllRefClock = commandParams[0];
                uint intDiv = (commandParams[1] >> 16) & 0xFFFF;
                uint fracDiv = commandParams[1] & 0xFFFF;
                uint flpllFreqRange = commandParams[2] & 0xF;
                uint qspiClockPrescaler = (commandParams[2] >> 4) & 0xF;

                if(flpllRefClock != 0x0 && flpllRefClock != 0x2)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid FLPLL reference clock {0}", flpllRefClock);
                    return ResponseCode.InvalidParameter;
                }
                if(fracDiv >= 2048 || intDiv <= 5 || intDiv >= 12)
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

        private ResponseCode HandleAesCcmDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 4)
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

            if(!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
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
                cipher.ProcessPacket(inputDataAndTag, 0, (int)(inputDataLength + tagLength), outputData, 0);
            }
            catch(Exception e)
            {
                if(e is InvalidCipherTextException)
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

        private ResponseCode HandleImportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
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

            if(keyMode == KeyMode.Unprotected)
            {
                parent.Log(LogLevel.Error, "IMPORT_KEY: invalid key mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor contains the plaintext key
            uint plaintextKeyPtr;
            uint keyLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out plaintextKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "IMPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     plaintextKeyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = new byte[keyLength];
            FetchFromRam(plaintextKeyPtr, key, 0, keyLength);
            parent.Log(LogLevel.Noisy, "Key=[{0}]", BitConverter.ToString(key));

            // Input DMA has actually a second descriptor containing 8 bytes all set to zeros. I assume that is the auth_data[8] field,
            // TODO: For now we don't use the auth_data to do anything so we just ignore it.
            if(keyMode == KeyMode.Volatile)
            {
                // if keyMode == KeyMode.Volatile, we store the key in a dictionary, 
                // since no data will be returned and the key will be transferred into an internal slot or KSU.
                // in our case, dictionary within SE model.
                // TODO: Double check if when doing FetchFromRam we need to account for offset.
                parent.Log(LogLevel.Noisy, "IMPORT_KEY: Assigning VOLATILE key to index {0}", keyIndex);
                volatileKeys[keyIndex] = key;

                return ResponseCode.Ok;
            }
            else if(keyMode == KeyMode.Wrapped)
            {
                uint outputDataPtr;
                uint outputDataLength;
                DmaTransferOptions outputTransferOptions;
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);

                ResponseCode result = WriteWrappedKeyToDescriptor(outputDataPtr, outputDataLength, key);
                return result;
            }
            else if(keyMode == KeyMode.Ksu)
            {
                parent.Log(LogLevel.Noisy, "IMPORT_KEY: Storing key in KSU at index {0}", keyIndex);
                ksuStorage.AddKey(keyIndex, key);

                return ResponseCode.Ok;
            }

            parent.Log(LogLevel.Error, "IMPORT_KEY: invalid parameter");
            return ResponseCode.InvalidParameter;
        }

        private ResponseCode HandleCreateKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "CREATE_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Noisy, "CREATE_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;
            EccWeirstrassKeyMetadata wprKeyMetadata = null;
            if(keyType == KeyType.EccWeirstrass)
            {
                wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
                if(wprKeyMetadata.HasDomainParameters)
                {
                    parent.Log(LogLevel.Error, "CREATE_KEY: Custom Curve not supported");
                    return ResponseCode.Abort;
                }
            }

            // Authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "CREATE_KEY: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            uint keyLength = 0;
            byte[] key = null;

            // TODO: for now we only support Raw and EccWeirstrass key types
            switch(keyType)
            {
            case KeyType.Raw:
            {
                UnpackRawKeyMetadata(keyMetadata, out keyLength);
                key = new byte[keyLength];

                for(uint i = 0; i < keyLength; i++)
                {
                    key[i] = (byte)random.Next();
                }
                break;
            }
            case KeyType.EccWeirstrass:
            {
                ECKeyPairGenerator generator = new ECKeyPairGenerator("ECDSA");
                ECKeyGenerationParameters genParams = new ECKeyGenerationParameters(GetECDomainParametersFromKeyLength(wprKeyMetadata.Size*8), new SecureRandom());
                generator.Init(genParams);
                AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();
                // We keep track of the key pair so we can later on retrive the public key in the ReadPublicKey commands
                ecKeyPairs.Add(keyPair);

                keyLength = (wprKeyMetadata.HasPrivateKey ? wprKeyMetadata.Size : 0) + (wprKeyMetadata.HasPublicKey ? wprKeyMetadata.Size * 2 : 0);
                key = new byte[keyLength];
                uint offset = 0;

                // Extract private and public keys
                ECPrivateKeyParameters privateKey = (ECPrivateKeyParameters)keyPair.Private;
                ECPublicKeyParameters publicKey = (ECPublicKeyParameters)keyPair.Public;

                if(wprKeyMetadata.HasPrivateKey)
                {
                    // Extract private key as byte array with proper padding
                    byte[] privateKeyBytes = BigIntegerToFixedByteArray(privateKey.D, (int)wprKeyMetadata.Size);
                    Array.Copy(privateKeyBytes, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                    parent.Log(LogLevel.Noisy, "CREATE_KEY: EccWeirstrass: PrivateKey=[{0}]", BitConverter.ToString(privateKeyBytes));
                }

                if(wprKeyMetadata.HasPublicKey)
                {
                    // Extract public key coordinates (X and Y) with proper padding
                    byte[] publicKeyX = BigIntegerToFixedByteArray(publicKey.Q.AffineXCoord.ToBigInteger(), (int)wprKeyMetadata.Size);
                    byte[] publicKeyY = BigIntegerToFixedByteArray(publicKey.Q.AffineYCoord.ToBigInteger(), (int)wprKeyMetadata.Size);

                    parent.Log(LogLevel.Noisy, "CREATE_KEY: EccWeirstrass: PublicKeyX=[{0}]", BitConverter.ToString(publicKeyX));
                    parent.Log(LogLevel.Noisy, "CREATE_KEY: EccWeirstrass: PublicKeyY=[{0}]", BitConverter.ToString(publicKeyY));
                    // Copy X and Y coordinates to the key array
                    Array.Copy(publicKeyX, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                    Array.Copy(publicKeyY, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                }
                break;
            }
            default:
                parent.Log(LogLevel.Noisy, "CREATE_KEY: keyType={0} not supported");
                return ResponseCode.InvalidParameter;
            }

            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions outputTransferOptions;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "CREATE_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, outputTransferOptions, nextDescriptorPtr);

            switch(keyMode)
            {
            case KeyMode.Unprotected:
            {
                if(outputDataLength != keyLength)
                {
                    parent.Log(LogLevel.Error, "CREATE_KEY: output descriptor length mismatch {0} - {1}", outputDataLength, keyLength);
                    return ResponseCode.InvalidParameter;
                }
                WriteToRam(key, 0, outputDataPtr, keyLength);
                break;
            }
            case KeyMode.Wrapped:
            {
                ResponseCode result = WriteWrappedKeyToDescriptor(outputDataPtr, outputDataLength, key);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                parent.Log(LogLevel.Noisy, "CREATE_KEY: Assigning VOLATILE key to index {0}", keyIndex);
                volatileKeys[keyIndex] = key;
                break;
            }
            case KeyMode.Ksu:
            {
                parent.Log(LogLevel.Noisy, "CREATE_KEY: Storing key in KSU at index {0}", keyIndex);
                ksuStorage.AddKey(keyIndex, key);
                break;
            }
            default:
                parent.Log(LogLevel.Error, "CREATE_KEY: keyMode={0} invalid", keyMode);
                return ResponseCode.InvalidParameter;
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleReadPublicKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            // Authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            // Input key
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            uint privateInputKeyLength = 0;

            // TODO: for now we only support EccWeirstrass key type
            if(keyType == KeyType.EccWeirstrass)
            {
                EccWeirstrassKeyMetadata wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
                privateInputKeyLength = wprKeyMetadata.Size;
                if(!wprKeyMetadata.HasPrivateKey)
                {
                    parent.Log(LogLevel.Error, "READ_PUB_KEY: Input EccWeirstrass has no private key");
                    return ResponseCode.InvalidParameter;
                }
            }
            else
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            byte[] privateInputKey = null;

            // Copy the private input key to privateInputKey
            switch(keyMode)
            {
            case KeyMode.Unprotected:
            {
                if(privateInputKeyLength != inputKeyLength)
                {
                    parent.Log(LogLevel.Error, "READ_PUB_KEY: input key descriptor length mismatch {0} - {1}", privateInputKeyLength, inputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                privateInputKey = new byte[privateInputKeyLength];
                FetchFromRam(inputKeyPtr, privateInputKey, 0, privateInputKeyLength);
                break;
            }
            case KeyMode.Wrapped:
            {
                ResponseCode result = ReadWrappedKeyFromDescriptor(inputKeyPtr, inputKeyLength, out privateInputKey);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                privateInputKey = volatileKeys[keyIndex];
                if(inputKeyLength != privateInputKey.Length)
                {
                    parent.Log(LogLevel.Error, "READ_PUB_KEY: input key descriptor length mismatch {0} - {1}", privateInputKey.Length, inputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                break;
            }
            case KeyMode.Ksu:
            {
                privateInputKey = ksuStorage.GetKey(keyIndex);
                if(inputKeyLength != privateInputKey.Length)
                {
                    parent.Log(LogLevel.Error, "READ_PUB_KEY: input key descriptor length mismatch {0} - {1}", privateInputKey.Length, inputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                break;
            }
            default:
                parent.Log(LogLevel.Error, "READ_PUB_KEY: keyMode={0} invalid", keyMode);
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: PrivateKey=[{0}]", BitConverter.ToString(privateInputKey));

            // Find the corresponding key pair based on the private key
            AsymmetricCipherKeyPair keyPair = LookUpKeyPair(privateInputKey);
            if(keyPair == null)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: corresponding key pair not found");
                return ResponseCode.InvalidParameter;
            }

            // Extract public key coordinates (X and Y) with proper padding
            byte[] publicKeyX = BigIntegerToFixedByteArray(((ECPublicKeyParameters)(keyPair.Public)).Q.AffineXCoord.ToBigInteger(), (int)privateInputKeyLength);
            byte[] publicKeyY = BigIntegerToFixedByteArray(((ECPublicKeyParameters)(keyPair.Public)).Q.AffineYCoord.ToBigInteger(), (int)privateInputKeyLength);

            // Combine X and Y coordinates
            byte[] outputPublicKey = new byte[privateInputKeyLength * 2];
            Array.Copy(publicKeyX, 0, outputPublicKey, 0, (int)privateInputKeyLength);
            Array.Copy(publicKeyY, 0, outputPublicKey, (int)privateInputKeyLength, (int)privateInputKeyLength);

            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions outputTransferOptions;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, outputTransferOptions, nextDescriptorPtr);
            if(outputDataLength != outputPublicKey.Length)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: outputDataLength mismatch {0} - {1}", outputDataLength, outputPublicKey.Length);
                return ResponseCode.InvalidParameter;
            }

            WriteToRam(outputPublicKey, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Noisy, "READ_PUB_KEY: PublicKey=[{0}]", BitConverter.ToString(outputPublicKey));

            return ResponseCode.Ok;
        }

        private ResponseCode HandleDiffieHellmanCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint outputKeyMetadata = commandParams[1];
            KeyMode keyMode, outputKeyMode;
            KeyType keyType, outputKeyType;
            KeyRestriction keyRestriction, outputKeyRestriction;
            uint keyIndex, outputKeyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint netPrivateInputKeyLength = 0;

            // TODO: for now we only support EccWeirstrass key type
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            EccWeirstrassKeyMetadata wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
            netPrivateInputKeyLength = wprKeyMetadata.Size;
            if(!wprKeyMetadata.HasPrivateKey)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Input EccWeirstrass has no private key");
                return ResponseCode.InvalidParameter;
            }

            // Key Metadata for derived key (shared secret). Must be a RAW key with the correct length.
            UnpackKeyMetadata(outputKeyMetadata, out outputKeyIndex, out outputKeyType, out outputKeyMode, out outputKeyRestriction);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: outputKeyIndex={0} outputKeyType={1} outputKeyMode={2} outputKeyRestriction={3}",
                     outputKeyIndex, outputKeyType, outputKeyMode, outputKeyRestriction);

            if(outputKeyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Derived key must be of Raw type (KeyType={0})", outputKeyType);
                return ResponseCode.InvalidParameter;
            }

            // Input key authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            // Input key
            uint privateInputKeyPtr;
            uint privateInputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out privateInputKeyPtr, out privateInputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: privateInputKeyPtr=0x{0:X} privateInputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       privateInputKeyPtr, privateInputKeyLength, transferOptions, nextDescriptorPtr);

            byte[] privateInputKey = null;

            // Copy the private input key to privateInputKey array
            switch(keyMode)
            {
            case KeyMode.Unprotected:
            {
                if(privateInputKeyLength != netPrivateInputKeyLength)
                {
                    parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: input key descriptor length mismatch {0} - {1}", privateInputKeyLength, netPrivateInputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                privateInputKey = new byte[netPrivateInputKeyLength];
                FetchFromRam(privateInputKeyPtr, privateInputKey, 0, netPrivateInputKeyLength);
                break;
            }
            case KeyMode.Wrapped:
            {
                ResponseCode result = ReadWrappedKeyFromDescriptor(privateInputKeyPtr, privateInputKeyLength, out privateInputKey);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                privateInputKey = volatileKeys[keyIndex];
                if(netPrivateInputKeyLength != privateInputKey.Length)
                {
                    parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Volatile key length mismatch {0} - {1}", privateInputKey.Length, netPrivateInputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                break;
            }
            case KeyMode.Ksu:
            {
                privateInputKey = ksuStorage.GetKey(keyIndex);
                if(netPrivateInputKeyLength != privateInputKey.Length)
                {
                    parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: KSU key length mismatch {0} - {1}", privateInputKey.Length, netPrivateInputKeyLength);
                    return ResponseCode.InvalidParameter;
                }
                break;
            }
            default:
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: keyMode={0} invalid", keyMode);
                return ResponseCode.InvalidParameter;
            }

            // Public input key (other party)
            uint publicInputKeyPtr;
            uint publicInputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out publicInputKeyPtr, out publicInputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: publicInputKeyPtr=0x{0:X} publicInputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       publicInputKeyPtr, publicInputKeyLength, transferOptions, nextDescriptorPtr);

            if(publicInputKeyLength != netPrivateInputKeyLength * 2)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: public input key length mismatch {0} - {1}", publicInputKeyLength, netPrivateInputKeyLength * 2);
                return ResponseCode.InvalidParameter;
            }
            byte[] publicInputKey = new byte[publicInputKeyLength];
            FetchFromRam(publicInputKeyPtr, publicInputKey, 0, publicInputKeyLength);

            // TODO: the following code assumes keyType to be EccWeirstrass. This was already checked earlier in this method.

            ECDomainParameters domainParams = GetECDomainParametersFromKeyLength(netPrivateInputKeyLength * 8);

            // Create ECPrivateKeyParameters from private key bytes
            BigInteger privateKeyValue = new BigInteger(1, privateInputKey); // 1 for positive sign
            ECPrivateKeyParameters privateKeyParam = new ECPrivateKeyParameters(privateKeyValue, domainParams);

            // Create ECPublicKeyParameters from public key bytes
            int coordinateSize = publicInputKey.Length / 2;
            byte[] xBytes = new byte[coordinateSize];
            byte[] yBytes = new byte[coordinateSize];

            Array.Copy(publicInputKey, 0, xBytes, 0, coordinateSize);
            Array.Copy(publicInputKey, coordinateSize, yBytes, 0, coordinateSize);

            BigInteger x = new BigInteger(1, xBytes);
            BigInteger y = new BigInteger(1, yBytes);

            ECPoint point = domainParams.Curve.CreatePoint(x, y);
            ECPublicKeyParameters publicKeyParam = new ECPublicKeyParameters(point, domainParams);

            // Perform ECDH calculation manually to get both X and Y coordinates
            ECPoint sharedPoint = publicKeyParam.Q.Multiply(privateKeyValue).Normalize();

            // Extract X and Y coordinates with proper padding to coordinate size
            byte[] sharedSecretX = BigIntegerToFixedByteArray(sharedPoint.AffineXCoord.ToBigInteger(), (int)netPrivateInputKeyLength);
            byte[] sharedSecretY = BigIntegerToFixedByteArray(sharedPoint.AffineYCoord.ToBigInteger(), (int)netPrivateInputKeyLength);

            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: SharedSecretX=[{0}]", BitConverter.ToString(sharedSecretX));
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: SharedSecretY=[{0}]", BitConverter.ToString(sharedSecretY));

            // Use X coordinate as the shared secret (standard ECDH behavior)
            byte[] sharedSecret = sharedSecretX;

            // Output DMA descriptor contains the shared secret
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "DIFFIE_HELLMAN: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Check if output buffer expects both X and Y coordinates (64 bytes total for P-256)
            if(outputDataLength != sharedSecretX.Length + sharedSecretY.Length)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Output descriptor length mismatch {0} vs expected {1}", outputDataLength, sharedSecretX.Length + sharedSecretY.Length);
                return ResponseCode.InvalidParameter;
            }

            WriteToRam(sharedSecretX, 0, outputDataPtr, (uint)sharedSecretX.Length);
            WriteToRam(sharedSecretY, 0, outputDataPtr + (uint)sharedSecretX.Length, (uint)sharedSecretY.Length);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeRound1Command(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            int expectedCommandParamsCount = -1;
            bool isGenerateCommand = (commandOptions & 0x100) == 0;
            if(isGenerateCommand)
            {
                expectedCommandParamsCount = 2;
            }
            else
            {
                expectedCommandParamsCount = 3;
            }

            if(commandParamsCount != expectedCommandParamsCount)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: invalid parameter count {0} vs expected {1} (isGenerateCommand={2})", commandParamsCount, expectedCommandParamsCount, isGenerateCommand);
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint userIdSize = commandParams[1];
            uint peerUserIdSize = 0;
            if(!isGenerateCommand)
            {
                peerUserIdSize = commandParams[2];
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}, userIdSize={4} peerUserIdSize={5}",
                       keyIndex, keyType, keyMode, keyRestriction, userIdSize, peerUserIdSize);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            return (isGenerateCommand) ? HandleJPakeRound1GenerateCommand(nextDescriptorPtr, outputDma, userIdSize)
                                       : HandleJPakeRound1VerifyCommand(nextDescriptorPtr, outputDma, userIdSize, peerUserIdSize);
        }

        private ResponseCode HandleJPakeRound1GenerateCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(inputDma, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_GEN: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // EC-JPAKE Round 1 Generation using NIST P-256 curve and SHA256
            // Get NIST P-256 curve parameters
            var curve = GetP256CurveParameters();
            var random = new SecureRandom();

            // Generate ephemeral key pairs (x1, g1) and (x2, g2)
            // x1, x2 are private scalars, g1 = x1*G, g2 = x2*G where G is the base point
            // TODO: if X1=O or X2=O, we should reject X1 and X2 and redraw (practically, never happens)
            var x1 = GenerateRandomScalar(curve, random);
            var x2 = GenerateRandomScalar(curve, random);

            var pointX1 = curve.G.Multiply(x1).Normalize();
            var pointX2 = curve.G.Multiply(x2).Normalize();

            // Generate Schnorr proofs for knowledge of discrete logarithms
            // ZKP{x1}: Prove knowledge of x1 such that g1 = x1*G
            var zkp1 = GenerateSchnorrProof(curve, x1, pointX1, userId, random, "JPAKE-X1");

            // ZKP{x2}: Prove knowledge of x2 such that g2 = x2*G
            var zkp2 = GenerateSchnorrProof(curve, x2, pointX2, userId, random, "JPAKE-X2");

            byte[] x2Bytes = BigIntegerToFixedByteArray(x2, 32);
            byte[] pointX1Bytes = EncodeECPointToBytes(pointX1);
            byte[] pointX2Bytes = EncodeECPointToBytes(pointX2);
            byte[] zkp1Bytes = EncodeSchnorrProofToBytes(zkp1);
            byte[] zkp2Bytes = EncodeSchnorrProofToBytes(zkp2);

            // First output DMA descriptor - random 32-byte scalar "S2"
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != x2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT0 (random scalar S2): Data length mismatch, expected {0} got {1}", x2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(x2Bytes, 0, outputDataPtr, (uint)x2Bytes.Length);

            // Second output DMA descriptor - point X1
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != pointX1Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT1 (point X1): Data length mismatch, expected {0} got {1}", pointX1Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(pointX1Bytes, 0, outputDataPtr, (uint)pointX1Bytes.Length);

            // Third output DMA descriptor - X1 ZKP
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != zkp1Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT2 (X1 ZKP): Data length mismatch, expected {0} got {1}", zkp1Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkp1Bytes, 0, outputDataPtr, (uint)zkp1Bytes.Length);

            // Fourth output DMA descriptor - point X2
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != pointX2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT3 (point X2): Data length mismatch, expected {0} got {1}", pointX2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(pointX2Bytes, 0, outputDataPtr, (uint)pointX2Bytes.Length);

            // Fifth output DMA descriptor - X2 ZKP
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != zkp2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT4 (X2 ZKP): Data length mismatch, expected {0} got {1}", zkp2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkp2Bytes, 0, outputDataPtr, (uint)zkp2Bytes.Length);

            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_GEN: Generated X1=[{0}], X2=[{1}]",
                       BitConverter.ToString(pointX1Bytes), BitConverter.ToString(pointX2Bytes));
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_GEN: Generated ZKP1=[{0}], ZKP2=[{1}]",
                       BitConverter.ToString(zkp1Bytes), BitConverter.ToString(zkp2Bytes));

            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeRound1VerifyCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions, uint peerUserIdLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(inputDma, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // Peer user ID
            uint peerUserIdPtr;
            uint peerUserIdLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out peerUserIdPtr, out peerUserIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: peerUserIdPtr=0x{0:X} peerUserIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       peerUserIdPtr, peerUserIdLength, transferOptions, nextDescriptorPtr);
            if(peerUserIdLength != peerUserIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: Peer User ID length mismatch, expected {0} got {1}", peerUserIdLengthFromCommandOptions, peerUserIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] peerUserId = new byte[peerUserIdLength];
            FetchFromRam(peerUserIdPtr, peerUserId, 0, peerUserIdLength);

            // Point X3 (from other party round 1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Zero knowledge proof for X3 (from other party round 1)
            uint zkp3Ptr;
            uint zkp3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkp3Ptr, out zkp3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: zkp3Ptr=0x{0:X} zkp3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkp3Ptr, zkp3Length, transferOptions, nextDescriptorPtr);
            byte[] zkp3 = new byte[zkp3Length];
            FetchFromRam(zkp3Ptr, zkp3, 0, zkp3Length);

            // Point X4 (from other party round 1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // Zero knowledge proof for X4 (from other party round 1)
            uint zkp4Ptr;
            uint zkp4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkp4Ptr, out zkp4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: zkp4Ptr=0x{0:X} zkp4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkp4Ptr, zkp4Length, transferOptions, nextDescriptorPtr);
            byte[] zkp4 = new byte[zkp4Length];
            FetchFromRam(zkp4Ptr, zkp4, 0, zkp4Length);

            // Verify the Schnorr zero-knowledge proofs for X3 and X4
            var curve = GetP256CurveParameters();

            ECPoint x3Point, x4Point;
            SchnorrProof zkp3Proof, zkp4Proof;

            try
            {
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                zkp3Proof = DecodeSchnorrProofFromBytes(zkp3, curve);
                zkp4Proof = DecodeSchnorrProofFromBytes(zkp4, curve);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: Failed to decode points or proofs: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x3Point.IsInfinity || x4Point.IsInfinity || zkp3Proof.V.IsInfinity || zkp4Proof.V.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x3Point.IsValid() || !x4Point.IsValid() || !zkp3Proof.V.IsValid() || !zkp4Proof.V.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // X3 and X4 must be different points
            if(x3Point.Equals(x4Point))
            {
                parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: Received points X3 and X4 are identical");
                return ResponseCode.InvalidParameter;
            }

            // Verify ZKP for X3: Check that the other party knows x3 such that X3 = x3*G
            bool zkp3Valid = VerifySchnorrProof(curve, x3Point, zkp3Proof, peerUserId, "JPAKE-X1");

            // Verify ZKP for X4: Check that the other party knows x4 such that X4 = x4*G
            bool zkp4Valid = VerifySchnorrProof(curve, x4Point, zkp4Proof, peerUserId, "JPAKE-X2");

            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: X3 verification: {0}, X4 verification: {1}", zkp3Valid, zkp4Valid);

            // Both proofs must be valid for the verification to succeed
            if(!zkp3Valid || !zkp4Valid)
            {
                parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: Zero-knowledge proof verification failed");
                return ResponseCode.CryptoError;
            }

            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1_VER: Verification successful");
            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeRound2Command(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            int expectedCommandParamsCount = -1;
            bool isGenerateCommand = (commandOptions & 0x100) == 0;
            if(isGenerateCommand)
            {
                expectedCommandParamsCount = 3;
            }
            else
            {
                expectedCommandParamsCount = 2;
            }

            if(commandParamsCount != expectedCommandParamsCount)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: invalid parameter count {0} vs expected {1} (isGenerateCommand={2})", commandParamsCount, expectedCommandParamsCount, isGenerateCommand);
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint passwordSize = 0;
            uint userIdSize = 0;
            uint peerUserIdSize = 0;
            if(isGenerateCommand)
            {
                passwordSize = commandParams[1];
                userIdSize = commandParams[2];
            }
            else
            {
                peerUserIdSize = commandParams[1];
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}, userIdSize={4} peerUserIdSize={5}",
                       keyIndex, keyType, keyMode, keyRestriction, userIdSize, peerUserIdSize);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_1: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            return (isGenerateCommand) ? HandleJPakeRound2GenerateCommand(nextDescriptorPtr, outputDma, userIdSize, passwordSize)
                                       : HandleJPakeRound2VerifyCommand(nextDescriptorPtr, outputDma, peerUserIdSize);
        }

        private ResponseCode HandleJPakeRound2GenerateCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions, uint passwordLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // Password
            uint passwordPtr;
            uint passwordLength;
            UnpackDmaDescriptor(inputDma, out passwordPtr, out passwordLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: passwordPtr=0x{0:X} passwordLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       passwordPtr, passwordLength, transferOptions, nextDescriptorPtr);
            if(passwordLength != passwordLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: Password length mismatch, expected {0} got {1}", passwordLengthFromCommandOptions, passwordLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] password = new byte[passwordLength];
            FetchFromRam(passwordPtr, password, 0, passwordLength);

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // S2 random scalar (from round1)
            uint s2RandomScalarPtr;
            uint s2RandomScalarLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out s2RandomScalarPtr, out s2RandomScalarLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: s2RandomScalarPtr=0x{0:X} s2RandomScalarLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       s2RandomScalarPtr, s2RandomScalarLength, transferOptions, nextDescriptorPtr);
            byte[] s2RandomScalar = new byte[s2RandomScalarLength];
            FetchFromRam(s2RandomScalarPtr, s2RandomScalar, 0, s2RandomScalarLength);

            // Point X1 (from round1)
            uint pointX1Ptr;
            uint pointX1Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX1Ptr, out pointX1Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: pointX1Ptr=0x{0:X} pointX1Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX1Ptr, pointX1Length, transferOptions, nextDescriptorPtr);
            byte[] pointX1 = new byte[pointX1Length];
            FetchFromRam(pointX1Ptr, pointX1, 0, pointX1Length);

            // Point X3 (from other party round1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Point X4 (from other party round1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // EC-JPAKE Round 2 Generation using NIST P-256 curve and SHA256
            var curve = GetP256CurveParameters();
            var random = new SecureRandom();

            ECPoint x1Point, x3Point, x4Point;
            BigInteger x2Scalar;

            try
            {
                x1Point = DecodeECPointFromBytes(pointX1, curve);
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                x2Scalar = new BigInteger(1, s2RandomScalar);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: Failed to decode points or scalars: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x1Point.IsInfinity || x3Point.IsInfinity || x4Point.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x1Point.IsValid() || !x3Point.IsValid() || !x4Point.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // Compute the password scalar using EC-JPAKE standard derivation
            var passwordScalar = ComputePasswordScalar(userId, password);

            // Round 2: Compute the combined public key
            // B = X1 + X3 + X4 (our X1 plus both of the other party's points)
            var pointB = x1Point.Add(x3Point).Add(x4Point).Normalize();

            // In EC-JPAKE Round 2, we use x2 from Round 1 combined with the password scalar
            // k = x2 * passwordScalar mod n (combine our Round 1 scalar with password)
            var k = x2Scalar.Multiply(passwordScalar).Mod(curve.N);

            // Compute A = k * B (this is the point we send to the other party)
            var pointA = pointB.Multiply(k).Normalize();

            // Generate Schnorr proof for knowledge of k such that A = k * B
            // This proves we know the discrete logarithm of A with respect to base B
            var zkpA = GenerateSchnorrProof(curve, k, pointA, userId, random, "JPAKE-Round2", pointB);

            // Encode the output data
            byte[] aBytes = EncodeECPointToBytes(pointA);
            byte[] zkpABytes = EncodeSchnorrProofToBytes(zkpA);

            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: Generated A=[{0}]", BitConverter.ToString(aBytes));
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: Generated ZKP_A=[{0}]", BitConverter.ToString(zkpABytes));

            uint outputDataPtr;
            uint outputDataLength;

            // First output DMA descriptor - point A
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: OUTPUT1: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            if(outputDataLength != aBytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: OUTPUT1 (point A): Data length mismatch, expected {0} got {1}", aBytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(aBytes, 0, outputDataPtr, (uint)aBytes.Length);

            // Second output DMA descriptor - ZKP for A
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_GEN: OUTPUT2: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            if(outputDataLength != zkpABytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: OUTPUT2 (ZKP for A): Data length mismatch, expected {0} got {1}", zkpABytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkpABytes, 0, outputDataPtr, (uint)zkpABytes.Length);

            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeRound2VerifyCommand(uint inputDma, uint outputDma, uint peerUserIdLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // Peer User ID
            uint peerUserIdPtr;
            uint peerUserIdLength;
            UnpackDmaDescriptor(inputDma, out peerUserIdPtr, out peerUserIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: peerUserIdPtr=0x{0:X} peerUserIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       peerUserIdPtr, peerUserIdLength, transferOptions, nextDescriptorPtr);
            if(peerUserIdLength != peerUserIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Peer User ID length mismatch, expected {0} got {1}", peerUserIdLengthFromCommandOptions, peerUserIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] peerUserId = new byte[peerUserIdLength];
            FetchFromRam(peerUserIdPtr, peerUserId, 0, peerUserIdLength);

            // Point X1 (from round1)
            uint pointX1Ptr;
            uint pointX1Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX1Ptr, out pointX1Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: pointX1Ptr=0x{0:X} pointX1Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX1Ptr, pointX1Length, transferOptions, nextDescriptorPtr);
            byte[] pointX1 = new byte[pointX1Length];
            FetchFromRam(pointX1Ptr, pointX1, 0, pointX1Length);

            // Point X2 (from round1)
            uint pointX2Ptr;
            uint pointX2Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX2Ptr, out pointX2Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: pointX2Ptr=0x{0:X} pointX2Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX2Ptr, pointX2Length, transferOptions, nextDescriptorPtr);
            byte[] pointX2 = new byte[pointX2Length];
            FetchFromRam(pointX2Ptr, pointX2, 0, pointX2Length);

            // Point X3 (from other party round1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Point B (from other party round2)
            uint pointBPtr;
            uint pointBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointBPtr, out pointBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: pointBPtr=0x{0:X} pointBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointBPtr, pointBLength, transferOptions, nextDescriptorPtr);
            byte[] pointB = new byte[pointBLength];
            FetchFromRam(pointBPtr, pointB, 0, pointBLength);

            // Zero knowledge proof for B (from other party round 2)
            uint zkpBPtr;
            uint zkpBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkpBPtr, out zkpBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: zkpBPtr=0x{0:X} zkpBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkpBPtr, zkpBLength, transferOptions, nextDescriptorPtr);
            byte[] zkpB = new byte[zkpBLength];
            FetchFromRam(zkpBPtr, zkpB, 0, zkpBLength);

            // EC-JPAKE Round 2 Verification using NIST P-256 curve and SHA256
            var curve = GetP256CurveParameters();

            ECPoint x1Point, x2Point, x3Point, bPoint;
            SchnorrProof zkpBProof;

            try
            {
                x1Point = DecodeECPointFromBytes(pointX1, curve);
                x2Point = DecodeECPointFromBytes(pointX2, curve);
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                bPoint = DecodeECPointFromBytes(pointB, curve);
                zkpBProof = DecodeSchnorrProofFromBytes(zkpB, curve);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Exception during verification: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x1Point.IsInfinity || x2Point.IsInfinity || x3Point.IsInfinity || bPoint.IsInfinity || zkpBProof.V.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x1Point.IsValid() || !x2Point.IsValid() || !x3Point.IsValid() || !bPoint.IsValid() || !zkpBProof.V.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // Rebuild the prover's base: B_base = X1 + X2 + X3
            var bBase = x1Point.Add(x2Point).Add(x3Point).Normalize();

            // Reject if B_base is not on-curve or is infinity
            if(bBase.IsInfinity || !bBase.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Computed B_base is invalid or at infinity");
                return ResponseCode.InvalidParameter;
            }

            // Verify the Schnorr proof: ZKP(B) with base point B_base
            // Using "JPAKE-Round2" as the standard label for Round 2 proofs
            bool zkpBValid = VerifySchnorrProof(curve, bPoint, zkpBProof, peerUserId, "JPAKE-Round2", bBase);

            if(!zkpBValid)
            {
                parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: ZKP(B) verification failed");
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Noisy, "JPAKE_ROUND_2_VER: Verification successful");
            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeGenerateSessionKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "JPAKE_GENERATE_SESSION_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            ShaMode hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint keyMetadata = commandParams[0];
            uint passwordLengthFromCommandOptions = commandParams[1];
            uint outKeyMetadata = commandParams[2];

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}", keyIndex, keyType, keyMode, keyRestriction);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            UnpackKeyMetadata(outKeyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: OUT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}", keyIndex, keyType, keyMode, keyRestriction);
            //EccWeirstrassKeyMetadata wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(outKeyMetadata);

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            // Password
            uint passwordPtr;
            uint passwordLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out passwordPtr, out passwordLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: passwordPtr=0x{0:X} passwordLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       passwordPtr, passwordLength, transferOptions, nextDescriptorPtr);
            if(passwordLength != passwordLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Password length mismatch, expected {0} got {1}", passwordLengthFromCommandOptions, passwordLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] password = new byte[passwordLength];
            FetchFromRam(passwordPtr, password, 0, passwordLength);

            // Point X2 (from round1)
            uint pointX2Ptr;
            uint pointX2Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX2Ptr, out pointX2Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: pointX2Ptr=0x{0:X} pointX2Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX2Ptr, pointX2Length, transferOptions, nextDescriptorPtr);
            byte[] pointX2 = new byte[pointX2Length];
            FetchFromRam(pointX2Ptr, pointX2, 0, pointX2Length);

            // Point X4 (from other party round1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // Point B (from other party round2)
            uint pointBPtr;
            uint pointBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointBPtr, out pointBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: pointBPtr=0x{0:X} pointBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointBPtr, pointBLength, transferOptions, nextDescriptorPtr);
            byte[] pointB = new byte[pointBLength];
            FetchFromRam(pointBPtr, pointB, 0, pointBLength);

            // x2 scalar (our private scalar from round 1)
            uint x2ScalarPtr;
            uint x2ScalarLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out x2ScalarPtr, out x2ScalarLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: x2ScalarPtr=0x{0:X} x2ScalarLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       x2ScalarPtr, x2ScalarLength, transferOptions, nextDescriptorPtr);
            byte[] x2ScalarBytes = new byte[x2ScalarLength];
            FetchFromRam(x2ScalarPtr, x2ScalarBytes, 0, x2ScalarLength);

            // Auth data for the generated key. length=0 if the generated key is not wrapped.
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);
            byte[] authData = new byte[authDataLength];
            FetchFromRam(authDataPtr, authData, 0, authDataLength);

            // EC-JPAKE Session Key Generation using NIST P-256 curve and SHA256
            var curve = GetP256CurveParameters();

            ECPoint x2Point, x4Point, bPoint;
            BigInteger x2Scalar;

            try
            {
                x2Point = DecodeECPointFromBytes(pointX2, curve);
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                bPoint = DecodeECPointFromBytes(pointB, curve);
                x2Scalar = new BigInteger(1, x2ScalarBytes);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Exception during session key generation: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x2Point.IsInfinity || x4Point.IsInfinity || bPoint.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x2Point.IsValid() || !x4Point.IsValid() || !bPoint.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // Derive password scalar: s = H(password) mod n. Ensure s != 0
            var passwordScalar = ComputePasswordScalar(new byte[0], password); // Use empty user ID for session key derivation
            if(passwordScalar.Equals(BigInteger.Zero))
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Password scalar is zero");
                return ResponseCode.InvalidParameter;
            }

            // Compute shared secret point: S = x2 * (B - s * X4)
            // First compute s * X4
            var sX4 = x4Point.Multiply(passwordScalar).Normalize();

            // Then compute B - s * X4
            var bMinusSX4 = bPoint.Subtract(sX4).Normalize();

            // Finally compute S = x2 * (B - s * X4)
            var sharedSecretPoint = bMinusSX4.Multiply(x2Scalar).Normalize();

            // Check if shared secret point is valid and not infinity
            if(sharedSecretPoint.IsInfinity || !sharedSecretPoint.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Shared secret point is invalid or at infinity");
                return ResponseCode.InvalidParameter;
            }

            // Derive session key as SHA-256(S.x_coord)
            var xCoordBytes = BigIntegerToFixedByteArray(sharedSecretPoint.AffineXCoord.ToBigInteger(), 32);

            var sha256 = new Sha256Digest();
            sha256.BlockUpdate(xCoordBytes, 0, xCoordBytes.Length);
            var sessionKey = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(sessionKey, 0);

            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: Generated session key=[{0}]", BitConverter.ToString(sessionKey));

            // Output the session key based on the output key mode
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2}",
                        outputDataPtr, outputDataLength, transferOptions);

            switch(keyMode)
            {
            case KeyMode.Unprotected:
                if(outputDataLength != sessionKey.Length)
                {
                    parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Output data length mismatch, expected {0} got {1}", sessionKey.Length, outputDataLength);
                    return ResponseCode.InvalidParameter;
                }
                WriteToRam(sessionKey, 0, outputDataPtr, (uint)sessionKey.Length);
                break;
            case KeyMode.Wrapped:
            {
                ResponseCode result = WriteWrappedKeyToDescriptor(outputDataPtr, outputDataLength, sessionKey);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: Storing session key as volatile key at index {0}", keyIndex);
                volatileKeys[keyIndex] = sessionKey;
                break;
            }
            case KeyMode.Ksu:
            {
                parent.Log(LogLevel.Noisy, "JPAKE_GEN_KEY: Storing session key in KSU at index {0}", keyIndex);
                ksuStorage.AddKey(keyIndex, sessionKey);
                break;
            }
            default:
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Unsupported key mode {0}", keyMode);
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Info, "JPAKE_GEN_KEY: Session key generation successful");
            return ResponseCode.Ok;
        }

        private void FillArray(byte[] arr, uint offset, uint length, byte value)
        {
            for(uint i = offset; i < offset + length && i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }

        private ResponseCode ReadWrappedKeyFromDescriptor(uint descriptorDataPtr, uint descriptorDataLength, out byte[] key)
        {
            // DMA is expected to be structured as follows:
            // - 12 bytes: random IV assigned at time of wrapping
            // - var bytes: encrypted key data
            // - 16 bytes: AESGCM authentication tag
            if(descriptorDataLength <= (KeyRandomIvLength + KeyAuthenticationTagLength))
            {
                parent.Log(LogLevel.Error, "ReadWrappedKeyFromDescriptor: input key descriptor invalid length mismatch {0}", descriptorDataLength);
                key = null;
                return ResponseCode.InvalidParameter;
            }

            key = new byte[descriptorDataLength - KeyRandomIvLength - KeyAuthenticationTagLength];
            FetchFromRam(descriptorDataPtr + KeyRandomIvLength, key, 0, (uint)key.Length);

            return ResponseCode.Ok;
        }

        private ResponseCode WriteWrappedKeyToDescriptor(uint descriptorDataPtr, uint descriptorDataLength, byte[] key)
        {
            // Output DMA is expected to be structured as follows:
            // - 12 bytes: random IV assigned at time of wrapping
            // - var bytes: encrypted key data
            // - 16 bytes: AESGCM authentication tag
            if(descriptorDataLength != (KeyRandomIvLength + key.Length + KeyAuthenticationTagLength))
            {
                parent.Log(LogLevel.Error, "WriteWrappedKeyToDescriptor: descriptor length mismatch {0} - {1}", descriptorDataLength, (KeyRandomIvLength + key.Length + KeyAuthenticationTagLength));
                return ResponseCode.InvalidParameter;
            }

            // TODO: for now we simply copy the plaintext key to the output DMA. 
            // Random IV and AESGCM tags are assigned to special markers for debugging purposes.
            uint offset = 0;

            // Random IV (fake random IV, filled with specific marker)
            byte[] randomIV = new byte[KeyRandomIvLength];
            FillArray(randomIV, 0, KeyRandomIvLength, WrappedKeyRandomIVMarker);
            WriteToRam(randomIV, 0, descriptorDataPtr + offset, KeyRandomIvLength);
            offset += KeyRandomIvLength;
            // "encrypted" key data
            WriteToRam(key, 0, descriptorDataPtr + offset, (uint)key.Length);
            offset += (uint)key.Length;
            // AESGCM authentication tag (fake tag, filled with specific marker)
            byte[] authTag = new byte[KeyAuthenticationTagLength];
            FillArray(authTag, 0, KeyAuthenticationTagLength, WrappedKeyAuthenticationTagMarker);
            WriteToRam(authTag, 0, descriptorDataPtr + offset, KeyAuthenticationTagLength);

            return ResponseCode.Ok;
        }

        private ECDomainParameters GetECDomainParametersFromKeyLength(uint length)
        {
            DerObjectIdentifier oid;

            switch(length)
            {
            case 192:
                oid = X9ObjectIdentifiers.Prime192v1;
                break;
            case 224:
                oid = SecObjectIdentifiers.SecP224r1;
                break;
            case 239:
                oid = X9ObjectIdentifiers.Prime239v1;
                break;
            case 256:
                oid = X9ObjectIdentifiers.Prime256v1;
                break;
            case 384:
                oid = SecObjectIdentifiers.SecP384r1;
                break;
            case 521:
                oid = SecObjectIdentifiers.SecP521r1;
                break;
            default:
                throw new InvalidParameterException("unknown key size.");
            }

            X9ECParameters ecps = ECNamedCurveTable.GetByOid(oid);

            return new ECDomainParameters(ecps.Curve, ecps.G, ecps.N, ecps.H, ecps.GetSeed());
        }

        private AsymmetricCipherKeyPair LookUpKeyPair(byte[] privateKey)
        {
            // Find the corresponding key pair based on the private key
            foreach(var keyPair in ecKeyPairs)
            {
                ECPrivateKeyParameters privKey = (ECPrivateKeyParameters)keyPair.Private;

                // Pad stored private key to match input length for comparison
                byte[] storedPrivateKeyBytes = BigIntegerToFixedByteArray(privKey.D, privateKey.Length);

                if(storedPrivateKeyBytes.SequenceEqual(privateKey))
                {
                    return keyPair;
                }
            }

            return null;
        }

        // from sl_se_manager_key_handling.c
        // Asymmetric key attributes:
        // KEYSPEC_ATTRIBUTES_ECC_PRIVATE_MASK (1U << 14)
        // KEYSPEC_ATTRIBUTES_ECC_PUBLIC_MASK  (1U << 13)
        // KEYSPEC_ATTRIBUTES_ECC_DOMAIN       (1U << 12)
        // KEYSPEC_ATTRIBUTES_ECC_SIGN         (1U << 10)
        // KEYSPEC_ATTRIBUTES_ECC_SIZE_MASK    0x0000007fU
        private EccWeirstrassKeyMetadata UnpackWeirstrassPrimeFieldKeyMetadata(uint keyMetadata)
        {
            var result = new EccWeirstrassKeyMetadata();

            // [6:0]: size of key in bytes minus 1 (0 = 8 bits, 127 = 1024 bits)
            result.Size = (keyMetadata & 0x7F) + 1;
            // [10]: true for signing false for key exchange
            result.Purpose = (keyMetadata & 0x0400) != 0 ? EccWeirstrassPurpose.Signature : EccWeirstrassPurpose.KeyExchange;
            // [12]: true if domain parameters are included
            result.HasDomainParameters = (keyMetadata & 0x1000) != 0;
            // [13]: true if public key included
            result.HasPublicKey = (keyMetadata & 0x2000) != 0;
            // [14]: true if private key included
            result.HasPrivateKey = (keyMetadata & 0x4000) != 0;

            // use params if domain else use selCurve
            if(result.HasDomainParameters)
            {
                // [5]: true if a = 0. Ignored if domain=0
                result.A0 = (keyMetadata & 0x0020) != 0;
                // [6]: true if a = -3. Ignored if domain=0
                result.An3 = (keyMetadata & 0x0040) != 0;
                // [7]: reserved
            }
            else
            {
                // [7:5]: Curve selected when domain = 0.
                // Only 0x0 is available (standard Weierstrass curve of size EccWPrMetadata.size).
                result.SelectCurve = (EccWeirstrassCurve)((keyMetadata >> 5) & 0x07);
                if(result.SelectCurve != EccWeirstrassCurve.CurveP192)
                {
                    throw new NotSupportedException("Only ECC Curve P-192 is supported ");
                }
            }

            parent.Log(LogLevel.Noisy, "UnpackWeirstrassPrimeFieldKeyMetadata: priKey={0} pubKey={1} domain={2} purpose={3} size={4}",
                    result.HasPrivateKey, result.HasPublicKey, result.HasDomainParameters, result.Purpose, result.Size);

            return result;
        }

        private byte[] CheckAndRetrieveKey(KeyType keyType, KeyMode keyMode, uint keyIndex, uint keyPointer)
        {
            byte[] key = new byte[16];

            // TODO: for now we only support "raw" key type.
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "Key TYPE not supported");
                return key;
            }

            if(keyMode == KeyMode.Unprotected)
            {
                FetchFromRam(keyPointer, key, 0, 16);
            }
            else if(keyMode == KeyMode.Wrapped)
            {
                FetchFromRam(keyPointer + KeyRandomIvLength, key, 0, 16);
            }
            else if(keyMode == KeyMode.Volatile)
            {
                if(!volatileKeys.ContainsKey(keyIndex))
                {
                    parent.Log(LogLevel.Error, "Volatile key not found");
                    return key;
                }

                key = volatileKeys[keyIndex];
                parent.Log(LogLevel.Noisy, "CheckAndRetrieveKey (volatile): keyIndex={0} key=[{1}]", keyIndex, BitConverter.ToString(key));
            }
            else if(keyMode == KeyMode.Ksu)
            {
                if(!ksuStorage.ContainsKey(keyIndex))
                {
                    throw new NotSupportedException("KSU key not found");
                }

                key = ksuStorage.GetKey(keyIndex);
                parent.Log(LogLevel.Noisy, "CheckAndRetrieveKey (KSU): keyIndex={0} key=[{1}]", keyIndex, BitConverter.ToString(key));
            }

            return key;
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
            // [15]
            // noProtection = (keyMetadata & 0x8000) != 0;

            parent.Log(LogLevel.Noisy, "UnpackKeyMetadata(): keyMetadata=0x{0:X} index={1} type={2} mode={3} restriction={4}",
                     keyMetadata, index, type, mode, restriction);
        }

        private void UnpackDmaDescriptor(uint dmaPointer, out uint dataPointer, out uint dataSize, out DmaTransferOptions options, out uint nextDescriptorPtr, Machine machine)
        {
            if(dmaPointer == NullDescriptor)
            {
                throw new NotSupportedException("UnpackDmaDescriptor trying to read a Null descriptor");
            }

            dataPointer = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer);
            nextDescriptorPtr = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 4);
            var word2 = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 8);
            dataSize = word2 & 0xFFFFFFF;
            options = (DmaTransferOptions)(word2 >> 28);

            parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): dataPointer=0x{0:X} dataSize={1} options={2} nextDescriptorPtr=0x{3:X}",
                     dataPointer, dataSize, options, nextDescriptorPtr);

            if(dataSize > 0)
            {
                byte[] data = new byte[dataSize];
                FetchFromRam(dataPointer, data, 0, dataSize);
                parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): DATA=[{0}]", BitConverter.ToString(data));
            }
        }

        private void FetchFromRam(uint sourcePointer, byte[] destination, uint destinationOffset, uint length)
        {
            for(uint i = 0; i < length; i++)
            {
                destination[destinationOffset + i] = (byte)machine.SystemBus.ReadByte(sourcePointer + i);
            }
        }

        private void WriteToRam(byte[] source, uint sourceOffset, uint destinationPointer, uint length)
        {
            for(uint i = 0; i < length; i++)
            {
                machine.SystemBus.WriteByte(destinationPointer + i, source[sourceOffset + i]);
            }
        }

        private void UnpackRawKeyMetadata(uint keyMetadata, out uint keyLength)
        {
            // [14:0]
            keyLength = (keyMetadata & 0x7FFF);
        }

        /// Converts a BigInteger to a fixed-length byte array with proper zero-padding.
        /// This fixes the issue where ToByteArrayUnsigned() removes leading zeros.
        private byte[] BigIntegerToFixedByteArray(BigInteger bigInteger, int targetLength)
        {
            byte[] rawBytes = bigInteger.ToByteArrayUnsigned();
            byte[] paddedBytes = new byte[targetLength];

            // Copy raw bytes to the right side of the padded array (zero-padded on left)
            Array.Copy(rawBytes, 0, paddedBytes, targetLength - rawBytes.Length, rawBytes.Length);

            return paddedBytes;
        }

        private IDigest CreateHashEngine(ShaMode hashMode)
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

        private bool CheckHashEngine(IDigest hashEngine, ShaMode hashMode)
        {
            bool ret = false;

            switch(hashMode)
            {
            case ShaMode.Sha1:
                if(hashEngine is Sha1Digest)
                {
                    ret = true;
                }
                break;
            case ShaMode.Sha224:
                if(hashEngine is Sha224Digest)
                {
                    ret = true;
                }
                break;
            case ShaMode.Sha256:
                if(hashEngine is Sha256Digest)
                {
                    ret = true;
                }
                break;
            case ShaMode.Sha384:
                if(hashEngine is Sha384Digest)
                {
                    ret = true;
                }
                break;
            case ShaMode.Sha512:
                if(hashEngine is Sha512Digest)
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

        private bool IsTagSizeValid(uint tagSize)
        {
            // Tag size must be 0, 4, 6, 8, 10, 12, 14 or 16 bytes
            return (tagSize == 0 || tagSize == 4 || tagSize == 6 || tagSize == 8 || tagSize == 10 || tagSize == 12 || tagSize == 14 || tagSize == 16);
        }

        private bool IsNonceSizeValid(uint nonceSize)
        {
            return (nonceSize >= 8 && nonceSize <= 13);
        }

        private bool IsDataLengthValid(uint dataSize, CryptoMode cryptoMode)
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

        private uint GetTagLength(uint tagLengthOption)
        {
            uint tagLength;
            switch(tagLengthOption)
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

        private ECDomainParameters GetP256CurveParameters()
        {
            // NIST P-256 curve parameters
            var p = new BigInteger("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF", 16);
            var a = new BigInteger("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC", 16);
            var b = new BigInteger("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B", 16);
            var gx = new BigInteger("6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296", 16);
            var gy = new BigInteger("4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5", 16);
            var n = new BigInteger("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551", 16);

            var curve = new FpCurve(p, a, b, n, BigInteger.One);
            var g = curve.CreatePoint(gx, gy);
            return new ECDomainParameters(curve, g, n);
        }

        private BigInteger GenerateRandomScalar(ECDomainParameters curve, SecureRandom random)
        {
            BigInteger scalar;
            do
            {
                scalar = new BigInteger(curve.N.BitLength, random);
            }
            while(scalar.CompareTo(BigInteger.One) <= 0 || scalar.CompareTo(curve.N) >= 0);

            return scalar;
        }

        private SchnorrProof GenerateSchnorrProof(ECDomainParameters curve, BigInteger privateKey, ECPoint publicKey, byte[] userId, SecureRandom random, string tag, ECPoint basePoint = null)
        {
            // EC-JPAKE Schnorr proof: ZKP{x}: publicKey = x*basePoint
            // Following EC-JPAKE protocol: prove knowledge of x such that X = x·basePoint
            // For Round 1: basePoint = G (generator), custom tag
            // For Round 2: basePoint = B (combined point), tag = "JPAKE-A"

            // Default to generator point if no base point specified
            var actualBasePoint = basePoint ?? curve.G;

            // 1. v = random_scalar(1, n-1)
            var v = GenerateRandomScalar(curve, random);

            // 2. V = point_mul(basePoint, v) - commitment using specified base point
            var vPoint = actualBasePoint.Multiply(v).Normalize();

            // 3. c = H(encode(basePoint) || encode(V) || encode(publicKey) || encode(user_id) || tag) mod n
            var challenge = ComputeSchnorrChallenge(actualBasePoint, vPoint, publicKey, userId, tag);

            // 4. r = (v - c * privateKey) mod n
            var r = v.Subtract(challenge.Multiply(privateKey)).Mod(curve.N);

            // ZKP = { V, r }
            return new SchnorrProof { V = vPoint, R = r };
        }

        private BigInteger ComputeSchnorrChallenge(ECPoint basePoint, ECPoint vPoint, ECPoint publicKey, byte[] userId, string tag)
        {
            // EC-JPAKE challenge: c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)
            // This unified method handles both Round 1 (with custom tags) and Round 2 (with "JPAKE-A")
            var sha256 = new Sha256Digest();

            // Add encode(basePoint) - the base point (G for Round 1, combined point B for Round 2)
            var basePointBytes = EncodeECPointToBytes(basePoint);
            sha256.BlockUpdate(basePointBytes, 0, basePointBytes.Length);

            // Add encode(V) - the commitment point
            var vBytes = EncodeECPointToBytes(vPoint);
            sha256.BlockUpdate(vBytes, 0, vBytes.Length);

            // Add encode(publicKey) - the public key we're proving knowledge of
            var publicKeyBytes = EncodeECPointToBytes(publicKey);
            sha256.BlockUpdate(publicKeyBytes, 0, publicKeyBytes.Length);

            // Add user_id
            sha256.BlockUpdate(userId, 0, userId.Length);

            // Add tag (e.g., "JPAKE-X1", "JPAKE-X2", "JPAKE-A")
            var jpakeTag = System.Text.Encoding.UTF8.GetBytes(tag);
            sha256.BlockUpdate(jpakeTag, 0, jpakeTag.Length);

            var hash = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(hash, 0);

            // Convert to BigInteger and reduce modulo curve order
            var challenge = new BigInteger(1, hash);
            var curve = GetP256CurveParameters();
            return challenge.Mod(curve.N);
        }

        private byte[] EncodeECPointToBytes(ECPoint point)
        {
            // Encode as uncompressed format without the 0x04 prefix: x || y
            var xBytes = BigIntegerToFixedByteArray(point.AffineXCoord.ToBigInteger(), 32);
            var yBytes = BigIntegerToFixedByteArray(point.AffineYCoord.ToBigInteger(), 32);

            var result = new byte[64]; // 32 + 32 bytes for P-256
            Array.Copy(xBytes, 0, result, 0, 32);
            Array.Copy(yBytes, 0, result, 32, 32);

            return result;
        }

        private byte[] EncodeSchnorrProofToBytes(SchnorrProof proof)
        {
            // Encode as V || r where V is the EC point and r is the scalar
            var vBytes = EncodeECPointToBytes(proof.V);
            var rBytes = BigIntegerToFixedByteArray(proof.R, 32);

            var result = new byte[96]; // 64 + 32 bytes
            Array.Copy(vBytes, 0, result, 0, 64);
            Array.Copy(rBytes, 0, result, 64, 32);

            return result;
        }

        private ECPoint DecodeECPointFromBytes(byte[] pointBytes, ECDomainParameters curve)
        {
            if(pointBytes.Length != 64) // 32 bytes x + 32 bytes y for P-256
            {
                throw new ArgumentException("Invalid point byte length");
            }

            // Extract x and y coordinates
            byte[] xBytes = new byte[32];
            byte[] yBytes = new byte[32];
            Array.Copy(pointBytes, 0, xBytes, 0, 32);
            Array.Copy(pointBytes, 32, yBytes, 0, 32);

            var x = new BigInteger(1, xBytes);
            var y = new BigInteger(1, yBytes);

            // Create and validate the point
            var point = curve.Curve.CreatePoint(x, y);
            if(!point.IsValid())
            {
                throw new ArgumentException("Invalid EC point");
            }

            return point.Normalize();
        }

        private SchnorrProof DecodeSchnorrProofFromBytes(byte[] proofBytes, ECDomainParameters curve)
        {
            if(proofBytes.Length != 96) // 64 bytes for point V + 32 bytes for scalar r
            {
                throw new ArgumentException("Invalid proof byte length");
            }

            // Extract commitment point V (first 64 bytes)
            byte[] vBytes = new byte[64];
            Array.Copy(proofBytes, 0, vBytes, 0, 64);
            var vPoint = DecodeECPointFromBytes(vBytes, curve);

            // Extract response scalar r (last 32 bytes)
            byte[] rBytes = new byte[32];
            Array.Copy(proofBytes, 64, rBytes, 0, 32);
            var r = new BigInteger(1, rBytes);

            return new SchnorrProof { V = vPoint, R = r };
        }

        private bool VerifySchnorrProof(ECDomainParameters curve, ECPoint publicKey, SchnorrProof proof, byte[] userId, string tag, ECPoint basePoint = null)
        {
            try
            {
                // Default to generator point if no base point specified
                var actualBasePoint = basePoint ?? curve.G;

                // Schnorr proof verification: Check if V = r*basePoint + c*publicKey
                // where c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)

                // EC-JPAKE format: c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)
                BigInteger challenge = ComputeSchnorrChallenge(actualBasePoint, proof.V, publicKey, userId, tag);

                // Compute r*basePoint + c*publicKey
                var rBase = actualBasePoint.Multiply(proof.R);
                var cPublicKey = publicKey.Multiply(challenge);
                var verificationPoint = rBase.Add(cPublicKey).Normalize();

                // Check if V == r*basePoint + c*publicKey
                return proof.V.Equals(verificationPoint);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "VerifySchnorrProof: Exception during verification: {0}", e.Message);
                return false;
            }
        }

        private BigInteger ComputePasswordScalar(byte[] userId, byte[] password)
        {
            // EC-JPAKE standard password derivation:
            // s_bytes = H(user_id || password)
            // s = int(s_bytes) mod n; if s = 0 then s = 1

            var sha256 = new Sha256Digest();

            // Hash user_id || password
            sha256.BlockUpdate(userId, 0, userId.Length);
            sha256.BlockUpdate(password, 0, password.Length);

            var hash = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(hash, 0);

            // Convert hash to BigInteger and reduce modulo curve order
            var passwordBigInt = new BigInteger(1, hash);
            var curve = GetP256CurveParameters();
            var s = passwordBigInt.Mod(curve.N);

            // If s = 0 then s = 1 (ensure non-zero scalar)
            if(s.Equals(BigInteger.Zero))
            {
                s = BigInteger.One;
            }

            return s;
        }

        private uint GetInitialDataSpaceLength
        {
            get
            {
                return flashSize - flashDataRegionStart;
            }
        }

        // TODO: This is a HACK: the Bouncy Castle hash function implementation does not allow to
        // set the state, which is needed for HASH_UPDATE commands.
        // The solution is to keep around the hash engine until the HASH_FINAL command is called,
        // but this requires all these commands to happen sequentially, hence the hack.
        private IDigest currentHashEngine = null;
        private uint wordsLeftToBeReceived;
        private readonly bool series3;
        private readonly List<AsymmetricCipherKeyPair> ecKeyPairs = new List<AsymmetricCipherKeyPair>();
        private readonly Dictionary<uint, byte[]> volatileKeys = new Dictionary<uint, byte[]>();
        private readonly uint flashDataRegionStart;
        private readonly uint flashCodeRegionEnd;
        private readonly uint flashCodeRegionStart;
        private readonly uint flashRegionSize;
        private readonly uint flashPageSize;
        private readonly uint flashSize;
        private readonly Queue<uint> rxFifo;
        private readonly Queue<uint> txFifo;
        private readonly SiLabs_IKeyStorage ksuStorage;
        private readonly Machine machine;
        private readonly IDoubleWordPeripheral parent;
        private const uint NullDescriptor = 1;
        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        // Series3 specific value.
        // Related to PSEC-5391, once that gets resolved, we might need to update this accordingly.
        private const uint QspiFlashHostBase = 0x1000000;
        private const byte WrappedKeyRandomIVMarker = 0xAE;
        private const byte WrappedKeyAuthenticationTagMarker = 0xBE;
        private const uint KeyRandomIvLength = 12;
        private const uint KeyAuthenticationTagLength = 16;

        private class EccWeirstrassKeyMetadata
        {
            public bool HasPrivateKey { get; set; }

            public bool HasPublicKey { get; set; }

            public bool HasDomainParameters { get; set; }

            public EccWeirstrassPurpose Purpose { get; set; }

            public bool A0 { get; set; }

            public bool An3 { get; set; }

            public EccWeirstrassCurve SelectCurve { get; set; }

            public uint Size { get; set; }
        }

        // Data structure for Schnorr proof
        private class SchnorrProof
        {
            public ECPoint V { get; set; }  // Commitment point

            public BigInteger R { get; set; }  // Response scalar
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
            JPakeRound1                     = 0x0B00,
            JPakeRound2                     = 0x0B01,
            JPakeGenerateSessionKey         = 0x0B02,
            DiffieHellman                   = 0x0E00,
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

        private enum DmaTransferOptions
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
            Ksu                = 0x3,
        }

        private enum KeyType
        {
            Raw           = 0x0,
            EccWeirstrass = 0x8,
            EccEdwards    = 0xA,
            EccEddsa      = 0xB,
            Ed25519       = 0xC,
        }

        private enum EccWeirstrassCurve
        {
            CurveP192 = 0x0,
            CurveP256 = 0x1,
            CurveP384 = 0x2,
            CurveP521 = 0x3,
        }

        private enum EccWeirstrassKeySize
        {
            KeySizeCurveP192 = 24,
            KeySizeCurveP256 = 32,
            KeySizeCurveP384 = 48,
            KeySizeCurveP521 = 66,
        }

        private enum EccWeirstrassPurpose
        {
            KeyExchange = 0,
            Signature = 1,
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
    }
}