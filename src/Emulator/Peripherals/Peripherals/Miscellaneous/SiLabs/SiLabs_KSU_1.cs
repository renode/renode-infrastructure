//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_KSU_1 : SiLabsPeripheral, SiLabs_IKeyStorage
    {
        public SiLabs_KSU_1(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();

            keyStorage.Clear();
        }

        public byte[] GetKey(uint slot)
        {
            this.Log(LogLevel.Noisy, "GetKey(): slot={0} size={1}", slot, keyStorage.Count);

            if(!ContainsKey(slot))
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

            if(!ContainsKey(slot))
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

        public GPIO IRQ { get; }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private readonly Dictionary<uint, byte[]> keyStorage = new Dictionary<uint, byte[]>();

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
    }
}