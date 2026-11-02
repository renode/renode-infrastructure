//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU55
{
    private static ulong Extend48(ushort parameter, uint payload)
    {
        return ((ulong)parameter << 32) | payload;
    }

    private static long SignExtend48(ushort parameter, uint payload)
    {
        return unchecked((long)BitHelper.SignExtend(Extend48(parameter, payload), 48));
    }

    private bool SetRegister(CommandOpcode opcode, ushort parameter)
    {
        switch(opcode)
        {
        case CommandOpcode.SetIfmPadTop:
            commandRegisters.IfmPadTop = parameter;
            break;
        case CommandOpcode.SetIfmPadLeft:
            commandRegisters.IfmPadLeft = parameter;
            break;
        case CommandOpcode.SetIfmPadRight:
            commandRegisters.IfmPadRight = parameter;
            break;
        case CommandOpcode.SetIfmPadBottom:
            commandRegisters.IfmPadBottom = parameter;
            break;
        case CommandOpcode.SetIfmDepthM1:
            commandRegisters.IfmDepthM1 = parameter;
            break;
        case CommandOpcode.SetIfmPrecision:
            commandRegisters.IfmPrecision = parameter;
            break;
        case CommandOpcode.SetIfmUpscale:
            commandRegisters.IfmUpscale = parameter;
            break;
        case CommandOpcode.SetIfmZeroPoint:
            commandRegisters.IfmZeroPoint = parameter;
            break;
        case CommandOpcode.SetIfmWidth0M1:
            commandRegisters.IfmWidth0M1 = parameter;
            break;
        case CommandOpcode.SetIfmHeight0M1:
            commandRegisters.IfmHeight0M1 = parameter;
            break;
        case CommandOpcode.SetIfmHeight1M1:
            commandRegisters.IfmHeight1M1 = parameter;
            break;
        case CommandOpcode.SetIfmIbEnd:
            commandRegisters.IfmIbEnd = parameter;
            break;
        case CommandOpcode.SetIfmRegion:
            commandRegisters.IfmRegion = parameter;
            break;
        case CommandOpcode.SetOfmWidthM1:
            commandRegisters.OfmWidthM1 = parameter;
            break;
        case CommandOpcode.SetOfmHeightM1:
            commandRegisters.OfmHeightM1 = parameter;
            break;
        case CommandOpcode.SetOfmDepthM1:
            commandRegisters.OfmDepthM1 = parameter;
            break;
        case CommandOpcode.SetOfmPrecision:
            commandRegisters.OfmPrecision = parameter;
            break;
        case CommandOpcode.SetOfmBlkWidthM1:
            commandRegisters.OfmBlkWidthM1 = parameter;
            break;
        case CommandOpcode.SetOfmBlkHeightM1:
            commandRegisters.OfmBlkHeightM1 = parameter;
            break;
        case CommandOpcode.SetOfmBlkDepthM1:
            commandRegisters.OfmBlkDepthM1 = parameter;
            break;
        case CommandOpcode.SetOfmZeroPoint:
            commandRegisters.OfmZeroPoint = parameter;
            break;
        case CommandOpcode.SetOfmWidth0M1:
            commandRegisters.OfmWidth0M1 = parameter;
            break;
        case CommandOpcode.SetOfmHeight0M1:
            commandRegisters.OfmHeight0M1 = parameter;
            break;
        case CommandOpcode.SetOfmHeight1M1:
            commandRegisters.OfmHeight1M1 = parameter;
            break;
        case CommandOpcode.SetOfmRegion:
            commandRegisters.OfmRegion = parameter;
            break;
        case CommandOpcode.SetKernelWidthM1:
            commandRegisters.KernelWidthM1 = parameter;
            break;
        case CommandOpcode.SetKernelHeightM1:
            commandRegisters.KernelHeightM1 = parameter;
            break;
        case CommandOpcode.SetKernelStride:
            commandRegisters.KernelStride = parameter;
            break;
        case CommandOpcode.SetParallelMode:
            commandRegisters.ParallelMode = parameter;
            break;
        case CommandOpcode.SetAccFormat:
            commandRegisters.AccFormat = parameter;
            break;
        case CommandOpcode.SetActivation:
            commandRegisters.Activation = parameter;
            break;
        case CommandOpcode.SetActivationMin:
            commandRegisters.ActivationMin = parameter;
            break;
        case CommandOpcode.SetActivationMax:
            commandRegisters.ActivationMax = parameter;
            break;
        case CommandOpcode.SetWeightRegion:
            commandRegisters.WeightRegion = parameter;
            break;
        case CommandOpcode.SetScaleRegion:
            commandRegisters.ScaleRegion = parameter;
            break;
        case CommandOpcode.SetAbStart:
            commandRegisters.AbStart = parameter;
            break;
        case CommandOpcode.SetBlockdep:
            commandRegisters.Blockdep = parameter;
            break;
        case CommandOpcode.SetDma0SrcRegion:
            commandRegisters.Dma0SrcRegion = parameter;
            break;
        case CommandOpcode.SetDma0DstRegion:
            commandRegisters.Dma0DstRegion = parameter;
            break;
        case CommandOpcode.SetIfm2Broadcast:
            commandRegisters.Ifm2Broadcast = parameter;
            break;
        case CommandOpcode.SetIfm2Scalar:
            commandRegisters.Ifm2Scalar = parameter;
            break;
        case CommandOpcode.SetIfm2Precision:
            commandRegisters.Ifm2Precision = parameter;
            break;
        case CommandOpcode.SetIfm2ZeroPoint:
            commandRegisters.Ifm2ZeroPoint = parameter;
            break;
        case CommandOpcode.SetIfm2Width0M1:
            commandRegisters.Ifm2Width0M1 = parameter;
            break;
        case CommandOpcode.SetIfm2Height0M1:
            commandRegisters.Ifm2Height0M1 = parameter;
            break;
        case CommandOpcode.SetIfm2Height1M1:
            commandRegisters.Ifm2Height1M1 = parameter;
            break;
        case CommandOpcode.SetIfm2IbStart:
            commandRegisters.Ifm2IbStart = parameter;
            break;
        case CommandOpcode.SetIfm2Region:
            commandRegisters.Ifm2Region = parameter;
            break;
        default:
            return false;
        }

        this.Log(LogLevel.Debug, "{0} at +0x{1:X}: parameter 0x{2:X4}", opcode, queueReadOffset.Value, parameter);
        return true;
    }

    private bool SetPayloadRegister(PayloadCommandOpcode opcode, uint payload, ushort parameter)
    {
        switch(opcode)
        {
        case PayloadCommandOpcode.SetIfmBase0:
            commandRegisters.IfmBase0 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase1:
            commandRegisters.IfmBase1 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase2:
            commandRegisters.IfmBase2 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase3:
            commandRegisters.IfmBase3 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideX:
            commandRegisters.IfmStrideX = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideY:
            commandRegisters.IfmStrideY = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideC:
            commandRegisters.IfmStrideC = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase0:
            commandRegisters.OfmBase0 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase1:
            commandRegisters.OfmBase1 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase2:
            commandRegisters.OfmBase2 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase3:
            commandRegisters.OfmBase3 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideX:
            commandRegisters.OfmStrideX = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideY:
            commandRegisters.OfmStrideY = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideC:
            commandRegisters.OfmStrideC = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeightBase:
            commandRegisters.WeightBase = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeightLength:
            commandRegisters.WeightLength = payload;
            break;
        case PayloadCommandOpcode.SetScaleBase:
            commandRegisters.ScaleBase = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetScaleLength:
            commandRegisters.ScaleLength = payload;
            break;
        case PayloadCommandOpcode.SetOfmScale:
            commandRegisters.OfmScale = payload;
            commandRegisters.OfmScaleShift = (byte)(parameter & 0x3F);
            break;
        case PayloadCommandOpcode.SetOpaScale:
            commandRegisters.OpaScale = payload;
            commandRegisters.OpaScaleShift = (byte)(parameter & 0x3F);
            break;
        case PayloadCommandOpcode.SetOpbScale:
            commandRegisters.OpbScale = payload;
            break;
        case PayloadCommandOpcode.SetDma0Src:
            commandRegisters.Dma0Src = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Dst:
            commandRegisters.Dma0Dst = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Len:
            commandRegisters.Dma0Len = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Skip0:
            commandRegisters.Dma0Skip0 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Skip1:
            commandRegisters.Dma0Skip1 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base0:
            commandRegisters.Ifm2Base0 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base1:
            commandRegisters.Ifm2Base1 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base2:
            commandRegisters.Ifm2Base2 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base3:
            commandRegisters.Ifm2Base3 = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideX:
            commandRegisters.Ifm2StrideX = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideY:
            commandRegisters.Ifm2StrideY = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideC:
            commandRegisters.Ifm2StrideC = SignExtend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight1Base:
            commandRegisters.Weight1Base = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight1Length:
            commandRegisters.Weight1Length = payload;
            break;
        case PayloadCommandOpcode.SetScale1Base:
            commandRegisters.Scale1Base = Extend48(parameter, payload);
            break;
        case PayloadCommandOpcode.SetScale1Length:
            commandRegisters.Scale1Length = payload;
            break;
        default:
            return false;
        }

        this.Log(LogLevel.Debug, "{0} at +0x{1:X}: parameter 0x{2:X4}, payload 0x{3:X8}", opcode, queueReadOffset.Value, parameter, payload);
        return true;
    }

    private CommandRegisterState commandRegisters = new CommandRegisterState();

    private sealed class CommandRegisterState
    {
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
        public ushort IfmIbEnd;
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
        public ushort ParallelMode;
        public ushort AccFormat;
        public ushort Activation;
        public ushort ActivationMin;
        public ushort ActivationMax;
        public ushort WeightRegion;
        public ushort ScaleRegion;
        public ushort AbStart;
        public ushort Blockdep;
        public ushort Dma0SrcRegion;
        public ushort Dma0DstRegion;
        public ushort Ifm2Broadcast;
        public ushort Ifm2Scalar;
        public ushort Ifm2Precision;
        public ushort Ifm2ZeroPoint;
        public ushort Ifm2Width0M1;
        public ushort Ifm2Height0M1;
        public ushort Ifm2Height1M1;
        public ushort Ifm2IbStart;
        public ushort Ifm2Region;

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
        public uint OpaScale;
        public byte OpaScaleShift;
        public uint OpbScale;
        public ulong Dma0Src;
        public ulong Dma0Dst;
        public ulong Dma0Len;
        public ulong Dma0Skip0;
        public ulong Dma0Skip1;
        public ulong Ifm2Base0;
        public ulong Ifm2Base1;
        public ulong Ifm2Base2;
        public ulong Ifm2Base3;
        public long Ifm2StrideX;
        public long Ifm2StrideY;
        public long Ifm2StrideC;
        public ulong Weight1Base;
        public uint Weight1Length;
        public ulong Scale1Base;
        public uint Scale1Length;
    }

    private enum CommandOpcode : ushort
    {
        OpStop = 0x000, // OP_STOP
        OpIrq = 0x001, // OP_IRQ
        OpConv = 0x002, // OP_CONV
        OpDepthwise = 0x003, // OP_DEPTHWISE
        OpPool = 0x005, // OP_POOL
        OpElementwise = 0x006, // OP_ELEMENTWISE
        OpDmaStart = 0x010, // OP_DMA_START
        OpDmaWait = 0x011, // OP_DMA_WAIT
        OpKernelWait = 0x012, // OP_KERNEL_WAIT
        OpPmuMask = 0x013, // OP_PMU_MASK
        SetIfmPadTop = 0x100, // SET_IFM_PAD_TOP
        SetIfmPadLeft = 0x101, // SET_IFM_PAD_LEFT
        SetIfmPadRight = 0x102, // SET_IFM_PAD_RIGHT
        SetIfmPadBottom = 0x103, // SET_IFM_PAD_BOTTOM
        SetIfmDepthM1 = 0x104, // SET_IFM_DEPTH_M1
        SetIfmPrecision = 0x105, // SET_IFM_PRECISION
        SetIfmUpscale = 0x107, // SET_IFM_UPSCALE
        SetIfmZeroPoint = 0x109, // SET_IFM_ZERO_POINT
        SetIfmWidth0M1 = 0x10A, // SET_IFM_WIDTH0_M1
        SetIfmHeight0M1 = 0x10B, // SET_IFM_HEIGHT0_M1
        SetIfmHeight1M1 = 0x10C, // SET_IFM_HEIGHT1_M1
        SetIfmIbEnd = 0x10D, // SET_IFM_IB_END
        SetIfmRegion = 0x10F, // SET_IFM_REGION
        SetOfmWidthM1 = 0x111, // SET_OFM_WIDTH_M1
        SetOfmHeightM1 = 0x112, // SET_OFM_HEIGHT_M1
        SetOfmDepthM1 = 0x113, // SET_OFM_DEPTH_M1
        SetOfmPrecision = 0x114, // SET_OFM_PRECISION
        SetOfmBlkWidthM1 = 0x115, // SET_OFM_BLK_WIDTH_M1
        SetOfmBlkHeightM1 = 0x116, // SET_OFM_BLK_HEIGHT_M1
        SetOfmBlkDepthM1 = 0x117, // SET_OFM_BLK_DEPTH_M1
        SetOfmZeroPoint = 0x118, // SET_OFM_ZERO_POINT
        SetOfmWidth0M1 = 0x11A, // SET_OFM_WIDTH0_M1
        SetOfmHeight0M1 = 0x11B, // SET_OFM_HEIGHT0_M1
        SetOfmHeight1M1 = 0x11C, // SET_OFM_HEIGHT1_M1
        SetOfmRegion = 0x11F, // SET_OFM_REGION
        SetKernelWidthM1 = 0x120, // SET_KERNEL_WIDTH_M1
        SetKernelHeightM1 = 0x121, // SET_KERNEL_HEIGHT_M1
        SetKernelStride = 0x122, // SET_KERNEL_STRIDE
        SetParallelMode = 0x123, // SET_PARALLEL_MODE
        SetAccFormat = 0x124, // SET_ACC_FORMAT
        SetActivation = 0x125, // SET_ACTIVATION
        SetActivationMin = 0x126, // SET_ACTIVATION_MIN
        SetActivationMax = 0x127, // SET_ACTIVATION_MAX
        SetWeightRegion = 0x128, // SET_WEIGHT_REGION
        SetScaleRegion = 0x129, // SET_SCALE_REGION
        SetAbStart = 0x12D, // SET_AB_START
        SetBlockdep = 0x12F, // SET_BLOCKDEP
        SetDma0SrcRegion = 0x130, // SET_DMA0_SRC_REGION
        SetDma0DstRegion = 0x131, // SET_DMA0_DST_REGION
        SetIfm2Broadcast = 0x180, // SET_IFM2_BROADCAST
        SetIfm2Scalar = 0x181, // SET_IFM2_SCALAR
        SetIfm2Precision = 0x185, // SET_IFM2_PRECISION
        SetIfm2ZeroPoint = 0x189, // SET_IFM2_ZERO_POINT
        SetIfm2Width0M1 = 0x18A, // SET_IFM2_WIDTH0_M1
        SetIfm2Height0M1 = 0x18B, // SET_IFM2_HEIGHT0_M1
        SetIfm2Height1M1 = 0x18C, // SET_IFM2_HEIGHT1_M1
        SetIfm2IbStart = 0x18D, // SET_IFM2_IB_START
        SetIfm2Region = 0x18F, // SET_IFM2_REGION
    }

    private enum PayloadCommandOpcode : ushort
    {
        SetIfmBase0 = 0x000, // SET_IFM_BASE0
        SetIfmBase1 = 0x001, // SET_IFM_BASE1
        SetIfmBase2 = 0x002, // SET_IFM_BASE2
        SetIfmBase3 = 0x003, // SET_IFM_BASE3
        SetIfmStrideX = 0x004, // SET_IFM_STRIDE_X
        SetIfmStrideY = 0x005, // SET_IFM_STRIDE_Y
        SetIfmStrideC = 0x006, // SET_IFM_STRIDE_C
        SetOfmBase0 = 0x010, // SET_OFM_BASE0
        SetOfmBase1 = 0x011, // SET_OFM_BASE1
        SetOfmBase2 = 0x012, // SET_OFM_BASE2
        SetOfmBase3 = 0x013, // SET_OFM_BASE3
        SetOfmStrideX = 0x014, // SET_OFM_STRIDE_X
        SetOfmStrideY = 0x015, // SET_OFM_STRIDE_Y
        SetOfmStrideC = 0x016, // SET_OFM_STRIDE_C
        SetWeightBase = 0x020, // SET_WEIGHT_BASE
        SetWeightLength = 0x021, // SET_WEIGHT_LENGTH
        SetScaleBase = 0x022, // SET_SCALE_BASE
        SetScaleLength = 0x023, // SET_SCALE_LENGTH
        SetOfmScale = 0x024, // SET_OFM_SCALE
        SetOpaScale = 0x025, // SET_OPA_SCALE
        SetOpbScale = 0x026, // SET_OPB_SCALE
        SetDma0Src = 0x030, // SET_DMA0_SRC
        SetDma0Dst = 0x031, // SET_DMA0_DST
        SetDma0Len = 0x032, // SET_DMA0_LEN
        SetDma0Skip0 = 0x033, // SET_DMA0_SKIP0
        SetDma0Skip1 = 0x034, // SET_DMA0_SKIP1
        SetIfm2Base0 = 0x080, // SET_IFM2_BASE0
        SetIfm2Base1 = 0x081, // SET_IFM2_BASE1
        SetIfm2Base2 = 0x082, // SET_IFM2_BASE2
        SetIfm2Base3 = 0x083, // SET_IFM2_BASE3
        SetIfm2StrideX = 0x084, // SET_IFM2_STRIDE_X
        SetIfm2StrideY = 0x085, // SET_IFM2_STRIDE_Y
        SetIfm2StrideC = 0x086, // SET_IFM2_STRIDE_C
        SetWeight1Base = 0x090, // SET_WEIGHT1_BASE
        SetWeight1Length = 0x091, // SET_WEIGHT1_LENGTH
        SetScale1Base = 0x092, // SET_SCALE1_BASE
        SetScale1Length = 0x093, // SET_SCALE1_LENGTH
    }
}
