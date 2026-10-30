//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU55
{
    // Section for weight stream parsing

    private bool TryUpdateWeights()
    {
        var region = commandRegisters.WeightRegion;
        var baseAddress = commandRegisters.WeightBase;
        var length = commandRegisters.WeightLength;

        if(!TryGetBufferAddress(commandRegisters.WeightRegion, commandRegisters.WeightBase, out var address, "weight", commandRegisters.WeightLength))
        {
            return false;
        }

        var encodedBuffer = sysbus.ReadBytes(address, (int)length);
        if(!TryParseWeightStream(new BitStreamReader(encodedBuffer), out var weightStream))
        {
            this.ErrorLog("Weight stream decoding aborted");
            return false;
        }

        if(!TryGenerateWeightTensor(weightStream, out weights))
        {
            this.ErrorLog("Weight stream to weight tensor conversion aborted");
            return false;
        }

        return true;
    }

    private bool TryParseWeightStream(BitStreamReader encodedStream, out List<int> weightStream)
    {
        weightStream = new List<int>();

        // U85 TODO: Implement support for fast weight decoding
        var endOfStream = false;
        var palette = new List<uint>();
        var paletteSize = 0u;
        var paletteBits = 0u;
        var directModeOffset = 0u;
        try
        {
            while(!endOfStream)
            {
                // The zDiv symbol specifies the coding mode:
                // 0-3 selects alternating mode with zero-run compression using a GRC
                // divisor of (1 << zDiv)
                // 6 selects that zero-run compression is not used
                // 7 specifies end of stream
                // other values are reserved
                var zDiv = encodedStream.ReadAsByte(3);
                if(zDiv == 7)
                {
                    // align to 128 bits using padding with '1' (we don't check if padding is done with '1')
                    encodedStream.AlignTo(128);
                    endOfStream = true;
                }
                else if(zDiv < 4 || zDiv == 6)
                {
                    var sliceLength = encodedStream.ReadAsUInt32(15) + 1u;
                    var wDiv = encodedStream.ReadAsByte(3);
                    if(wDiv == 6)
                    {
                        this.ErrorLog("Received invalid wDiv value: {0}", wDiv);
                        return false;
                    }
                    var wTruncate = encodedStream.ReadAsBool();
                    var newPalette = encodedStream.ReadAsBool();

                    if(newPalette)
                    {
                        directModeOffset = encodedStream.ReadAsByte(5);
                        paletteSize = encodedStream.ReadAsByte(5);
                        paletteSize = (paletteSize == 0) ? 0 : paletteSize + 1u;
                        paletteBits = encodedStream.ReadAsByte(3) + 2u;
                        palette.Clear();
                        for(var i = 0; i < paletteSize; i++)
                        {
                            palette.Add(encodedStream.ReadAsUInt32(paletteBits));
                        }
                    }
                    else if(weightStream.Count == 0)
                    {
                        this.ErrorLog("Newpal must be set for the first slice");
                        return false;
                    }

                    var nonZeroWeightCount = 0;

                    while(nonZeroWeightCount < sliceLength)
                    {
                        if(!TryParseWeightChunk(encodedStream, zDiv, wDiv, paletteSize, paletteBits, sliceLength, newPalette, wTruncate, out var weightIndices, out var zRuns))
                        {
                            return false;
                        }

                        if(zDiv >= 4)
                        {
                            if(zRuns.Count > 0)
                            {
                                this.ErrorLog("Weights chunk got decoded in non alternating mode, yet zRuns got populated");
                                return false;
                            }
                        }
                        else
                        {
                            if(zRuns.Count == 0)
                            {
                                this.ErrorLog("Weights chunk got decoded in alternating mode, yet zRuns didn't get populated");
                                return false;
                            }
                        }

                        if(zRuns.Count > 0 && newPalette)
                        {
                            weightStream.AddRange(Enumerable.Repeat(0, (int)zRuns[0]));
                        }

                        for(var i = 0; i < weightIndices.Count; i++)
                        {
                            var weightIndex = weightIndices[i];
                            var encodedWeightValue = (ulong)0;
                            if(weightIndex < paletteSize)
                            {
                                encodedWeightValue = palette[(int)weightIndex];
                            }
                            else
                            {
                                encodedWeightValue = weightIndex - paletteSize + directModeOffset;
                            }
                            var weightValue = (encodedWeightValue & 1) == 1 ? -(int)(encodedWeightValue >> 1) : (int)(encodedWeightValue >> 1);
                            weightStream.Add(weightValue);
                            if(zRuns.Count > 0)
                            {
                                weightStream.AddRange(Enumerable.Repeat(0, (int)zRuns[i + (newPalette ? 1 : 0)]));
                            }
                        }

                        nonZeroWeightCount += weightIndices.Count;
                    }
                }
                else
                {
                    this.ErrorLog("Received invalid zDiv {0}", zDiv);
                    return false;
                }
            }
        }
        catch(ArgumentOutOfRangeException e)
        {
            this.ErrorLog("Ran out of buffer while reading stream: {0}", e.Message);
            return false;
        }
        return true;
    }

    private bool TryParseWeightChunk(BitStreamReader encodedStream, uint zDiv, uint wDiv, uint paletteSize, uint paletteBits, uint sliceLength, bool newPalette, bool wTruncate, out List<uint> weightIndices, out List<uint> zRuns)
    {
        var wCnt = sliceLength;
        var zCnt = sliceLength + (newPalette ? 1 : 0);

        weightIndices = new List<uint>((int)wCnt);
        zRuns = new List<uint>((int)zCnt);

        var zUnaryLength = (zDiv < 3) ? 12u : 8u;
        var isAlternatingMode = (zDiv < 4);
        var isUncompressedMode = (wDiv == 7);
        var uncompressedBits = paletteSize > 0 ? (uint)Math.Ceiling(Math.Log2(paletteSize)) : paletteBits;
        var wRemainderBits = isUncompressedMode ? uncompressedBits : wDiv;
        var uncompressedPerChunk = (uncompressedBits <= 5) ? 12 : 8;

        var wq = 0u;
        var wqI = 0;
        var wrI = 0;
        var zq = 0u;
        var zqI = 0;
        var zrI = 0;
        var previousWEnabled = false;
        var previousZEnabled = false;
        var previousWqI = 0;
        var previousZqI = 0;
        var wUnary0 = 0u;

        do
        {
            // In alternating mode, balance ensures the rate of weight indices and zrun values are kept about the same.
            var balance = wqI - zqI;
            var wEnabled = (balance < 8 || !isAlternatingMode) && wqI < wCnt;
            var zEnabled = balance >= 0 && isAlternatingMode && zqI < zCnt;

            if(wEnabled && !isUncompressedMode)
            {
                wUnary0 = encodedStream.ReadAsUInt32(12);
            }

            if(zEnabled)
            {
                var zUnary = encodedStream.ReadAsUInt32(zUnaryLength);
                for(var i = 0; i < zUnaryLength; i++) // in pseudocde the halting value is set to zunary_bits which aren't defined earlier, Vela uses z_unary_len in this case
                {
                    if(((zUnary >> i) & 1) == 1)
                    {
                        zq++;
                    }
                    else
                    {
                        // No need to append values above zCnt as they will be dropped anyway
                        if(zqI < zCnt)
                        {
                            // zRuns[zqI++] = zq << (int)zDiv;
                            zRuns.Add(zq << (int)zDiv);
                        }
                        zqI++;
                        zq = 0;
                    }
                }
            }

            if(wEnabled && !isUncompressedMode)
            {
                var wUnary1Len = 0u;
                for(var i = 0; i < 12; i++)
                {
                    if(((wUnary0 >> i) & 1) == 1)
                    {
                        wUnary1Len++;
                    }
                }
                var wUnary1 = encodedStream.ReadAsUInt32(wUnary1Len);
                for(int i = 0, j = 0; i < 12 && wqI < wCnt; i++)
                {
                    var c = 0u;
                    if(((wUnary0 >> i) & 1) == 1)
                    {
                        c = 1 + ((wUnary1 >> j) & 1);
                        j++;
                    }
                    wq += c;
                    if((c < 2) || wTruncate)
                    {
                        if(wq >= 32)
                        {
                            this.ErrorLog("Invalid weight chunk decode, wq larger than 32");
                            return false;
                        }
                        // weight_indices[wqI++] = wq << (int)wDiv;
                        weightIndices.Add(wq << (int)wDiv);
                        wqI++;
                        wq = 0;
                    }
                }
            }

            if(wEnabled && isUncompressedMode)
            {
                for(var i = 0; i < uncompressedPerChunk && wqI < wCnt; i++)
                {
                    // weight_indices[wqI++] = 0;
                    weightIndices.Add(0);
                    wqI++;
                }
            }

            // Remainders corresponding to the quotients in the previous chunk
            if(previousWEnabled)
            {
                while(wrI < previousWqI && wrI < wCnt)
                {
                    weightIndices[wrI++] += encodedStream.ReadAsUInt32(wRemainderBits);
                }
            }

            if(previousZEnabled)
            {
                while(zrI < previousZqI && zrI < zCnt) // zrI < zCnt is optimization used by Vela, without it we run out of buts in the encoded stream
                {
                    zRuns[zrI++] += encodedStream.ReadAsUInt32(zDiv);
                }
            }
            previousWEnabled = wEnabled;
            previousWqI = wqI;
            previousZEnabled = zEnabled;
            previousZqI = zqI;
        } while(previousWEnabled || previousZEnabled);

        return true;
    }

    private bool TryGenerateWeightTensor(List<int> weightStream, out int[,,,] weightTensor)
    {
        var isPartkernel = BitHelper.GetValue(commandRegisters.KernelStride, 2, 1) == 1;

        var dilatedKernelHeightM1 = commandRegisters.KernelHeightM1;
        var dilatedKernelWidthM1 = commandRegisters.KernelWidthM1;

        var kernelYDilationM1 = BitHelper.GetValue(commandRegisters.KernelStride, 4, 1);
        var kernelXDilationM1 = BitHelper.GetValue(commandRegisters.KernelStride, 3, 1);

        // Undilate the height and width
        var kernelHeight = (dilatedKernelHeightM1 + (1 << kernelYDilationM1))  >> kernelYDilationM1;
        var kernelWidth = (dilatedKernelWidthM1 + (1 << kernelXDilationM1))  >> kernelXDilationM1;

        var outputHeight = commandRegisters.OfmHeightM1 + 1;
        var outputWidth = commandRegisters.OfmWidthM1 + 1;
        var outputDepth = commandRegisters.OfmDepthM1 + 1;

        var inputDepth = commandRegisters.IfmDepthM1 + 1;

        var outputBlockDepth = commandRegisters.OfmBlkDepthM1 + 1;
        var outputBlockWidth = commandRegisters.OfmBlkWidthM1 + 1;
        var outputBlockHeight = commandRegisters.OfmBlkHeightM1 + 1;

        var decomposition = BitHelper.GetValue(commandRegisters.KernelStride,5,1) == 1 ? 4 : 8;

        var decompositionWidth = decomposition >> kernelXDilationM1;
        var decompositionHeight = decomposition >> kernelYDilationM1;

        weightTensor = new int[outputDepth, kernelHeight, kernelWidth, inputDepth];

        var inputBitDepth = (int)BitHelper.GetValue(commandRegisters.IfmPrecision, 2, 2);
        if(inputBitDepth == 2)
        {
            this.ErrorLog("32 bit precision is unsupported for weight stream");
            return false;
        }

        inputBitDepth = inputBitDepth == 0 ? 8 : 16;

        var inputBlockDepth = (isPartkernel || inputBitDepth == 16) ? 16 : 32;

        const int inputMicroblockDepth = 8;
        const int outputMicroblockDepth = 8; // Always 8 for macs 128 and 256

        var weightStreamIndex = 0;

        for(var outputBlockZ = 0; outputBlockZ < outputDepth; outputBlockZ += outputBlockDepth)
        {
            for(var inputBlockZ = 0; inputBlockZ < inputDepth; inputBlockZ += inputBlockDepth)
            {
                // Don't trust the docs, it's Y first, X second just like in Vela compiler code: https://github.com/nxp-imx/ethos-u-vela/blob/lf-6.18.20_2.0.0/ethosu/mlw_codec/mlw_encode.c#L1024
                for(var kernelY = 0; kernelY < kernelHeight; kernelY += decompositionHeight)
                {
                    var subKernelHeight = Math.Min(kernelHeight - kernelY, decompositionHeight);
                    for(var kernelX = 0; kernelX < kernelWidth; kernelX += decompositionWidth)
                    {
                        var subKernelWidth = Math.Min(kernelWidth - kernelX, decompositionWidth);
                        var subKernelSize = subKernelWidth * subKernelHeight;

                        if(isPartkernel)
                        {
                            if(inputBitDepth == 16)
                            {
                                subKernelSize = ((subKernelSize + 1) / 2) * 2;
                            }
                            if(inputBitDepth == 8)
                            {
                                subKernelSize = ((subKernelSize + 3) / 4) * 4;
                            }
                        }

                        var subInputDepth = Math.Min(16, inputDepth - inputBlockZ);
                        // Use this loop only in partkernel scenario
                        for(var subInputZPartkernel = isPartkernel ? 0 : subInputDepth - 1; subInputZPartkernel < subInputDepth; subInputZPartkernel += inputMicroblockDepth)
                        {
                            var subOutputDepth = Math.Min(outputBlockDepth, outputDepth - outputBlockZ);
                            for(var subOutputZ = 0; subOutputZ < subOutputDepth; subOutputZ += outputMicroblockDepth)
                            {
                                for(var kernelI = 0; kernelI < subKernelSize; kernelI++)
                                {
                                    var subKernelX = kernelI % subKernelWidth;
                                    var subKernelY = kernelI / subKernelWidth;
                                    // Iterate with subInputZDepthwise only in depthfirst scenario
                                    for(var subInputZDepthfirst = isPartkernel ? inputBlockDepth - 1 : 0; subInputZDepthfirst < inputBlockDepth; subInputZDepthfirst += inputMicroblockDepth)
                                    {
                                        var subInputZ = isPartkernel ? subInputZPartkernel : subInputZDepthfirst;
                                        for(var outputZ = 0; outputZ < outputMicroblockDepth; outputZ++)
                                        {
                                            for(var inputZ = 0; inputZ < inputMicroblockDepth; inputZ++)
                                            {
                                                var kx = kernelX + subKernelX;
                                                var ky = kernelY + subKernelY;
                                                var iz = inputBlockZ + subInputZ + inputZ;
                                                var oz = outputBlockZ + subOutputZ + outputZ;
                                                if((isPartkernel && subKernelY >= subKernelHeight) ||
                                                   iz >= inputDepth ||
                                                   oz >= outputDepth)
                                                {
                                                    var check = weightStream[weightStreamIndex++];
                                                    if(check != 0)
                                                    {
                                                        this.ErrorLog("Read different value than 0 from out of bounds weight stream coordinates.");
                                                        return false;
                                                    }
                                                }
                                                else
                                                {
                                                    weightTensor[oz, ky, kx, iz] = weightStream[weightStreamIndex++];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return true;
    }

    // Section for bias, scale and shift parsing

    private bool TryUpdateBiasScaleShiftStream()
    {
        biasStream = new List<long>();
        scaleStream = new List<long>();
        shiftStream = new List<int>();

        if(!TryGetBufferAddress(commandRegisters.ScaleRegion, commandRegisters.ScaleBase, out var address, "bias, scale, shift", commandRegisters.ScaleLength))
        {
            return false;
        }

        var encodedBuffer = sysbus.ReadBytes(address, (int)commandRegisters.ScaleLength);
        if(!TryParseBiasScaleShiftStream(new BitStreamReader(encodedBuffer)))
        {
            this.ErrorLog("Bias, scale, shift stream decoding aborted");
            return false;
        }
        return true;
    }

    private bool TryParseBiasScaleShiftStream(BitStreamReader encodedStream)
    {
        // This method referenced encoding from Vela compiler as implementation for U85 is slightly different
        // U85 TODO: Implement decoding for U85
        var ocSize = commandRegisters.OfmDepthM1 + 1;
        try
        {
            for(var i = 0; i < ocSize; i++)
            {
                biasStream.Add((long)BitHelper.SignExtend(encodedStream.ReadAsUInt64(40), 40));
                scaleStream.Add((long)encodedStream.ReadAsUInt32(32));
                shiftStream.Add(encodedStream.ReadAsByte(6));
                ValidateReserved(encodedStream.ReadAsUInt64(2));
            }
        }
        catch(ArgumentOutOfRangeException e)
        {
            this.ErrorLog("Run out of buffer while reading stream: {0}", e.Message);
            return false;
        }
        return true;
    }

    private void ValidateReserved(ulong reserved)
    {
        if(reserved > 0)
        {
            this.WarningLog("Received 1 when reading reserved sequence");
        }
    }

    private List<long> biasStream;
    private List<long> scaleStream;
    private List<int> shiftStream;
    private int[,,,] weights;
}