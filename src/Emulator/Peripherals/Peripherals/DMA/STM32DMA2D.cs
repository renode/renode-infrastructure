//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Peripherals.DMA
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public sealed class STM32DMA2D : IDoubleWordPeripheral, IKnownSize
    {
        public STM32DMA2D(IMachine machine) : this()
        {
            sysbus = machine.GetSystemBus(this);
            IRQ = new GPIO();
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; private set; }

        public long Size
        {
            get
            {
                return 0xC00;
            }
        }

        private byte[] foregroundClut;
        private byte[] backgroundClut;

        private STM32DMA2D()
        {
            var controlRegister = new DoubleWordRegister(this)
                .WithFlag(0, out startFlag, name: "Start", writeCallback: (old, @new) => { if(@new) DoTransfer(); })
                .WithEnumField(16, 2, out dma2dMode, name: "Mode")
            ;

            var foregroundClutMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32);
            var backgroundClutMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32);

            var interruptStatusRegister = new DoubleWordRegister(this)
                .WithTaggedFlag("TEIF", 0)
                .WithFlag(1, out transferCompleteFlag, FieldMode.Read, name: "TCIF")
                .WithTaggedFlag("TWIF", 2)
                .WithTaggedFlag("CAEIF", 3)
                .WithTaggedFlag("CTCIF", 4)
                .WithTaggedFlag("CEIF", 5)
                .WithReservedBits(6, 26)
            ;

            var interruptFlagClearRegister = new DoubleWordRegister(this)
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "CTCIF", writeCallback: (_, val) => {
                    if(val) { IRQ.Unset(); transferCompleteFlag.Value = false; }})
            ;

            var numberOfLineRegister = new DoubleWordRegister(this)
                .WithValueField(0, 16, out numberOfLineField, name: "NL")
                .WithValueField(16, 14, out pixelsPerLineField, name: "PL")
                .WithChangeCallback((_, __) =>
                    {
                        HandleOutputBufferSizeChange();
                        HandleBackgroundBufferSizeChange();
                        HandleForegroundBufferSizeChange();
                    })
            ;

            outputMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32);
            backgroundMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32);
            foregroundMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32);

            var outputPfcControlRegister = new DoubleWordRegister(this)
                .WithEnumField(0, 3, out outputColorModeField, name: "CM",
                    changeCallback: (_, __) =>
                    {
                        HandlePixelFormatChange();
                        HandleOutputBufferSizeChange();
                    })
            ;

            var foregroundPfcControlRegister = new DoubleWordRegister(this)
                .WithEnumField(0, 4, out foregroundColorModeField, name: "CM",
                    changeCallback: (_, __) =>
                    {
                        HandlePixelFormatChange();
                        HandleForegroundBufferSizeChange();
                    })
                .WithEnumField(4, 1, out foregroundClutColorModeField, name: "CCM", changeCallback: (_, __) => HandlePixelFormatChange())
                .WithValueField(8, 8, out var foregroundClutSizeField, name: "CS") //out of order to use the var in the next field
                .WithFlag(5, name: "START", valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        foregroundClut = new byte[(foregroundClutSizeField.Value + 1) * (uint)foregroundClutColorModeField.Value.ToPixelFormat().GetColorDepth()];
                        sysbus.ReadBytes(foregroundClutMemoryAddressRegister.Value, foregroundClut.Length, foregroundClut, 0, true);
                    })
                .WithEnumField(16, 2, out foregroundAlphaMode, name: "AM", changeCallback: (_, __) => HandlePixelFormatChange())
                .WithValueField(24, 8, out foregroundAlphaField, name: "ALPHA")
            ;

            var foregroundColorRegister = new DoubleWordRegister(this)
                .WithValueField(0, 8, out foregroundColorBlueChannelField, name: "BLUE")
                .WithValueField(8, 8, out foregroundColorGreenChannelField, name: "GREEN")
                .WithValueField(16, 8, out foregroundColorRedChannelField, name: "RED")
                .WithReservedBits(24, 8)
                .WithChangeCallback((_, __) => HandlePixelFormatChange())
            ;

            var backgroundPfcControlRegister = new DoubleWordRegister(this)
                .WithEnumField(0, 4, out backgroundColorModeField, name: "CM",
                    changeCallback: (_, __) =>
                    {
                        HandlePixelFormatChange();
                        HandleBackgroundBufferSizeChange();
                    })
                .WithEnumField(4, 1, out backgroundClutColorModeField, name: "CCM", changeCallback: (_, __) => HandlePixelFormatChange())
                .WithValueField(8, 8, out var backgroundClutSizeField, name: "CS") //out of order to use the var in the next field
                .WithFlag(5, name: "START", valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        backgroundClut = new byte[(backgroundClutSizeField.Value + 1) * (uint)backgroundClutColorModeField.Value.ToPixelFormat().GetColorDepth()];
                        sysbus.ReadBytes(backgroundClutMemoryAddressRegister.Value, backgroundClut.Length, backgroundClut, 0, true);
                    })
                .WithEnumField(16, 2, out backgroundAlphaMode, name: "AM", changeCallback: (_, __) => HandlePixelFormatChange())
                .WithValueField(24, 8, out backgroundAlphaField, name: "ALPHA")
            ;

            var backgroundColorRegister = new DoubleWordRegister(this)
                .WithValueField(0, 8, out backgroundColorBlueChannelField, name: "BLUE")
                .WithValueField(8, 8, out backgroundColorGreenChannelField, name: "GREEN")
                .WithValueField(16, 8, out backgroundColorRedChannelField, name: "RED")
                .WithReservedBits(24, 8)
                .WithChangeCallback((_, __) => HandlePixelFormatChange())
            ;

            outputColorRegister = new DoubleWordRegister(this).WithValueField(0, 32);

            var outputOffsetRegister = new DoubleWordRegister(this)
                .WithValueField(0, 14, out outputLineOffsetField, name: "LO")
            ;

            var foregroundOffsetRegister = new DoubleWordRegister(this)
                .WithValueField(0, 14, out foregroundLineOffsetField, name: "LO")
            ;

            var backgroundOffsetRegister = new DoubleWordRegister(this)
                .WithValueField(0, 14, out backgroundLineOffsetField, name: "LO")
            ;

            var regs = new Dictionary<long, DoubleWordRegister>
            {
                { (long)Register.ControlRegister, controlRegister },
                { (long)Register.InterruptStatusRegister, interruptStatusRegister },
                { (long)Register.InterruptFlagClearRegister, interruptFlagClearRegister },
                { (long)Register.ForegroundMemoryAddressRegister, foregroundMemoryAddressRegister },
                { (long)Register.ForegroundOffsetRegister, foregroundOffsetRegister },
                { (long)Register.BackgroundMemoryAddressRegister, backgroundMemoryAddressRegister },
                { (long)Register.BackgroundOffsetRegister, backgroundOffsetRegister },
                { (long)Register.ForegroundPfcControlRegister, foregroundPfcControlRegister },
                { (long)Register.ForegroundColorRegister, foregroundColorRegister },
                { (long)Register.BackgroundPfcControlRegister, backgroundPfcControlRegister },
                { (long)Register.BackgroundColorRegister, backgroundColorRegister },
                { (long)Register.OutputPfcControlRegister, outputPfcControlRegister },
                { (long)Register.OutputColorRegister, outputColorRegister },
                { (long)Register.OutputMemoryAddressRegister, outputMemoryAddressRegister },
                { (long)Register.OutputOffsetRegister, outputOffsetRegister },
                { (long)Register.NumberOfLineRegister, numberOfLineRegister },
                { (long)Register.ForegroundClutMemoryAddressRegister, foregroundClutMemoryAddressRegister },
                { (long)Register.BackgroundClutMemoryAddressRegister, backgroundClutMemoryAddressRegister }
            };

            registers = new DoubleWordRegisterCollection(this, regs);
        }

        private void HandleOutputBufferSizeChange()
        {
            var outputFormatColorDepth = outputColorModeField.Value.ToPixelFormat().GetColorDepth();
            outputBuffer = new byte[numberOfLineField.Value * pixelsPerLineField.Value * (uint)outputFormatColorDepth];
            outputLineBuffer = new byte[pixelsPerLineField.Value * (uint)outputFormatColorDepth];
        }

        private void HandleBackgroundBufferSizeChange()
        {
            var backgroundFormatColorDepth = backgroundColorModeField.Value.ToPixelFormat().GetColorDepth();
            backgroundBuffer = new byte[pixelsPerLineField.Value * numberOfLineField.Value * (uint)backgroundFormatColorDepth];
            backgroundLineBuffer = new byte[pixelsPerLineField.Value * (uint)backgroundFormatColorDepth];
        }

        private void HandleForegroundBufferSizeChange()
        {
            var foregroundFormatColorDepth = foregroundColorModeField.Value.ToPixelFormat().GetColorDepth();
            foregroundBuffer = new byte[pixelsPerLineField.Value * numberOfLineField.Value * (uint)foregroundFormatColorDepth];
            foregroundLineBuffer = new byte[pixelsPerLineField.Value * (uint)foregroundFormatColorDepth];
        }

        [PostDeserialization]
        private void HandlePixelFormatChange()
        {
            var outputFormat = outputColorModeField.Value.ToPixelFormat();
            var backgroundFormat = backgroundColorModeField.Value.ToPixelFormat();
            var backgroundFixedColor = new Pixel(
                (byte)backgroundColorRedChannelField.Value,
                (byte)backgroundColorGreenChannelField.Value,
                (byte)backgroundColorBlueChannelField.Value,
                (byte)0xFF);

            var foregroundFormat = foregroundColorModeField.Value.ToPixelFormat();
            var foregroundFixedColor = new Pixel(
                (byte)foregroundColorRedChannelField.Value,
                (byte)foregroundColorGreenChannelField.Value,
                (byte)foregroundColorBlueChannelField.Value,
                (byte)0xFF);

            bgConverter = PixelManipulationTools.GetConverter(backgroundFormat, Endianness, outputFormat, Endianness, backgroundClutColorModeField.Value.ToPixelFormat(), backgroundFixedColor);
            fgConverter = PixelManipulationTools.GetConverter(foregroundFormat, Endianness, outputFormat, Endianness, foregroundClutColorModeField.Value.ToPixelFormat(), foregroundFixedColor);
            blender = PixelManipulationTools.GetBlender(backgroundFormat, Endianness, foregroundFormat, Endianness, outputFormat, Endianness, foregroundClutColorModeField.Value.ToPixelFormat(), backgroundClutColorModeField.Value.ToPixelFormat(), backgroundFixedColor, foregroundFixedColor);
        }

        private void DoTransfer()
        {
            var foregroundFormat = foregroundColorModeField.Value.ToPixelFormat();
            var outputFormat = outputColorModeField.Value.ToPixelFormat();

            switch(dma2dMode.Value)
            {
                case Mode.RegisterToMemory:
                    var colorBytes = BitConverter.GetBytes(outputColorRegister.Value);
                    var colorDepth = outputFormat.GetColorDepth();

                    // fill area with the color defined in output color register
                    for(var i = 0; i < outputBuffer.Length; i++)
                    {
                        outputBuffer[i] = colorBytes[i % colorDepth];
                    }

                    if(outputLineOffsetField.Value == 0)
                    {
                        // we can copy everything at once - it might be faster
                        sysbus.WriteBytes(outputBuffer, outputMemoryAddressRegister.Value);
                    }
                    else
                    {
                        // we have to copy per line
                        var lineWidth = (int)pixelsPerLineField.Value * outputFormat.GetColorDepth();
                        var offset = lineWidth + ((int)outputLineOffsetField.Value * outputFormat.GetColorDepth());
                        for(var line = 0; line < (int)numberOfLineField.Value; line++)
                        {
                            sysbus.WriteBytes(outputBuffer, (ulong)(outputMemoryAddressRegister.Value + line * offset), line * lineWidth, lineWidth);
                        }
                    }
                    break;
                case Mode.MemoryToMemoryWithBlending:
                    var bgBlendingMode = backgroundAlphaMode.Value.ToPixelBlendingMode();
                    var fgBlendingMode = foregroundAlphaMode.Value.ToPixelBlendingMode();
                    var bgAlpha = (byte)backgroundAlphaField.Value;
                    var fgAlpha = (byte)foregroundAlphaField.Value;

                    if(outputLineOffsetField.Value == 0 && foregroundLineOffsetField.Value == 0 && backgroundLineOffsetField.Value == 0)
                    {
                        // we can optimize here and copy everything at once
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value, foregroundBuffer,
                               converter: (localForegroundBuffer, line) =>
                               {
                                   sysbus.ReadBytes(backgroundMemoryAddressRegister.Value, backgroundBuffer.Length, backgroundBuffer, 0);
                                   // per-pixel alpha blending
                                   blender.Blend(backgroundBuffer, backgroundClut, localForegroundBuffer, foregroundClut, ref outputBuffer, new Pixel(0, 0, 0, 0xFF), bgAlpha, bgBlendingMode, fgAlpha, fgBlendingMode);
                                   return outputBuffer;
                               });
                    }
                    else
                    {
                        var backgroundFormat = backgroundColorModeField.Value.ToPixelFormat();
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value,
                               foregroundLineBuffer,
                               (int)foregroundLineOffsetField.Value * foregroundFormat.GetColorDepth(),
                               (int)outputLineOffsetField.Value * outputFormat.GetColorDepth(),
                               (int)numberOfLineField.Value,
                               (localForegroundBuffer, line) =>
                                {
                                    sysbus.ReadBytes((ulong)(backgroundMemoryAddressRegister.Value + line * (uint)(backgroundLineOffsetField.Value + pixelsPerLineField.Value) * backgroundFormat.GetColorDepth()), backgroundLineBuffer.Length, backgroundLineBuffer, 0);
                                    blender.Blend(backgroundLineBuffer, backgroundClut, localForegroundBuffer, foregroundClut, ref outputLineBuffer, null, bgAlpha, bgBlendingMode, fgAlpha, fgBlendingMode);
                                    return outputLineBuffer;
                                });
                    }
                    break;
                case Mode.MemoryToMemoryWithPfc:
                    fgAlpha = (byte)foregroundAlphaField.Value;
                    fgBlendingMode = foregroundAlphaMode.Value.ToPixelBlendingMode();

                    if(outputLineOffsetField.Value == 0 && foregroundLineOffsetField.Value == 0 && backgroundLineOffsetField.Value == 0)
                    {
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value,
                                foregroundBuffer,
                                converter: (localForegroundBuffer, line) =>
                                {
                                    fgConverter.Convert(localForegroundBuffer, foregroundClut, fgAlpha, fgBlendingMode, ref outputBuffer);
                                    return outputBuffer;
                                });
                    }
                    else
                    {
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value,
                                foregroundLineBuffer,
                                (int)foregroundLineOffsetField.Value * foregroundFormat.GetColorDepth(),
                                (int)outputLineOffsetField.Value * outputFormat.GetColorDepth(),
                                (int)numberOfLineField.Value,
                                (localForegroundBuffer, line) =>
                                {
                                    fgConverter.Convert(localForegroundBuffer, foregroundClut, fgAlpha, fgBlendingMode, ref outputLineBuffer);
                                    return outputLineBuffer;
                                });
                    }
                    break;
                case Mode.MemoryToMemory:
                    if(outputLineOffsetField.Value == 0 && foregroundLineOffsetField.Value == 0)
                    {
                        // we can optimize here and copy everything at once
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value, foregroundBuffer);
                    }
                    else
                    {
                        // in this mode no graphical data transformation is performed
                        // color format is stored in foreground pfc control register

                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value,
                                       foregroundLineBuffer,
                                       (int)foregroundLineOffsetField.Value * foregroundFormat.GetColorDepth(),
                                       (int)outputLineOffsetField.Value * foregroundFormat.GetColorDepth(),
                                       (int)numberOfLineField.Value);
                    }
                    break;
            }

            startFlag.Value = false;
            transferCompleteFlag.Value = true;
            IRQ.Set();
        }

        private void DoCopy(ulong sourceAddress, ulong destinationAddress, byte[] sourceBuffer, int sourceOffset = 0, int destinationOffset = 0, int count = 1, Func<byte[], int, byte[]> converter = null)
        {
            var currentSource = sourceAddress;
            var currentDestination = destinationAddress;

            for(var line = 0; line < count; line++)
            {
                sysbus.ReadBytes(currentSource, sourceBuffer.Length, sourceBuffer, 0);
                var destinationBuffer = converter == null ? sourceBuffer : converter(sourceBuffer, line);
                sysbus.WriteBytes(destinationBuffer, currentDestination, 0, destinationBuffer.Length);

                currentSource += (ulong)(sourceBuffer.Length + sourceOffset);
                currentDestination += (ulong)(destinationBuffer.Length + destinationOffset);
            }
        }

        private readonly IBusController sysbus;
        private readonly IFlagRegisterField startFlag;
        private readonly IFlagRegisterField transferCompleteFlag;
        private readonly IEnumRegisterField<Mode> dma2dMode;
        private readonly IValueRegisterField numberOfLineField;
        private readonly IValueRegisterField pixelsPerLineField;
        private readonly DoubleWordRegister outputMemoryAddressRegister;
        private readonly DoubleWordRegister backgroundMemoryAddressRegister;
        private readonly DoubleWordRegister foregroundMemoryAddressRegister;
        private readonly IEnumRegisterField<Dma2DColorMode> outputColorModeField;
        private readonly IEnumRegisterField<Dma2DColorMode> foregroundColorModeField;
        private readonly IEnumRegisterField<Dma2DColorMode> backgroundColorModeField;
        private readonly IEnumRegisterField<Dma2DAlphaMode> backgroundAlphaMode;
        private readonly IEnumRegisterField<Dma2DAlphaMode> foregroundAlphaMode;
        private readonly IValueRegisterField backgroundColorBlueChannelField;
        private readonly IValueRegisterField backgroundColorGreenChannelField;
        private readonly IValueRegisterField backgroundColorRedChannelField;
        private readonly IValueRegisterField foregroundColorBlueChannelField;
        private readonly IValueRegisterField foregroundColorGreenChannelField;
        private readonly IValueRegisterField foregroundColorRedChannelField;
        private readonly IValueRegisterField backgroundAlphaField;
        private readonly IValueRegisterField foregroundAlphaField;
        private readonly DoubleWordRegister outputColorRegister;
        private readonly IValueRegisterField outputLineOffsetField;
        private readonly IValueRegisterField foregroundLineOffsetField;
        private readonly IValueRegisterField backgroundLineOffsetField;
        private readonly IEnumRegisterField<Dma2DColorMode> foregroundClutColorModeField;
        private readonly IEnumRegisterField<Dma2DColorMode> backgroundClutColorModeField;
        private readonly DoubleWordRegisterCollection registers;

        private byte[] outputBuffer;
        private byte[] outputLineBuffer;

        private byte[] foregroundBuffer;
        private byte[] foregroundLineBuffer;

        private byte[] backgroundBuffer;
        private byte[] backgroundLineBuffer;

        [Transient]
        private IPixelBlender blender;
        [Transient]
        private IPixelConverter bgConverter;
        [Transient]
        private IPixelConverter fgConverter;

        private const ELFSharp.ELF.Endianess Endianness = ELFSharp.ELF.Endianess.LittleEndian;

        private enum Mode
        {
            MemoryToMemory,
            MemoryToMemoryWithPfc,
            MemoryToMemoryWithBlending,
            RegisterToMemory
        }

        private enum Register : long
        {
            ControlRegister = 0x0,
            InterruptStatusRegister = 0x4,
            InterruptFlagClearRegister = 0x8,
            ForegroundMemoryAddressRegister = 0xC,
            ForegroundOffsetRegister = 0x10,
            BackgroundMemoryAddressRegister = 0x14,
            BackgroundOffsetRegister = 0x18,
            ForegroundPfcControlRegister = 0x1C,
            ForegroundColorRegister = 0x20,
            BackgroundPfcControlRegister = 0x24,
            BackgroundColorRegister = 0x28,
            ForegroundClutMemoryAddressRegister = 0x2C,
            BackgroundClutMemoryAddressRegister = 0x30,
            OutputPfcControlRegister = 0x34,
            OutputColorRegister = 0x38,
            OutputMemoryAddressRegister = 0x3C,
            OutputOffsetRegister = 0x40,
            NumberOfLineRegister = 0x44
        }
    }
}
