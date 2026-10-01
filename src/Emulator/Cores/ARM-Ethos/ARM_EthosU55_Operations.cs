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
    // U85 TRM rounded_shift_right
    private static long RoundedShiftRight(long acc, int shift, RoundingMode mode, int doubleRoundShift)
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

    // U85 TRM rescale
    private bool TryRescale(long accumulator, RescaleMode mode, int validCount, out long result)
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

        RoundingMode? roundingMode = ((commandRegisters.OfmPrecision >> 14) & 0b11) switch
        {
            0 => RoundingMode.DoubleSymmetric,
            1 => RoundingMode.TruncateToZero,
            2 => RoundingMode.Natural,
            _ => null
        };

        if(!roundingMode.HasValue)
        {
            this.Log(LogLevel.Error, "Reserved OFM rounding mode");
            return false;
        }

        var product = mode == RescaleMode.ShiftOnly ? accumulator : checked(accumulator * scale);

        // U55 Doesn't encode dbl_rnd in OFM_SCALE
        var doubleRoundShift = 0;
        result = RoundedShiftRight(product, shift, roundingMode.Value, doubleRoundShift);
        return true;
    }

    // U85 TRM write_accumulator
    private bool WriteAccumulator(byte[] outputBuffer, int row, int column, int channel, long accumulator, RescaleMode rescaleMode, int validCount)
    {
        if(!TryRescale(accumulator, rescaleMode, validCount, out var outputValue))
        {
            return false;
        }

        var outputByteAddress = GetTensorAddress(row, column, channel, false);
        var outputBytes = GetElementByteLength(false);
        if(outputByteAddress < 0 || outputByteAddress > outputBuffer.LongLength - outputBytes)
        {
            this.Log(LogLevel.Error, "Out of bounds output write: x={0}, y={1}, c={2}, outputLength={3}, offset={4}, bytes={5}",
                column, row, channel, outputBuffer.Length, outputByteAddress, outputBytes);
            return false;
        }

        // TODO: Implement zero point and activation path
        for(var i = 0; i < outputBytes; i++)
        {
            outputBuffer[outputByteAddress + i] = unchecked((byte)(outputValue >> (8 * i)));
        }

        return true;
    }

    private int GetElementByteLength(bool input)
    {
        var precision = (input ? (commandRegisters.IfmPrecision >> 2) : (commandRegisters.OfmPrecision >> 1)) & 0b11;
        return precision switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => throw new InvalidOperationException()
        };
    }

    private long GetTensorAddress(long row, long column, long channel, bool input)
    {
        // TODO: Implement tiling
        var (columnStride, rowStride, channelStride) = GetTensorStrides(input);
        var channelOffset = channel * channelStride;
        if(IsNHCWB16Layout(input))
        {
            channelOffset = (channel / 16) * channelStride + (channel % 16) * GetElementByteLength(input);
        }
        return channelOffset + row * rowStride + column * columnStride;
    }

    private uint GetTensorValue(long row, long column, long channel, bool input, byte[] bytes)
    {
        // TODO: Implement upscaling
        // TODO: Implement zero-point configuration
        var byteAddress = (int)GetTensorAddress(row, column, channel, input);
        var byteLength = GetElementByteLength(input);
        if(byteAddress + byteLength > bytes.Length)
        {
            throw new InvalidOperationException($"Out of bounds address: {row} {column} {channel} {input}, {bytes.Length} {byteAddress}");
        }
        return byteLength switch
        {
            1 => bytes[byteAddress],
            2 => BitConverter.ToUInt16(bytes, byteAddress),
            4 => BitConverter.ToUInt32(bytes, byteAddress),
            _ => throw new UnreachableException()
        };
    }

    private long DecodeTensorValue(uint raw, int byteLength, bool signed)
    {
        // Preserve numeric values
        return byteLength switch
        {
            1 => signed ? unchecked((sbyte)raw) : (byte)raw,
            2 => signed ? unchecked((short)raw) : (ushort)raw,
            4 => signed ? unchecked((int)raw) : (long)raw,
            _ => throw new UnreachableException()
        };
    }

    private bool IsNHCWB16Layout(bool input)
    {
        var precision = input ? commandRegisters.IfmPrecision : commandRegisters.OfmPrecision;
        return (precision & (1 << 6)) != 0;
    }

    private bool TryGetBufferAddress(bool input, out ulong address)
    {
        address = 0;
        var region = input ? commandRegisters.IfmRegion : commandRegisters.OfmRegion;
        var offset = input ? commandRegisters.IfmBase0 : commandRegisters.OfmBase0;
        if(region >= basePointers.Length / 2)
        {
            this.Log(LogLevel.Error, "Invalid {0} region: {1}", input ? "IFM" : "OFM", region);
            return false;
        }

        var regionBase = (basePointers[2 * region + 1].Value << 32) | basePointers[2 * region].Value;
        if(regionBase > ulong.MaxValue - offset)
        {
            this.Log(LogLevel.Error, "Region: {0} address overflow", input ? "IFM" : "OFM");
            return false;
        }

        address = regionBase + offset;
        return true;
    }

    // TODO: Include stride in calculation
    private int GetBufferLength(bool input)
    {
        var (width, height, depth) = GetTensorDimensions(input);
        if(IsNHCWB16Layout(input))
        {
            depth = ((depth + 15) / 16) * 16;
        }
        return height * width * depth * GetElementByteLength(input);
    }

    // TODO: Implement strided input
    private bool ReadInputBytes(out byte[] bytes)
    {
        bytes = null;
        if(!TryGetBufferAddress(true, out var sourceAddress))
        {
            return false;
        }

        var inputLength = GetBufferLength(true);
        bytes = sysbus.ReadBytes(sourceAddress, inputLength);

        this.Log(LogLevel.Debug, "Read input: address=0x{0:X}, length={1}", sourceAddress, inputLength);
        return true;
    }

    // TODO: Implement strided output
    private bool WriteOutputBytes(byte[] bytes)
    {
        if(!TryGetBufferAddress(false, out var destinationAddress))
        {
            return false;
        }

        sysbus.WriteBytes(bytes, destinationAddress);

        this.Log(LogLevel.Debug, "Written output: address=0x{0:X}, length={1}", destinationAddress, bytes.Length);
        return true;
    }

    private (int Width, int Height, int Depth) GetTensorDimensions(bool input)
    {
        return input
            ? (commandRegisters.IfmWidth0M1 + 1, commandRegisters.IfmHeight0M1 + 1, commandRegisters.IfmDepthM1 + 1)
            : (commandRegisters.OfmWidth0M1 + 1, commandRegisters.OfmHeight0M1 + 1, commandRegisters.OfmDepthM1 + 1);
    }

    private (long X, long Y, long Channel) GetTensorStrides(bool input)
    {
        return input
            ? (commandRegisters.IfmStrideX, commandRegisters.IfmStrideY, commandRegisters.IfmStrideC)
            : (commandRegisters.OfmStrideX, commandRegisters.OfmStrideY, commandRegisters.OfmStrideC);
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

        if(!ReadInputBytes(out var input))
        {
            return false;
        }

        var outputBuffer = new byte[GetBufferLength(false)];

        var kernelXDilation = BitHelper.GetBits(commandRegisters.KernelStride)[3] ? 2 : 1;
        var kernelYDilation = BitHelper.GetBits(commandRegisters.KernelStride)[4] ? 2 : 1;
        var kernelWidth = (commandRegisters.KernelWidthM1 / kernelXDilation) + 1;
        var kernelHeight = (commandRegisters.KernelHeightM1 / kernelYDilation) + 1;
        var (kernelStrideX, kernelStrideY) = GetKernelStrides();

        var (inputWidth, inputHeight, inputDepth) = GetTensorDimensions(true);
        var (outputWidth, outputHeight, outputDepth) = GetTensorDimensions(false);

        var inputSigned = BitHelper.GetBits(commandRegisters.IfmPrecision)[0];

        var topPad = BitHelper.GetValue(commandRegisters.IfmPadTop, 0, 15);
        var leftPad = BitHelper.GetValue(commandRegisters.IfmPadLeft, 0, 15);
        var bottomPad = BitHelper.GetValue(commandRegisters.IfmPadBottom, 0, 16);
        var rightPad = BitHelper.GetValue(commandRegisters.IfmPadRight, 0, 16);
        var hasPadding = topPad != 0 || leftPad != 0 || bottomPad != 0 || rightPad != 0;

        // TODO: Validate all configuration options

        var inputBytes = GetElementByteLength(true);
        var outputBytes = GetElementByteLength(false);

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
                    // TODO: Wrap accumulation in 32 or 40 bits
                    long accumulator = identity;
                    var validCount = 0;

                    if(mode == PoolMode.ReduceSum)
                    {
                        for(var iC = 0; iC < inputDepth; iC++)
                        {
                            var raw = GetTensorValue(oY, oX, iC, true, input);
                            var value = DecodeTensorValue(raw, inputBytes, inputSigned);
                            accumulator += value;
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
                                var pad = iX < 0 || iY < 0 || iX >= inputWidth || iY >= inputHeight;

                                // Don't include padding in calculations
                                if(pad)
                                {
                                    continue;
                                }
                                validCount++;

                                var raw = GetTensorValue(iY, iX, iC, true, input);
                                var value = DecodeTensorValue(raw, inputBytes, inputSigned);
                                if(mode == PoolMode.Max)
                                {
                                    accumulator = Math.Max(accumulator, value);
                                }
                                else
                                {
                                    accumulator += value;
                                }
                            }
                        }
                    }

                    var outputByteAddress = GetTensorAddress(oY, oX, oD, false);

                    var rescaleMode = mode switch
                    {
                        PoolMode.Max => RescaleMode.None,
                        PoolMode.Average => hasPadding ? RescaleMode.AveragePool : RescaleMode.Normal,
                        PoolMode.ReduceSum => RescaleMode.ShiftOnly,
                        _ => throw new UnreachableException()
                    };

                    if(!WriteAccumulator(outputBuffer, oY, oX, oD, accumulator, rescaleMode, validCount))
                    {
                        return false;
                    }
                }
            }
        }

        return WriteOutputBytes(outputBuffer);
    }

    enum PoolMode
    {
        Max,
        Average,
        ReduceSum,
    };

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
