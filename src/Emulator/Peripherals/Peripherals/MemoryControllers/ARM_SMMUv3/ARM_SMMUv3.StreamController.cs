//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public interface ISMMUv3StreamController
    {
        bool Enabled { get; set; }

        void InvalidateTlb(ulong? virtualAddress = null);
    }
}