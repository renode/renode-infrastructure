//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.PCI
{
    public class VersatilePCI : SimpleContainer<IPCIPeripheral>, IDoubleWordPeripheral
    {
        public VersatilePCI(IMachine machine) : base(machine)
        {
            info = new PCIInfo[4];
            _info = new bool[4];
            int i;
            for(i = 0; i < 4; i++)
            {
                _info[i] = false;
            }
        }

        public override void Register(IPCIPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            base.Register(peripheral, registrationPoint);
            info[registrationPoint.Address] = peripheral.GetPCIInfo();
            _info[registrationPoint.Address] = true;
        }

        [ConnectionRegion("config")]
        public uint ReadDoubleWordConfig(long offset)
        {
            switch(offset)
            {
            case 0x0:
                return 0x030010ee;  // DEVICE_ID
            case 0x8:
                return 0x0b400000; // CLASS_ID
            }
            return 0;
        }

        [ConnectionRegion("config")]
        public void WriteDoubleWordConfig(long offset, uint value)
        {
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            //if (offset < 0x800) return 0;
            //offset -= 0x800;
            int pci_num = (int)(offset / 0x800);
            if(pci_num > 3)
                return 0;
            if(!_info[pci_num])
                return 0;
            PCIInfo linfo = info[pci_num];
            offset -= pci_num * 0x800; 
            if(offset == 0x00)
                return  (uint)linfo.vendor_id + (uint)(linfo.device_id << 16);
            if(offset == 0x04)
                return (1 << 25) | (1 << 1) | (1 << 0); // cmd ?
            if(offset == 0x08)
                return (uint)(linfo.device_class << 16); // class 
            if(offset == 0x0c)
                return 0x8; // ?
            if((offset >= 0x10) && (offset < 0x2c))
            {
                uint bar_id = (uint)((offset - 0x10) / 4);
                return linfo.BAR[bar_id];
            }
            if(offset == 0x2c)
                return  (uint)linfo.sub_vendor_id + (uint)(linfo.sub_device_id << 16);
            if(offset == 0x30)
                return 0x1; // rom ?
            if(offset == 0x3c)
                return (uint)((0x1 << 8) | (24 + pci_num)); // slot 24 pin 1 (pci0), slot 25 pin 1 (pci1) ...
            if(offset == 0x34)
                return 0x1; // ?
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            //if (offset < 0x800) return;
            //offset -= 0x800;
            int pci_num = (int)(offset / 0x800);
            if(pci_num > 3)
                return;
            if(!_info[pci_num])
                return;
            PCIInfo linfo = info[pci_num];
            offset -= pci_num * 0x800;
            if((offset >= 0x10) && (offset < 0x2c))
            {
                uint bar_id = (uint)((offset - 0x10) / 4);
                if(value == 0xFFFFFFFF)
                {
                    linfo.BAR[bar_id] = linfo.BAR_len[bar_id];
                }
                else
                {
                    linfo.BAR[bar_id] = value;
                }
            }
        }

        [ConnectionRegion("io")]
        public void WriteDoubleWordIO(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "writeIO {0:X}, value 0x{1:X}", offset, value);

            int found = -1;
            int bar_no = -1;
            for(int c = 0; c < 3; c++)
            {
                if(!_info[c])
                    continue;
                for(int i = 0; i < 8; i++)
                {
                    if(info[c].BAR_len[i] == 0)
                        continue;
                    if((offset >= (info[c].BAR[i] & 0x0FFFFFFF)) && (offset < ((info[c].BAR[i] & 0xFFFFFFF) + info[c].BAR_len[i])))
                    {
                        found = c;
                        bar_no = i;
                        break;
                    }
                }
            }
            if(found == -1)
                return;

            PCIInfo linfo = info[found];
            offset -= (linfo.BAR[bar_no] & 0xFFFFFFF);
            IPCIPeripheral pci_device = GetByAddress(found);
            pci_device.WriteDoubleWordPCI((uint)bar_no, offset, value);
        }

        [ConnectionRegion("io")]
        public uint ReadDoubleWordIO(long offset)
        {
            this.Log(LogLevel.Noisy, "readIO {0:X}", offset);

            // (1) search for pci slot and bar no
            int found = -1;
            int bar_no = -1;
            for(int c = 0; c < 3; c++)
            {
                if(!_info[c])
                    continue;
                for(int i = 0; i < 8; i++)
                {
                    if(info[c].BAR_len[i] == 0)
                        continue;
                    if((offset >= (info[c].BAR[i] & 0x0FFFFFFF)) && (offset < ((info[c].BAR[i] & 0xFFFFFFF) + info[c].BAR_len[i])))
                    {
                        found = c;
                        bar_no = i;
                        break;
                    }
                }
            }
            if(found == -1)
                return 0;

            // (2) forward read
            PCIInfo linfo = info[found];
            offset -= (linfo.BAR[bar_no] & 0xFFFFFFF);
            IPCIPeripheral pci_device = GetByAddress(found);
            return pci_device.ReadDoubleWordPCI((uint)bar_no, offset);
        }

        public override void Reset()
        {
        }
        //private void TransferData(uint value)
        //{
        //        if (!children.Keys.Select (x => x.Number).Contains (slaveAddressForPacket)) 
        //}

        private void Update()
        {
        }

        private PCIInfo[] info;
        bool[] _info;
    }
}

