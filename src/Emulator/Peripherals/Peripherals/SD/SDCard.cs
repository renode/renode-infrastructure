//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using static Antmicro.Renode.Utilities.BitHelper;

namespace Antmicro.Renode.Peripherals.SD
{
    // Features NOT supported:
    // * Toggling selected state
    // * RCA (relative card address) filtering
    // As a result any SD controller with more than one SD card attached at the same time might not work properly.
    // Card type (SC/HC/XC/UC) is determined based on the provided capacity
    public class SDCard : ISPIPeripheral, IDisposable
    {
        public SDCard(long capacity, bool persistent = false, bool spiMode = false, BlockLength blockSize = BlockLength.Undefined)
            : this(DataStorage.CreateInMemory((int)capacity), capacity, persistent, spiMode, blockSize) {}

        public SDCard(string imageFile, long capacity, bool persistent = false, bool spiMode = false, BlockLength blockSize = BlockLength.Undefined)
            : this(DataStorage.CreateFromFile(imageFile, capacity, persistent), capacity, persistent, spiMode, blockSize) {}

        private SDCard(Stream dataBackend, long capacity, bool persistent = false, bool spiMode = false, BlockLength blockSize = BlockLength.Undefined)
        {
            var blockLenghtInBytes = SDHelpers.BlockLengthInBytes(blockSize);
            if((blockSize != BlockLength.Undefined) && (capacity % blockLenghtInBytes != 0))
            {
                throw new ConstructionException($"Size (0x{capacity:X}) is not aligned to selected block size(0x{blockLenghtInBytes:X})");
            }

            this.spiMode = spiMode;
            this.highCapacityMode = SDHelpers.TypeFromCapacity((ulong)capacity) != CardType.StandardCapacity_SC;
            this.capacity = capacity;
            this.blockSize = blockSize;
            spiContext = new SpiContext();

            this.dataBackend = dataBackend;

            var sdCapacityParameters = SDHelpers.SeekForCapacityParameters(capacity, blockSize);
            blockLengthInBytes = SDHelpers.BlockLengthInBytes(sdCapacityParameters.BlockSize);

            cardStatusGenerator = new VariableLengthValue(32)
                .DefineFragment(5, 1, () => (treatNextCommandAsAppCommand ? 1 : 0u), name: "APP_CMD bit")
                .DefineFragment(8, 1, 1, name: "READY_FOR_DATA bit")
                .DefineFragment(9, 4, () => (uint)state, name: "CURRENT_STATE")
            ;

            operatingConditionsGenerator = new VariableLengthValue(32)
                .DefineFragment(8, 1, 1, name: "VDD voltage window 2.0 - 2.1")
                .DefineFragment(9, 1, 1, name: "VDD voltage window 2.1 - 2.2")
                .DefineFragment(10, 1, 1, name: "VDD voltage window 2.2 - 2.3")
                .DefineFragment(11, 1, 1, name: "VDD voltage window 2.3 - 2.4")
                .DefineFragment(12, 1, 1, name: "VDD voltage window 2.4 - 2.5")
                .DefineFragment(13, 1, 1, name: "VDD voltage window 2.5 - 2.6")
                .DefineFragment(14, 1, 1, name: "VDD voltage window 2.6 - 2.7")
                .DefineFragment(15, 1, 1, name: "VDD voltage window 2.7 - 2.8")
                .DefineFragment(16, 1, 1, name: "VDD voltage window 2.8 - 2.9")
                .DefineFragment(17, 1, 1, name: "VDD voltage window 2.9 - 3.0")
                .DefineFragment(18, 1, 1, name: "VDD voltage window 3.0 - 3.1")
                .DefineFragment(19, 1, 1, name: "VDD voltage window 3.1 - 3.2")
                .DefineFragment(20, 1, 1, name: "VDD voltage window 3.2 - 3.3")
                .DefineFragment(21, 1, 1, name: "VDD voltage window 3.3 - 3.4")
                .DefineFragment(22, 1, 1, name: "VDD voltage window 3.4 - 3.5")
                .DefineFragment(23, 1, 1, name: "VDD voltage window 3.5 - 3.6")
                .DefineFragment(30, 1, () => this.highCapacityMode ? 1 : 0u, name: "Card Capacity Status")
                .DefineFragment(31, 1, 1, name: "Card power up status bit (busy)")
            ;

            if(!highCapacityMode)
            {
                cardSpecificDataGenerator = new VariableLengthValue(128)
                    .DefineFragment(22, 4, (ulong)sdCapacityParameters.BlockSize, name: "max write data block length")
                    .DefineFragment(47, 3, (uint)sdCapacityParameters.Multiplier, name: "device size multiplier")
                    .DefineFragment(62, 12, (ulong)sdCapacityParameters.DeviceSize, name: "device size")
                    .DefineFragment(80, 4, (uint)sdCapacityParameters.BlockSize, name: "max read data block length")
                    .DefineFragment(84, 12, (uint)(
                          CardCommandClass.Class0
                        | CardCommandClass.Class2
                        | CardCommandClass.Class4
                        ), name: "card command classes")
                    .DefineFragment(96, 3, (uint)TransferRate.Transfer10Mbit, name: "transfer rate unit")
                    .DefineFragment(99, 4, (uint)TransferMultiplier.Multiplier2_5, name: "transfer multiplier")
                    .DefineFragment(126, 2, (uint)CSD.Version1, name: "CSD structure")
                ;
            }
            else
            {
                cardSpecificDataGenerator = new VariableLengthValue(128)
                    .DefineFragment(22, 4, (ulong)sdCapacityParameters.BlockSize, name: "max write data block length")
                    .DefineFragment(48, 22, (ulong)sdCapacityParameters.DeviceSize, name: "device size")
                    .DefineFragment(80, 4, (uint)sdCapacityParameters.BlockSize, name: "max read data block length")
                    .DefineFragment(84, 12, (uint)(
                          CardCommandClass.Class0
                        | CardCommandClass.Class2
                        | CardCommandClass.Class4
                        ), name: "card command classes")
                    .DefineFragment(96, 3, (uint)TransferRate.Transfer10Mbit, name: "transfer rate unit")
                    .DefineFragment(99, 5, (uint)TransferMultiplier.Multiplier2_5, name: "transfer multiplier")
                    .DefineFragment(126, 2, (uint)CSD.Version2, name: "CSD structure")
                ;
            }

            extendedCardSpecificDataGenerator = new VariableLengthValue(4096)
                .DefineFragment((120), 8, 1, name: "command queue enabled")
                .DefineFragment((1472), 8, 1, name: "es support")
                .DefineFragment((1480), 8, 1, name: "hw hs timing")
                .DefineFragment((1568), 8, (uint)DeviceType.SDR50Mhz, name: "device type")
                .DefineFragment((2464), 8, 1, name: "command queue support")
            ;

            cardIdentificationGenerator = new VariableLengthValue(128)
                .DefineFragment(8, 4, 8, name: "manufacturer date code - month")
                .DefineFragment(12, 8, 18, name: "manufacturer date code - year")
                .DefineFragment(64, 8, (uint)'D', name: "product name 5")
                .DefineFragment(72, 8, (uint)'O', name: "product name 4")
                .DefineFragment(80, 8, (uint)'N', name: "product name 3")
                .DefineFragment(88, 8, (uint)'E', name: "product name 2")
                .DefineFragment(96, 8, (uint)'R', name: "product name 1")
                .DefineFragment(120, 8, 0xab, name: "manufacturer ID")
            ;

            switchFunctionStatusGenerator = new VariableLengthValue(512)
                .DefineFragment(128, 1, 0x1, name: "Function Number/Status Code")
            ;

            cardConfigurationGenerator = new VariableLengthValue(64)
                .DefineFragment(0, 32, 0, name: "reserved")
                .DefineFragment(32, 16, 0, name: "reserved")
                .DefineFragment(48, 4, 5, name: "DAT Bus width supported") // DAT0 (1-bit) and DAT0-3 (4-bit)
                .DefineFragment(52, 3, 0, name: "SD Security Support") // 0: No security
                .DefineFragment(55, 1, 0, name: "data_status_after erases")
                .DefineFragment(56, 4, 0, name: "SD Card - Spec. Version") // 0: Version 1.0-1.01
                .DefineFragment(60, 4, 0, name: "SCR Structure") // 0: SCR version No 1.0
            ;

            operatingVoltageGenerator = new VariableLengthValue(24)
                .DefineFragment(0, 4, 0x1, name: "voltage accepted") // 0x1: 2.7-3.6V
                .DefineFragment(4, 16, 0, name: "reserved")
                .DefineFragment(20, 4, 0, name: "command version");
            ;

            crcEngine = new CRCEngine(0x1021, 16, false, false, 0x00);
            var bufferSize = highCapacityMode ? HighCapacityBlockLength : blockLengthInBytes;
            /*
             * enough to hold:
             * 1 byte - start block token
             * bufferSize bytes - actual data
             * 2 bytes - CRC
             * 1 byte - dummy byte required for the read transaction
             */
            spiContext.DataBuffer = new byte[bufferSize + 4];
        }

        public void Reset()
        {
            GoToIdle();

            var sdCapacityParameters = SDHelpers.SeekForCapacityParameters(capacity, blockSize);
            blockLengthInBytes = SDHelpers.BlockLengthInBytes(sdCapacityParameters.BlockSize);
        }

        public void Dispose()
        {
            dataBackend.Dispose();
        }

        public BitStream HandleCommand(uint commandIndex, uint arg)
        {
            BitStream result;
            this.Log(LogLevel.Noisy, "Command received: 0x{0:x} with arg 0x{1:x}", commandIndex, arg);
            var treatNextCommandAsAppCommandLocal = treatNextCommandAsAppCommand;
            treatNextCommandAsAppCommand = false;
            if(!treatNextCommandAsAppCommandLocal || !TryHandleApplicationSpecificCommand((SdCardApplicationSpecificCommand)commandIndex, arg, out result))
            {
                result = HandleStandardCommand((SdCardCommand)commandIndex, arg);
            }
            this.Log(LogLevel.Noisy, "Sending command response: {0}", result.ToString());
            return result;
        }

        public void WriteData(byte[] data)
        {
            WriteData(data, data.Length);
        }

        public byte[] ReadData(uint size)
        {
            byte[] result;
            if(readContext.Data != null)
            {
                result = readContext.Data.AsByteArray(readContext.Offset, size);
                Array.Reverse(result);
                readContext.Move(size * 8);
            }
            else
            {
                result = ReadDataFromUnderlyingFile(readContext.Offset, checked((int)size));
                readContext.Move(size);
            }
            return result;
        }

        public byte Transmit(byte data)
        {
            if(!spiMode)
            {
                this.Log(LogLevel.Error, "Received data over SPI, but the SPI mode is disabled.");
                return 0;
            }

            this.Log(LogLevel.Noisy, "SPI: Received byte 0x{0:X} in state {1}", data, spiContext.State);

            switch(spiContext.State)
            {
                case SpiState.WaitingForCommand:
                {
                    if(spiContext.IoState == IoState.Idle && data == DummyByte)
                    {
                        this.Log(LogLevel.Noisy, "Received a DUMMY byte in the idle state, ignoring it");
                        break;
                    }

                    if((spiContext.IoState == IoState.Idle || spiContext.IoState == IoState.MultipleBlockRead) && data != DummyByte)
                    {
                        // two MSB of the SPI command byte should be '01'
                        if(BitHelper.IsBitSet(data, 7) || !BitHelper.IsBitSet(data, 6))
                        {
                            this.Log(LogLevel.Warning, "Unexpected command number value 0x{0:X}, ignoring this - expect problems", data);
                            return GenerateR1Response(illegalCommand: true).AsByte();
                        }

                        // clear COMMAND bit, we don't need it anymore
                        BitHelper.ClearBits(ref data, 6);

                        spiContext.CommandNumber = (uint)data;
                        spiContext.ArgumentBytes = 0;
                        spiContext.State = SpiState.WaitingForArgBytes;
                        
                        break;
                    }

                    switch(spiContext.IoState)
                    {
                        case IoState.SingleBlockRead:
                        case IoState.MultipleBlockRead:
                            return HandleRead();
                        case IoState.SingleBlockWrite:
                        case IoState.MultipleBlockWrite:
                            var ret = HandleWrite(data);
                            return ret;
                    }

                    break;
                }

                case SpiState.WaitingForArgBytes:
                {
                    this.Log(LogLevel.Noisy, "Storing as arg byte #{0}", spiContext.ArgumentBytes);

                    spiContext.Argument <<= 8;
                    spiContext.Argument |= data;
                    spiContext.ArgumentBytes++;

                    if(spiContext.ArgumentBytes == 4)
                    {
                        spiContext.State = SpiState.WaitingForCRC;
                    }
                    break;
                }

                case SpiState.WaitingForCRC:
                {
                    // we don't check CRC

                    this.Log(LogLevel.Noisy, "Sending a command to the SD card");
                    var result = HandleCommand(spiContext.CommandNumber, spiContext.Argument).AsByteArray();

                    if(result.Length == 0)
                    {
                        this.Log(LogLevel.Warning, "Received an empty response, this is strange and might cause problems!");
                        spiContext.State = SpiState.WaitingForCommand;
                    }
                    else
                    {
                        // The response is sent back within command response time,
                        // 0 to 8 bytes for SDC, 1 to 8 bytes for MMC.
                        // Send single dummy byte for compatibility with both cases.
                        spiContext.ResponseBuffer.Enqueue(DummyByte);

                        spiContext.ResponseBuffer.EnqueueRange(result);
                        spiContext.State = SpiState.SendingResponse;
                    }
                    break;
                }

                case SpiState.SendingResponse:
                {
                    spiContext.ResponseBuffer.TryDequeue(out var res);

                    if(spiContext.ResponseBuffer.Count == 0)
                    {
                        this.Log(LogLevel.Noisy, "This is the end of response buffer");
                        spiContext.State = SpiState.WaitingForCommand;
                    }
                    return res;
                }

                default:
                {
                    throw new ArgumentException($"Received data 0x{data:X} in an unexpected state {spiContext.State}");
                }
            }

            return DummyByte;
        }

        public void FinishTransmission()
        {
            if(!spiMode)
            {
                this.Log(LogLevel.Error, "Received SPI transmission finish signal, but the SPI mode is disabled.");
                return;
            }

            this.Log(LogLevel.Noisy, "Finishing transmission");
            spiContext.Reset();
        }

        public byte[] ReadSwitchFunctionStatusRegister()
        {
            return switchFunctionStatusGenerator.Bits.AsByteArray();
        }

        public byte[] ReadExtendedCardSpecificDataRegister()
        {
            return extendedCardSpecificDataGenerator.Bits.AsByteArray();
        }

        public ushort CardAddress { get; set; }

        public BitStream CardStatus => cardStatusGenerator.Bits;

        public BitStream OperatingConditions => operatingConditionsGenerator.Bits;

        public BitStream SDConfiguration => cardConfigurationGenerator.Bits;

        public BitStream SDStatus => new VariableLengthValue(512).Bits;

        public BitStream CardSpecificData => cardSpecificDataGenerator.Bits;

        public BitStream CardIdentification => cardIdentificationGenerator.Bits;

        public BitStream OperatingVoltage => operatingVoltageGenerator.Bits;

        private void WriteData(byte[] data, int length)
        {
            WriteDataToUnderlyingFile(writeContext.Offset, length, data);
            writeContext.Move((uint)length);
            state = SDCardState.Transfer;
        }

        private void WriteDataToUnderlyingFile(long offset, int size, byte[] data)
        {
            dataBackend.Position = offset;
            var actualSize = checked((int)Math.Min(size, dataBackend.Length - dataBackend.Position));
            if(actualSize < size)
            {
                this.Log(LogLevel.Warning, "Tried to write {0} bytes of data to offset {1}, but space for only {2} is available.", size, offset, actualSize);
            }
            dataBackend.Write(data, 0, actualSize);
        }

        private byte[] ReadDataFromUnderlyingFile(long offset, int size)
        {
            dataBackend.Position = offset;
            var actualSize = checked((int)Math.Min(size, dataBackend.Length - dataBackend.Position));
            if(actualSize < size)
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes of data from offset {1}, but only {2} is available.", size, offset, actualSize);
            }

            /* During multi-block read, when reading till the end of disk, the SD card could reach end of disk before receiving
             * stop transmission command. SD card specification doesn't specify what should be done in such situation.
             * Return 0s as missing bytes.
             */
            var result = new byte[size];
            var readSoFar = 0;
            while(readSoFar < actualSize)
            {
                var readThisTime = dataBackend.Read(result, readSoFar, actualSize - readSoFar);
                if(readThisTime == 0)
                {
                    // this should not happen as we calculated the available data size
                    throw new ArgumentException("Unexpected end of data in file stream");
                }
                readSoFar += readThisTime;
            }

            return result;
        }

        private void GoToIdle()
        {
            readContext.Reset();
            writeContext.Reset();
            treatNextCommandAsAppCommand = false;

            state = SDCardState.Idle;

            spiContext.Reset();
        }

        /* Data packet has the following format:
         * bytes:
         * [ 0                          ] data token
         * [ 1            : blockSize   ] actual data
         * [ blocksSize+1 : blockSize+2 ] CRC
         *
         * After sending CRC, we have to send single DummyByte before sending next
         * data packet.
         */
        private byte HandleRead()
        {
            var blockSize = highCapacityMode ? HighCapacityBlockLength : blockLengthInBytes;
            ushort crc = 0;

            if(spiContext.BytesSent == 0)
            {
                var readData = ReadData(blockSize);
                crc = (ushort)crcEngine.Calculate(readData);

                spiContext.DataBuffer[0] = BlockBeginIndicator;
                System.Array.Copy(readData, 0, spiContext.DataBuffer, 1, blockSize);
                spiContext.DataBuffer[blockSize + 1] = crc.HiByte();
                spiContext.DataBuffer[blockSize + 2] = crc.LoByte();
                // before sending new packet, at least one DummyByte should be sent
                spiContext.DataBuffer[blockSize + 3] = DummyByte;
            }

            var res = spiContext.DataBuffer[spiContext.BytesSent]; 

            spiContext.BytesSent++;
            // We sent whole required data and can proceed to the next data packet,
            // or finish the data read.
            if(spiContext.BytesSent == blockSize + 4)
            {
                if(spiContext.IoState == IoState.SingleBlockRead)
                {
                    spiContext.IoState = IoState.Idle;
                }
                spiContext.BytesSent = 0;
            }
            return res;
        }

        /* Data packet has the following format:
         * bytes:
         * [ 0                          ] data token
         * [ 1            : blockSize   ] actual data
         * [ blocksSize+1 : blockSize+2 ] CRC
         *
         * After sending CRC, we have to send data response.
         */
        private byte HandleWrite(byte data)
        {
            var blockSize = highCapacityMode ? HighCapacityBlockLength : blockLengthInBytes;

            switch(spiContext.ReceptionState)
            {
                case ReceptionState.WaitingForDataToken:
                    switch((DataToken)data)
                    {
                        case DataToken.StopTran:
                            spiContext.IoState = IoState.Idle;
                            state = SDCardState.Programming;
                            break;
                        case DataToken.SingleWriteStartBlock:
                        case DataToken.MultiWriteStartBlock:
                            spiContext.DataBytesReceived = 0;
                            spiContext.ReceptionState = ReceptionState.ReceivingData;
                            break;
                    }
                    return DummyByte;
                case ReceptionState.ReceivingData:
                    spiContext.DataBuffer[spiContext.DataBytesReceived] = data;
                    spiContext.DataBytesReceived++;
                    if(spiContext.DataBytesReceived == blockSize)
                    {
                        spiContext.ReceptionState = ReceptionState.ReceivingCRC;
                        spiContext.CRCBytesReceived = 0;
                    }
                    return DummyByte;
                case ReceptionState.ReceivingCRC:
                    spiContext.CRCBytesReceived++;
                    if(spiContext.CRCBytesReceived == 2)
                    {
                        spiContext.ReceptionState = ReceptionState.SendingResponse;
                    }
                    return DummyByte;
                case ReceptionState.SendingResponse:
                    WriteData(spiContext.DataBuffer, (int)blockSize);
                    spiContext.ReceptionState = ReceptionState.WaitingForDataToken;
                    if(spiContext.IoState == IoState.SingleBlockWrite)
                    {
                        spiContext.IoState = IoState.Idle;
                        state = SDCardState.Programming;
                    }
                    return DataAcceptedResponse;
            }
            return DummyByte;
        }

        private BitStream GenerateR1Response(bool illegalCommand = false)
        {
            return new BitStream()
                .AppendBit(state == SDCardState.Idle)
                .AppendBit(false) // Erase Reset
                .AppendBit(illegalCommand)
                .AppendBit(false) // Com CRC Error
                .AppendBit(false) // Erase Seq Error
                .AppendBit(false) // Address Error
                .AppendBit(false) // Parameter Error
                .AppendBit(false); // always 0
        }

        private BitStream GenerateR2Response()
        {
            return GenerateR1Response()
                .Append((byte)0); // TODO: fill with the actual data
        }

        private BitStream GenerateR3Response()
        {
            return GenerateR1Response()
                .Append(OperatingConditions.AsByteArray());
        }

        private BitStream GenerateR7Response(byte checkPattern)
        {
            return GenerateR1Response()
                .Append(OperatingVoltage.AsByteArray().Reverse().ToArray())
                .Append(checkPattern);
        }

        private BitStream GenerateRegisterResponse(BitStream register)
        {
            var reg = register.AsByteArray().Reverse().ToArray();
            ushort crc = (ushort)crcEngine.Calculate(reg);
            return GenerateR1Response()
                .Append(BlockBeginIndicator)
                .Append(reg)
                .Append(crc.HiByte())
                .Append(crc.LoByte());
        }

        private BitStream HandleStandardCommand(SdCardCommand command, uint arg)
        {
            this.Log(LogLevel.Noisy, "Handling as a standard command: {0}", command);
            switch(command)
            {
                case SdCardCommand.GoIdleState_CMD0:
                    GoToIdle();
                    return spiMode
                        ? GenerateR1Response()
                        : BitStream.Empty; // no response in SD mode

                case SdCardCommand.SendSupportInformation_CMD1:
                    return spiMode
                        ? GenerateR3Response()
                        : OperatingConditions;

                case SdCardCommand.SendCardIdentification_CMD2:
                {
                    if(spiMode)
                    {
                        // this command is not supported in the SPI mode
                        break;
                    }

                    state = SDCardState.Identification;

                    return CardIdentification;
                }

                case SdCardCommand.SendRelativeAddress_CMD3:
                {
                    if(spiMode)
                    {
                        // this command is not supported in the SPI mode
                        break;
                    }

                    state = SDCardState.Standby;

                    var status = CardStatus.AsUInt32();
                    return BitHelper.BitConcatenator.New()
                        .StackAbove(status, 13, 0)
                        .StackAbove(status, 1, 19)
                        .StackAbove(status, 2, 22)
                        .StackAbove(CardAddress, 16, 0)
                        .Bits;
                }

                case SdCardCommand.CheckSwitchableFunction_CMD6:
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.SelectDeselectCard_CMD7:
                {
                    if(spiMode)
                    {
                        // this command is not supported in the SPI mode
                        break;
                    }

                    // this is a toggle command:
                    // Select is used to start a transfer;
                    // Deselct is used to abort the active transfer
                    switch(state)
                    {
                        case SDCardState.Standby:
                            state = SDCardState.Transfer;
                            break;

                        case SDCardState.Transfer:
                        case SDCardState.Programming:
                            state = SDCardState.Standby;
                            break;
                    }

                    return CardStatus;
                }

                case SdCardCommand.SendInterfaceConditionCommand_CMD8:
                    return spiMode
                        ? GenerateR7Response((byte)arg)
                        : CardStatus;

                case SdCardCommand.SendCardSpecificData_CMD9:
                    return spiMode
                        ? GenerateRegisterResponse(CardSpecificData)
                        : CardSpecificData;

                case SdCardCommand.SendCardIdentification_CMD10:
                    return spiMode
                        ? GenerateRegisterResponse(CardIdentification)
                        : CardIdentification;

                case SdCardCommand.StopTransmission_CMD12:
                    readContext.Reset();
                    writeContext.Reset();

                    switch(state)
                    {
                        case SDCardState.SendingData:
                            state = SDCardState.Transfer;
                            spiContext.IoState = IoState.Idle;
                            break;

                        case SDCardState.ReceivingData:
                            state = SDCardState.Programming;
                            break;
                    }

                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.SendStatus_CMD13:
                    return spiMode
                        ? GenerateR2Response()
                        : CardStatus;

                case SdCardCommand.SetBlockLength_CMD16:
                    blockLengthInBytes = arg;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.ReadSingleBlock_CMD17:
                    spiContext.IoState = IoState.SingleBlockRead;
                    state = SDCardState.SendingData;
                    spiContext.BytesSent = 0;
                    readContext.Offset = highCapacityMode
                        ? arg * HighCapacityBlockLength
                        : arg;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.ReadMultipleBlocks_CMD18:
                    spiContext.IoState = IoState.MultipleBlockRead;
                    state = SDCardState.SendingData;
                    spiContext.BytesSent = 0;
                    readContext.Offset = highCapacityMode
                        ? arg * HighCapacityBlockLength
                        : arg;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.SetBlockCount_CMD23:
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.WriteSingleBlock_CMD24:
                    state = SDCardState.ReceivingData;
                    spiContext.IoState = IoState.SingleBlockWrite;
                    spiContext.DataBytesReceived = 0;
                    spiContext.ReceptionState = ReceptionState.WaitingForDataToken;
                    writeContext.Offset = highCapacityMode
                        ? arg * HighCapacityBlockLength
                        : arg;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.WriteMultipleBlocks_CMD25:
                    state = SDCardState.ReceivingData;
                    spiContext.IoState = IoState.MultipleBlockWrite;
                    spiContext.DataBytesReceived = 0;
                    spiContext.ReceptionState = ReceptionState.WaitingForDataToken;
                    writeContext.Offset = highCapacityMode
                        ? arg * HighCapacityBlockLength
                        : arg;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.AppCommand_CMD55:
                    treatNextCommandAsAppCommand = true;
                    return spiMode
                        ? GenerateR1Response()
                        : CardStatus;

                case SdCardCommand.ReadOperationConditionRegister_CMD58:
                    return spiMode
                        ? GenerateR3Response()
                        : BitStream.Empty;

                case SdCardCommand.EnableCRCChecking_CMD59:
                    // we don't have to check CRC, but the software requires proper response after such request
                    return spiMode
                        ? GenerateR1Response()
                        : BitStream.Empty;
            }

            this.Log(LogLevel.Warning, "Unsupported command: {0}. Ignoring it", command);
            return spiMode
                ? GenerateR1Response(illegalCommand: true)
                : BitStream.Empty;
        }

        private bool TryHandleApplicationSpecificCommand(SdCardApplicationSpecificCommand command, uint arg, out BitStream result)
        {
            this.Log(LogLevel.Noisy, "Handling as an application specific command: {0}", command);
            switch(command)
            {
                case SdCardApplicationSpecificCommand.SendSDCardStatus_ACMD13:
                    readContext.Data = SDStatus;
                    result = spiMode
                        ? GenerateR2Response()
                        : CardStatus;
                    return true;

                case SdCardApplicationSpecificCommand.SendOperatingConditionRegister_ACMD41:
                    // If HCS is set to 0, High Capacity SD Memory Card never returns ready state
                    var hcs = BitHelper.IsBitSet(arg, 30);
                    if(!highCapacityMode || hcs)
                    {
                        // activate the card
                        state = SDCardState.Ready;
                    }

                    result = spiMode
                        ? GenerateR1Response()
                        : OperatingConditions;
                    return true;

                case SdCardApplicationSpecificCommand.SendSDConfigurationRegister_ACMD51:
                    readContext.Data = SDConfiguration;
                    result = spiMode
                        ? GenerateRegisterResponse(SDConfiguration)
                        : CardStatus;
                    return true;

                default:
                    this.Log(LogLevel.Noisy, "Command #{0} seems not to be any application specific command", command);
                    result = null;
                    return false;
            }
        }

        private SDCardState state;

        private bool treatNextCommandAsAppCommand;
        private uint blockLengthInBytes;
        private IoContext writeContext;
        private IoContext readContext;
        private readonly Stream dataBackend;
        private readonly VariableLengthValue cardStatusGenerator;
        private readonly VariableLengthValue cardConfigurationGenerator;
        private readonly VariableLengthValue operatingConditionsGenerator;
        private readonly VariableLengthValue cardSpecificDataGenerator;
        private readonly VariableLengthValue extendedCardSpecificDataGenerator;
        private readonly VariableLengthValue cardIdentificationGenerator;
        private readonly VariableLengthValue switchFunctionStatusGenerator;
        private readonly VariableLengthValue operatingVoltageGenerator;

        private readonly long capacity;
        private readonly BlockLength blockSize;
        private readonly bool spiMode;
        private readonly bool highCapacityMode;
        private readonly SpiContext spiContext;
        private readonly CRCEngine crcEngine;
        private const byte DummyByte = 0xFF;
        private const byte BlockBeginIndicator = 0xFE;
        private const int HighCapacityBlockLength = 512;
        private const byte DataAcceptedResponse = 0x05;

        private struct IoContext
        {
            public uint Offset
            {
                get { return offset; }
                set
                {
                    offset = value;
                    data = null;
                }
            }

            public BitStream Data
            {
                get { return data; }
                set
                {
                    data = value;
                    offset = 0;
                }
            }

            public void Move(uint offset)
            {
                this.offset += offset;
                if(data == null)
                {
                    bytesLeft -= offset;
                }
            }

            public void Reset()
            {
                offset = 0;
                bytesLeft = 0;
                data = null;
            }

            private uint bytesLeft;
            private uint offset;
            private BitStream data;
        }

        private enum SdCardCommand
        {
            GoIdleState_CMD0 = 0,
            SendSupportInformation_CMD1 = 1,
            SendCardIdentification_CMD2 = 2,
            SendRelativeAddress_CMD3 = 3,
            CheckSwitchableFunction_CMD6 = 6,
            SelectDeselectCard_CMD7 = 7,
            // this command has been added in spec version 2.0 - we don't have to answer to it
            SendInterfaceConditionCommand_CMD8 = 8,
            SendCardSpecificData_CMD9 = 9,
            SendCardIdentification_CMD10 = 10,
            StopTransmission_CMD12 = 12,
            SendStatus_CMD13 = 13,
            SetBlockLength_CMD16 = 16,
            ReadSingleBlock_CMD17 = 17,
            ReadMultipleBlocks_CMD18 = 18,
            SetBlockCount_CMD23 = 23,
            WriteSingleBlock_CMD24 = 24,
            WriteMultipleBlocks_CMD25 = 25,
            AppCommand_CMD55 = 55,
            ReadOperationConditionRegister_CMD58 = 58,
            EnableCRCChecking_CMD59 = 59
        }

        private enum SdCardApplicationSpecificCommand
        {
            SendSDCardStatus_ACMD13 = 13,
            SendOperatingConditionRegister_ACMD41 = 41,
            SendSDConfigurationRegister_ACMD51 = 51
        }

        [Flags]
        private enum CardCommandClass
        {
            Class0 = (1 << 0),
            Class1 = (1 << 1),
            Class2 = (1 << 2),
            Class3 = (1 << 3),
            Class4 = (1 << 4),
            Class5 = (1 << 5),
            Class6 = (1 << 6),
            Class7 = (1 << 7),
            Class8 = (1 << 8),
            Class9 = (1 << 9),
            Class10 = (1 << 10),
            Class11 = (1 << 11)
        }

        private enum TransferRate
        {
            Transfer100kbit = 0,
            Transfer1Mbit = 1,
            Transfer10Mbit = 2,
            Transfer100Mbit = 3,
            // the rest is reserved
        }

        private enum TransferMultiplier
        {
            Reserved = 0,
            Multiplier1 = 1,
            Multiplier1_2 = 2,
            Multiplier1_3 = 3,
            Multiplier1_5 = 4,
            Multiplier2 = 5,
            Multiplier2_5 = 6,
            Multiplier3 = 7,
            Multiplier3_5 = 8,
            Multiplier4 = 9,
            Multiplier4_5 = 10,
            Multiplier5 = 11,
            Multiplier5_5 = 12,
            Multiplier6 = 13,
            Multiplier7 = 14,
            Multiplier8 = 15
        }

        private enum DeviceType
        {
            Legacy = 0,
            SDR25Mhz = 1,
            SDR50Mhz = 2,
            SDR = 3,
            DDR = 4
        }

        private enum DataToken
        {
            SingleWriteStartBlock = 0xFE,
            MultiWriteStartBlock = 0xFC,
            StopTran = 0xFD
        }

        private enum SpiState
        {
            WaitingForCommand,
            WaitingForArgBytes,
            WaitingForCRC,
            SendingResponse
        }

        private enum IoState
        {
            Idle,
            SingleBlockRead,
            MultipleBlockRead,
            SingleBlockWrite,
            MultipleBlockWrite
        }

        private enum ReceptionState
        {
            WaitingForDataToken,
            ReceivingData,
            ReceivingCRC,
            SendingResponse
        }

        private class SpiContext
        {
            public void Reset()
            {
                ResponseBuffer.Clear();
                ArgumentBytes = 0;
                Argument = 0;
                CommandNumber = 0;
                State = SpiState.WaitingForCommand;
                BytesSent = 0;
                IoState = IoState.Idle;
            }

            public Queue<byte> ResponseBuffer = new Queue<byte>();
            public byte[] DataBuffer;
            public int ArgumentBytes;
            public uint Argument;
            public uint CommandNumber;
            public SpiState State;
            public uint BytesSent;
            public uint DataBytesReceived;
            public uint CRCBytesReceived;
            public IoState IoState;
            public ReceptionState ReceptionState;
        }

        private enum SDCardState
        {
            Idle = 0,
            Ready = 1,
            Identification = 2,
            Standby = 3,
            Transfer = 4,
            SendingData = 5,
            ReceivingData = 6,
            Programming = 7,
            Disconnect = 8
            // the rest is reserved
        }

        private enum CSD
        {
            Version1 = 0,
            Version2 = 1
        }
    }
}
