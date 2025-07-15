//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SiLabs_SEMAPHORE_1 : IDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_SEMAPHORE_1(Machine machine, bool logRegisterAccess = false)
        {
            this.machine = machine;
            this.LogRegisterAccess = logRegisterAccess;
            
            lockBits = new BitArray(NumberOfLocks, false);
            
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            lockBits.SetAll(false);
        }

        // TODO: for now we don't support Set/Clear/Toggle registers. If we want to add support for it,
        // we must revise the AcquireLock/ReleaseLock logic (an initial read on any LOCK register would cause 
        // the lock to be acquired).

        public uint ReadDoubleWord(long offset)
        {
            var result = 0U;

            try
            {
                if(registersCollection.TryRead(offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", offset, (Registers)offset, result);
                }
            }

            if (LogRegisterAccess)
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", offset, (Registers)offset);
            }
            
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            machine.ClockSource.ExecuteInLock(delegate {
                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
                }
                if(!registersCollection.TryWrite(offset, value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IpVersion, new DoubleWordRegister(this, 0x1)
                    .WithTag("IPVERSION", 0, 32)
                },
            };

            for(var index = 0; index < NumberOfLocks; index++)
            {
                var i = index;
                registerDictionary.Add((long)Registers.Lock0 + 4*i,
                    new DoubleWordRegister(this)
                        .WithFlag(0, valueProviderCallback: _ => AcquireLock(i), writeCallback: (_, value) => ReleaseLock(i), name: "LOCK")
                        .WithReservedBits(1, 31)
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private bool LogRegisterAccess;
        private const int NumberOfLocks = 128;
        private readonly BitArray lockBits;

#region register fields
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        
        private void ReleaseLock(int index)
        {
            this.Log(LogLevel.Noisy, "ReleaseLock(): index={0}, was{1}acquired", index, lockBits[index] ? " " : " not ");

            machine.ClockSource.ExecuteInLock(delegate {
                lockBits[index] = false;
            });
        }

        private bool AcquireLock(int index)
        {
            bool ret = false;

            machine.ClockSource.ExecuteInLock(delegate {
                ret = lockBits[index];
                lockBits[index] = true;
            });

            this.Log(LogLevel.Noisy, "AcquireLock(): index={0}, {1}", index, ret ? "failed" : "success");
            return ret;
        }
#endregion

#region enums
        private enum Registers
        {
            IpVersion                                 = 0x0000,
            Lock0                                     = 0x0004,
            Lock1                                     = 0x0008,
            Lock2                                     = 0x000C,
            Lock3                                     = 0x0010,
            Lock4                                     = 0x0014,
            Lock5                                     = 0x0018,
            Lock6                                     = 0x001C,
            Lock7                                     = 0x0020,
            Lock8                                     = 0x0024,
            Lock9                                     = 0x0028,
            Lock10                                    = 0x002C,
            Lock11                                    = 0x0030,
            Lock12                                    = 0x0034,
            Lock13                                    = 0x0038,
            Lock14                                    = 0x003C,
            Lock15                                    = 0x0040,
            Lock16                                    = 0x0044,
            Lock17                                    = 0x0048,
            Lock18                                    = 0x004C,
            Lock19                                    = 0x0050,
            Lock20                                    = 0x0054,
            Lock21                                    = 0x0058,
            Lock22                                    = 0x005C,
            Lock23                                    = 0x0060,
            Lock24                                    = 0x0064,
            Lock25                                    = 0x0068,
            Lock26                                    = 0x006C,
            Lock27                                    = 0x0070,
            Lock28                                    = 0x0074,
            Lock29                                    = 0x0078,
            Lock30                                    = 0x007C,
            Lock31                                    = 0x0080,
            Lock32                                    = 0x0084,
            Lock33                                    = 0x0088,
            Lock34                                    = 0x008C,
            Lock35                                    = 0x0090,
            Lock36                                    = 0x0094,
            Lock37                                    = 0x0098,
            Lock38                                    = 0x009C,
            Lock39                                    = 0x00A0,
            Lock40                                    = 0x00A4,
            Lock41                                    = 0x00A8,
            Lock42                                    = 0x00AC,
            Lock43                                    = 0x00B0,
            Lock44                                    = 0x00B4,
            Lock45                                    = 0x00B8,
            Lock46                                    = 0x00BC,
            Lock47                                    = 0x00C0,
            Lock48                                    = 0x00C4,
            Lock49                                    = 0x00C8,
            Lock50                                    = 0x00CC,
            Lock51                                    = 0x00D0,
            Lock52                                    = 0x00D4,
            Lock53                                    = 0x00D8,
            Lock54                                    = 0x00DC,
            Lock55                                    = 0x00E0,
            Lock56                                    = 0x00E4,
            Lock57                                    = 0x00E8,
            Lock58                                    = 0x00EC,
            Lock59                                    = 0x00F0,
            Lock60                                    = 0x00F4,
            Lock61                                    = 0x00F8,
            Lock62                                    = 0x00FC,
            Lock63                                    = 0x0100,
            Lock64                                    = 0x0104,
            Lock65                                    = 0x0108,
            Lock66                                    = 0x010C,
            Lock67                                    = 0x0110,
            Lock68                                    = 0x0114,
            Lock69                                    = 0x0118,
            Lock70                                    = 0x011C,
            Lock71                                    = 0x0120,
            Lock72                                    = 0x0124,
            Lock73                                    = 0x0128,
            Lock74                                    = 0x012C,
            Lock75                                    = 0x0130,
            Lock76                                    = 0x0134,
            Lock77                                    = 0x0138,
            Lock78                                    = 0x013C,
            Lock79                                    = 0x0140,
            Lock80                                    = 0x0144,
            Lock81                                    = 0x0148,
            Lock82                                    = 0x014C,
            Lock83                                    = 0x0150,
            Lock84                                    = 0x0154,
            Lock85                                    = 0x0158,
            Lock86                                    = 0x015C,
            Lock87                                    = 0x0160,
            Lock88                                    = 0x0164,
            Lock89                                    = 0x0168,
            Lock90                                    = 0x016C,
            Lock91                                    = 0x0170,
            Lock92                                    = 0x0174,
            Lock93                                    = 0x0178,
            Lock94                                    = 0x017C,
            Lock95                                    = 0x0180,
            Lock96                                    = 0x0184,
            Lock97                                    = 0x0188,
            Lock98                                    = 0x018C,
            Lock99                                    = 0x0190,
            Lock100                                   = 0x0194,
            Lock101                                   = 0x0198,
            Lock102                                   = 0x019C,
            Lock103                                   = 0x01A0,
            Lock104                                   = 0x01A4,
            Lock105                                   = 0x01A8,
            Lock106                                   = 0x01AC,
            Lock107                                   = 0x01B0,
            Lock108                                   = 0x01B4,
            Lock109                                   = 0x01B8,
            Lock110                                   = 0x01BC,
            Lock111                                   = 0x01C0,
            Lock112                                   = 0x01C4,
            Lock113                                   = 0x01C8,
            Lock114                                   = 0x01CC,
            Lock115                                   = 0x01D0,
            Lock116                                   = 0x01D4,
            Lock117                                   = 0x01D8,
            Lock118                                   = 0x01DC,
            Lock119                                   = 0x01E0,
            Lock120                                   = 0x01E4,
            Lock121                                   = 0x01E8,
            Lock122                                   = 0x01EC,
            Lock123                                   = 0x01F0,
            Lock124                                   = 0x01F4,
            Lock125                                   = 0x01F8,
            Lock126                                   = 0x01FC,
            Lock127                                   = 0x0200,
      }
#endregion
    }
}