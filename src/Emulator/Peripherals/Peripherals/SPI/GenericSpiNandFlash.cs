//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class GenericSpiNandFlash : ISPIPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IGPIOReceiver
    {
        public GenericSpiNandFlash(
            MappedMemory underlyingMemory = null,
            uint pageSize = DefaultPageSize,
            uint spareSize = DefaultSpareSize,
            uint pagesPerBlock = DefaultPagesPerBlock,
            uint blocksCount = DefaultBlocksCount,
            byte manufacturerId = DefaultManufacturerId,
            ushort deviceId = DefaultDeviceId)
        {
            this.pageSize = pageSize;
            this.spareSize = spareSize;
            this.pagesPerBlock = pagesPerBlock;
            this.blocksCount = blocksCount;
            this.manufacturerId = manufacturerId;
            this.deviceId = deviceId;
            this.underlyingMemory = underlyingMemory;

            if(deviceId > 0xFF)
            {
                idBytes = new byte[] { manufacturerId, (byte)(deviceId >> 8), (byte)(deviceId & 0xFF) };
            }
            else
            {
                idBytes = new byte[] { manufacturerId, (byte)deviceId };
            }

            var rawPageSize = (int)(pageSize + spareSize);
            pageBuffer = new byte[rawPageSize];
            Array.Fill(pageBuffer, EmptySegment);

            long totalCapacity = (long)blocksCount * pagesPerBlock * rawPageSize;
            if(underlyingMemory != null)
            {
                if(underlyingMemory.Size < totalCapacity)
                {
                    throw new ConstructionException($"Underlying memory size (0x{underlyingMemory.Size:X}) is smaller than required SPI-NAND capacity (0x{totalCapacity:X})");
                }
                underlyingMemory.ResetByte = EmptySegment;
            }
            else
            {
                flashMemory = new byte[totalCapacity];
                Array.Fill(flashMemory, EmptySegment);
            }

            addressBuffer = new List<byte>();
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            state = State.Idle;
            readIdIndex = 0;
            currentColumnAddress = 0;
            featureRegisterAddress = 0;
            addressBuffer.Clear();
            Array.Fill(pageBuffer, EmptySegment);
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Transmitting data 0x{0:X2}, current state: {1}", data, state);
            byte result = 0;

            switch(state)
            {
            case State.Idle:
                result = HandleIdleCommand((Command)data);
                break;

            case State.ReadIdDummy:
                state = State.ReadIdData;
                readIdIndex = 0;
                result = 0;
                break;

            case State.ReadIdData:
                result = idBytes[readIdIndex];
                readIdIndex = (readIdIndex + 1) % idBytes.Length;
                break;

            case State.GetFeatureAddress:
                featureRegisterAddress = data;
                state = State.GetFeatureData;
                result = 0;
                break;

            case State.GetFeatureData:
                result = RegistersCollection.Read(featureRegisterAddress);
                break;

            case State.SetFeatureAddress:
                featureRegisterAddress = data;
                state = State.SetFeatureData;
                result = 0;
                break;

            case State.SetFeatureData:
                RegistersCollection.Write(featureRegisterAddress, data);
                this.Log(LogLevel.Noisy, "Set feature 0x{0:X2} = 0x{1:X2}", featureRegisterAddress, data);
                state = State.Idle;
                result = 0;
                break;

            case State.PageReadAddress:
                addressBuffer.Add(data);
                if(addressBuffer.Count == RowAddressByteCount)
                {
                    uint rowAddress = ParseRowAddress();
                    ExecutePageRead(rowAddress);
                    state = State.Idle;
                }
                result = 0;
                break;

            case State.ReadCacheAddress:
                addressBuffer.Add(data);
                if(addressBuffer.Count == ColumnAddressByteCount)
                {
                    currentColumnAddress = (uint)((addressBuffer[0] << 8) | addressBuffer[1]) % (uint)pageBuffer.Length;
                    state = State.ReadCacheDummy;
                }
                result = 0;
                break;

            case State.ReadCacheDummy:
                state = State.ReadCacheData;
                result = 0;
                break;

            case State.ReadCacheData:
                result = pageBuffer[currentColumnAddress];
                currentColumnAddress = (currentColumnAddress + 1) % (uint)pageBuffer.Length;
                break;

            case State.ProgramLoadAddress:
                addressBuffer.Add(data);
                if(addressBuffer.Count == ColumnAddressByteCount)
                {
                    currentColumnAddress = (uint)((addressBuffer[0] << 8) | addressBuffer[1]) % (uint)pageBuffer.Length;
                    if(!isRandomProgramLoad)
                    {
                        Array.Fill(pageBuffer, EmptySegment);
                    }
                    state = State.ProgramLoadData;
                }
                result = 0;
                break;

            case State.ProgramLoadData:
                pageBuffer[currentColumnAddress] = data;
                currentColumnAddress = (currentColumnAddress + 1) % (uint)pageBuffer.Length;
                result = 0;
                break;

            case State.ProgramExecuteAddress:
                addressBuffer.Add(data);
                if(addressBuffer.Count == RowAddressByteCount)
                {
                    uint rowAddress = ParseRowAddress();
                    ExecuteProgram(rowAddress);
                    state = State.Idle;
                }
                result = 0;
                break;

            case State.BlockEraseAddress:
                addressBuffer.Add(data);
                if(addressBuffer.Count == RowAddressByteCount)
                {
                    uint rowAddress = ParseRowAddress();
                    ExecuteBlockErase(rowAddress);
                    state = State.Idle;
                }
                result = 0;
                break;

            default:
                this.Log(LogLevel.Error, "Received byte 0x{0:X2} in unexpected state: {1}", data, state);
                break;
            }

            return result;
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Chip Select deasserted. Finishing transmission.");
            state = State.Idle;
            addressBuffer.Clear();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == 0 && value)
            {
                FinishTransmission();
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public uint PageSize => pageSize;
        public uint SpareSize => spareSize;
        public uint PagesPerBlock => pagesPerBlock;
        public uint BlocksCount => blocksCount;
        public byte ManufacturerId => manufacturerId;
        public ushort DeviceId => deviceId;
        public MappedMemory UnderlyingMemory => underlyingMemory;
        public byte[] PageBuffer => pageBuffer;

        private byte HandleIdleCommand(Command command)
        {
            switch(command)
            {
            case Command.Reset:
                Reset();
                break;

            case Command.ReadId:
                state = State.ReadIdDummy;
                readIdIndex = 0;
                break;

            case Command.WriteEnable:
                writeEnableLatch.Value = true;
                this.Log(LogLevel.Noisy, "Write enabled (WEL = 1)");
                break;

            case Command.WriteDisable:
                writeEnableLatch.Value = false;
                this.Log(LogLevel.Noisy, "Write disabled (WEL = 0)");
                break;

            case Command.GetFeature:
                state = State.GetFeatureAddress;
                break;

            case Command.SetFeature:
                state = State.SetFeatureAddress;
                break;

            case Command.PageRead:
                addressBuffer.Clear();
                state = State.PageReadAddress;
                break;

            case Command.ReadFromCache:
            case Command.FastReadFromCache:
            case Command.ReadFromCacheDual:
            case Command.ReadFromCacheQuad:
                addressBuffer.Clear();
                state = State.ReadCacheAddress;
                break;

            case Command.ProgramLoad:
                addressBuffer.Clear();
                isRandomProgramLoad = false;
                state = State.ProgramLoadAddress;
                break;

            case Command.ProgramLoadRandom:
                addressBuffer.Clear();
                isRandomProgramLoad = true;
                state = State.ProgramLoadAddress;
                break;

            case Command.ProgramExecute:
                addressBuffer.Clear();
                state = State.ProgramExecuteAddress;
                break;

            case Command.BlockErase:
                addressBuffer.Clear();
                state = State.BlockEraseAddress;
                break;

            default:
                this.Log(LogLevel.Warning, "Unsupported SPI-NAND command: 0x{0:X2}", (byte)command);
                break;
            }

            return 0;
        }

        private uint ParseRowAddress()
        {
            // Row Address is 24 bits in standard SPI-NAND command framing:
            // On 1Gb devices, addressBuffer[0] is dummy (0x00).
            // On 2Gb/4Gb devices (e.g. Alliance Memory AS5F14G04SND, Winbond W25N02GV/W25N04GV),
            // the lower bits of addressBuffer[0] carry upper page/row address bits RA16-RA19.
            return (uint)(((addressBuffer[0] & 0x0F) << 16) | (addressBuffer[1] << 8) | addressBuffer[2]);
        }

        private void ExecutePageRead(uint rowAddress)
        {
            var rawPageSize = (int)(pageSize + spareSize);
            var totalPages = blocksCount * pagesPerBlock;
            if(rowAddress >= totalPages)
            {
                this.Log(LogLevel.Error, "PageRead row address out of bounds: 0x{0:X} (max: 0x{1:X})", rowAddress, totalPages);
                return;
            }

            long offset = (long)rowAddress * rawPageSize;
            ReadFlashBytes(offset, pageBuffer, 0, rawPageSize);
            eccStatus.Value = 0; // ECC clean
            this.Log(LogLevel.Noisy, "Loaded page 0x{0:X} (offset 0x{1:X}) into page buffer", rowAddress, offset);
        }

        private void ExecuteProgram(uint rowAddress)
        {
            if(!writeEnableLatch.Value)
            {
                this.Log(LogLevel.Warning, "Attempted to Program Execute while write is disabled (WEL = 0)");
                programFail.Value = true;
                return;
            }

            var rawPageSize = (int)(pageSize + spareSize);
            var totalPages = blocksCount * pagesPerBlock;
            if(rowAddress >= totalPages)
            {
                this.Log(LogLevel.Error, "ProgramExecute row address out of bounds: 0x{0:X} (max: 0x{1:X})", rowAddress, totalPages);
                programFail.Value = true;
                writeEnableLatch.Value = false;
                return;
            }

            long offset = (long)rowAddress * rawPageSize;
            WriteFlashBytes(offset, pageBuffer, 0, rawPageSize);
            programFail.Value = false;
            writeEnableLatch.Value = false;
            this.Log(LogLevel.Noisy, "Programmed page 0x{0:X} (offset 0x{1:X}) from page buffer", rowAddress, offset);
        }

        private void ExecuteBlockErase(uint rowAddress)
        {
            if(!writeEnableLatch.Value)
            {
                this.Log(LogLevel.Warning, "Attempted Block Erase while write is disabled (WEL = 0)");
                eraseFail.Value = true;
                return;
            }

            var rawPageSize = (int)(pageSize + spareSize);
            var blockSize = (int)(pagesPerBlock * rawPageSize);
            uint blockIndex = rowAddress / pagesPerBlock;
            if(blockIndex >= blocksCount)
            {
                this.Log(LogLevel.Error, "BlockErase block index out of bounds: 0x{0:X} (max: 0x{1:X})", blockIndex, blocksCount);
                eraseFail.Value = true;
                writeEnableLatch.Value = false;
                return;
            }

            long offset = (long)blockIndex * blockSize;
            FillFlashRange(offset, blockSize, EmptySegment);
            eraseFail.Value = false;
            writeEnableLatch.Value = false;
            this.Log(LogLevel.Noisy, "Erased block {0} (offset 0x{1:X}, size 0x{2:X})", blockIndex, offset, blockSize);
        }

        private void ReadFlashBytes(long address, byte[] destination, int offset, int count)
        {
            if(underlyingMemory != null)
            {
                underlyingMemory.ReadBytes(address, count, destination, offset);
            }
            else
            {
                Buffer.BlockCopy(flashMemory, (int)address, destination, offset, count);
            }
        }

        private void WriteFlashBytes(long address, byte[] source, int offset, int count)
        {
            if(underlyingMemory != null)
            {
                underlyingMemory.WriteBytes(address, source, offset, count);
            }
            else
            {
                Buffer.BlockCopy(source, offset, flashMemory, (int)address, count);
            }
        }

        private void FillFlashRange(long address, int count, byte value)
        {
            if(underlyingMemory != null)
            {
                underlyingMemory.SetRange(address, count, value);
            }
            else
            {
                Array.Fill(flashMemory, value, (int)address, count);
            }
        }

        private void DefineRegisters()
        {
            RegisterType.BlockLock.Define(this, 0x38)
                .WithFlag(1, out statusRegisterProtect1, name: "SRP1")
                .WithFlag(2, out writeProtectEnable, name: "WP_E")
                .WithValueField(3, 4, out blockProtectBits, name: "BP0-BP3")
                .WithFlag(7, out statusRegisterProtect0, name: "SRP0");

            RegisterType.Config.Define(this, 0x18)
                .WithFlag(0, name: "CFG0")
                .WithFlag(1, out quadEnable, name: "QE")
                .WithReservedBits(2, 2)
                .WithFlag(4, out eccEnable, name: "ECC_EN")
                .WithReservedBits(5, 1)
                .WithFlag(6, name: "OTP_EN")
                .WithFlag(7, name: "OTP_PRT");

            RegisterType.Status.Define(this, 0x00)
                .WithFlag(0, out operationInProgress, FieldMode.Read, name: "OIP")
                .WithFlag(1, out writeEnableLatch, FieldMode.Read | FieldMode.Write, name: "WEL")
                .WithFlag(2, out eraseFail, FieldMode.Read | FieldMode.Write, name: "E_FAIL")
                .WithFlag(3, out programFail, FieldMode.Read | FieldMode.Write, name: "P_FAIL")
                .WithValueField(4, 2, out eccStatus, FieldMode.Read, name: "ECCS")
                .WithReservedBits(6, 2);

            RegisterType.DieSelect.Define(this, 0x00)
                .WithReservedBits(0, 6)
                .WithFlag(6, out dieSelect, name: "DS0")
                .WithReservedBits(7, 1);
        }

        private State state;
        private int readIdIndex;
        private uint currentColumnAddress;
        private byte featureRegisterAddress;
        private bool isRandomProgramLoad;

        private readonly byte[] idBytes;
        private readonly byte[] pageBuffer;
        private readonly byte[] flashMemory;
        private readonly MappedMemory underlyingMemory;
        private readonly List<byte> addressBuffer;

        private readonly uint pageSize;
        private readonly uint spareSize;
        private readonly uint pagesPerBlock;
        private readonly uint blocksCount;
        private readonly byte manufacturerId;
        private readonly ushort deviceId;

        private IFlagRegisterField statusRegisterProtect0;
        private IFlagRegisterField statusRegisterProtect1;
        private IFlagRegisterField writeProtectEnable;
        private IValueRegisterField blockProtectBits;
        private IFlagRegisterField quadEnable;
        private IFlagRegisterField eccEnable;
        private IFlagRegisterField operationInProgress;
        private IFlagRegisterField writeEnableLatch;
        private IFlagRegisterField eraseFail;
        private IFlagRegisterField programFail;
        private IValueRegisterField eccStatus;
        private IFlagRegisterField dieSelect;

        private const uint DefaultPageSize = 2048;
        private const uint DefaultSpareSize = 64;
        private const uint DefaultPagesPerBlock = 64;
        private const uint DefaultBlocksCount = 1024;
        private const byte DefaultManufacturerId = 0x52; // Alliance Memory
        private const ushort DefaultDeviceId = 0x24;    // AS5F14G04SND

        private const int ColumnAddressByteCount = 2;
        private const int RowAddressByteCount = 3; // 1 dummy byte + 2 row address bytes
        private const byte EmptySegment = 0xFF;

        public enum RegisterType : byte
        {
            BlockLock = 0xA0,
            Config = 0xB0,
            Status = 0xC0,
            DieSelect = 0xD0
        }

        private enum State
        {
            Idle,
            ReadIdDummy,
            ReadIdData,
            GetFeatureAddress,
            GetFeatureData,
            SetFeatureAddress,
            SetFeatureData,
            PageReadAddress,
            ReadCacheAddress,
            ReadCacheDummy,
            ReadCacheData,
            ProgramLoadAddress,
            ProgramLoadData,
            ProgramExecuteAddress,
            BlockEraseAddress,
        }

        private enum Command : byte
        {
            Reset = 0xFF,
            ReadId = 0x9F,
            WriteEnable = 0x06,
            WriteDisable = 0x04,
            GetFeature = 0x0F,
            SetFeature = 0x1F,
            PageRead = 0x13,
            ReadFromCache = 0x03,
            FastReadFromCache = 0x0B,
            ReadFromCacheDual = 0x3B,
            ReadFromCacheQuad = 0x6B,
            ProgramLoad = 0x02,
            ProgramLoadRandom = 0x84,
            ProgramExecute = 0x10,
            BlockErase = 0xD8,
        }
    }
}
