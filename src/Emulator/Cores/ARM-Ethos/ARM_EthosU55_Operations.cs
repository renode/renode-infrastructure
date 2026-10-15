//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;
using System.Numerics;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU55
{
    private static long ReadValue(byte[] bytes, int byteId, int byteLength, bool signed)
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
            _ => throw new InvalidOperationException($"Invalid element byte length: {byteLength}"),
        };
    }

    private static void WriteValue(byte[] bytes, long byteId, long value, int byteLength)
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

    // U85 TRM rounded_shift_right
    private static long RoundedShiftRight(long acc, int shift, RoundingMode mode, int doubleRoundShift = 0)
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

        var rounding = BigInteger.One << (shift - 1);
        var doubleRounding = shift > (31 - doubleRoundShift) ? BigInteger.One << (30 - doubleRoundShift) : BigInteger.Zero;
        switch(mode)
        {
        case RoundingMode.DoubleSymmetric:
            rounding += acc >= 0 ? doubleRounding : -doubleRounding;
            break;
        case RoundingMode.DoubleAsymmetric:
            rounding += doubleRounding;
            break;
        case RoundingMode.TruncateToZero:
            rounding = acc >= 0 ? BigInteger.Zero : (BigInteger.One << shift) - 1;
            break;
        case RoundingMode.TruncateToLower:
            rounding = BigInteger.Zero;
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
        return (long)(((BigInteger)acc + rounding) >> shift);
    }

    // Adapted from:
    // https://github.com/tensorflow/tflite-micro/blob/7b49bf8dd673394808938aae9a255323d2573a02/tensorflow/lite/micro/kernels/internal/reference/integer_ops/tanh.h#L95
    private static long EvaluateTanh(long value)
    {
        var interpolation = InterpolateLookupTable(value, 8, 0xFFFF << 8);
        return value >= 0
            ? (interpolation - (1 << 23) + 128) >> 8
            : (-interpolation + (1 << 23) + 127) >> 8;
    }

    // Adapted from:
    // https://github.com/tensorflow/tflite-micro/blob/7b49bf8dd673394808938aae9a255323d2573a02/tensorflow/lite/micro/kernels/internal/reference/integer_ops/logistic.h#L100
    private static long EvaluateSigmoid(long value)
    {
        var interpolation = InterpolateLookupTable(value, 9, 0x7FFF << 10);
        return value >= 0
            ? (interpolation + 512) >> 10
            : ((1 << 25) - interpolation + 511) >> 10;
    }

    private static long InterpolateLookupTable(long value, int fractionBits, long limit)
    {
        var magnitude = Math.Abs(value);
        var index = magnitude >> fractionBits;
        return index >= SigmoidTable.Length - 1 ? limit
            : (SigmoidTable[index] << fractionBits)
                + (magnitude & ((1 << fractionBits) - 1)) * (SigmoidTable[index + 1] - SigmoidTable[index]);
    }

    // Decode the activation type of an xFM precision register.
    private static (bool Signed, int ByteLength) DecodeActivationType(ushort precision, bool isOfm)
    {
        var signed = (precision & 1) != 0;
        var precisionBits = isOfm ? (precision >> 1) & 0b11 : (precision >> 2) & 0b11;
        var byteLength = precisionBits switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => 0,
        };
        return (signed, byteLength);
    }

    private static RoundingMode? DecodeRoundingMode(ushort precision)
    {
        var mode = (precision >> 14) & 0b11;
        return mode switch
        {
            0 => RoundingMode.DoubleSymmetric,
            1 => RoundingMode.TruncateToZero,
            2 => RoundingMode.Natural,
            _ => null,
        };
    }

    private static long Accumulate(long accumulator, long value, int accumulatorBits)
    {
        // Accumulator values wrap at the width selected by NPU_SET_ACC_FORMAT
        // (TRM 4.4, 0x124, and U85 TRM accumulate()).
        return unchecked((long)BitHelper.SignExtend(BitHelper.SignTruncate(accumulator + value, accumulatorBits), accumulatorBits));
    }

    private static (long Minimum, long Maximum) GetActivationRange(int byteLength, bool signed)
    {
        return (signed ? -(1L << (8 * byteLength - 1)) : 0, (1L << (8 * byteLength - (signed ? 1 : 0))) - 1);
    }

    // U85 TRM rescale
    private bool TryRescale(long accumulator, RescaleMode mode, int validCount, out long result, bool saturateToInt32 = false)
    {
        long scale;
        int shift;
        result = 0;

        if(mode == RescaleMode.None)
        {
            result = accumulator;
            return true;
        }

        if(mode == RescaleMode.AveragePool)
        {
            if(validCount < 1 || validCount > 64)
            {
                this.Log(LogLevel.Error, "Invalid average pool count: {0}", validCount);
                return false;
            }

            // U55 uses 31 bits of reciprocal precision, based on Vela pooling scale calculation
            const int reciprocalPrecision = 31;

            var k = 32 - BitOperations.LeadingZeroCount((uint)(validCount - 1));
            var numerator = ((1L << reciprocalPrecision) + 1) << k;
            scale = numerator / validCount;
            shift = reciprocalPrecision + k;
        }
        else if(((commandRegisters.OfmPrecision >> 8) & 1) == 0)
        {
            // TODO: Implement per-channel scale, shift and bias
            this.Log(LogLevel.Error, "Per-channel rescaling is not implemented");
            return false;
        }
        else
        {
            scale = commandRegisters.OfmScale;
            shift = commandRegisters.OfmScaleShift;
        }

        var roundingMode = DecodeRoundingMode(commandRegisters.OfmPrecision);
        if(!roundingMode.HasValue)
        {
            this.ErrorLog("Reserved OFM rounding mode");
            return false;
        }

        try
        {
            // The product must fit int64.
            var product = mode == RescaleMode.ShiftOnly ? accumulator : checked(accumulator * scale);
            result = RoundedShiftRight(product, shift, roundingMode.Value);
            if(saturateToInt32)
            {
                result = Math.Clamp(result, int.MinValue, int.MaxValue);
            }
            return true;
        }
        catch(OverflowException)
        {
            this.ErrorLog("Scaling product exceeds the signed 64-bit range");
            return false;
        }
    }

    // U85 TRM write_accumulator: rescale, then activation stage.
    private bool WriteAccumulator(FeatureMapTiles output, int row, int column, int channel, long accumulator, RescaleMode rescaleMode, int validCount, FusedActivation activation)
    {
        if(!TryRescale(accumulator, rescaleMode, validCount, out var outputValue))
        {
            return false;
        }
        outputValue = ActivationStage(outputValue, activation);
        WriteFeatureMapValue(output, row, column, channel, outputValue);
        return true;
    }

    private ushort GetPrecision(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => commandRegisters.IfmPrecision,
            FeatureMap.Ifm2 => commandRegisters.Ifm2Precision,
            FeatureMap.Ofm => commandRegisters.OfmPrecision,
            _ => throw new UnreachableException(),
        };
    }

    private bool IsSigned(FeatureMap map)
    {
        return (GetPrecision(map) & 1) != 0;
    }

    private int GetElementByteLength(FeatureMap map)
    {
        var (_, byteLength) = DecodeActivationType(GetPrecision(map), isOfm: map == FeatureMap.Ofm);
        return byteLength != 0 ? byteLength : throw new InvalidOperationException($"Invalid {map} precision: 0x{GetPrecision(map):X}");
    }

    private bool IsNHCWB16Layout(FeatureMap map)
    {
        return (GetPrecision(map) & (1 << 6)) != 0;
    }

    private bool HasValidLayout(FeatureMap map)
    {
        // Layout encodings 0 (NHWC) and 1 (NHCWB16) are valid, bit 7 selects a reserved encoding.
        return (GetPrecision(map) & (1 << 7)) == 0;
    }

    private (long X, long Y, long Channel) GetTensorStrides(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => (commandRegisters.IfmStrideX, commandRegisters.IfmStrideY, commandRegisters.IfmStrideC),
            FeatureMap.Ifm2 => (commandRegisters.Ifm2StrideX, commandRegisters.Ifm2StrideY, commandRegisters.Ifm2StrideC),
            FeatureMap.Ofm => (commandRegisters.OfmStrideX, commandRegisters.OfmStrideY, commandRegisters.OfmStrideC),
            _ => throw new UnreachableException(),
        };
    }

    private bool TryGetBaseAddress(FeatureMap map, int tile, out ulong address)
    {
        var (region, offset) = map switch
        {
            FeatureMap.Ifm => (commandRegisters.IfmRegion, tile switch
            {
                0 => commandRegisters.IfmBase0,
                1 => commandRegisters.IfmBase1,
                2 => commandRegisters.IfmBase2,
                _ => commandRegisters.IfmBase3,
            }),
            FeatureMap.Ifm2 => (commandRegisters.Ifm2Region, tile switch
            {
                0 => commandRegisters.Ifm2Base0,
                1 => commandRegisters.Ifm2Base1,
                2 => commandRegisters.Ifm2Base2,
                _ => commandRegisters.Ifm2Base3,
            }),
            FeatureMap.Ofm => (commandRegisters.OfmRegion, tile switch
            {
                0 => commandRegisters.OfmBase0,
                1 => commandRegisters.OfmBase1,
                2 => commandRegisters.OfmBase2,
                _ => commandRegisters.OfmBase3,
            }),
            _ => throw new UnreachableException(),
        };
        return TryGetBufferAddress(region, offset, out address, $"{map} tile {tile}");
    }

    private bool TryGetBufferAddress(int region, ulong offset, out ulong address, string description, ulong length = 1)
    {
        address = 0;
        if(region >= BasePointerCount / 2)
        {
            ParseError($"Invalid {description} region: {region}");
            return false;
        }

        var regionBase = (basePointers[2 * region + 1].Value << 32) | basePointers[2 * region].Value;
        if(regionBase > ulong.MaxValue - offset
            || (length != 0 && length - 1 > ulong.MaxValue - (regionBase + offset)))
        {
            ParseError($"{description} address overflow");
            return false;
        }
        address = regionBase + offset;
        return true;
    }

    private long GetTensorAddress(FeatureMap map, long row, long column, long channel)
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

    private (long Start, long Length) GetFeatureMapBufferRange(FeatureMap map, long width, long height, long depth)
    {
        // Strides are signed (U55 Table 4-113). Include both ends of each axis,
        // including full bricks preceding a partial final NHCWB16 brick.
        var rowOffset = GetTensorAddress(map, height - 1, 0, 0);
        var columnOffset = GetTensorAddress(map, 0, width - 1, 0);
        var channelOffset = GetTensorAddress(map, 0, 0, depth - 1);
        var minimumChannelOffset = Math.Min(0, channelOffset);
        var maximumChannelOffset = Math.Max(0, channelOffset);
        if(IsNHCWB16Layout(map) && depth > 16)
        {
            var lastFullChannel = ((depth - 1) / 16) * 16 - 1;
            foreach(var channel in new[] { 15L, lastFullChannel - 15, lastFullChannel, lastFullChannel + 1 })
            {
                channelOffset = GetTensorAddress(map, 0, 0, channel);
                minimumChannelOffset = Math.Min(minimumChannelOffset, channelOffset);
                maximumChannelOffset = Math.Max(maximumChannelOffset, channelOffset);
            }
        }

        var start = checked(Math.Min(0, rowOffset) + Math.Min(0, columnOffset) + minimumChannelOffset);
        var end = checked(Math.Max(0, rowOffset) + Math.Max(0, columnOffset) + maximumChannelOffset + GetElementByteLength(map));
        return (start, checked(end - start));
    }

    // Read a feature map stripe, which can span up to four memory tiles
    private bool TryReadFeatureMap(FeatureMap map, long width, long height, long depth, out FeatureMapTiles tiles)
    {
        tiles = null;
        var (strideX, strideY, strideC) = GetTensorStrides(map);
        var alignment = IsNHCWB16Layout(map) ? 16 : GetElementByteLength(map);
        if((strideY % alignment) != 0 || ((IsNHCWB16Layout(map) ? strideC : strideX) % alignment) != 0)
        {
            this.ErrorLog("{0} strides are not {1}-byte aligned", map, alignment);
            return false;
        }

        var result = new FeatureMapTiles
        {
            Map = map,
            Width = width,
            Height = height,
            Depth = depth,
            Width0 = GetTensorDimensions(map).Width,
            Height0 = GetTensorDimensions(map).Height,
            Height1 = GetTileHeight1(map),
        };

        if(!IsNHCWB16Layout(map) && (width > result.Width0 || height > result.Height0))
        {
            this.ErrorLog("{0}: NHWC supports only tile 0 (U55 TRM 3.1.1)", map);
            return false;
        }

        var leftWidth = Math.Min(result.Width0, width);
        var rightWidth = Math.Max(0, width - result.Width0);
        var tileWidths = new[] { leftWidth, rightWidth, leftWidth, rightWidth };
        var tileHeights = new[]
        {
            Math.Min(result.Height0, height),
            Math.Min(result.Height1, height),
            Math.Max(0, height - result.Height0),
            Math.Max(0, height - result.Height1),
        };

        for(var tile = 0; tile < 4; tile++)
        {
            if(tileWidths[tile] == 0 || tileHeights[tile] == 0)
            {
                continue;
            }

            long start, length;
            try
            {
                (start, length) = GetFeatureMapBufferRange(map, tileWidths[tile], tileHeights[tile], depth);
            }
            catch(OverflowException)
            {
                this.ErrorLog("{0} tile {1} size overflow", map, tile);
                return false;
            }
            if(length > MaximumFeatureMapBytes)
            {
                this.Log(LogLevel.Error, "{0} tile {1} is too large: {2} bytes", map, tile, length);
                return false;
            }

            if(!TryGetBaseAddress(map, tile, out var address))
            {
                return false;
            }
            if(address % (ulong)alignment != 0)
            {
                this.ErrorLog("{0} tile {1} is not {2}-byte aligned", map, tile, alignment);
                return false;
            }
            if(address < (ulong)-start)
            {
                this.ErrorLog("{0} tile {1} address range underflow", map, tile);
                return false;
            }
            address -= (ulong)-start;
            if((ulong)(length - 1) > ulong.MaxValue - address)
            {
                this.ErrorLog("{0} tile {1} address range overflow", map, tile);
                return false;
            }

            result.Addresses[tile] = address;
            result.BufferOffsets[tile] = start;
            result.Buffers[tile] = sysbus.ReadBytes(address, (int)length);
        }

        tiles = result;
        return true;
    }

    // U85 TRM read_ifm/read_ifm2: read an IFM element already including zero point subtraction.
    private long GetFeatureMapValue(FeatureMapTiles tiles, long row, long column, long channel)
    {
        var offset = GetFeatureMapByteOffset(tiles, row, column, channel, out var tile);
        var value = ReadValue(tiles.Buffers[tile], (int)offset, GetElementByteLength(tiles.Map), IsSigned(tiles.Map));
        return value - GetZeroPoint(tiles.Map);
    }

    // Read an element of a 2x2-upscaled IFM. U85 TRM read_ifm: upscaled coordinates are
    // divided by two and inserted zero pixels read as zero.
    private long GetUpscaledFeatureMapValue(FeatureMapTiles tiles, long row, long column, long channel, IfmUpscaleMode upscale)
    {
        if(upscale == IfmUpscaleMode.InsertZeros && ((row | column) & 1) != 0)
        {
            return 0;
        }
        if(upscale != IfmUpscaleMode.None)
        {
            row >>= 1;
            column >>= 1;
        }
        return GetFeatureMapValue(tiles, row, column, channel);
    }

    private void WriteFeatureMapValue(FeatureMapTiles tiles, long row, long column, long channel, long value)
    {
        var offset = GetFeatureMapByteOffset(tiles, row, column, channel, out var tile);
        WriteValue(tiles.Buffers[tile], offset, value, GetElementByteLength(tiles.Map));
    }

    private long GetFeatureMapByteOffset(FeatureMapTiles tiles, long row, long column, long channel, out int tileNumber)
    {
        var (tile, tileRow, tileColumn) = tiles.GetTileCoordinates(row, column);
        tileNumber = tile;
        if(tiles.Buffers[tile] == null)
        {
            throw new InvalidOperationException($"Access to unused {tiles.Map} tile {tile}");
        }
        return GetTensorAddress(tiles.Map, tileRow, tileColumn, channel) - tiles.BufferOffsets[tile];
    }

    private bool WriteFeatureMap(FeatureMapTiles tiles)
    {
        var elementBytes = GetElementByteLength(tiles.Map);
        var channelsPerWrite = IsNHCWB16Layout(tiles.Map) ? 16 : tiles.Depth;
        for(var row = 0; row < tiles.Height; row++)
        {
            for(var column = 0; column < tiles.Width; column++)
            {
                for(var channel = 0L; channel < tiles.Depth; channel += channelsPerWrite)
                {
                    var offset = GetFeatureMapByteOffset(tiles, row, column, channel, out var tile);
                    var count = Math.Min(channelsPerWrite, tiles.Depth - channel) * elementBytes;
                    sysbus.WriteBytes(tiles.Buffers[tile], tiles.Addresses[tile] + (ulong)offset, (int)offset, count);
                }
            }
        }
        return true;
    }

    // NPU_SET_xFM_HEIGHT1_M1 splits the right column, so tiles 1 and 3.
    private long GetTileHeight1(FeatureMap map)
    {
        return 1 + map switch
        {
            FeatureMap.Ifm => commandRegisters.IfmHeight1M1,
            FeatureMap.Ifm2 => commandRegisters.Ifm2Height1M1,
            FeatureMap.Ofm => commandRegisters.OfmHeight1M1,
            _ => throw new UnreachableException(),
        };
    }

    private long GetZeroPoint(FeatureMap map, bool? signed = null)
    {
        var raw = map switch
        {
            FeatureMap.Ifm => commandRegisters.IfmZeroPoint,
            FeatureMap.Ifm2 => commandRegisters.Ifm2ZeroPoint,
            FeatureMap.Ofm => commandRegisters.OfmZeroPoint,
            _ => throw new UnreachableException(),
        };
        // The zero point is encoded as a signed value for signed activations and as an
        // unsigned value for unsigned activations.
        return (signed ?? IsSigned(map)) ? (short)raw : raw;
    }

    private RoundingMode GetRoundingMode(FeatureMap map)
    {
        return DecodeRoundingMode(GetPrecision(map)) ?? throw new InvalidOperationException("Reserved rounding mode");
    }

    private bool TryGetIfmUpscale(out IfmUpscaleMode upscale)
    {
        var raw = commandRegisters.IfmUpscale;
        if(raw > (ushort)IfmUpscaleMode.InsertZeros)
        {
            this.ErrorLog("Unsupported IFM upscale mode: {0}", raw);
            upscale = IfmUpscaleMode.None;
            return false;
        }

        upscale = (IfmUpscaleMode)raw;
        return true;
    }

    // Validate the buffer and scheduling registers.
    private bool ValidateOperationRegisters(bool elementwise, bool useIfm2 = false)
    {
        var ifmIbEnd = commandRegisters.IfmIbEnd;
        var ifm2IbStart = commandRegisters.Ifm2IbStart;
        var abStart = commandRegisters.AbStart;
        // 128/256-MAC configurations always reserve the last 2KB, even
        // without a LUT (U55 Table 4-144).
        var bufferEnd = sharedBuffer.Length / 1024 - 2;

        if(((ifmIbEnd | (useIfm2 ? ifm2IbStart : 0) | abStart) & 1) != 0)
        {
            this.ErrorLog("Shared buffer boundaries must be multiples of 2KB: IFM_IB_END={0}, IFM2_IB_START={1}, AB_START={2}",
                ifmIbEnd, ifm2IbStart, abStart);
            return false;
        }

        if(ifmIbEnd <= 2 || ifmIbEnd > abStart || abStart > bufferEnd
            || (elementwise && abStart != bufferEnd)
            || (useIfm2 && (ifm2IbStart <= 2 || ifm2IbStart >= ifmIbEnd)))
        {
            this.ErrorLog("Invalid shared buffer layout: IFM2_IB_START={0}, IFM_IB_END={1}, AB_START={2}, SB_SIZE={3}KB",
                ifm2IbStart, ifmIbEnd, abStart, sharedBuffer.Length / 1024);
            return false;
        }

        // U55 is a single core NPU, so no parallel mode other than 0 exists.
        if(commandRegisters.ParallelMode != 0)
        {
            this.ErrorLog("U55 is a single-core NPU, unsupported parallel mode: {0}", commandRegisters.ParallelMode);
            return false;
        }

        if(commandRegisters.Blockdep > 3)
        {
            this.ErrorLog("Invalid block dependency offset: {0}", commandRegisters.Blockdep);
            return false;
        }

        return true;
    }

    private bool TryGetAccumulatorBits(out int accumulatorBits)
    {
        accumulatorBits = 32;
        switch(commandRegisters.AccFormat)
        {
        case 0:
            return true;
        case 1:
            accumulatorBits = 40;
            return true;
        case 2:
            this.ErrorLog("Floating point accumulator format is not supported");
            return false;
        default:
            this.ErrorLog("Reserved accumulator format: {0}", commandRegisters.AccFormat);
            return false;
        }
    }

    private bool ValidateElementwisePrecisions(ElementwiseMode mode, bool isUnary)
    {
        var (ifmSigned, ifmByteLength) = DecodeActivationType(commandRegisters.IfmPrecision, isOfm: false);
        var (ofmSigned, ofmByteLength) = DecodeActivationType(commandRegisters.OfmPrecision, isOfm: true);
        if(ifmByteLength == 0 || ofmByteLength == 0
            || (!ifmSigned && ifmByteLength != 1) || (!ofmSigned && ofmByteLength != 1))
        {
            this.ErrorLog("Invalid elementwise precision: IFM=0x{0:X4}, OFM=0x{1:X4}",
                commandRegisters.IfmPrecision, commandRegisters.OfmPrecision);
            return false;
        }

        if(!isUnary)
        {
            var (ifm2Signed, ifm2ByteLength) = DecodeActivationType(commandRegisters.Ifm2Precision, isOfm: false);
            if(ifm2ByteLength == 0 || ifm2Signed != ifmSigned || ifm2ByteLength != ifmByteLength)
            {
                this.ErrorLog("IFM2 precision 0x{0:X4} must match IFM precision 0x{1:X4}",
                    commandRegisters.Ifm2Precision, commandRegisters.IfmPrecision);
                return false;
            }
        }

        if(!HasValidLayout(FeatureMap.Ifm)
            || !HasValidLayout(FeatureMap.Ofm)
            || (!isUnary && !HasValidLayout(FeatureMap.Ifm2))
            || !DecodeRoundingMode(commandRegisters.OfmPrecision).HasValue)
        {
            this.ErrorLog("Reserved elementwise layout or rounding mode");
            return false;
        }

        switch(mode)
        {
        // Table 4-126: Min, Max, LReLU and ABS require the IFM and OFM to be
        // the same 8 or 16-bit type.
        case ElementwiseMode.Min:
        case ElementwiseMode.Max:
        case ElementwiseMode.LeakyRelu:
        case ElementwiseMode.Abs:
            if(ifmByteLength > 2 || ifmSigned != ofmSigned || ifmByteLength != ofmByteLength)
            {
                this.ErrorLog("{0} requires matching 8 or 16-bit IFM and OFM types", mode);
                return false;
            }
            break;
        // Table 4-126: CLZ and SHL are int32 to int32 only.
        case ElementwiseMode.Clz:
        case ElementwiseMode.Shl:
            if(!ifmSigned || ifmByteLength != 4 || !ofmSigned || ofmByteLength != 4)
            {
                this.ErrorLog("{0} requires an int32 IFM and an int32 OFM", mode);
                return false;
            }
            break;
        // Table 4-126: SHR is int32 to int8, uint8 or int32.
        case ElementwiseMode.Shr:
            if(!ifmSigned || ifmByteLength != 4
                || (ofmByteLength != 1 && (ofmByteLength != 4 || !ofmSigned)))
            {
                this.ErrorLog("SHR requires an int32 IFM and an int8, uint8 or int32 OFM");
                return false;
            }
            break;
        }

        if((commandRegisters.OfmPrecision & 0x100) == 0
            && mode != ElementwiseMode.Min && mode != ElementwiseMode.Max
            && mode != ElementwiseMode.Clz && mode != ElementwiseMode.Shr && mode != ElementwiseMode.Shl)
        {
            this.ErrorLog("Elementwise operations require global output scaling");
            return false;
        }

        return true;
    }

    private bool ValidateZeroPoint(FeatureMap map, int byteLength, bool signed)
    {
        var value = GetZeroPoint(map, signed);
        var (minimum, maximum) = GetActivationRange(byteLength, signed);
        // U55 Table 4-112: 32-bit inputs and activation ranges require zero.
        if(value < minimum || value > maximum || (byteLength == 4 && value != 0))
        {
            this.ErrorLog("Invalid {0} zero point: {1}", map, value);
            return false;
        }
        return true;
    }

    private bool ExecuteElementwise(ushort parameter)
    {
        // U85 TRM NPU_OP_ELEMENTWISE
        if(parameter > (ushort)ElementwiseMode.Shl)
        {
            this.Log(LogLevel.Error, "Unsupported elementwise mode: {0}", parameter);
            return false;
        }

        var mode = (ElementwiseMode)parameter;
        var isUnary = mode == ElementwiseMode.Abs || mode == ElementwiseMode.LeakyRelu || mode == ElementwiseMode.Clz;
        var broadcast = commandRegisters.Ifm2Broadcast;
        var useScalar = (broadcast & 0x80) != 0;
        var reversedOperands = (broadcast & 0x40) != 0;
        var useIfm2 = !(isUnary || useScalar);

        if(!TryGetIfmUpscale(out var upscale))
        {
            return false;
        }
        if(upscale != IfmUpscaleMode.None)
        {
            this.ErrorLog("Elementwise operations do not support IFM upscale");
            return false;
        }
        if(!ValidateOperationRegisters(elementwise: true, useIfm2: useIfm2) || !TryGetAccumulatorBits(out _))
        {
            return false;
        }
        if(!ValidateElementwisePrecisions(mode, isUnary))
        {
            return false;
        }
        // Table 4-126: a scalar constant is not supported for a 32-bit IFM.
        if(!isUnary && useScalar && GetElementByteLength(FeatureMap.Ifm2) == 4)
        {
            this.ErrorLog("IFM2 scalar broadcast is not supported for a 32-bit IFM");
            return false;
        }
        if(!TryPrepareActivation(out var activation))
        {
            return false;
        }
        var inputByteLength = GetElementByteLength(FeatureMap.Ifm);
        if(!ValidateZeroPoint(FeatureMap.Ifm, inputByteLength, IsSigned(FeatureMap.Ifm))
            || (!isUnary && !ValidateZeroPoint(FeatureMap.Ifm2, inputByteLength, IsSigned(FeatureMap.Ifm2)))
            || !ValidateZeroPoint(FeatureMap.Ofm, activation.InputByteLength, activation.InputSigned))
        {
            return false;
        }
        var scalar = 0L;
        if(!isUnary && useScalar)
        {
            scalar = IsSigned(FeatureMap.Ifm2) ? (long)(short)commandRegisters.Ifm2Scalar : commandRegisters.Ifm2Scalar;
            var (minimum, maximum) = GetActivationRange(inputByteLength, IsSigned(FeatureMap.Ifm2));
            if(scalar < minimum || scalar > maximum)
            {
                this.ErrorLog("Invalid IFM2 scalar: {0}", scalar);
                return false;
            }
            // NPU_SET_IFM2_SCALAR is encoded with IFM2_ZERO_POINT (U55 Table 4-112).
            scalar -= GetZeroPoint(FeatureMap.Ifm2);
        }
        // Scaling depends on the activation precision before a widening LUT.
        var is32Bit = inputByteLength == 4 || activation.InputByteLength == 4;
        if((mode == ElementwiseMode.Add || mode == ElementwiseMode.Sub) && !is32Bit
            && (((commandRegisters.IfmPrecision >> 8) & 3) == 3 || ((commandRegisters.IfmPrecision >> 14) & 1) != 0))
        {
            this.ErrorLog("Reserved ADD/SUB input scaling or rounding mode");
            return false;
        }

        // Elementwise operations map input elements 1:1 onto output
        // elements, so the IFM stripe matches the OFM stripe.
        var ofmDimensions = GetOfmStripeDimensions();
        var ifmDimensions = GetTensorDimensions(FeatureMap.Ifm);
        if(ofmDimensions != ifmDimensions)
        {
            this.ErrorLog($"IFM dimensions ({ifmDimensions}) don't match OFM dimensions ({ofmDimensions})");
        }

        var (outputWidth, outputHeight, outputDepth) = ofmDimensions;
        if(!TryReadFeatureMap(FeatureMap.Ifm, outputWidth, outputHeight, outputDepth, out var input))
        {
            return false;
        }

        FeatureMapTiles input2 = null;
        if(useIfm2)
        {
            var width2 = (broadcast & 0b010) != 0 ? 1 : outputWidth;
            var height2 = (broadcast & 0b001) != 0 ? 1 : outputHeight;
            var depth2 = (broadcast & 0b100) != 0 ? 1 : outputDepth;
            if(!TryReadFeatureMap(FeatureMap.Ifm2, width2, height2, depth2, out input2))
            {
                return false;
            }
        }

        // Preserve memory between strided output elements and in channel padding.
        if(!TryReadFeatureMap(FeatureMap.Ofm, outputWidth, outputHeight, outputDepth, out var output))
        {
            return false;
        }
        this.DebugLog(
            "Elementwise {0}: input={1}x{2}x{3}, output={4}x{5}x{6}, precision={7}/{8}, broadcast=0x{9:X2}, scalar={10}, activation=0x{11:X}",
            mode, outputWidth, outputHeight, outputDepth,
            outputWidth, outputHeight, outputDepth,
            commandRegisters.IfmPrecision, commandRegisters.OfmPrecision,
            broadcast, useScalar, commandRegisters.Activation);

        for(var oY = 0; oY < outputHeight; oY++)
        {
            for(var oX = 0; oX < outputWidth; oX++)
            {
                for(var oD = 0; oD < outputDepth; oD++)
                {
                    var operandA = GetFeatureMapValue(input, oY, oX, oD);
                    var operandB = 0L;
                    if(!isUnary)
                    {
                        if(useScalar)
                        {
                            operandB = scalar;
                        }
                        else
                        {
                            var row2 = (broadcast & 0b001) != 0 ? 0 : oY;
                            var column2 = (broadcast & 0b010) != 0 ? 0 : oX;
                            var channel2 = (broadcast & 0b100) != 0 ? 0 : oD;
                            operandB = GetFeatureMapValue(input2, row2, column2, channel2);
                        }
                        if(reversedOperands)
                        {
                            (operandA, operandB) = (operandB, operandA);
                        }
                    }

                    var rescaleMode = is32Bit ? RescaleMode.ShiftOnly : RescaleMode.Normal;
                    long result;
                    switch(mode)
                    {
                    case ElementwiseMode.Mul:
                        result = operandA * operandB;
                        if(inputByteLength == 4 && activation.InputByteLength == 4 && commandRegisters.OfmScaleShift == 0)
                        {
                            // TOSA MUL with shift=0 retains the low 32 bits of the product.
                            result = unchecked((int)result);
                            rescaleMode = RescaleMode.None;
                        }
                        break;
                    case ElementwiseMode.Add:
                        result = is32Bit ? operandA + operandB : ScaleAddSubOperands(operandA, operandB, 1);
                        break;
                    case ElementwiseMode.Sub:
                        result = is32Bit ? operandA - operandB : ScaleAddSubOperands(operandA, operandB, -1);
                        break;
                    case ElementwiseMode.Min:
                        result = Math.Min(operandA, operandB);
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Max:
                        result = Math.Max(operandA, operandB);
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.LeakyRelu:
                        result = operandA;
                        if(result >= 0)
                        {
                            rescaleMode = RescaleMode.None;
                        }
                        break;
                    case ElementwiseMode.Abs:
                        result = Math.Abs(operandA);
                        break;
                    case ElementwiseMode.Clz:
                        result = BitOperations.LeadingZeroCount(unchecked((uint)operandA));
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Shr:
                        // U55 permits only int32 inputs here, so scale_a/scale_b are identities.
                        result = RoundedShiftRight(operandA, (int)(operandB & 31), GetRoundingMode(FeatureMap.Ofm));
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Shl:
                        // Apply the 32-bit activation mask after shifting (U85 TRM NPU_OP_ELEMENTWISE).
                        result = unchecked((int)operandA << (int)(operandB & 31));
                        rescaleMode = RescaleMode.None;
                        break;
                    default:
                        throw new UnreachableException();
                    }
                    if(!TryRescale(result, rescaleMode, 0, out var outputValue, saturateToInt32: true))
                    {
                        return false;
                    }

                    outputValue = ActivationStage(outputValue, activation);
                    WriteFeatureMapValue(output, oY, oX, oD, outputValue);
                }
            }
        }

        return WriteFeatureMap(output);
    }

    // Decode and validate the NPU_SET_ACTIVATION register (TRM 4.4, 0x125) for the
    // output unit and resolve the lookup table location for LUT activations.
    private bool TryPrepareActivation(out FusedActivation activation)
    {
        activation = default;
        var register = commandRegisters.Activation;
        var kind = register & 0xF;
        var clipRange = (register >> 12) & 0xF;

        if((register & 0xFE0) != 0)
        {
            this.ErrorLog("Invalid activation encoding: 0x{0:X}", register);
            return false;
        }

        var function = ActivationKind.None;
        var lutIndex = 0;
        if((register & 0x10) != 0)
        {
            // 0x10+n selects a lookup table at address n*256 bytes in the last 2KB page
            // of the shared buffer (n is stored in bits [3:0]).
            if(kind > 7)
            {
                this.ErrorLog("Invalid lookup table index: {0}", kind);
                return false;
            }
            function = ActivationKind.Lut;
            lutIndex = kind;
        }
        else
        {
            function = kind switch
            {
                0 => ActivationKind.None,
                3 => ActivationKind.Tanh,
                4 => ActivationKind.Sigmoid,
                _ => ActivationKind.Reserved,
            };
            if(function == ActivationKind.Reserved)
            {
                this.ErrorLog("Unsupported activation function: {0}", kind);
                return false;
            }
        }

        var outputByteLength = GetElementByteLength(FeatureMap.Ofm);
        var outputSigned = IsSigned(FeatureMap.Ofm);

        // The hardwired tanh/sigmoid table consumes 16-bit activations in the 1/(3*4096)
        // format; Vela expresses 8-bit sigmoid/tanh through lookup tables instead.
        if((function == ActivationKind.Tanh || function == ActivationKind.Sigmoid) && outputByteLength != 2)
        {
            this.ErrorLog("Tanh/sigmoid activation requires a 16-bit output precision");
            return false;
        }

        // U55 Table 4-112: a forced activation range is only valid for LUTs.
        if(function != ActivationKind.Lut && clipRange != 0)
        {
            this.ErrorLog("Activation clip range requires a lookup table");
            return false;
        }

        if(clipRange != 0 && clipRange != 2 && clipRange != 3 && clipRange != 5)
        {
            this.ErrorLog("Invalid activation clip range: {0}", clipRange);
            return false;
        }
        var (rangeSigned, rangeByteLength) = clipRange switch
        {
            0 => (outputSigned, outputByteLength),
            2 => (false, 1),
            3 => (true, 1),
            _ => (true, 2),
        };

        activation = new FusedActivation
        {
            Function = function,
            InputByteLength = rangeByteLength,
            InputSigned = rangeSigned,
            OutputZeroPoint = GetZeroPoint(FeatureMap.Ofm, rangeSigned),
            ClipMinimum = rangeSigned ? (long)(short)commandRegisters.ActivationMin : commandRegisters.ActivationMin,
            ClipMaximum = rangeSigned ? (long)(short)commandRegisters.ActivationMax : commandRegisters.ActivationMax,
        };
        (activation.OutputMinimum, activation.OutputMaximum) = GetActivationRange(outputByteLength, outputSigned);
        if((function == ActivationKind.Lut || rangeByteLength < 4) && activation.ClipMinimum > activation.ClipMaximum)
        {
            this.ErrorLog("Invalid activation clip bounds: {0} > {1}", activation.ClipMinimum, activation.ClipMaximum);
            return false;
        }
        if(function != ActivationKind.Lut)
        {
            return true;
        }

        // The lookup index is 8 or 16-bit wide, so a 32-bit activation range
        // cannot address the table.
        if(rangeByteLength == 4)
        {
            this.ErrorLog("Lookup table with a 32-bit output requires an 8 or 16-bit activation clip range");
            return false;
        }

        var eightBitIndex = rangeByteLength == 1;
        if(!eightBitIndex && outputByteLength == 1)
        {
            this.ErrorLog("Lookup table with a 16-bit index requires a 16 or 32-bit output");
            return false;
        }
        activation.EntryByteLength = eightBitIndex ? outputByteLength : 4;
        activation.EntryCount = eightBitIndex ? 256 : 512;
        activation.SignedEntries = eightBitIndex ? outputSigned : true;
        activation.PreserveInterpolation = !eightBitIndex && outputByteLength == 4;
        if(!eightBitIndex && !activation.InputSigned)
        {
            this.ErrorLog("Lookup table requires a signed 16-bit index");
            return false;
        }

        var tableSpan = activation.EntryCount * activation.EntryByteLength;
        var tableOffset = (int)(SharedBufferSizeInKilobytes * 1024 - LutAreaBytes + lutIndex * LutSlotBytes);
        if(tableOffset + tableSpan > sharedBuffer.Length)
        {
            this.ErrorLog("Lookup table {0} exceeds the shared buffer lookup area", lutIndex);
            return false;
        }
        activation.TableOffset = tableOffset;

        (activation.LookupMinimum, activation.LookupMaximum) = GetActivationRange(rangeByteLength, rangeSigned);
        return true;
    }

    private long ApplyActivation(FusedActivation activation, long value)
    {
        switch(activation.Function)
        {
        case ActivationKind.Tanh:
            return EvaluateTanh(value);
        case ActivationKind.Sigmoid:
            return EvaluateSigmoid(value);
        case ActivationKind.Lut:
            return LookupActivationTable(activation, value);
        default:
            return value;
        }
    }

    // U85 TRM activation()
    private long ActivationStage(long value, FusedActivation activation)
    {
        value += activation.OutputZeroPoint;

        if(activation.Function == ActivationKind.Lut)
        {
            // Bounds apply to lookup input.
            value = Math.Clamp(value, activation.ClipMinimum, activation.ClipMaximum);
        }

        value = ApplyActivation(activation, value);

        if(activation.Function != ActivationKind.Lut && activation.InputByteLength < 4)
        {
            value = Math.Clamp(value, activation.ClipMinimum, activation.ClipMaximum);
        }

        return Math.Clamp(value, activation.OutputMinimum, activation.OutputMaximum);
    }

    private long LookupActivationTable(FusedActivation activation, long index)
    {
        index = Math.Clamp(index, activation.LookupMinimum, activation.LookupMaximum);

        if(activation.EntryCount == 512)
        {
            var entry = ReadValue(sharedBuffer, activation.TableOffset + (int)((index - short.MinValue) >> 7) * 4, 4, signed: false);
            var tableBase = (short)entry;
            var tableSlope = (short)(entry >> 16);
            var fraction = index & 0x7F;
            var interpolation = tableBase * 128L + tableSlope * fraction;
            // TOSA TABLE (int16 -> int32) retains all seven fractional bits.
            // The int16 output mode rounds and truncates the interpolation (U85 TRM 3.6.7).
            return activation.PreserveInterpolation ? interpolation : unchecked((short)((interpolation + 64) >> 7));
        }

        var entryOffset = activation.TableOffset + (int)(index - (activation.InputSigned ? -128 : 0)) * activation.EntryByteLength;
        return ReadValue(sharedBuffer, entryOffset, activation.EntryByteLength, activation.SignedEntries);
    }

    private long ScaleAddSubOperands(long operandA, long operandB, int sign)
    {
        // Adapted from Vela's advanced_elementwise_add_sub_scale
        var scaleMode = (commandRegisters.IfmPrecision >> 8) & 0b11;
        var inputRoundingMode = GetRoundingMode(FeatureMap.Ifm);
        var inputFixedShift = GetElementByteLength(FeatureMap.Ifm) == 1 ? 19 : 14;

        switch(scaleMode)
        {
        case 0: // 16-bit OPA/OPB scales, the shift is ignored.
            return checked(operandA * commandRegisters.OpaScale + sign * operandB * commandRegisters.OpbScale);
        case 1: // 32-bit OPA scale applied to operand A, fixed shift for operand B.
            operandA = Math.Clamp(RoundedShiftRight(checked(operandA * commandRegisters.OpaScale), commandRegisters.OpaScaleShift, inputRoundingMode), int.MinValue, int.MaxValue);
            operandB <<= inputFixedShift;
            break;
        case 2: // 32-bit OPA scale applied to operand B, fixed shift for operand A.
            operandA <<= inputFixedShift;
            operandB = Math.Clamp(RoundedShiftRight(checked(operandB * commandRegisters.OpaScale), commandRegisters.OpaScaleShift, inputRoundingMode), int.MinValue, int.MaxValue);
            break;
        default:
            throw new InvalidOperationException($"Invalid IFM scale mode: {scaleMode}");
        }
        return checked(operandA + sign * operandB);
    }

    // Tile 0, 2 (left-hand tiles) dimensions and depth.
    private (int Width, int Height, int Depth) GetTensorDimensions(FeatureMap map)
    {
        return map switch
        {
            FeatureMap.Ifm => (commandRegisters.IfmWidth0M1 + 1, commandRegisters.IfmHeight0M1 + 1, commandRegisters.IfmDepthM1 + 1),
            FeatureMap.Ifm2 => (commandRegisters.Ifm2Width0M1 + 1, commandRegisters.Ifm2Height0M1 + 1, commandRegisters.IfmDepthM1 + 1),
            FeatureMap.Ofm => (commandRegisters.OfmWidth0M1 + 1, commandRegisters.OfmHeight0M1 + 1, commandRegisters.OfmDepthM1 + 1),
            _ => throw new UnreachableException(),
        };
    }

    // Whole stripe dimensions.
    private (int Width, int Height, int Depth) GetOfmStripeDimensions()
    {
        return (commandRegisters.OfmWidthM1 + 1, commandRegisters.OfmHeightM1 + 1, commandRegisters.OfmDepthM1 + 1);
    }

    private (int X, int Y) GetKernelStrides()
    {
        var kernelStrideXBottomBits = BitHelper.GetValue(commandRegisters.KernelStride, 0, 1);
        var kernelStrideYBottomBits = BitHelper.GetValue(commandRegisters.KernelStride, 1, 1);
        var kernelStrideXTopBits = BitHelper.GetValue(commandRegisters.KernelStride, 6, 2);
        var kernelStrideYTopBits = BitHelper.GetValue(commandRegisters.KernelStride, 9, 2);
        var kernelStrideX = (kernelStrideXTopBits << 1 | kernelStrideXBottomBits) + 1;
        var kernelStrideY = (kernelStrideYTopBits << 1 | kernelStrideYBottomBits) + 1;
        return (kernelStrideX, kernelStrideY);
    }

    private bool ExecutePool(ushort parameter)
    {
        PoolMode? mode = parameter switch
        {
            0 => PoolMode.Max,
            1 => PoolMode.Average,
            2 => PoolMode.ReduceSum,
            _ => null,
        };

        if(!mode.HasValue)
        {
            this.Log(LogLevel.Error, "Invalid OP_POOL mode: {0}", parameter);
            return false;
        }

        if(!TryPrepareActivation(out var activation))
        {
            this.ErrorLog("Pooling activation {0} is invalid", commandRegisters.Activation);
            return false;
        }

        if(!TryGetIfmUpscale(out var upscale))
        {
            return false;
        }
        if(!ValidateOperationRegisters(elementwise: false) || !TryGetAccumulatorBits(out var accumulatorBits))
        {
            return false;
        }

        var kernelXDilation = BitHelper.GetBits(commandRegisters.KernelStride)[3] ? 2 : 1;
        var kernelYDilation = BitHelper.GetBits(commandRegisters.KernelStride)[4] ? 2 : 1;
        var kernelWidth = (commandRegisters.KernelWidthM1 / kernelXDilation) + 1;
        var kernelHeight = (commandRegisters.KernelHeightM1 / kernelYDilation) + 1;
        var (kernelStrideX, kernelStrideY) = GetKernelStrides();

        if(mode == PoolMode.ReduceSum)
        {
            // Table 4-127: channel reduction supports 1x1 upscaling only.
            if(upscale != IfmUpscaleMode.None)
            {
                this.ErrorLog("Channel reduction does not support IFM upscale");
                return false;
            }
        }
        else
        {
            // Table 4-124: max pooling only supports nearest-neighbor upscale.
            if(upscale == IfmUpscaleMode.InsertZeros && mode == PoolMode.Max)
            {
                this.ErrorLog("Max pooling only supports nearest-neighbor IFM upscale");
                return false;
            }
            // Padding is applied after upscale, so a 2x2 upscale must use a stride of 1x1.
            if(upscale != IfmUpscaleMode.None && (kernelStrideX != 1 || kernelStrideY != 1))
            {
                this.ErrorLog("A 2x2 IFM upscale requires a stride of 1x1");
                return false;
            }
        }

        var (inputWidth, inputHeight, inputDepth) = GetTensorDimensions(FeatureMap.Ifm);
        var (outputWidth, outputHeight, outputDepth) = GetOfmStripeDimensions();

        // An upscaled IFM is read in its 2x coordinates and mapped back to the
        // stored elements, so bounds checks use the upscaled dimensions.
        var upscaleFactor = upscale == IfmUpscaleMode.None ? 1 : 2;
        var upscaledWidth = inputWidth * upscaleFactor;
        var upscaledHeight = inputHeight * upscaleFactor;

        if(!TryReadFeatureMap(FeatureMap.Ifm, inputWidth, inputHeight, inputDepth, out var input)
            || !TryReadFeatureMap(FeatureMap.Ofm, outputWidth, outputHeight, outputDepth, out var outputBuffer))
        {
            return false;
        }

        var topPad = BitHelper.GetValue(commandRegisters.IfmPadTop, 0, 15);
        var leftPad = BitHelper.GetValue(commandRegisters.IfmPadLeft, 0, 15);
        var bottomPad = BitHelper.GetValue(commandRegisters.IfmPadBottom, 0, 16);
        var rightPad = BitHelper.GetValue(commandRegisters.IfmPadRight, 0, 16);
        var hasPadding = topPad != 0 || leftPad != 0 || bottomPad != 0 || rightPad != 0;

        // TODO: Validate all configuration options
        // Table 4-124: a 40-bit accumulator is only available for average
        // pooling without padding; Table 4-127 allows it for reduction.
        if(accumulatorBits == 40 && (mode == PoolMode.Max || (mode == PoolMode.Average && hasPadding)))
        {
            this.ErrorLog("A 40-bit accumulator is not supported for {0} pooling", mode);
            return false;
        }

        this.Log(LogLevel.Debug,
            "POOL configuration: kernel={0}x{1}, stride={2}x{3}, dilation={4}x{5}, output={6}x{7}x{8}, pad={9},{10},{11},{12}, mode={13}",
            kernelWidth, kernelHeight, kernelStrideX, kernelStrideY, kernelXDilation, kernelYDilation,
            outputWidth, outputHeight, outputDepth, topPad, leftPad, bottomPad, rightPad, mode);

        for(var oY = 0; oY < outputHeight; oY++)
        {
            for(var oX = 0; oX < outputWidth; oX++)
            {
                for(var oD = 0; oD < outputDepth; oD++)
                {
                    var identity = mode == PoolMode.Max ? int.MinValue : 0;
                    long accumulator = identity;
                    var validCount = 0;

                    if(mode == PoolMode.ReduceSum)
                    {
                        for(var iC = 0; iC < inputDepth; iC++)
                        {
                            var value = GetFeatureMapValue(input, oY, oX, iC);
                            accumulator = Accumulate(accumulator, value, accumulatorBits);
                        }
                    }
                    else
                    {
                        for(var kY = 0; kY < kernelHeight; kY++)
                        {
                            for(var kX = 0; kX < kernelWidth; kX++)
                            {
                                var iX = -leftPad + oX * kernelStrideX + kX * kernelXDilation;
                                var iY = -topPad + oY * kernelStrideY + kY * kernelYDilation;
                                var iC = oD;
                                var pad = iX < 0 || iY < 0 || iX >= upscaledWidth || iY >= upscaledHeight;

                                // Don't include padding in calculations
                                if(pad)
                                {
                                    continue;
                                }
                                validCount++;

                                var value = GetUpscaledFeatureMapValue(input, iY, iX, iC, upscale);
                                if(mode == PoolMode.Max)
                                {
                                    accumulator = Math.Max(accumulator, value);
                                }
                                else
                                {
                                    accumulator = Accumulate(accumulator, value, accumulatorBits);
                                }
                            }
                        }
                    }

                    var rescaleMode = mode switch
                    {
                        PoolMode.Max => RescaleMode.None,
                        PoolMode.Average => hasPadding ? RescaleMode.AveragePool : RescaleMode.Normal,
                        PoolMode.ReduceSum => RescaleMode.ShiftOnly,
                        _ => throw new UnreachableException()
                    };

                    if(!WriteAccumulator(outputBuffer, oY, oX, oD, accumulator, rescaleMode, validCount, activation))
                    {
                        return false;
                    }
                }
            }
        }

        return WriteFeatureMap(outputBuffer);
    }

    private const long MaximumFeatureMapBytes = 0x1000000;

    // The activation lookup tables occupy the last 2KB page of the shared buffer,
    // split into 8 slots of 256 bytes.
    private const int LutAreaBytes = 2048;
    private const int LutSlotBytes = 256;

    // Tile 0/1/2/3 are the top-left, top-right, bottom-left and bottom-right tiles.
    private class FeatureMapTiles
    {
        public (int tile, long row, long column) GetTileCoordinates(long row, long column)
        {
            // U85 TRM element_address_in_region tile2x2.
            // TODO: U85 supports also 3x1 tiles.
            var tile = 0;
            var tileHeight = Height0;
            if(column >= Width0)
            {
                column -= Width0;
                tile = 1;
                tileHeight = Height1;
            }
            if(row >= tileHeight)
            {
                row -= tileHeight;
                tile += 2;
            }

            return (tile, row, column);
        }

        public FeatureMap Map;
        public long Width;
        public long Height;
        public long Depth;
        public long Width0;
        public long Height0;
        public long Height1;
        public readonly ulong[] Addresses = new ulong[4];
        public readonly long[] BufferOffsets = new long[4];
        public readonly byte[][] Buffers = new byte[4][];
    }

    private struct FusedActivation
    {
        public ActivationKind Function;
        public int InputByteLength;
        public bool InputSigned;
        // OFM zero point and ACTIVATION_MIN/MAX clip bounds.
        public long OutputZeroPoint;
        public long ClipMinimum;
        public long ClipMaximum;
        // Final OFM precision bounds, applied after the activation function.
        public long OutputMinimum;
        public long OutputMaximum;
        // Lookup index clipping applied before the table lookup (ACTIVATION[15:12]).
        public long LookupMinimum;
        public long LookupMaximum;
        // Lookup table geometry within the shared buffer lookup area.
        public int TableOffset;
        public int EntryByteLength;
        public int EntryCount;
        public bool SignedEntries;
        public bool PreserveInterpolation;
    }

    // Table of sigmoid(i/24) at 0.16 format (Q16), shared by sigmoid and tanh
    // hardwired activations of the output unit.
    private static readonly ushort[] SigmoidTable =
    {
        32768, 33451, 34133, 34813, 35493, 36169, 36843, 37513,
        38180, 38841, 39498, 40149, 40794, 41432, 42064, 42688,
        43304, 43912, 44511, 45102, 45683, 46255, 46817, 47369,
        47911, 48443, 48964, 49475, 49975, 50464, 50942, 51409,
        51865, 52311, 52745, 53169, 53581, 53983, 54374, 54755,
        55125, 55485, 55834, 56174, 56503, 56823, 57133, 57433,
        57724, 58007, 58280, 58544, 58800, 59048, 59288, 59519,
        59743, 59959, 60168, 60370, 60565, 60753, 60935, 61110,
        61279, 61441, 61599, 61750, 61896, 62036, 62172, 62302,
        62428, 62549, 62666, 62778, 62886, 62990, 63090, 63186,
        63279, 63368, 63454, 63536, 63615, 63691, 63765, 63835,
        63903, 63968, 64030, 64090, 64148, 64204, 64257, 64308,
        64357, 64405, 64450, 64494, 64536, 64576, 64614, 64652,
        64687, 64721, 64754, 64786, 64816, 64845, 64873, 64900,
        64926, 64950, 64974, 64997, 65019, 65039, 65060, 65079,
        65097, 65115, 65132, 65149, 65164, 65179, 65194, 65208,
        65221, 65234, 65246, 65258, 65269, 65280, 65291, 65301,
        65310, 65319, 65328, 65337, 65345, 65352, 65360, 65367,
        65374, 65381, 65387, 65393, 65399, 65404, 65410, 65415,
        65420, 65425, 65429, 65433, 65438, 65442, 65445, 65449,
        65453, 65456, 65459, 65462, 65465, 65468, 65471, 65474,
        65476, 65479, 65481, 65483, 65485, 65488, 65489, 65491,
        65493, 65495, 65497, 65498, 65500, 65501, 65503, 65504,
        65505, 65507, 65508, 65509, 65510, 65511, 65512, 65513,
        65514, 65515, 65516, 65517, 65517, 65518, 65519, 65520,
        65520, 65521, 65522, 65522, 65523, 65523, 65524, 65524,
        65525, 65525, 65526, 65526, 65526, 65527, 65527, 65528,
        65528, 65528, 65529, 65529, 65529, 65529, 65530, 65530,
        65530, 65530, 65531, 65531, 65531, 65531, 65531, 65532,
        65532, 65532, 65532, 65532, 65532, 65533, 65533, 65533,
        65533, 65533, 65533, 65533, 65533, 65534, 65534, 65534,
        65534, 65534, 65534, 65534, 65534, 65534, 65534, 65535,
    };

    private enum FeatureMap
    {
        Ifm,
        Ifm2,
        Ofm,
    }

    private enum IfmUpscaleMode
    {
        None = 0,
        Nearest = 1,
        InsertZeros = 2,
    }

    // NPU_OP_POOL modes (U55 Table 4-112).
    private enum PoolMode
    {
        Max,
        Average,
        ReduceSum,
    };

    // NPU_OP_ELEMENTWISE modes (U55 Table 4-112).
    private enum ElementwiseMode
    {
        Mul = 0,
        Add = 1,
        Sub = 2,
        Min = 3,
        Max = 4,
        LeakyRelu = 5, // LReLU
        Abs = 6,
        Clz = 7,
        Shr = 8,
        Shl = 9,
    }

    private enum ActivationKind
    {
        None,
        Tanh,
        Sigmoid,
        Lut,
        Reserved,
    }

    private enum RoundingMode
    {
        DoubleSymmetric,
        DoubleAsymmetric,
        TruncateToZero,
        TruncateToLower,
        Natural,
        Symmetric,
    }

    private enum RescaleMode
    {
        None,
        Normal,
        AveragePool,
        ShiftOnly,
    }
}
