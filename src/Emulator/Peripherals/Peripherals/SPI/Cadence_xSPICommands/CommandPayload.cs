//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal class CommandPayload
    {
        public CommandPayload(uint[] payload)
        {
            const int RequiredLength = 6;
            if(payload.Length != RequiredLength)
            {
                throw new ArgumentOutOfRangeException($"The payload must be {RequiredLength} elements length.");
            }
            this.payload = payload;
        }

        // The payload is immutable.
        public uint this[int index] => payload[index];

        private readonly uint[] payload;
    }
}
