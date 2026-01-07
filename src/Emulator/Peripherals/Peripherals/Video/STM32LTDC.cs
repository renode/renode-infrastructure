//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Video
{
    public class STM32LTDC : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public STM32LTDC(IMachine machine) : base(machine)
        {
            Reconfigure(format: PixelFormat.RGBX8888);

            IRQ = new GPIO();
            ErrorIRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            interruptManager = new InterruptManager<Events>(this);

            sysbus = machine.GetSystemBus(this);
            internalLock = new object();
            localLayerBuffer = new byte[2][];

            // We don't care about the sync, back porch, or front porch pixels, but since the registers specify sums of pixel areas, we need the back porch sum and active sum registers to find the width and height. We're providing the other registers to silence warnings.
            Register.SyncronizationSizeConfigurationRegister.Define(this)
                .WithValueField(0, 10, name: "VSH")
                .WithReservedBits(11, 5)
                .WithValueField(16, 11, name: "HSW")
                .WithReservedBits(28, 4);

            Register.BackPorchConfigurationRegister.Define(this)
                .WithValueField(0, 11, out accumulatedVerticalBackPorchField, name: "AVBP")
                .WithReservedBits(11, 5)
                .WithValueField(16, 12, out accumulatedHorizontalBackPorchField, name: "AHBP", writeCallback: (_, __) => HandleActiveDisplayChange())
                .WithReservedBits(28, 4);

            Register.ActiveWidthConfigurationRegister.Define(this)
                .WithValueField(0, 11, out accumulatedActiveHeightField, name: "AAH")
                .WithReservedBits(11, 5)
                .WithValueField(16, 12, out accumulatedActiveWidthField, name: "AAW", writeCallback: (_, __) => HandleActiveDisplayChange())
                .WithReservedBits(28, 4);

            Register.TotalWidthConfigurationRegister.Define(this)
                .WithValueField(0, 10, name: "TOTALH")
                .WithValueField(16, 11, name: "TOTALW");

            Register.GlobalControlRegister.Define(this, resetValue: 0x2220)
                .WithFlag(0, out ltdcEnabledField, name: "LTDCEN")
                .WithReservedBits(1, 3)
                .WithTag("DBW", 4, 3)
                .WithReservedBits(7, 1)
                .WithTag("DGW", 8, 3)
                .WithReservedBits(11, 1)
                .WithTag("DGW", 12, 3)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("DEN", 16)
                .WithReservedBits(17, 11)
                .WithTaggedFlag("PCPOL", 28)
                .WithTaggedFlag("DEPOL", 29)
                .WithTaggedFlag("VSPOL", 30)
                .WithTaggedFlag("HSPOL", 31);

            Register.ShadowReloadConfigurationRegister.Define(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "IMR", writeCallback: (_, @new) =>
                {
                    if(!@new) return;
                    RegistersCollection.ShadowReload();
                })
                .WithFlag(1, out verticalBlankingReload, FieldMode.Read | FieldMode.WriteToSet, name: "VBR")
                .WithReservedBits(2, 30);

            Register.BackgroundColorConfigurationRegister.Define(this)
                .WithValueField(0, 8, out backgroundColorBlueChannelField, name: "BCBLUE")
                .WithValueField(8, 8, out backgroundColorGreenChannelField, name: "BCGREEN")
                .WithValueField(16, 8, out backgroundColorRedChannelField, name: "BCRED", writeCallback: (_, __) => HandleBackgroundColorChange());

            RegistersCollection.AddRegister((long)Register.InterruptEnableRegister, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Register.InterruptStatusRegister, interruptManager.GetRawInterruptFlagRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Register.InterruptClearRegister, interruptManager.GetInterruptClearRegister<DoubleWordRegister>());

            Register.LineInterruptPositionConfigurationRegister.Define(this)
                .WithValueField(0, 11, name: "LIPOS");

            layer = new Layer[] { new Layer(this, 0), new Layer(this, 1) };

            RegistersCollection.Reset();
            HandlePixelFormatChange();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            interruptManager.Reset();
        }

        public void WriteDoubleWord(long address, uint value)
        {
            RegistersCollection.Write(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [IrqProvider]
        [DefaultInterrupt]
        public GPIO IRQ { get; private set; }

        [IrqProvider("error", 1)]
        public GPIO ErrorIRQ { get; private set; }

        public long Size { get { return 0xC00; } }

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        protected override void Repaint()
        {
            lock(internalLock)
            {
                if(Width == 0 || Height == 0 || !ltdcEnabledField.Value)
                {
                    return;
                }

                for(var i = 0; i < 2; i++)
                {
                    if(layer[i].LayerEnableFlag.ShadowValue && layer[i].ColorFrameBufferAddressField.ShadowValue != 0)
                    {
                        var providedBufferLength = (int)(layer[i].BufferLineLength.ShadowValue * layer[i].BufferLineNumber.ShadowValue);
                        var bufferLength = layer[i].LayerBuffer.Length;

                        if(providedBufferLength < bufferLength)
                        {
                            interruptManager.SetInterrupt(Events.FIFOUnderrun);
                            // Reference manual doesn't say what's supposed to happen after an underrun interrupt, so let's copy the partial buffer
                            bufferLength = providedBufferLength;
                        }
                        // Excess buffer data is discarded, no need to check

                        sysbus.ReadBytes(layer[i].ColorFrameBufferAddressField.ShadowValue, bufferLength, layer[i].LayerBuffer, 0);
                        localLayerBuffer[i] = layer[i].LayerBuffer;
                    }
                    else
                    {
                        localLayerBuffer[i] = layer[i].LayerBackgroundBuffer;
                    }
                }

                blender.Blend(localLayerBuffer[0], localLayerBuffer[1],
                    ref buffer,
                    backgroundColor,
                    (byte)layer[0].ConstantAlphaConfigurationField.Value,
                    layer[1].BlendingFactor2.Value == BlendingFactor2.Multiply ? PixelBlendingMode.Multiply : PixelBlendingMode.NoModification,
                    (byte)layer[1].ConstantAlphaConfigurationField.Value,
                    layer[1].BlendingFactor1.Value == BlendingFactor1.Multiply ? PixelBlendingMode.Multiply : PixelBlendingMode.NoModification);

                interruptManager.SetInterrupt(Events.Line);
                if(verticalBlankingReload.Value)
                {
                    verticalBlankingReload.Value = false;
                    RegistersCollection.ShadowReload();
                    interruptManager.SetInterrupt(Events.RegisterReload);
                }
            }
        }

        private void HandleBackgroundColorChange()
        {
            backgroundColor = new Pixel(
                (byte)backgroundColorRedChannelField.Value,
                (byte)backgroundColorGreenChannelField.Value,
                (byte)backgroundColorBlueChannelField.Value,
                (byte)0xFF);
        }

        private void HandleActiveDisplayChange()
        {
            lock(internalLock)
            {
                var width = (int)(accumulatedActiveWidthField.Value - accumulatedHorizontalBackPorchField.Value);
                var height = (int)(accumulatedActiveHeightField.Value - accumulatedVerticalBackPorchField.Value);

                if((width == Width && height == Height) || width < 0 || height < 0)
                {
                    return;
                }

                Reconfigure(width, height);
                layer[0].RestoreBuffers();
                layer[1].RestoreBuffers();
            }
        }

        [PostDeserialization]
        private void HandlePixelFormatChange()
        {
            lock(internalLock)
            {
                blender = PixelManipulationTools.GetBlender(layer[0].PixelFormatField.Value.ToPixelFormat(), Endianess, layer[1].PixelFormatField.Value.ToPixelFormat(), Endianess, Format, Endianess);
            }
        }

        [Transient]
        private IPixelBlender blender;
        private Pixel backgroundColor;

        private readonly InterruptManager<Events> interruptManager;

        private readonly byte[][] localLayerBuffer;

        private readonly IValueRegisterField accumulatedVerticalBackPorchField;
        private readonly IValueRegisterField accumulatedHorizontalBackPorchField;
        private readonly IValueRegisterField accumulatedActiveHeightField;
        private readonly IValueRegisterField accumulatedActiveWidthField;
        private readonly IFlagRegisterField ltdcEnabledField;
        private readonly IFlagRegisterField verticalBlankingReload;
        private readonly IValueRegisterField backgroundColorBlueChannelField;
        private readonly IValueRegisterField backgroundColorGreenChannelField;
        private readonly IValueRegisterField backgroundColorRedChannelField;
        private readonly Layer[] layer;

        private readonly object internalLock;
        private readonly IBusController sysbus;

        private class Layer
        {
            public Layer(STM32LTDC video, int layerId)
            {
                var coll = video.RegistersCollection;
                var offset = layerId * 0x80 + 0x80;

                coll.DefineRegister(offset + (long)Register.Control)
                    .WithFlag(0, out LayerEnableFlag, name: "LEN", shadowReloadCallback: (_, __) => WarnAboutWrongBufferConfiguration())
                    .WithTaggedFlag("COLKEN", 1)
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("CLUTEN", 4)
                    .WithReservedBits(5, 27);

                coll.DefineRegister(offset + (long)Register.WindowHorizontalPositionConfigurationRegister)
                    .WithValueField(0, 12, out WindowHorizontalStartPositionField, name: "WHSTPOS")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 12, out WindowHorizontalStopPositionField, name: "WHPPOS", shadowReloadCallback: (_, __) => HandleLayerWindowConfigurationChange())
                    .WithReservedBits(28, 4);

                coll.DefineRegister(offset + (long)Register.WindowVerticalPositionConfigurationRegister)
                    .WithValueField(0, 12, out WindowVerticalStartPositionField, name: "WVSTPOS")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 12, out WindowVerticalStopPositionField, name: "WVSPPOS", shadowReloadCallback: (_, __) => HandleLayerWindowConfigurationChange())
                    .WithReservedBits(28, 4);

                coll.DefineRegister(offset + (long)Register.ColorKeyingConfigurationRegister)
                    .WithTag("CKBLUE", 0, 8)
                    .WithTag("CKGREEN", 8, 8)
                    .WithTag("CKGREEN", 16, 8)
                    .WithReservedBits(24, 8);

                coll.DefineRegister(offset + (long)Register.PixelFormatConfigurationRegister)
                    .WithEnumField(0, 3, out PixelFormatField, name: "PF", shadowReloadCallback: (old, @new) => { if(old == @new) return; RestoreBuffers(); video.HandlePixelFormatChange(); })
                    .WithReservedBits(3, 29);

                coll.DefineRegister(offset + (long)Register.ConstantAlphaConfigurationRegister, 0xFF)
                    .WithValueField(0, 8, out ConstantAlphaConfigurationField, name: "CONSTA")
                    .WithReservedBits(8, 24);

                coll.DefineRegister(offset + (long)Register.BlendingFactorConfigurationRegister, 0x0607)
                    .WithEnumField(0, 3, out BlendingFactor2, name: "BF2")
                    .WithReservedBits(3, 5)
                    .WithEnumField(8, 3, out BlendingFactor1, name: "BF1")
                    .WithReservedBits(11, 21);

                coll.DefineRegister(offset + (long)Register.ColorFrameBufferAddressRegister)
                    .WithValueField(0, 32, out ColorFrameBufferAddressField, name: "CFBADD", shadowReloadCallback: (_, __) => WarnAboutWrongBufferConfiguration());

                coll.DefineRegister(offset + (long)Register.DefaultColorConfigurationRegister)
                    .WithValueField(0, 8, out DefaultColorBlueField, name: "DCBLUE")
                    .WithValueField(8, 8, out DefaultColorGreenField, name: "DCGREEN")
                    .WithValueField(16, 8, out DefaultColorRedField, name: "DCRED")
                    .WithValueField(24, 8, out DefaultColorAlphaField, name: "DCALPHA");

                coll.DefineRegister(offset + (long)Register.ColorFrameBufferLengthRegister)
                    .WithValueField(0, 13, out BufferLineLength, name: "CFBLL")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 13, out BufferPitch, name: "CFBP")
                    .WithReservedBits(29, 3);

                coll.DefineRegister(offset + (long)Register.ColorFrameBufferLineNumberRegister)
                    .WithValueField(0, 11, out BufferLineNumber, name: "CFBLNBR")
                    .WithReservedBits(11, 21);

                coll.DefineRegister(offset + (long)Register.CLUTWriteRegister)
                    .WithTag("BLUE", 0, 8)
                    .WithTag("GREEN", 8, 8)
                    .WithTag("RED", 16, 8)
                    .WithTag("CLUEADD", 24, 8);

                this.layerId = layerId;
                this.video = video;
            }

            public void RestoreBuffers()
            {
                lock(video.internalLock)
                {
                    var layerPixelFormat = PixelFormatField.ShadowValue.ToPixelFormat();
                    var colorDepth = layerPixelFormat.GetColorDepth();
                    LayerBuffer = new byte[video.Width * video.Height * colorDepth];
                    LayerBackgroundBuffer = new byte[LayerBuffer.Length];

                    HandleLayerBackgroundColorChange();
                }
            }

            public IValueRegisterField DefaultColorAlphaField;
            public IValueRegisterField DefaultColorRedField;
            public IValueRegisterField DefaultColorGreenField;
            public IValueRegisterField DefaultColorBlueField;

            public IValueRegisterField WindowVerticalStartPositionField;
            public IValueRegisterField WindowVerticalStopPositionField;

            public IValueRegisterField WindowHorizontalStartPositionField;
            public IValueRegisterField WindowHorizontalStopPositionField;

            public IValueRegisterField ColorFrameBufferAddressField;
            public IEnumRegisterField<BlendingFactor2> BlendingFactor2;
            public IEnumRegisterField<BlendingFactor1> BlendingFactor1;

            public IValueRegisterField ConstantAlphaConfigurationField;
            public IEnumRegisterField<Dma2DColorMode> PixelFormatField;

            public IFlagRegisterField LayerEnableFlag;

            public IValueRegisterField BufferPitch;
            public IValueRegisterField BufferLineLength;

            public IValueRegisterField BufferLineNumber;

            public byte[] LayerBuffer;
            public byte[] LayerBackgroundBuffer;

            private void WarnAboutWrongBufferConfiguration()
            {
                lock(video.internalLock)
                {
                    if(LayerEnableFlag.ShadowValue && ColorFrameBufferAddressField.ShadowValue == 0)
                    {
                        if(!warningAlreadyIssued)
                        {
                            video.Log(LogLevel.Warning, "Layer {0} is enabled, but no frame buffer register is set", layerId);
                            warningAlreadyIssued = true;
                        }
                    }
                    else
                    {
                        warningAlreadyIssued = false;
                    }
                }
            }

            private void HandleLayerWindowConfigurationChange()
            {
                lock(video.internalLock)
                {
                    var width = (int)(WindowHorizontalStopPositionField.ShadowValue - WindowHorizontalStartPositionField.ShadowValue) + 1;
                    var height = (int)(WindowVerticalStopPositionField.ShadowValue - WindowVerticalStartPositionField.ShadowValue) + 1;

                    if(LayerEnableFlag.ShadowValue && (width != video.Width || height != video.Height))
                    {
                        video.Log(LogLevel.Warning, "Windowing is not supported yet for layer {0}.", layerId);
                    }
                }
            }

            private void HandleLayerBackgroundColorChange()
            {
                var colorBuffer = new byte[4 * video.Width * video.Height];
                for(var i = 0; i < colorBuffer.Length; i += 4)
                {
                    colorBuffer[i] = (byte)DefaultColorAlphaField.ShadowValue;
                    colorBuffer[i + 1] = (byte)DefaultColorRedField.ShadowValue;
                    colorBuffer[i + 2] = (byte)DefaultColorGreenField.ShadowValue;
                    colorBuffer[i + 3] = (byte)DefaultColorBlueField.ShadowValue;
                }

                PixelManipulationTools.GetConverter(PixelFormat.ARGB8888, video.Endianess, PixelFormatField.ShadowValue.ToPixelFormat(), video.Endianess)
                    .Convert(colorBuffer, ref LayerBackgroundBuffer);
            }

            private bool warningAlreadyIssued;
            private readonly int layerId;
            private readonly STM32LTDC video;

            private enum Register : long
            {
                Control = 0x4,
                WindowHorizontalPositionConfigurationRegister = 0x8,
                WindowVerticalPositionConfigurationRegister = 0xC,
                ColorKeyingConfigurationRegister = 0x10,
                PixelFormatConfigurationRegister = 0x14,
                ConstantAlphaConfigurationRegister = 0x18,
                DefaultColorConfigurationRegister = 0x1C,
                BlendingFactorConfigurationRegister = 0x20,
                ColorFrameBufferAddressRegister = 0x2C,
                ColorFrameBufferLengthRegister = 0x30,
                ColorFrameBufferLineNumberRegister = 0x34,
                CLUTWriteRegister = 0x44,
            }
        }

        private enum BlendingFactor1
        {
            Constant = 0x100,
            Multiply = 0x110
        }

        private enum BlendingFactor2
        {
            Constant = 0x101,
            Multiply = 0x111
        }

        private enum Events
        {
            Line = 0,
            [Subvector(1)]
            FIFOUnderrun = 1,
            [Subvector(1)]
            TransferError = 2,
            RegisterReload = 3
        }

        private enum Register : long
        {
            SyncronizationSizeConfigurationRegister = 0x8,
            BackPorchConfigurationRegister = 0x0C,
            ActiveWidthConfigurationRegister = 0x10,
            TotalWidthConfigurationRegister = 0x14,
            GlobalControlRegister = 0x18,
            ShadowReloadConfigurationRegister = 0x24,
            BackgroundColorConfigurationRegister = 0x2C,
            InterruptEnableRegister = 0x34,
            InterruptStatusRegister = 0x38,
            InterruptClearRegister = 0x3C,
            LineInterruptPositionConfigurationRegister = 0x40,
        }
    }
}
