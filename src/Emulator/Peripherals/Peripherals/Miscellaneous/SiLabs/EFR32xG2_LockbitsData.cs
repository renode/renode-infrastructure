//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EFR32xG2_LockbitsData : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_LockbitsData(uint size, 
                            String cbkeDataCertificate = null,
                            String cbkeDataCaPublicKey = null,
                            String cbkeDataPrivateKey = null,
                            String cbkeDataFlags = null,
                            String securityConfig = null,
                            String installationCodeFlags = null, 
                            String installationCodeValue = null,
                            String installationCodeCrc = null,
                            String secureBootloaderKey = null,
                            String cbke283k1DataCertificate = null,
                            String cbke283k1DataCaPublicKey = null,
                            String cbke283k1DataPrivateKey = null,
                            String ccbke283k1DataFlags = null,
                            String bootloadAesKey = null,
                            String signedBootloaderKeyX = null,
                            String signedBootloaderKeyY = null,
                            String threadJoinKey = null,
                            byte threadJoinKeyLength = 0,
                            String nvm3CryptoKey = null,
                            String zWavePrivateKey = null,
                            String zWavePublicKey = null,
                            String zWaveQrCode = null,
                            String zWaveInitialized = null,
                            String zWaveQrCodeExtended = null)
        {
            if ((size & 0x3) > 0)
            {
                throw new ConstructionException("Size must be a multiple of 4");
            }
            this.size = size;

            memory = new byte[size];
            for(uint i = 0; i < size; i++)
            {
                memory[i] = 0xFF;
            }

            if (cbkeDataCertificate != null)
            {
                ParseHexStringArgument("cbke data certificate", cbkeDataCertificate, 48, (uint)DataOffset.CbkeData);
            }
            if (cbkeDataCaPublicKey != null)
            {
                ParseHexStringArgument("cbke data public key", cbkeDataCaPublicKey, 22, (uint)DataOffset.CbkeData + 48);
            }
            if (cbkeDataPrivateKey != null)
            {
                ParseHexStringArgument("cbke data private key", cbkeDataPrivateKey, 21, (uint)DataOffset.CbkeData + 48 + 22);
            }
            if (cbkeDataFlags != null)
            {
                ParseHexStringArgument("cbke data flags", cbkeDataFlags, 1, (uint)DataOffset.CbkeData + 48 + 22 + 21);
            }
            if (securityConfig != null)
            {
                ParseHexStringArgument("security config", securityConfig, 2, (uint)DataOffset.SecurityConfig);
            }
            if (installationCodeFlags != null)
            {
                ParseHexStringArgument("installation code flags", installationCodeFlags, 2, (uint)DataOffset.InstallationCode);
            }
            if (installationCodeValue != null)
            {
                ParseHexStringArgument("installation code value", installationCodeValue, 16, (uint)DataOffset.InstallationCode + 2);
            }
            if (installationCodeCrc != null)
            {
                ParseHexStringArgument("installation code CRC", installationCodeCrc, 2, (uint)DataOffset.InstallationCode + 2 + 16);
            }
            if (secureBootloaderKey != null)
            {
                ParseHexStringArgument("secure bootloader key", secureBootloaderKey, 16, (uint)DataOffset.SecureBootloaderKey);
            }
            if (cbke283k1DataCertificate != null)
            {
                ParseHexStringArgument("cbke 283k certificate", cbke283k1DataCertificate, 74, (uint)DataOffset.Cbke283k1Data);
            }
            if (cbke283k1DataCaPublicKey != null)
            {
                ParseHexStringArgument("cbke 283k public key", cbke283k1DataCaPublicKey, 37, (uint)DataOffset.Cbke283k1Data + 74);
            }
            if (cbke283k1DataPrivateKey != null)
            {
                ParseHexStringArgument("cbke 283k private key", cbke283k1DataPrivateKey, 36, (uint)DataOffset.Cbke283k1Data + 74 + 37);
            }
            if (ccbke283k1DataFlags != null)
            {
                ParseHexStringArgument("cbke 283k flags", ccbke283k1DataFlags, 1, (uint)DataOffset.Cbke283k1Data + 74 + 37 + 36);
            }
            if (bootloadAesKey != null)
            {
                ParseHexStringArgument("bootloader AES key", bootloadAesKey, 16, (uint)DataOffset.BootloadAesKey);
            }
            if (signedBootloaderKeyX != null)
            {
                ParseHexStringArgument("signed bootloader key X", signedBootloaderKeyX, 32, (uint)DataOffset.SignedBootloaderKeyX);
            }
            if (signedBootloaderKeyY != null)
            {
                ParseHexStringArgument("signed bootloader key Y", signedBootloaderKeyY, 32, (uint)DataOffset.SignedBootloaderKeyY);
            }
            if (threadJoinKey != null)
            {
                if (threadJoinKeyLength > 32)
                {
                    throw new ConstructionException("threadJoinKeyLength > 32");
                }
                ParseHexStringArgument("thread join key", threadJoinKey, threadJoinKeyLength, (uint)DataOffset.ThreadJoinKey);
            }
            memory[(uint)DataOffset.ThreadJoinKey + 32] = threadJoinKeyLength;
            if (nvm3CryptoKey != null)
            {
                ParseHexStringArgument("NVM3 crypto key", nvm3CryptoKey, 16, (uint)DataOffset.Nvm3CryptoKey);
            }
            if (zWavePrivateKey != null)
            {
                ParseHexStringArgument("Z-Wave private key", zWavePrivateKey, 32, (uint)DataOffset.ZWavePrivateKey);
            }
            if (zWavePublicKey != null)
            {
                ParseHexStringArgument("Z-Wave public key", zWavePublicKey, 32, (uint)DataOffset.ZWavePublicKey);
            }
            if (zWaveQrCode != null)
            {
                ParseHexStringArgument("Z-Wave QR code", zWaveQrCode, 90, (uint)DataOffset.ZWaveQrCode);
            }
            if (zWaveInitialized != null)
            {
                ParseHexStringArgument("Z-Wave initialized", zWaveInitialized, 1, (uint)DataOffset.ZWaveInitialized);
            }
            if (zWaveQrCodeExtended != null)
            {
                ParseHexStringArgument("Z-Wave QR code extended", zWaveQrCodeExtended, 90, (uint)DataOffset.ZWaveQrCodeExtended);
            }
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            if ((offset & 0x3) > 0)
            {
                this.Log(LogLevel.Error, "ReadDoubleWord: Offset must be a multiple of 4");
                return 0;
            }

            uint ret = (memory[offset] 
                        | ((uint)memory[offset + 1] << 8)
                        | ((uint)memory[offset + 2] << 16)
                        | ((uint)memory[offset + 3] << 24));
            return ret;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if ((offset & 0x3) > 0)
            {
                this.Log(LogLevel.Error, "WriteDoubleWord: Offset must be a multiple of 4");
                return;
            }

            memory[offset] = (byte)(value & 0xFF);
            memory[offset + 1] = (byte)((value >> 8) & 0xFF);
            memory[offset + 2] = (byte)((value >> 16) & 0xFF);
            memory[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void ParseHexStringArgument(string fieldName, string value, uint expectedLength, uint destinationOffset)
        {
            byte[] temp;
            var lengthInBytes = value.Length / 2;
            
            if(lengthInBytes != expectedLength)
            {
                throw new ConstructionException($"Expected `{fieldName}`'s size is {expectedLength} bytes, got {lengthInBytes}");
            }
            try
            {
                temp = Misc.HexStringToByteArray(value);
            }
            catch
            {
                throw new ConstructionException($"Could not parse `{fieldName}`: Expected hexstring, got: \"{value}\"");
            }
            
            temp.CopyTo(memory, destinationOffset);
        }


        private uint size;
        private byte[] memory; 
        public long Size => (long)size;

        private enum DataOffset
        {
            CbkeData             = 0x204,
            SecurityConfig       = 0x260,
            InstallationCode     = 0x270,
            SecureBootloaderKey  = 0x286,
            Cbke283k1Data        = 0x298,
            BootloadAesKey       = 0x32C,
            SignedBootloaderKeyX = 0x34C,
            SignedBootloaderKeyY = 0x36C,
            ThreadJoinKey        = 0x38C,
            Nvm3CryptoKey        = 0x3B0,
            ZWavePrivateKey      = 0x3C0,
            ZWavePublicKey       = 0x3E0,
            ZWaveQrCode          = 0x400,
            ZWaveInitialized     = 0x45C,
            ZWaveQrCodeExtended  = 0x460,
        }
    }
}
