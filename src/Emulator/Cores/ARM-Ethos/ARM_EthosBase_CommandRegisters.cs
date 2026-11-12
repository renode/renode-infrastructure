//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public abstract partial class ARM_EthosBase
{
    // Combine a CMD1 header's upper address bits (parameter) with its 32-bit payload into an
    // address of the given width: 40 bits on the U85 (addr_hi in bits 23:16), 48 on the U55.
    protected static ulong ExtendAddress(ushort parameter, uint payload, int bits)
    {
        var upperMask = (1UL << (bits - 32)) - 1;
        return ((parameter & upperMask) << 32) | payload;
    }

    protected static long SignExtendAddress(ushort parameter, uint payload, int bits)
    {
        return unchecked((long)BitHelper.SignExtend(ExtendAddress(parameter, payload, bits), bits));
    }

    protected abstract SharedCommandRegisterState CommonRegisters { get; }

    protected class SharedCommandRegisterState
    {
        // One-word commands: raw 16-bit parameter, decoded when the operation runs
        public ushort IfmPadTop;
        public ushort IfmPadLeft;
        public ushort IfmPadRight;
        public ushort IfmPadBottom;
        public ushort IfmDepthM1;
        public ushort IfmPrecision;
        public ushort IfmUpscale;
        public ushort IfmZeroPoint;
        public ushort IfmWidth0M1;
        public ushort IfmHeight0M1;
        public ushort IfmHeight1M1;
        public ushort IfmRegion;
        public ushort OfmWidthM1;
        public ushort OfmHeightM1;
        public ushort OfmDepthM1;
        public ushort OfmPrecision;
        public ushort OfmBlkWidthM1;
        public ushort OfmBlkHeightM1;
        public ushort OfmBlkDepthM1;
        public ushort OfmZeroPoint;
        public ushort OfmWidth0M1;
        public ushort OfmHeight0M1;
        public ushort OfmHeight1M1;
        public ushort OfmRegion;
        public ushort KernelWidthM1;
        public ushort KernelHeightM1;
        public ushort KernelStride;
        public ushort AccFormat;
        public ushort Activation;
        public ushort ActivationMin;
        public ushort ActivationMax;
        public ushort WeightRegion;
        public ushort ScaleRegion;
        public ushort Blockdep;
        public ushort Dma0SrcRegion;
        public ushort Dma0DstRegion;
        public ushort Ifm2Broadcast;
        public ushort Ifm2Precision;
        public ushort Ifm2ZeroPoint;
        public ushort Ifm2Width0M1;
        public ushort Ifm2Height0M1;
        public ushort Ifm2Height1M1;
        public ushort Ifm2Region;

        // Two-word commands
        public ulong IfmBase0;
        public ulong IfmBase1;
        public ulong IfmBase2;
        public ulong IfmBase3;
        public long IfmStrideX;
        public long IfmStrideY;
        public long IfmStrideC;
        public ulong OfmBase0;
        public ulong OfmBase1;
        public ulong OfmBase2;
        public ulong OfmBase3;
        public long OfmStrideX;
        public long OfmStrideY;
        public long OfmStrideC;
        public ulong WeightBase;
        public uint WeightLength;
        public ulong ScaleBase;
        public uint ScaleLength;
        public uint OfmScale;
        public byte OfmScaleShift;
        public ulong Dma0Src;
        public ulong Dma0Dst;
        public ulong Dma0Len;
        public ulong Ifm2Base0;
        public ulong Ifm2Base1;
        public ulong Ifm2Base2;
        public ulong Ifm2Base3;
        public long Ifm2StrideX;
        public long Ifm2StrideY;
        public long Ifm2StrideC;
        public ulong Weight1Base;
        public uint Weight1Length;
    }
}
