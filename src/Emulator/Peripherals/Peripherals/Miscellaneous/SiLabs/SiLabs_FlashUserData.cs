//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_FlashUserData : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public static sbyte DefaultCcaThreshold = -75;

        public SiLabs_FlashUserData(Machine machine)
        {
            this.machine = machine;
            this.uid = ++count;
        }

        public void Reset()
        {
        }

        public byte ReadByte(long offset)
        {
            if(offset >= (long)ManufacturerToken.mfgCustomEui64 && offset < (long)ManufacturerToken.mfgCustomEui64 + (long)ManufacturerTokenLength.mfgCustomEui64Length)
            {
                return Eui64(offset);
            }
            else if(offset >= (long)ManufacturerToken.mfgCcaThreshold && offset < (long)ManufacturerToken.mfgCcaThreshold + (long)ManufacturerTokenLength.mfgCcaThresholdLength)
            {
                return CcaThreshold(offset);
            }
            else if(offset >= (long)ManufacturerToken.mfgAshConfig && offset < (long)ManufacturerToken.mfgAshConfig + (long)ManufacturerTokenLength.mfgAshConfigLength)
            {
                return AshConfig(offset);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X}", offset);
            }

            return 0xFF;
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        }

        public uint ReadDoubleWord(long offset)
        {
            return (uint)((ReadWord(offset) << 16) | ReadWord(offset + 2));
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Error, "Writing manufacturer token space");
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteByte(offset, (byte)(value >> 8));
            WriteByte(offset + 1, (byte)(value & 0xFF));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteWord(offset, (ushort)(value >> 16));
            WriteWord(offset + 2, (ushort)(value & 0xFFFF));
        }

        public byte Eui64(long offset)
        {
            int byteIndex = (int)(offset - (long)ManufacturerToken.mfgCustomEui64);

            if(byteIndex >= Eui64Size)
            {
                this.Log(LogLevel.Error, "EUI64 index out of bounds {0}", byteIndex);
                return 0;
            }

            // Most significant bytes represent the company OUI
            if(byteIndex >= (Eui64Size - Eui64OUILength))
            {
                return siliconLabsEui64OUI[byteIndex - (Eui64Size - Eui64OUILength)];
            }
            // Least significant 4 bytes are the UID
            else if(byteIndex < 4)
            {
                return (byte)((uid >> byteIndex * 8) & 0xFF);
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
            if(byteIndex == 0)
            {
                return (byte)DefaultCcaThreshold;
            }
            else
            {
                return 0;
            }
        }

        // The format of this token is (all 2-byte words):
        // 0-1: baudRate:           ///< SerialBaudRate enum value
        // 2-3: 2-3: traceFlags:         ///< trace output control bit flags (see defs below)
        // 4-5: unused0:            ///< (not used)
        // 6-7: txK:                ///< max frames that can be sent w/o being ACKed (1-7)
        // 8-9: randomize:          ///< enables randomizing DATA frame payloads
        // 10-11: ackTimeInit:        ///< adaptive rec'd ACK timeout initial value
        // 12-13: ackTimeMin:         ///<  "     "     "     "     "  minimum
        // 14-15: ackTimeMax:         ///<  "     "     "     "     "  maximum
        // 16-17: maxTimeouts:        ///< ACK timeouts needed to enter the ERROR state
        // 18-19: unused1:            ///< (not used)
        // 20-21: rebootDelay:        ///< reboot delay before sending RSTACK
        // 22-23: unused2:            ///< (not used)
        // 24-25: unused3:            ///< (not used)
        // 26-27: unused4:            ///< (not used)
        // 28-29: nrTime:             ///< time after which a rec'd nFlag expires
        public byte AshConfig(long offset)
        {
            int byteIndex = (int)(offset - (long)ManufacturerToken.mfgAshConfig);

            // For now we hard-code the rebootDelay to 0 to speed up Zigbee HOST/NCP testing
            // (default value when the token returns 0xFFFF is 1s).
            if(byteIndex == 20 || byteIndex == 21)
            {
                return 0;
            }

            // The rest of the token for now returns 0xFF which causes firmware to use
            // software-level default values.
            return 0xFF;
        }

        public long Size => 0xA;

        public const uint Eui64OUILength = 3;
        private static uint count = 0;
        private readonly uint uid;
        private readonly Machine machine;
        private static readonly byte[] siliconLabsEui64OUI = {0xCC, 0xCC, 0xCC};
        private const uint Eui64Size = 8;

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