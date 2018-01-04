//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.PCI
{
    public struct PCIInfo {
    	
	public uint[] BAR;
	public uint[] BAR_len;
	public ushort device_id;
	public ushort vendor_id;
	public ushort sub_device_id;
	public ushort sub_vendor_id;
	public ushort device_class;

	public PCIInfo(ushort did, ushort vid, ushort sdid, ushort svid, ushort dclass) {
		device_id = did;
		vendor_id = vid;
		sub_device_id = sdid;
		sub_vendor_id = svid;
		device_class = dclass;
		BAR = new uint[8];
		for (int i = 0; i < 8; i++) BAR[i] = 0;
		BAR_len = new uint[8];
		for (int i = 0; i < 8; i++) BAR_len[i] = 0;
	}
    }

    public interface IPCIPeripheral : IPeripheral
    {
        PCIInfo GetPCIInfo();
	void WriteDoubleWordPCI (uint bar, long offset, uint value);
	uint ReadDoubleWordPCI (uint bar, long offset);
    }
}

