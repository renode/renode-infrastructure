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

        // The data is only present if the destination or source is a buffer, and not a memory address
        // The data is read from either ReadAddress location or ReadData buffer
        // And written to either WriteAddress location or WriteData buffer
        public byte[] ReadData { get; set; }

        public byte[] WriteData { get; set; }
    }
}