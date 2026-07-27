//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H5_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32H5_FlashController(IMachine machine, MappedMemory flash) : base(machine)
        {
            this.flash = flash;
            nonSecureLock = new LockRegister(this, nameof(nonSecureLock), NonSecureKeys);

            DefineRegisters();
            Reset();
        }

        public GPIO IRQ { get; } = new GPIO();

        public override void Reset()
        {
            base.Reset();
            nonSecureLock.Reset();
            writeBufferCounter = 0;
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this, 0x00000000)
                .WithValueField(0, 4, name: "LATENCY")
                .WithValueField(4, 2, name: "WRHIGHFREQ")
                .WithReservedBits(6, 2)
                .WithFlag(8, name: "PRFTEN")
                .WithReservedBits(9, 23);

            Registers.NonSecureKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "NSKEY", writeCallback: (_, value) =>
                {
                    nonSecureLock.ConsumeValue((uint)value);
                    if(nonSecureLock.DisabledUntilReset)
                    {
                        this.Log(LogLevel.Warning,
                            "Bad unlock key 0x{0:X8} written to NSKEYR; flash stays locked until reset", value);
                    }
                });

            Registers.OperationStatus.Define(this, 0x00000000)
                .WithValueField(0, 20, FieldMode.Read, name: "ADDR_OP")
                .WithReservedBits(20, 1)
                .WithFlag(21, FieldMode.Read, name: "DATA_OP")
                .WithFlag(22, FieldMode.Read, name: "BK_OP")
                .WithFlag(23, FieldMode.Read, name: "SYSF_OP")
                .WithFlag(24, FieldMode.Read, name: "OTP_OP")
                .WithReservedBits(25, 4)
                .WithValueField(29, 3, FieldMode.Read, name: "CODE_OP");

            Registers.NonSecureStatus.Define(this, 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "BSY")
                .WithFlag(1, FieldMode.Read, name: "WBNE",
                    valueProviderCallback: _ => programWriteEnabled.Value && writeBufferCounter > 0)
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Read, name: "DBNE")
                .WithReservedBits(4, 12)
                .WithFlag(16, out eop, name: "EOP")
                .WithFlag(17, out wrperr, name: "WRPERR")
                .WithFlag(18, out pgserr, name: "PGSERR")
                .WithFlag(19, out strberr, name: "STRBERR")
                .WithFlag(20, out incerr, name: "INCERR")
                // OBKERR (bit 21), OBKWERR (bit 22), and OPTCHANGEERR (bit 23) are defined
                // but never set by this model. They always read 0. This is deliberate:
                // defining them keeps NSSR free of unhandled-bits warnings, which is required
                // by the full-bit-coverage acceptance criterion. The option-byte and OBK
                // functionality that would set these bits is out of scope for this version.
                .WithFlag(21, out obkerr, name: "OBKERR")
                .WithFlag(22, out obkwerr, name: "OBKWERR")
                .WithFlag(23, out optchangeerr, name: "OPTCHANGEERR")
                .WithReservedBits(24, 8);

            Registers.NonSecureControl.Define(this, 0x00000001)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "LOCK",
                    valueProviderCallback: _ => nonSecureLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            nonSecureLock.Lock();
                        }
                    })
                .WithFlag(1, out programWriteEnabled, name: "PG",
                    changeCallback: (_, val) => HandleProgramWriteEnableChange(val))
                .WithFlag(2, out sectorEraseRequest, name: "SER")
                .WithFlag(3, out bankEraseRequest, name: "BER")
                .WithFlag(4, FieldMode.Read | FieldMode.Set, name: "FW",
                    valueProviderCallback: _ => false,
                    writeCallback: (_, val) => { if(val) FinishProgramWrite(); })
                .WithFlag(5, FieldMode.Read | FieldMode.Set, name: "START",
                    valueProviderCallback: _ => false,
                    writeCallback: (_, val) => { if(val) PerformErase(); })
                .WithValueField(6, 7, out sectorNumber, name: "SNB")
                .WithReservedBits(13, 2)
                .WithFlag(15, out massEraseRequest, name: "MER")
                .WithFlag(16, out eopInterruptEnable, name: "EOPIE")
                .WithFlag(17, out wrperrInterruptEnable, name: "WRPERRIE")
                .WithFlag(18, out pgserrInterruptEnable, name: "PGSERRIE")
                .WithFlag(19, out strberrInterruptEnable, name: "STRBERRIE")
                .WithFlag(20, out incerrInterruptEnable, name: "INCERRIE")
                .WithFlag(21, out obkerrInterruptEnable, name: "OBKERRIE")
                .WithFlag(22, out obkwerrInterruptEnable, name: "OBKWERRIE")
                .WithFlag(23, out optchangeerrInterruptEnable, name: "OPTCHANGEERRIE")
                .WithReservedBits(24, 5)
                .WithFlag(29, name: "INV")
                .WithReservedBits(30, 1)
                .WithFlag(31, out bankSelect, name: "BKSEL");

            Registers.NonSecureClearControl.Define(this, 0x00000000)
                .WithReservedBits(0, 16)
                .WithFlag(16, FieldMode.Write, name: "CLR_EOP",
                    writeCallback: (_, val) => { if(val) eop.Value = false; })
                .WithFlag(17, FieldMode.Write, name: "CLR_WRPERR",
                    writeCallback: (_, val) => { if(val) wrperr.Value = false; })
                .WithFlag(18, FieldMode.Write, name: "CLR_PGSERR",
                    writeCallback: (_, val) => { if(val) pgserr.Value = false; })
                .WithFlag(19, FieldMode.Write, name: "CLR_STRBERR",
                    writeCallback: (_, val) => { if(val) strberr.Value = false; })
                .WithFlag(20, FieldMode.Write, name: "CLR_INCERR",
                    writeCallback: (_, val) => { if(val) incerr.Value = false; })
                .WithFlag(21, FieldMode.Write, name: "CLR_OBKERR",
                    writeCallback: (_, val) => { if(val) obkerr.Value = false; })
                .WithFlag(22, FieldMode.Write, name: "CLR_OBKWERR",
                    writeCallback: (_, val) => { if(val) obkwerr.Value = false; })
                .WithFlag(23, FieldMode.Write, name: "CLR_OPTCHANGEERR",
                    writeCallback: (_, val) => { if(val) optchangeerr.Value = false; })
                .WithReservedBits(24, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void PerformErase()
        {
            if(nonSecureLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Erase requested while flash is locked. Setting WRPERR.");
                wrperr.Value = true;
                UpdateInterrupts();
                return;
            }

            // MER (mass erase) has highest priority — erases both banks
            if(massEraseRequest.Value)
            {
                this.Log(LogLevel.Debug, "Mass erase: erasing both banks (0x{0:X} bytes)", FlashSizeDefault);
                flash.SetRange(0, FlashSizeDefault, 0xFF);
                eop.Value = true;
                UpdateInterrupts();
            }
            // BER (bank erase) — erase the bank selected by BKSEL
            else if(bankEraseRequest.Value)
            {
                var bankOffset = bankSelect.Value ? FlashBankSize : 0;
                this.Log(LogLevel.Debug, "Bank erase: bank {0}, offset 0x{1:X}, size 0x{2:X}",
                    bankSelect.Value ? 1 : 0, bankOffset, FlashBankSize);
                flash.SetRange(bankOffset, FlashBankSize, 0xFF);
                eop.Value = true;
                UpdateInterrupts();
            }
            // SER (sector erase) — erase the sector identified by BKSEL + SNB
            else if(sectorEraseRequest.Value)
            {
                var bank = bankSelect.Value ? 1 : 0;
                var snb = (long)sectorNumber.Value;
                var sectorStartAddr = bank * FlashBankSize + snb * FlashSectorSize;
                this.Log(LogLevel.Debug, "Sector erase: bank {0}, sector {1}, offset 0x{2:X}, size 0x{3:X}",
                    bank, snb, sectorStartAddr, FlashSectorSize);
                flash.SetRange(sectorStartAddr, FlashSectorSize, 0xFF);
                eop.Value = true;
                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Warning,
                    "START bit set but none of MER, BER, or SER are set. No erase performed.");
            }
        }

        private void HandleProgramWriteEnableChange(bool value)
        {
            if(value && nonSecureLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "PG set while flash is locked. Setting PGSERR.");
                pgserr.Value = true;
                UpdateInterrupts();
                return;
            }

            var cpus = machine.GetSystemBus(this).GetCPUs().OfType<ICPUWithMemoryAccessHooks>();
            foreach(var cpu in cpus)
            {
                cpu.SetHookAtMemoryAccess(value ? (MemoryAccessHook)OnMemoryProgramWrite : null);
            }

            writeBufferCounter = 0;
        }

        private void OnMemoryProgramWrite(ulong pc, MemoryOperation operation, ulong virtualAddress, ulong physicalAddress, uint width, ulong value)
        {
            if(operation != MemoryOperation.MemoryWrite)
            {
                return;
            }

            var writeTarget = machine.GetSystemBus(this).WhatIsAt(physicalAddress)?.Peripheral;
            if(writeTarget != flash)
            {
                return;
            }

            if(nonSecureLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Flash write while locked. Setting WRPERR.");
                wrperr.Value = true;
                UpdateInterrupts();
                return;
            }

            if(!programWriteEnabled.Value || incerr.Value)
            {
                pgserr.Value = true;
                UpdateInterrupts();
                return;
            }

            if(writeBufferCounter == 0)
            {
                writeBufferAddress = physicalAddress;
            }
            else if(writeBufferAddress + (ulong)writeBufferCounter != physicalAddress)
            {
                incerr.Value = true;
                UpdateInterrupts();
                return;
            }

            writeBufferCounter += (int)width;

            if(writeBufferCounter >= WriteBufferSize)
            {
                if(writeBufferCounter > WriteBufferSize)
                {
                    this.Log(LogLevel.Warning,
                        "More than the required number of bytes ({0} bytes) have been written to flash",
                        WriteBufferSize);
                }
                FinishProgramWrite();
            }
        }

        private void FinishProgramWrite()
        {
            writeBufferCounter = 0;
            eop.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var irqStatus = (eopInterruptEnable.Value && eop.Value)
                || (wrperrInterruptEnable.Value && wrperr.Value)
                || (pgserrInterruptEnable.Value && pgserr.Value)
                || (strberrInterruptEnable.Value && strberr.Value)
                || (incerrInterruptEnable.Value && incerr.Value)
                || (obkerrInterruptEnable.Value && obkerr.Value)
                || (obkwerrInterruptEnable.Value && obkwerr.Value)
                || (optchangeerrInterruptEnable.Value && optchangeerr.Value);
            this.DebugLog("Set IRQ: {0}", irqStatus);
            IRQ.Set(irqStatus);
        }

        // NSSR status fields
        private IFlagRegisterField eop;
        private IFlagRegisterField wrperr;
        private IFlagRegisterField pgserr;
        private IFlagRegisterField strberr;
        private IFlagRegisterField incerr;
        private IFlagRegisterField obkerr;
        private IFlagRegisterField obkwerr;
        private IFlagRegisterField optchangeerr;

        // NSCR control fields for erase operations
        private IFlagRegisterField sectorEraseRequest;
        private IFlagRegisterField bankEraseRequest;
        private IFlagRegisterField massEraseRequest;
        private IValueRegisterField sectorNumber;
        private IFlagRegisterField bankSelect;
        private IFlagRegisterField programWriteEnabled;

        // NSCR interrupt enable fields
        private IFlagRegisterField eopInterruptEnable;
        private IFlagRegisterField wrperrInterruptEnable;
        private IFlagRegisterField pgserrInterruptEnable;
        private IFlagRegisterField strberrInterruptEnable;
        private IFlagRegisterField incerrInterruptEnable;
        private IFlagRegisterField obkerrInterruptEnable;
        private IFlagRegisterField obkwerrInterruptEnable;
        private IFlagRegisterField optchangeerrInterruptEnable;

        private readonly MappedMemory flash;
        private readonly LockRegister nonSecureLock;

        private int writeBufferCounter;
        private ulong writeBufferAddress;

        private static readonly uint[] NonSecureKeys = { 0x45670123, 0xCDEF89AB };

        // Flash geometry: 2 MB total, 2 banks of 1 MB each, 128 sectors of 8 KB per bank
        private const long FlashSizeDefault = 0x200000;       // 2 MB
        private const long FlashBankSize = 0x100000;          // 1 MB
        private const long FlashSectorSize = 0x2000;          // 8 KB
        private const int WriteBufferSize = 16;               // 128-bit flash word

        private enum Registers : long
        {
            AccessControl = 0x00,
            NonSecureKey = 0x04,
            // --- Scope boundary: out-of-scope registers and areas ---
            //
            // The following registers and areas are deliberately left undefined so that any
            // access produces a warning-level log entry naming the peripheral and the offset,
            // rather than silently returning zero:
            //
            //   OPTKEYR   0x0C  — option byte key register
            //   OPTCR     0x1C  — option control register
            //   OBK area registers (option byte key storage)
            //   EDATA high-cycle area registers
            //   HDP and watermark configuration registers
            //   ECC registers: ECCCORR, ECCDETR, ECCDR
            //
            // This model implements no option-byte LockRegister, no OPTSTART PRG→CUR
            // copying, and no ECC status or error injection. Program and erase are the
            // scope boundary for this version.
            //
            OperationStatus = 0x18,
            NonSecureStatus = 0x20,
            NonSecureControl = 0x28,
            NonSecureClearControl = 0x30,
        }
    }
}
