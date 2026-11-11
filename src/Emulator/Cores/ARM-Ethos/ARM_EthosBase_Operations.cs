//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public abstract partial class ARM_EthosBase
{
    protected static (int Tile, long Row, long Column) SelectTile2x2(long row, long column, long width0, long height0, long height1)
    {
        var tile = 0;
        var tileHeight = height0;
        if(column >= width0)
        {
            column -= width0;
            tile = 1;
            tileHeight = height1;
        }
        if(row >= tileHeight)
        {
            row -= tileHeight;
            tile += 2;
        }
        return (tile, row, column);
    }

    // Accumulators wrap at the width selected by NPU_SET_ACC_FORMAT
    // (U55 TRM 4.4, 0x124, and U85 TRM accumulate()).
    protected static long Accumulate(long accumulator, long value, int accumulatorBits)
        => SignExtendTruncated(accumulator + value, accumulatorBits);

    protected static long ReadValue(byte[] bytes, int byteId, int byteLength, bool signed)
    {
        if(byteId < 0 || byteId + byteLength > bytes.Length)
        {
            throw new InvalidOperationException($"Out of bounds read at {byteId} ({byteLength} bytes, buffer length {bytes.Length})");
        }

        return byteLength switch
        {
            1 => signed ? unchecked((sbyte)bytes[byteId]) : bytes[byteId],
            2 => signed ? BitConverter.ToInt16(bytes, byteId) : BitConverter.ToUInt16(bytes, byteId),
            4 => signed ? BitConverter.ToInt32(bytes, byteId) : BitConverter.ToUInt32(bytes, byteId),
            8 => signed ? BitConverter.ToInt64(bytes, byteId) : unchecked((long)BitConverter.ToUInt64(bytes, byteId)),
            _ => throw new InvalidOperationException($"Invalid element byte length: {byteLength}"),
        };
    }

    protected static void WriteValue(byte[] bytes, long byteId, long value, int byteLength)
    {
        if(byteId < 0 || byteId + byteLength > bytes.Length)
        {
            throw new InvalidOperationException($"Out of bounds write at {byteId} ({byteLength} bytes, buffer length {bytes.Length})");
        }

        for(var i = 0; i < byteLength; i++)
        {
            bytes[byteId + i] = unchecked((byte)(value >> (8 * i)));
        }
    }

    // Truncate to the requested width and interpret the result as signed.
    protected static long SignExtendTruncated(long value, int bitCount)
    {
        if(bitCount <= 0 || bitCount > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }
        if(bitCount == 64)
        {
            return value;
        }
        return unchecked((long)BitHelper.SignExtend(BitHelper.SignTruncate(value, bitCount), bitCount));
    }

    // U85 TRM rounded_shift_right
    protected static long RoundedShiftRight(long acc, int shift, RoundingMode mode, int doubleRoundShift = 0)
    {
        if(shift < 0 || shift > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(shift));
        }
        if(doubleRoundShift < 0 || doubleRoundShift > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleRoundShift));
        }

        if(shift == 0)
        {
            return acc;
        }

        var rounding = Int128.One << (shift - 1);
        var doubleRounding = shift > (31 - doubleRoundShift) ? Int128.One << (30 - doubleRoundShift) : Int128.Zero;
        switch(mode)
        {
        case RoundingMode.DoubleSymmetric:
            rounding += acc >= 0 ? doubleRounding : -doubleRounding;
            break;
        case RoundingMode.DoubleAsymmetric:
            rounding += doubleRounding;
            break;
        case RoundingMode.TruncateToZero:
            rounding = acc >= 0 ? Int128.Zero : (Int128.One << shift) - 1;
            break;
        case RoundingMode.TruncateToLower:
            rounding = Int128.Zero;
            break;
        case RoundingMode.Natural:
            break;
        case RoundingMode.Symmetric:
            if(acc < 0)
            {
                rounding -= 1;
            }
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        return (long)(((Int128)acc + rounding) >> shift);
    }

    // Decode the activation type of an xFM precision register.
    protected (bool Signed, int ByteLength) DecodeActivationType(ushort precision, bool isOfm)
    {
        var signed = (precision & 1) != 0;
        var precisionBits = isOfm ? (precision >> 1) & 0b11 : (precision >> 2) & 0b11;
        var byteLength = precisionBits switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            3 when Supports64BitActivations => 8,
            _ => 0,
        };
        return (signed, byteLength);
    }

    protected ushort GetPrecision(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => CommonRegisters.IfmPrecision,
            FeatureMap.Ifm2 => CommonRegisters.Ifm2Precision,
            FeatureMap.Ofm => CommonRegisters.OfmPrecision,
            _ => throw new UnreachableException(),
        };
    }

    protected (long X, long Y, long Channel) GetTensorStrides(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => (CommonRegisters.IfmStrideX, CommonRegisters.IfmStrideY, CommonRegisters.IfmStrideC),
            FeatureMap.Ifm2 => (CommonRegisters.Ifm2StrideX, CommonRegisters.Ifm2StrideY, CommonRegisters.Ifm2StrideC),
            FeatureMap.Ofm => (CommonRegisters.OfmStrideX, CommonRegisters.OfmStrideY, CommonRegisters.OfmStrideC),
            _ => throw new UnreachableException(),
        };
    }

    protected long GetZeroPoint(FeatureMap map, bool? signed = null)
    {
        var raw = map switch
        {
            FeatureMap.Ifm => CommonRegisters.IfmZeroPoint,
            FeatureMap.Ifm2 => CommonRegisters.Ifm2ZeroPoint,
            FeatureMap.Ofm => CommonRegisters.OfmZeroPoint,
            _ => throw new UnreachableException(),
        };

        return (signed ?? IsSigned(map)) ? unchecked((short)raw) : raw;
    }

    protected bool TryGetIfmUpscale(out IfmUpscaleMode upscale)
    {
        var raw = CommonRegisters.IfmUpscale;
        if(raw > (ushort)IfmUpscaleMode.InsertZeros)
        {
            this.ErrorLog("Unsupported IFM upscale mode: {0}", raw);
            upscale = IfmUpscaleMode.None;
            return false;
        }

        upscale = (IfmUpscaleMode)raw;
        return true;
    }

    // NPU_SET_xFM_HEIGHT1_M1 splits the right column, so tiles 1 and 3.
    protected long GetTileHeight1(FeatureMap map)
    {
        return 1 + map switch
        {
            FeatureMap.Ifm => CommonRegisters.IfmHeight1M1,
            FeatureMap.Ifm2 => CommonRegisters.Ifm2Height1M1,
            FeatureMap.Ofm => CommonRegisters.OfmHeight1M1,
            _ => throw new UnreachableException(),
        };
    }

    // Tile 0, 2 (left-hand tiles) dimensions and depth.
    protected (int Width, int Height, int Depth) GetTensorDimensions(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => (CommonRegisters.IfmWidth0M1 + 1, CommonRegisters.IfmHeight0M1 + 1, CommonRegisters.IfmDepthM1 + 1),
            FeatureMap.Ifm2 => (CommonRegisters.Ifm2Width0M1 + 1, CommonRegisters.Ifm2Height0M1 + 1, CommonRegisters.IfmDepthM1 + 1),
            FeatureMap.Ofm => (CommonRegisters.OfmWidth0M1 + 1, CommonRegisters.OfmHeight0M1 + 1, CommonRegisters.OfmDepthM1 + 1),
            _ => throw new UnreachableException(),
        };
    }

    protected bool IsSigned(FeatureMap map)
    {
        return (GetPrecision(map) & 1) != 0;
    }

    protected bool IsNHCWB16Layout(FeatureMap map)
    {
        return (GetPrecision(map) & (1 << 6)) != 0;
    }

    // Whole stripe dimensions.
    protected (int Width, int Height, int Depth) GetOfmStripeDimensions()
    {
        return (CommonRegisters.OfmWidthM1 + 1, CommonRegisters.OfmHeightM1 + 1, CommonRegisters.OfmDepthM1 + 1);
    }

    protected bool TryGetBaseAddress(FeatureMap map, int tile, out ulong address)
    {
        var (region, offset) = map switch
        {
            FeatureMap.Ifm => (CommonRegisters.IfmRegion, tile switch
            {
                0 => CommonRegisters.IfmBase0,
                1 => CommonRegisters.IfmBase1,
                2 => CommonRegisters.IfmBase2,
                _ => CommonRegisters.IfmBase3,
            }),
            FeatureMap.Ifm2 => (CommonRegisters.Ifm2Region, tile switch
            {
                0 => CommonRegisters.Ifm2Base0,
                1 => CommonRegisters.Ifm2Base1,
                2 => CommonRegisters.Ifm2Base2,
                _ => CommonRegisters.Ifm2Base3,
            }),
            FeatureMap.Ofm => (CommonRegisters.OfmRegion, tile switch
            {
                0 => CommonRegisters.OfmBase0,
                1 => CommonRegisters.OfmBase1,
                2 => CommonRegisters.OfmBase2,
                _ => CommonRegisters.OfmBase3,
            }),
            _ => throw new UnreachableException(),
        };
        return TryGetBufferAddress(region, offset, out address, $"{map} tile {tile}");
    }

    protected bool TryGetBufferAddress(int region, ulong offset, out ulong address, string description, ulong length = 1)
    {
        address = 0;
        if(region < 0 || region >= BasePointerCount / 2)
        {
            ParseError($"Invalid {description} region: {region}");
            return false;
        }

        var regionBase = (basePointers[2 * region + 1].Value << 32) | basePointers[2 * region].Value;
        // The whole range must be addressable, not just its first byte.
        if(regionBase > ulong.MaxValue - offset
            || (length != 0 && length - 1 > ulong.MaxValue - (regionBase + offset)))
        {
            ParseError($"{description} address overflow");
            return false;
        }
        address = regionBase + offset;
        return true;
    }

    protected long GetTensorAddress(FeatureMap map, long row, long column, long channel)
    {
        // U85 TRM element_address_in_region
        var elementByteLength = GetElementByteLength(map);
        var (columnStride, rowStride, channelStride) = GetTensorStrides(map);

        if(IsNHCWB16Layout(map))
        {
            const int channelsPerBlock = 16;
            return checked((channel / channelsPerBlock) * channelStride
                + row * rowStride
                + column * channelsPerBlock * elementByteLength
                + (channel % channelsPerBlock) * elementByteLength);
        }

        return checked(channel * elementByteLength + row * rowStride + column * columnStride);
    }

    protected (bool Signed, int ByteLength) GetElementType(FeatureMap map)
    {
        var precision = GetPrecision(map);
        var (signed, byteLength) = DecodeActivationType(precision, isOfm: map == FeatureMap.Ofm);
        if(byteLength == 0)
        {
            throw new InvalidOperationException($"Invalid {map} precision: 0x{precision:X4}");
        }
        return (signed, byteLength);
    }

    protected int GetElementByteLength(FeatureMap map) => GetElementType(map).ByteLength;

    protected enum FeatureMap
    {
        Ifm,
        Ifm2,
        Ofm,
    }

    protected enum IfmUpscaleMode
    {
        None = 0,
        Nearest = 1,
        InsertZeros = 2,
    }

    // Model-neutral rounding modes. Each model decodes its own register encoding into these
    // (U55: OFM_PRECISION bits 15:14, U85: OFM_SCALE.round_mode); never cast raw register values.
    protected enum RoundingMode
    {
        DoubleSymmetric,
        DoubleAsymmetric,
        TruncateToZero,
        TruncateToLower,
        Natural,
        Symmetric,
    }

    protected enum ActivationFormat
    {
        NHWC = 0,
        NHCWB16 = 1,
    }
}
