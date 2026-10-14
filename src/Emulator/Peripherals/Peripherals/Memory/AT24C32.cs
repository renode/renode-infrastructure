//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Memory
{
    // Functional 24C32-class EEPROM: 4096 bytes, 16-bit word address,
    // sequential reads, 32-byte page wrapping and STOP-triggered programming.
    // The 5 ms write cycle is an explicit approximation, not vendor identity.
    public sealed class AT24C32 : IMPC56xxI2CPeripheral
    {
        public AT24C32(IMachine machine, uint writeCycleMilliseconds = DefaultWriteCycleMilliseconds,
            bool writeProtected = false)
        {
            if(writeCycleMilliseconds == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writeCycleMilliseconds));
            }
            this.writeCycleMilliseconds = writeCycleMilliseconds;
            this.writeProtected = writeProtected;
            memory = new byte[MemorySize];
            stagedPage = new byte[PageSize];
            programmingPage = new byte[PageSize];
            Array.Fill(memory, byte.MaxValue);
            programTimer = new LimitTimer(machine.ClockSource, 1000, this, nameof(programTimer),
                limit: writeCycleMilliseconds, direction: Direction.Descending, enabled: false,
                workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
            programTimer.LimitReached += CompleteProgramming;
            Reset();
        }

        public bool LoadImage(string path)
        {
            byte[] image;
            try
            {
                image = File.ReadAllBytes(path);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Could not load EEPROM image '{path}': {e.Message}");
            }
            if(image.Length != MemorySize)
            {
                throw new RecoverableException($"EEPROM image '{path}' must contain exactly {MemorySize} bytes; got {image.Length}");
            }
            lock(sync)
            {
                Array.Copy(image, memory, MemorySize);
                inputPath = Path.GetFullPath(path);
                CancelProtocolAndProgramming();
                ResetCounters();
            }
            return true;
        }

        public bool SaveImage(string path)
        {
            try
            {
                lock(sync)
                {
                    File.WriteAllBytes(path, memory);
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Could not save EEPROM image '{path}': {e.Message}");
            }
            return true;
        }

        public bool BeginTransmission(bool read)
        {
            lock(sync)
            {
                if(IsBusy)
                {
                    BusyAddressNackCount++;
                    return false;
                }
                addressBytes = 0;
                if(!read)
                {
                    stagedMask = 0;
                    stagedPageBase = 0;
                }
                return true;
            }
        }

        public void Write(byte[] data)
        {
            lock(sync)
            {
                foreach(var value in data)
                {
                    if(addressBytes < AddressSize)
                    {
                        if(addressBytes == 0)
                        {
                            currentAddress = value << 8;
                        }
                        else
                        {
                            currentAddress = (currentAddress | value) & AddressMask;
                            stagedPageBase = currentAddress & ~(PageSize - 1);
                        }
                        addressBytes++;
                        continue;
                    }

                    var pageOffset = currentAddress & (PageSize - 1);
                    stagedPage[pageOffset] = value;
                    stagedMask |= 1u << pageOffset;
                    currentAddress = stagedPageBase | ((currentAddress + 1) & (PageSize - 1));
                    WriteCount++;
                }
            }
        }

        public byte[] Read(int count = 1)
        {
            if(count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            lock(sync)
            {
                var result = new byte[count];
                for(var index = 0; index < count; index++)
                {
                    LastReadAddress = (uint)currentAddress;
                    result[index] = memory[currentAddress];
                    currentAddress = (currentAddress + 1) & AddressMask;
                    ReadCount++;
                }
                return result;
            }
        }

        public void FinishTransmission()
        {
            EndTransmission(stop: true);
        }

        public void EndTransmission(bool stop)
        {
            lock(sync)
            {
                addressBytes = 0;
                if(!stop || stagedMask == 0)
                {
                    return;
                }
                if(writeProtected)
                {
                    stagedMask = 0;
                    return;
                }

                programmingPageBase = stagedPageBase;
                programmingMask = stagedMask;
                Array.Copy(stagedPage, programmingPage, PageSize);
                stagedMask = 0;
                programTimer.Enabled = false;
                programTimer.Limit = writeCycleMilliseconds;
                programTimer.Value = writeCycleMilliseconds;
                programTimer.Enabled = true;
            }
        }

        public void AbortTransmission()
        {
            lock(sync)
            {
                addressBytes = 0;
                stagedMask = 0;
            }
        }

        public void Reset()
        {
            lock(sync)
            {
                CancelProtocolAndProgramming();
                ResetCounters();
            }
        }

        public uint ReadMemoryByte(uint address)
        {
            if(address >= MemorySize)
            {
                throw new RecoverableException($"EEPROM address 0x{address:X} is outside the {MemorySize}-byte image");
            }
            lock(sync)
            {
                return memory[address];
            }
        }

        public bool IsBusy
        {
            get
            {
                lock(sync)
                {
                    return programTimer.Enabled || programmingMask != 0;
                }
            }
        }

        public uint CurrentAddress
        {
            get
            {
                lock(sync)
                {
                    return (uint)currentAddress;
                }
            }
        }

        public ulong ReadCount { get; private set; }

        public ulong WriteCount { get; private set; }

        public ulong ProgramCount { get; private set; }

        public ulong BusyAddressNackCount { get; private set; }

        public uint LastReadAddress { get; private set; }

        public string InputPath => inputPath;

        private void CompleteProgramming()
        {
            lock(sync)
            {
                for(var offset = 0; offset < PageSize; offset++)
                {
                    if((programmingMask & (1u << offset)) != 0)
                    {
                        memory[programmingPageBase + offset] = programmingPage[offset];
                    }
                }
                programmingMask = 0;
                programTimer.Enabled = false;
                ProgramCount++;
            }
        }

        private void CancelProtocolAndProgramming()
        {
            addressBytes = 0;
            currentAddress = 0;
            stagedMask = 0;
            programmingMask = 0;
            programTimer.Reset();
            programTimer.AutoUpdate = false;
            programTimer.EventEnabled = true;
            programTimer.Limit = writeCycleMilliseconds;
            programTimer.Value = writeCycleMilliseconds;
            programTimer.Enabled = false;
        }

        private void ResetCounters()
        {
            ReadCount = 0;
            WriteCount = 0;
            ProgramCount = 0;
            BusyAddressNackCount = 0;
            LastReadAddress = 0;
        }

        private int addressBytes;
        private int currentAddress;
        private int stagedPageBase;
        private uint stagedMask;
        private int programmingPageBase;
        private uint programmingMask;
        private string inputPath;

        private readonly object sync = new object();
        private readonly byte[] memory;
        private readonly byte[] stagedPage;
        private readonly byte[] programmingPage;
        private readonly LimitTimer programTimer;
        private readonly uint writeCycleMilliseconds;
        private readonly bool writeProtected;

        private const int MemorySize = 4096;
        private const int AddressMask = MemorySize - 1;
        private const int AddressSize = 2;
        private const int PageSize = 32;
        private const uint DefaultWriteCycleMilliseconds = 5;
    }
}
