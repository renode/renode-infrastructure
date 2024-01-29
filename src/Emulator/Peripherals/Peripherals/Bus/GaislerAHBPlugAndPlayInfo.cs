//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class GaislerAHBPlugAndPlayInfo : IDoubleWordPeripheral
    {
        public GaislerAHBPlugAndPlayInfo(IMachine machine)
        {
            this.machine = machine;
            Reset();
        }
        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            if(!recordsCached) //if first read 
            {
                cacheRecords();
                recordsCached = true;
            }
                        
            var deviceNumber = 0;
            var master = false;
            if (offset >= slaveOffset) //slave device record read
            {
                deviceNumber = (int)(offset - slaveOffset) / 32;
            }
            else
            {
                deviceNumber = (int)(offset) / 32;
                master = true;
            }
            var deviceRecord = getDeviceRecord(deviceNumber, master);
            var recordOffset = (uint)((offset % 0x20)/4);
            return deviceRecord.ToUintArray()[recordOffset];
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            this.Log(LogLevel.Warning,"Write attempt. This memory region is Read Only");
        }
        #endregion

        #region IPeripheral implementation
        public void Reset ()
        {
            recordsCached = false;
            emptyRecord = new GaislerAHBPlugAndPlayRecord();
            masterDevices = new List<GaislerAHBPlugAndPlayRecord>();
            slaveDevices = new List<GaislerAHBPlugAndPlayRecord>();
        }
        #endregion

        private void cacheRecords()
        {
            //this.cacheMemory();
            var recordsFound = machine.SystemBus.Children.Where(x => x.Peripheral is IGaislerAHB);
            //.Cast<IRegistered<IGaislerAHB, IRegistrationPoint>>();
            foreach (var record in recordsFound)
            {
                var peripheral = (IGaislerAHB)record.Peripheral;
                var registration = record.RegistrationPoint;
                
                var recordEntry = new GaislerAHBPlugAndPlayRecord();
                                                
                var deviceAddress = registration.Range.StartAddress;
                                               
                recordEntry.IdentificationRegister.Vendor = peripheral.GetVendorID();
                recordEntry.IdentificationRegister.Device = peripheral.GetDeviceID();
                recordEntry.BankAddressRegister[0].Type = peripheral.GetSpaceType();
                //XXX: hack
                if(recordEntry.IdentificationRegister.Vendor == 0x01 && recordEntry.IdentificationRegister.Device == 0x006)
                {
                    deviceAddress -= PlugAndPlayRecordsOffset;
                }
                if(recordEntry.BankAddressRegister[0].Type == GaislerAHBPlugAndPlayRecord.SpaceType.AHBMemorySpace)
                {
                    recordEntry.BankAddressRegister[0].Address = (uint)((deviceAddress >> 20) & 0xfff);
                }
                else if(recordEntry.BankAddressRegister[0].Type == GaislerAHBPlugAndPlayRecord.SpaceType.AHBIOSpace)
                {
                    recordEntry.BankAddressRegister[0].Address = (uint)((deviceAddress >> 8) & 0xfff);
                }
                
                recordEntry.BankAddressRegister[0].Mask = 0xfff;
                
                if (peripheral.IsMaster())
                {
                    masterDevices.Add(recordEntry);
                }
                else
                {
                    slaveDevices.Add(recordEntry);
                }
            }
        }
                
        private GaislerAHBPlugAndPlayRecord getDeviceRecord(int number, bool master)
        {
            var record = emptyRecord;
            if(master)
            {
                if( number < masterDevices.Count)
                {
                    record = masterDevices[number];
                }
            }
            else
            {
                if( number < slaveDevices.Count)
                {
                    record = slaveDevices[number];
                }
            }
            return record;
        }
        
        private bool recordsCached;
        private List<GaislerAHBPlugAndPlayRecord> masterDevices;
        private List<GaislerAHBPlugAndPlayRecord> slaveDevices;
        private GaislerAHBPlugAndPlayRecord emptyRecord;

        private readonly IMachine machine;
        private readonly uint slaveOffset = 0x800;
        private readonly uint PlugAndPlayRecordsOffset = 0xff000;
    }
}

