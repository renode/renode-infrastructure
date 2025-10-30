//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H7_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32H7_FlashController(IMachine machine, MappedMemory flash1, MappedMemory flash2) : base(machine)
        {
            banks = new Bank[NrOfBanks]
            {
                new Bank(this, 1, flash1),
                new Bank(this, 2, flash2),
            };

            optionControlLock = new LockRegister(this, nameof(optionControlLock), OptionControlKey);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var bank in banks)
            {
                bank.Reset();
            }
            optionControlLock.Reset();

            ProgramCurrentValues();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(optionControlLock.IsLocked && IsOffsetToProgramRegister(offset))
            {
                this.Log(LogLevel.Warning, "Attempted to write to a program register ({0}) while OPTLOCK bit is set. Ignoring", Enum.GetName(typeof(Registers), (Registers)offset));
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public void TriggerProgrammingSequenceError(int bankId) => TriggerError(bankId, Error.ProgrammingSequence);

        public void TriggerOperationError(int bankId) => TriggerError(bankId, Error.Operation);

        public void TriggerSingleEccError(int bankId) => TriggerError(bankId, Error.SingleECC);

        public void TriggerDoubleEccError(int bankId) => TriggerError(bankId, Error.DoubleECC);

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // Software writes to this register and expects written value back during reads to work correctly
            Registers.AccessControl.Define(this, 0x37)
                .WithValueField(0, 4, name: "LATENCY")
                .WithValueField(4, 2, name: "WRHIGHFREQ")
                .WithReservedBits(6, 26);

            Registers.OptionKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_OPTKEYR",
                    writeCallback: (_, value) => optionControlLock.ConsumeValue((uint)value));

            Registers.OptionControl.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "OPTLOCK", valueProviderCallback: _ => optionControlLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            optionControlLock.Lock();
                        }
                    })
                .WithFlag(1, FieldMode.Write, name: "OPTSTART", writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        if(optionControlLock.IsLocked)
                        {
                            this.Log(LogLevel.Warning, "Trying to start option byte change operation while the controller is locked. Ignoring");
                            return;
                        }

                        ProgramCurrentValues();
                    })
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "MER",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, val) => { if(val) MassErase(); })
                .WithReservedBits(5, 25)
                .WithTaggedFlag("OPTCHANGEERRIE", 30)
                .WithTaggedFlag("SWAP_BANK", 31);

            Registers.OptionStatusCurrent.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "FLASH_OPTSR_CUR", valueProviderCallback: _ => optionStatusCurrentValue);

            Registers.OptionStatusProgram.Define(this, 0x406AAF0, false)
                .WithValueField(0, 32, out optionStatusProgramRegister, name: "FLASH_OPTSR_PRG", softResettable: false);

            foreach(var bank in banks)
            {
                bank.DefineRegisters();
            }
        }

        private void TriggerError(int bankId, Error error)
        {
            if(bankId < 1 || bankId > NrOfBanks)
            {
                throw new RecoverableException($"Bank ID must be in range [1, {NrOfBanks}]. Ignoring operation.");
            }

            banks[bankId - 1].TriggerError(error);
        }

        private void UpdateInterrupts()
        {
            var irqStatus = banks.Any(bank => bank.IrqStatus);
            this.DebugLog("Set IRQ: {0}", irqStatus);
            IRQ.Set(irqStatus);
        }

        private void ProgramCurrentValues()
        {
            optionStatusCurrentValue = (uint)optionStatusProgramRegister.Value;
            foreach(var bank in banks)
            {
                bank.ProgramCurrentValues();
            }
        }

        private bool IsOffsetToProgramRegister(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.ProtectionAddressProgramBank1:
            case Registers.ProtectionAddressProgramBank2:
            case Registers.BootAddressProgram:
            case Registers.OptionStatusProgram:
            case Registers.SecureAddressProgramBank1:
            case Registers.SecureAddressProgramBank2:
            case Registers.WriteSectorProtectionProgramBank1:
            case Registers.WriteSectorProtectionProgramBank2:
                return true;
            default:
                return false;
            }
        }

        private void MassErase()
        {
            foreach(var bank in banks)
            {
                bank.HandleMassErase();
            }
        }

        private void OnMemoryProgramWrite(ulong _, MemoryOperation operation, ulong __, ulong physicalAddress, uint width, ulong ___)
        {
            if(operation != MemoryOperation.MemoryWrite)
            {
                return;
            }

            var writeTarget = machine.GetSystemBus(this).WhatIsAt(physicalAddress)?.Peripheral;
            foreach(var bank in banks)
            {
                bank.HandleMemoryProgramWrite(writeTarget, physicalAddress, width);
            }
        }

        private uint optionStatusCurrentValue;

        private IValueRegisterField optionStatusProgramRegister;

        private readonly LockRegister optionControlLock;

        private readonly Bank[] banks;

        private static readonly uint[] ControlBankKey = {0x45670123, 0xCDEF89AB};
        private static readonly uint[] OptionControlKey = {0x08192A3B, 0x4C5D6E7F};

        private const int NrOfBanks = 2;

        public enum Error
        {
            ProgrammingSequence,
            Operation,
            SingleECC,
            DoubleECC,
        }

        private class Bank
        {
            public Bank(STM32H7_FlashController parent, int id, MappedMemory memory)
            {
                this.parent = parent;
                this.Id = id;
                this.memory = memory;

                controlBankLock = new LockRegister(parent, $"{nameof(controlBankLock)}{id}", ControlBankKey);
            }

            public void Reset()
            {
                controlBankLock.Reset();
            }

            public void ProgramCurrentValues()
            {
                bankWriteProtectionCurrentValue = (byte)bankWriteProtectionProgramRegister.Value;
            }

            public void HandleMassErase()
            {
                bankEraseRequest.Value = true;
                BankErase();
            }

            public void TriggerError(Error error)
            {
                switch(error)
                {
                case Error.ProgrammingSequence:
                    bankProgrammingErrorStatus.Value = true;
                    break;
                case Error.Operation:
                    bankOperationErrorStatus.Value = true;
                    break;
                case Error.SingleECC:
                    bankSingleEccErrorStatus.Value = true;
                    break;
                case Error.DoubleECC:
                    bankDoubleEccErrorStatus.Value = true;
                    break;
                default:
                    parent.WarningLog("Invalid error type {0}. Ignoring operation.", error);
                    return;
                }
                parent.UpdateInterrupts();
            }

            public void HandleMemoryProgramWrite(IPeripheral writeTarget, ulong address, uint width)
            {
                if(writeTarget != memory)
                {
                    return;
                }

                // We don't monitor memory banks all the time because it's expensive.
                // But since the hook is already set up we might as well check if write is enabled.
                // It might not be if hook was set up by another bank.
                if(!bankWriteEnabled.Value || bankInconsistencyErrorStatus.Value)
                {
                    bankProgrammingErrorStatus.Value = true;
                    parent.UpdateInterrupts();
                    return;
                }

                if(bankWriteBufferCounter == 0)
                {
                    bankWriteBufferAddress = address;
                }
                // Writes have to be consecutive, otherwise incosistency error is raised.
                else if(bankWriteBufferAddress + (ulong)bankWriteBufferCounter != address)
                {
                    bankInconsistencyErrorStatus.Value = true;
                    parent.UpdateInterrupts();
                    return;
                }

                bankWriteBufferCounter += (int)width;

                if(bankWriteBufferCounter >= WriteBufferSize)
                {
                    if(bankWriteBufferCounter > WriteBufferSize)
                    {
                        // Manual doesn't describe what should happen in this case, hence instead of
                        // setting any error, we only show warning.
                        parent.WarningLog(
                            "More than the required number of bytes (32 bytes) have been written to the Flash Bank {0}",
                            Id
                        );
                    }
                    FinishProgramWrite();
                }
            }

            public void DefineRegisters()
            {
                var bankOffset = (Id - 1) * BanksOffset;

                (Registers.KeyBank1 + bankOffset).Define(parent)
                    .WithValueField(0, 32, FieldMode.Write, name: $"FLASH_KEYR{Id}",
                        writeCallback: (_, value) => controlBankLock.ConsumeValue((uint)value));

                (Registers.ControlBank1 + bankOffset).Define(parent, 0x31)
                    .WithFlag(0, FieldMode.Read | FieldMode.Set, name: $"LOCK{Id}",
                        valueProviderCallback: _ => controlBankLock.IsLocked,
                        changeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                controlBankLock.Lock();
                            }
                        })
                    .WithFlag(1, out bankWriteEnabled, name: $"PG{Id}",
                        changeCallback: (_, val) => HandleProgramWriteEnableChange(val))
                    .WithFlag(2, out bankSectorEraseRequest, name: $"SER{Id}")
                    .WithFlag(3, out bankEraseRequest, name: $"BER{Id}")
                    .WithTag($"PSIZE{Id}", 4, 2)
                    .WithFlag(6, FieldMode.Set | FieldMode.Read, name: $"FW{Id}",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, val) => { if(val) FinishProgramWrite(); })
                    .WithFlag(7, FieldMode.Set | FieldMode.Read, name: $"START{Id}",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, val) => { if(val) BankErase(); })
                    .WithValueField(8, 3, out bankSectorEraseNumber, name: $"SNB{Id}")
                    .WithReservedBits(11, 4)
                    .WithTaggedFlag($"CRC_EN", 15)
                    .WithFlag(16, out bankEndOfProgramIrqEnabled, name: $"EOPIE{Id}")
                    .WithTaggedFlag($"WRPERRIE{Id}", 17)
                    .WithFlag(18, out bankProgrammingErrorIrqEnable, name: $"PGSERRIE{Id}")
                    .WithTaggedFlag($"STRBERRIE{Id}", 19)
                    .WithReservedBits(20, 1)
                    .WithFlag(21, out bankInconsistencyErrorIrqEnable, name: $"INCERRIE{Id}")
                    .WithFlag(22, out bankOperationErrorIrqEnable, name: $"OPERRIE{Id}")
                    .WithTaggedFlag($"RDPERRIE{Id}", 23)
                    .WithTaggedFlag($"RDSERRIE{Id}", 24)
                    .WithFlag(25, out bankSingleEccErrorIrqEnable, name: $"SNECCERRIE{Id}")
                    .WithFlag(26, out bankDoubleEccErrorIrqEnable, name: $"DBECCERRIE{Id}")
                    .WithTaggedFlag($"CRCENDIE{Id}", 27)
                    .WithTaggedFlag($"CRCRDERRIE{Id}", 28)
                    .WithReservedBits(29, 3);

                (Registers.StatusBank1 + bankOffset).Define(parent)
                    .WithTaggedFlag($"BSY{Id}", 0)
                    .WithFlag(1, FieldMode.Read, name: $"WBNE{Id}",
                        valueProviderCallback: _ => bankWriteEnabled.Value && bankWriteBufferCounter > 0)
                    .WithTaggedFlag($"QW{Id}", 2)
                    .WithTaggedFlag($"CRC_BUSY{Id}", 3)
                    .WithReservedBits(4, 12)
                    .WithFlag(16, out bankEndOfProgramIrqStatus, name: $"EOP{Id}")
                    .WithTaggedFlag($"WRPERR{Id}", 17)
                    .WithFlag(18, out bankProgrammingErrorStatus, FieldMode.Read, name: $"PGSERR{Id}")
                    .WithTaggedFlag($"STRBERR{Id}", 19)
                    .WithReservedBits(20, 1)
                    .WithFlag(21, out bankInconsistencyErrorStatus, FieldMode.Read, name: $"INCERR{Id}")
                    .WithFlag(22, out bankOperationErrorStatus, FieldMode.Read, name: $"OPERR{Id}")
                    .WithTaggedFlag($"RDPERR{Id}", 23)
                    .WithTaggedFlag($"RDSERR{Id}", 24)
                    .WithFlag(25, out bankSingleEccErrorStatus, FieldMode.Read, name: $"SNECCERR{Id}")
                    .WithFlag(26, out bankDoubleEccErrorStatus, FieldMode.Read, name: $"DBECCERR{Id}")
                    .WithTaggedFlag($"CRCEND{Id}", 27)
                    .WithReservedBits(28, 4);

                (Registers.ClearControlBank1 + bankOffset).Define(parent)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, FieldMode.Set, name: $"CLR_EOP{Id}",
                        writeCallback: (_, val) => { if(val) bankEndOfProgramIrqStatus.Value = false; })
                    .WithTaggedFlag($"CLR_WRPERR{Id}", 17)
                    .WithFlag(18, FieldMode.Set, name: $"CLR_PGSERR{Id}",
                        writeCallback: (_, val) => { if(val) bankProgrammingErrorStatus.Value = false; })
                    .WithTaggedFlag($"CLR_STRBERR{Id}", 19)
                    .WithReservedBits(20, 1)
                    .WithFlag(21, FieldMode.Set, name: $"CLR_INCERR{Id}",
                        writeCallback: (_, val) => { if(val) bankInconsistencyErrorStatus.Value = false; })
                    .WithFlag(22, FieldMode.Set, name: $"CLR_OPERR{Id}",
                        writeCallback: (_, val) => { if(val) bankOperationErrorStatus.Value = false; })
                    .WithTaggedFlag($"CLR_RDPERR{Id}", 23)
                    .WithTaggedFlag($"CLR_RDSERR{Id}", 24)
                    .WithFlag(25, FieldMode.Set, name: $"CLR_SNECCERR{Id}",
                        writeCallback: (_, val) => { if(val) bankSingleEccErrorStatus.Value = false; })
                    .WithFlag(26, FieldMode.Set, name: $"CLR_DBECCERR{Id}",
                        writeCallback: (_, val) => { if(val) bankDoubleEccErrorStatus.Value = false; })
                    .WithTaggedFlag($"CLR_CRCEND{Id}", 27)
                    .WithReservedBits(28, 4)
                    .WithWriteCallback((_, __) => parent.UpdateInterrupts());

                (Registers.WriteSectorProtectionCurrentBank1 + bankOffset).Define(parent)
                    .WithValueField(0, 8, FieldMode.Read, name: $"WRPSn{Id}", valueProviderCallback: _ => bankWriteProtectionCurrentValue)
                    .WithReservedBits(8, 24);

                (Registers.WriteSectorProtectionProgramBank1 + bankOffset).Define(parent, 0xFF, false)
                    .WithValueField(0, 8, out bankWriteProtectionProgramRegister, name: $"WRPSn{Id}", softResettable: false)
                    .WithReservedBits(8, 24);
            }

            public int Id { get; }

            public bool IrqStatus => (bankEndOfProgramIrqEnabled.Value && bankEndOfProgramIrqStatus.Value) ||
                                     (bankInconsistencyErrorIrqEnable.Value && bankInconsistencyErrorStatus.Value) ||
                                     (bankProgrammingErrorIrqEnable.Value && bankProgrammingErrorStatus.Value) ||
                                     (bankOperationErrorIrqEnable.Value && bankOperationErrorStatus.Value) ||
                                     (bankSingleEccErrorIrqEnable.Value && bankSingleEccErrorStatus.Value) ||
                                     (bankDoubleEccErrorIrqEnable.Value && bankDoubleEccErrorStatus.Value);

            public bool WriteEnabled => bankWriteEnabled.Value;

            private void BankErase()
            {
                // Bank erase operation has higher priority than sector erase operation
                if(bankEraseRequest.Value)
                {
                    memory.SetRange(0, memory.Size, 0xff);
                    bankEndOfProgramIrqStatus.Value = true;
                    parent.UpdateInterrupts();
                }
                else if(bankSectorEraseRequest.Value)
                {
                    var sectorIdx = bankSectorEraseNumber.Value;
                    var sectorStartAddr = (long)(sectorIdx * SectorSize);
                    memory.SetRange(sectorStartAddr, SectorSize, 0xff);
                    bankEndOfProgramIrqStatus.Value = true;
                    parent.UpdateInterrupts();
                }
                else
                {
                    parent.WarningLog(
                        "Trying to perform a bank erase operation but neither Bank Erase Request nor Sector Erase Request was selected."
                    );
                }
            }

            private void HandleProgramWriteEnableChange(bool value)
            {
                var areOtherBanksInWriteState = parent
                    .banks
                    .Any(bank => bank.Id != Id && bank.WriteEnabled);

                // Entering write state, when another bank is already in write state doesn't require setting up hooks,
                // as they already should be configured.
                // When leaving write state we also shouldn't remove hooks as other bank is still using them.
                if(!areOtherBanksInWriteState)
                {
                    var cpus = parent.machine.GetSystemBus(parent).GetCPUs().OfType<ICPUWithMemoryAccessHooks>();
                    foreach(var cpu in cpus)
                    {
                        cpu.SetHookAtMemoryAccess(value ? (MemoryAccessHook)parent.OnMemoryProgramWrite : null);
                    }
                }

                bankWriteBufferCounter = 0;
            }

            private void FinishProgramWrite()
            {
                bankWriteBufferCounter = 0;
                bankEndOfProgramIrqStatus.Value = true;
                parent.UpdateInterrupts();
            }

            private IValueRegisterField bankWriteProtectionProgramRegister;
            private IFlagRegisterField bankEraseRequest;
            private IFlagRegisterField bankSectorEraseRequest;
            private IValueRegisterField bankSectorEraseNumber;
            private IFlagRegisterField bankEndOfProgramIrqEnabled;
            private IFlagRegisterField bankEndOfProgramIrqStatus;
            private IFlagRegisterField bankWriteEnabled;
            private IFlagRegisterField bankInconsistencyErrorIrqEnable;
            private IFlagRegisterField bankInconsistencyErrorStatus;
            private IFlagRegisterField bankProgrammingErrorIrqEnable;
            private IFlagRegisterField bankProgrammingErrorStatus;
            private IFlagRegisterField bankOperationErrorIrqEnable;
            private IFlagRegisterField bankOperationErrorStatus;
            private IFlagRegisterField bankSingleEccErrorIrqEnable;
            private IFlagRegisterField bankSingleEccErrorStatus;
            private IFlagRegisterField bankDoubleEccErrorIrqEnable;
            private IFlagRegisterField bankDoubleEccErrorStatus;

            private byte bankWriteProtectionCurrentValue;
            private int bankWriteBufferCounter;
            private ulong bankWriteBufferAddress;

            private readonly STM32H7_FlashController parent;
            private readonly MappedMemory memory;

            private readonly LockRegister controlBankLock;

            private const int BanksOffset = 0x100;
            private const int SectorSize = 0x20000; // 128KiB
            private const int WriteBufferSize = 32;
        }

        private enum Registers
        {
            AccessControl = 0x000,                      // FLASH_ACR
            KeyBank1 = 0x004,                           // FLASH_KEYR1
            OptionKey = 0x008,                          // FLASH_OPTKEYR
            ControlBank1 = 0x00C,                       // FLASH_CR1
            StatusBank1 = 0x010,                        // FLASH_SR1
            ClearControlBank1 = 0x014,                  // FLASH_CCR1
            OptionControl = 0x018,                      // FLASH_OPTCR
            OptionStatusCurrent = 0x01C,                // FLASH_OPTSR_CUR
            OptionStatusProgram = 0x020,                // FLASH_OPTSR_PRG
            OptionClearControl = 0x024,                 // FLASH_OPTCCR
            ProtectionAddressCurrentBank1 = 0x028,      // FLASH_PRAR_CUR1
            ProtectionAddressProgramBank1 = 0x02C,      // FLASH_PRAR_PRG1
            SecureAddressCurrentBank1 = 0x030,          // FLASH_SCAR_CUR1
            SecureAddressProgramBank1 = 0x034,          // FLASH_SCAR_PRG1
            WriteSectorProtectionCurrentBank1 = 0x038,  // FLASH_WPSN_CUR1R
            WriteSectorProtectionProgramBank1 = 0x03C,  // FLASH_WPSN_PRG1R
            BootAddressCurrent = 0x040,                 // FLASH_BOOT_CURR
            BootAddressProgram = 0x044,                 // FLASH_BOOT_PRGR
            CRCControlBank1 = 0x050,                    // FLASH_CRCCR1
            CRCStartAddressBank1 = 0x054,               // FLASH_CRCSADD1R
            CRCEndAddressBank1 = 0x058,                 // FLASH_CRCEADD1R
            CRCData = 0x05C,                            // FLASH_CRCDATAR
            ECCFailAddressBank1 = 0x060,                // FLASH_ECC_FA1R
            KeyBank2 = 0x104,                           // FLASH_KEYR2
            ControlBank2 = 0x10C,                       // FLASH_CR2
            StatusBank2 = 0x110,                        // FLASH_SR2
            ClearControlBank2 = 0x114,                  // FLASH_CCR2
            ProtectionAddressCurrentBank2 = 0x128,      // FLASH_PRAR_CUR2
            ProtectionAddressProgramBank2 = 0x12C,      // FLASH_PRAR_PRG2
            SecureAddressCurrentBank2 = 0x130,          // FLASH_SCAR_CUR2
            SecureAddressProgramBank2 = 0x134,          // FLASH_SCAR_PRG2
            WriteSectorProtectionCurrentBank2 = 0x138,  // FLASH_WPSN_CUR2R
            WriteSectorProtectionProgramBank2 = 0x13C,  // FLASH_WPSN_PRG2R
            CRCControlBank2 = 0x150,                    // FLASH_CRCCR2
            CRCStartAddressBank2 = 0x154,               // FLASH_CRCSADD2R
            CRCEndAddressBank2 = 0x158,                 // FLASH_CRCEADD2R
            ECCFailAddressBank2 = 0x160,                // FLASH_ECC_FA2R
        }
    }
}
