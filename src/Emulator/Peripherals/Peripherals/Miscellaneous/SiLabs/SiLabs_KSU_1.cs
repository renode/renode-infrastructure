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
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SiLabs_KSU_1 : IDoubleWordPeripheral, IKnownSize, SiLabs_IKeyStorage
    {
        public SiLabs_KSU_1(Machine machine, bool logRegisterAccess = false, bool logInterrupts = false)
        {
            this.machine = machine;
            this.LogRegisterAccess = logRegisterAccess;
            this.LogInterrupts = logInterrupts;
            
            IRQ = new GPIO();
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
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

#region register fields
        public long Size => 0x4000;
        public GPIO IRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private bool LogRegisterAccess;
        private bool LogInterrupts;
        private Dictionary<uint, byte[]> keyStorage = new Dictionary<uint, byte[]>();
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = false;
                IRQ.Set(irq);                        
            });
        }

        public byte[] GetKey(uint slot)
        {
            this.Log(LogLevel.Noisy, "GetKey(): slot={0} size={1}", slot, keyStorage.Count);

            if (!ContainsKey(slot))
            {
                throw new Exception($"Key not found in slot {slot}");
            }
            return keyStorage[slot];
        }

        public void AddKey(uint slot, byte[] key)
        {
            keyStorage[slot] = key;
            this.Log(LogLevel.Noisy, "AddKey(): slot={0} size={1}", slot, keyStorage.Count);
        }

        public void RemoveKey(uint slot)
        {
            this.Log(LogLevel.Noisy, "RemoveKey(): slot={0} size={1}", slot, keyStorage.Count);

            if (!ContainsKey(slot))
            {
                throw new Exception($"Key not found in slot {slot}");
            }
            keyStorage.Remove(slot);
            this.Log(LogLevel.Noisy, "RemoveKey() done: slot={0} size={1}", slot, keyStorage.Count);
        }

        public bool ContainsKey(uint slot)
        {
            bool ret = keyStorage.ContainsKey(slot);
            this.Log(LogLevel.Noisy, "ContainsKey(): slot={0} present={1} size={2}", slot, ret, keyStorage.Count);
            return ret;
        }
#endregion

#region enums
        private enum Registers
        {
            IpVersion                                 = 0x0000,
            Enable                                    = 0x0004,
            Control                                   = 0x0008,
            IrkCommand                                = 0x000C,
            Crypto0Command                            = 0x0010,
            Crypto1Command                            = 0x0014,
            Info                                      = 0x0018,
            Status                                    = 0x001C,
            IrkInterruptEnable                        = 0x0020,
            IrkInterruptFlags                         = 0x0024,
            Crypto0InterruptEnable                    = 0x0028,
            Crypto0InterruptFlags                     = 0x002C,
            Crypto1InterruptEnable                    = 0x0030,
            Crypto1InterruptFlags                     = 0x0034,
            IrkErrorSlot                              = 0x0038,
            Crypto0ErrorSlot                          = 0x003C,
            Crypto1ErrorSlot                          = 0x0040,
            CoreReset                                 = 0x0044,
            Miscellaneous                             = 0x0048,
            RootControl                               = 0x0080,
            RootCommand                               = 0x0084,
            RootEccErrorRamAddress                    = 0x0088,
            RootBaseAddressMetadata                   = 0x008C,
            RootStatus                                = 0x0090,
            // Set registers
            IpVersion_Set                             = 0x1000,
            Enable_Set                                = 0x1004,
            Control_Set                               = 0x1008,
            IrkCommand_Set                            = 0x100C,
            Crypto0Command_Set                        = 0x1010,
            Crypto1Command_Set                        = 0x1014,
            Info_Set                                  = 0x1018,
            Status_Set                                = 0x101C,
            IrkInterruptEnable_Set                    = 0x1020,
            IrkInterruptFlags_Set                     = 0x1024,
            Crypto0InterruptEnable_Set                = 0x1028,
            Crypto0InterruptFlags_Set                 = 0x102C,
            Crypto1InterruptEnable_Set                = 0x1030,
            Crypto1InterruptFlags_Set                 = 0x1034,
            IrkErrorSlot_Set                          = 0x1038,
            Crypto0ErrorSlot_Set                      = 0x103C,
            Crypto1ErrorSlot_Set                      = 0x1040,
            CoreReset_Set                             = 0x1044,
            Miscellaneous_Set                         = 0x1048,
            RootControl_Set                           = 0x1080,
            RootCommand_Set                           = 0x1084,
            RootEccErrorRamAddress_Set                = 0x1088,
            RootBaseAddressMetadata_Set               = 0x108C,
            RootStatus_Set                            = 0x1090,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            Enable_Clr                                = 0x2004,
            Control_Clr                               = 0x2008,
            IrkCommand_Clr                            = 0x200C,
            Crypto0Command_Clr                        = 0x2010,
            Crypto1Command_Clr                        = 0x2014,
            Info_Clr                                  = 0x2018,
            Status_Clr                                = 0x201C,
            IrkInterruptEnable_Clr                    = 0x2020,
            IrkInterruptFlags_Clr                     = 0x2024,
            Crypto0InterruptEnable_Clr                = 0x2028,
            Crypto0InterruptFlags_Clr                 = 0x202C,
            Crypto1InterruptEnable_Clr                = 0x2030,
            Crypto1InterruptFlags_Clr                 = 0x2034,
            IrkErrorSlot_Clr                          = 0x2038,
            Crypto0ErrorSlot_Clr                      = 0x203C,
            Crypto1ErrorSlot_Clr                      = 0x2040,
            CoreReset_Clr                             = 0x2044,
            Miscellaneous_Clr                         = 0x2048,
            RootControl_Clr                           = 0x2080,
            RootCommand_Clr                           = 0x2084,
            RootEccErrorRamAddress_Clr                = 0x2088,
            RootBaseAddressMetadata_Clr               = 0x208C,
            RootStatus_Clr                            = 0x2090,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            Enable_Tgl                                = 0x3004,
            Control_Tgl                               = 0x3008,
            IrkCommand_Tgl                            = 0x300C,
            Crypto0Command_Tgl                        = 0x3010,
            Crypto1Command_Tgl                        = 0x3014,
            Info_Tgl                                  = 0x3018,
            Status_Tgl                                = 0x301C,
            IrkInterruptEnable_Tgl                    = 0x3020,
            IrkInterruptFlags_Tgl                     = 0x3024,
            Crypto0InterruptEnable_Tgl                = 0x3028,
            Crypto0InterruptFlags_Tgl                 = 0x302C,
            Crypto1InterruptEnable_Tgl                = 0x3030,
            Crypto1InterruptFlags_Tgl                 = 0x3034,
            IrkErrorSlot_Tgl                          = 0x3038,
            Crypto0ErrorSlot_Tgl                      = 0x303C,
            Crypto1ErrorSlot_Tgl                      = 0x3040,
            CoreReset_Tgl                             = 0x3044,
            Miscellaneous_Tgl                         = 0x3048,
            RootControl_Tgl                           = 0x3080,
            RootCommand_Tgl                           = 0x3084,
            RootEccErrorRamAddress_Tgl                = 0x3088,
            RootBaseAddressMetadata_Tgl               = 0x308C,
            RootStatus_Tgl                            = 0x3090,
       }
#endregion
    }
}