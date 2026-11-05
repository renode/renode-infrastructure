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
    private static bool TryDecodeResizeMode(ushort parameter, out ResizeMode mode)
    {
        switch(parameter)
        {
        case (ushort)ResizeMode.Bilinear:
        case (ushort)ResizeMode.Replicate:
        case (ushort)ResizeMode.Nearest:
            mode = (ResizeMode)parameter;
            return true;
        default:
            mode = default;
            return false;
        }
    }

    // U85 TRM 3.10.11 NPU_OP_RESIZE
    private bool ExecuteResize(ushort parameter)
    {
        if(!TryDecodeResizeMode(parameter, out var resizeMode))
        {
            this.ErrorLog("Unsupported resize mode: {0}", parameter);
            return false;
        }

        var (outputWidth, outputHeight, outputDepth) = GetOfmStripeDimensions();
        // Vela clips the width to the sampled span (matching U85 TRM 3.10.45).
        var ifmHeightM1 = commandRegisters.KernelHeightM1;
        var ifmWidthM1 = commandRegisters.KernelWidthM1;
        var scaleY = (commandRegisters.ResizeYScaleNM1 & 0x7FF) + 1;
        var scaleX = (commandRegisters.ResizeXScaleNM1 & 0x7FF) + 1;
        var yOffset = SignExtendTruncated(commandRegisters.ResizeYOffset, 12);
        var xOffset = SignExtendTruncated(commandRegisters.ResizeXOffset, 12);
        var stepYMod = commandRegisters.ResizeYOneStepMod;
        var stepXMod = commandRegisters.ResizeXOneStepMod;
        var stepYInt = commandRegisters.ResizeYOneStepInt;
        var stepXInt = commandRegisters.ResizeXOneStepInt;

        if(yOffset < -scaleY || yOffset >= scaleY)
        {
            this.ErrorLog("Resize Y offset {0} is outside the range [-{1}, {1})", yOffset, scaleY);
            return false;
        }
        if(xOffset < -scaleX || xOffset >= scaleX)
        {
            this.ErrorLog("Resize X offset {0} is outside the range [-{1}, {1})", xOffset, scaleX);
            return false;
        }
        if(stepYMod >= scaleY)
        {
            this.ErrorLog("Resize Y step modulo {0} must be less than scale {1}", stepYMod, scaleY);
            return false;
        }
        if(stepXMod >= scaleX)
        {
            this.ErrorLog("Resize X step modulo {0} must be less than scale {1}", stepXMod, scaleX);
            return false;
        }

        if(!ValidateResizeConfiguration())
        {
            return false;
        }

        var (_, ifmByteLength) = DecodeActivationType(commandRegisters.IfmPrecision, isOfm: false);
        var (_, ofmByteLength) = DecodeActivationType(commandRegisters.OfmPrecision, isOfm: true);
        // The resize accumulator is int31_t for 8-bit input and int39_t for signed 16-bit input (U85 TRM 3.10.11).
        var accumulatorBits = ifmByteLength == 1 ? 31 : 39;

        var activation = default(FusedActivation);
        if(ofmByteLength != 8)
        {
            if(!TryPrepareActivation(out activation) || !ValidateOutputZeroPoint(FeatureMap.Ofm, activation))
            {
                return false;
            }
        }

        this.DebugLog(
            "Resize {0}: output={1}x{2}x{3}, input={4}x{5}, scale={6}/{7}, offset={8}/{9}, step={10}+{11}/{12}+{13}, IFM precision=0x{14:X4}, OFM precision=0x{15:X4}",
            resizeMode, outputWidth, outputHeight, outputDepth, ifmWidthM1 + 1, ifmHeightM1 + 1,
            scaleX, scaleY, xOffset, yOffset, stepXInt, stepXMod, stepYInt, stepYMod, commandRegisters.IfmPrecision, commandRegisters.OfmPrecision);

        var yInt = yOffset < 0 ? -1 : 0;
        var yMod = yOffset < 0 ? yOffset + scaleY : yOffset;
        for(var oy = 0; oy < outputHeight; oy++)
        {
            var iy0 = Math.Max(yInt, 0);
            var iy1 = Math.Min(yInt + 1, ifmHeightM1);
            if(iy0 > ifmHeightM1)
            {
                this.ErrorLog("Resize Y coordinate {0} exceeds the IFM height", iy0);
                return false;
            }

            var xInt = xOffset < 0 ? -1 : 0;
            var xMod = xOffset < 0 ? xOffset + scaleX : xOffset;
            for(var ox = 0; ox < outputWidth; ox++)
            {
                var ix0 = Math.Max(xInt, 0);
                var ix1 = Math.Min(xInt + 1, ifmWidthM1);
                if(ix0 > ifmWidthM1)
                {
                    this.ErrorLog("Resize X coordinate {0} exceeds the IFM width", ix0);
                    return false;
                }
                if(ox == outputWidth - 1 && ix1 != ifmWidthM1)
                {
                    this.ErrorLog("Resize X scaling does not map the last OFM column to the last IFM column");
                    return false;
                }

                for(var oc = 0; oc < outputDepth; oc++)
                {
                    if(!TryGetFeatureMapValue(FeatureMap.Ifm, iy0, ix0, oc, out var v0))
                    {
                        return false;
                    }

                    long acc;
                    switch(resizeMode)
                    {
                    case ResizeMode.Bilinear:
                        if(!TryGetFeatureMapValue(FeatureMap.Ifm, iy0, ix1, oc, out var v1)
                            || !TryGetFeatureMapValue(FeatureMap.Ifm, iy1, ix0, oc, out var v2)
                            || !TryGetFeatureMapValue(FeatureMap.Ifm, iy1, ix1, oc, out var v3))
                        {
                            return false;
                        }
                        acc = (scaleY - yMod) * ((scaleX - xMod) * v0 + xMod * v1)
                            + yMod * ((scaleX - xMod) * v2 + xMod * v3);
                        break;
                    case ResizeMode.Nearest:
                        var ix = 2 * xMod < scaleX ? ix0 : ix1;
                        var iy = 2 * yMod < scaleY ? iy0 : iy1;
                        if(!TryGetFeatureMapValue(FeatureMap.Ifm, iy, ix, oc, out acc))
                        {
                            return false;
                        }
                        break;
                    case ResizeMode.Replicate:
                        acc = v0;
                        break;
                    default:
                        throw new UnreachableException();
                    }
                    acc = SignExtendTruncated(acc, accumulatorBits);

                    // Resize only supports shift-only rescaling, with the accumulator shifted left by 16 bits.
                    if(!TryRescale(acc << 16, RescaleMode.ShiftOnly, out var outputValue))
                    {
                        return false;
                    }
                    if(ofmByteLength != 8)
                    {
                        outputValue = ActivationStage(outputValue, activation);
                    }

                    if(!WriteOfmValue(oy, ox, oc, outputValue))
                    {
                        return false;
                    }
                }

                xMod += stepXMod;
                xInt += stepXInt;
                if(xMod >= scaleX)
                {
                    xMod -= scaleX;
                    xInt++;
                }
            }

            yMod += stepYMod;
            yInt += stepYInt;
            if(yMod >= scaleY)
            {
                yMod -= scaleY;
                yInt++;
            }
        }

        return true;
    }

    private bool ValidateResizeConfiguration()
    {
        var ifmPrecision = commandRegisters.IfmPrecision;
        var ofmPrecision = commandRegisters.OfmPrecision;

        bool Check(bool condition, string message)
        {
            if(!condition)
            {
                this.ErrorLog("Invalid resize configuration: {0}", message);
            }
            return condition;
        }

        if(!Check(((ifmPrecision >> 6) & 0b11) <= (int)ActivationFormat.NHCWB16
                && ((ofmPrecision >> 6) & 0b11) <= (int)ActivationFormat.NHCWB16,
                $"reserved activation format: IFM=0x{ifmPrecision:X4}, OFM=0x{ofmPrecision:X4}"))
        {
            return false;
        }

        var ifmStorage = (ifmPrecision >> 14) & 0b11;
        if(!Check(ifmStorage != (int)TileStorage.Chained, "chained IFM storage is not supported")
            || !Check(ifmStorage != (int)TileStorage.None, "IFM activation storage is required"))
        {
            return false;
        }

        var ofmStorage = (ofmPrecision >> 14) & 0b11;
        if(!Check(ofmStorage != (int)TileStorage.Chained, "chained OFM storage is not supported")
            || !Check(((ofmPrecision >> 11) & 0b111) == 0, "OFM transposition is not supported"))
        {
            return false;
        }

        var (ifmSigned, ifmByteLength) = DecodeActivationType(ifmPrecision, isOfm: false);
        if(!Check(ifmByteLength == 1 || (ifmByteLength == 2 && ifmSigned),
                $"unsupported IFM precision: 0x{ifmPrecision:X4}"))
        {
            return false;
        }

        var (_, ofmByteLength) = DecodeActivationType(ofmPrecision, isOfm: true);
        var reverse = (ActivationReverse)((ofmPrecision >> 9) & 0b11);
        // write_ofm returns before applying reverse when storage is disabled.
        if(!Check(ofmStorage == (int)TileStorage.None || reverse == ActivationReverse.None || ofmByteLength != 8,
                "OFM reverse with 64-bit output is not supported"))
        {
            return false;
        }

        if(!ValidateOutputChannelReverse())
        {
            return false;
        }

        if(!TryGetIfmUpscale(out var upscale))
        {
            return false;
        }
        if(!Check(upscale == IfmUpscaleMode.None, "IFM upscale is not supported"))
        {
            return false;
        }
        if(!Check(commandRegisters.IfmZeroPoint == 0 && commandRegisters.Ifm2ZeroPoint == 0,
                "IFM and IFM2 zero points must be zero"))
        {
            return false;
        }
        if(!Check((ofmPrecision & (1 << 8)) != 0, "global output scaling is required"))
        {
            return false;
        }
        if(!Check(commandRegisters.OfmScaleShift >= 16, "output scale shift must be at least 16"))
        {
            return false;
        }
        if(!Check(commandRegisters.OfmScaleDoubleRound <= 30 && commandRegisters.OfmScaleRoundMode <= 5,
                "invalid output rounding parameters"))
        {
            return false;
        }

        return true;
    }

    private enum ResizeMode : ushort
    {
        Bilinear = 0b00,
        Replicate = 0b01,
        Nearest = 0b10,
    }
}
