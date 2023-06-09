//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.DMA
{
    public struct Response
    {
        public ulong? ReadAddress { get; set; }
        public ulong? WriteAddress { get; set; }
    }
}

