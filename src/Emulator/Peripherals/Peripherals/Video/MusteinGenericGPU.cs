//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2018-2022 Microchip
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
// Basic generic graphical controller without any 2D/3D acceleration.
// Example baremetal drivers located at https://github.com/AntonKrug/mustein_gpu_driver
//
// v0.2 2018/11/29 anton.krug@microchip.com SanFrancisco Summit variant
// v0.3 2019/02/13 anton.krug@microchip.com More color modes, embedded memory and added versality
//

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Video
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MusteinGenericGPU : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize
    {
        public MusteinGenericGPU(IMachine machine, MappedMemory buffer, bool registers64bitAligned = false, int controlBit = 23, uint frameBufferSize = 0x800000) : base(machine)
        {
            this.machine = machine;
            this.frameBufferSize = frameBufferSize;
            this.controlBit = controlBit;
            this.controlOffset = 1U << controlBit;
            this.is64bitAligned = registers64bitAligned;
            this.accessAligment = (registers64bitAligned) ? 8 : 4;
            this.sync = new object();
            this.underlyingBuffer = buffer;

            // Allows to switch ordering for the 8bit mode lookup table depending what is closer to the native host colorspace
            var lookupTableRgbx = false;
#if !PLATFORM_WINDOWS
            lookupTableRgbx = true;
#endif

            // Populating lookup table for the 8bit color mode to 24bit conversion, because the 332 format is unbalanced so
            // much and the Red/Green have 50% more bits than Blue the value 0xFF has a yellow tint. Balanced white (gray)
            // color is actually value 0xF6. The white color is fairly gray because the discarted least significant bits
            // still acumulate to a 1/4 of the total brightness/value (we are cutting away too many bits).
            colorTable = new uint[256];
            for(var index = 0u; index < colorTable.Length; index++)
            {
                var red = index & 0x7;
                var green = (index & 0x38) >> 3;
                var blue = (index & 0xc0) >> 6;
                var value = 0u;
                if(lookupTableRgbx)
                {
                    value = red << 21 | green << 13 | blue << 6;  // Converting RGB332 to RGB888 which will be used for RGBX8888
                }
                else
                {
                    value = red << 5  | green << 13 | blue << 22; // Converting RGB332 to BGR888 which will be used for BGRX8888
                }
                colorTable[index] = value;
            }

            colorModeToPixelFormatTable = new Dictionary<ColorMode, PixelFormat>()
            {
                { ColorMode.LowColor,  (lookupTableRgbx) ? PixelFormat.RGBX8888 : PixelFormat.BGRX8888 },
                { ColorMode.HighColor, PixelFormat.RGB565 },
                { ColorMode.TrueColor, PixelFormat.RGBX8888 }
            };

            Reconfigure(DefaultWidth, DefaultHeight, DefaultColor);

            copyPatterns = new Dictionary<Tuple<ColorMode, bool, PixelPacking>, Action>()
            {
                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.SinglePixelPerWrite), () => ConvertAndSkip(1, 3) },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(2, 2) },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.SinglePixelPerWrite), CopyFully },

                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.FullyPacked32bit), ConvertFully },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.FullyPacked32bit), CopyFully },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.FullyPacked32bit), CopyFully },

                // In a 32bit peripheral aligment mode using fully packed 64bit is ilegal and will act as fully packed 32bit
                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.FullyPacked64bit), ConvertFully },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.FullyPacked64bit), CopyFully },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.FullyPacked64bit), CopyFully },

                { Tuple.Create(ColorMode.LowColor,  true,  PixelPacking.SinglePixelPerWrite), () => ConvertAndSkip(1, 7) },
                { Tuple.Create(ColorMode.HighColor, true,  PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(2, 6) },
                { Tuple.Create(ColorMode.TrueColor, true,  PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(4, 4) },

                { Tuple.Create(ColorMode.LowColor,  true, PixelPacking.FullyPacked32bit), () => ConvertAndSkip(4, 4) },
                { Tuple.Create(ColorMode.HighColor, true, PixelPacking.FullyPacked32bit), () => CopyAndSkip(2, 4) },
                { Tuple.Create(ColorMode.TrueColor, true, PixelPacking.FullyPacked32bit), () => CopyAndSkip(4, 4) },

                { Tuple.Create(ColorMode.LowColor,  true, PixelPacking.FullyPacked64bit), ConvertFully },
                { Tuple.Create(ColorMode.HighColor, true, PixelPacking.FullyPacked64bit), CopyFully },
                { Tuple.Create(ColorMode.TrueColor, true, PixelPacking.FullyPacked64bit), CopyFully },
            };

            // Populate the control registers addresses
            GenerateRegisterCollection();
        }

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
            colorMode = default(ColorMode);
            pixelPacking = default(PixelPacking);
        }

        public void ChangePacking(PixelPacking packing)
        {
            this.Log(LogLevel.Noisy, "The display is using {0} packing format", packing);
            pixelPacking = packing;
        }

        public long Size => controlOffset * 2; // Peripheral is split into 2 equal partitions (buffer and control registers)

        protected override void Repaint()
        {
            if(copyPatterns.TryGetValue(Tuple.Create(colorMode, is64bitAligned, pixelPacking), out Action command))
            {
                lock(sync)
                {
                    command.Invoke();
                }
            }
            else
            {
                this.Log(LogLevel.Error, "Unsuported colorMode ({0}, {1}, {2}), aligment and pixel packing is used", colorMode, is64bitAligned, pixelPacking);
            }
        }

        private void Reconfigure(uint? setWidth = null, uint? setHeight = null, ColorMode? setColor = null)
        {
            var finalFormat = Format;
            if(setColor != null)
            {
                if(colorModeToPixelFormatTable.TryGetValue((ColorMode)setColor, out finalFormat))
                {
                    colorMode = (ColorMode)setColor;
                }
                else
                {
                    this.Log(LogLevel.Error, "Setting wrong color value {0}, keeping original value {1}", setColor, finalFormat);
                }
            }

            lock(sync)
            {
                base.Reconfigure((int?)setWidth, (int?)setHeight, finalFormat);
            }

            this.Log(LogLevel.Noisy, "The display is reconfigured to {0}x{1} with {2} color format (setColor ={3})", Width, Height, Format.ToString(), setColor);

            if(((setWidth != null) || (setHeight != null)) && ((Width * Height * accessAligment) > frameBufferSize))
            {
                this.Log(LogLevel.Warning, "This resolution with some (or all) pixel packing modes will not fit in the frameBuffer, if needed increase the frameBufferSize.");
            }
        }

        private void GenerateRegisterCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                { controlOffset + (long)Registers.Width * accessAligment, new DoubleWordRegister(this, DefaultWidth)
                    .WithValueField(0, 16, name: "Width", writeCallback: (_, x) => Reconfigure(setWidth: (uint)x), valueProviderCallback: _ => (uint)Width)
                },

                { controlOffset + (long)Registers.Height * accessAligment, new DoubleWordRegister(this, DefaultHeight)
                    .WithValueField(0, 16, name: "Height", writeCallback: (_, y) => Reconfigure(setHeight: (uint)y), valueProviderCallback: _ => (uint)Height)
                },

                { controlOffset + (long)Registers.Format * accessAligment, new DoubleWordRegister(this, (uint)DefaultColor | ((uint)DefaultPacking << 4))
                    .WithEnumField<DoubleWordRegister, ColorMode>(0, 4, name: "Color", writeCallback: (_, c) => Reconfigure(setColor: c), valueProviderCallback: _ => colorMode)
                    .WithEnumField<DoubleWordRegister, PixelPacking>(4, 4, name: "Packing", writeCallback: (_, c) => ChangePacking(c), valueProviderCallback: _ => pixelPacking)
                }
            };

            if(is64bitAligned)
            {
                // Dormant registers for the high 32bit accesses of the 64bit registers,
                // just so there will not be logged any unimplemented accesses
                foreach(long registerIndex in Enum.GetValues(typeof(Registers)))
                {
                    registerDictionary.Add(
                        controlOffset + registerIndex * accessAligment + 4, new DoubleWordRegister(this, 0x0)
                            .WithTag("Dormant upper 32bit of 64bit registers", 0, 32)
                    );
                }
            }
            registers = new DoubleWordRegisterCollection(this, registerDictionary);
        }

        // We can convert from the 332 format the whole lot of bytes without skipping bytes in the input buffer
        private void ConvertFully()
        {
            var colorValues = underlyingBuffer.ReadBytes(0, buffer.Length * 4);
            for(var i = 0; i < buffer.Length; ++i)
            {
                buffer[i] = 0;
                buffer[i] = colorValues[i * 4 + 1];
                buffer[i] = (byte)(colorValues[i * 4 + 2] >> 8);
                buffer[i] = (byte)(colorValues[i * 4 + 3] >> 16);
            }
        }

        // When there are parts to skip and the the rest needs to be converted from 332 format
        private void ConvertAndSkip(int bytesToCopy, int bytesToSkip)
        {
            var indexDest = 0;
            var indexSrc = 0;
            while(indexDest < buffer.Length)
            {
                for(var indexPack = 0u; indexPack < bytesToCopy; indexPack++, indexSrc++)
                {
                    HandleByte(indexSrc, ref indexDest);
                }
                indexSrc += bytesToSkip;
            }
        }

        private void HandleByte(int indexSrc, ref int indexDest)
        {
            // Each byte gets transfered via the lookup table (to transfer the 8bit 332 format to a format the backend can display
            var colorValue = colorTable[underlyingBuffer.ReadDoubleWord(indexSrc)];
            buffer[indexDest++] = 0;
            buffer[indexDest++] = (byte)(colorValue);
            buffer[indexDest++] = (byte)(colorValue >> 8);
            buffer[indexDest++] = (byte)(colorValue >> 16);
        }

        // We can copy the whole lot as it is. No conversion between the bytes
        private void CopyFully()
        {
            var bytes = underlyingBuffer.ReadBytes(0, buffer.Length);
            for(var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = bytes[index];
            }
        }

        // When there are parts to skip but no conversion needed
        private void CopyAndSkip(int bytesToCopy, int bytesToSkip)
        {
            var indexDest = 0;
            var indexSrc  = 0;
            while(indexDest < buffer.Length)
            {
                for(var indexPack = 0; indexPack < bytesToCopy; indexPack++, indexDest++, indexSrc++)
                {
                    buffer[indexDest] = underlyingBuffer.ReadByte(indexSrc);
                }
                indexSrc += bytesToSkip;
            }
        }

        private DoubleWordRegisterCollection registers;
        private object sync;
        private ColorMode colorMode;
        private PixelPacking pixelPacking;
        private readonly MappedMemory underlyingBuffer;

        private readonly IMachine machine;
        private readonly int accessAligment;
        private readonly bool is64bitAligned;
        private readonly int controlBit;
        private readonly uint controlOffset;
        private readonly uint frameBufferSize;
        private readonly uint[] colorTable;
        private readonly Dictionary<Tuple<ColorMode, bool, PixelPacking>, Action> copyPatterns;
        private readonly Dictionary<ColorMode, PixelFormat>  colorModeToPixelFormatTable;

        private const int Alignment = 0x1000;
        private const uint DefaultWidth = 128;
        private const uint DefaultHeight = 128;
        private const ColorMode DefaultColor = ColorMode.TrueColor;
        private const PixelPacking DefaultPacking = PixelPacking.SinglePixelPerWrite;

        public enum PixelPacking : uint
        {
            SinglePixelPerWrite = 0,
            FullyPacked32bit = 1,
            FullyPacked64bit = 2
        }

        private enum Registers : long
        {
            Width = 0x0,
            Height = 0x1,
            Format = 0x2
        }

        private enum ColorMode : uint
        {
            LowColor = 0,   // 8-bit  per pixel, 3-bits Red, 3-bits Green and 2-bits Blue
            HighColor = 1,  // 16-bit per pixel, 5-bits Red, 6-bits Green and 5-bits Blue
            TrueColor = 2   // 32-bit per pixel, 8-bits Red, 8-bits Green and 8-bits Blue
        }
    }
}
