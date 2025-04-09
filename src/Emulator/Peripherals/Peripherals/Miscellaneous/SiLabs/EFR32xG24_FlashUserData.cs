//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
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

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
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
            if (offset >= (long)ManufacturerToken.mfgCustomEui64 && offset < (long)ManufacturerToken.mfgCustomEui64 + (long)ManufacturerTokenLength.mfgCustomEui64Length)
            {
                return Eui64(offset);
            }
            else if (offset >= (long)ManufacturerToken.mfgCcaThreshold && offset < (long)ManufacturerToken.mfgCcaThreshold + (long)ManufacturerTokenLength.mfgCcaThresholdLength)
            {
                return CcaThreshold(offset);
            }
            else
            {
                this.Log(LogLevel.Error, "Unhandled read at offset 0x{0:X}", offset);
            }

            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Error, "Writing manufacturer token space");
        }

        public byte Eui64(long offset)
        {
            int byteIndex = (int)(offset - (long)ManufacturerToken.mfgCustomEui64);

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

        // The format of this token is:
        // uint8_t ccaThresholdSubNeg     : 7, // bits <15:9> unsigned but negated
        // bool    ccaThreshold2p4Invalid : 1, // bit  <8> 1=invalid 0=valid
        // int8_t  ccaThreshold2p4        : 8, // bits <7:0> signed 2's complement
        // where:
        // ccaThresholdSubNeg is 7-bit unsigned value where values 0 and 127
        //   both mean "use default" (which is what allows us to jam SubGHz
        //   CCA threshold oonfiguration into this pre-existing token), and
        //   values 1..126 are negated to map to -1..-126 dBm, respectively.
        // ccaThreshold2p4Invalid indicates whether ccaThreshold2p4 is valid;
        // ccaThreshold2p4 is the actual signed 2's complement CCA threshold;
        public byte CcaThreshold(long offset)
        {
            int byteIndex = (int)(offset - (long)ManufacturerToken.mfgCcaThreshold);
            if (byteIndex == 0)
            {
                return (byte)ccaThreshold;
            }
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
        public static sbyte ccaThreshold = -75;
        private enum ManufacturerToken
        {
            mfgCustomEui64           = 0x002,
            mfgCustomVersion         = 0x00C,
            mfgString                = 0x010,
            mfgBoardName             = 0x020,
            mfgManufacturerId        = 0x030,
            mfgPhyConfig             = 0x034,
            mfgAshConfig             = 0x038,
            mfgSynthFrequencyOffset  = 0x060,
            mfgCcaThreshold          = 0x064,
            mfgEzspStorage           = 0x068,
            mfgXoTune                = 0x070,
            mfgZwaveCountryFrequency = 0x074,
            mfgZwaveHardwareVersion  = 0x078,
            mfgZwavePseudoRng        = 0x07C,
            mfgSerialNumber          = 0x08C,
            mfgLfxoTune              = 0x09C,
            mfgCtune                 = 0x100,
            mfgKitSignature          = 0x104,
        }

        private enum ManufacturerTokenLength
        {
            mfgCustomEui64Length           = 8,
            mfgCustomVersionLength         = 2,
            mfgStringLength                = 16,
            mfgBoardNameLength             = 16,
            mfgManufacturerIdLength        = 2,
            mfgPhyConfigLength             = 2,
            mfgAshConfigLength             = 40,
            mfgSynthFrequencyOffsetLength  = 2,
            mfgCcaThresholdLength          = 2,
            mfgEzspStorageLength           = 8,
            mfgXoTuneLength                = 2,
            mfgZwaveCountryFrequencyLength = 1,
            mfgZwaveHardwareVersionLength  = 1,
            mfgZwavePseudoRngLength        = 16,
            mfgSerialNumberLength          = 16,
            mfgLfxoTuneLength              = 1,
            mfgCtuneLength                 = 2,
            mfgKitSignatureLength          = 4,
        }
    }
}