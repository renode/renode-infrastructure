//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
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
    public class SiLabs_RvCfg_2 : IDoubleWordPeripheral, IKnownSize, SiLabs_IRvConfig
    {
        public SiLabs_RvCfg_2(Machine machine, bool logRegisterAccess = false)
        {
            this.machine = machine;
            this.LogRegisterAccess = logRegisterAccess;
            
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {  
                    this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            if (LogRegisterAccess && !internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);

            }

            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BootAddressConfig, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out bootAddress, name: "BOOTADDRCFG")
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private bool LogRegisterAccess;

#region register fields
        private IValueRegisterField bootAddress;
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;        

        public ulong BootAddress
        { 
            get
            {
                return bootAddress.Value;
            }
        }
#endregion

#region enums
        private enum Registers
        {
            IpVersion                                 = 0x0000,
            SoftwareReset                             = 0x0004,
            IsoControl                                = 0x0008,
            IsoStatus                                 = 0x000C,
            FetchEnable                               = 0x0020,
            BootAddressConfig                         = 0x0024,
            MtVecAddressConfig                        = 0x0028,
            Status                                    = 0x002C,
            PcSample                                  = 0x0030,
            // Set registers
            IpVersion_Set                             = 0x1000,
            SoftwareReset_Set                         = 0x1004,
            IsoControl_Set                            = 0x1008,
            IsoStatus_Set                             = 0x100C,
            FetchEnable_Set                           = 0x1020,
            BootAddressConfig_Set                     = 0x1024,
            MtVecAddressConfig_Set                    = 0x1028,
            Status_Set                                = 0x102C,
            PcSample_Set                              = 0x1030,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            SoftwareReset_Clr                         = 0x2004,
            IsoControl_Clr                            = 0x2008,
            IsoStatus_Clr                             = 0x200C,
            FetchEnable_Clr                           = 0x2020,
            BootAddressConfig_Clr                     = 0x2024,
            MtVecAddressConfig_Clr                    = 0x2028,
            Status_Clr                                = 0x202C,
            PcSample_Clr                              = 0x2030,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            SoftwareReset_Tgl                         = 0x3004,
            IsoControl_Tgl                            = 0x3008,
            IsoStatus_Tgl                             = 0x300C,
            FetchEnable_Tgl                           = 0x3020,
            BootAddressConfig_Tgl                     = 0x3024,
            MtVecAddressConfig_Tgl                    = 0x3028,
            Status_Tgl                                = 0x302C,
            PcSample_Tgl                              = 0x3030,
       }
#endregion
    }
}