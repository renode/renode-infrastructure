//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;

using Antmicro.Renode.Utilities;

using ELFSharp.ELF;

namespace Antmicro.Renode.Backends.Display
{
    public static class PixelManipulationTools
    {
        public static IPixelConverter GetConverter(PixelFormat inputFormat, Endianess inputEndianess, PixelFormat outputFormat, Endianess outputEndianess, int? pitch = null, int? height = null, PixelFormat? clutInputFormat = null, Pixel? inputFixedColor = null /* fixed color for A4 and A8 mode */)
        {
            if(inputFormat.IsPlanar() && (pitch == null || height == null))
            {
                throw new ArgumentNullException($"Pitch and height must be known to convert from a planar format such as {inputFormat}");
            }
            var inputBufferDescriptor =  new BufferDescriptor
            {
                ColorFormat = inputFormat,
                ClutColorFormat = clutInputFormat,
                FixedColor = inputFixedColor,
                DataEndianness = inputEndianess,
                Pitch = pitch,
                Height = height,
            };
            var outputBufferDescriptor = new BufferDescriptor
            {
                ColorFormat = outputFormat,
                DataEndianness = outputEndianess
            };
            var converterConfiguration = Tuple.Create(inputBufferDescriptor, outputBufferDescriptor);
            return convertersCache.GetOrAdd(converterConfiguration, (_) =>
                new PixelConverter(inputFormat, outputFormat, GenerateConvertMethod(inputBufferDescriptor, outputBufferDescriptor)));
        }

        public static IPixelBlender GetBlender(PixelFormat backBuffer, Endianess backBufferEndianess, PixelFormat frontBuffer, Endianess frontBufferEndianes, PixelFormat output, Endianess outputEndianess, PixelFormat? clutForegroundFormat = null, PixelFormat? clutBackgroundFormat = null, Pixel? bgFixedColor = null, Pixel? fgFixedColor = null)
        {
            var blenderConfiguration = Tuple.Create(backBuffer, backBufferEndianess, frontBuffer, frontBufferEndianes, output, outputEndianess, bgFixedColor, fgFixedColor);
            return blendersCache.GetOrAdd(blenderConfiguration, (_) =>
                new PixelBlender(backBuffer, frontBuffer, output, GenerateBlendMethod(
                    new BufferDescriptor
                    {
                        ColorFormat = backBuffer,
                        ClutColorFormat = clutBackgroundFormat,
                        FixedColor = bgFixedColor,
                        DataEndianness = backBufferEndianess
                    },
                    new BufferDescriptor
                    {
                        ColorFormat = frontBuffer,
                        FixedColor = fgFixedColor,
                        ClutColorFormat = clutForegroundFormat,
                        DataEndianness = frontBufferEndianes
                    },
                    new BufferDescriptor
                    {
                        ColorFormat = output,
                        DataEndianness = outputEndianess
                    })));
        }

        private static uint Blend(uint source, uint dest, byte ratio)
        {
            return (uint)(((ulong)source * (byte)(0xff - ratio) + (ulong)dest * ratio) / 0xff);
        }

        private static Pixel Blend(Pixel dest, Pixel source)
        {
            if(dest.Alpha == 0)
            {
                return source;
            }
            if(source.Alpha == 0)
            {
                return dest;
            }
            var alpha = (byte)Blend(dest.Alpha, 0xff, source.Alpha);
            return new Pixel(
                (byte)(Blend((uint)dest.Red * dest.Alpha, (uint)source.Red * 0xff, source.Alpha) / alpha),
                (byte)(Blend((uint)dest.Green * dest.Alpha, (uint)source.Green * 0xff, source.Alpha) / alpha),
                (byte)(Blend((uint)dest.Blue * dest.Alpha, (uint)source.Blue * 0xff, source.Alpha) / alpha),
                alpha
            );
        }

        private static BlendDelegate GenerateBlendMethod(BufferDescriptor backBufDesc, BufferDescriptor frontBufDesc, BufferDescriptor outBufDesc)
        {
            var fromBack = GenerateFrom(backBufDesc);
            var fromFront = GenerateFrom(frontBufDesc);
            var toOut = GenerateTo(outBufDesc);
            return (byte[] backBuf, byte[] backClutBuf, byte[] frontBuf, byte[] frontClutBuf, ref byte[] outBuf, Pixel background, byte backAlpha, PixelBlendingMode backMode, byte frontAlpha, PixelBlendingMode frontMode) =>
            {
                var byteLen = backBufDesc.ColorFormat.IsPlanar() ? backBufDesc.Pitch.Value * backBufDesc.Height.Value : backBuf.Length;
                var stepLen = (int)backBufDesc.ColorFormat.GetPixelCount((ulong)byteLen);
                for(var pos = 0; pos < stepLen; pos += 1)
                {
                    var back = fromBack(backBuf, backClutBuf, pos);
                    back = ApplyAlpha(back, backMode, backAlpha);
                    var front = fromFront(frontBuf, frontClutBuf, pos);
                    front = ApplyAlpha(front, frontMode, frontAlpha);
                    var outColor = Blend(Blend(background, back), front);
                    toOut(outBuf, pos, outColor);
                }
            };
        }

        private static Pixel ApplyAlpha(Pixel pixel, PixelBlendingMode mode, byte alpha)
        {
            switch(mode)
            {
            case PixelBlendingMode.Multiply:
                pixel.Alpha = (byte)(pixel.Alpha * alpha / 0xFF);
                break;
            case PixelBlendingMode.Replace:
                pixel.Alpha = alpha;
                break;
            }
            return pixel;
        }

        private static ConvertDelegate GenerateConvertMethod(BufferDescriptor inBufDesc, BufferDescriptor outBufDesc)
        {
            var fromConv = GenerateFrom(inBufDesc);
            var toConv = GenerateTo(outBufDesc);
            return (byte[] inBuffer, byte[] clutBuffer, byte alpha, PixelBlendingMode alphaReplaceMode, ref byte[] outBuffer) =>
            {
                var byteLen = inBufDesc.ColorFormat.IsPlanar() ? inBufDesc.Pitch.Value * inBufDesc.Height.Value : inBuffer.Length;
                var stepLen = byteLen * 8 / inBufDesc.ColorFormat.GetColorDepth();
                for(var pos = 0; pos < stepLen; pos += 1)
                {
                    var color = fromConv(inBuffer, clutBuffer, pos);
                    color = ApplyAlpha(color, alphaReplaceMode, alpha);
                    toConv(outBuffer, pos, color);
                }
            };
        }

        delegate Pixel ConvertFrom(byte[] inbuffer, byte[] clutBuffer, int position);

        private static ConvertFrom GenerateFrom(BufferDescriptor inputBufferDescriptor)
        {
            var endianess = inputBufferDescriptor.DataEndianness;
            var format = inputBufferDescriptor.ColorFormat;

            var fromClut = inputBufferDescriptor.ClutColorFormat == null ?
                null :
                GenerateFrom(new BufferDescriptor
                {
                    ColorFormat = inputBufferDescriptor.ClutColorFormat.Value,
                    DataEndianness = inputBufferDescriptor.DataEndianness,
                    FixedColor = inputBufferDescriptor.FixedColor
                });

            switch(format)
            {
            case PixelFormat.A4:
                return (inBuf, _, pos) =>
                {
                    var alpha = (pos & 1) != 0 ? inBuf[pos / 2] >> 4 : inBuf[pos / 2] & 0b00001111;
                    var baseColor = inputBufferDescriptor.FixedColor.Value;
                    baseColor.Alpha = Inflate(alpha, 4);
                    return baseColor;
                };
            case PixelFormat.L4:
                return (inBuf, clutBuf, pos) =>
                {
                    var luminance = (pos & 1) != 0 ? inBuf[pos / 2] >> 4 : inBuf[pos / 2] & 0b00001111;
                    // The `inflate` is not a mistake - when using 4-bit luminance, we can only index colors 0, 0x11, 0x22, etc.
                    return fromClut(clutBuf, null, Inflate(luminance, 4));
                };
            case PixelFormat.A8:
                return (inBuf, _, pos) =>
                {
                    var baseColor = inputBufferDescriptor.FixedColor.Value;
                    baseColor.Alpha = inBuf[pos];
                    return baseColor;
                };
            case PixelFormat.L8:
                return (inBuf, clutBuf, pos) =>
                {
                    return fromClut(clutBuf, null, inBuf[pos]);
                };
            case PixelFormat.AL44:
                return (inBuf, clutBuf, pos) =>
                {
                    var alpha = inBuf[pos] >> 4;
                    var luminance = inBuf[pos] & 0b00001111;
                    var color = fromClut(clutBuf, null, luminance * 0x11);
                    color.Alpha = (byte)alpha;
                    return color;
                };
            case PixelFormat.AL88:
                return (inBuf, clutBuf, pos) =>
                {
                    var alpha = endianess == Endianess.LittleEndian ? inBuf[2 * pos + 1] : inBuf[2 * pos];
                    var luminance = endianess == Endianess.LittleEndian ? inBuf[2 * pos + 1] : inBuf[2 * pos];
                    var color = fromClut(clutBuf, null, luminance);
                    color.Alpha = (byte)alpha;
                    return color;
                };
            case PixelFormat.RGB565:
            case PixelFormat.BGR565:
                return Takes2(endianess, format == PixelFormat.BGR565, (a, b) => new Pixel(Inflate((a & 0b11111000) >> 3, 5), Inflate(((a & 0b00000111) << 3) + ((b & 0b11100000) >> 5), 6), Inflate(b & 0b00011111, 5)));

            case PixelFormat.RGB888:
            case PixelFormat.BGR888:
                return Takes3(endianess, format == PixelFormat.BGR888, (a, b, c) => new Pixel(a, b, c));

            case PixelFormat.ARGB1555:
                return Takes2(endianess, false, (a, b) => new Pixel(Inflate((a & 0b01111100) >> 2, 5), Inflate(((a & 0b00000011) << 3) + ((b & 0b11100000) >> 5), 5), Inflate(b & 0b00011111, 5), Inflate((a & 0b10000000) >> 7, 1)));

            case PixelFormat.ARGB4444:
            case PixelFormat.ABGR4444:
                return Takes4Half(endianess, format == PixelFormat.ABGR4444, (a, b, c, d) => new Pixel(b, c, d, a));

            case PixelFormat.ARGB8888:
            case PixelFormat.ABGR8888:
                return Takes4(endianess, format == PixelFormat.ABGR8888, (a, b, c, d) => new Pixel(b, c, d, a));

            case PixelFormat.XRGB4444:
            case PixelFormat.XBGR4444:
                return Takes4Half(endianess, format == PixelFormat.XRGB4444, (_, b, c, d) => new Pixel(b, c, d));

            case PixelFormat.XRGB8888:
            case PixelFormat.XBGR8888:
                return Takes4(endianess, format == PixelFormat.XRGB8888, (_, b, c, d) => new Pixel(b, c, d));

            case PixelFormat.RGBA4444:
            case PixelFormat.BGRA4444:
                return Takes4Half(endianess, format == PixelFormat.BGRA4444, (a, b, c, d) => new Pixel(a, b, c, d));

            case PixelFormat.RGBA8888:
            case PixelFormat.BGRA8888:
                return Takes4(endianess, format == PixelFormat.BGRA8888, (a, b, c, d) => new Pixel(a, b, c, d));

            case PixelFormat.RGBX4444:
            case PixelFormat.BGRX4444:
                return Takes4Half(endianess, format == PixelFormat.BGRX4444, (a, b, c, _) => new Pixel(a, b, c));

            case PixelFormat.RGBX8888:
            case PixelFormat.BGRX8888:
                return Takes4(endianess, format == PixelFormat.BGRX8888, (a, b, c, _) => new Pixel(a, b, c));
            case PixelFormat.NV12:
                var pitch = inputBufferDescriptor.Pitch.Value;
                var planeSize = pitch * inputBufferDescriptor.Height.Value;
                return (inBuf, _, pos) =>
                {
                    var uvCol = pos % pitch / 2;
                    var uvRow = pos / pitch / 2;
                    var uvPixelOffset = uvRow * pitch + uvCol * 2;
                    var uPos = planeSize + uvPixelOffset;

                    var y = inBuf[pos];
                    var u = inBuf[uPos];
                    var v = inBuf[uPos + 1];

                    var c = (y - 16) * 298 + 128;
                    var d = u - 128;
                    var e = v - 128;

                    return new Pixel(
                        (byte)Misc.Clamp((c + 409 * e) >> 8, 0, 255),
                        (byte)Misc.Clamp((c - 100 * d - 208 * e) >> 8, 0, 255),
                        (byte)Misc.Clamp((c + 516 * d) >> 8, 0, 255)
                    );
                };
            default:
                throw new Exception($"Unsupported input format {format}");
            }
        }

        delegate void ConvertTo(byte[] outbuffer, int position, Pixel color);

        private static ConvertTo GenerateTo(BufferDescriptor outputBufferDescriptor)
        {
            var endianess = outputBufferDescriptor.DataEndianness;
            var format = outputBufferDescriptor.ColorFormat;
            switch(outputBufferDescriptor.ColorFormat)
            {
            case PixelFormat.A4:
                return (outBuf, pos, p) =>
                {
                    var alpha = p.Alpha / 16;
                    outBuf[pos / 2] &= (byte)~((pos & 1) != 0 ? 0b11110000 : 0b00001111);
                    outBuf[pos / 2] |= (byte)(((pos & 1) != 0) ? alpha << 4 : alpha);
                };

            case PixelFormat.A8:
                return (outBuf, pos, p) =>
                {
                    outBuf[pos] = p.Alpha;
                };

            case PixelFormat.RGB565:
            case PixelFormat.BGR565:
                return Gives2(endianess, format == PixelFormat.BGR565, p => new TwoBytes(
                    (byte)((p.Red & 0b11111000) | ((p.Green & 0b11100000) >> 5)),
                    (byte)(((p.Green & 0b00011100) << 3) | ((p.Blue & 0b11111000) >> 3))
                ));

            case PixelFormat.RGB888:
            case PixelFormat.BGR888:
                return Gives3(endianess, format == PixelFormat.BGR888, p => new ThreeBytes(
                    p.Red,
                    p.Green,
                    p.Blue
                ));

            case PixelFormat.ARGB1555:
                return Gives2(endianess, false, p => new TwoBytes(
                    (byte)((p.Alpha == 0xff ? 0b10000000 : 0) | ((p.Red & 0b11111000) >> 1) | ((p.Green & 0b11000000) >> 6)),
                    (byte)(((p.Green & 0b00111000) << 2) | ((p.Blue & 0b11111000) >> 3))
                ));

            case PixelFormat.ARGB4444:
            case PixelFormat.ABGR4444:
                return Gives4half(endianess, format == PixelFormat.ABGR4444, p => new FourBytes(p.Alpha, p.Red, p.Green, p.Blue));

            case PixelFormat.ARGB8888:
            case PixelFormat.ABGR8888:
                return Gives4(endianess, format == PixelFormat.ABGR8888, p => new FourBytes(p.Alpha, p.Red, p.Green, p.Blue));

            case PixelFormat.XRGB4444:
            case PixelFormat.XBGR4444:
                return Gives4half(endianess, format == PixelFormat.XBGR4444, p => new FourBytes(255, p.Red, p.Green, p.Blue));

            case PixelFormat.XRGB8888:
            case PixelFormat.XBGR8888:
                return Gives4(endianess, format == PixelFormat.XBGR8888, p => new FourBytes(255, p.Red, p.Green, p.Blue));

            case PixelFormat.RGBA4444:
            case PixelFormat.BGRA4444:
                return Gives4half(endianess, format == PixelFormat.BGRA4444, p => new FourBytes(p.Red, p.Green, p.Blue, p.Alpha));

            case PixelFormat.RGBA8888:
            case PixelFormat.BGRA8888:
                return Gives4(endianess, format == PixelFormat.BGRA8888, p => new FourBytes(p.Red, p.Green, p.Blue, p.Alpha));

            case PixelFormat.RGBX4444:
            case PixelFormat.BGRX4444:
                return Gives4half(endianess, format == PixelFormat.BGRX4444, p => new FourBytes(p.Red, p.Green, p.Blue, 255));

            case PixelFormat.RGBX8888:
            case PixelFormat.BGRX8888:
                return Gives4(endianess, format == PixelFormat.BGRX8888, p => new FourBytes(p.Red, p.Green, p.Blue, 255));

            default:
                throw new ArgumentException($"Unsupported output format {format}");
            }
        }

        private static ConvertFrom Takes2(Endianess endianess, bool swap, Func<byte, byte, Pixel> f)
        {
            if(swap)
            {
                f = (a, b) => f(a, b).RedBlueSwapped;
            }
            if(endianess == Endianess.BigEndian)
            {
                return (inBuf, _, pos) => f(inBuf[pos * 2], inBuf[pos * 2 + 1]);
            }
            return (inBuf, _, pos) => f(inBuf[pos * 2 + 1], inBuf[pos * 2]);
        }

        private static ConvertFrom Takes3(Endianess endianess, bool swap, Func<byte, byte, byte, Pixel> f)
        {
            if(swap)
            {
                f = (a, b, c) => f(a, b, c).RedBlueSwapped;
            }
            if(endianess == Endianess.BigEndian)
            {
                return (inBuf, _, pos) => f(inBuf[pos * 3], inBuf[pos * 3 + 1], inBuf[pos * 3 + 2]);
            }
            return (inBuf, _, pos) => f(inBuf[pos * 3 + 2], inBuf[pos * 3 + 1], inBuf[pos * 3]);
        }

        private static ConvertFrom Takes4(Endianess endianess, bool swap, Func<byte, byte, byte, byte, Pixel> f)
        {
            if(swap)
            {
                f = (a, b, c, d) => f(a, b, c, d).RedBlueSwapped;
            }
            if(endianess == Endianess.BigEndian)
            {
                return (inBuf, _, pos) => f(inBuf[pos * 4], inBuf[pos * 4 + 1], inBuf[pos * 4 + 2], inBuf[pos * 4 + 3]);
            }
            return (inBuf, _, pos) => f(inBuf[pos * 4 + 3], inBuf[pos * 4 + 2], inBuf[pos * 4 + 1], inBuf[pos * 4]);
        }

        private static ConvertFrom Takes4Half(Endianess endianess, bool swap, Func<byte, byte, byte, byte, Pixel> f)
        {
            return Takes2(endianess, swap, (a, b) => f(Inflate((a & 0b11110000) >> 4, 4), Inflate(a & 0b00001111, 4), Inflate((b & 0b11110000) >> 4, 4), Inflate(b & 0b00001111, 4)));
        }

        private static byte Inflate(int bits, byte width)
        {
            switch(width)
            {
            case 1:
                return (bits & 1) != 0 ? (byte)0b11111111 : (byte)0b00000000;
            case 2:
                return (byte)((bits << 6) | (bits << 4) | (bits << 2) | bits);
            case 3:
                return (byte)((bits << 5) | (bits << 2) | (bits >> 1));
            case 4:
                return (byte)((bits << 4) | bits);
            case 5:
                return (byte)((bits << 3) | (bits >> 2));
            case 6:
                return (byte)((bits << 2) | (bits >> 4));
            case 7:
                return (byte)((bits << 1) | (bits >> 6));
            case 8:
                return (byte)bits;
            default:
                throw new ArgumentOutOfRangeException("Width must be between 1 and 8");
            }
        }

        private static ConvertTo Gives2(Endianess endianess, bool swap, Func<Pixel, TwoBytes> f)
        {
            if(swap)
            {
                f = p => f(p.RedBlueSwapped);
            }
            if(endianess == Endianess.BigEndian)
            {
                return (outBuf, pos, p) =>
                {
                    var v = f(p);
                    outBuf[pos * 2] = v.One;
                    outBuf[pos * 2 + 1] = v.Two;
                };
            }
            return (outBuf, pos, p) =>
            {
                var v = f(p);
                outBuf[pos * 2] = v.Two;
                outBuf[pos * 2 + 1] = v.One;
            };
        }

        private static ConvertTo Gives3(Endianess endianess, bool swap, Func<Pixel, ThreeBytes> f)
        {
            if(swap)
            {
                f = p => f(p.RedBlueSwapped);
            }
            if(endianess == Endianess.BigEndian)
            {
                return (outBuf, pos, p) =>
                {
                    var v = f(p);
                    outBuf[pos * 3] = v.One;
                    outBuf[pos * 3 + 1] = v.Two;
                    outBuf[pos * 3 + 2] = v.Three;
                };
            }
            return (outBuf, pos, p) =>
            {
                var v = f(p);
                outBuf[pos * 3] = v.Three;
                outBuf[pos * 3 + 1] = v.Two;
                outBuf[pos * 3 + 2] = v.One;
            };
        }

        private static ConvertTo Gives4(Endianess endianess, bool swap, Func<Pixel, FourBytes> f)
        {
            if(swap)
            {
                f = p => f(p.RedBlueSwapped);
            }
            if(endianess == Endianess.BigEndian)
            {
                return (outBuf, pos, p) =>
                {
                    var v = f(p);
                    outBuf[pos * 4] = v.One;
                    outBuf[pos * 4 + 1] = v.Two;
                    outBuf[pos * 4 + 2] = v.Three;
                    outBuf[pos * 4 + 3] = v.Four;
                };
            }
            return (outBuf, pos, p) =>
            {
                var v = f(p);
                outBuf[pos * 4] = v.Four;
                outBuf[pos * 4 + 1] = v.Three;
                outBuf[pos * 4 + 2] = v.Two;
                outBuf[pos * 4 + 3] = v.One;
            };
        }

        private static ConvertTo Gives4half(Endianess endianess, bool swap, Func<Pixel, FourBytes> f)
        {
            return Gives2(endianess, swap, (p) =>
            {
                var v = f(p);
                return new TwoBytes((byte)((v.One & 0b11110000) | (v.Two >> 4)), (byte)((v.Three & 0b11110000) | (v.Four >> 4)));
            });
        }

        private static readonly ConcurrentDictionary<Tuple<BufferDescriptor, BufferDescriptor>, IPixelConverter> convertersCache = new ConcurrentDictionary<Tuple<BufferDescriptor, BufferDescriptor>, IPixelConverter>();
        private static readonly ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat, Endianess, Pixel?, Tuple<Pixel?>>, IPixelBlender> blendersCache = new ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat, Endianess, Pixel?, Tuple<Pixel?>>, IPixelBlender>();

        private class PixelConverter : IPixelConverter
        {
            public PixelConverter(PixelFormat input, PixelFormat output, ConvertDelegate converter)
            {
                Input = input;
                Output = output;
                this.converter = converter;
            }

            public void Convert(byte[] inBuffer, ref byte[] outBuffer)
            {
                converter(inBuffer, null, 0xff, PixelBlendingMode.NoModification, ref outBuffer);
            }

            public void Convert(byte[] inBuffer, byte[] clutBuffer, byte alpha, PixelBlendingMode alphaReplaceMode, ref byte[] outBuffer)
            {
                converter(inBuffer, clutBuffer, alpha, alphaReplaceMode, ref outBuffer);
            }

            public PixelFormat Input { get; private set; }

            public PixelFormat Output { get; private set; }

            private readonly ConvertDelegate converter;
        }

        private class PixelBlender : IPixelBlender
        {
            public PixelBlender(PixelFormat back, PixelFormat front, PixelFormat output, BlendDelegate blender)
            {
                BackBuffer = back;
                FrontBuffer = front;
                Output = output;
                this.blender = blender;
            }

            public void Blend(byte[] backBuffer, byte[] frontBuffer, ref byte[] output, Pixel? background = null, byte backBufferAlphaMultiplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply)
            {
                Blend(backBuffer, null, frontBuffer, null, ref output, background, backBufferAlphaMultiplier, backgroundBlendingMode, frontBufferAlphaMultiplayer, foregroundBlendingMode);
            }

            public void Blend(byte[] backBuffer, byte[] backClutBuffer, byte[] frontBuffer, byte[] frontClutBuffer, ref byte[] output, Pixel? background = null, byte backBufferAlphaMultiplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply)
            {
                if(background == null)
                {
                    background = new Pixel(0x00, 0x00, 0x00, 0x00);
                }
                blender(backBuffer, backClutBuffer, frontBuffer, frontClutBuffer, ref output, background.Value, backBufferAlphaMultiplier, backgroundBlendingMode, frontBufferAlphaMultiplayer, foregroundBlendingMode);
            }

            public PixelFormat BackBuffer { get; private set; }

            public PixelFormat FrontBuffer { get; private set; }

            public PixelFormat Output { get; private set; }

            private readonly BlendDelegate blender;
        }

        private struct BufferDescriptor
        {
            public PixelFormat ColorFormat { get; set; }

            public Endianess DataEndianness { get; set; }

            public PixelFormat? ClutColorFormat { get; set; }

            public Pixel? FixedColor { get; set; } // for A4 and A8 modes

            public int? Pitch { get; set; }

            public int? Height { get; set; }
        }

        private struct TransformationDescriptor
        {
            public TransformationDescriptor(sbyte shift, byte mask, byte usedBits) : this()
            {
                ShiftBits = shift;
                MaskBits = mask;
                UsedBits = usedBits;
            }

            public sbyte ShiftBits { get; private set; }

            public byte MaskBits { get; private set; }

            public byte UsedBits { get; private set; }
        }

        // Workaround for Mono not having `ValueTuple`s
        private struct TwoBytes
        {
            public TwoBytes(byte one, byte two)
            {
                One = one;
                Two = two;
            }

            public byte One;
            public byte Two;
        }

        private struct ThreeBytes
        {
            public ThreeBytes(byte one, byte two, byte three)
            {
                One = one;
                Two = two;
                Three = three;
            }

            public byte One;
            public byte Two;
            public byte Three;
        }

        private struct FourBytes
        {
            public FourBytes(byte one, byte two, byte three, byte four)
            {
                One = one;
                Two = two;
                Three = three;
                Four = four;
            }

            public byte One;
            public byte Two;
            public byte Three;
            public byte Four;
        }

        private delegate void ConvertDelegate(byte[] inBuffer, byte[] clutBuffer, byte alpha, PixelBlendingMode alphaReplaceMode, ref byte[] outBuffer);

        private delegate void BlendDelegate(byte[] backBuffer, byte[] backClutBuffer, byte[] frontBuffer, byte[] frontClutBuffer, ref byte[] outBuffer, Pixel background, byte backBufferAlphaMulitplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply);
    }
}
