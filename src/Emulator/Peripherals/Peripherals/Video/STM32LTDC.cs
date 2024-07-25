//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Peripherals.Video
{
    public class STM32LTDC : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize
    {
        public STM32LTDC(IMachine machine) : base(machine)
        {
            Reconfigure(format: PixelFormat.RGBX8888);

            IRQ = new GPIO();

            sysbus = machine.GetSystemBus(this);
            internalLock = new object();

            var activeWidthConfigurationRegister = new DoubleWordRegister(this);
            accumulatedActiveHeightField = activeWidthConfigurationRegister.DefineValueField(0, 11, name: "AAH");
            accumulatedActiveWidthField = activeWidthConfigurationRegister.DefineValueField(16, 12, name: "AAW", writeCallback: (_, __) => HandleActiveDisplayChange());

            var backPorchConfigurationRegister = new DoubleWordRegister(this);
            accumulatedVerticalBackPorchField = backPorchConfigurationRegister.DefineValueField(0, 11, name: "AVBP");
            accumulatedHorizontalBackPorchField = backPorchConfigurationRegister.DefineValueField(16, 12, name: "AHBP", writeCallback: (_, __) => HandleActiveDisplayChange());

            var backgroundColorConfigurationRegister = new DoubleWordRegister(this);
            backgroundColorBlueChannelField = backgroundColorConfigurationRegister.DefineValueField(0, 8, name: "BCBLUE");
            backgroundColorGreenChannelField = backgroundColorConfigurationRegister.DefineValueField(8, 8, name: "BCGREEN");
            backgroundColorRedChannelField = backgroundColorConfigurationRegister.DefineValueField(16, 8, name: "BCRED", writeCallback: (_, __) => HandleBackgroundColorChange());

            var interruptEnableRegister = new DoubleWordRegister(this);
            lineInterruptEnableFlag = interruptEnableRegister.DefineFlagField(0, name: "LIE");

            var interruptClearRegister = new DoubleWordRegister(this);
            interruptClearRegister.DefineFlagField(0, FieldMode.Write, name: "CLIF", writeCallback: (_, @new) => 
            { 
                if(!@new) return;
                lineInterruptFlag.Value = false; 
                UpdateInterrupts(); 
            });
            
            var interruptStatusRegister = new DoubleWordRegister(this);
            lineInterruptFlag = interruptStatusRegister.DefineFlagField(0, FieldMode.Read, name: "LIF");

            lineInterruptPositionConfigurationRegister = new DoubleWordRegister(this).WithValueField(0, 11, name: "LIPOS");

            var registerMappings = new Dictionary<long, DoubleWordRegister>
            {
                { (long)Register.BackPorchConfigurationRegister, backPorchConfigurationRegister },
                { (long)Register.ActiveWidthConfigurationRegister, activeWidthConfigurationRegister },
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
            }

            registers = new DoubleWordRegisterCollection(this, registerMappings);
            registers.Reset();
            HandlePixelFormatChange();
        }

        public GPIO IRQ { get; private set; }

        public long Size { get { return 0xC00; } }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            registers.Reset();
        }

        protected override void Repaint()
        {
            lock(internalLock)
            {
                if(Width == 0 || Height == 0)
                {
                    return;
                }

                for(var i = 0; i < 2; i++)
                {
                    if(layer[i].LayerEnableFlag.Value && layer[i].ColorFrameBufferAddressRegister.Value != 0)
                    {
                        sysbus.ReadBytes(layer[i].ColorFrameBufferAddressRegister.Value, layer[i].LayerBuffer.Length, layer[i].LayerBuffer, 0);
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
                    (byte)layer[0].ConstantAlphaConfigurationRegister.Value,
                    layer[1].blendingFactor2.Value == BlendingFactor2.Multiply ? PixelBlendingMode.Multiply : PixelBlendingMode.NoModification,
                    (byte)layer[1].ConstantAlphaConfigurationRegister.Value,
                    layer[1].blendingFactor1.Value == BlendingFactor1.Multiply ? PixelBlendingMode.Multiply : PixelBlendingMode.NoModification);

                lineInterruptFlag.Value = true;
                UpdateInterrupts();
            }
        }

        private readonly byte[][] localLayerBuffer;

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

        private void UpdateInterrupts()
        {
            IRQ.Set(lineInterruptEnableFlag.Value && lineInterruptFlag.Value);
        }

        private readonly IValueRegisterField accumulatedVerticalBackPorchField;
        private readonly IValueRegisterField accumulatedHorizontalBackPorchField;
        private readonly IValueRegisterField accumulatedActiveHeightField;
        private readonly IValueRegisterField accumulatedActiveWidthField;
        private readonly IValueRegisterField backgroundColorBlueChannelField;
        private readonly IValueRegisterField backgroundColorGreenChannelField;
        private readonly IValueRegisterField backgroundColorRedChannelField;
        private readonly IFlagRegisterField lineInterruptEnableFlag;
        private readonly IFlagRegisterField lineInterruptFlag;
        private readonly DoubleWordRegister lineInterruptPositionConfigurationRegister;
        private readonly Layer[] layer;
        private readonly DoubleWordRegisterCollection registers;

        private readonly object internalLock;
        private readonly IBusController sysbus;

        [Transient]
        private IPixelBlender blender;
        private Pixel backgroundColor;

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

        private enum Register : long
        {
            BackPorchConfigurationRegister = 0x0C,
            ActiveWidthConfigurationRegister = 0x10,
            BackgroundColorConfigurationRegister = 0x2C,
            InterruptEnableRegister = 0x34,
            InterruptStatusRegister = 0x38,
            InterruptClearRegister = 0x3C,
            LineInterruptPositionConfigurationRegister = 0x40,
        }

        private class Layer
        {
            public Layer(STM32LTDC video, int layerId)
            {
                ControlRegister = new DoubleWordRegister(video);
                LayerEnableFlag = ControlRegister.DefineFlagField(0, name: "LEN", writeCallback: (_, __) => WarnAboutWrongBufferConfiguration());

                WindowHorizontalPositionConfigurationRegister = new DoubleWordRegister(video);
                WindowHorizontalStartPositionField = WindowHorizontalPositionConfigurationRegister.DefineValueField(0, 12, name: "WHSTPOS");
                WindowHorizontalStopPositionField = WindowHorizontalPositionConfigurationRegister.DefineValueField(16, 12, name: "WHSPPOS", writeCallback: (_, __) => HandleLayerWindowConfigurationChange());

                WindowVerticalPositionConfigurationRegister = new DoubleWordRegister(video);
                WindowVerticalStartPositionField = WindowVerticalPositionConfigurationRegister.DefineValueField(0, 12, name: "WVSTPOS");
                WindowVerticalStopPositionField = WindowVerticalPositionConfigurationRegister.DefineValueField(16, 12, name: "WVSPPOS", writeCallback: (_, __) => HandleLayerWindowConfigurationChange());

                PixelFormatConfigurationRegister = new DoubleWordRegister(video);
                PixelFormatField = PixelFormatConfigurationRegister.DefineEnumField<Dma2DColorMode>(0, 3, name: "PF", writeCallback: (_, __) => { RestoreBuffers(); video.HandlePixelFormatChange(); });

                ConstantAlphaConfigurationRegister = new DoubleWordRegister(video, 0xFF).WithValueField(0, 8, name: "CONSTA");

                BlendingFactorConfigurationRegister = new DoubleWordRegister(video, 0x0607);
                blendingFactor1 = BlendingFactorConfigurationRegister.DefineEnumField<BlendingFactor1>(8, 3, name: "BF1", writeCallback: (_, __) => RestoreBuffers());
                blendingFactor2 = BlendingFactorConfigurationRegister.DefineEnumField<BlendingFactor2>(0, 3, name: "BF2", writeCallback: (_, __) => RestoreBuffers());

                ColorFrameBufferAddressRegister = new DoubleWordRegister(video).WithValueField(0, 32, name: "CFBADD", writeCallback: (_, __) => WarnAboutWrongBufferConfiguration());

                DefaultColorConfigurationRegister = new DoubleWordRegister(video);
                DefaultColorBlueField = DefaultColorConfigurationRegister.DefineValueField(0, 8, name: "DCBLUE");
                DefaultColorGreenField = DefaultColorConfigurationRegister.DefineValueField(8, 8, name: "DCGREEN");
                DefaultColorRedField = DefaultColorConfigurationRegister.DefineValueField(16, 8, name: "DCRED");
                DefaultColorAlphaField = DefaultColorConfigurationRegister.DefineValueField(24, 8, name: "DCALPHA", writeCallback: (_, __) => HandleLayerBackgroundColorChange());

                this.layerId = layerId;
                this.video = video;
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

                    if(width != video.Width || height != video.Height)
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

            public DoubleWordRegister ControlRegister;
            public IFlagRegisterField LayerEnableFlag;

            public DoubleWordRegister PixelFormatConfigurationRegister;
            public IEnumRegisterField<Dma2DColorMode> PixelFormatField;

            public DoubleWordRegister ConstantAlphaConfigurationRegister;
            public DoubleWordRegister BlendingFactorConfigurationRegister;
            public IEnumRegisterField<BlendingFactor1> blendingFactor1;
            public IEnumRegisterField<BlendingFactor2> blendingFactor2;

            public DoubleWordRegister ColorFrameBufferAddressRegister;

            public DoubleWordRegister WindowHorizontalPositionConfigurationRegister;
            public IValueRegisterField WindowHorizontalStopPositionField;
            public IValueRegisterField WindowHorizontalStartPositionField;

            public DoubleWordRegister WindowVerticalPositionConfigurationRegister;
            public IValueRegisterField WindowVerticalStopPositionField;
            public IValueRegisterField WindowVerticalStartPositionField;

            public DoubleWordRegister DefaultColorConfigurationRegister;
            public IValueRegisterField DefaultColorBlueField;
            public IValueRegisterField DefaultColorGreenField;
            public IValueRegisterField DefaultColorRedField;
            public IValueRegisterField DefaultColorAlphaField;

            public byte[] LayerBuffer;
            public byte[] LayerBackgroundBuffer;

            private bool warningAlreadyIssued;
            private readonly int layerId;
            private readonly STM32LTDC video;
        }
    }
}
