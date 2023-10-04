//
// Copyright (c) 2010-2023 Antmicro
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
    public class LiteSDCard_CSR32 : NullRegistrationPointPeripheralContainer<SDCard>, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteSDCard_CSR32(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
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

            readerAddress = 0;
            writerAddress = 0;

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

            CoreRegisters.Argument.Define(coreRegistersCollection)
                .WithValueField(0, 32, out argumentValue, name: "argument");

            CoreRegisters.Command.Define(coreRegistersCollection)
                .WithEnumField<DoubleWordRegister, ResponseType>(0, 2, out responseTypeField, name: "respone_type")
                .WithReservedBits(2, 3)
                .WithEnumField<DoubleWordRegister, TransferType>(5, 2, out transferTypeField, name: "transfer_type")
                .WithReservedBits(7, 1)
                .WithValueField(8, 6, out commandIndexField, name: "command_index")
                .WithReservedBits(14, 18);

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
                        this.Log(LogLevel.Warning, "Issued a 0x{0:X} command with 0x{1:X} argument, but there is no SD card attached", commandIndexField.Value, argumentValue.Value);
                        return;
                    }

                    this.Log(LogLevel.Noisy, "Issuing command #{0}, transfer type is {1}, response type is {2}", commandIndexField.Value, transferTypeField.Value, responseTypeField.Value);

                    var resp = RegisteredPeripheral.HandleCommand((uint)commandIndexField.Value, (uint)argumentValue.Value).AsByteArray();

                    this.Log(LogLevel.Noisy, "Received response of size {0}", resp.Length);
#if DEBUG_PACKETS
                    this.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(resp));
#endif

                    var expectedResponseLength = 0;

                    switch(responseTypeField.Value)
                    {
                        case ResponseType.Short:
                        case ResponseType.ShortBusy:
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
                        //responseBuffer[i] = resp[i];
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

            CoreRegisters.Response.DefineMany(coreRegistersCollection, ResponseBufferLength / 4, (register, idx) =>
            {
                register
                    .WithValueField(0,  8, FieldMode.Read, name: $"Response{(4 * idx + 0)}", valueProviderCallback: _ => responseBuffer[4 * idx + 3])
                    .WithValueField(8,  8, FieldMode.Read, name: $"Response{(4 * idx + 1)}", valueProviderCallback: _ => responseBuffer[4 * idx + 2])
                    .WithValueField(16, 8, FieldMode.Read, name: $"Response{(4 * idx + 2)}", valueProviderCallback: _ => responseBuffer[4 * idx + 1])
                    .WithValueField(24, 8, FieldMode.Read, name: $"Response{(4 * idx + 3)}", valueProviderCallback: _ => responseBuffer[4 * idx + 0]);
            });

            CoreRegisters.CommandEvent.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "cmddone", valueProviderCallback: _ => true)
                .WithTag("cerrwrite", 1, 1)
                .WithTag("cerrtimeout", 2, 1)
                .WithTag("cerrcrc", 3, 1)
                .WithReservedBits(4, 28);

            CoreRegisters.DataEvent.Define(coreRegistersCollection)
                .WithFlag(0, FieldMode.Read, name: "datadone", valueProviderCallback: _ => true)
                .WithTag("derrwrite", 1, 1)
                .WithTag("derrtimeout", 2, 1)
                .WithTag("derrcrc", 3, 1)
                .WithReservedBits(4, 28);

            CoreRegisters.BlockSize.Define(coreRegistersCollection)
                .WithValueField(0, 10, out blockSize, name: "BlockSize")
                .WithReservedBits(10, 22);

            CoreRegisters.BlockCount.Define(coreRegistersCollection)
                .WithValueField(0, 32, out blockCount, name: "BlockSize");

            ReaderRegisters.DmaBase.DefineMany(readerRegistersCollection, 2, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, name: $"ReaderAddress{idx}",
                        writeCallback: (_, val) => BitHelper.ReplaceBits(ref readerAddress, val, width: 32, destinationPosition: 32 - idx * 32),
                        valueProviderCallback: _ => (uint)BitHelper.GetValue(readerAddress, offset: 32 - idx * 32, size: 32));
            });

            ReaderRegisters.DmaLength.Define(readerRegistersCollection)
                .WithValueField(0, 32, out readerLength, name: "ReaderLength");

            ReaderRegisters.DmaEnable.Define(readerRegistersCollection)
                .WithFlag(0, out dmaReaderEnabled, name: "enable")
                .WithReservedBits(1, 31);

            ReaderRegisters.DmaDone.Define(readerRegistersCollection)
                .WithFlag(0, name: "done", valueProviderCallback: _ => true)
                .WithReservedBits(1, 31);

            ReaderRegisters.DmaLoop.Define(readerRegistersCollection)
                .WithTag("loop", 0, 1)
                .WithReservedBits(1, 31);

            WriterRegisters.DmaBase.DefineMany(writerRegistersCollection, 2, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, name: $"WriterAddress{idx}",
                        writeCallback: (_, val) => BitHelper.ReplaceBits(ref writerAddress, val, width: 32, destinationPosition: 32 - idx * 32),
                        valueProviderCallback: _ => (uint)BitHelper.GetValue(writerAddress, offset: 32 - idx * 32, size: 32));
            });

            WriterRegisters.DmaLength.Define(writerRegistersCollection)
                .WithValueField(0, 32, out writerLength, name: "WriterLength");

            WriterRegisters.DmaEnable.Define(writerRegistersCollection)
                .WithFlag(0, out dmaWriterEnabled, name: "enable")
                .WithReservedBits(1, 31);

            WriterRegisters.DmaDone.Define(writerRegistersCollection)
                .WithFlag(0, name: "done", valueProviderCallback: _ => true)
                .WithReservedBits(1, 31);

            WriterRegisters.DmaLoop.Define(writerRegistersCollection)
                .WithTag("loop", 0, 1)
                .WithReservedBits(1, 31);
        }

        private void ReadData()
        {
            readerAddress &= 0xffffffff;

            var data = RegisteredPeripheral.ReadData((uint)readerLength.Value);
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Reading {0} bytes of data from device: {1}. Writing it to 0x{2:X}", data.Length, Misc.PrettyPrintCollectionHex(data), readerAddress);
#endif

            sysbus.WriteBytes(data, readerAddress);
        }

        private void WriteData()
        {
            writerAddress &= 0xffffffff;

            var data = sysbus.ReadBytes(writerAddress, (int)writerLength.Value);
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Writing {0} bytes of data read from 0x{1:X} to the device: {2}", data.Length, writerAddress, Misc.PrettyPrintCollectionHex(data));
#endif

            RegisteredPeripheral.WriteData(data);
        }

        private ulong readerAddress;
        private ulong writerAddress;
        private IValueRegisterField readerLength;
        private IValueRegisterField writerLength;

        private IValueRegisterField blockSize;
        private IValueRegisterField blockCount;
        private IValueRegisterField commandIndexField;
        private IEnumRegisterField<ResponseType> responseTypeField;
        private IEnumRegisterField<TransferType> transferTypeField;
        private IFlagRegisterField dmaWriterEnabled;
        private IFlagRegisterField dmaReaderEnabled;
        private IValueRegisterField argumentValue;

        private byte[] responseBuffer;

        private readonly IBusController sysbus;
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
            Long,
            ShortBusy,
        }

        private enum PhyRegisters
        {
            CardDetect = 0x0,
            ClockDivider = 0x4,
            InitInitialize = 0x8,
            DataWStatus = 0xC,
        }

        private enum CoreRegisters
        {
            Argument = 0x0,
            Command = 0x4,
            IssueCommand = 0x8,
            Response = 0xC,
            CommandEvent = 0x1C,
            DataEvent = 0x20,
            BlockSize = 0x24,
            BlockCount = 0x28
        }

        private enum ReaderRegisters
        {
            // 64-bits long, spread accross 2 registers
            DmaBase = 0x0,
            // 32-bits long, spread accross 1 register
            DmaLength = 0x8,
            // 1-bit long, spread accross 1 register
            DmaEnable = 0xC,
            // 1-bit long, spread accross 1 register
            DmaDone = 0x10,
            // 1-bit long, spread accross 1 register
            DmaLoop = 0x14,
            // 32-bits long, spread accross 1 register
            DmaOffset = 0x18
        }

        private enum WriterRegisters
        {
            // 64-bits long, spread accross 2 registers
            DmaBase = 0x0,
            // 32-bits long, spread accross 1 register
            DmaLength = 0x8,
            // 1-bit long, spread accross 1 registers
            DmaEnable = 0xC,
            // 1-bit long, spread accross 1 registers
            DmaDone = 0x10,
            // 1-bit long, spread accross 1 registers
            DmaLoop = 0x14,
            // 32-bits long, spread accross 1 register
            DmaOffset = 0x18
        }
    }
}
