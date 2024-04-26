//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class NPCX_Flash : ISPIPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public NPCX_Flash(NPCX_FIU fiu, MappedMemory memory)
        {
            this.owner = fiu;
            this.memory = memory;

            addressBuffer = new List<byte>();
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            writeEnabled = false;
            resetWriteEnable = false;
            FinishTransmission();
        }

        public byte Transmit(byte data)
        {
            byte returnValue = 0x0;
            if(!currentCommand.HasValue)
            {
                currentCommand = (Commands)data;
                // The following commands will only send 1 byte so they are
                // explicitly handled with the command latching logic
                switch(currentCommand.Value)
                {
                    case Commands.WriteEnable:
                        WriteEnabled = true;
                        break;
                    case Commands.WriteDisable:
                        WriteEnabled = false;
                        break;
                }
                return returnValue;
            }

            switch(currentCommand.Value)
            {
                case Commands.ReadStatusRegister1:
                    returnValue = RegistersCollection.Read((long)Registers.StatusRegister1);
                    break;
                case Commands.ReadStatusRegister2:
                    returnValue = RegistersCollection.Read((long)Registers.StatusRegister2);
                    break;
                case Commands.BlockErase64:
                    if(addressBuffer.Count < AddressByteCount)
                    {
                        addressBuffer.Add(data);
                    }

                    if(addressBuffer.Count != AddressByteCount)
                    {
                        break;
                    }

                    if(!WriteEnabled)
                    {
                        this.ErrorLog("Attempted to perform a Block Erase operation while flash is in write-disabled state. Operation will be ignored");
                        break;
                    }

                    var address = BitHelper.ToUInt32(addressBuffer.ToArray(), 0, AddressByteCount, true);
                    memory.SetRange(address, 64.KB(), 0xFF);
                    WriteEnabled = false;
                    break;
                case Commands.PageProgram:
                    if(addressBuffer.Count < AddressByteCount)
                    {
                        addressBuffer.Add(data);
                        if(addressBuffer.Count == AddressByteCount)
                        {
                            temporaryAddress = BitHelper.ToUInt32(addressBuffer.ToArray(), 0, AddressByteCount, true);
                        }
                        break;
                    }

                    if(!WriteEnabled)
                    {
                        this.ErrorLog("Attempted to perform a Page Program operation while flash is in write-disabled state. Operation will be ignored");
                        break;
                    }

                    memory.WriteByte(temporaryAddress, data);
                    var currentPage = temporaryAddress / PageProgramSize;
                    var nextPage = (temporaryAddress + 1) / PageProgramSize;
                    if(nextPage == currentPage)
                    {
                        temporaryAddress++;
                    }
                    else
                    {
                        // Address should wrap around to the start of the page
                        // when attempting to cross the page boundary
                        temporaryAddress = currentPage * PageProgramSize;
                    }
                    resetWriteEnable = true;
                    break;
                default:
                    if(Enum.IsDefined(typeof(Commands), currentCommand.Value))
                    {
                        this.WarningLog("Unsupported command: {0}", currentCommand.Value);
                    }
                    else
                    {
                        this.ErrorLog("Invalid command: {0}", currentCommand.Value);
                    }
                    break;
            }
            return returnValue;
        }

        public void FinishTransmission()
        {
            currentCommand = null;
            addressBuffer.Clear();
            temporaryAddress = 0x0;
            if(resetWriteEnable)
            {
                WriteEnabled = false;
            }
            resetWriteEnable = false;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.StatusRegister1.Define(this)
                .WithTaggedFlag("BUSY", 0)
                .WithFlag(1, name: "WEL (Write Enable Latch)",
                    valueProviderCallback: _ => WriteEnabled,
                    writeCallback: (_, value) => WriteEnabled = value)
                .WithTaggedFlag("BP0 (Block Protect Bits)", 2)
                .WithTaggedFlag("BP1 (Block Protect Bits)", 3)
                .WithTaggedFlag("BP2 (Block Protect Bits)", 4)
                .WithTaggedFlag("BP3 (Block Protect Bits)", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("SRP (Status Register Protect)", 7);

            Registers.StatusRegister2.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("QE (Quad Enable)", 1)
                .WithTaggedFlag("LB0 (Security Register Lock)", 2)
                .WithTaggedFlag("LB1 (Security Register Lock)", 3)
                .WithTaggedFlag("LB2 (Security Register Lock)", 4)
                .WithTaggedFlag("LB3 (Security Register Lock)", 5)
                .WithReservedBits(6, 2);
        }

        private bool WriteEnabled
        {
            get => writeEnabled;
            set
            {
                if(owner.IsLocked)
                {
                    // Locking FIU disables the ability to enable writing to flash
                    writeEnabled = false;
                    return;
                }
                writeEnabled = value;
            }
        }

        private Commands? currentCommand;

        private bool writeEnabled;
        private uint temporaryAddress;
        private bool resetWriteEnable;

        private readonly List<byte> addressBuffer;
        private readonly NPCX_FIU owner;
        private readonly MappedMemory memory;

        private const int PageProgramSize = 256;
        private const int AddressByteCount = 3;

        private enum Registers
        {
            StatusRegister1,
            StatusRegister2,
        }

        private enum Commands : byte
        {
            WriteEnable                             = 0x06,
            WriteEnableForVolatileStatusRegister    = 0x50,
            WriteDisable                            = 0x04,
            ReadStatusRegister1                     = 0x05,
            ReadStatusRegister2                     = 0x35,
            WriteStatusRegister                     = 0x01,
            ReadSFDPRegister                        = 0x5A,
            ReadData                                = 0x03,
            FastRead                                = 0x0B,
            FastReadDualIO                          = 0xBB,
            FastReadQuadIO                          = 0xEB,
            PageProgram                             = 0x02,
            SectorErase                             = 0x20, // Erases 4KB
            BlockErase32                            = 0x51, // Erases 32KB
            BlockErase64                            = 0xD8, // Erases 64KB
            ChipErase1                              = 0xC7,
            ChipErase2                              = 0x60,
            PowerDown                               = 0xB9,
            ReleasePowerDown                        = 0xAB,
            ManufacturerDeviceID                    = 0x90,
            JEDECID                                 = 0x9F,
        }
    }
}
