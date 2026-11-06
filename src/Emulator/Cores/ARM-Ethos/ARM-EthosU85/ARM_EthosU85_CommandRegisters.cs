//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
    private static ulong Extend40(ushort parameter, uint payload)
    {
        return ((ulong)(parameter & 0xFF) << 32) | payload;
    }

    private static long SignExtend40(ushort parameter, uint payload)
    {
        return unchecked((long)BitHelper.SignExtend(Extend40(parameter, payload), 40));
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
        case CommandOpcode.SetIfmBroadcast:
            commandRegisters.IfmBroadcast = parameter;
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
        case CommandOpcode.SetResizeXScaleNM1:
            commandRegisters.ResizeXScaleNM1 = parameter;
            break;
        case CommandOpcode.SetResizeYScaleNM1:
            commandRegisters.ResizeYScaleNM1 = parameter;
            break;
        case CommandOpcode.SetResizeXOffset:
            commandRegisters.ResizeXOffset = parameter;
            break;
        case CommandOpcode.SetResizeYOffset:
            commandRegisters.ResizeYOffset = parameter;
            break;
        case CommandOpcode.SetWeightFormat:
            commandRegisters.WeightFormat = parameter;
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
        case CommandOpcode.SetDma0Size0:
            commandRegisters.Dma0Size0 = parameter;
            break;
        case CommandOpcode.SetDma0Size1:
            commandRegisters.Dma0Size1 = parameter;
            break;
        case CommandOpcode.SetDma0IdxRegion:
            commandRegisters.Dma0IdxRegion = parameter;
            break;
        case CommandOpcode.SetIfm2Broadcast:
            commandRegisters.Ifm2Broadcast = parameter;
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
        case CommandOpcode.SetIfm2Region:
            commandRegisters.Ifm2Region = parameter;
            break;
        default:
            return false;
        }

        this.DebugLog("{0} at +0x{1:X}: parameter 0x{2:X4}", opcode, queueReadOffset.Value, parameter);
        return true;
    }

    private bool SetPayloadRegister(PayloadCommandOpcode opcode, uint payload, ushort parameter)
    {
        switch(opcode)
        {
        case PayloadCommandOpcode.SetIfmBase0:
            commandRegisters.IfmBase0 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase1:
            commandRegisters.IfmBase1 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase2:
            commandRegisters.IfmBase2 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmBase3:
            commandRegisters.IfmBase3 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideX:
            commandRegisters.IfmStrideX = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideY:
            commandRegisters.IfmStrideY = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfmStrideC:
            commandRegisters.IfmStrideC = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase0:
            commandRegisters.OfmBase0 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase1:
            commandRegisters.OfmBase1 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase2:
            commandRegisters.OfmBase2 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmBase3:
            commandRegisters.OfmBase3 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideX:
            commandRegisters.OfmStrideX = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideY:
            commandRegisters.OfmStrideY = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetOfmStrideC:
            commandRegisters.OfmStrideC = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeightBase:
            commandRegisters.WeightBase = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeightLength:
            commandRegisters.WeightLength = payload;
            break;
        case PayloadCommandOpcode.SetScaleBase:
            commandRegisters.ScaleBase = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetScaleLength:
            commandRegisters.ScaleLength = payload & 0xFFFFF; // length is bits 51:32 (20 bits)
            break;
        case PayloadCommandOpcode.SetOfmScale:
            commandRegisters.OfmScale = payload & 0x7FFFFFFF; // scale is bits 62:32
            commandRegisters.OfmScaleShift = (byte)(parameter & 0x3F); // bits 21:16
            commandRegisters.OfmScaleDoubleRound = (byte)((parameter >> 6) & 0x1F); // bits 26:22
            commandRegisters.OfmScaleRoundMode = (byte)((parameter >> 13) & 0x7); // bits 31:29
            break;
        case PayloadCommandOpcode.SetIfmScale:
            commandRegisters.IfmScale = payload & 0x7FFFFFFF;
            commandRegisters.IfmScaleShift = (byte)(parameter & 0x3F);
            commandRegisters.IfmScaleDoubleRound = (byte)((parameter >> 6) & 0x1F);
            commandRegisters.IfmScaleRoundMode = (byte)((parameter >> 13) & 0x1); // bit 29
            break;
        case PayloadCommandOpcode.SetIfm2Scale:
            commandRegisters.Ifm2Scale = payload & 0x7FFFFFFF;
            commandRegisters.Ifm2ScaleShift = (byte)(parameter & 0x3F);
            commandRegisters.Ifm2ScaleDoubleRound = (byte)((parameter >> 6) & 0x1F);
            commandRegisters.Ifm2ScaleRoundMode = (byte)((parameter >> 13) & 0x1);
            break;
        case PayloadCommandOpcode.SetOpScalar:
            commandRegisters.OpScalar = unchecked((int)payload); // signed 32-bit
            break;
        case PayloadCommandOpcode.SetDma0Src:
            commandRegisters.Dma0Src = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Dst:
            commandRegisters.Dma0Dst = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Len:
            commandRegisters.Dma0Len = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0SrcStride0:
            commandRegisters.Dma0SrcStride0 = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0SrcStride1:
            commandRegisters.Dma0SrcStride1 = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0DstStride0:
            commandRegisters.Dma0DstStride0 = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0DstStride1:
            commandRegisters.Dma0DstStride1 = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0Idx:
            commandRegisters.Dma0Idx = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetDma0IdxMax:
            commandRegisters.Dma0IdxMax = payload & 0x7FFFFFFF; // idx_max is bits 62:32
            break;
        case PayloadCommandOpcode.SetDma0IdxSkip1:
            commandRegisters.Dma0IdxSkip1 = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base0:
            commandRegisters.Ifm2Base0 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base1:
            commandRegisters.Ifm2Base1 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base2:
            commandRegisters.Ifm2Base2 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2Base3:
            commandRegisters.Ifm2Base3 = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideX:
            commandRegisters.Ifm2StrideX = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideY:
            commandRegisters.Ifm2StrideY = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetIfm2StrideC:
            commandRegisters.Ifm2StrideC = SignExtend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight1Base:
            commandRegisters.Weight1Base = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight1Length:
            commandRegisters.Weight1Length = payload;
            break;
        case PayloadCommandOpcode.SetWeight2Base:
            commandRegisters.Weight2Base = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight2Length:
            commandRegisters.Weight2Length = payload;
            break;
        case PayloadCommandOpcode.SetWeight3Base:
            commandRegisters.Weight3Base = Extend40(parameter, payload);
            break;
        case PayloadCommandOpcode.SetWeight3Length:
            commandRegisters.Weight3Length = payload;
            break;
        case PayloadCommandOpcode.SetResizeXStep:
            commandRegisters.ResizeXOneStepInt = (byte)(parameter & 0xF); // bits 19:16
            commandRegisters.ResizeXBlockStepInt = (ushort)((parameter >> 4) & 0x7FF); // bits 30:20
            commandRegisters.ResizeXOneStepMod = (ushort)(payload & 0x7FF); // bits 42:32
            commandRegisters.ResizeXBlockStepMod = (ushort)((payload >> 16) & 0x7FF); // bits 58:48
            break;
        case PayloadCommandOpcode.SetResizeYStep:
            commandRegisters.ResizeYOneStepInt = (byte)(parameter & 0xF);
            commandRegisters.ResizeYBlockStepInt = (ushort)((parameter >> 4) & 0x7FF);
            commandRegisters.ResizeYOneStepMod = (ushort)(payload & 0x7FF);
            commandRegisters.ResizeYBlockStepMod = (ushort)((payload >> 16) & 0x7FF);
            break;
        default:
            return false;
        }

        this.DebugLog("{0} at +0x{1:X}: parameter 0x{2:X4}, payload 0x{3:X8}", opcode, queueReadOffset.Value, parameter, payload);
        return true;
    }

    private CommandRegisterState commandRegisters = new CommandRegisterState();

    private sealed class CommandRegisterState
    {
        // One-word commands: raw 16-bit parameter, decoded when the operation runs
        public ushort IfmPadTop;
        public ushort IfmPadLeft;
        public ushort IfmPadRight;
        public ushort IfmPadBottom;
        public ushort IfmDepthM1;
        public ushort IfmPrecision;
        public ushort IfmUpscale;
        public ushort IfmBroadcast;
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
        public ushort ResizeXScaleNM1;
        public ushort ResizeYScaleNM1;
        public ushort ResizeXOffset;
        public ushort ResizeYOffset;
        public ushort WeightFormat;
        public ushort Blockdep;
        public ushort Dma0SrcRegion;
        public ushort Dma0DstRegion;
        public ushort Dma0Size0;
        public ushort Dma0Size1;
        public ushort Dma0IdxRegion;
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
        public byte OfmScaleDoubleRound;
        public byte OfmScaleRoundMode;
        public uint IfmScale;
        public byte IfmScaleShift;
        public byte IfmScaleDoubleRound;
        public byte IfmScaleRoundMode;
        public uint Ifm2Scale;
        public byte Ifm2ScaleShift;
        public byte Ifm2ScaleDoubleRound;
        public byte Ifm2ScaleRoundMode;
        public int OpScalar;
        public ulong Dma0Src;
        public ulong Dma0Dst;
        public ulong Dma0Len;
        public long Dma0SrcStride0;
        public long Dma0SrcStride1;
        public long Dma0DstStride0;
        public long Dma0DstStride1;
        public ulong Dma0Idx;
        public uint Dma0IdxMax;
        public long Dma0IdxSkip1;
        public ulong Ifm2Base0;
        public ulong Ifm2Base1;
        public ulong Ifm2Base2;
        public ulong Ifm2Base3;
        public long Ifm2StrideX;
        public long Ifm2StrideY;
        public long Ifm2StrideC;
        public ulong Weight1Base;
        public uint Weight1Length;
        public ulong Weight2Base;
        public uint Weight2Length;
        public ulong Weight3Base;
        public uint Weight3Length;
        public byte ResizeXOneStepInt;
        public ushort ResizeXBlockStepInt;
        public ushort ResizeXOneStepMod;
        public ushort ResizeXBlockStepMod;
        public byte ResizeYOneStepInt;
        public ushort ResizeYBlockStepInt;
        public ushort ResizeYOneStepMod;
        public ushort ResizeYBlockStepMod;
    }

    private enum CommandOpcode : ushort
    {
        OpStop = 0x000, // OP_STOP
        OpIrq = 0x001, // OP_IRQ
        OpConv = 0x002, // OP_CONV
        OpDepthwise = 0x003, // OP_DEPTHWISE
        OpPool = 0x005, // OP_POOL
        OpElementwise = 0x006, // OP_ELEMENTWISE
        OpResize = 0x007, // OP_RESIZE
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
        SetIfmBroadcast = 0x108, // SET_IFM_BROADCAST
        SetIfmZeroPoint = 0x109, // SET_IFM_ZERO_POINT
        SetIfmWidth0M1 = 0x10A, // SET_IFM_WIDTH0_M1
        SetIfmHeight0M1 = 0x10B, // SET_IFM_HEIGHT0_M1
        SetIfmHeight1M1 = 0x10C, // SET_IFM_HEIGHT1_M1
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
        SetAccFormat = 0x124, // SET_ACC_FORMAT
        SetActivation = 0x125, // SET_ACTIVATION
        SetActivationMin = 0x126, // SET_ACTIVATION_MIN
        SetActivationMax = 0x127, // SET_ACTIVATION_MAX
        SetWeightRegion = 0x128, // SET_WEIGHT_REGION
        SetScaleRegion = 0x129, // SET_SCALE_REGION
        SetResizeXScaleNM1 = 0x12A, // SET_RESIZE_X_SCALE_N_M1
        SetResizeYScaleNM1 = 0x12B, // SET_RESIZE_Y_SCALE_N_M1
        SetResizeXOffset = 0x12C, // SET_RESIZE_X_OFFSET
        SetResizeYOffset = 0x12D, // SET_RESIZE_Y_OFFSET
        SetWeightFormat = 0x12E, // SET_WEIGHT_FORMAT
        SetBlockdep = 0x12F, // SET_BLOCKDEP
        SetDma0SrcRegion = 0x130, // SET_DMA0_SRC_REGION
        SetDma0DstRegion = 0x131, // SET_DMA0_DST_REGION
        SetDma0Size0 = 0x132, // SET_DMA0_SIZE0
        SetDma0Size1 = 0x133, // SET_DMA0_SIZE1
        SetDma0IdxRegion = 0x134, // SET_DMA0_IDX_REGION
        SetIfm2Broadcast = 0x180, // SET_IFM2_BROADCAST
        SetIfm2Precision = 0x185, // SET_IFM2_PRECISION
        SetIfm2ZeroPoint = 0x189, // SET_IFM2_ZERO_POINT
        SetIfm2Width0M1 = 0x18A, // SET_IFM2_WIDTH0_M1
        SetIfm2Height0M1 = 0x18B, // SET_IFM2_HEIGHT0_M1
        SetIfm2Height1M1 = 0x18C, // SET_IFM2_HEIGHT1_M1
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
        SetIfmScale = 0x025, // SET_IFM_SCALE
        SetIfm2Scale = 0x026, // SET_IFM2_SCALE
        SetOpScalar = 0x027, // SET_OP_SCALAR
        SetDma0Src = 0x030, // SET_DMA0_SRC
        SetDma0Dst = 0x031, // SET_DMA0_DST
        SetDma0Len = 0x032, // SET_DMA0_LEN
        SetDma0SrcStride0 = 0x033, // SET_DMA0_SRC_STRIDE0
        SetDma0SrcStride1 = 0x034, // SET_DMA0_SRC_STRIDE1
        SetDma0DstStride0 = 0x035, // SET_DMA0_DST_STRIDE0
        SetDma0DstStride1 = 0x036, // SET_DMA0_DST_STRIDE1
        SetDma0Idx = 0x037, // SET_DMA0_IDX
        SetDma0IdxMax = 0x038, // SET_DMA0_IDX_MAX
        SetDma0IdxSkip1 = 0x039, // SET_DMA0_IDX_SKIP1
        SetIfm2Base0 = 0x080, // SET_IFM2_BASE0
        SetIfm2Base1 = 0x081, // SET_IFM2_BASE1
        SetIfm2Base2 = 0x082, // SET_IFM2_BASE2
        SetIfm2Base3 = 0x083, // SET_IFM2_BASE3
        SetIfm2StrideX = 0x084, // SET_IFM2_STRIDE_X
        SetIfm2StrideY = 0x085, // SET_IFM2_STRIDE_Y
        SetIfm2StrideC = 0x086, // SET_IFM2_STRIDE_C
        SetWeight1Base = 0x090, // SET_WEIGHT1_BASE
        SetWeight1Length = 0x091, // SET_WEIGHT1_LENGTH
        SetWeight2Base = 0x092, // SET_WEIGHT2_BASE
        SetWeight2Length = 0x093, // SET_WEIGHT2_LENGTH
        SetWeight3Base = 0x094, // SET_WEIGHT3_BASE
        SetWeight3Length = 0x095, // SET_WEIGHT3_LENGTH
        SetResizeXStep = 0x096, // SET_RESIZE_X_STEP
        SetResizeYStep = 0x097, // SET_RESIZE_Y_STEP
        OpBranch = 0x100, // OP_BRANCH
    }
}
