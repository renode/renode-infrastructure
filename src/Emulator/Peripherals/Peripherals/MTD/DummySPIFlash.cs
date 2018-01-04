//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.MTD
{
    public class DummySPIFlash: ISPIFlash
    {
        #region IPeripheral implementation

        public void Reset()
        {
        }

        #endregion

        #region ISPIFlash implementation

        public void WriteEnable()
        {
            throw new NotImplementedException();
        }

        public void WriteDisable()
        {
            throw new NotImplementedException();
        }

        public void WriteStatusRegister(uint registerNumber, uint value)
        {
            throw new NotImplementedException();
        }

        public uint ReadStatusRegister(uint registerNumber)
        {
            throw new NotImplementedException();
        }

        public uint ReadID()
        {
            this.Log(LogLevel.Warning,"Reading ID");
            return 0xffffffff;
        }

        #endregion

        public DummySPIFlash()
        {
        }
    }
}

