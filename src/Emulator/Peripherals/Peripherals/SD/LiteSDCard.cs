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

            writerDataBuffer = new byte[512];
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

            readerDataBuffer = null;

            Array.Clear(responseBuffer, 0, responseBuffer.Length);
            Array.Clear(writerDataBuffer, 0, writerDataBuffer.Length);
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

        [ConnectionRegionAttribute("readerBuffer")]
        public byte ReadFromReaderBuffer(long offset)
        {
            if(readerDataBuffer == null || offset >= readerDataBuffer.Length)
            {
                this.Log(LogLevel.Warning, "Tried to read from reader buffer at offset {0}, but there is not enough data there", offset);
                return 0;
            }

            return readerDataBuffer[offset];
        }

        [ConnectionRegionAttribute("readerBuffer")]
        public void WriteToReaderBuffer(long offset, byte value)
        {
            this.Log(LogLevel.Error, "Writing to the read buffer is not supported");
        }

        [ConnectionRegionAttribute("writerBuffer")]
        public byte ReadFromWriterBuffer(long offset)
        {
            if(offset >= writerDataBuffer.Length)
            {
                this.Log(LogLevel.Warning, "Tried to read from writer buffer at offset {0}, but there is not enough data there", offset);
                return 0;
            }

            return writerDataBuffer[offset];
        }

        [ConnectionRegionAttribute("writerBuffer")]
        public void WriteToWriterBuffer(long offset, byte value)
        {
            if(offset >= writerDataBuffer.Length)
            {
                this.Log(LogLevel.Warning, "Tried to write to writer buffer at offset {0}, but there is not enough data there", offset);
                return;
            }

            writerDataBuffer[offset] = value;
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

                    this.Log(LogLevel.Noisy, "Issuing command #{0}, transfer type is {1}, response type is {2}", commandIndexField.Value, transferTypeField.Value, responseTypeField.Value);

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
                            if(readerStartFlag.Value)
                            {
                                readerStartFlag.Value = false;
                                ReadData();
                            }
                            break;

                        case TransferType.Write:
                            if(writerStartFlag.Value)
                            {
                                writerStartFlag.Value = false;
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

            CoreRegisters.CommandEvent0.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.CommandEvent1.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.CommandEvent2.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.CommandEvent3.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "cmddone", valueProviderCallback: _ => true)
                .WithReservedBits(1, 1)
                .WithTag("cerrtimeout", 2, 1)
                .WithTag("cerrcrc", 3, 1)
                .WithReservedBits(4, 4)
                .WithIgnoredBits(8, 24);

            CoreRegisters.DataEvent0.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.DataEvent1.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.DataEvent2.Define(coreRegistersCollection)
                .WithIgnoredBits(0, 32);

            CoreRegisters.DataEvent3.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "datadone", valueProviderCallback: _ => true)
                .WithTag("derrwrite", 1, 1)
                .WithTag("derrtimeout", 2, 1)
                .WithTag("derrcrc", 3, 1)
                .WithReservedBits(4, 4)
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

            ReaderRegisters.ReaderReset.Define(readerRegistersCollection)
                .WithIgnoredBits(0, 1) // reset bit, no need for handling this
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);

            ReaderRegisters.ReaderStart.Define(readerRegistersCollection)
                .WithFlag(0, out readerStartFlag, name: "start")
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);

            ReaderRegisters.ReaderDone.Define(readerRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "done", valueProviderCallback: _ => true)
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);

            WriterRegisters.WriterReset.Define(writerRegistersCollection)
                .WithIgnoredBits(0, 1) // reset bit, no need for handling this
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);

            WriterRegisters.WriterStart.Define(writerRegistersCollection)
                .WithFlag(0, out writerStartFlag, name: "start")
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);

            WriterRegisters.WriterDone.Define(writerRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "done", valueProviderCallback: _ => true)
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24);
        }

        private void ReadData()
        {
            if(blockCount != 1)
            {
                this.Log(LogLevel.Warning, "This model curently supports only reading a sinlge block at a time, but the block count is set to {0}", blockCount);
                return;
            }

            readerDataBuffer = RegisteredPeripheral.ReadData(blockSize);
            this.Log(LogLevel.Noisy, "Received data is: {0}", Misc.PrettyPrintCollectionHex(readerDataBuffer));
        }

        private void WriteData()
        {
            var data = writerDataBuffer.Take((int)blockSize).ToArray();

            this.Log(LogLevel.Noisy, "Writing data: {0}", Misc.PrettyPrintCollectionHex(data));
            RegisteredPeripheral.WriteData(data);
        }

        private uint blockSize;
        private uint blockCount;
        private IFlagRegisterField readerStartFlag;
        private IFlagRegisterField writerStartFlag;
        private IValueRegisterField commandIndexField;
        private IEnumRegisterField<ResponseType> responseTypeField;
        private IEnumRegisterField<TransferType> transferTypeField;

        private byte[] responseBuffer;
        private byte[] readerDataBuffer;
        private byte[] writerDataBuffer;

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
            CommandEvent0 = 0x64,
            CommandEvent1 = 0x64 + 0x4,
            CommandEvent2 = 0x64 + 0x8,
            CommandEvent3 = 0x64 + 0xC,
            DataEvent0 = 0x74 + 0x0,
            DataEvent1 = 0x74 + 0x4,
            DataEvent2 = 0x74 + 0x8,
            DataEvent3 = 0x74 + 0xC,
            BlockSize = 0x84,
            BlockCount = 0x8C,
            DataTimeout = 0x9C,
            CommandTimeout = 0xAC,
            DataWCRCClear = 0xBC,
            DataWCRCValids = 0xC0,
            DataWCRCErrors = 0xD0
        }

        private enum ReaderRegisters
        {
            ReaderReset = 0x0,
            ReaderStart = 0x4,
            ReaderDone = 0x8,
            ReaderErrors = 0xC
        }

        private enum WriterRegisters
        {
            WriterReset = 0x0,
            WriterStart = 0x4,
            WriterDone = 0x8
        }
    }
}
