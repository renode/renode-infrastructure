//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Extensions;
using System.Net;

namespace Antmicro.Renode.Peripherals.Bus
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class GaislerAPBController : IDoubleWordPeripheral, IGaislerAHB
    {
        public GaislerAPBController(Machine machine)
        {
            this.machine = machine;
            Reset();
        }

        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            if(!recordsCached)
            {
                this.cacheRecords();
                recordsCached = true;
            }
            var record = emptyRecord;
            var recordNumber = (int)(offset / 8);
            if(recordNumber < records.Count)
            {
                record = records[recordNumber];
            }
            var recordOffset = (int)((offset % 8) / 4);
            return record.ToUintArray()[recordOffset];
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            //throw new NotImplementedException ();
        }
        #endregion

        #region IPeripheral implementation
        public void Reset ()
        {
            emptyRecord = new GaislerAPBPlugAndPlayRecord();
            recordsCached = false;
            records = new List<GaislerAPBPlugAndPlayRecord>();
        }
        #endregion
  
        private readonly uint vendorID = 0x01;  // Aeroflex Gaisler
        private readonly uint deviceID = 0x006; // GRLIB APBCTRL
        private readonly bool master = false;   // This device is AHB slave  
        private readonly GaislerAHBPlugAndPlayRecord.SpaceType spaceType = GaislerAHBPlugAndPlayRecord.SpaceType.AHBMemorySpace;
        
        #region IGaisslerAHB implementation
        public uint GetVendorID ()
        {
            return vendorID;
        }

        public uint GetDeviceID ()
        {
            return deviceID;
        }
        
        public bool IsMaster ()
        {
            return master;
        }
        
        public GaislerAHBPlugAndPlayRecord.SpaceType GetSpaceType ()
        {
            return spaceType;
        }
        #endregion
  
        private void cacheRecords()
        { 
            var recordsFound = machine.SystemBus.Children.Where(x => x.Peripheral is IGaislerAPB);
            foreach (var record in recordsFound)
            {
                var peripheral = (IGaislerAPB)record.Peripheral;
                var registration = record.RegistrationPoint;
                var recordEntry = new GaislerAPBPlugAndPlayRecord();
                var deviceAddress = registration.Range.StartAddress;
                recordEntry.ConfigurationWord.Vendor = peripheral.GetVendorID();
                recordEntry.ConfigurationWord.Device = peripheral.GetDeviceID();
                recordEntry.BankAddressRegister.Type = peripheral.GetSpaceType();
                recordEntry.ConfigurationWord.Irq = peripheral.GetInterruptNumber();
                if(recordEntry.BankAddressRegister.Type == GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace)
                {
                    recordEntry.BankAddressRegister.Address = (uint)((deviceAddress >> 8) & 0xfff);
                }
                recordEntry.BankAddressRegister.Mask = 0xfff;
                records.Add(recordEntry);                
            }
        }
        
        private readonly Machine machine;
        private List<GaislerAPBPlugAndPlayRecord> records;
        private bool recordsCached;
        private GaislerAPBPlugAndPlayRecord emptyRecord;
    }
}
