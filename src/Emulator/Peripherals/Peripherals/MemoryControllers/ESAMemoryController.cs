//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class ESAMemoryController : IDoubleWordPeripheral, IGaislerAPB
    {
        public ESAMemoryController()
        {
            Reset();
        }
        
        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            switch( (RegisterOffset) offset )
            {
            case RegisterOffset.Config1:
                return config1;
            case RegisterOffset.Config2:
                return config2;
            case RegisterOffset.Config3:
                return config3;
            case RegisterOffset.PowerSavingConfig:
                return powerSavingConfig;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            switch( (RegisterOffset) offset )
            {
            case RegisterOffset.Config1:
                config1 = value;
                return;
            case RegisterOffset.Config2:
                config2 = value;
                return;
            case RegisterOffset.Config3:
                config3 = value;
                return;
            case RegisterOffset.PowerSavingConfig:
                powerSavingConfig = value;
                return;
            default:
                this.LogUnhandledWrite(offset, value);
                return;
            }
        }
        #endregion
        
        #region IGaislerAPB implementation
        public uint GetVendorID ()
        {
            return vendorID;
        }

        public uint GetDeviceID ()
        {
            return deviceID;
        }

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType ()
        {
            return spaceType;
        }
        
        public uint GetInterruptNumber()
        {
            return 0;
        }
        #endregion

        #region IPeripheral implementation
        public void Reset ()
        {
            config1 = 0xFu;
            config2 = 0;
            config3 = 0;
            powerSavingConfig = 0;
        }
        #endregion

        private uint config1;
        private uint config2;
        private uint config3;
        private uint powerSavingConfig;
        private readonly uint vendorID = 0x04;  // European Space Agency (ESA)
        private readonly uint deviceID = 0x00f; // GRLIB MCTRL
        private readonly GaislerAPBPlugAndPlayRecord.SpaceType spaceType = GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;
        
        private enum RegisterOffset : uint
        {
            Config1 = 0x00,
            Config2 = 0x04,
            Config3 = 0x08,
            PowerSavingConfig = 0x0c
        }
    }
}

