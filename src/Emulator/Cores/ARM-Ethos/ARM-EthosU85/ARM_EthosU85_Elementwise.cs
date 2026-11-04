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

using static Antmicro.Renode.Peripherals.ARM_Ethos.ARM_EthosCommon;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
    private static long BroadcastIndex(int broadcast, long value, int axis)
    {
        return (broadcast & axis) != 0 ? 0 : value;
    }

    private static bool ElementwiseUsesOutputScale(ElementwiseMode mode)
    {
        // The U85 pseudocode returns without rescaling only for the shift modes.
        return mode != ElementwiseMode.Shr
            && mode != ElementwiseMode.Shl
            && mode != ElementwiseMode.Lsr;
    }

    // U85 TRM 3.10.6 NPU_OP_ELEMENTWISE
    private bool ExecuteElementwise(ushort parameter)
    {
        if(!TryDecodeElementwiseMode(parameter, out var mode))
        {
            this.ErrorLog("Unsupported elementwise mode: {0}", parameter);
            return false;
        }

        var isUnary = mode == ElementwiseMode.LeakyRelu
            || mode == ElementwiseMode.Abs
            || mode == ElementwiseMode.Clz
            || mode == ElementwiseMode.Not;
        var ifmBroadcast = commandRegisters.IfmBroadcast & 0xF;
        var ifm2Broadcast = commandRegisters.Ifm2Broadcast & 0xF;
        if(ifmBroadcast > (ushort)BroadcastMode.Scalar || (!isUnary && ifm2Broadcast > (ushort)BroadcastMode.Scalar))
        {
            this.ErrorLog("Reserved elementwise broadcast mode: IFM=0x{0:X}, IFM2=0x{1:X}",
                commandRegisters.IfmBroadcast, commandRegisters.Ifm2Broadcast);
            return false;
        }

        var ifmScalar = ifmBroadcast == (ushort)BroadcastMode.Scalar;
        var ifm2Scalar = ifm2Broadcast == (ushort)BroadcastMode.Scalar;
        if(!isUnary && ifmScalar && ifm2Scalar)
        {
            this.ErrorLog("Both elementwise inputs cannot use scalar broadcast");
            return false;
        }
        var useIfm2 = !(isUnary || ifm2Scalar);

        if(!TryGetIfmUpscale(out var upscale))
        {
            return false;
        }
        if(upscale != IfmUpscaleMode.None)
        {
            this.ErrorLog("Elementwise operations do not support IFM upscale");
            return false;
        }
        if(!ValidateElementwiseConfiguration(mode, isUnary, ifmScalar, ifm2Scalar))
        {
            return false;
        }
        if(!TryPrepareActivation(out var activation))
        {
            return false;
        }

        var (outputWidth, outputHeight, outputDepth) = GetOfmStripeDimensions();
        var (ifmSigned, ifmByteLength) = DecodeActivationType(commandRegisters.IfmPrecision, isOfm: false);
        if((!ifmScalar && !ValidateZeroPoint(FeatureMap.Ifm, ifmByteLength, ifmSigned))
            || (useIfm2 && !ValidateZeroPoint(FeatureMap.Ifm2, ifmByteLength, ifmSigned))
            || !ValidateOutputZeroPoint(FeatureMap.Ofm, activation))
        {
            return false;
        }
        if(mode == ElementwiseMode.Mul && ifmByteLength == 2 && !ifmSigned
            && (GetZeroPoint(FeatureMap.Ifm) != 32768 || GetZeroPoint(FeatureMap.Ifm2) != 32768))
        {
            this.ErrorLog("Unsigned 16-bit multiply requires input zero points of 32768");
            return false;
        }
        if(!ValidateElementwiseRounding(mode, isUnary, ifmByteLength, ifmSigned))
        {
            return false;
        }

        var scalar = commandRegisters.OpScalar;
        this.DebugLog(
            "Elementwise {0}: output={1}x{2}x{3}, precision={4}/{5}, ifm broadcast=0x{6:X}, ifm2 broadcast=0x{7:X}, scalar={8}, activation=0x{9:X}",
            mode, outputWidth, outputHeight, outputDepth,
            commandRegisters.IfmPrecision, commandRegisters.OfmPrecision,
            ifmBroadcast, ifm2Broadcast, scalar, commandRegisters.Activation);

        for(var oY = 0; oY < outputHeight; oY++)
        {
            for(var oX = 0; oX < outputWidth; oX++)
            {
                for(var oD = 0; oD < outputDepth; oD++)
                {
                    if(!TryGetElementwiseOperand(FeatureMap.Ifm, ifmBroadcast, oY, oX, oD, scalar, out var operandA))
                    {
                        return false;
                    }
                    var operandBValue = 0L;
                    if(!isUnary && !TryGetElementwiseOperand(FeatureMap.Ifm2, ifm2Broadcast, oY, oX, oD, scalar, out operandBValue))
                    {
                        return false;
                    }
                    var rescaleMode = RescaleMode.Global;
                    long result;
                    switch(mode)
                    {
                    case ElementwiseMode.Mul:
                        result = operandA * operandBValue;
                        // 32-bit multiplication uses the full product and only shifts.
                        if(ifmByteLength != 1 && ifmByteLength != 2)
                        {
                            rescaleMode = RescaleMode.ShiftOnly;
                        }
                        break;
                    case ElementwiseMode.Add:
                        result = ScaleInput(FeatureMap.Ifm, operandA) + ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    case ElementwiseMode.Sub:
                        result = ScaleInput(FeatureMap.Ifm, operandA) - ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    case ElementwiseMode.Min:
                        result = Math.Min(ScaleInput(FeatureMap.Ifm, operandA), ScaleInput(FeatureMap.Ifm2, operandBValue));
                        break;
                    case ElementwiseMode.Max:
                        result = Math.Max(ScaleInput(FeatureMap.Ifm, operandA), ScaleInput(FeatureMap.Ifm2, operandBValue));
                        break;
                    case ElementwiseMode.LeakyRelu:
                        result = operandA >= 0
                            ? ScaleInput(FeatureMap.Ifm, operandA)
                            : ScaleInput(FeatureMap.Ifm2, operandA);
                        break;
                    case ElementwiseMode.Abs:
                        var scaledA = ScaleInput(FeatureMap.Ifm, operandA);
                        result = scaledA < 0 ? unchecked(-scaledA) : scaledA;
                        break;
                    case ElementwiseMode.Clz:
                        result = BitOperations.LeadingZeroCount(unchecked((uint)ScaleInput(FeatureMap.Ifm, operandA)));
                        break;
                    case ElementwiseMode.Shr:
                        result = RoundedShiftRight(
                            ScaleInput(FeatureMap.Ifm, operandA),
                            (int)(ScaleInput(FeatureMap.Ifm2, operandBValue) & 31),
                            GetOfmRoundingMode(),
                            commandRegisters.OfmScaleDoubleRound);
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Shl:
                        var shifted = ScaleInput(FeatureMap.Ifm, operandA) << (int)(ScaleInput(FeatureMap.Ifm2, operandBValue) & 31);
                        result = ifmByteLength switch
                        {
                            1 => (long)(byte)shifted,
                            2 => (long)(ushort)shifted,
                            _ => unchecked((int)shifted),
                        };
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Lsr:
                        // Logical shift right: no rounding, and the source is
                        // masked to the IFM precision before the unsigned shift.
                        var logical = ScaleInput(FeatureMap.Ifm, operandA);
                        if(ifmByteLength == 1)
                        {
                            logical &= 0xFF;
                        }
                        else if(ifmByteLength == 2)
                        {
                            logical &= 0xFFFF;
                        }
                        result = (long)(unchecked((uint)logical) >> (int)(ScaleInput(FeatureMap.Ifm2, operandBValue) & 31));
                        rescaleMode = RescaleMode.None;
                        break;
                    case ElementwiseMode.Div:
                        if(operandBValue == 0)
                        {
                            this.ErrorLog("Elementwise divide by zero");
                            return false;
                        }
                        if(operandA == int.MinValue && operandBValue == -1)
                        {
                            this.ErrorLog("Elementwise divide overflow");
                            return false;
                        }
                        result = operandA / operandBValue;
                        break;
                    case ElementwiseMode.CmpEq:
                        result = ScaleInput(FeatureMap.Ifm, operandA) == ScaleInput(FeatureMap.Ifm2, operandBValue) ? -1 : 0;
                        break;
                    case ElementwiseMode.CmpNe:
                        result = ScaleInput(FeatureMap.Ifm, operandA) != ScaleInput(FeatureMap.Ifm2, operandBValue) ? -1 : 0;
                        break;
                    case ElementwiseMode.CmpGe:
                        result = ScaleInput(FeatureMap.Ifm, operandA) >= ScaleInput(FeatureMap.Ifm2, operandBValue) ? -1 : 0;
                        break;
                    case ElementwiseMode.CmpGt:
                        result = ScaleInput(FeatureMap.Ifm, operandA) > ScaleInput(FeatureMap.Ifm2, operandBValue) ? -1 : 0;
                        break;
                    case ElementwiseMode.And:
                        result = ScaleInput(FeatureMap.Ifm, operandA) & ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    case ElementwiseMode.Or:
                        result = ScaleInput(FeatureMap.Ifm, operandA) | ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    case ElementwiseMode.Xor:
                        result = ScaleInput(FeatureMap.Ifm, operandA) ^ ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    case ElementwiseMode.Not:
                        result = ~ScaleInput(FeatureMap.Ifm, operandA);
                        break;
                    case ElementwiseMode.AndNot:
                        result = ScaleInput(FeatureMap.Ifm, operandA) & ~ScaleInput(FeatureMap.Ifm2, operandBValue);
                        break;
                    default:
                        throw new UnreachableException();
                    }

                    // AO arithmetic produces int32 values. The 32-bit multiply
                    // is the exception: its full product feeds shift-only rescaling.
                    if(rescaleMode != RescaleMode.ShiftOnly)
                    {
                        result = unchecked((int)result);
                    }
                    if(!TryRescale(result, rescaleMode, out var outputValue))
                    {
                        return false;
                    }
                    outputValue = ActivationStage(outputValue, activation);
                    if(!WriteFeatureMapValue(FeatureMap.Ofm, oY, oX, oD, outputValue))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private bool TryGetElementwiseOperand(FeatureMap map, int broadcast, long row, long column, long channel, int scalar, out long value)
    {
        if(broadcast == (int)BroadcastMode.Scalar)
        {
            // NPU_SET_OP_SCALAR is the operand value directly; no zero point is
            // subtracted for a scalar broadcast (U85 TRM read_ifm/read_ifm2).
            value = scalar;
            return true;
        }
        return TryGetFeatureMapValue(map, BroadcastIndex(broadcast, row, 0x1), BroadcastIndex(broadcast, column, 0x2), BroadcastIndex(broadcast, channel, 0x4), out value);
    }

    private bool TryDecodeElementwiseMode(ushort parameter, out ElementwiseMode mode)
    {
        switch(parameter)
        {
        case (ushort)ElementwiseMode.Mul:
        case (ushort)ElementwiseMode.Add:
        case (ushort)ElementwiseMode.Sub:
        case (ushort)ElementwiseMode.Min:
        case (ushort)ElementwiseMode.Max:
        case (ushort)ElementwiseMode.LeakyRelu:
        case (ushort)ElementwiseMode.Abs:
        case (ushort)ElementwiseMode.Clz:
        case (ushort)ElementwiseMode.Shr:
        case (ushort)ElementwiseMode.Shl:
        case (ushort)ElementwiseMode.Lsr:
        case (ushort)ElementwiseMode.Div:
        case (ushort)ElementwiseMode.CmpEq:
        case (ushort)ElementwiseMode.CmpNe:
        case (ushort)ElementwiseMode.CmpGe:
        case (ushort)ElementwiseMode.CmpGt:
        case (ushort)ElementwiseMode.And:
        case (ushort)ElementwiseMode.Or:
        case (ushort)ElementwiseMode.Xor:
        case (ushort)ElementwiseMode.Not:
        case (ushort)ElementwiseMode.AndNot:
            mode = (ElementwiseMode)parameter;
            return true;
        default:
            mode = default;
            return false;
        }
    }

    private bool ValidateElementwiseRounding(ElementwiseMode mode, bool isUnary, int ifmByteLength, bool ifmSigned)
    {
        // Validate before executing so malformed commands report a parse error
        // instead of escaping through RoundedShiftRight/GetOfmRoundingMode.
        var scalesInputs = mode != ElementwiseMode.Mul && mode != ElementwiseMode.Div
            && (ifmByteLength == 1 || (ifmByteLength == 2 && ifmSigned));
        if(scalesInputs && (commandRegisters.IfmScaleDoubleRound > 30
            || ((!isUnary || mode == ElementwiseMode.LeakyRelu) && commandRegisters.Ifm2ScaleDoubleRound > 30)))
        {
            this.ErrorLog("Invalid input double-round shift");
            return false;
        }
        if((ElementwiseUsesOutputScale(mode) || mode == ElementwiseMode.Shr)
            && (commandRegisters.OfmScaleDoubleRound > 30 || commandRegisters.OfmScaleRoundMode > 5))
        {
            this.ErrorLog("Invalid output rounding parameters");
            return false;
        }
        return true;
    }

    private bool ValidateElementwiseConfiguration(ElementwiseMode mode, bool isUnary, bool ifmScalar, bool ifm2Scalar)
    {
        var (ifmSigned, ifmByteLength) = DecodeActivationType(commandRegisters.IfmPrecision, isOfm: false);
        var (_, ofmByteLength) = DecodeActivationType(commandRegisters.OfmPrecision, isOfm: true);
        if(ifmByteLength == 0 || ofmByteLength == 0)
        {
            this.ErrorLog("Invalid elementwise precision: IFM=0x{0:X4}, OFM=0x{1:X4}",
                commandRegisters.IfmPrecision, commandRegisters.OfmPrecision);
            return false;
        }
        // U85 TRM 3.10.6: 8- or 16-bit inputs of either type, or signed 32-bit.
        if(ifmByteLength > 4 || (ifmByteLength == 4 && !ifmSigned))
        {
            this.ErrorLog("Unsupported elementwise IFM precision: 0x{0:X4}", commandRegisters.IfmPrecision);
            return false;
        }
        if(ofmByteLength > 4)
        {
            this.ErrorLog("Unsupported elementwise OFM precision: 0x{0:X4}", commandRegisters.OfmPrecision);
            return false;
        }

        if(!isUnary)
        {
            var (ifm2Signed, ifm2ByteLength) = DecodeActivationType(commandRegisters.Ifm2Precision, isOfm: false);
            if(ifm2ByteLength != ifmByteLength || ifm2Signed != ifmSigned)
            {
                this.ErrorLog("IFM2 precision 0x{0:X4} must match IFM precision 0x{1:X4}",
                    commandRegisters.Ifm2Precision, commandRegisters.IfmPrecision);
                return false;
            }
        }

        if(!HasValidElementwiseLayout(FeatureMap.Ifm, ifmScalar)
            || (!isUnary && !HasValidElementwiseLayout(FeatureMap.Ifm2, ifm2Scalar))
            || !HasValidElementwiseLayout(FeatureMap.Ofm, scalar: false))
        {
            return false;
        }

        // Elementwise operations use the global output scale, never per-channel.
        if(ElementwiseUsesOutputScale(mode)
            && (commandRegisters.OfmPrecision & (1 << 8)) == 0)
        {
            this.ErrorLog("Elementwise operations require global output scaling");
            return false;
        }

        if(mode == ElementwiseMode.Div && !ValidateDivisionRegisters())
        {
            return false;
        }

        return true;
    }

    private bool HasValidElementwiseLayout(FeatureMap map, bool scalar)
    {
        var precision = GetPrecision(map);
        if(((precision >> 6) & 0b11) > (int)ActivationFormat.NHCWB16)
        {
            this.ErrorLog("Reserved {0} activation format: 0x{1:X4}", map, precision);
            return false;
        }
        var storage = (precision >> 14) & 0b11;
        if(storage == (int)TileStorage.Chained)
        {
            this.ErrorLog("Chained {0} storage is not implemented", map);
            return false;
        }
        if(scalar && storage != (int)TileStorage.None)
        {
            this.ErrorLog("Scalar {0} requires disabled activation storage", map);
            return false;
        }
        if(map != FeatureMap.Ofm && !scalar && storage == (int)TileStorage.None)
        {
            this.ErrorLog("{0} has no activation storage", map);
            return false;
        }
        if(map == FeatureMap.Ofm)
        {
            if(((precision >> 9) & 0b11) != 0)
            {
                this.ErrorLog("OFM reverse is not supported for elementwise operations");
                return false;
            }
            if(((precision >> 11) & 0b111) != 0)
            {
                this.ErrorLog("OFM transpose is not supported for elementwise operations");
                return false;
            }
        }
        return true;
    }

    private bool ValidateDivisionRegisters()
    {
        // U85 TRM 3.10.6 divide: all input/output scales must be identity and
        // all zero points zero.
        if(commandRegisters.IfmScale != 1 || commandRegisters.IfmScaleShift != 0
            || commandRegisters.Ifm2Scale != 1 || commandRegisters.Ifm2ScaleShift != 0
            || commandRegisters.OfmScale != 1 || commandRegisters.OfmScaleShift != 0
            || GetZeroPoint(FeatureMap.Ifm) != 0
            || GetZeroPoint(FeatureMap.Ifm2) != 0
            || GetZeroPoint(FeatureMap.Ofm) != 0)
        {
            this.ErrorLog("Elementwise divide requires identity scales and zero points");
            return false;
        }
        return true;
    }

    private enum ElementwiseMode : ushort
    {
        Mul = 0b000000,
        Add = 0b000001,
        Sub = 0b000010,
        Min = 0b000011,
        Max = 0b000100,
        LeakyRelu = 0b000101,
        Abs = 0b000110,
        Clz = 0b000111,
        Shr = 0b001000,
        Shl = 0b001001,
        Lsr = 0b001010,
        Div = 0b001011,
        CmpEq = 0b010000,
        CmpNe = 0b010001,
        CmpGe = 0b010010,
        CmpGt = 0b010011,
        And = 0b100001,
        Or = 0b100010,
        Xor = 0b100011,
        Not = 0b100100,
        AndNot = 0b101010,
    }

    private enum BroadcastMode : ushort
    {
        None = 0b0000,
        Height = 0b0001,
        Width = 0b0010,
        HeightWidth = 0b0011,
        Channel = 0b0100,
        ChannelHeight = 0b0101,
        ChannelWidth = 0b0110,
        ChannelWidthHeight = 0b0111,
        Scalar = 0b1000,
    }
}
