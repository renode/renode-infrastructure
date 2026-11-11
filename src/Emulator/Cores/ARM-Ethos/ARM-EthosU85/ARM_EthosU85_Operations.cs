//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Logging;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
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

        var (signed, byteLength) = GetElementType(map);
        var bytes = sysbus.ReadBytes(address, byteLength);
        value = ReadValue(bytes, 0, byteLength, signed) - GetZeroPoint(map);
        return true;
    }

    private bool ValidateOutputChannelReverse()
    {
        var precision = commandRegisters.OfmPrecision;
        var storage = (TileStorage)((precision >> 14) & 0b11);
        var reverse = (ActivationReverse)((precision >> 9) & 0b11);
        if(storage == TileStorage.None || reverse != ActivationReverse.C)
        {
            return true;
        }

        // U85 TRM 3.6.4 microblock_depth() and 3.6.5 write_ofm().
        var microblockDepth = ((commandRegisters.AccFormat >> 8) & 0b111) switch
        {
            0 => macsPerCycle / 8,   // u1x1
            1 => macsPerCycle / 16,  // u1x2
            2 => macsPerCycle / 32,  // u1x4
            3 => macsPerCycle / 32,  // u2x2
            4 => macsPerCycle / 64,  // u2x4
            5 => macsPerCycle / 128, // u4x4
            _ => 0,
        };
        if(microblockDepth == 0)
        {
            this.ErrorLog("Reserved microblock encoding for OFM channel reverse");
            return false;
        }
        var (_, _, depth) = GetOfmStripeDimensions();
        if(depth % Math.Max(16, microblockDepth) != 0
            && (IsNHCWB16Layout(FeatureMap.Ofm) || commandRegisters.OfmBlkDepthM1 >= 16))
        {
            this.ErrorLog("OFM channel reverse with a partial microblock requires NHWC and block depth at most 16");
            return false;
        }
        return true;
    }

    private bool WriteOfmValue(long row, long column, long channel, long value)
    {
        // U85 TRM write_ofm: record the low bit even when output storage is
        // disabled. Only the OFM has a result flag, it is unspecified for
        // tensors with multiple elements.
        if(GetOfmStripeDimensions() == (1, 1, 1))
        {
            conditionResultFlag.Value = (value & 1) != 0;
        }
        var storage = (GetPrecision(FeatureMap.Ofm) >> 14) & 0b11;
        if(storage == (int)TileStorage.None)
        {
            return true;
        }

        // Reverse coordinates before selecting the output tile.
        var reverse = (ActivationReverse)((GetPrecision(FeatureMap.Ofm) >> 9) & 0b11);
        var (width, height, depth) = GetOfmStripeDimensions();
        switch(reverse)
        {
        case ActivationReverse.H:
            row = height - 1 - row;
            break;
        case ActivationReverse.W:
            column = width - 1 - column;
            break;
        case ActivationReverse.C:
            channel = depth - 1 - channel;
            break;
        }
        if(!TryGetElementAddress(FeatureMap.Ofm, row, column, channel, out var address))
        {
            return false;
        }

        var byteLength = GetElementByteLength(FeatureMap.Ofm);
        var bytes = new byte[byteLength];
        WriteValue(bytes, 0, value, byteLength);
        sysbus.WriteBytes(bytes, address);
        return true;
    }

    private bool TryGetElementAddress(FeatureMap map, long row, long column, long channel, out ulong address)
    {
        address = 0;
        var precision = GetPrecision(map);
        var storage = (precision >> 14) & 0b11;
        var (width0, height0, _) = GetTensorDimensions(map);
        var height1 = GetTileHeight1(map);

        long tile = 0;
        long tileRow = row;
        long tileColumn = column;
        if(storage == (int)TileStorage.Tile2x2)
        {
            (tile, tileRow, tileColumn) = SelectTile2x2(row, column, width0, height0, height1);
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

        long offset;
        try
        {
            offset = GetTensorAddress(map, tileRow, tileColumn, channel);
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

    private struct FusedActivation
    {
        public ActivationClipRange ClipRange;
        public long OutputZeroPoint;
        public long ClipMinimum;
        public long ClipMaximum;
        public bool OutputSigned;
    }

    private enum ActivationReverse
    {
        None,
        H,
        W,
        C,
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
