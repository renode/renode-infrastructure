//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public abstract partial class ARM_EthosBase
{
    // Combine a CMD1 header's upper address bits (parameter) with its 32-bit payload into an
    // address of the given width: 40 bits on the U85 (addr_hi in bits 23:16), 48 on the U55.
    protected static ulong ExtendAddress(ushort parameter, uint payload, int bits)
    {
        var upperMask = (1UL << (bits - 32)) - 1;
        return ((parameter & upperMask) << 32) | payload;
    }

    protected static long SignExtendAddress(ushort parameter, uint payload, int bits)
    {
        return unchecked((long)BitHelper.SignExtend(ExtendAddress(parameter, payload, bits), bits));
    }
}
