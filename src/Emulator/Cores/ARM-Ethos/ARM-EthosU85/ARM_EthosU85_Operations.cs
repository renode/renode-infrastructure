//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;

using Antmicro.Renode.Logging;

using static Antmicro.Renode.Peripherals.ARM_Ethos.ARM_EthosCommon;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
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

    private bool IsNHCWB16Layout(FeatureMap map)
    {
        return (GetPrecision(map) & (1 << 6)) != 0;
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

    private long GetZeroPoint(FeatureMap map, bool? signed = null)
    {
        var raw = map switch
        {
            FeatureMap.Ifm => commandRegisters.IfmZeroPoint,
            FeatureMap.Ifm2 => commandRegisters.Ifm2ZeroPoint,
            FeatureMap.Ofm => commandRegisters.OfmZeroPoint,
            _ => throw new UnreachableException(),
        };
        // U85 encodes signed zero points as int16 and unsigned ones as uint16.
        return (signed ?? IsSigned(map)) ? unchecked((short)raw) : raw;
    }

    private bool ValidateZeroPoint(FeatureMap map, int byteLength, bool signed)
    {
        var value = GetZeroPoint(map, signed);
        if(signed)
        {
            if((byteLength == 1 && (value < -128 || value > 127))
                || (byteLength != 1 && value != 0))
            {
                this.ErrorLog("Invalid {0} zero point: {1}", map, value);
                return false;
            }
        }
        else
        {
            if((byteLength == 1 && (value < 0 || value > 255))
                || (byteLength == 2 && value != 0 && value != 32768)
                || (byteLength != 1 && byteLength != 2 && value != 0))
            {
                this.ErrorLog("Invalid {0} zero point: {1}", map, value);
                return false;
            }
        }
        return true;
    }

    private bool ValidateOutputZeroPoint(FeatureMap map, FusedActivation activation)
    {
        var value = GetZeroPoint(map, activation.OutputSigned);
        // U85 TRM 3.10.57: these limits are independent of OFM precision.
        if(activation.OutputSigned ? value < -128 || value > 127 : value > 255 && value != 32768)
        {
            this.ErrorLog("Invalid {0} zero point: {1}", map, value);
            return false;
        }
        return true;
    }

    // U85 TRM scale_a/scale_b: input scaling applies to 8-bit inputs and to
    // signed 16-bit inputs only.
    private long ScaleInput(FeatureMap map, long value)
    {
        // Both scale_a and scale_b use IFM precision. Leaky ReLU uses scale_b
        // for negative IFM values without reading IFM2 or its precision.
        var (signed, byteLength) = DecodeActivationType(commandRegisters.IfmPrecision, isOfm: false);
        if(byteLength != 1 && !(byteLength == 2 && signed))
        {
            return value;
        }

        var scale = map == FeatureMap.Ifm ? commandRegisters.IfmScale : commandRegisters.Ifm2Scale;
        var shift = map == FeatureMap.Ifm ? commandRegisters.IfmScaleShift : commandRegisters.Ifm2ScaleShift;
        var doubleRoundShift = map == FeatureMap.Ifm ? commandRegisters.IfmScaleDoubleRound : commandRegisters.Ifm2ScaleDoubleRound;
        var roundMode = map == FeatureMap.Ifm ? commandRegisters.IfmScaleRoundMode : commandRegisters.Ifm2ScaleRoundMode;
        var mode = roundMode != 0 ? RoundingMode.Natural : RoundingMode.DoubleSymmetric;
        return unchecked((int)RoundedShiftRight(checked(value * scale), shift, mode, doubleRoundShift));
    }

    private RoundingMode GetOfmRoundingMode()
    {
        return commandRegisters.OfmScaleRoundMode switch
        {
            0 => RoundingMode.DoubleSymmetric,
            1 => RoundingMode.Natural,
            2 => RoundingMode.DoubleAsymmetric,
            3 => RoundingMode.Symmetric,
            4 => RoundingMode.TruncateToZero,
            5 => RoundingMode.TruncateToLower,
            _ => throw new InvalidOperationException("Reserved OFM rounding mode"),
        };
    }

    private bool TryRescale(long accumulator, RescaleMode mode, out long result)
    {
        result = 0;
        if(mode == RescaleMode.None)
        {
            result = accumulator;
            return true;
        }

        if((commandRegisters.OfmPrecision & (1 << 8)) == 0)
        {
            this.ErrorLog("Elementwise operations require global output scaling");
            return false;
        }

        RoundingMode rounding;
        try
        {
            rounding = GetOfmRoundingMode();
        }
        catch(InvalidOperationException)
        {
            this.ErrorLog("Reserved OFM rounding mode");
            return false;
        }

        try
        {
            var product = mode == RescaleMode.ShiftOnly
                ? accumulator
                : checked(accumulator * commandRegisters.OfmScale);
            result = RoundedShiftRight(product, commandRegisters.OfmScaleShift, rounding, commandRegisters.OfmScaleDoubleRound);
            return true;
        }
        catch(OverflowException)
        {
            this.ErrorLog("Scaling product exceeds the signed 64-bit range");
            return false;
        }
    }

    // U85 TRM 3.6.7 activation()
    private bool TryPrepareActivation(out FusedActivation activation)
    {
        activation = default;
        var register = commandRegisters.Activation;
        var function = (register >> 0) & 0x1F;
        var clipRange = ((register >> 12) & 1) == 0 ? ActivationClipRange.B16 : ActivationClipRange.None;
        if((register & ~((0x1F << 0) | (0x7 << 5) | (1 << 12))) != 0)
        {
            this.ErrorLog("Invalid activation encoding: 0x{0:X}", register);
            return false;
        }

        var kind = function switch
        {
            0 => ActivationKind.None,
            10 => ActivationKind.Tanh,
            11 => ActivationKind.Sigmoid,
            1 => ActivationKind.LutU8U8,
            4 => ActivationKind.LutS8S8,
            5 => ActivationKind.LutS8S16,
            7 => ActivationKind.LutS8S32,
            8 => ActivationKind.LutS16S16,
            9 => ActivationKind.LutS16S32,
            _ => ActivationKind.Reserved,
        };
        if(kind == ActivationKind.Reserved)
        {
            this.ErrorLog("Reserved activation function: {0}", function);
            return false;
        }
        if(kind != ActivationKind.None)
        {
            // Unlike U55, U85 also implements tanh and sigmoid through a
            // programmable LUT. DMA loading of that table is not implemented.
            this.ErrorLog("Lookup table activations are not implemented for U85 elementwise operations");
            return false;
        }

        var outputSigned = IsSigned(FeatureMap.Ofm);
        activation = new FusedActivation
        {
            ClipRange = clipRange,
            OutputZeroPoint = GetZeroPoint(FeatureMap.Ofm, outputSigned),
            ClipMinimum = outputSigned ? (short)commandRegisters.ActivationMin : commandRegisters.ActivationMin,
            ClipMaximum = outputSigned ? (short)commandRegisters.ActivationMax : commandRegisters.ActivationMax,
            OutputSigned = outputSigned,
        };
        if(clipRange == ActivationClipRange.B16 && activation.ClipMinimum > activation.ClipMaximum)
        {
            this.ErrorLog("Activation minimum exceeds maximum");
            return false;
        }
        return true;
    }

    private long ActivationStage(long value, FusedActivation activation)
    {
        // The activation input is int32, including after a wider rescale.
        value = unchecked((int)(value + activation.OutputZeroPoint));

        if(activation.ClipRange == ActivationClipRange.B16)
        {
            value = Math.Clamp(value, activation.ClipMinimum, activation.ClipMaximum);
        }

        return value;
    }

    private bool TryGetFeatureMapValue(FeatureMap map, long row, long column, long channel, out long value)
    {
        value = 0;
        if(!TryGetElementAddress(map, row, column, channel, out var address))
        {
            return false;
        }

        var (signed, byteLength) = DecodeActivationType(GetPrecision(map), isOfm: false);
        var bytes = sysbus.ReadBytes(address, byteLength);
        value = ReadValue(bytes, 0, byteLength, signed) - GetZeroPoint(map);
        return true;
    }

    private bool WriteFeatureMapValue(FeatureMap map, long row, long column, long channel, long value)
    {
        // U85 TRM write_ofm: record the low bit even when output storage is
        // disabled. The flag is unspecified for tensors with multiple elements.
        if(GetOfmStripeDimensions() == (1, 1, 1))
        {
            conditionResultFlag.Value = (value & 1) != 0;
        }
        var storage = (GetPrecision(map) >> 14) & 0b11;
        if(storage == (int)TileStorage.None)
        {
            return true;
        }
        if(!TryGetElementAddress(map, row, column, channel, out var address))
        {
            return false;
        }

        var (_, byteLength) = DecodeActivationType(GetPrecision(map), isOfm: true);
        var bytes = new byte[byteLength];
        WriteValue(bytes, 0, value, byteLength);
        sysbus.WriteBytes(bytes, address);
        return true;
    }

    private bool TryGetElementAddress(FeatureMap map, long row, long column, long channel, out ulong address)
    {
        address = 0;
        var precision = GetPrecision(map);
        var (_, byteLength) = DecodeActivationType(precision, isOfm: map == FeatureMap.Ofm);
        var storage = (precision >> 14) & 0b11;
        var (width0, height0, _) = GetTensorDimensions(map);
        var height1 = GetTileHeight1(map);

        long tile = 0;
        long tileRow = row;
        long tileColumn = column;
        if(storage == (int)TileStorage.Tile2x2)
        {
            if(tileColumn >= width0)
            {
                tileColumn -= width0;
                tile++;
            }
            var tileHeight = tile == 1 ? height1 : height0;
            if(tileRow >= tileHeight)
            {
                tileRow -= tileHeight;
                tile += 2;
            }
        }
        else if(storage == (int)TileStorage.Tile3x1)
        {
            if(tileRow >= height1)
            {
                tileRow -= height1;
                tile = 2;
            }
            else if(tileRow >= height0)
            {
                tileRow -= height0;
                tile = 1;
            }
        }
        else if(storage != (int)TileStorage.None)
        {
            this.ErrorLog("Unsupported {0} storage mode: {1}", map, storage);
            return false;
        }

        if(!TryGetBaseAddress(map, (int)tile, out var tileBase))
        {
            return false;
        }

        var (strideX, strideY, strideC) = GetTensorStrides(map);
        long offset;
        try
        {
            offset = IsNHCWB16Layout(map)
                ? checked((channel / 16) * strideC
                    + tileRow * strideY
                    + tileColumn * 16 * byteLength
                    + (channel % 16) * byteLength)
                : checked(channel * byteLength + tileRow * strideY + tileColumn * strideX);
        }
        catch(OverflowException)
        {
            this.ErrorLog("{0} element address overflow", map);
            return false;
        }

        if(offset >= 0)
        {
            if((ulong)offset > ulong.MaxValue - tileBase)
            {
                this.ErrorLog("{0} element address overflow", map);
                return false;
            }
            address = tileBase + (ulong)offset;
        }
        else
        {
            var magnitude = (ulong)(-offset);
            if(tileBase < magnitude)
            {
                this.ErrorLog("{0} element address underflow", map);
                return false;
            }
            address = tileBase - magnitude;
        }
        return true;
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

    private bool TryGetBufferAddress(int region, ulong offset, out ulong address, string description)
    {
        address = 0;
        if(region < 0 || region >= BasePointerCount / 2)
        {
            this.ErrorLog("Invalid {0} region: {1}", description, region);
            return false;
        }

        var regionBase = (basePointers[2 * region + 1].Value << 32) | basePointers[2 * region].Value;
        if(regionBase > ulong.MaxValue - offset)
        {
            this.ErrorLog("{0} address overflow", description);
            return false;
        }
        address = regionBase + offset;
        return true;
    }

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

    private (int Width, int Height, int Depth) GetOfmStripeDimensions()
    {
        return (commandRegisters.OfmWidthM1 + 1, commandRegisters.OfmHeightM1 + 1, commandRegisters.OfmDepthM1 + 1);
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

    private struct FusedActivation
    {
        public ActivationClipRange ClipRange;
        public long OutputZeroPoint;
        public long ClipMinimum;
        public long ClipMaximum;
        public bool OutputSigned;
    }

    private enum ActivationFormat
    {
        NHWC = 0,
        NHCWB16 = 1,
    }

    private enum TileStorage
    {
        Tile2x2 = 0,
        Tile3x1 = 1,
        Chained = 2,
        None = 3,
    }

    private enum RescaleMode
    {
        None,
        Global,
        ShiftOnly,
    }

    private enum ActivationKind
    {
        None,
        Tanh,
        Sigmoid,
        LutU8U8,
        LutS8S8,
        LutS8S16,
        LutS8S32,
        LutS16S16,
        LutS16S32,
        Reserved,
    }

    private enum ActivationClipRange
    {
        B16,
        None,
    }
}
