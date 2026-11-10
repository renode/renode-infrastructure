//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public abstract partial class ARM_EthosBase
{
    protected const uint MaximumCommandBytes = 0x1000000; // 16 MB
    protected const uint Cmd0Encoding = 0b000000;
    protected const uint Cmd1Encoding = 0b010000;
}
