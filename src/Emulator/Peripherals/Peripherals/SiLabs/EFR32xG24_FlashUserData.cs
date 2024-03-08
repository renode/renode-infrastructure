//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public class EFR32xG24_FlashUserData : IBytePeripheral, IKnownSize
    {
        public EFR32xG24_FlashUserData(Machine machine)
        {
            this.machine = machine;
            this.uid = ++count;
        }

        public void Reset()
        {
        }

        public byte ReadByte(long offset)
        {
            if (offset >= (long)ManufacturerToken.eui64 && offset < (long)ManufacturerToken.eui64 + Eui64Size)
            {
                return Eui64(offset);
            }
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Error, "Writing manufacturer token space");
        }

        public byte Eui64(long offset)
        {
            int byteIndex = (int)(offset - (long)ManufacturerToken.eui64);

            if (byteIndex >=  Eui64Size)
            {
                this.Log(LogLevel.Error, "EUI64 index out of bounds {0}", byteIndex);
                return 0;
            }

            // Most significant bytes represent the company OUI
            if (byteIndex >= (Eui64Size - Eui64OUILength)) {
                return SiliconLabsEui64OUI[byteIndex - (Eui64Size - Eui64OUILength)];
            }
            // Least significant 4 bytes are the UID
            else if (byteIndex < 4)
            {
                return (byte)((uid >> byteIndex*8) & 0xFF);
            }
            // We set the rest of the bytes to zeros
            else
            {
                return 0;
            }
        }

        public long Size => 0xA;
        private const uint Eui64Size = 8;
        public const uint Eui64OUILength = 3;
        private uint uid;
        private readonly Machine machine;
        private static uint count = 0;
        public static readonly byte[] SiliconLabsEui64OUI = {0xCC, 0xCC, 0xCC};
        private enum ManufacturerToken
        {
            eui64 = 0x02,
        }
    }
}