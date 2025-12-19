//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public partial class GenericSpiFlash
    {
        public abstract class SFDPParameter
        {
            public SFDPParameter(byte major, byte minor, byte manufacturerId)
            {
                Major = major;
                Minor = minor;
                ManufacturerId = manufacturerId;
            }

            public byte[] FirstHeaderDWord()
            {
                var firstDwordHeader = new byte[DWord];
                firstDwordHeader[0] = ManufacturerId;
                firstDwordHeader[1] = Minor;
                firstDwordHeader[2] = Major;
                var length = this.Bytes.Length;
                if(length > byte.MaxValue)
                {
                    throw new RecoverableException($"SFDP parameter length ({length}) exceeeds {byte.MaxValue} bytes.");
                }
                firstDwordHeader[3] = (byte)length;
                return firstDwordHeader;
            }

            public byte[] Bytes
            {
                get
                {
                    if(bytes == null)
                    {
                        bytes = ToBytes();
                    }
                    return bytes;
                }
            }

            public byte Major { get; }

            public byte Minor { get; }

            public byte ManufacturerId { get; }

            protected abstract byte[] ToBytes();

            protected const byte DWord = 4;

            private byte[] bytes = null;
        }

        protected class JEDECParameter : SFDPParameter
        {
            // Write Enable, Write Disable, Read, and Program opcodes are standardized.            
            public JEDECParameter(int eraseSize, long flashDensity, int pageSize, byte eraseCode, byte major = 1, byte minor = 0x6)
                : base(major, minor, 0)
            {
                EraseSize = eraseSize;
                EraseCode = eraseCode;
                FlashDensity = flashDensity;
                PageSize = pageSize;
            }

            public byte EraseCode { get; }

            public long FlashDensity { get; }

            public int PageSize { get; }

            public int EraseSize { get; }

            protected override byte[] ToBytes()
            {
                var result = new byte[23 * DWord];

                // Is 4KB erase supported
                var firstDW = new byte[4];
                if(EraseSize == 4.KB())
                {
                    firstDW[1] = EraseCode;
                }
                Array.Copy(firstDW, 0, result, FirstDWIndex * DWord, firstDW.Length);

                // Flash Density

                // JESD216H, Section 6.4.5:
                // For densities 2 gigabits or less, bit-31 is set to 0b. The field 30:0 defines the size in bits.
                // Example: 00FFFFFFh = 16 megabits
                // For densities 4 gigabits and above, bit-31 is set to 1b. The field 30:0 defines ‘N’ where the
                // density is computed as 2^N bits (N must be >= 32).
                // Example: 80000021h = 2^33 = 8 gigabits
                if(FlashDensity <= MaxLinearlySavedSize)
                {
                    var flashDensityBin = BitHelper.GetBytesFromValue((ulong)FlashDensity * 8 - 1, 4, true);
                    Array.Copy(flashDensityBin, 0, result, FlashDensityDWIndex * DWord, flashDensityBin.Length);
                }
                else
                {
                    var flashDensityLog = (uint)Math.Floor(Math.Log(FlashDensity, 2)) | (uint)BitHelper.Bit(31);
                    var flashDensityLogBin = BitHelper.GetBytesFromValue((ulong)flashDensityLog, 4, true);
                    Array.Copy(flashDensityLogBin, 0, result, FlashDensityDWIndex * DWord, flashDensityLogBin.Length);
                }

                // Erase Size
                var eraseSizeBin = (byte)Math.Floor(Math.Log(EraseSize, 2));
                result[EraseDWIndex * DWord] = eraseSizeBin;
                result[EraseDWIndex * DWord + 1] = EraseCode;

                // Page Size
                var pageSizeLogBin = (byte)Math.Floor(Math.Log(PageSize, 2));
                result[PageSizeDWIndex * DWord] = (byte)(pageSizeLogBin << 4);

                // Enter/Exit 4 bytes adressing mode, Soft Reset sequence
                var sixteenthDW = Enter4ByteWENotRequired << Enter4ByteShift |
                                    Exit4ByteWENotRequired << Exit4ByteShift |
                                    ResetEnableShift <<  ResetEnableShift;

                var sixteenthDWBin = BitHelper.GetBytesFromValue((ulong)sixteenthDW, 4, true);
                Array.Copy(sixteenthDWBin, 0, result, SixtenenthDWIndex * DWord, sixteenthDWBin.Length);

                return result;
            }

            private const uint Enter4ByteWENotRequired = 0b1;
            private const int Enter4ByteShift = 24;
            private const uint Exit4ByteWENotRequired = 0b1;
            private const int Exit4ByteShift = 14;

            private const uint ResetEnableRequired = 0b10000;
            private const int ResetEnableShift = 8;

            private const long MaxLinearlySavedSize =  2 * (1u << 30) / 8;

            private const int FirstDWIndex = 0;
            private const int SixtenenthDWIndex = 0;

            private const int FlashDensityDWIndex = 1;

            private const int EraseDWIndex = 7;

            private const int PageSizeDWIndex = 10;
        }

        protected class SFDP
        {
            public SFDP(IReadOnlyDictionary<uint, SFDPParameter> parameters, byte major = 1, byte minor = 0xC)
            {
                Major = major;
                Minor = minor;
                ParametersDictionary = parameters;
            }

            public byte Major { get; }

            public byte Minor { get; }

            public IReadOnlyDictionary<uint, SFDPParameter> ParametersDictionary { get; }

            public byte NPH { get => (byte)(ParametersDictionary.Count - 1); }

            public byte[] Bytes
            {
                get
                {
                    if(bytes == null)
                    {
                        bytes = ToBytes();
                    }
                    return bytes;
                }
            }

            private byte[] ToBytes()
            {
                var totalSize = ParametersDictionary.Max(kvp => kvp.Key + (uint)kvp.Value.Bytes.Length);
                var result = new byte[totalSize];

                // SFDP Header
                // magic bytes "SFDP"
                var sfdpHeader = new byte[SfdpHeaderLength];
                var magicBytes = Encoding.ASCII.GetBytes("SFDP");

                Array.Copy(magicBytes, sfdpHeader, magicBytes.Length);
                sfdpHeader[4] = Minor;
                sfdpHeader[5] = Major;
                sfdpHeader[6] = NPH;
                sfdpHeader[7] = 0xFF; // Unused

                Array.Copy(sfdpHeader, result, sfdpHeader.Length);

                // Parameters Header
                var parametersHeader = new byte[ParameterHeaderLength * (ParametersDictionary.Count)];
                var index = 0;
                foreach(var kvp in ParametersDictionary)
                {
                    var ptp = kvp.Key;
                    var parameter = kvp.Value;
                    var parameterHeader = new byte[ParameterHeaderLength];
                    Array.Copy(parameter.FirstHeaderDWord(), parameterHeader, DWord);
                    if(ptp > PTPMaxValue)
                    {
                        throw new RecoverableException($"SFDP Parameter Table Pointer length ({ptp}) exceeeds {PTPMaxValue}");
                    }
                    parameterHeader[DWord] = (byte)ptp;
                    parameterHeader[DWord + 1] = (byte)(ptp >> 8);
                    parameterHeader[DWord + 2] = (byte)(ptp >> 16);
                    parameterHeader[DWord + 3] = 0xFF; // Unused

                    Array.Copy(parameterHeader, 0, parametersHeader, parameterHeader.Length * index, parameterHeader.Length);
                    index++;
                }

                Array.Copy(parametersHeader, 0, result, sfdpHeader.Length, parametersHeader.Length);

                // Parameter Table
                foreach(var kvp in ParametersDictionary)
                {
                    var ptp = kvp.Key;
                    var parameter = kvp.Value;
                    var paramBytes = parameter.Bytes;
                    Array.Copy(paramBytes, 0, result, ptp, paramBytes.Length);
                }

                return result;
            }

            private byte[] bytes;

            private const uint PTPMaxValue = 0xFFFFFF;

            private const int DWord = 4;

            private const int SfdpHeaderLength = 2 * DWord;

            private const int ParameterHeaderLength = 2 * DWord;
        }
    }
}
