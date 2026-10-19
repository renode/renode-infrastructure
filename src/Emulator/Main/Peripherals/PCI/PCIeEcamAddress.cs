//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.PCI;

public struct EcamAddress
{
    public EcamAddress(uint address) : this()
    {
        Bus = (int)BitHelper.GetValue(address, 20, 8);
        Device = (int)BitHelper.GetValue(address, 15, 5);
        Function = (int)BitHelper.GetValue(address, 12, 3);
        Offset = BitHelper.GetValue(address, 0, 12);
    }

    public int Bus;
    public int Device;
    public int Function;
    public uint Offset;
}
