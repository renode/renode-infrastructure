//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class GaislerAPBPlugAndPlayRecord
    {
        public GaislerAPBPlugAndPlayRecord()
        {
            ConfigurationWord = new IdReg();
            BankAddressRegister = new Bar();
        }
        
        public IdReg ConfigurationWord;
        public Bar BankAddressRegister;
        
        public class IdReg
        {
            public uint Vendor = 0;
            public uint Device = 0;
            public uint Version = 0;
            public uint Irq = 0;
            public uint GetValue()
            {
                var value = ((Vendor & 0xff) << 24) | ((Device & 0xfff) << 12) | ((Version & 0x1f) << 5) | ((Irq & 0x1f) << 0 );
                return value;
            }
        }
        public class Bar
        {
            public uint Address = 0;
            public bool Prefechable = false;
            public bool Cacheble = false;
            public uint Mask = 0;
            public SpaceType Type = SpaceType.None;
                    
            public uint GetValue()
            {
                var value = ((Address & 0xfff) << 20) | (Prefechable ? 1u<<17 : 0) | (Cacheble ? 1u<<16 : 0) | ((Mask & 0xfff) << 4) | (uint)(Type);
                return value;
            }
        }
        
        public uint[] ToUintArray()
        {
            var arr = new uint[2];
            arr[0] = ConfigurationWord.GetValue();
            arr[1] = BankAddressRegister.GetValue();
            
            return arr;
        }
        
        public enum SpaceType : uint
        {
            None = 0x00,
            APBIOSpace = 0x01,
            AHBMemorySpace = 0x02,
            AHBIOSpace = 0x03
        }
    }
}
