//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;

using Antmicro.Renode.Peripherals.Bus;

using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SD
{
    public class LiteSDCard : NullRegistrationPointPeripheralContainer<SDCard>, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteSDCard(Machine machine) : base(machine)
        {
            phyRegistersCollection = new DoubleWordRegisterCollection(this);
            coreRegistersCollection = new DoubleWordRegisterCollection(this);
            readerRegistersCollection = new DoubleWordRegisterCollection(this);
            writerRegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();

            responseBuffer = new byte[ResponseBufferLength];
        }

        public override void Reset()
        {
            phyRegistersCollection.Reset();
            coreRegistersCollection.Reset();
            readerRegistersCollection.Reset();
            writerRegistersCollection.Reset();

            argumentValue = 0;
            blockSize = 0;
            blockCount = 0;

            readerAddress = 0;
            writerAddress = 0;

            readerLength = 0;
            writerLength = 0;

            Array.Clear(responseBuffer, 0, responseBuffer.Length);
        }

        public uint ReadDoubleWord(long offset)
        {
            return phyRegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            phyRegistersCollection.Write(offset, value);
        }

        [ConnectionRegionAttribute("core")]
        public uint ReadDoubleWordFromCore(long offset)
        {
            return coreRegistersCollection.Read(offset);
        }

        [ConnectionRegionAttribute("core")]
        public void WriteDoubleWordToCore(long offset, uint value)
        {
            coreRegistersCollection.Write(offset, value);
        }

        [ConnectionRegionAttribute("reader")]
        public uint ReadDoubleWordFromReader(long offset)
        {
            return readerRegistersCollection.Read(offset);
        }

        [ConnectionRegionAttribute("reader")]
        public void WriteDoubleWordToReader(long offset, uint value)
        {
            readerRegistersCollection.Write(offset, value);
        }

        [ConnectionRegionAttribute("writer")]
        public uint ReadDoubleWordFromWriter(long offset)
        {
            return writerRegistersCollection.Read(offset);
        }

        [ConnectionRegionAttribute("writer")]
        public void WriteDoubleWordToWriter(long offset, uint value)
        {
            writerRegistersCollection.Write(offset, value);
        }

        public long Size => 0x200;

        public DoubleWordRegisterCollection RegistersCollection => phyRegistersCollection;

        private void DefineRegisters()
        {
            PhyRegisters.CardDetect.Define(this)
                // note: `true` means *no* card
                .WithFlag(0, FieldMode.Read, name: "card_detect", valueProviderCallback: _ => RegisteredPeripheral == null)
                .WithReservedBits(1, 31);

            CoreRegisters.Argument.DefineMany(coreRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"argument{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref argumentValue, width: 8, source: val, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            CoreRegisters.Command0.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.Command1.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.Command2.Define(coreRegistersCollection)
                .WithValueField(0, 7, out commandIndexField, name: "command_index")
                .WithReservedBits(7, 1)
                .WithIgnoredBits(8, 24);

            CoreRegisters.Command3.Define(coreRegistersCollection)
                .WithEnumField<DoubleWordRegister, ResponseType>(0, 3, out responseTypeField, name: "respone_type")
                .WithReservedBits(3, 2)
                .WithEnumField<DoubleWordRegister, TransferType>(5, 3, out transferTypeField, name: "transfer_type")
                .WithIgnoredBits(8, 24);

            CoreRegisters.IssueCommand.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "issue_command", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        // we are only interested in `true`
                        return;
                    }

                    if(RegisteredPeripheral == null)
                    {
                        this.Log(LogLevel.Warning, "Issued a 0x{0:X} command with 0x{1:X} argument, but there is no SD card attached", commandIndexField.Value, argumentValue);
                        return;
                    }

                    this.Log(LogLevel.Noisy, "Issuing command #{0} with argument 0x{3:X}, transfer type is {1}, response type is {2}", commandIndexField.Value, transferTypeField.Value, responseTypeField.Value, argumentValue);

                    var resp = RegisteredPeripheral.HandleCommand(commandIndexField.Value, argumentValue).AsByteArray();

                    this.Log(LogLevel.Noisy, "Received response of size {0}", resp.Length);
#if DEBUG_PACKETS
                    this.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(resp));
#endif

                    var expectedResponseLength = 0;

                    switch(responseTypeField.Value)
                    {
                        case ResponseType.Short:
                            expectedResponseLength = 4;
                            break;

                        case ResponseType.Long:
                            expectedResponseLength = 16;
                            break;
                    }

                    if(resp.Length != expectedResponseLength)
                    {
                        this.Log(LogLevel.Warning, "Expected response of length {0} bytes, but received {1} bytes", expectedResponseLength, resp.Length);
                        return;
                    }
                    
                    for(var i = 0; i < resp.Length; i++)
                    {
                        responseBuffer[ResponseBufferLength - 1 - i] = resp[i];
                    }

                    switch(transferTypeField.Value)
                    {
                        case TransferType.Read:
                            if(dmaReaderEnabled.Value)
                            {
                                ReadData();
                            }
                            break;

                        case TransferType.Write:
                            if(dmaWriterEnabled.Value)
                            {
                                WriteData();
                            }
                            break;
                    }
                })
                .WithReservedBits(1, 7)
                .WithReservedBits(8, 24);

            CoreRegisters.Response.DefineMany(coreRegistersCollection, ResponseBufferLength, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, FieldMode.Read, name: $"Response{idx}", valueProviderCallback: _ =>
                    {
                        return responseBuffer[idx];
                    })
                    .WithIgnoredBits(8, 24);
            });

            CoreRegisters.CommandEvent.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "cmd_done", valueProviderCallback: _ => true)
                .WithTag("cmd_error", 1, 1)
                .WithTag("cmd_timeout", 2, 1)
                .WithReservedBits(3, 5)
                .WithIgnoredBits(8, 24);

            CoreRegisters.DataEvent.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "data_done", valueProviderCallback: _ => true)
                .WithTag("data_error", 1, 1)
                .WithTag("data_timeout", 2, 1)
                .WithReservedBits(3, 5)
                .WithIgnoredBits(8, 24);
            ;

            CoreRegisters.BlockSize.DefineMany(coreRegistersCollection, 2, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"BlockSize{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref blockSize, width: 8, source: val, destinationPosition: 8 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            CoreRegisters.BlockCount.DefineMany(coreRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"BlockCount{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref blockCount, width: 8, source: val, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            ReaderRegisters.DmaBase.DefineMany(readerRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"ReaderAddress{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref readerAddress, val, width: 8, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            ReaderRegisters.DmaLength.DefineMany(readerRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"ReaderLength{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref readerLength, val, width: 8, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            ReaderRegisters.DmaEnable.Define(readerRegistersCollection)
                .WithFlag(0, out dmaReaderEnabled, name: "enable")
                .WithIgnoredBits(1, 31);

            ReaderRegisters.DmaDone.Define(readerRegistersCollection)
                .WithFlag(0, name: "done", valueProviderCallback: _ => true)
                .WithIgnoredBits(1, 31);

            ReaderRegisters.DmaLoop.Define(readerRegistersCollection)
                .WithTag("loop", 0, 1)
                .WithIgnoredBits(1, 31);

            WriterRegisters.DmaBase.DefineMany(writerRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"WriterAddress{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref writerAddress, val, width: 8, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            WriterRegisters.DmaLength.DefineMany(writerRegistersCollection, 4, (register, idx) =>
            {
                register
                    .WithValueField(0, 8, name: $"WriterLength{idx}", writeCallback: (_, val) =>
                    {
                        BitHelper.ReplaceBits(ref writerLength, val, width: 8, destinationPosition: 24 - idx * 8);
                    })
                    .WithIgnoredBits(8, 24);
            });

            WriterRegisters.DmaEnable.Define(writerRegistersCollection)
                .WithFlag(0, out dmaWriterEnabled, name: "enable")
                .WithIgnoredBits(1, 31);

            WriterRegisters.DmaDone.Define(writerRegistersCollection)
                .WithFlag(0, name: "done", valueProviderCallback: _ => true)
                .WithIgnoredBits(1, 31);

            WriterRegisters.DmaLoop.Define(writerRegistersCollection)
                .WithTag("loop", 0, 1)
                .WithIgnoredBits(1, 31);
        }

        private void ReadData()
        {
            var data = RegisteredPeripheral.ReadData(readerLength);
            this.Log(LogLevel.Noisy, "Reading {0} bytes of data from device: {1}. Writing it to 0x{2:X}", data.Length, Misc.PrettyPrintCollectionHex(data), readerAddress);

            Machine.SystemBus.WriteBytes(data, readerAddress);
        }

        private void WriteData()
        {
            var data = Machine.SystemBus.ReadBytes(writerAddress, (int)writerLength);
            this.Log(LogLevel.Noisy, "Writing {0} bytes of data read from 0x{1:X} to the device: {2}", data.Length, writerAddress, Misc.PrettyPrintCollectionHex(data));

            RegisteredPeripheral.WriteData(data);
        }

        private uint readerAddress;
        private uint writerAddress;
        private uint readerLength;
        private uint writerLength;

        private uint blockSize;
        private uint blockCount;
        private IValueRegisterField commandIndexField;
        private IEnumRegisterField<ResponseType> responseTypeField;
        private IEnumRegisterField<TransferType> transferTypeField;
        private IFlagRegisterField dmaWriterEnabled;
        private IFlagRegisterField dmaReaderEnabled;

        private byte[] responseBuffer;

        private uint argumentValue;

        private readonly DoubleWordRegisterCollection phyRegistersCollection;
        private readonly DoubleWordRegisterCollection coreRegistersCollection;
        private readonly DoubleWordRegisterCollection readerRegistersCollection;
        private readonly DoubleWordRegisterCollection writerRegistersCollection;

        private const int ResponseBufferLength = 16;

        private enum TransferType
        {
            None,
            Read,
            Write
        }

        private enum ResponseType
        {
            None,
            Short,
            Long
        }

        private enum PhyRegisters
        {
            CardDetect = 0x0
        }

        private enum CoreRegisters
        {
            Argument = 0x0,
            Command0 = 0x10,
            Command1 = 0x10 + 0x4,
            Command2 = 0x10 + 0x8,
            Command3 = 0x10 + 0xC,
            IssueCommand = 0x20,
            Response = 0x24,

            CommandEvent = 0x64,
            DataEvent = 0x68,

            BlockSize = 0x6C,
            BlockCount = 0x74
        }

        private enum ReaderRegisters
        {
            DmaBase = 0x0,
            DmaLength = 0x10,
            DmaEnable = 0x20,
            DmaDone = 0x24,
            DmaLoop = 0x28
        }

        private enum WriterRegisters
        {
            DmaBase = 0x0,
            DmaLength = 0x10,
            DmaEnable = 0x20,
            DmaDone = 0x24,
            DmaLoop = 0x28,
            DmaOffset = 0x2C
        }
    }
}
