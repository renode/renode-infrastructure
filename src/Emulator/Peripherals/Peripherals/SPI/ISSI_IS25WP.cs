//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.SPI.NORFlash;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class ISSI_IS25WP : GenericSpiFlash
    {
        public ISSI_IS25WP(MappedMemory underlyingMemory, BlockSize blockSize = BlockSize._256KB) : base(underlyingMemory, ManufacturerId, MemoryType, capacityCode: (byte)Misc.Logarithm2((int)underlyingMemory.Size), sectorSizeKB: SectorSizeInKB)
        {
            if(!Enum.IsDefined(typeof(BlockSize), blockSize))
            {
                throw new ConstructionException($"{blockSize} is invalid value for '{nameof(blockSize)}'");
            }

            if(!Misc.IsPowerOfTwo((ulong)underlyingMemory.Size))
            {
                throw new ConstructionException($"'{nameof(underlyingMemory)}' size should be a power of 2");
            }

            if(underlyingMemory.Size % 64.KB() > 0)
            {
                throw new ConstructionException($"'{nameof(underlyingMemory)}' size should be divisible by 64KB");
            }

            bankAddressRegister = new ByteRegister(this)
                .WithFlag(0, name: "BA24",
                    valueProviderCallback: _ => BitHelper.IsBitSet(additionalAddressMask, 24),
                    writeCallback: (_, value) => BitHelper.SetBit(ref additionalAddressMask, 24, value))
                .WithFlag(1, name: "BA25",
                    valueProviderCallback: _ => BitHelper.IsBitSet(additionalAddressMask, 25),
                    writeCallback: (_, value) => BitHelper.SetBit(ref additionalAddressMask, 25, value))
                .WithReservedBits(2, 5)
                .WithFlag(7, out extendedAddressing, name: "EXTADD")
            ;

            advancedSectorBlockProtectionRegister = new WordRegister(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("PSTMLB", 1)
                .WithTaggedFlag("PWDMLB", 2)
                .WithReservedBits(3, 12)
                .If(blockSize == BlockSize._64KB)
                    .Then(register => register.WithEnumField<WordRegister, SubblocksLocation>(15, 1, name: "TBPARM",
                        valueProviderCallback: _ => subblocksLocation,
                        writeCallback: (_, value) => subblocksLocation = value))
                    .Else(register => register.WithReservedBits(15, 1))
            ;

            passwordRegister = new QuadWordRegister(this)
                .WithTag("PSWD", 0, 64)
            ;

            this.blockSize = blockSize;

            var sectorsNum = 0L;
            switch(blockSize)
            {
            case BlockSize._64KB:
                sectorsNum = 32; // Top or bottom 4KB sectors
                sectorsNum += (underlyingMemory.Size - SubsectorSizeInKB.KB()) / 64.KB(); // Rest of the memory is divided in 64KB blocks
                break;
            case BlockSize._256KB:
                sectorsNum = underlyingMemory.Size / 256.KB(); // Memory is uniformly divided in 256KB blocks
                break;
            default:
                throw new Exception("unreachable");
            }

            blockConfiguration = Misc.Iterate(BlockConfiguration.Create)
                .Take((int)sectorsNum)
                .ToArray()
            ;
        }

        protected override byte ReadFromMemory()
        {
            if(currentOperation.AddressLength == 3)
            {
                // NOTE: If we are accessing the memory using 3-byte address, extend it with BA24/BA25
                currentOperation.ExecutionAddress |= additionalAddressMask;
            }
            return base.ReadFromMemory();
        }

        protected override void WriteToMemory(byte val)
        {
            if(currentOperation.AddressLength == 3)
            {
                // NOTE: If we are accessing the memory using 3-byte address, extend it with BA24/BA25
                currentOperation.ExecutionAddress |= additionalAddressMask;
            }

            if(!TryVerifyWriteToMemory(out var position))
            {
                return;
            }

            var blockNumber = TranslateAddressToBlock(position);
            if(blockConfiguration[blockNumber].Protected)
            {
                this.Log(LogLevel.Warning, "Tried to write to 0x{0:X} but it's protected; PPB={1:X02}, DYB={2:X02}",
                    position, blockConfiguration[blockNumber].PPB ? 0xFF : 0x00, blockConfiguration[blockNumber].DYB ? 0xFF : 0x00);
                return;
            }

            underlyingMemory.WriteByte(position, val);
        }

        protected override void WriteRegister(Register register, byte data)
        {
            if(!Enum.IsDefined(typeof(InternalRegisters), (uint)register))
            {
                base.WriteRegister(register, data);
                return;
            }

            if(!WriteEnable)
            {
                this.Log(LogLevel.Error, "Trying to write a register, but write enable latch is not set");
                return;
            }

            switch((InternalRegisters)register)
            {
            case InternalRegisters.BankAddressRegister:
                bankAddressRegister.Write(0, data);
                return;

            case InternalRegisters.AdvancedSectorBlockProtection:
                if(currentOperation.CommandBytesHandled < 2)
                {
                    var value = (uint)advancedSectorBlockProtectionRegister.Read();
                    BitHelper.SetMaskedValue(ref value, data, 8 * currentOperation.CommandBytesHandled, 8);
                    advancedSectorBlockProtectionRegister.Write(0, (ushort)value);
                }

                this.Log(LogLevel.Warning, "Tried to repeatedly write {0} register", Enum.GetName(typeof(InternalRegisters), register));
                return;

            case InternalRegisters.PasswordRegister:
                if(currentOperation.CommandBytesHandled < 8)
                {
                    var value = passwordRegister.Read();
                    BitHelper.SetMaskedValue(ref value, data, 8 * currentOperation.CommandBytesHandled, 8);
                    passwordRegister.Write(0, value);
                    return;
                }

                this.Log(LogLevel.Warning, "Tried to repeatedly write {0} register", Enum.GetName(typeof(InternalRegisters), register));
                return;

            case InternalRegisters.PersistentProtectionBitsRegister:
                if(!persistentProtectionBitsLockBit)
                {
                    this.Log(LogLevel.Warning, "Tried to write PPB register with lock bit zeroed");
                    return;
                }

                if(currentOperation.ExecutionAddress >= blockConfiguration.Length)
                {
                    this.Log(LogLevel.Warning, "Tried to write invalid PPB index: {0}", currentOperation.ExecutionAddress);
                }

                blockConfiguration[currentOperation.ExecutionAddress].PPB = data != 0;
                return;

            case InternalRegisters.DynamicProtectionBitsRegister:
                if(currentOperation.ExecutionAddress >= blockConfiguration.Length)
                {
                    this.Log(LogLevel.Warning, "Tried to write invalid DYB index: {0}", currentOperation.ExecutionAddress);
                }

                blockConfiguration[currentOperation.ExecutionAddress].DYB = data != 0;
                return;

            default:
                // NOTE: This is not possible as we have already checked that register is defined in InternalRegisters
                throw new Exception("unreachable");
            }
        }

        protected override byte ReadRegister(Register register)
        {
            if(!Enum.IsDefined(typeof(InternalRegisters), (uint)register))
            {
                return base.ReadRegister(register);
            }

            switch((InternalRegisters)register)
            {
            case InternalRegisters.BankAddressRegister:
                return bankAddressRegister.Read();

            case InternalRegisters.AdvancedSectorBlockProtection:
                if(currentRegisterValue == null)
                {
                    var bytes = BitHelper.GetBytesFromValue(advancedSectorBlockProtectionRegister.Read(), 2);
                    currentRegisterValue = new Queue<byte>(bytes);
                }
                break;

            case InternalRegisters.PasswordRegister:
                if(currentRegisterValue == null)
                {
                    var bytes = BitHelper.GetBytesFromValue(advancedSectorBlockProtectionRegister.Read(), 8);
                    currentRegisterValue = new Queue<byte>(bytes);
                }
                break;

            case InternalRegisters.PersistentProtectionBitsLockBitRegister:
                var registerValue = 0U;
                BitHelper.SetBit(ref registerValue, bit: 0 /* PPBLK */, value: persistentProtectionBitsLockBit);
                BitHelper.SetBit(ref registerValue, bit: 7 /* FREEZE */, value: freezeBit);
                return (byte)registerValue;

            case InternalRegisters.PersistentProtectionBitsRegister:
                if(currentOperation.ExecutionAddress >= blockConfiguration.Length)
                {
                    this.Log(LogLevel.Warning, "Tried to read PPB of {0} block > {1}; returning zero",
                        currentOperation.ExecutionAddress,
                        blockConfiguration.Length);
                    return 0;
                }
                return blockConfiguration[currentOperation.ExecutionAddress].PPB ? (byte)0xFF : (byte)0x00;

            case InternalRegisters.DynamicProtectionBitsRegister:
                if(currentOperation.ExecutionAddress >= blockConfiguration.Length)
                {
                    this.Log(LogLevel.Warning, "Tried to read DYB of {0} block > {1}; returning zero",
                        currentOperation.ExecutionAddress,
                        blockConfiguration.Length);
                    return 0;
                }
                return blockConfiguration[currentOperation.ExecutionAddress].DYB ? (byte)0xFF : (byte)0x00;

            default:
                // NOTE: This is not possible as we have already checked that register is defined in InternalRegisters
                throw new Exception("unreachable");
            }

            // NOTE: Explicit declaration is required by mono compiler
            byte data = default(byte);
            if(currentRegisterValue?.TryDequeue(out data) ?? false)
            {
                return data;
            }

            this.Log(LogLevel.Warning, "Tried to read past data stored in {0}; returning 0", register);
            return 0;
        }

        protected override void RecognizeOperation(byte firstByte)
        {
            if(!Enum.IsDefined(typeof(InternalCommands), firstByte))
            {
                // NOTE: Not defined in our overrides; use the normal path
                base.RecognizeOperation(firstByte);
                return;
            }

            currentOperation.CommandBytesHandled = 0;
            currentOperation.State = DecodedOperation.OperationState.HandleCommand;

            switch((InternalCommands)firstByte)
            {
            case InternalCommands.ReadAdvancedSectorBlockProtectionRegister:
                currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                currentOperation.Register = (uint)InternalRegisters.AdvancedSectorBlockProtection;
                break;

            case InternalCommands.ProgramAdvancedSectorBlockProtectionRegister:
                currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                currentOperation.Register = (uint)InternalRegisters.AdvancedSectorBlockProtection;
                break;

            case InternalCommands.ReadPassword:
                currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                currentOperation.Register = (uint)InternalRegisters.PasswordRegister;
                break;

            case InternalCommands.ProgramPassword:
                currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                currentOperation.Register = (uint)InternalRegisters.PasswordRegister;
                break;

            case InternalCommands.UnlockPassword:
                this.Log(LogLevel.Warning, "Password Protection Mode is not supported; unlock password ignored");
                break;

            case InternalCommands.ReadPersistentProtectionBitsRegister:
                currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                currentOperation.Register = (uint)InternalRegisters.PersistentProtectionBitsRegister;
                currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                currentOperation.AddressLength = NormalAddressLength;
                break;

            case InternalCommands.ProgramPersistentProtectionBitsRegister:
                currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                currentOperation.Register = (uint)InternalRegisters.PersistentProtectionBitsRegister;
                currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                currentOperation.AddressLength = NormalAddressLength;
                break;

            case InternalCommands.ErasePersistentProtectionBitsRegister:
                // NOTE: If we can, erase all PPB bits
                if(persistentProtectionBitsLockBit)
                {
                    this.Log(LogLevel.Warning, "Tried to erase PPB array while locked");
                    return;
                }
                for(var i = 0; i < blockConfiguration.Length; ++i)
                {
                    blockConfiguration[i].PPB = true;
                }
                break;

            case InternalCommands.ReadDynamicProtectionBitsRegister:
                currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                currentOperation.Register = (uint)InternalRegisters.DynamicProtectionBitsRegister;
                currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                currentOperation.AddressLength = NormalAddressLength;
                break;

            case InternalCommands.ProgramDynamicProtectionBitsRegister:
                currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                currentOperation.Register = (uint)InternalRegisters.DynamicProtectionBitsRegister;
                currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                currentOperation.AddressLength = NormalAddressLength;
                break;

            case InternalCommands.ReadPersistentProtectionBitsLockBit:
                currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                currentOperation.Register = (uint)InternalRegisters.PersistentProtectionBitsLockBitRegister;
                break;

            case InternalCommands.WritePersistentProtectionBitsLockBit:
                // NOTE: Just set the bit
                persistentProtectionBitsLockBit = true;
                break;

            case InternalCommands.SetFreezeBit:
                // NOTE: Just set the bit
                freezeBit = true;
                break;

            default:
                // NOTE: This is not possible as we have already checked that firstByte is defined in InternalCommands
                throw new Exception("unreachable");
            }
        }

        private int TranslateAddressToBlock(long address)
        {
            switch(blockSize)
            {
            case BlockSize._64KB:
                // NOTE: Depending on subblockLocation, either two bottom blocks or two top blocks are split to 4KB blocks
                var blockNumber = 0;
                if(subblocksLocation == SubblocksLocation.Bottom)
                {
                    blockNumber += (int)Math.Min(SubsectorSizeInKB.KB(), address) / 4.KB();
                    address -= Math.Min(SubsectorSizeInKB.KB(), address);
                }

                var fullBlocks = (int)(address / 64.KB());
                var maximumFullBlocks = (int)(underlyingMemory.Size / 64.KB()) - 2;
                if(fullBlocks < maximumFullBlocks)
                {
                    return blockNumber + fullBlocks;
                }

                fullBlocks = Math.Min(fullBlocks, maximumFullBlocks);
                blockNumber += fullBlocks;
                address -= fullBlocks * 64.KB();

                if(subblocksLocation == SubblocksLocation.Top)
                {
                    blockNumber += (int)Math.Min(SubsectorSizeInKB.KB(), address) / 4.KB();
                    address -= Math.Min(SubsectorSizeInKB.KB(), address);
                }

                return blockNumber;

            case BlockSize._256KB:
                return (int)(address / 256.KB());

            default:
                throw new Exception("unreachable");
            }
        }

        private int NormalAddressLength => extendedAddressing.Value ? 4 : 3;

        private Queue<byte> currentRegisterValue;

        private bool persistentProtectionBitsLockBit;
        private bool freezeBit;
        private SubblocksLocation subblocksLocation;
        private uint additionalAddressMask;

        private readonly IFlagRegisterField extendedAddressing;
        private readonly BlockConfiguration[] blockConfiguration;
        private readonly ByteRegister bankAddressRegister;
        private readonly WordRegister advancedSectorBlockProtectionRegister;
        private readonly QuadWordRegister passwordRegister;
        private readonly BlockSize blockSize;

        private const byte ManufacturerId = 0x9D;
        private const byte MemoryType = 0x70;
        private const byte CapacityCode = 0x1A;
        private const int SectorSizeInKB = 4;
        private const int SubsectorSizeInKB = 128;

        public enum BlockSize
        {
            _64KB,
            _256KB,
        }

        private struct BlockConfiguration
        {
            public static BlockConfiguration Create()
            {
                var self = new BlockConfiguration();

                self.PPB = true;
                self.DYB = true;
                return self;
            }

            // NOTE: Persistent Protection Bits
            public bool PPB { get; set; }

            // NOTE: Dynamic Protection Bits
            public bool DYB { get; set; }

            public bool Protected => !PPB || !DYB;
        }

        private enum InternalCommands : byte
        {
            ReadBankAddressRegister = 0x16,
            WriteBankAddressRegisterVolatile = 0x17,
            WriteBankAddressRegisterNonVolatile = 0x18,

            ReadAdvancedSectorBlockProtectionRegister = 0x2B,
            ProgramAdvancedSectorBlockProtectionRegister = 0x2F,

            ReadPassword = 0xE7,
            ProgramPassword = 0xE8,
            UnlockPassword = 0xE9,

            ReadPersistentProtectionBitsLockBit = 0xA7,
            WritePersistentProtectionBitsLockBit = 0xA6,
            SetFreezeBit = 0x91,

            ReadPersistentProtectionBitsRegister = 0xFC,
            ProgramPersistentProtectionBitsRegister = 0xFD,
            ErasePersistentProtectionBitsRegister = 0xE4,

            ReadDynamicProtectionBitsRegister = 0xFA,
            ProgramDynamicProtectionBitsRegister = 0xFB,
        }

        private enum InternalRegisters : uint
        {
            BankAddressRegister = Register.FirstNonstandardRegister,
            AdvancedSectorBlockProtection,
            PasswordRegister,
            PersistentProtectionBitsLockBitRegister,
            PersistentProtectionBitsRegister,
            DynamicProtectionBitsRegister,
        }

        private enum SubblocksLocation
        {
            Top,
            Bottom,
        }
    }
}