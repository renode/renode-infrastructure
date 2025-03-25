//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class EFR32xG2_AES_1 : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_AES_1(Machine machine)
        {
            this.machine = machine;

            IRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            var result = 0U;
            if(!registersCollection.TryRead(offset, out result))
            {
                this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", offset, (Registers)offset);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", offset, (Registers)offset, result);
            }
            return result;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
            if(!registersCollection.TryWrite(offset, value))
            {
                this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
                return;
            }
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
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
                    .WithReservedBits(30, 2)
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
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
#region register fields
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
        // When this bit is zero, the fetcher runs in direct mode. 
        // When this bit is one, the fetcher runs in scatter-gather mode.
        private IFlagRegisterField  fetcherScatterGather;
        // This bit is high as long as the fetcher is busy
        private IFlagRegisterField fetcherBusy;
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
        // This bit is high as long as the pusher is busy
        private IFlagRegisterField pusherBusy;

        // Interrupt flags
        private IFlagRegisterField fetcherEndOfBlockInterruptEnable;
        private IFlagRegisterField fetcherStoppedInterruptEnable;
        private IFlagRegisterField fetcherErrorInterruptEnable;
        private IFlagRegisterField pusherEndOfBlockInterruptEnable;
        private IFlagRegisterField pusherStoppedInterruptEnable;
        private IFlagRegisterField pusherErrorInterruptEnable;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField fetcherEndOfBlockInterrupt;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField fetcherStoppedInterrupt;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField fetcherErrorInterrupt;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField pusherEndOfBlockInterrupt;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField pusherStoppedInterrupt;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField pusherErrorInterrupt;
#endregion

#region methods
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((fetcherEndOfBlockInterruptEnable.Value && fetcherEndOfBlockInterrupt.Value)
                           || (fetcherStoppedInterruptEnable.Value || fetcherStoppedInterrupt.Value)
                           || (fetcherErrorInterruptEnable.Value || fetcherErrorInterrupt.Value)
                           || (pusherEndOfBlockInterruptEnable.Value && pusherEndOfBlockInterrupt.Value)
                           || (pusherStoppedInterruptEnable.Value || pusherStoppedInterrupt.Value)
                           || (pusherErrorInterruptEnable.Value || pusherErrorInterrupt.Value));
                IRQ.Set(irq);
            });
        }

        // Commmands
        private void StartFetcher()
        {
            // TODO: for now we support only Scatter/Gather mode for both fetcher and pusher
            if (!fetcherScatterGather.Value || !pusherScatterGather.Value)
            {
                this.Log(LogLevel.Error, "START_FETCHER: direct mode not supported");
                return;
            }

            fetcherBusy.Value = true;
            pusherBusy.Value = true;

            this.Log(LogLevel.Noisy, "FETCHER Descriptor(s):");
            List<DmaDescriptor> fetcherDescriptorList = ParseDescriptors(true);
            this.Log(LogLevel.Noisy, "PUSHER Descriptor(s):");
            List<DmaDescriptor> pusherDescriptorList = ParseDescriptors(false);

            switch(fetcherDescriptorList[0].EngineSelect)
            {
                case CryptoEngine.Aes:
                    RunAesEngine(fetcherDescriptorList, pusherDescriptorList);
                break;
                default:
                    this.Log(LogLevel.Error, "START_FETCHER: crypto engine not supported");
                    return;
            }

            fetcherBusy.Value = false;
            pusherBusy.Value = false;

            // TODO: we might need to set some interrupt flags here. However, sl_radioaes driver does not make use of them.

            UpdateInterrupts();
        }
        
        private void StartPusher()
        {
            // Nothing to do here, the StartFetcher command already grabs the pusher descriptors and writes them.
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
                if (parseFetcher)
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
                for(uint i=0; i<descriptor.Length; i++)
                {
                    // When the zero-padding bit is set, the fetcher generates zeroes instead of reading data from memory.
                    // For pusher, we always zero the data.
                    descriptor.Data[i] = (byte)((descriptor.ZeroPadding || !parseFetcher) ? 0 : machine.SystemBus.ReadByte(descriptor.FirstDataAddress + i));
                }
                list.Add(descriptor);

                this.Log(LogLevel.Noisy, "DESCRIPTOR {0}: Tag:{1:X} Length:{2} Last:{3} Const:{4} Realign:{5} ZeroPad:{6} IE:{7} Data:[{8}]",
                         descriptorIndex, descriptor.Tag, descriptor.Length, descriptor.LastDescriptor, descriptor.ConstantAddress, 
                         descriptor.Realign, descriptor.ZeroPadding, descriptor.InterruptEnable, BitConverter.ToString(descriptor.Data));
                this.Log(LogLevel.Noisy, "COMMON: Engine:{0}, Last:{1}", descriptor.EngineSelect, descriptor.IsLastDataOrConfig);
                if (parseFetcher)
                {
                    if (descriptor.IsData)
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

                // In scatter/gather node, the hardware updates the fetchAddress after each processed descriptor
                // unless the constant address flag is set.
                if (parseFetcher && fetcherScatterGather.Value && moreDescriptors && !descriptor.ConstantAddress)
                {
                    fetcherAddress.Value = currentDescriptorAddress;
                }
            }
            return list;
        }

        void RunAesEngine(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            int configDescriptorIndex = -1;
            int keyDescriptorIndex = -1;
            int key2DescriptorIndex = -1;
            int inputTextDescriptorIndex = -1;
            int ivDescriptorIndex = -1;
            int iv2DescriptorIndex = -1;
            for(int i=0; i<fetcherDescriptorList.Count; i++)
            {
                if (fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.Payload)
                {
                    inputTextDescriptorIndex = i;
                }
                else if (fetcherDescriptorList[i].IsConfig && fetcherDescriptorList[i].OffsetStartAddress == 0x8)
                {
                    keyDescriptorIndex = i;
                }
                else if (fetcherDescriptorList[i].IsConfig && fetcherDescriptorList[i].OffsetStartAddress == 0x0)
                {
                    configDescriptorIndex = i;
                }
                else if (fetcherDescriptorList[i].IsConfig && fetcherDescriptorList[i].OffsetStartAddress == 0x28)
                {
                    ivDescriptorIndex = i;
                }
                else if (fetcherDescriptorList[i].IsConfig && fetcherDescriptorList[i].OffsetStartAddress == 0x38)
                {
                    iv2DescriptorIndex = i;
                }
                else if (fetcherDescriptorList[i].IsConfig && fetcherDescriptorList[i].OffsetStartAddress == 0x48)
                {
                    key2DescriptorIndex = i;
                }
            }
            
            this.Log(LogLevel.Noisy, "RunAesEngine(): config_index={0} key_index={1} key2_index={2} iv_index={3} iv2_index={4} plaintextIndex={5}", 
                     configDescriptorIndex, keyDescriptorIndex, key2DescriptorIndex, ivDescriptorIndex, iv2DescriptorIndex, inputTextDescriptorIndex);    

            if (keyDescriptorIndex < 0 || configDescriptorIndex < 0 || inputTextDescriptorIndex < 0)
            {
                this.Log(LogLevel.Error, "RunAesEngine(): one or more expected descriptors is missing");
                return;
            }

            this.Log(LogLevel.Noisy, "RunAesEngine(): Config:[{0}]", BitConverter.ToString(fetcherDescriptorList[configDescriptorIndex].Data));

            IBlockCipher cipher = null;
            KeyParameter keyParameter = new KeyParameter(fetcherDescriptorList[keyDescriptorIndex].Data);
            this.Log(LogLevel.Noisy, "RunAesEngine(): key:[{0}]", BitConverter.ToString(fetcherDescriptorList[keyDescriptorIndex].Data));

            ParametersWithIV parametersWithIV = null;
            if (ivDescriptorIndex >= 0)
            {
                parametersWithIV = new ParametersWithIV(keyParameter, fetcherDescriptorList[ivDescriptorIndex].Data);
                this.Log(LogLevel.Noisy, "RunAesEngine(): IV:[{0}]", BitConverter.ToString(fetcherDescriptorList[ivDescriptorIndex].Data));
            }

            this.Log(LogLevel.Noisy, "RunAesEngine(): Input Data:[{0}]", BitConverter.ToString(fetcherDescriptorList[inputTextDescriptorIndex].Data));
            
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
            // 16:8 Mode of Operation
            AesMode mode = (AesMode)(fetcherDescriptorList[configDescriptorIndex].Data[1] | ((fetcherDescriptorList[configDescriptorIndex].Data[2] & 0x1) << 8));
            this.Log(LogLevel.Noisy, "RunAesEngine(): encrypt={0} cxLoad={1} cxSave={2} AES mode: {3}", encrypt, cxLoad, cxSave, mode);

            int outputTextDescriptorIndex = -1;
            int outputIvDescriptorIndex = -1;

            switch(mode)
            {
                case AesMode.Ecb:
                    cipher = new AesEngine();
                    // The output IV length is always 0 when using ECB
                    cipher.Init(encrypt, keyParameter);                
                    // In ECB mode we expect a single pusher descriptor which stores the ouput text
                    outputTextDescriptorIndex = 0;
                break;
                case AesMode.Ctr:
                    cipher = new SicBlockCipher(new AesEngine());
                    cipher.Init(encrypt, parametersWithIV);
                    outputTextDescriptorIndex = 0;
                    outputIvDescriptorIndex = 1;
                break;
                default:
                    this.Log(LogLevel.Error, "RunAesEngine(): AES mode not supported");
                    return;
            }

            for(int i = 0; i < fetcherDescriptorList[inputTextDescriptorIndex].Length; i += 16)
            {
                cipher.ProcessBlock(fetcherDescriptorList[inputTextDescriptorIndex].Data, i, pusherDescriptorList[outputTextDescriptorIndex].Data, i);
            }

            for (uint i=0; i<pusherDescriptorList[outputTextDescriptorIndex].Length; i++)
            {
                machine.SystemBus.WriteByte(pusherDescriptorList[outputTextDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputTextDescriptorIndex].Data[i]);
            }

            this.Log(LogLevel.Noisy, "RunAesEngine(): Output Data:[{0}]", BitConverter.ToString(pusherDescriptorList[outputTextDescriptorIndex].Data));

            // TODO: RENODE-51: The crypto mode classes do not expose the IV (for example the SicBlockCipher class).
            // We should write the output IV here, for now we just do a +1 on each byte of the input IV.
            if (cxSave)
            {
                for (uint i=0; i<pusherDescriptorList[outputIvDescriptorIndex].Length; i++)
                {
                    pusherDescriptorList[outputIvDescriptorIndex].Data[i] = (byte)(fetcherDescriptorList[ivDescriptorIndex].Data[i] + 1);
                    machine.SystemBus.WriteByte(pusherDescriptorList[outputIvDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputIvDescriptorIndex].Data[i]);
                }
                this.Log(LogLevel.Noisy, "RunAesEngine(): Output IV:[{0}]", BitConverter.ToString(pusherDescriptorList[outputIvDescriptorIndex].Data));
            }
        }
#endregion

#region enums
        private enum AesMode
        {
            Ecb           = 0x001,
            Ccb           = 0x002,
            Ctr           = 0x004,
            Cfb           = 0x008,
            Ofb           = 0x010,
            Ccm           = 0x020,
            GcmGmac       = 0x040,
            Xts           = 0x080,
            Cmac          = 0x100,
        }
        private enum Registers
        {
            FetcherAddress              = 0x000,
            FetcherLength               = 0x008,
            FetcherTag                  = 0x00C,
            PusherAddress               = 0x010,
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
#endregion

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
                    if (!IsData)
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
    }
}