//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.PCI
{
    public struct PCIInfo
    {
        public uint[] Bar;
        public uint[] BarLen;
        public ushort DeviceId;
        public ushort VendorId;
        public ushort SubDeviceId;
        public ushort SubVendorId;
        public ushort DeviceClass;

        public PCIInfo(ushort did, ushort vid, ushort sdid, ushort svid, ushort dclass)
        {
            DeviceId = did;
            VendorId = vid;
            SubDeviceId = sdid;
            SubVendorId = svid;
            DeviceClass = dclass;
            Bar = new uint[8];
            for(int i = 0; i < 8; i++) Bar[i] = 0;
            BarLen = new uint[8];
            for(int i = 0; i < 8; i++) BarLen[i] = 0;
        }
    }

    public interface IPCIPeripheral : IPeripheral
    {
        PCIInfo GetPCIInfo();

        void WriteDoubleWordPCI(uint bar, long offset, uint value);

        uint ReadDoubleWordPCI(uint bar, long offset);
    }
}