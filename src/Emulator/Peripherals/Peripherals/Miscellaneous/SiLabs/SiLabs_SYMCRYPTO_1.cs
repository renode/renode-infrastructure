//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.DMA;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_SYMCRYPTO_1 : SiLabsPeripheral
    {
        public SiLabs_SYMCRYPTO_1(Machine machine, SiLabs_IKeyStorage ksu = null) : base(machine)
        {
            this.ksu = ksu;
            dmaEngine = new DmaEngine(machine.GetSystemBus(this));
            IRQ = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();

            currentDigestEngine = null;
        }

        public GPIO IRQ { get; }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = ((fetcherEndOfBlockInterruptEnable.Value && fetcherEndOfBlockInterrupt.Value)
                           || (fetcherStoppedInterruptEnable.Value || fetcherStoppedInterrupt.Value)
                           || (fetcherErrorInterruptEnable.Value || fetcherErrorInterrupt.Value)
                           || (pusherEndOfBlockInterruptEnable.Value && pusherEndOfBlockInterrupt.Value)
                           || (pusherStoppedInterruptEnable.Value || pusherStoppedInterrupt.Value)
                           || (pusherErrorInterruptEnable.Value || pusherErrorInterrupt.Value));
                IRQ.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.FetcherAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out fetcherAddress, name: "ADDR")
                },
                {(long)Registers.FetcherLength, new DoubleWordRegister(this)
                    .WithValueField(0, 28, out fetcherLength, name: "LENGTH")
                    .WithFlag(28, out fetcherConstantAddress, name: "CONSTADDR")
                    .WithFlag(29, out fetcherRealignLength, name: "REALIGN")
                    .WithTaggedFlag("ZPADDING", 30)
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.FetcherTag, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out fetcherTag, name: "TAG")
                },
                {(long)Registers.PusherAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out pusherAddress, name: "ADDR")
                },
                {(long)Registers.PusherLength, new DoubleWordRegister(this)
                    .WithValueField(0, 28, out pusherLength, name: "LENGTH")
                    .WithFlag(28, out pusherConstantAddress, name: "CONSTADDR")
                    .WithFlag(29, out pusherRealignLength, name: "REALIGN")
                    .WithFlag(30, out pusherDiscardData, name: "DISCARD")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherEndOfBlockInterruptEnable, name: "FETCHERENDOFBLOCKIEN")
                    .WithFlag(1, out fetcherStoppedInterruptEnable, name: "FETCHERSTOPPEDIEN")
                    .WithFlag(2, out fetcherErrorInterruptEnable, name: "FETCHERERRORIEN")
                    .WithFlag(3, out pusherEndOfBlockInterruptEnable, name: "PUSHERENDOFBLOCKIEN")
                    .WithFlag(4, out pusherStoppedInterruptEnable, name: "PUSHERSTOPPEDIEN")
                    .WithFlag(5, out pusherErrorInterruptEnable, name: "PUSHERERRORIEN")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterruptEnable.Value = true; }, name: "FETCHERENDOFBLOCKIENSET")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterruptEnable.Value = true; }, name: "FETCHERSTOPPEDIENSET")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterruptEnable.Value = true; }, name: "FETCHERERRORIENSET")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterruptEnable.Value = true; }, name: "PUSHERENDOFBLOCKIENSET")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterruptEnable.Value = true; }, name: "PUSHERSTOPPEDIENSET")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterruptEnable.Value = true; }, name: "PUSHERERRORIENSET")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterruptEnable.Value = false; }, name: "FETCHERENDOFBLOCKIENCLR")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterruptEnable.Value = false; }, name: "FETCHERSTOPPEDIENCLR")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterruptEnable.Value = false; }, name: "FETCHERERRORIENCLR")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterruptEnable.Value = false; }, name: "PUSHERENDOFBLOCKIENCLR")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterruptEnable.Value = false; }, name: "PUSHERSTOPPEDIENCLR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterruptEnable.Value = false; }, name: "PUSHERERRORIENCLR")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherEndOfBlockInterrupt, FieldMode.Read, name: "FETCHERENDOFBLOCKIF")
                    .WithFlag(1, out fetcherStoppedInterrupt, FieldMode.Read, name: "FETCHERSTOPPEDIF")
                    .WithFlag(2, out fetcherErrorInterrupt, FieldMode.Read, name: "FETCHERERRORIF")
                    .WithFlag(3, out pusherEndOfBlockInterrupt, FieldMode.Read, name: "PUSHERENDOFBLOCKIF")
                    .WithFlag(4, out pusherStoppedInterrupt, FieldMode.Read, name: "PUSHERSTOPPEDIF")
                    .WithFlag(5, out pusherErrorInterrupt, FieldMode.Read, name: "PUSHERERRORIF")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlagMasked, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => (fetcherEndOfBlockInterrupt.Value && fetcherEndOfBlockInterruptEnable.Value), name: "FETCHERENDOFBLOCKIF")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => (fetcherStoppedInterrupt.Value && fetcherStoppedInterruptEnable.Value), name: "FETCHERSTOPPEDIF")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => (fetcherErrorInterrupt.Value && fetcherErrorInterruptEnable.Value), name: "FETCHERERRORIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => (pusherEndOfBlockInterrupt.Value && pusherEndOfBlockInterruptEnable.Value), name: "PUSHERENDOFBLOCKIF")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => (pusherStoppedInterrupt.Value && pusherStoppedInterruptEnable.Value), name: "PUSHERSTOPPEDIF")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => (pusherErrorInterrupt.Value && pusherErrorInterruptEnable.Value), name: "PUSHERERRORIF")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlagClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterrupt.Value = false; }, name: "FETCHERENDOFBLOCKIFCLR")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterrupt.Value = false; }, name: "FETCHERSTOPPEDIFCLR")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterrupt.Value = false; }, name: "FETCHERERRORIFCLR")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterrupt.Value = false; }, name: "PUSHERENDOFBLOCKIFCLR")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterrupt.Value = false; }, name: "PUSHERSTOPPEDIFCLR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterrupt.Value = false; }, name: "PUSHERERRORIFCLR")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherScatterGather, name: "FETCHERSCATTERGATHER")
                    .WithFlag(1, out pusherScatterGather, name: "PUSHERSCATTERGATHER")
                    .WithFlag(2, out fetcherStopAtEndOfBlock, name: "STOPFETCHER")
                    .WithFlag(3, out pusherStopAtEndOfBlock, name: "STOPPUSHER")
                    .WithTaggedFlag("SWRESET", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartFetcher(); } }, name: "STARTFETCHER")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartPusher(); } }, name: "STARTPUSHER")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherBusy, FieldMode.Read, name: "FETCHERBSY")
                    .WithFlag(1, out pusherBusy, FieldMode.Read, name: "PUSHERBSY")
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("NOTEMPTY", 4)
                    .WithTaggedFlag("WAITING", 5)
                    .WithTaggedFlag("SOFTRSTBSY", 6)
                    .WithReservedBits(7, 9)
                    .WithTag("FIFODATANUM", 16, 16)
                    //.WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => /* TODO */ 0, name: "FIFODATANUM")
                },
                {(long)Registers.IncludeIpsHardwareConfig, new DoubleWordRegister(this, 0x00000011)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "AES")
                    .WithTaggedFlag("AESGCM", 1)
                    .WithTaggedFlag("AESXTS", 2)
                    .WithTaggedFlag("DES", 3)
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "HASH")
                    .WithTaggedFlag("ChaChaPoly", 5)
                    .WithTaggedFlag("SHA3", 6)
                    .WithTaggedFlag("ZUC", 7)
                    .WithTaggedFlag("SM4", 8)
                    .WithTaggedFlag("PKE", 9)
                    .WithTaggedFlag("NDRNG", 10)
                    .WithTaggedFlag("HPChaChaPoly", 11)
                    .WithTaggedFlag("Snow3G", 12)
                    .WithTaggedFlag("Kasumi", 13)
                    .WithTaggedFlag("Aria", 14)
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.Ba411eHardwareConfig1, new DoubleWordRegister(this, 0x0F03017F)
                    .WithValueField(0, 9, FieldMode.Read, valueProviderCallback: _ => 0x17F, name: "AesModesPoss")
                    .WithReservedBits(9, 7)
                    .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "CS")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "UseMasking")
                    .WithReservedBits(18, 6)
                    .WithValueField(24, 3, FieldMode.Read, valueProviderCallback: _ => 0x7, name: "KeySize")
                    .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => true, name: "CxSwitch")
                    .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => false, name: "GlitchProtection")
                    .WithReservedBits(29, 3)
                },
                {(long)Registers.Ba413HardwareConfig, new DoubleWordRegister(this, 0x0013007F)
                    .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => 0x7F, name: "HashMaskFunc")
                    .WithReservedBits(7, 9)
                    .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "HashPadding")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "HMAC_enabled")
                    .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => false, name: "HashVerifyDigest")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 4, FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Ext_nb_Hash_keys")
                    .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ => 0x0, name: "IKG_nv_Hash_keys")
                    .WithReservedBits(28, 4)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        // Commmands
        private void StartFetcher()
        {
            // Scatter/Gather mode
            if(fetcherScatterGather.Value)
            {
                fetcherBusy.Value = true;
                pusherBusy.Value = true;

                this.Log(LogLevel.Noisy, "***** FETCHER Descriptor(s): *****");
                List<DmaDescriptor> fetcherDescriptorList = ParseDescriptors(true);
                this.Log(LogLevel.Noisy, "***** PUSHER Descriptor(s): *****");
                List<DmaDescriptor> pusherDescriptorList = ParseDescriptors(false);

                switch(fetcherDescriptorList[0].EngineSelect)
                {
                case CryptoEngine.Aes:
                    RunAesEngine(fetcherDescriptorList, pusherDescriptorList);
                    break;
                case CryptoEngine.Bypass:
                    RunBypassTransfer(fetcherDescriptorList, pusherDescriptorList);
                    break;
                case CryptoEngine.Hash:
                    RunHashEngine(fetcherDescriptorList, pusherDescriptorList);
                    break;
                default:
                    this.Log(LogLevel.Error, "EngineSelect={0}", fetcherDescriptorList[0].EngineSelect);
                    throw new NotSupportedException("START_FETCHER: crypto engine not supported");
                }

                fetcherBusy.Value = false;
                pusherBusy.Value = false;

                // TODO: we might need to set some interrupt flags here. However, sl_radioaes driver does not make use of them.
                UpdateInterrupts();
            }
            else // Direct mode
            {
                // TODO: handle CONSTADDR, REALIGN and ZPADDING flags.
                this.Log(LogLevel.Noisy, "Direct Mode: SrcAddress:0x{0:X} DestAddress=0x{1:X} length={2}", fetcherAddress.Value, pusherAddress.Value, fetcherLength.Value);

                DmaTransfer(fetcherAddress.Value, pusherAddress.Value, fetcherLength.Value);
            }
        }

        private void StartPusher()
        {
            // Nothing to do here, the StartFetcher command already grabs the pusher descriptors and writes them.
        }

        private void RunAesEngine(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            int configDescriptorIndex = -1;
            int keyDescriptorIndex = -1;
            int key2DescriptorIndex = -1;
            int ivDescriptorIndex = -1;
            int iv2DescriptorIndex = -1;
            int maskDescriptorIndex = -1;
            List<int> inputHeaderDescriptorIndices = new List<int>();
            List<int> inputPayloadDescriptorIndices = new List<int>();

            for(int i = 0; i < fetcherDescriptorList.Count; i++)
            {
                if(fetcherDescriptorList[i].IsData)
                {
                    switch(fetcherDescriptorList[i].DataType)
                    {
                    case CryptoDataType.Header:
                        inputHeaderDescriptorIndices.Add(i);
                        break;
                    case CryptoDataType.Payload:
                        inputPayloadDescriptorIndices.Add(i);
                        break;
                    }
                }
                else if(fetcherDescriptorList[i].IsConfig)
                {
                    switch((AesParameterOffset)fetcherDescriptorList[i].OffsetStartAddress)
                    {
                    case AesParameterOffset.Config:
                        configDescriptorIndex = i;
                        break;
                    case AesParameterOffset.Key:
                        keyDescriptorIndex = i;
                        break;
                    case AesParameterOffset.IV:
                        ivDescriptorIndex = i;
                        break;
                    case AesParameterOffset.IV2:
                        iv2DescriptorIndex = i;
                        break;
                    case AesParameterOffset.Key2:
                        key2DescriptorIndex = i;
                        break;
                    case AesParameterOffset.Mask:
                        maskDescriptorIndex = i;
                        break;
                    default:
                        throw new ArgumentException($"Unknown AesParameterOffset: {fetcherDescriptorList[i].OffsetStartAddress}");
                    }
                }
            }

            if(maskDescriptorIndex >= 0 && (configDescriptorIndex < 0 || inputPayloadDescriptorIndices.Count == 0))
            {
                // This command is reseeding the LFSR. We can just ignore the whole thing
                this.Log(LogLevel.Noisy, "RunAesEngine(): only maskDescriptor present: Setting the LFSR, gracefully ignore the command");
                return;
            }

            if(configDescriptorIndex < 0 || inputPayloadDescriptorIndices.Count == 0)
            {
                throw new ArgumentException("RunAesEngine(): one or more expected descriptors is missing");
            }

            int firstInputPayloadDescriptorIndex = inputPayloadDescriptorIndices[0];

            this.Log(LogLevel.Noisy, "RunAesEngine(): config_index={0} key_index={1} key2_index={2} iv_index={3} iv2_index={4} mask_index={5} FirstPayloadIndex={6}",
                     configDescriptorIndex, keyDescriptorIndex, key2DescriptorIndex, ivDescriptorIndex, iv2DescriptorIndex, maskDescriptorIndex, firstInputPayloadDescriptorIndex);

            // Bit 0: encryption or decryption operation
            // 0: encryption operation
            // 1: decryption operation
            bool encrypt = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0x1) == 0);
            // Bit 4: Cx_Load: 
            // 0: AES operation is initial; no contet is given as input
            // 1: AES operation is not initial; the context must be provided as input
            bool cxLoad = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & (0x1 << 4)) > 0);
            // Bit 5: Cx_Save: 
            // 0: AES operation is final; the engine will not return the context
            // 1: AES operation is not final; the engine will return the context
            bool cxSave = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & (0x1 << 4)) > 0);
            // Bits 7:6 and 31:28: KeySel
            // bits 7:6->1:0
            // bits 31:28->5:2
            uint keySelect = (uint)(((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0xC0) >> 6)
                                    | ((fetcherDescriptorList[configDescriptorIndex].Data[3] & 0xF0) >> 2));
            // From software examination, appears that key slots are passed in as 1-based indexes.
            keySelect -= 1;
            // 16:8 Mode of Operation
            AesMode mode = (AesMode)(fetcherDescriptorList[configDescriptorIndex].Data[1] | ((fetcherDescriptorList[configDescriptorIndex].Data[2] & 0x1) << 8));

            this.Log(LogLevel.Noisy, "RunAesEngine(): Config:[{0}]", BitConverter.ToString(fetcherDescriptorList[configDescriptorIndex].Data));
            this.Log(LogLevel.Noisy, "RunAesEngine(): encrypt={0} cxLoad={1} cxSave={2} keySel={3:X} AES mode: {4}", encrypt, cxLoad, cxSave, keySelect, mode);

            byte[] key;
            if(keyDescriptorIndex < 0)
            {
                this.Log(LogLevel.Noisy, "RunAesEngine(): fetching key from KSU at index {0}", keySelect);
                key = ksu.GetKey(keySelect);
            }
            else
            {
                key = fetcherDescriptorList[keyDescriptorIndex].Data;
            }

            KeyParameter keyParameter = new KeyParameter(key);
            this.Log(LogLevel.Noisy, "RunAesEngine(): key:[{0}]", BitConverter.ToString(key));

            ParametersWithIV parametersWithIV = null;
            if(ivDescriptorIndex >= 0)
            {
                parametersWithIV = new ParametersWithIV(keyParameter, fetcherDescriptorList[ivDescriptorIndex].Data);
                this.Log(LogLevel.Noisy, "RunAesEngine(): IV:[{0}]", BitConverter.ToString(fetcherDescriptorList[ivDescriptorIndex].Data));
            }

            this.Log(LogLevel.Noisy, "RunAesEngine(): First Input Data:[{0}]", BitConverter.ToString(fetcherDescriptorList[firstInputPayloadDescriptorIndex].Data));

            int outputTextDescriptorIndex = -1;
            int outputIvDescriptorIndex = -1;

            switch(mode)
            {
            case AesMode.Ecb:
            case AesMode.Ctr:
            {
                IBlockCipher cipher = null;

                if(mode == AesMode.Ecb)
                {
                    cipher = new AesEngine();
                    // The output IV length is always 0 when using ECB
                    cipher.Init(encrypt, keyParameter);
                    // In ECB mode we expect a single pusher descriptor which stores the output text
                    outputTextDescriptorIndex = 0;
                }
                else // AesMode.Ctr
                {
                    cipher = new SicBlockCipher(new AesEngine());
                    cipher.Init(encrypt, parametersWithIV);
                    outputTextDescriptorIndex = 0;
                    outputIvDescriptorIndex = 1;
                }

                // TODO: process all payload descriptors (also take into account "invalid bytes" field)
                for(int i = 0; i < fetcherDescriptorList[firstInputPayloadDescriptorIndex].Length; i += 16)
                {
                    cipher.ProcessBlock(fetcherDescriptorList[firstInputPayloadDescriptorIndex].Data, i, pusherDescriptorList[outputTextDescriptorIndex].Data, i);
                }

                for(uint i = 0; i < pusherDescriptorList[outputTextDescriptorIndex].Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[outputTextDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputTextDescriptorIndex].Data[i]);
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): Output Data:[{0}]", BitConverter.ToString(pusherDescriptorList[outputTextDescriptorIndex].Data));

                // TODO: RENODE-51: The crypto mode classes do not expose the IV (for example the SicBlockCipher class).
                // We should write the output IV here, for now we just do a +1 on each byte of the input IV.
                if(cxSave)
                {
                    for(uint i = 0; i < pusherDescriptorList[outputIvDescriptorIndex].Length; i++)
                    {
                        pusherDescriptorList[outputIvDescriptorIndex].Data[i] = (byte)(fetcherDescriptorList[ivDescriptorIndex].Data[i] + 1);
                        machine.SystemBus.WriteByte(pusherDescriptorList[outputIvDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputIvDescriptorIndex].Data[i]);
                    }
                    this.Log(LogLevel.Noisy, "RunAesEngine(): Output IV:[{0}]", BitConverter.ToString(pusherDescriptorList[outputIvDescriptorIndex].Data));
                }
                break;
            }
            case AesMode.Cmac:
            {
                CMac mac = new CMac(new AesEngine());
                mac.Init(keyParameter);
                outputTextDescriptorIndex = 0;
                // Process the input data for CMAC
                // TODO: process all payload descriptors (also take into account "invalid bytes" field)
                mac.BlockUpdate(fetcherDescriptorList[firstInputPayloadDescriptorIndex].Data, 0, fetcherDescriptorList[firstInputPayloadDescriptorIndex].Data.Length);
                byte[] macResult = new byte[mac.GetMacSize()];
                mac.DoFinal(macResult, 0);
                // Store the result in the pusher descriptor
                Array.Copy(macResult, 0, pusherDescriptorList[outputTextDescriptorIndex].Data, 0, macResult.Length);
                this.Log(LogLevel.Noisy, "RunAesEngine(): Output Data:[{0}]", BitConverter.ToString(pusherDescriptorList[outputTextDescriptorIndex].Data));
                // Write the result to the output descriptor
                for(uint i = 0; i < macResult.Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[outputTextDescriptorIndex].FirstDataAddress + i, macResult[i]);
                }
                break;
            }
            case AesMode.GcmGmac:
            {
                // TODO: For now we assume a single AAD descriptor (at index 2) and a single payload descriptor (at index 3).
                // Same assumption for output descriptors.
                int headerDescriptorIndex = 2;
                int payloadDescriptorIndex = 3;
                if(!fetcherDescriptorList[headerDescriptorIndex].IsData || fetcherDescriptorList[headerDescriptorIndex].DataType != CryptoDataType.Header)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC expected AAD descriptor");
                }
                if(!fetcherDescriptorList[payloadDescriptorIndex].IsData || fetcherDescriptorList[payloadDescriptorIndex].DataType != CryptoDataType.Payload)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC expected payload descriptor");
                }

                // Last data payload descriptor is a 16 byte descriptor containing len(A) || len(C)
                int lastFetcherIndex = fetcherDescriptorList.Count - 1;
                if(!fetcherDescriptorList[lastFetcherIndex].IsData || fetcherDescriptorList[lastFetcherIndex].DataType != CryptoDataType.Payload || fetcherDescriptorList[lastFetcherIndex].Data.Length != 16)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC requires last descriptor len(A)||len(C)");
                }
                byte[] reversedLastDescriptorData = new byte[fetcherDescriptorList[lastFetcherIndex].Data.Length];
                Array.Copy(fetcherDescriptorList[lastFetcherIndex].Data, reversedLastDescriptorData, fetcherDescriptorList[lastFetcherIndex].Data.Length);
                Array.Reverse(reversedLastDescriptorData);

                int tagLength = 16;
                int aadLength = (int)(BitConverter.ToUInt64(reversedLastDescriptorData, 8) / 8);
                int payloadLength = (int)(BitConverter.ToUInt64(reversedLastDescriptorData, 0) / 8);
                byte[] outputDataAndTag = new byte[payloadLength + tagLength];
                byte[] aad = new byte[aadLength];
                Array.Copy(fetcherDescriptorList[headerDescriptorIndex].Data, fetcherDescriptorList[headerDescriptorIndex].Data, aadLength);

                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GCMAC: aadLength={0} payloadLength={1} tagLength={2} aad=[{3}]", aadLength, payloadLength, tagLength, BitConverter.ToString(aad));

                if(aadLength != (fetcherDescriptorList[headerDescriptorIndex].Data.Length - fetcherDescriptorList[headerDescriptorIndex].InvalidBytesOrBits))
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC AAD length mismatch");
                }

                if(payloadLength != (fetcherDescriptorList[payloadDescriptorIndex].Data.Length - fetcherDescriptorList[payloadDescriptorIndex].InvalidBytesOrBits))
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC payload length mismatch");
                }

                GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                AeadParameters aeadParameters = new AeadParameters(keyParameter, tagLength*8, fetcherDescriptorList[ivDescriptorIndex].Data, aad);
                cipher.Init(encrypt, aeadParameters);

                int len = 0;
                len += cipher.ProcessBytes(fetcherDescriptorList[payloadDescriptorIndex].Data, 0, payloadLength, outputDataAndTag, len);

                // If decrypting, process as many extra zero bytes as the tag length.
                if(!encrypt)
                {
                    for(uint i = 0; i < tagLength; i++)
                    {
                        len += cipher.ProcessByte(0, outputDataAndTag, len);
                    }
                }

                // BA411E hardware does not verify the tag for GCM/GMAC mode.
                try
                {
                    len += cipher.DoFinal(outputDataAndTag, len);
                }
                catch(Exception e)
                {
                    if(!(e is InvalidCipherTextException))
                    {
                        throw e;
                    }
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GMAC length={0} outputDataAndTag=[{1}]", outputDataAndTag.Length, BitConverter.ToString(outputDataAndTag));

                // Encrypted/decrypted data is stored in the pusher descriptor #1 (assume single output payload descriptor).
                if(payloadLength != pusherDescriptorList[1].Data.Length)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC output payload descriptor length mismatch");
                }
                for(uint i = 0; i < payloadLength; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[1].FirstDataAddress + i, outputDataAndTag[i]);
                }

                // Tag is stored in the next pusher descriptor that is does not have the "discard" bit set.
                int tagDescriptorIndex = pusherDescriptorList[2].Discard ? 3 : 2;
                byte[] tag = cipher.GetMac();
                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GMAC tag=[{0}] tagDescriptor={1}", BitConverter.ToString(tag), tagDescriptorIndex);
                if(tag.Length != tagLength)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC tag length mismatch");
                }

                for(uint i = 0; i < tagLength && i < pusherDescriptorList[tagDescriptorIndex].Data.Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[tagDescriptorIndex].FirstDataAddress + i, tag[i]);
                }
                break;
            }
            case AesMode.Ccm:
            {
                if(inputHeaderDescriptorIndices.Count == 0)
                {
                    this.Log(LogLevel.Error, "RunAesEngine(): one or more expected descriptors is missing");
                    return;
                }

                byte[] headerData = BuildDataArray(inputHeaderDescriptorIndices, fetcherDescriptorList);
                this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Header Data ({0} bytes: [{1}])", headerData.Length, BitConverter.ToString(headerData));
                if(headerData.Length < 16)
                {
                    throw new ArgumentException($"RunAesEngine(): CCM header data too short: {headerData.Length} bytes, expected at least 16");
                }

                // Parse the header structure according to RFC 3610 and sx_aead_create_ccmheader:
                // Byte 0: Flags byte
                byte flags = headerData[0];
                bool hasAad = (flags & (1 << 6)) != 0;
                int l = (flags & 0x7) + 1; // Length field size
                int nonceSize = 15 - l; // Nonce size
                int tagLength = ((flags >> 3) & 0x7) * 2 + 2; // (t-2)/2 encoded in bits 5:3, so t = ((flags>>3)&0x7)*2 + 2

                this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Flags: 0x{0:X2}, hasAad: {1}, l: {2}, nonceSize: {3}, tagLength: {4}", flags, hasAad, l, nonceSize, tagLength);

                // Bytes 1 to (1+nonceSize): Nonce
                byte[] nonce = new byte[nonceSize];
                Array.Copy(headerData, 1, nonce, 0, nonceSize);

                // Bytes (1+nonceSize) to 15: Message length (big endian)
                uint messageLength = 0;
                for(int i = 0; i < l; i++)
                {
                    messageLength = (messageLength << 8) | headerData[1 + nonceSize + i];
                }

                // AAD length and data (if present)
                uint aadLength = 0;
                if(hasAad && headerData.Length >= 18)
                {
                    aadLength = (uint)((headerData[16] << 8) | headerData[17]);
                }

                // AAD data (if present) starts at byte 18 after the AAD length field
                byte[] aad = null;
                if(hasAad && aadLength > 0 && headerData.Length > 18)
                {
                    // AAD data starts at byte 18
                    int availableAadBytes = headerData.Length - 18;
                    int actualAadLength = Math.Min((int)aadLength, availableAadBytes);
                    aad = new byte[actualAadLength];
                    Array.Copy(headerData, 18, aad, 0, actualAadLength);
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Nonce: [{0}]", BitConverter.ToString(nonce));
                this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Message Length: {0}", messageLength);
                this.Log(LogLevel.Noisy, "RunAesEngine(): CCM AAD Length: {0}", aadLength);
                if(aad != null)
                {
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM AAD: [{0}]", BitConverter.ToString(aad));
                }

                CcmBlockCipher cipher = new CcmBlockCipher(new AesEngine());
                AeadParameters parameters = new AeadParameters(keyParameter,
                                                               tagLength * 8, // tag length in bits (inferred from flags byte)
                                                               nonce,
                                                               aad);
                cipher.Init(encrypt, parameters);

                byte[] finalOutput = null;

                if(encrypt)
                {
                    byte[] plaintextData = BuildDataArray(inputPayloadDescriptorIndices, fetcherDescriptorList);
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Plaintext Data ({0} bytes): [{1}]", plaintextData.Length, BitConverter.ToString(plaintextData));

                    // Create output buffer for ciphertext + tag
                    finalOutput = new byte[plaintextData.Length + tagLength];

                    // Process the plaintext
                    cipher.ProcessBytes(plaintextData, 0, plaintextData.Length, finalOutput, 0);

                    // Finalize the encryption (this writes out the encrypted data and the authentication tag)
                    cipher.DoFinal(finalOutput, 0);

                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Encrypted Data: [{0}]", BitConverter.ToString(finalOutput, 0, plaintextData.Length));
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Authentication Tag: [{0}]", BitConverter.ToString(finalOutput, plaintextData.Length, tagLength));
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM final output: [{0}]", BitConverter.ToString(finalOutput));
                }
                else
                {
                    byte[] inputData = BuildDataArray(inputPayloadDescriptorIndices, fetcherDescriptorList);
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Input Data (cipher text + Tag) ({0} bytes): [{1}]", inputData.Length, BitConverter.ToString(inputData));

                    finalOutput = new byte[inputData.Length];

                    // Output buffer is for plaintext only (input size - tag size)
                    int ciphertextLength = inputData.Length - tagLength;

                    // Process ciphertext and tag
                    cipher.ProcessBytes(inputData, 0, inputData.Length, finalOutput, 0);
                    byte[] tag = cipher.GetMac();

                    // Finalize the decryption (this verifies the authentication tag)
                    bool tagMatch = true;
                    try
                    {
                        cipher.DoFinal(finalOutput, 0);
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

                    // Copy the tag before DoFinal() which is XOR'd with the key (hence, all zeros when the tag matches)
                    Array.Copy(tag, 0, finalOutput, ciphertextLength, tagLength);

                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Decrypt TagMatch={0} Tag={{1}}", tagMatch, BitConverter.ToString(tag));
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM Decrypted Data: [{0}]", BitConverter.ToString(finalOutput, 0, ciphertextLength));
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CCM final output: [{0}]", BitConverter.ToString(finalOutput));
                }

                // Write finalOutput to memory
                uint writtenBytesCount = 0;
                int currentPusherDescriptorIndex = 0;
                while(writtenBytesCount < finalOutput.Length)
                {
                    if(pusherDescriptorList[currentPusherDescriptorIndex].Discard)
                    {
                        currentPusherDescriptorIndex++;
                        continue;
                    }

                    uint writeLength = (uint)Math.Min(pusherDescriptorList[currentPusherDescriptorIndex].Length, (finalOutput.Length - writtenBytesCount));
                    for(uint i = 0; i < writeLength; i++)
                    {
                        machine.SystemBus.WriteByte((pusherDescriptorList[currentPusherDescriptorIndex].FirstDataAddress + i),
                                                    finalOutput[writtenBytesCount + i]);
                    }

                    writtenBytesCount += writeLength;
                    currentPusherDescriptorIndex++;
                }
                break;
            }
            default:
            {
                throw new ArgumentException("RunAesEngine(): AES mode not supported");
            }
            }
        }

        private void RunHashEngine(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            int configDescriptorIndex = -1;

            for(int i = 0; i < fetcherDescriptorList.Count; i++)
            {
                if(fetcherDescriptorList[i].IsConfig)
                {
                    switch((HashParameterOffset)fetcherDescriptorList[i].OffsetStartAddress)
                    {
                    case HashParameterOffset.Config:
                        configDescriptorIndex = i;
                        break;
                    default:
                        throw new ArgumentException($"Unknown HashParameterOffset: {fetcherDescriptorList[i].OffsetStartAddress}");
                    }
                }
            }

            if(configDescriptorIndex < 0)
            {
                this.Log(LogLevel.Error, "RunHashEngine(): Missing config descriptor {0}", configDescriptorIndex);
                return;
            }

            this.Log(LogLevel.Noisy, "RunHashEngine(): config_index={0}", configDescriptorIndex);

            // Bit 6:0 Hash_mode
            HashMode mode = (HashMode)((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0x7F));
            // Bit 8: hmac
            // 0: Perform hash operation
            // 1: Perform hmac operation (requires Padding=1 and Hash_final=1)
            bool hmac = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & 0x1) > 0);
            // Bit 9: Padding
            // 0: padding is done in software
            // 1: padding is done in hardware (requires Hash_final=1, and cannot be used when using initialization data)
            bool padding = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 1)) > 0);
            // Bit 10: Hash_final
            // 0: hash operation is update. Engine wil return the state
            // 1: hash operation is final. Engine will return the digest
            bool hashFinal = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 2)) > 0);
            // Bit 11: Verify
            // 0: Generate HASH
            // 1: verify the input reference hash instead of generating a new one
            bool verify = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 3)) > 0);
            // Bits 27:26 and 31:28: KeySel
            // bits 27:26->5:4
            // bits 31:28->3:0
            uint keySelect = (uint)(((fetcherDescriptorList[configDescriptorIndex].Data[3] & 0x0C) << 2)
                                    | ((fetcherDescriptorList[configDescriptorIndex].Data[3] & 0xF0) >> 4));
            // From software examination, appears that key slots are passed in as 1-based indexes.
            keySelect -= 1;

            this.Log(LogLevel.Noisy, "RunHashEngine(): mode={0}, hmac={1}, padding={2}, hashFinal={3}, verify={4}, keySelect=0x{5:X}",
                     mode, hmac, padding, hashFinal, verify, keySelect);

            if(hmac)
            {
                this.Log(LogLevel.Error, "RunHashEngine(): HMAC not supported");
                return;
            }
            if(verify)
            {
                this.Log(LogLevel.Error, "RunHashEngine(): Verify not supported");
                return;
            }

            if(padding)
            {
                // TODO: unclear what padding should be done and if needed at all, the crypto library
                // might be doing it automatically.
            }

            // We assume all HASH update operations happen sequentially. The currentDigestEngine is set
            // back to null is hashFinal is true.
            if(currentDigestEngine == null)
            {
                currentDigestEngine = CreateHashEngine(mode);
            }
            else
            {
                // As sanity check we verify that the requested hash mode is the same as the current one.
                IDigest testEngine = CreateHashEngine(mode);
                if(testEngine.GetType() != currentDigestEngine.GetType())
                {
                    this.Log(LogLevel.Error, "RunHashEngine(): HASH update operation with different hash modes");
                    return;
                }
            }

            // DATA input descriptor with type "Message" are input payload. 
            // Note, for update operations an input descriptor of type "InitializationData" is passed in.
            // Since we are keeping the digest engine around, we can ignore that.
            for(int inputIndex = 0; inputIndex < fetcherDescriptorList.Count; inputIndex++)
            {
                if(fetcherDescriptorList[inputIndex].IsData)
                {
                    for(uint i = 0; i < fetcherDescriptorList[inputIndex].Length; i++)
                    {
                        currentDigestEngine.Update(fetcherDescriptorList[inputIndex].Data[i]);
                    }
                }
            }

            if(hashFinal)
            {
                if(pusherDescriptorList[0].Length != currentDigestEngine.GetDigestSize())
                {
                    this.Log(LogLevel.Error, "RunHashEngine(): output descriptor length does not match digest size: got {0}, want {1}", pusherDescriptorList[0].Length, currentDigestEngine.GetDigestSize());
                }

                currentDigestEngine.DoFinal(pusherDescriptorList[0].Data, 0);
                this.Log(LogLevel.Noisy, "RunHashEngine(): Output Data:[{0}]", BitConverter.ToString(pusherDescriptorList[0].Data));

                for(uint i = 0; i < pusherDescriptorList[0].Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[0].FirstDataAddress + i, pusherDescriptorList[0].Data[i]);
                }
                currentDigestEngine = null;
            }
            else
            {
                // We mark the oputput descriptor for non-final operations for debugging purposes. 
                // The hardware in this case would output the state of the digest engine, though
                // Bouncy Crypto library does not currently support that.
                for(uint i = 0; i < pusherDescriptorList[0].Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[0].FirstDataAddress + i, 0xF4);
                }
            }
        }

        private void RunBypassTransfer(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            this.Log(LogLevel.Noisy, "RunBypassTransfer(): fetcherListLength={0} pusherListLength={1}", fetcherDescriptorList.Count, pusherDescriptorList.Count);

            if(fetcherDescriptorList.Count == 0 || pusherDescriptorList.Count == 0)
            {
                throw new ArgumentException("RunBypassTransfer(): no fetcher or pusher descriptor");
            }

            int fetcherDescriptorIndex = 0;
            int pusherDescriptorIndex = 0;
            uint fetcherAddressOffset = 0;
            uint pusherAddressOffset = 0;

            while(fetcherDescriptorIndex < fetcherDescriptorList.Count && pusherDescriptorIndex < pusherDescriptorList.Count)
            {
                uint copyLength = Math.Min(fetcherDescriptorList[fetcherDescriptorIndex].Length - fetcherAddressOffset,
                                           pusherDescriptorList[pusherDescriptorIndex].Length - pusherAddressOffset);

                DmaTransfer(fetcherDescriptorList[fetcherDescriptorIndex].FirstDataAddress + fetcherAddressOffset,
                            pusherDescriptorList[pusherDescriptorIndex].FirstDataAddress + pusherAddressOffset,
                            copyLength);

                if(copyLength + fetcherAddressOffset < fetcherDescriptorList[fetcherDescriptorIndex].Length)
                {
                    fetcherAddressOffset += copyLength;
                }
                else
                {
                    fetcherAddressOffset = 0;
                    fetcherDescriptorIndex++;
                }

                if(copyLength + pusherAddressOffset < pusherDescriptorList[pusherDescriptorIndex].Length)
                {
                    pusherAddressOffset += copyLength;
                }
                else
                {
                    pusherAddressOffset = 0;
                    pusherDescriptorIndex++;
                }
            }
        }

        private List<DmaDescriptor> ParseDescriptors(bool parseFetcher)
        {
            List<DmaDescriptor> list = new List<DmaDescriptor>();
            uint currentDescriptorAddress = (uint)((parseFetcher) ? fetcherAddress.Value : pusherAddress.Value);
            bool moreDescriptors = true;
            uint descriptorIndex = 0;

            while(moreDescriptors)
            {
                DmaDescriptor descriptor = new DmaDescriptor();
                descriptor.FirstDataAddress = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress);
                descriptor.NextDescriptorAddress = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0x4);
                descriptor.LastDescriptor = ((descriptor.NextDescriptorAddress & 0x1) > 0);
                descriptor.NextDescriptorAddress &= ~0x3U;
                descriptor.Length = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0x8);
                descriptor.ConstantAddress = ((descriptor.Length & 0x10000000) > 0);
                descriptor.Realign = ((descriptor.Length & 0x20000000) > 0);
                if(parseFetcher)
                {
                    descriptor.ZeroPadding = ((descriptor.Length & 0x40000000) > 0);
                }
                else
                {
                    descriptor.Discard = ((descriptor.Length & 0x40000000) > 0);
                }
                descriptor.InterruptEnable = ((descriptor.Length & 0x80000000) > 0);
                descriptor.Length &= 0x0FFFFFFF;
                descriptor.Tag = (parseFetcher) ? machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0xC) : 0;
                descriptor.Data = new byte[descriptor.Length];
                for(uint i = 0; i < descriptor.Length; i++)
                {
                    // When the zero-padding bit is set, the fetcher generates zeroes instead of reading data from memory.
                    // For pusher, we always zero the data.
                    descriptor.Data[i] = (byte)((descriptor.ZeroPadding || !parseFetcher) ? 0 : machine.SystemBus.ReadByte(descriptor.FirstDataAddress + i));
                }
                list.Add(descriptor);

                this.Log(LogLevel.Noisy, "DESCRIPTOR {0}: Tag:{1:X} Length:{2} Last:{3} Const:{4} Realign:{5} ZeroPad:{6} Discard:{7} IE:{8} Data:[{9}]",
                         descriptorIndex, descriptor.Tag, descriptor.Length, descriptor.LastDescriptor, descriptor.ConstantAddress,
                         descriptor.Realign, descriptor.ZeroPadding, descriptor.Discard, descriptor.InterruptEnable, BitConverter.ToString(descriptor.Data));
                this.Log(LogLevel.Noisy, "COMMON: Engine:{0}, Last:{1}", descriptor.EngineSelect, descriptor.IsLastDataOrConfig);
                if(parseFetcher)
                {
                    if(descriptor.IsData)
                    {
                        this.Log(LogLevel.Noisy, "DATA: Type:{0}, Invalid Bytes/Bits:{1}", descriptor.DataType, descriptor.InvalidBytesOrBits);
                    }
                    else
                    {
                        this.Log(LogLevel.Noisy, "CONFIG: Offset:{0}", descriptor.OffsetStartAddress);
                    }
                }

                moreDescriptors = !descriptor.LastDescriptor;
                // TODO: handle the realign bit set here
                currentDescriptorAddress = descriptor.NextDescriptorAddress;
                descriptorIndex++;

                // In scatter/gather node, the hardware updates the fetchAddress/pushAddress after each processed
                // descriptor unless the constant address flag is set.
                if(((parseFetcher && fetcherScatterGather.Value) || (!parseFetcher && pusherScatterGather.Value))
                    && moreDescriptors
                    && !descriptor.ConstantAddress)
                {
                    if(parseFetcher)
                    {
                        fetcherAddress.Value = currentDescriptorAddress;
                    }
                    else
                    {
                        pusherAddress.Value = currentDescriptorAddress;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Builds a concatenated byte array from multiple descriptors.
        /// </summary>
        /// <param name="descriptorIndices">List of descriptor indices to concatenate</param>
        /// <param name="descriptorList">The list of fetcher descriptors to extract data from</param>
        /// <returns>A byte array containing all the data from the specified descriptors</returns>
        private byte[] BuildDataArray(List<int> descriptorIndices, List<DmaDescriptor> descriptorList)
        {
            if(descriptorIndices == null || descriptorIndices.Count == 0)
            {
                return null;
            }

            // Calculate total size needed (length - InvalidBytesOrBits for each descriptor)
            int totalSize = 0;
            foreach(int idx in descriptorIndices)
            {
                if(idx >= 0 && idx < descriptorList.Count)
                {
                    int validBytes = (int)(descriptorList[idx].Length - (descriptorList[idx].Length % 16 == 0 ? descriptorList[idx].InvalidBytesOrBits : 0));
                    totalSize += Math.Max(0, validBytes); // Ensure non-negative
                }
            }

            // Build the concatenated array
            byte[] result = new byte[totalSize];
            int offset = 0;

            foreach(int idx in descriptorIndices)
            {
                if(idx >= 0 && idx < descriptorList.Count)
                {
                    byte[] data = descriptorList[idx].Data;
                    int validBytes = (int)(descriptorList[idx].Length - (descriptorList[idx].Length % 16 == 0 ? descriptorList[idx].InvalidBytesOrBits : 0));
                    validBytes = Math.Max(0, Math.Min(validBytes, data.Length)); // Ensure bounds checking

                    if(validBytes > 0)
                    {
                        Array.Copy(data, 0, result, offset, validBytes);
                        offset += validBytes;
                    }
                }
            }

            return result;
        }

        private void DmaTransfer(ulong sourceAddress, ulong destinationAddress, ulong length)
        {
            var request = new Request(
                    source: new Place(sourceAddress),
                    destination: new Place(destinationAddress),
                    size: (int)length,
                    readTransferType: TransferType.Byte,
                    writeTransferType: TransferType.Byte
                    );
            dmaEngine.IssueCopy(request);
        }

        private IDigest CreateHashEngine(HashMode hashMode)
        {
            IDigest ret = null;
            switch(hashMode)
            {
            case HashMode.Md5:
                ret = new MD5Digest();
                break;
            case HashMode.Sha_1:
                ret = new Sha1Digest();
                break;
            case HashMode.Sha_224:
                ret = new Sha224Digest();
                break;
            case HashMode.Sha_256:
                ret = new Sha256Digest();
                break;
            case HashMode.Sha_384:
                ret = new Sha384Digest();
                break;
            case HashMode.Sha_512:
                ret = new Sha512Digest();
                break;
            case HashMode.Sm3:
                ret = new SM3Digest();
                break;
            default:
                this.Log(LogLevel.Error, "CreateHashEngine(): invalid hash mode");
                break;
            }
            return ret;
        }

        private IFlagRegisterField fetcherErrorInterruptEnable;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField pusherStoppedInterrupt;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField pusherEndOfBlockInterrupt;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField fetcherErrorInterrupt;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField fetcherStoppedInterrupt;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField fetcherEndOfBlockInterrupt;
        private IFlagRegisterField pusherErrorInterruptEnable;
        private IFlagRegisterField pusherStoppedInterruptEnable;
        private IFlagRegisterField pusherEndOfBlockInterruptEnable;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField pusherErrorInterrupt;
        private IFlagRegisterField fetcherStoppedInterruptEnable;
        // This bit is high as long as the pusher is busy
        private IFlagRegisterField pusherBusy;
        // The Bouncy Castle hash function implementation does not allow to
        // set the state, which is needed for HASH_UPDATE commands.
        // The solution is to keep around the hash engine until the HASH_FINAL command is called,
        // but this requires all these commands to happen sequentially.
        private IDigest currentDigestEngine = null;
        // Direct mode: written by SW (address of the first data)
        // Scatter/gather mode: Written by SW (address of first descriptor). Afterwards, it is updated 
        //                      by the hardware after each processed descriptor.
        private IValueRegisterField fetcherAddress;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField fetcherLength;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  fetcherConstantAddress;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  fetcherRealignLength;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField fetcherTag;
        // When this bit is high, the fetcher will stop at the end of the current block 
        // (even if the STOP bit in the descriptor is low).        
        private IFlagRegisterField  fetcherStopAtEndOfBlock;

        // Interrupt flags
        private IFlagRegisterField fetcherEndOfBlockInterruptEnable;
        // When this bit is zero, the fetcher runs in direct mode. 
        // When this bit is one, the fetcher runs in scatter-gather mode.
        private IFlagRegisterField  fetcherScatterGather;
        // Direct mode: written by SW (address of the first data)
        // Scatter/gather mode: Written by SW (address of first descriptor). Afterwards, it is updated 
        //                      by the hardware after each processed descriptor.
        private IValueRegisterField pusherAddress;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField pusherLength;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherConstantAddress;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherRealignLength;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherDiscardData;
        // When this bit is high, the pusher will stop at the end of the current block 
        // (even if the STOP bit in the descriptor is low).
        private IFlagRegisterField  pusherStopAtEndOfBlock;
        // When this bit is zero, the pusher runs in direct mode. 
        // When this bit is one, the pusher runs in scatter-gather mode.
        private IFlagRegisterField  pusherScatterGather;
        // This bit is high as long as the fetcher is busy
        private IFlagRegisterField fetcherBusy;

        private readonly SiLabs_IKeyStorage ksu;
        private readonly DmaEngine dmaEngine;

        private class DmaDescriptor
        {
            public DmaDescriptor()
            {
            }

            public CryptoEngine EngineSelect
            {
                get => (CryptoEngine)(this.Tag & 0xF);
            }

            public bool IsData
            {
                get => ((this.Tag & 0x10) == 0);
            }

            public bool IsConfig
            {
                get => !this.IsData;
            }

            public bool IsLastDataOrConfig
            {
                get => ((this.Tag & 0x20) > 0);
            }

            public uint InvalidBytesOrBits
            {
                // Bits 13:8
                get => IsData ? ((this.Tag & 0xCF00) >> 8) : 0;
            }

            public uint OffsetStartAddress
            {
                // Bits 15:8
                get => IsData ? 0 : ((this.Tag & 0xFF00) >> 8);
            }

            public CryptoDataType DataType
            {
                get
                {
                    if(!IsData)
                    {
                        return CryptoDataType.Unused;
                    }

                    // Bits 7:6
                    uint dataType = ((this.Tag & 0xC0) >> 6);
                    switch(this.EngineSelect)
                    {
                    case CryptoEngine.Aes:
                    case CryptoEngine.Sm4:
                    case CryptoEngine.Aria:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        }
                        break;
                    case CryptoEngine.Hash:
                    case CryptoEngine.Sha3:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Message;
                        case 1:
                            return CryptoDataType.InitializationData;
                        case 2:
                            return CryptoDataType.HMAC_Key;
                        case 3:
                            return CryptoDataType.ReferenceHash;
                        }
                        break;
                    case CryptoEngine.AesGcm:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        case 3:
                            return CryptoDataType.ReferenceTag;
                        }
                        break;
                    case CryptoEngine.ChaChaPoly:
                    case CryptoEngine.HpChaChaPoly:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        case 3:
                            return CryptoDataType.ReferenceDigest;
                        }
                        break;
                    }
                    return CryptoDataType.Unused;
                }
            }

            public uint FirstDataAddress;
            public bool LastDescriptor;
            public uint NextDescriptorAddress;
            public uint Length;
            public bool ConstantAddress;
            public bool Realign;
            public bool Discard;
            public bool ZeroPadding;
            public bool InterruptEnable;
            public uint Tag;
            public byte[] Data;
        }

        private enum AesMode
        {
            Ecb     = 0x001,
            Ccb     = 0x002,
            Ctr     = 0x004,
            Cfb     = 0x008,
            Ofb     = 0x010,
            Ccm     = 0x020,
            GcmGmac = 0x040,
            Xts     = 0x080,
            Cmac    = 0x100,
        }

        private enum AesParameterOffset : uint
        {
            Config = 0x00,
            Key    = 0x08,
            IV     = 0x28,
            IV2    = 0x38,
            Key2   = 0x48,
            Mask   = 0x68,
        }

        private enum HashMode : uint
        {
            Md5     = 0x01,
            Sha_1   = 0x02,
            Sha_224 = 0x04,
            Sha_256 = 0x08,
            Sha_384 = 0x10,
            Sha_512 = 0x20,
            Sm3     = 0x40,
        }

        private enum HashParameterOffset : uint
        {
            Config = 0x00,
        }

        private enum Registers
        {
            FetcherAddress              = 0x000,
            FetcherAddressMsb           = 0x004,
            FetcherLength               = 0x008,
            FetcherTag                  = 0x00C,
            PusherAddress               = 0x010,
            PusherAddressMsb            = 0x014,
            PusherLength                = 0x018,
            InterruptEnable             = 0x01C,
            InterruptEnableSet          = 0x020,
            InterruptEnableClear        = 0x024,
            InterruptFlag               = 0x028,
            InterruptFlagMasked         = 0x02C,
            InterruptFlagClear          = 0x030,
            Control                     = 0x034,
            Command                     = 0x038,
            Status                      = 0x03C,
            IncludeIpsHardwareConfig    = 0x400,
            Ba411eHardwareConfig1       = 0x404,
            Ba411eHardwareConfig2       = 0x408,
            Ba413HardwareConfig         = 0x40C,
            Ba418HardwareConfig         = 0x410,
            Ba419HardwareConfig         = 0x414,
            Ba424HardwareConfig         = 0x418,
        }

        private enum CryptoEngine
        {
            Bypass       = 0x0,
            Aes          = 0x1,
            Des          = 0x2,
            Hash         = 0x3,
            ChaChaPoly   = 0x4,
            Sha3         = 0x5,
            AesGcm       = 0x6,
            AesXts       = 0x7,
            HashPlusAes  = 0x8,
            AesPlusHash  = 0x9,
            Zuc          = 0xA,
            Sm4          = 0xB,
            HpChaChaPoly = 0xC,
            Snow3g       = 0xD,
            Kasumi       = 0xE,
            Aria         = 0xF,
        }

        private enum CryptoDataType
        {
            Unused,
            Payload,
            Header,
            Message,
            InitializationData,
            HMAC_Key,
            ReferenceHash,
            ReferenceTag,
            ReferenceDigest,
        }
    }
}