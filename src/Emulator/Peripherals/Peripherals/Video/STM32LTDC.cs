//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

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
    public class STM32LTDC : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize
    {
        public STM32LTDC(IMachine machine) : base(machine)
        {
            Reconfigure(format: PixelFormat.RGBX8888);

            IRQ = new GPIO();
            ErrorIRQ = new GPIO();
            interruptManager = new InterruptManager<Events>(this);

            sysbus = machine.GetSystemBus(this);
            internalLock = new object();

            // We don't care about the sync, back porch, or front porch pixels, but since the registers specify sums of pixel areas, we need the back porch sum and active sum registers to find the width and height. We're providing the other registers to silence warnings.
            var syncronizationSizeConfigurationRegister = new DoubleWordRegister(this)
                .WithValueField(0, 10, name: "VSH")
                .WithReservedBits(11, 5)
                .WithValueField(16, 11, name: "HSW")
                .WithReservedBits(28, 4);

            var backPorchConfigurationRegister = new DoubleWordRegister(this)
                .WithValueField(0, 11, out accumulatedVerticalBackPorchField, name: "AVBP")
                .WithReservedBits(11, 5)
                .WithValueField(16, 12, out accumulatedHorizontalBackPorchField, name: "AHBP", writeCallback: (_, __) => HandleActiveDisplayChange())
                .WithReservedBits(28, 4);

            var activeWidthConfigurationRegister = new DoubleWordRegister(this)
                .WithValueField(0, 11, out accumulatedActiveHeightField, name: "AAH")
                .WithReservedBits(11, 5)
                .WithValueField(16, 12, out accumulatedActiveWidthField, name: "AAW", writeCallback: (_, __) => HandleActiveDisplayChange())
                .WithReservedBits(28, 4);

            var totalWidthConfigurationRegister = new DoubleWordRegister(this)
                .WithValueField(0, 10, name: "TOTALH")
                .WithValueField(16, 11, name: "TOTALW");

            var globalControlRegister = new DoubleWordRegister(this, resetValue: 0x2220)
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

            var shadowReloadRegister = new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "IMR", writeCallback: (_, @new) =>
                {
                    if (!@new) return;
                    ReloadShadowRegisters();
                })
                .WithFlag(1, out verticalBlankingReload,FieldMode.Read | FieldMode.WriteToSet, name: "VBR")
                .WithReservedBits(2, 30);

            var backgroundColorConfigurationRegister = new DoubleWordRegister(this)
                .WithValueField(0, 8, out backgroundColorBlueChannelField, name: "BCBLUE")
                .WithValueField(8, 8, out backgroundColorGreenChannelField, name: "BCGREEN")
                .WithValueField(16, 8, out backgroundColorRedChannelField, name: "BCRED", writeCallback: (_, __) => HandleBackgroundColorChange());

            var interruptEnableRegister = interruptManager.GetInterruptEnableRegister<DoubleWordRegister>();
            var interruptStatusRegister = interruptManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>();
            var interruptClearRegister = interruptManager.GetInterruptClearRegister<DoubleWordRegister>();

            lineInterruptPositionConfigurationRegister = new DoubleWordRegister(this).WithValueField(0, 11, name: "LIPOS");

            var registerMappings = new Dictionary<long, DoubleWordRegister>
            {
                { (long)Register.SyncronizationSizeConfigurationRegister, syncronizationSizeConfigurationRegister },
                { (long)Register.TotalWidthConfigurationRegister, totalWidthConfigurationRegister },
                { (long)Register.BackPorchConfigurationRegister, backPorchConfigurationRegister },
                { (long)Register.ActiveWidthConfigurationRegister, activeWidthConfigurationRegister },
                { (long)Register.GlobalControlRegister, globalControlRegister },
                { (long)Register.ShadowReloadConfigurationRegister, shadowReloadRegister },
                { (long)Register.BackgroundColorConfigurationRegister, backgroundColorConfigurationRegister },
                { (long)Register.InterruptEnableRegister, interruptEnableRegister },
                { (long)Register.InterruptStatusRegister, interruptStatusRegister },
                { (long)Register.InterruptClearRegister, interruptClearRegister },
                { (long)Register.LineInterruptPositionConfigurationRegister, lineInterruptPositionConfigurationRegister }
            };

            localLayerBuffer = new byte[2][];
            layer = new Layer[2];
            for(var i = 0; i < layer.Length; i++)
            {
                layer[i] = new Layer(this, i);

                var offset = 0x80 * i;
                registerMappings.Add(0x84 + offset, layer[i].ControlRegister);
                registerMappings.Add(0x88 + offset, layer[i].WindowHorizontalPositionConfigurationRegister);
                registerMappings.Add(0x8C + offset, layer[i].WindowVerticalPositionConfigurationRegister);
                registerMappings.Add(0x94 + offset, layer[i].PixelFormatConfigurationRegister);
                registerMappings.Add(0x98 + offset, layer[i].ConstantAlphaConfigurationRegister);
                registerMappings.Add(0x9C + offset, layer[i].DefaultColorConfigurationRegister);
                registerMappings.Add(0xA0 + offset, layer[i].BlendingFactorConfigurationRegister);
                registerMappings.Add(0xAC + offset, layer[i].ColorFrameBufferAddressRegister);
                registerMappings.Add(0xB0 + offset, layer[i].ColorFrameBufferLengthRegister);
                registerMappings.Add(0xB4 + offset, layer[i].ColorFrameBufferLineNumberRegister);
            }

            registers = new DoubleWordRegisterCollection(this, registerMappings);
            registers.Reset();
            HandlePixelFormatChange();
        }

        public override void Reset()
        {
            registers.Reset();
            interruptManager.Reset();
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        [IrqProvider]
        [DefaultInterrupt]
        public GPIO IRQ { get; private set; }

        [IrqProvider("error", 1)]
        public GPIO ErrorIRQ { get; private set; }

        public long Size { get { return 0xC00; } }

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
                    if(layer[i].LayerEnableFlag.Value && layer[i].ColorFrameBufferAddressRegister.Value != 0)
                    {
                        var providedBufferLength = (int)(layer[i].BufferLineLength.Value * layer[i].BufferLineNumber.Value);
                        var bufferLength = layer[i].LayerBuffer.Length;

                        if(providedBufferLength < bufferLength)
                        {
                            interruptManager.SetInterrupt(Events.FIFOUnderrun);
                            // Reference manual doesn't say what's supposed to happen after an underrun interrupt, so let's copy the partial buffer
                            bufferLength = providedBufferLength;
                        }
                        // Excess buffer data is discarded, no need to check

                        sysbus.ReadBytes(layer[i].ColorFrameBufferAddressRegister.Value, bufferLength, layer[i].LayerBuffer, 0);
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
                    ReloadShadowRegisters();
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

        private void ReloadShadowRegisters()
        {
            for(var idx = 0; idx < 2; idx += 1)
            {
                layer[idx].ReloadShadowRegisters();
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
        private readonly DoubleWordRegister lineInterruptPositionConfigurationRegister;
        private readonly Layer[] layer;
        private readonly DoubleWordRegisterCollection registers;

        private readonly object internalLock;
        private readonly IBusController sysbus;

        private class Layer
        {
            public Layer(STM32LTDC video, int layerId)
            {
                ControlRegister = new DoubleWordRegister(video);
                LayerEnableFlag = new ShadowedRegisterField<bool>(ControlRegister.DefineFlagField(0, name: "LEN"), writeCallback: (_, __) => WarnAboutWrongBufferConfiguration());
                ControlRegister
                    .WithTaggedFlag("COLKEN", 1)
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("CLUTEN", 4)
                    .WithReservedBits(5, 27);

                WindowHorizontalPositionConfigurationRegister = new DoubleWordRegister(video);
                WindowHorizontalStartPositionField = new ShadowedRegisterField<ulong>(WindowHorizontalPositionConfigurationRegister.DefineValueField(0, 12, name: "WHSTPOS"));
                WindowHorizontalPositionConfigurationRegister.WithReservedBits(12, 4);
                WindowHorizontalStopPositionField = new ShadowedRegisterField<ulong>(WindowHorizontalPositionConfigurationRegister.DefineValueField(16, 12, name: "WHSPPOS"), writeCallback: (_, __) => HandleLayerWindowConfigurationChange());
                WindowHorizontalPositionConfigurationRegister.WithReservedBits(28, 4);

                WindowVerticalPositionConfigurationRegister = new DoubleWordRegister(video);
                WindowVerticalStartPositionField = new ShadowedRegisterField<ulong>(WindowVerticalPositionConfigurationRegister.DefineValueField(0, 12, name: "WVSTPOS"));
                WindowVerticalPositionConfigurationRegister.WithReservedBits(12, 4);
                WindowVerticalStopPositionField = new ShadowedRegisterField<ulong>(WindowVerticalPositionConfigurationRegister.DefineValueField(16, 12, name: "WVSPPOS", writeCallback: (_, __) => HandleLayerWindowConfigurationChange()));
                WindowVerticalPositionConfigurationRegister.WithReservedBits(28, 4);

                PixelFormatConfigurationRegister = new DoubleWordRegister(video);
                PixelFormatField = new ShadowedRegisterField<Dma2DColorMode>(PixelFormatConfigurationRegister.DefineEnumField<Dma2DColorMode>(0, 3, name: "PF"), writeCallback: (old, @new) => { if(old == @new) return; RestoreBuffers(); video.HandlePixelFormatChange(); });
                PixelFormatConfigurationRegister.WithReservedBits(3, 29);

                ConstantAlphaConfigurationRegister = new DoubleWordRegister(video, 0xFF);
                ConstantAlphaConfigurationField = new ShadowedRegisterField<ulong>(ConstantAlphaConfigurationRegister.DefineValueField(0, 8, name: "CONSTA"));
                ConstantAlphaConfigurationRegister.WithReservedBits(8, 24);

                BlendingFactorConfigurationRegister = new DoubleWordRegister(video, 0x0607);
                BlendingFactor2 = new ShadowedRegisterField<BlendingFactor2>(BlendingFactorConfigurationRegister.DefineEnumField<BlendingFactor2>(0, 3, name: "BF2"));
                BlendingFactorConfigurationRegister.WithReservedBits(3, 5);
                BlendingFactor1 = new ShadowedRegisterField<BlendingFactor1>(BlendingFactorConfigurationRegister.DefineEnumField<BlendingFactor1>(8, 3, name: "BF1"));
                BlendingFactorConfigurationRegister.WithReservedBits(11, 21);

                ColorFrameBufferAddressRegister = new DoubleWordRegister(video);
                ColorFrameBufferAddressField = new ShadowedRegisterField<ulong>(ColorFrameBufferAddressRegister.DefineValueField(0, 32, name: "CFBADD"), writeCallback: (_, __) => WarnAboutWrongBufferConfiguration());

                DefaultColorConfigurationRegister = new DoubleWordRegister(video);
                DefaultColorBlueField = new ShadowedRegisterField<ulong>(DefaultColorConfigurationRegister.DefineValueField(0, 8, name: "DCBLUE"));
                DefaultColorGreenField = new ShadowedRegisterField<ulong>(DefaultColorConfigurationRegister.DefineValueField(8, 8, name: "DCGREEN"));
                DefaultColorRedField = new ShadowedRegisterField<ulong>(DefaultColorConfigurationRegister.DefineValueField(16, 8, name: "DCRED"));
                DefaultColorAlphaField = new ShadowedRegisterField<ulong>(DefaultColorConfigurationRegister.DefineValueField(24, 8, name: "DCALPHA"), writeCallback: (_, __) => HandleLayerBackgroundColorChange());

                ColorFrameBufferLengthRegister = new DoubleWordRegister(video);
                BufferLineLength = new ShadowedRegisterField<ulong>(ColorFrameBufferLengthRegister.DefineValueField(0, 13, name: "CFBLL"));
                ColorFrameBufferLengthRegister.WithReservedBits(13, 3);
                BufferPitch = new ShadowedRegisterField<ulong>(ColorFrameBufferLengthRegister.DefineValueField(16, 13, name: "CFBP"));
                ColorFrameBufferLengthRegister.WithReservedBits(29, 3);

                ColorFrameBufferLineNumberRegister = new DoubleWordRegister(video);
                BufferLineNumber = new ShadowedRegisterField<ulong>(ColorFrameBufferLineNumberRegister.DefineValueField(0, 11, name: "CFBLNBR"));
                ColorFrameBufferLineNumberRegister.WithReservedBits(11, 21);

                this.layerId = layerId;
                this.video = video;
            }

            public void ReloadShadowRegisters()
            {
                var valueRegisters = new ShadowedRegisterField<ulong>[] {
                    DefaultColorAlphaField,
                    DefaultColorRedField,
                    DefaultColorGreenField,
                    DefaultColorBlueField,
                    WindowVerticalStartPositionField,
                    WindowVerticalStopPositionField,
                    WindowHorizontalStartPositionField,
                    WindowHorizontalStopPositionField,
                    ColorFrameBufferAddressField,
                    BufferPitch,
                    BufferLineLength,
                    BufferLineNumber
                };
                foreach(var reg in valueRegisters)
                {
                    reg.Reload();
                }
                LayerEnableFlag.Reload();
                BlendingFactor1.Reload();
                BlendingFactor2.Reload();
                PixelFormatField.Reload();
            }

            public void RestoreBuffers()
            {
                lock(video.internalLock)
                {
                    var layerPixelFormat = PixelFormatField.Value.ToPixelFormat();
                    var colorDepth = layerPixelFormat.GetColorDepth();
                    LayerBuffer = new byte[video.Width * video.Height * colorDepth];
                    LayerBackgroundBuffer = new byte[LayerBuffer.Length];

                    HandleLayerBackgroundColorChange();
                }
            }

            public DoubleWordRegister ControlRegister;
            public ShadowedRegisterField<ulong> DefaultColorAlphaField;
            public ShadowedRegisterField<ulong> DefaultColorRedField;
            public ShadowedRegisterField<ulong> DefaultColorGreenField;
            public ShadowedRegisterField<ulong> DefaultColorBlueField;

            public DoubleWordRegister DefaultColorConfigurationRegister;
            public ShadowedRegisterField<ulong> WindowVerticalStartPositionField;
            public ShadowedRegisterField<ulong> WindowVerticalStopPositionField;

            public DoubleWordRegister WindowVerticalPositionConfigurationRegister;
            public ShadowedRegisterField<ulong> WindowHorizontalStartPositionField;
            public ShadowedRegisterField<ulong> WindowHorizontalStopPositionField;

            public DoubleWordRegister WindowHorizontalPositionConfigurationRegister;

            public ShadowedRegisterField<ulong> ColorFrameBufferAddressField;
            public DoubleWordRegister ColorFrameBufferAddressRegister;
            public ShadowedRegisterField<BlendingFactor2> BlendingFactor2;
            public ShadowedRegisterField<BlendingFactor1> BlendingFactor1;
            public DoubleWordRegister BlendingFactorConfigurationRegister;

            public ShadowedRegisterField<ulong> ConstantAlphaConfigurationField;
            public DoubleWordRegister ConstantAlphaConfigurationRegister;
            public ShadowedRegisterField<Dma2DColorMode> PixelFormatField;

            public DoubleWordRegister PixelFormatConfigurationRegister;
            public ShadowedRegisterField<bool> LayerEnableFlag;

            public DoubleWordRegister ColorFrameBufferLengthRegister;
            public ShadowedRegisterField<ulong> BufferPitch;
            public ShadowedRegisterField<ulong> BufferLineLength;

            public DoubleWordRegister ColorFrameBufferLineNumberRegister;
            public ShadowedRegisterField<ulong> BufferLineNumber;

            public byte[] LayerBuffer;
            public byte[] LayerBackgroundBuffer;

            private void WarnAboutWrongBufferConfiguration()
            {
                lock(video.internalLock)
                {
                    if(LayerEnableFlag.Value && ColorFrameBufferAddressRegister.Value == 0)
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
                    var width = (int)(WindowHorizontalStopPositionField.Value - WindowHorizontalStartPositionField.Value) + 1;
                    var height = (int)(WindowVerticalStopPositionField.Value - WindowVerticalStartPositionField.Value) + 1;

                    if(LayerEnableFlag.Value && (width != video.Width || height != video.Height))
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
                    colorBuffer[i] = (byte)DefaultColorAlphaField.Value;
                    colorBuffer[i + 1] = (byte)DefaultColorRedField.Value;
                    colorBuffer[i + 2] = (byte)DefaultColorGreenField.Value;
                    colorBuffer[i + 3] = (byte)DefaultColorBlueField.Value;
                }

                PixelManipulationTools.GetConverter(PixelFormat.ARGB8888, video.Endianess, PixelFormatField.Value.ToPixelFormat(), video.Endianess)
                    .Convert(colorBuffer, ref LayerBackgroundBuffer);
            }

            private bool warningAlreadyIssued;
            private readonly int layerId;
            private readonly STM32LTDC video;

            public class ShadowedRegisterField<T>
            {
                public ShadowedRegisterField(IRegisterField<T> field, Action<T, T> writeCallback = null)
                {
                    ShadowField = field;
                    this.writeCallback = writeCallback;
                }

                public void Reload()
                {
                    var oldValue = Value;
                    Value = ShadowField.Value;
                    if(writeCallback != null)
                    {
                        this.writeCallback(oldValue, Value);
                    }
                }

                public T Value { get; private set; }

                public IRegisterField<T> ShadowField;

                private readonly Action<T, T> writeCallback;
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
