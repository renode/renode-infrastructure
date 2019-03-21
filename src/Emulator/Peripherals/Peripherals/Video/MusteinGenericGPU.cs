//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
// Basic generic graphical controller without any 2D/3D acceleration.
// Example baremetal drivers located at https://github.com/AntonKrug/mustein_gpu_driver
//
// v0.2 2018/11/29 anton.krug@microchip.com SanFrancisco Summit variant
// v0.3 2019/02/13 anton.krug@microchip.com More color modes, embedded memory and added versality

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using System.Runtime.InteropServices;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System;

namespace Antmicro.Renode.Peripherals.Video {
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)] // allow byte aligned accesses on whole peripheral   
    public class MusteinGenericGPU : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize, IMapped {

        public MusteinGenericGPU(Machine machine, bool registers64bitAligned = false, uint controlBit = 23,
                                                         uint frameBufferSize = 0x800000) : base(machine) {

            this.machine         = machine;
            this.frameBufferSize = (int)frameBufferSize; // default value 0x800000 is enough for 1024x1024 pixels with 64-bit aligment per pixel
            this.controlBit      = controlBit; // All acceses with this bit set are handled by the control registers
            this.controlOffset   = 1L << (int)controlBit; // All acceses smaller than this value are going to the buffer
            this.is64bitAligned  = registers64bitAligned; // Are control register aligned to 32bit or 64bit
            this.accessAligment  = (registers64bitAligned) ? 8 : 4; // To what bytes the control registers are aligned
            this.sync            = new object();

            // Allows to switch ordering for the 8bit mode lookup table depending what is closer to the native host colorspace
#if PLATFORM_WINDOWS
            this.lookupTableRgbx = false;
#else
            this.lookupTableRgbx = true;
#endif

            // Populating lookup table for the 8bit color mode to 24bit conversion, because the 332 format is unbalanced so 
            // much and the Red/Green have 50% more bits than Blue the value 0xFF has a yellow tint. Balanced white (gray) 
            // color is actually value 0xF6. The white color is fairly gray because the discarted least significant bits
            // still acumulate to a 1/4 of the total brightness/value (we are cutting away too many bits).
            colorTable = new uint[256];
            for (uint index = 0; index < 256; index++) {
                uint blue  = (index & 0xc0) >> 6;
                uint green = (index & 0x38) >> 3;
                uint red   = index & 0x7;
                uint value;
                if (lookupTableRgbx) {
                    value = red << 21 | green << 13 | blue << 6;  // Converting RGB332 to RGB888 which will be used for RGBX8888
                } else {
                    value = red << 5  | green << 13 | blue << 22; // Converting RGB332 to BGR888 which will be used for BGRX8888
                }
                colorTable[index] = value;
                //this.Log(LogLevel.Noisy, string.Format("colorTable[{0}] = 0x{1:X};", index, value));
            }

            // Populating transfer colorModeToPixelFormat
            colorModeToPixelFormatTable = new Dictionary<ColorMode, PixelFormat>() {
                {ColorMode.LowColor,  (lookupTableRgbx) ? PixelFormat.RGBX8888 : PixelFormat.BGRX8888},
                {ColorMode.HighColor, PixelFormat.RGB565},
                {ColorMode.TrueColor, PixelFormat.RGBX8888}
            };

            // Allocate and align the framebuffer memory. We are hardcoding the mapped segment between addresses 0 and <size> 
            // the WriteDoubleWord is not going to be invoked for these addresses and the raw data will go directly to the 
            // frame buffer. The MappedSegment increased the complexity of the code, but performs magnitude faster.
            describedFbSegments    = new IMappedSegment[1];
            describedFbSegments[0] = new MusteinSegment(this, frameBufferSize);

            this.NoisyLog("Allocate memory {0} bytes big with aligment margin of {1}. Together it's {2} bytes.", 
                          frameBufferSize, alignment, frameBufferSize + alignment);
            
            var allocSeg              = Marshal.AllocHGlobal(this.frameBufferSize + alignment);
            var originalPointer       = (long)allocSeg;
            var alignedPointer        = (IntPtr)((originalPointer + alignment) & ~(alignment - 1));
            musteinFramebufferSegment = alignedPointer;

            this.NoisyLog(string.Format("FB alloc @ 0x{0:X} (aligned to 0x{1:X}, even after aligment there is at least 0x{2:X} bytes avaiable).", 
                                        allocSeg.ToInt64(), alignedPointer.ToInt64(), frameBufferSize));

            // Populate the buffer with zeros, on bigger buffers and slower perfomance, use the memSet DLLimport.
            // https://stackoverflow.com/questions/1897555
            for (int index = 0; index < this.frameBufferSize; index++) {
                Marshal.WriteByte(alignedPointer + index, 0);
            }

            // Configuire the base class Video width/height/color
            Reconfigure(defaultWidth, defaultHeight, defaultColor);

            // Populate a transfer function for each color, alignment and packing modes 
            copyPatterns = new Dictionary<Tuple<ColorMode, bool, PixelPacking>, Action>() {
                // <colors, is64bitAlignedPeripheral, packingMode> = CommandActionToExecute

                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.SinglePixelPerWrite), () => ConvertAndSkip(1, 3) },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(   2, 2) },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.SinglePixelPerWrite), CopyFully },

                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.FullyPacked32bit),    ConvertFully },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.FullyPacked32bit),    CopyFully },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.FullyPacked32bit),    CopyFully },

                // In a 32bit peripheral aligment mode using fully packed 64bit is ilegal and will act as fully packed 32bit
                { Tuple.Create(ColorMode.LowColor,  false, PixelPacking.FullyPacked64bit),    ConvertFully },
                { Tuple.Create(ColorMode.HighColor, false, PixelPacking.FullyPacked64bit),    CopyFully },
                { Tuple.Create(ColorMode.TrueColor, false, PixelPacking.FullyPacked64bit),    CopyFully },

                { Tuple.Create(ColorMode.LowColor,  true,  PixelPacking.SinglePixelPerWrite), () => ConvertAndSkip(1, 7) },
                { Tuple.Create(ColorMode.HighColor, true,  PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(   2, 6) },
                { Tuple.Create(ColorMode.TrueColor, true,  PixelPacking.SinglePixelPerWrite), () => CopyAndSkip(   4, 4) },

                { Tuple.Create(ColorMode.LowColor,  true, PixelPacking.FullyPacked32bit),     () => ConvertAndSkip(4, 4) },
                { Tuple.Create(ColorMode.HighColor, true, PixelPacking.FullyPacked32bit),     () => CopyAndSkip(   2, 4) },
                { Tuple.Create(ColorMode.TrueColor, true, PixelPacking.FullyPacked32bit),     () => CopyAndSkip(   4, 4) },

                { Tuple.Create(ColorMode.LowColor,  true, PixelPacking.FullyPacked64bit),     ConvertFully },
                { Tuple.Create(ColorMode.HighColor, true, PixelPacking.FullyPacked64bit),     CopyFully },
                { Tuple.Create(ColorMode.TrueColor, true, PixelPacking.FullyPacked64bit),     CopyFully },
            };

            // Populate the control registers addresses
            GenerateRegisterCollection();
        }

        public long Size => controlOffset * 2; // Peripheral is split into 2 equal partitions (buffer and control registers)

        public IEnumerable<IMappedSegment> MappedSegments => describedFbSegments;

        private void Reconfigure(uint? setWidth = null, uint? setHeight = null, ColorMode? setColor = null) {

            PixelFormat finalFormat = Format;
            if (setColor != null) {
                // Change the color only if it's not null
                if (colorModeToPixelFormatTable.TryGetValue((ColorMode)setColor, out finalFormat)) {
                    colorMode = (ColorMode)setColor;
                } else {
                    this.Log(LogLevel.Error, "Setting wrong color value {0}, keeping original value {1}", 
                             setColor, finalFormat);
                }
            }

            lock (sync) {
                base.Reconfigure((int?)setWidth, (int?)setHeight, finalFormat); // use inherited Reconfigurator
            }

            this.Log(LogLevel.Noisy, "The display is reconfigured to {0}x{1} with {2} color format (setColor ={3})",
                     Width, Height, Format.ToString(), setColor);

            if ((setWidth != null || setHeight != null) &&Width * Height * accessAligment > frameBufferSize) {
                this.Log(LogLevel.Warning, "This resolution with some (or all) pixel packing modes will not fit in the frameBuffer, if needed increase the frameBufferSize.");
            }
        }

        public void ChangePacking(PixelPacking packing) {
            this.Log(LogLevel.Noisy, "The display is using {0} packing format", packing);
            pixelPacking = packing;
        }

        private void GenerateRegisterCollection() {
            var registerDictionary = new Dictionary<long, DoubleWordRegister> {     

                // Register to change width
                {controlOffset + (long)Registers.Width * accessAligment, new DoubleWordRegister(this, defaultWidth)
                    .WithValueField(0,  16, name: "Width",
                        writeCallback: (_, x) => Reconfigure(setWidth:  x ), valueProviderCallback: _ => (uint)Width)
                },

                // Register to change height
                {controlOffset + (long)Registers.Height * accessAligment, new DoubleWordRegister(this, defaultHeight)
                    .WithValueField(0, 16, name: "Height",
                        writeCallback: (_, y) => Reconfigure(setHeight: y ), valueProviderCallback: _ => (uint)Height)
                },

                // Register to change color format and pixel mapping        
                {controlOffset + (long)Registers.Format * accessAligment, new DoubleWordRegister(this, (uint)defaultColor | ((uint)defaultPacking << 4))
                    .WithEnumField<DoubleWordRegister, ColorMode>(   0, 4, name: "Color",   writeCallback: (_, c) => Reconfigure(setColor: c))
                    .WithEnumField<DoubleWordRegister, PixelPacking>(4, 4, name: "Packing", writeCallback: (_, c) => ChangePacking(c))
                },
            };

            if (is64bitAligned) {
                // Dormant registers for the high 32bit accesses of the 64bit registers, 
                // just so there will not be logged any unimplemented accesses 

                foreach (long registerIndex in Enum.GetValues(typeof(Registers))) {
                    registerDictionary.Add(
                        controlOffset + registerIndex * accessAligment + 4, new DoubleWordRegister(this, 0x0)
                            .WithTag("Dormant upper 32bit of 64bit registers", 0, 32)
                    );
                }
            }

            registerHandler = new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public void WriteDoubleWord(long address, uint value) => registerHandler.Write(address, value);

        public uint ReadDoubleWord(long offset) => registerHandler.Read(offset);

        public override void Reset() => registerHandler.Reset();

        protected override void Repaint() {
            if (copyPatterns.TryGetValue(Tuple.Create(colorMode, is64bitAligned, pixelPacking), out Action command)) {
                lock (sync) {
                    command.Invoke();
                }
            } else {
                // Shouldn't ever reach this point as all possible options are populated. And colorMode is checked for 
                // correct enum value when it's mutated
                this.Log(LogLevel.Error, "Unsuported colorMode ({0}, {1}, {2}), aligment and pixel packing is used", 
                         colorMode, is64bitAligned, pixelPacking);
            }
        }

        // We can convert from the 332 format the whole lot of bytes without skipping bytes in the input buffer
        private void ConvertFully() {
            for (int indexSrc = 0, indexDest = 0; indexDest < buffer.Length; indexSrc++) {
                // Each byte gets transfered via the lookup table (to transfer the 8bit 332 format to a format the backend can display
                uint colorValue = colorTable[Marshal.ReadByte(musteinFramebufferSegment + indexSrc)];
                buffer[indexDest++] = 0;
                buffer[indexDest++] = (byte)(colorValue);
                buffer[indexDest++] = (byte)(colorValue >> 8);
                buffer[indexDest++] = (byte)(colorValue >> 16);
            }
        }

        // When there are parts to skip and the the rest needs to be converted from 332 format
        private void ConvertAndSkip(int bytesToCopy, int bytesToSkip) {
            int indexDest = 0;
            int indexSrc = 0;
            while (indexDest < buffer.Length) {
                for (uint indexPack = 0; indexPack < bytesToCopy; indexPack++, indexSrc++) {
                    // Each byte gets transfered via the lookup table (to transfer the 8bit 332 format to a format the backend can display
                    uint colorValue = colorTable[Marshal.ReadByte(musteinFramebufferSegment + indexSrc)];
                    buffer[indexDest++] = 0;
                    buffer[indexDest++] = (byte)(colorValue);
                    buffer[indexDest++] = (byte)(colorValue >> 8);
                    buffer[indexDest++] = (byte)(colorValue >> 16);
                }
                indexSrc += bytesToSkip;
            }
        }

        // We can copy the whole lot as it is. No conversion between the bytes
        private void CopyFully() {
            for (int index = 0; index < buffer.Length; index++) {
                buffer[index] = Marshal.ReadByte(musteinFramebufferSegment + index);
            }
        }

        // When there are parts to skip but no conversion needed
        private void CopyAndSkip(int bytesToCopy, int bytesToSkip) {
            int indexDest = 0;
            int indexSrc  = 0;
            while (indexDest < buffer.Length) {
                for (int indexPack = 0; indexPack < bytesToCopy; indexPack++, indexDest++, indexSrc++) {
                    buffer[indexDest] = Marshal.ReadByte(musteinFramebufferSegment + indexSrc);
                }
                indexSrc += bytesToSkip;
            }
        }

        private enum Registers : long {
            // These will get multiplied by the accessAligment which is depending on the registers64bitAligned
            Width  = 0x0,
            Height = 0x1,
            Format = 0x2
        }

        public enum PixelPacking : uint {
            SinglePixelPerWrite = 0,
            FullyPacked32bit    = 1,
            FullyPacked64bit    = 2
        }

        public enum ColorMode : uint {
            LowColor  = 0,  // 8-bit  per pixel, 3-bits Red, 3-bits Green and 2-bits Blue
            HighColor = 1,  // 16-bit per pixel, 5-bits Red, 6-bits Green and 5-bits Blue
            TrueColor = 2   // 32-bit per pixel, 8-bits Red, 8-bits Green and 8-bits Blue
        }

        // Default reset values of the control registers
        private const int          alignment      = 0x1000; // Alignment of the framebuffer
        private const uint         defaultWidth   = 128;
        private const uint         defaultHeight  = 128;
        private const ColorMode    defaultColor   = ColorMode.TrueColor;
        private const PixelPacking defaultPacking = PixelPacking.SinglePixelPerWrite;

        private readonly Machine          machine;
        private readonly int              accessAligment;
        private readonly bool             is64bitAligned;
        private readonly bool             lookupTableRgbx;
        private readonly uint             controlBit;
        private readonly long             controlOffset;
        private readonly int              frameBufferSize;
        private readonly uint[]           colorTable;
        private readonly IntPtr           musteinFramebufferSegment;
        private readonly IMappedSegment[] describedFbSegments;

        private DoubleWordRegisterCollection registerHandler;
        private object                       sync;
        private ColorMode                    colorMode;
        private PixelPacking                 pixelPacking;

        // Lookup tables
        private readonly Dictionary<Tuple<ColorMode, bool, PixelPacking>, Action>       copyPatterns;
        private readonly Dictionary<ColorMode,                            PixelFormat>  colorModeToPixelFormatTable;

        // Mapping segment of peripheral directly to the memory so it will not go trough WriteDoubleWord
        private class MusteinSegment : IMappedSegment {
            private readonly MusteinGenericGPU parent;
            private readonly uint              size;

            public MusteinSegment(MusteinGenericGPU parent, uint size) {
                this.parent = parent;
                this.size   = size;
            }

            public IntPtr Pointer => parent.musteinFramebufferSegment;

            public ulong Size => size;

            public ulong StartingOffset => 0; // Hardcoding the offset to 0

            public void Touch() {
                // Frame buffer memory is fixed and pre-allocated so no dynamic allocation is needed
            }

            public override string ToString() => string.Format("[MusteinSegment: Size=0x{0:X}]", Size);
        }

    }


}
