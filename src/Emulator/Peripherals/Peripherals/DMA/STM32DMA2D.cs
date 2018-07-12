//
// Copyright (c) 2010-2018 Antmicro
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

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32DMA2D : IDoubleWordPeripheral, IKnownSize
    {
        public STM32DMA2D(Machine machine) : this()
        {
            this.machine = machine;
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
            var controlRegister = new DoubleWordRegister(this);
            startFlag = controlRegister.DefineFlagField(0, FieldMode.Read | FieldMode.Write, name: "Start", changeCallback: (old, @new) => { if(@new) DoTransfer(); });
            dma2dMode = controlRegister.DefineEnumField<Mode>(16, 2, FieldMode.Read | FieldMode.Write, name: "Mode");

            var foregroundClutMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);
            var backgroundClutMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);

            var interruptFlagClearRegister = new DoubleWordRegister(this).WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "CTCIF", changeCallback: (old, @new) => { if(!@new) IRQ.Unset(); });

            var numberOfLineRegister = new DoubleWordRegister(this);
            numberOfLineField = numberOfLineRegister.DefineValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "NL");
            pixelsPerLineField = numberOfLineRegister.DefineValueField(16, 14, FieldMode.Read | FieldMode.Write, name: "PL", 
                writeCallback: (_, __) => 
                { 
                    HandleOutputBufferSizeChange(); 
                    HandleBackgroundBufferSizeChange();
                    HandleForegroundBufferSizeChange();
                });

            outputMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);
            backgroundMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);
            foregroundMemoryAddressRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);

            var outputPfcControlRegister = new DoubleWordRegister(this);
            outputColorModeField = outputPfcControlRegister.DefineEnumField<Dma2DColorMode>(0, 3, FieldMode.Read | FieldMode.Write, name: "CM", 
                writeCallback: (_, __) => 
                { 
                    HandlePixelFormatChange(); 
                    HandleOutputBufferSizeChange(); 
                });

            var foregroundPfcControlRegister = new DoubleWordRegister(this);
            foregroundColorModeField = foregroundPfcControlRegister.DefineEnumField<Dma2DColorMode>(0, 4, FieldMode.Read | FieldMode.Write, name: "CM", 
                writeCallback: (_, __) => 
                {
                    HandlePixelFormatChange(); 
                    HandleForegroundBufferSizeChange(); 
                });
            var foregroundClutSizeField = foregroundPfcControlRegister.DefineValueField(8, 8, FieldMode.Read | FieldMode.Write, name: "CS");
            foregroundClutColorModeField = foregroundPfcControlRegister.DefineEnumField<Dma2DColorMode>(4, 1, FieldMode.Read | FieldMode.Write, name: "CCM",
                changeCallback: (_, __) =>
                {
                    HandlePixelFormatChange();
                });
            foregroundPfcControlRegister.DefineFlagField(5, FieldMode.Read, name: "START",
                writeCallback: (_, value) =>
            {
                if(!value)
                {
                    return;
                }

                foregroundClut = new byte[(foregroundClutSizeField.Value + 1) * foregroundClutColorModeField.Value.ToPixelFormat().GetColorDepth()];
                machine.SystemBus.ReadBytes(foregroundClutMemoryAddressRegister.Value, foregroundClut.Length, foregroundClut, 0, true);
            });

            var backgroundPfcControlRegister = new DoubleWordRegister(this);
            backgroundColorModeField = backgroundPfcControlRegister.DefineEnumField<Dma2DColorMode>(0, 4, FieldMode.Read | FieldMode.Write, name: "CM", 
                writeCallback: (_, __) => 
                { 
                    HandlePixelFormatChange(); 
                    HandleBackgroundBufferSizeChange(); 
                });
            var backgroundClutSizeField = backgroundPfcControlRegister.DefineValueField(8, 8, FieldMode.Read | FieldMode.Write, name: "CS");
            backgroundClutColorModeField = backgroundPfcControlRegister.DefineEnumField<Dma2DColorMode>(4, 1, FieldMode.Read | FieldMode.Write, name: "CCM",
                changeCallback: (_, __) =>
                {
                    HandlePixelFormatChange();
                });
            backgroundPfcControlRegister.DefineFlagField(5, FieldMode.Read, name: "START",
                writeCallback: (_, value) =>
            {
                if(!value)
                {
                    return;
                }

                backgroundClut = new byte[(backgroundClutSizeField.Value + 1) * backgroundClutColorModeField.Value.ToPixelFormat().GetColorDepth()];
                machine.SystemBus.ReadBytes(backgroundClutMemoryAddressRegister.Value, backgroundClut.Length, backgroundClut, 0, true);
            });

            outputColorRegister = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read | FieldMode.Write);

            var outputOffsetRegister = new DoubleWordRegister(this);
            outputLineOffsetField = outputOffsetRegister.DefineValueField(0, 14, FieldMode.Read | FieldMode.Write, name: "LO");

            var foregroundOffsetRegister = new DoubleWordRegister(this);
            foregroundLineOffsetField = foregroundOffsetRegister.DefineValueField(0, 14, FieldMode.Read | FieldMode.Write, name: "LO");

            var backgroundOffsetRegister = new DoubleWordRegister(this);
            backgroundLineOffsetField = backgroundOffsetRegister.DefineValueField(0, 14, FieldMode.Read | FieldMode.Write, name: "LO");

            var regs = new Dictionary<long, DoubleWordRegister>
            {
                { (long)Register.ControlRegister, controlRegister },
                { (long)Register.InterruptFlagClearRegister, interruptFlagClearRegister },
                { (long)Register.ForegroundMemoryAddressRegister, foregroundMemoryAddressRegister },
                { (long)Register.ForegroundOffsetRegister, foregroundOffsetRegister },
                { (long)Register.BackgroundMemoryAddressRegister, backgroundMemoryAddressRegister },
                { (long)Register.BackgroundOffsetRegister, backgroundOffsetRegister },
                { (long)Register.ForegroundPfcControlRegister, foregroundPfcControlRegister },
                { (long)Register.BackgroundPfcControlRegister, backgroundPfcControlRegister },
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
            outputBuffer = new byte[numberOfLineField.Value * pixelsPerLineField.Value * outputFormatColorDepth];
            outputLineBuffer = new byte[pixelsPerLineField.Value * outputFormatColorDepth];
        }

        private void HandleBackgroundBufferSizeChange()
        {
            var backgroundFormatColorDepth = backgroundColorModeField.Value.ToPixelFormat().GetColorDepth();
            backgroundBuffer = new byte[pixelsPerLineField.Value * numberOfLineField.Value * backgroundFormatColorDepth];
            backgroundLineBuffer = new byte[pixelsPerLineField.Value * backgroundFormatColorDepth];
        }

        private void HandleForegroundBufferSizeChange()
        {
            var foregroundFormatColorDepth = foregroundColorModeField.Value.ToPixelFormat().GetColorDepth();
            foregroundBuffer = new byte[pixelsPerLineField.Value * numberOfLineField.Value * foregroundFormatColorDepth];
            foregroundLineBuffer = new byte[pixelsPerLineField.Value * foregroundFormatColorDepth];
        }

        private void HandlePixelFormatChange()
        {
            var outputFormat = outputColorModeField.Value.ToPixelFormat();
            var backgroundFormat = backgroundColorModeField.Value.ToPixelFormat();
            var foregroundFormat = foregroundColorModeField.Value.ToPixelFormat();

            converter = PixelManipulationTools.GetConverter(foregroundFormat, Endianness, outputFormat, Endianness, foregroundClutColorModeField.Value.ToPixelFormat());
            blender = PixelManipulationTools.GetBlender(backgroundFormat, Endianness, foregroundFormat, Endianness, outputFormat, Endianness, foregroundClutColorModeField.Value.ToPixelFormat(), backgroundClutColorModeField.Value.ToPixelFormat());
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
                        machine.SystemBus.WriteBytes(outputBuffer, outputMemoryAddressRegister.Value);
                    }
                    else
                    {
                        // we have to copy per line
                        var lineWidth = (int)(pixelsPerLineField.Value * outputFormat.GetColorDepth());
                        var offset = lineWidth + (outputLineOffsetField.Value * outputFormat.GetColorDepth());
                        for(var line = 0; line < numberOfLineField.Value; line++)
                        {
                            machine.SystemBus.WriteBytes(outputBuffer, (ulong)(outputMemoryAddressRegister.Value + line * offset), line * lineWidth, lineWidth);
                        }
                    }
                break;
                case Mode.MemoryToMemoryWithBlending:
                    if(outputLineOffsetField.Value == 0 && foregroundLineOffsetField.Value == 0 && backgroundLineOffsetField.Value == 0)
                    {
                        // we can optimize here and copy everything at once
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value, foregroundBuffer,
                               converter: (localForegroundBuffer, line) =>
                               {
                                   machine.SystemBus.ReadBytes(backgroundMemoryAddressRegister.Value, backgroundBuffer.Length, backgroundBuffer, 0);
                                   // per-pixel alpha blending
                                   blender.Blend(backgroundBuffer, backgroundClut, localForegroundBuffer, foregroundClut, ref outputBuffer);
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
                                    machine.SystemBus.ReadBytes((ulong)(backgroundMemoryAddressRegister.Value + line * (backgroundLineOffsetField.Value + pixelsPerLineField.Value) * backgroundFormat.GetColorDepth()), backgroundLineBuffer.Length, backgroundLineBuffer, 0);
                                    blender.Blend(backgroundLineBuffer, backgroundClut, localForegroundBuffer, foregroundClut, ref outputLineBuffer);
                                    return outputLineBuffer;
                                });
                    }
                break;
                case Mode.MemoryToMemoryWithPfc:
                    if(outputLineOffsetField.Value == 0 && foregroundLineOffsetField.Value == 0 && backgroundLineOffsetField.Value == 0)
                    {
                        DoCopy(foregroundMemoryAddressRegister.Value, outputMemoryAddressRegister.Value,
                                foregroundBuffer,
                                converter: (localForegroundBuffer, line) =>
                                {
                                    converter.Convert(localForegroundBuffer, foregroundClut, ref outputBuffer);
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
                                    converter.Convert(localForegroundBuffer, foregroundClut, ref outputLineBuffer);
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
            IRQ.Set();
        }

        private void DoCopy(ulong sourceAddress, ulong destinationAddress, byte[] sourceBuffer, int sourceOffset = 0, int destinationOffset = 0, int count = 1, Func<byte[], int, byte[]> converter = null)
        {
            var currentSource = sourceAddress;
            var currentDestination = destinationAddress;

            for(var line = 0; line < count; line++)
            {
                machine.SystemBus.ReadBytes(currentSource, sourceBuffer.Length, sourceBuffer, 0);
                var destinationBuffer = converter == null ? sourceBuffer : converter(sourceBuffer, line);
                machine.SystemBus.WriteBytes(destinationBuffer, currentDestination, 0, destinationBuffer.Length);

                currentSource += (ulong)(sourceBuffer.Length + sourceOffset);
                currentDestination += (ulong)(destinationBuffer.Length + destinationOffset);
            }
        }

        private readonly Machine machine;
        private readonly IFlagRegisterField startFlag;
        private readonly IEnumRegisterField<Mode> dma2dMode;
        private readonly IValueRegisterField numberOfLineField;
        private readonly IValueRegisterField pixelsPerLineField;
        private readonly DoubleWordRegister outputMemoryAddressRegister;
        private readonly DoubleWordRegister backgroundMemoryAddressRegister;
        private readonly DoubleWordRegister foregroundMemoryAddressRegister;
        private readonly IEnumRegisterField<Dma2DColorMode> outputColorModeField;
        private readonly IEnumRegisterField<Dma2DColorMode> foregroundColorModeField;
        private readonly IEnumRegisterField<Dma2DColorMode> backgroundColorModeField;
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

        private IPixelBlender blender;
        private IPixelConverter converter;

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
            InterruptFlagClearRegister = 0x8,
            ForegroundMemoryAddressRegister = 0xC,
            ForegroundOffsetRegister = 0x10,
            BackgroundMemoryAddressRegister = 0x14,
            BackgroundOffsetRegister = 0x18,
            ForegroundPfcControlRegister = 0x1C,
            BackgroundPfcControlRegister = 0x24,
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
