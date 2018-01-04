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
    public class GaislerAHBPlugAndPlayRecord
    {
        public GaislerAHBPlugAndPlayRecord()
        {
            IdentificationRegister = new IdReg();
            UserDefinedRegister = new uint[3];
            BankAddressRegister = new Bar[4];
            for (var i = 0; i < 4; i++ )
            {
                BankAddressRegister[i] = new Bar();
            }
        }
   
        public IdReg IdentificationRegister;
        public uint[] UserDefinedRegister;
        public Bar[] BankAddressRegister;
        
        public byte[] ToByteArray()
        {
            var arr = new byte[32];
            
            Array.Copy( BitConverter.GetBytes(IdentificationRegister.GetValue()),0,arr,0,4);
            
            for( var i = 0; i < 3; i++)
            {
                Array.Copy( BitConverter.GetBytes(UserDefinedRegister[i]),0,arr,(4 + 4*i),4);
            }
            for( var i = 0; i < 4; i++)
            {
                Array.Copy( BitConverter.GetBytes(BankAddressRegister[i].GetValue()), 0, arr, (16 + 4*i), 4);
            }
            return arr;
            
        }
        
        public uint[] ToUintArray()
        {
            var arr = new uint[8];
            
            arr[0] = IdentificationRegister.GetValue();
            for (var i=0; i<3; i++)
            {
                arr[i+1] = UserDefinedRegister[i];
            }
            for (var i=0; i<4; i++)
            {
                arr[i+4] = BankAddressRegister[i].GetValue();
            }
            
            return arr;
        }
        
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
        
        public enum SpaceType : uint
        {
            None = 0x00,
            APBIOSpace = 0x01,
            AHBMemorySpace = 0x02,
            AHBIOSpace = 0x03
        }
    }
}

