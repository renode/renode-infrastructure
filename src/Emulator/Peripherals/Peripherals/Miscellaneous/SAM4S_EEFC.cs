//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SAM4S_EEFC : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM4S_EEFC(IMachine machine, IMemory underlyingMemory, uint flashIdentifier = DefaultFlashIdentifier,
            int pageSize = 512, int sectorSize = 64000, int lockRegionSize = 8000) : base(machine)
        {
            AssertPositive(nameof(pageSize), pageSize);
            AssertPositive(nameof(sectorSize), sectorSize);
            AssertPositive(nameof(lockRegionSize), lockRegionSize);

            if(underlyingMemory == null)
            {
                throw new ConstructionException($"'{nameof(underlyingMemory)}' can't be null");
            }

            if(underlyingMemory.Size % pageSize > 0)
            {
                throw new ConstructionException($"Size of '{nameof(underlyingMemory)}' is not divisible by page size ({pageSize})");
            }

            if(sectorSize % pageSize > 0)
            {
                throw new ConstructionException($"Sector size ({sectorSize}) is not divisible by page size ({pageSize})");
            }

            if(sectorSize % lockRegionSize > 0)
            {
                throw new ConstructionException($"Sector size ({sectorSize}) is not divisible by lock region size ({lockRegionSize})");
            }

            this.underlyingMemory = underlyingMemory;
            this.flashIdentifier = flashIdentifier;
            this.pageSize = pageSize;
            this.sectorSize = sectorSize;
            this.lockRegionSize = lockRegionSize;
            this.lockBits = new bool[NumberOfLockRegions];

            DefineRegisters();
        }

        public void EraseAll()
        {
            for(var i = 0; i < NumberOfPages; ++i)
            {
                ErasePage(i);
            }
        }

        public void ErasePage(int page)
        {
            if(page < 0 || page > NumberOfPages)
            {
                throw new RecoverableException($"'{nameof(page)}' should be between 0 and {NumberOfPages - 1}");
            }

            if(IsPageLocked(page))
            {
                triedWriteLocked.Value = true;
                this.Log(LogLevel.Debug, "Tried to erase page #{0} which is currently locked", page);
                return;
            }

            underlyingMemory.WriteBytes(page * pageSize, Enumerable.Repeat((byte)0xFF, pageSize).ToArray(), 0, pageSize);
        }

        public long Size => 0x200;

        public GPIO IRQ { get; } = new GPIO();

        public int NumberOfPages => (int)underlyingMemory.Size / pageSize;

        public int PagesInSector => sectorSize / pageSize;

        public int NumberOfLockRegions => (int)underlyingMemory.Size / lockRegionSize;

        private int PageToLockRegionIndex(int page)
        {
            return page * pageSize / lockRegionSize;
        }

        private bool IsPageLocked(int page)
        {
            return lockBits[PageToLockRegionIndex(page)];
        }

        private void ExecuteFlashCommand(Commands command, int argument)
        {
            this.Log(LogLevel.Debug, "Executing command {0}, argument 0x{1:X}", command, argument);

            // NOTE: We're busy, unset IRQ
            IRQ.Unset();

            switch(command)
            {
                case Commands.GetFlashDescriptor:
                    resultQueue.EnqueueRange(new uint[]
                    {
                        /* Flash interface description */ flashIdentifier,
                        /*         Flash size in bytes */ (uint)underlyingMemory.Size,
                        /*          Page size in bytes */ (uint)pageSize,
                        /*            Number of planes */ 1,
                        /* Number of bytes in plane #0 */ (uint)underlyingMemory.Size,
                        /*         Number of lock bits */ (uint)NumberOfLockRegions
                    });
                    // Number of bytes in (each) lock region
                    resultQueue.EnqueueRange(Enumerable.Repeat((uint)lockRegionSize, NumberOfLockRegions));
                    break;

                case Commands.WritePage:
                    // NOTE: This command does nothing, as changes are committed immediately to underlying memory
                    break;

                case Commands.WritePageAndLock:
                    // NOTE: This command effectively locks single page; see comment above
                    goto case Commands.SetLockBit;

                case Commands.ErasePageAndWritePage:
                    if(argument >= NumberOfPages)
                    {
                        commandError.Value = true;
                        return;
                    }
                    ErasePage(argument);
                    break;

                case Commands.ErasePageAndWritePageThenLock:
                    if(argument >= NumberOfPages)
                    {
                        commandError.Value = true;
                        return;
                    }
                    ErasePage(argument);
                    goto case Commands.SetLockBit;

                case Commands.EraseAll:
                    EraseAll();
                    break;

                case Commands.ErasePages:
                    var arg0 = argument & 0x3;
                    var arg1 = argument >> (2 + arg0);
                    var numberOfPages = 4 << arg0;
                    var pageStart = numberOfPages * arg1;

                    this.Log(LogLevel.Debug, "Erasing {0} pages starting from {1}", numberOfPages, pageStart);
                    for(var i = 0; i < numberOfPages && i + pageStart < NumberOfPages; ++i)
                    {
                        ErasePage(pageStart + i);
                    }
                    break;

                case Commands.SetLockBit:
                    if(argument >= NumberOfPages)
                    {
                        commandError.Value = true;
                        return;
                    }
                    lockBits[PageToLockRegionIndex(argument)] = true;
                    this.Log(LogLevel.Debug, "Locked region #{0}", PageToLockRegionIndex(argument));
                    break;

                case Commands.ClearLockBit:
                    if(argument >= NumberOfPages)
                    {
                        commandError.Value = true;
                        return;
                    }
                    lockBits[PageToLockRegionIndex(argument)] = false;
                    this.Log(LogLevel.Debug, "Unlocked region #{0}", PageToLockRegionIndex(argument));
                    break;

                case Commands.GetLockBit:
                    if(argument >= NumberOfPages)
                    {
                        commandError.Value = true;
                        return;
                    }
                    resultQueue.Enqueue(lockBits[PageToLockRegionIndex(argument)] ? 1U : 0U);
                    break;

                case Commands.EraseSector:
                    if(argument >= NumberOfLockRegions)
                    {
                        commandError.Value = true;
                        return;
                    }

                    if(lockBits[argument])
                    {
                        triedWriteLocked.Value = true;
                        this.Log(LogLevel.Debug, "Tried to erase sector #{0} which is currently locked", argument);
                    }

                    var firstPage = argument * PagesInSector;
                    for(var i = 0; i < PagesInSector; ++i)
                    {
                        ErasePage(firstPage + i);
                    }
                    break;

                case Commands.SetGPNVM:
                case Commands.ClearGPNVM:
                case Commands.GetGPNVM:
                case Commands.StartReadUniqueIdentifier:
                case Commands.StopReadUniqueIdentifier:
                case Commands.GetCALIB:
                case Commands.WriteUserSignature:
                case Commands.EraseUserSignature:
                case Commands.StartReadUserSignature:
                case Commands.StopReadUserSignature:
                    this.Log(LogLevel.Warning, "{0} command is not supported", command);
                    break;

                default:
                    throw new Exception("unreachable");
            }
        }

        private void AssertPositive(string argument, int value)
        {
            if(value <= 0)
            {
                throw new ConstructionException($"'{argument}' should be greater than zero");
            }
        }

        private void DefineRegisters()
        {
            Registers.Mode.Define(this)
                .WithFlag(0, out generateInterrupt, name: "FRDY",
                    changeCallback: (_, __) => IRQ.Set(generateInterrupt.Value))
                .WithTag("FWS", 8, 4)
                .WithTaggedFlag("SCOD", 16)
                .WithTaggedFlag("FAM", 24)
                .WithTaggedFlag("CLOE", 26)
            ;

            Registers.Command.Define(this)
                .WithValueField(0, 8, out var command, name: "FCMD")
                .WithValueField(8, 16, out var argument, name: "FARG")
                .WithValueField(24, 8, out var key, name: "FKEY")
                .WithWriteCallback((_, __) =>
                {
                    if(key.Value != Password || !Enum.IsDefined(typeof(Commands), (int)command.Value))
                    {
                        // NOTE: Invalid password or command; ignore
                        commandError.Value = true;
                        return;
                    }

                    ExecuteFlashCommand((Commands)command.Value, (int)argument.Value);
                    IRQ.Set(generateInterrupt.Value);
                })
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "FRDY",
                    valueProviderCallback: _ => true)
                .WithTaggedFlag("FCMDE", 1)
                .WithFlag(2, out triedWriteLocked, FieldMode.ReadToClear, name: "FLOCKE")
                .WithFlag(3, out commandError, FieldMode.ReadToClear, name: "FLERR")
                .WithReservedBits(4, 28)
            ;

            Registers.Result.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "FRR",
                    valueProviderCallback: _ => resultQueue.TryDequeue(out var result) ? result : 0)
            ;
        }

        private IFlagRegisterField generateInterrupt;
        private IFlagRegisterField triedWriteLocked;
        private IFlagRegisterField commandError;

        private readonly IMemory underlyingMemory;
        private readonly uint flashIdentifier;
        private readonly int pageSize;
        private readonly int sectorSize;
        private readonly int lockRegionSize;
        private readonly bool[] lockBits; // NOTE: Initialized in constructor
        private readonly Queue<uint> resultQueue = new Queue<uint>();

        private const uint Password = 0x5A;
        private const uint DefaultFlashIdentifier = 0x00112233;

        private enum Commands
        {
            GetFlashDescriptor = 0x00,
            WritePage = 0x01,
            WritePageAndLock = 0x02,
            ErasePageAndWritePage = 0x03,
            ErasePageAndWritePageThenLock = 0x04,
            EraseAll = 0x05,
            ErasePages = 0x07,
            SetLockBit = 0x08,
            ClearLockBit = 0x09,
            GetLockBit = 0x0A,
            SetGPNVM = 0x0B,
            ClearGPNVM = 0x0C,
            GetGPNVM = 0x0D,
            StartReadUniqueIdentifier = 0x0E,
            StopReadUniqueIdentifier = 0x0F,
            GetCALIB = 0x10,
            EraseSector = 0x11,
            WriteUserSignature = 0x12,
            EraseUserSignature = 0x13,
            StartReadUserSignature = 0x14,
            StopReadUserSignature = 0x15,
        }

        private enum Registers
        {
            Mode = 0x00,
            Command = 0x04,
            Status = 0x08,
            Result = 0x0C,
        }
    }
}
