//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ELFSharp.ELF;

namespace Antmicro.Renode.Backends.Display
{
    public static class PixelManipulationTools
    {
        public static IPixelConverter GetConverter(PixelFormat inputFormat, Endianess inputEndianess, PixelFormat outputFormat, Endianess outputEndianess, PixelFormat? clutInputFormat = null, Pixel inputFixedColor = null /* fixed color for A4 and A8 mode */)
        {
            var converterConfiguration = Tuple.Create(inputFormat, inputEndianess, outputFormat, outputEndianess, clutInputFormat, inputFixedColor);
            return convertersCache.GetOrAdd(converterConfiguration, (_) => 
                new PixelConverter(inputFormat, outputFormat, GenerateConvertMethod(
                    new BufferDescriptor
                    {
                        ColorFormat = inputFormat,
                        ClutColorFormat = clutInputFormat,
                        FixedColor = inputFixedColor,
                        DataEndianness = inputEndianess
                    },
                    new BufferDescriptor
                    {
                        ColorFormat = outputFormat,
                        DataEndianness = outputEndianess
                    })));
        }

        public static IPixelBlender GetBlender(PixelFormat backBuffer, Endianess backBufferEndianess, PixelFormat frontBuffer, Endianess frontBufferEndianes, PixelFormat output, Endianess outputEndianess, PixelFormat? clutForegroundFormat = null, PixelFormat? clutBackgroundFormat = null, Pixel bgFixedColor = null, Pixel fgFixedColor = null)
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

        private static BlendDelegate GenerateBlendMethod(BufferDescriptor backgroudBufferDescriptor, BufferDescriptor foregroundBufferDescriptor, BufferDescriptor outputBufferDescriptor)
        {
            var outputPixel = new PixelDescriptor();
            var inputBackgroundPixel = new PixelDescriptor();
            var inputForegroundPixel = new PixelDescriptor();
            var contantBackgroundPixel = new PixelDescriptor();

            var vBackPos = Expression.Variable(typeof(int), "backPos");
            var vFrontPos = Expression.Variable(typeof(int), "frontPos");
            var vOutPos = Expression.Variable(typeof(int), "outPos");
            var vBackStep = Expression.Variable(typeof(int), "backStep");
            var vFrontStep = Expression.Variable(typeof(int), "frontStep");
            var vOutStep = Expression.Variable(typeof(int), "outStep");
            var vLength = Expression.Variable(typeof(int),  "length");

            var vBackBuffer = Expression.Parameter(typeof(byte[]), "backBuffer");
            var vBackClutBuffer = Expression.Parameter(typeof(byte[]), "backClutBuffer");
            var vFrontBuffer = Expression.Parameter(typeof(byte[]), "frontBuffer");
            var vFrontClutBuffer = Expression.Parameter(typeof(byte[]), "frontClutBuffer");
            var vOutputBuffer = Expression.Parameter(typeof(byte[]).MakeByRefType(), "outputBuffer");
            var vBackgroundColor = Expression.Parameter(typeof(Pixel), "backgroundColor");
            var vBackBufferAlphaMultiplier = Expression.Parameter(typeof(byte), "backBufferAlphaMultiplier");
            var vFrontBufferAlphaMultiplier = Expression.Parameter(typeof(byte), "frontBufferAlphaMultiplier");

            var vBackgroundBlendingMode = Expression.Parameter(typeof(PixelBlendingMode), "backgroundBlendingMode");
            var vForegroundBlendingMode = Expression.Parameter(typeof(PixelBlendingMode), "foregroundBlendingMode");

            var vBackAlphaBlended = Expression.Variable(typeof(uint), "backAlphaBlended");
            var vBackgroundColorAlphaBlended = Expression.Variable(typeof(uint), "backgroundColorAlphaBlended");

            var tmp = Expression.Variable(typeof(uint));

            var outOfLoop = Expression.Label();

            var block = Expression.Block(
                new[] {  outputPixel.RedChannel, outputPixel.GreenChannel, outputPixel.BlueChannel, outputPixel.AlphaChannel,
                         inputForegroundPixel.RedChannel, inputForegroundPixel.GreenChannel, inputForegroundPixel.BlueChannel, inputForegroundPixel.AlphaChannel,
                         inputBackgroundPixel.RedChannel, inputBackgroundPixel.GreenChannel, inputBackgroundPixel.BlueChannel, inputBackgroundPixel.AlphaChannel,
                         vBackStep, vFrontStep, vOutStep, vLength, vBackPos, vFrontPos, vOutPos,
                         contantBackgroundPixel.RedChannel, contantBackgroundPixel.GreenChannel, contantBackgroundPixel.BlueChannel, contantBackgroundPixel.AlphaChannel,
                         vBackAlphaBlended, vBackgroundColorAlphaBlended,
                                    tmp
                },

                Expression.Assign(vBackStep, Expression.Constant(backgroudBufferDescriptor.ColorFormat.GetColorDepth())),
                Expression.Assign(vFrontStep, Expression.Constant(foregroundBufferDescriptor.ColorFormat.GetColorDepth())),
                Expression.Assign(vOutStep, Expression.Constant(outputBufferDescriptor.ColorFormat.GetColorDepth())),
                Expression.Assign(vLength, Expression.Property(vBackBuffer, "Length")),

                Expression.Assign(vBackPos, Expression.Constant(0x00)),
                Expression.Assign(vFrontPos, Expression.Constant(0x00)),
                Expression.Assign(vOutPos, Expression.Constant(0x00)),

                Expression.Assign(contantBackgroundPixel.AlphaChannel, Expression.Convert(Expression.Property(vBackgroundColor, "Alpha"), typeof(uint))),
                Expression.Assign(contantBackgroundPixel.RedChannel, Expression.Convert(Expression.Property(vBackgroundColor, "Red"), typeof(uint))),
                Expression.Assign(contantBackgroundPixel.GreenChannel, Expression.Convert(Expression.Property(vBackgroundColor, "Green"), typeof(uint))),
                Expression.Assign(contantBackgroundPixel.BlueChannel, Expression.Convert(Expression.Property(vBackgroundColor, "Blue"), typeof(uint))),

                Expression.Loop(
                    Expression.IfThenElse(Expression.LessThan(vBackPos, vLength),
                        Expression.Block(

                            GenerateFrom(backgroudBufferDescriptor, vBackBuffer, vBackClutBuffer, vBackPos, inputBackgroundPixel, tmp),
                            GenerateFrom(foregroundBufferDescriptor, vFrontBuffer, vFrontClutBuffer, vFrontPos, inputForegroundPixel, tmp),

                            Expression.Switch(typeof(void), vBackgroundBlendingMode, null, null, new SwitchCase[]
                            {
                                // Multiply: input BG Pixel Alpha *= bg alpha
                                Expression.SwitchCase(
                                    Expression.Assign(inputBackgroundPixel.AlphaChannel, Expression.Divide(Expression.Multiply(inputBackgroundPixel.AlphaChannel, Expression.Convert(vBackBufferAlphaMultiplier, typeof(uint))), Expression.Constant((uint)0xFF))),
                                    Expression.Constant(PixelBlendingMode.Multiply)
                                ),

                                // Replace: input BG Pixel Alpha = bg alpha
                                Expression.SwitchCase(
                                    Expression.Assign(inputBackgroundPixel.AlphaChannel, Expression.Convert(vBackBufferAlphaMultiplier, typeof(uint))),
                                    Expression.Constant(PixelBlendingMode.Replace)
                                )
                            }),

                            Expression.Switch(typeof(void), vForegroundBlendingMode, null, null, new SwitchCase[]
                            {
                                // Multiply: input FG Pixel Alpha *= fg alpha
                                Expression.SwitchCase(
                                    Expression.Assign(inputForegroundPixel.AlphaChannel, Expression.Divide(Expression.Multiply(inputForegroundPixel.AlphaChannel, Expression.Convert(vFrontBufferAlphaMultiplier, typeof(uint))), Expression.Constant((uint)0xFF))),
                                    Expression.Constant(PixelBlendingMode.Multiply)
                                ),

                                // Replace: input FG Pixel Alpha = fg alpha
                                Expression.SwitchCase(
                                    Expression.Assign(inputForegroundPixel.AlphaChannel, Expression.Convert(vFrontBufferAlphaMultiplier, typeof(uint))),
                                    Expression.Constant(PixelBlendingMode.Replace)
                                )
                            }),

                            Expression.Block(
                                // (b_alpha * (0xFF - f_alpha)) / 0xFF
                                Expression.Assign(
                                    vBackAlphaBlended,
                                    Expression.Divide(
                                        Expression.Multiply(
                                            inputBackgroundPixel.AlphaChannel,
                                            Expression.Subtract(
                                                Expression.Constant((uint)0xFF),
                                                inputForegroundPixel.AlphaChannel)),
                                        Expression.Constant((uint)0xFF))),

                                // (c_alpha * (0xFF - (f_alpha + b_alpha * (0xFF - f_alpha)))) / 0xFF
                                Expression.Assign(
                                        vBackgroundColorAlphaBlended,
                                        Expression.Divide(
                                            Expression.Multiply(
                                                contantBackgroundPixel.AlphaChannel,
                                                Expression.Subtract(
                                                    Expression.Constant((uint)0xFF),
                                                    Expression.Add(
                                                        inputForegroundPixel.AlphaChannel,
                                                        vBackAlphaBlended))),
                                             Expression.Constant((uint)0xFF))),

                                Expression.Assign(
                                    outputPixel.AlphaChannel,
                                    Expression.Add(
                                        inputForegroundPixel.AlphaChannel,
                                        Expression.Add(
                                            vBackAlphaBlended,
                                            vBackgroundColorAlphaBlended))),

                                Expression.IfThenElse(
                                    // If output pixel is 100% transparent
                                    Expression.Equal(outputPixel.AlphaChannel, Expression.Constant((uint)0)),
                                    // ... then the outputpixel should actually be equal to the underlying background pixel
                                    Expression.Block(
                                        Expression.Assign(outputPixel.RedChannel, contantBackgroundPixel.RedChannel),
                                        Expression.Assign(outputPixel.GreenChannel, contantBackgroundPixel.GreenChannel),
                                        Expression.Assign(outputPixel.BlueChannel, contantBackgroundPixel.BlueChannel),
                                        Expression.Assign(outputPixel.AlphaChannel, contantBackgroundPixel.AlphaChannel)),
                                    // ... else, apply alpha to all color channels
                                    Expression.Block(
                                        Expression.Assign(
                                            outputPixel.RedChannel,
                                            Expression.Divide(
                                                Expression.Add(
                                                    Expression.Add(
                                                        Expression.Multiply(vBackgroundColorAlphaBlended, contantBackgroundPixel.RedChannel),
                                                        Expression.Multiply(vBackAlphaBlended, inputBackgroundPixel.RedChannel)),
                                                    Expression.Multiply(inputForegroundPixel.AlphaChannel, inputForegroundPixel.RedChannel)),
                                                outputPixel.AlphaChannel)),
                                        Expression.Assign(
                                            outputPixel.GreenChannel,
                                            Expression.Divide(
                                                Expression.Add(
                                                    Expression.Add(
                                                        Expression.Multiply(vBackgroundColorAlphaBlended, contantBackgroundPixel.GreenChannel),
                                                        Expression.Multiply(vBackAlphaBlended, inputBackgroundPixel.GreenChannel)),
                                                    Expression.Multiply(inputForegroundPixel.AlphaChannel, inputForegroundPixel.GreenChannel)),
                                                outputPixel.AlphaChannel)),
                                        Expression.Assign(
                                            outputPixel.BlueChannel,
                                            Expression.Divide(
                                                Expression.Add(
                                                    Expression.Add(
                                                        Expression.Multiply(vBackgroundColorAlphaBlended, contantBackgroundPixel.BlueChannel),
                                                        Expression.Multiply(vBackAlphaBlended, inputBackgroundPixel.BlueChannel)),
                                                    Expression.Multiply(inputForegroundPixel.AlphaChannel, inputForegroundPixel.BlueChannel)),
                                                outputPixel.AlphaChannel))))
                            ),

                            GenerateTo(outputBufferDescriptor, vOutputBuffer, vOutPos, outputPixel),

                            Expression.AddAssign(vBackPos, vBackStep),
                            Expression.AddAssign(vFrontPos, vFrontStep),
                            Expression.AddAssign(vOutPos, vOutStep)
                        ),
                        Expression.Break(outOfLoop)
                    ),
                    outOfLoop
                )
            );

            return Expression.Lambda<BlendDelegate>(block, vBackBuffer, vBackClutBuffer, vFrontBuffer, vFrontClutBuffer, vOutputBuffer, vBackgroundColor, vBackBufferAlphaMultiplier, vBackgroundBlendingMode, vFrontBufferAlphaMultiplier, vForegroundBlendingMode).Compile();
        }

        private static ConvertDelegate GenerateConvertMethod(BufferDescriptor inputBufferDescriptor, BufferDescriptor outputBufferDescriptor)
        {
            var vColor = new PixelDescriptor();

            var vInStep = Expression.Variable(typeof(int),  "inStep");
            var vOutStep = Expression.Variable(typeof(int), "outStep");
            var vLength = Expression.Variable(typeof(int),  "length");

            var vInputBuffer = Expression.Parameter(typeof(byte[]), "inputBuffer");
            var vClutBuffer = Expression.Parameter(typeof(byte[]), "clutBuffer");
            var vAlpha = Expression.Parameter(typeof(byte), "alpha");
            var vAlphaReplaceMode = Expression.Parameter(typeof(PixelBlendingMode), "alphaReplaceMode");
            var vOutputBuffer = Expression.Parameter(typeof(byte[]).MakeByRefType(), "outputBuffer");

            var vInPos = Expression.Variable(typeof(int), "inPos");
            var vOutPos = Expression.Variable(typeof(int), "outPos");

            var tmp = Expression.Variable(typeof(uint));

            var outOfLoop = Expression.Label();

            var block = Expression.Block(
                new [] { vColor.RedChannel, vColor.GreenChannel, vColor.BlueChannel, vColor.AlphaChannel, vInStep, vOutStep, vLength, vInPos, vOutPos, tmp },

                Expression.Assign(vInStep, Expression.Constant(inputBufferDescriptor.ColorFormat.GetColorDepth())),
                Expression.Assign(vOutStep, Expression.Constant(outputBufferDescriptor.ColorFormat.GetColorDepth())),
                Expression.Assign(vLength, Expression.Property(vInputBuffer, "Length")),

                Expression.Assign(vInPos, Expression.Constant(0)),
                Expression.Assign(vOutPos, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(Expression.LessThan(vInPos, vLength),
                        Expression.Block(
                            GenerateFrom(inputBufferDescriptor, vInputBuffer, vClutBuffer, vInPos, vColor, tmp),

                            // handle the case where alpha needs to be changed
                            Expression.Switch(typeof(void), vAlphaReplaceMode, null, null, new SwitchCase[]
                            {
                                // Multiply
                                Expression.SwitchCase(
                                    Expression.Assign(vColor.AlphaChannel, Expression.Divide(Expression.Multiply(vColor.AlphaChannel, Expression.Convert(vAlpha, typeof(uint))), Expression.Constant((uint)0xFF))),
                                    Expression.Constant(PixelBlendingMode.Multiply)
                                ),

                                // Replace
                                Expression.SwitchCase(
                                    Expression.Assign(vColor.AlphaChannel, Expression.Convert(vAlpha, typeof(uint))),
                                    Expression.Constant(PixelBlendingMode.Replace)
                                )
                            }),

                            GenerateTo(outputBufferDescriptor, vOutputBuffer, vOutPos, vColor),

                            Expression.AddAssign(vInPos, vInStep),
                            Expression.AddAssign(vOutPos, vOutStep)
                        ),
                        Expression.Break(outOfLoop)
                    ),
                    outOfLoop
                )
            );

            return Expression.Lambda<ConvertDelegate>(block, vInputBuffer, vClutBuffer, vAlpha, vAlphaReplaceMode, vOutputBuffer).Compile();
        }

        /// <summary>
        /// Generates expression reading one pixel encoded in given format from input buffer (with bytes ordered accordingly to endianess) at given position
        /// and storing each channel in separate variable expression.
        /// </summary>
        /// <returns>Generated expression.</returns>
        /// <param name="inputBufferDescriptor">Object containing information about input buffer: color format and endianness.</param>
        /// <param name="inBuffer">Input buffer.</param>
        /// <param name="clutBuffer">Color look-up table buffer.</param>
        /// <param name="inPosition">Position of pixel in buffer.</param>
        /// <param name="color">Variable where values of color channels should be stored.</param>
        private static Expression GenerateFrom(BufferDescriptor inputBufferDescriptor, ParameterExpression inBuffer, ParameterExpression clutBuffer, Expression inPosition, PixelDescriptor color, Expression tmp)
        {
            byte currentBit = 0;
            byte currentByte = 0;
            bool isAlphaSet = false;

            var expressions = new List<Expression>();
            var inputBytes = new ParameterExpression[inputBufferDescriptor.ColorFormat.GetColorDepth()];
            for(int i = 0; i < inputBytes.Length; i++)
            {
                inputBytes[i] = Expression.Variable(typeof(uint));

                expressions.Add(
                    Expression.Assign(
                        inputBytes[i],
                        Expression.Convert(
                            Expression.ArrayIndex(
                                inBuffer,
                                Expression.Add(
                                    inPosition,
                                    Expression.Constant((inputBufferDescriptor.DataEndianness == Endianess.BigEndian) ? i : inputBytes.Length - i - 1))),
                            typeof(uint))));
            }

            foreach(var colorDescriptor in inputBufferDescriptor.ColorFormat.GetColorsLengths())
            {
                Expression colorExpression = null;

                foreach(var transformation in ByteInflate(colorDescriptor.Value, currentBit))
                {
                    Expression currentExpressionFragment = inputBytes[currentByte];

                    if(transformation.MaskBits != 0xFF)
                    {
                        currentExpressionFragment = Expression.And(currentExpressionFragment, Expression.Constant((uint)transformation.MaskBits));
                    }

                    if(transformation.ShiftBits > 0)
                    {
                        currentExpressionFragment = Expression.RightShift(currentExpressionFragment, Expression.Constant((int)transformation.ShiftBits));
                    }
                    else if(transformation.ShiftBits < 0)
                    {
                        currentExpressionFragment = Expression.And(
                            Expression.LeftShift(currentExpressionFragment, Expression.Constant((int)(-transformation.ShiftBits))),
                            Expression.Constant((uint)0xFF));
                    }

                    currentBit += transformation.UsedBits;
                    if(currentBit >= 8)
                    {
                        currentBit -= 8;
                        currentByte++;
                    }

                    colorExpression = (colorExpression == null) ? currentExpressionFragment : Expression.Or(colorExpression, currentExpressionFragment);
                }

                if(colorDescriptor.Key == ColorType.X)
                {
                    continue;
                }

                // luminance - indirect color indexing using CLUT
                if(colorDescriptor.Key == ColorType.L)
                {
                    if(!inputBufferDescriptor.ClutColorFormat.HasValue)
                    {
                        throw new ArgumentException("CLUT mode required but not set");
                    }

                    var clutWidth = Expression.Constant((uint)inputBufferDescriptor.ClutColorFormat.Value.GetColorDepth());
                    var clutOffset = Expression.Multiply(colorExpression, clutWidth);

                    expressions.Add(Expression.Assign(tmp, color.AlphaChannel));

                    // todo: indirect parameters should not be needed here, but we  m u s t  pass something
                    expressions.Add(
                        GenerateFrom(new BufferDescriptor
                    {
                        ColorFormat = inputBufferDescriptor.ClutColorFormat.Value,
                        DataEndianness = inputBufferDescriptor.DataEndianness
                    }, clutBuffer, clutBuffer, Expression.Convert(clutOffset, typeof(int)), color, tmp));

                    expressions.Add(Expression.Assign(color.AlphaChannel, tmp));
                }
                else
                {
                    Expression currentColor = null;
                    switch(colorDescriptor.Key)
                    {
                    case ColorType.A:
                        currentColor = color.AlphaChannel;
                        isAlphaSet = true;
                        break;
                    case ColorType.B:
                        currentColor = color.BlueChannel;
                        break;
                    case ColorType.G:
                        currentColor = color.GreenChannel;
                        break;
                    case ColorType.R:
                        currentColor = color.RedChannel;
                        break;
                    }

                    if((inputBufferDescriptor.ColorFormat == PixelFormat.A4 || inputBufferDescriptor.ColorFormat == PixelFormat.A8) && inputBufferDescriptor.FixedColor != null)
                    {
                        expressions.Add(Expression.Assign(color.AlphaChannel, colorExpression));
                        expressions.Add(Expression.Assign(color.RedChannel, Expression.Constant((uint)inputBufferDescriptor.FixedColor.Red)));
                        expressions.Add(Expression.Assign(color.GreenChannel, Expression.Constant((uint)inputBufferDescriptor.FixedColor.Green)));
                        expressions.Add(Expression.Assign(color.BlueChannel, Expression.Constant((uint)inputBufferDescriptor.FixedColor.Blue)));
                    }
                    else
                    {
                        expressions.Add(Expression.Assign(currentColor, colorExpression));

                        // filling lsb '0'-bits with copy of msb pattern
                        var numberOfBits = colorDescriptor.Value;
                        var zeroBits = 8 - numberOfBits;
                        while(zeroBits > 0)
                        {
                            expressions.Add(Expression.OrAssign(
                                currentColor,
                                Expression.RightShift(
                                    currentColor,
                                    Expression.Constant((int)numberOfBits))));
                            zeroBits -= numberOfBits;
                        }
                    }
                }
            }

            if(!isAlphaSet)
            {
                expressions.Add(Expression.Assign(color.AlphaChannel, Expression.Constant((uint)0xFF)));
            }
            return Expression.Block(inputBytes, expressions);
        }

        /// <summary>
        /// Generates expression converting and storing one pixel in output format to output buffer (ordering bytes accordingly to endianess) at given position using channels values provided by red/green/blue/alpha variables.
        /// </summary>
        /// <returns>Generated expression.</returns>
        /// <param name="outputBufferDescriptor">Object containing information about output buffer: color format and endianness.</param>
        /// <param name="outBuffer">Output buffer.</param>
        /// <param name="outPosition">Position of pixel in output buffer.</param>
        /// <param name="color">Object with variables from which value of color channels should be read.</param>
        private static Expression GenerateTo(BufferDescriptor outputBufferDescriptor, ParameterExpression outBuffer, ParameterExpression outPosition, PixelDescriptor color)
        {
            byte currentBit = 0;
            byte currentByte = 0;
            var expressions = new List<Expression>();

            Expression currentExpression = null;

            foreach(var colorDescriptor in outputBufferDescriptor.ColorFormat.GetColorsLengths())
            {
                Expression colorExpression = null;

                switch(colorDescriptor.Key)
                {
                case ColorType.A:
                    colorExpression = color.AlphaChannel;
                    break;
                case ColorType.B:
                    colorExpression = color.BlueChannel;
                    break;
                case ColorType.G:
                    colorExpression = color.GreenChannel;
                    break;
                case ColorType.R:
                    colorExpression = color.RedChannel;
                    break;
                case ColorType.X:
                    colorExpression = Expression.Constant((uint)0xFF);
                    break;
                case ColorType.L:
                    throw new ArgumentException("Luminance channel is not allowed in target color format");
                }

                foreach(var transformation in ByteSqueezeAndMove(colorDescriptor.Value, currentBit))
                {
                    Expression currentExpressionFragment = colorExpression;

                    if(transformation.MaskBits != 0xFF)
                    {
                        currentExpressionFragment = Expression.And(currentExpressionFragment, Expression.Constant((uint)transformation.MaskBits));
                    }

                    if(transformation.ShiftBits > 0)
                    {
                        currentExpressionFragment = Expression.RightShift(currentExpressionFragment, Expression.Constant((int)transformation.ShiftBits));
                    }
                    else if(transformation.ShiftBits < 0)
                    {
                        currentExpressionFragment = Expression.And(
                            Expression.LeftShift(currentExpressionFragment, Expression.Constant((int)(-transformation.ShiftBits))),
                            Expression.Constant((uint)0xFF));

                    }

                    currentExpression = (currentExpression == null) ? currentExpressionFragment : Expression.Or(currentExpression, currentExpressionFragment);

                    currentBit += transformation.UsedBits;
                    while(currentBit >= 8)
                    {
                        expressions.Add(
                            Expression.Assign(
                                Expression.ArrayAccess(outBuffer,
                                    Expression.Add(
                                        outPosition,
                                        Expression.Constant((outputBufferDescriptor.DataEndianness == Endianess.BigEndian) ? (int) currentByte : (outputBufferDescriptor.ColorFormat.GetColorDepth() - currentByte - 1)))),
                                Expression.Convert(currentExpression, typeof(byte))));

                        currentExpression = null;
                        currentBit -= 8;
                        currentByte++;
                    }
                }
            }

            return Expression.Block(expressions);
        }

        /// <summary>
        /// Calculates a set of transformations that reduces a byte variable into lower number of bits (by cutting off the least significant bits)
        /// and shifts them by given offset.
        /// </summary>
        /// <returns>Set of byte transformations.</returns>
        /// <param name="bits">Number of bits of resulting data.</param>
        /// <param name="offset">Number of bits by which resulting data should be shifted (in right direction).</param>
        private static TransformationDescriptor[] ByteSqueezeAndMove(byte bits, byte offset)
        {
            if(offset < 0 || offset > 7)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if(bits == 0 || bits > 8)
            {
                throw new ArgumentOutOfRangeException("bits");
            }

            var result = new List<TransformationDescriptor>();

            var bitsLeft = bits;
            var additionalShift = 0;
            while(bitsLeft > 0)
            {
                var currentBits = (byte)Math.Min(8 - offset, bitsLeft);
                var mask = (byte)(((1 << currentBits) - 1) << (8 - additionalShift - currentBits));
                result.Add(new TransformationDescriptor((sbyte)(bitsLeft < bits ? -additionalShift : offset), mask, currentBits));

                additionalShift += currentBits;
                bitsLeft -= currentBits;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Calculates a set of transformations that reads reduced byte variable of size 'bits' shifted by 'bitOffset'.
        /// </summary>
        /// <returns>Set of byte transformations.</returns>
        /// <param name="bits">Number of bits of input data.</param>
        /// <param name="bitOffset">Number of bits by which input data is shifted.</param>
        private static TransformationDescriptor[] ByteInflate(byte bits, byte bitOffset)
        {
            if(bits == 0 || bits > 8)
            {
                throw new ArgumentOutOfRangeException("bits");
            }

            if(bitOffset > 7)
            {
                throw new ArgumentOutOfRangeException("bitOffset");
            }

            var result = new List<TransformationDescriptor>();

            var bitsLeft = bits;
            var additionalShift = 0;

            while(bitsLeft > 0)
            {
                var currentBits = (byte)Math.Min(8 - bitOffset, bitsLeft);
                var mask = ((1 << currentBits) - 1) << (8 - bitOffset - currentBits);
                var shift = (sbyte)(-bitOffset + additionalShift);

                // optimization
                if ((mask >> shift) == (0xFF >> shift))
                {
                    mask = 0xFF;
                }

                var descriptor = new TransformationDescriptor(shift, (byte)mask, currentBits);
                result.Add(descriptor);

                bitOffset = 0;
                additionalShift += currentBits;
                bitsLeft -= currentBits;
            }

            return result.ToArray();
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
            public byte  MaskBits  { get; private set; }
            public byte  UsedBits  { get; private set; }
        }

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

            public void Blend(byte[] backBuffer, byte[] frontBuffer, ref byte[] output, Pixel background = null, byte backBufferAlphaMultiplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply)
            {
                Blend(backBuffer, null, frontBuffer, null, ref output, background, backBufferAlphaMultiplier, backgroundBlendingMode, frontBufferAlphaMultiplayer, foregroundBlendingMode);
            }

            public void Blend(byte[] backBuffer, byte[] backClutBuffer, byte[] frontBuffer, byte[] frontClutBuffer, ref byte[] output, Pixel background = null, byte backBufferAlphaMultiplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply)
            {
                if(background == null)
                {
                    background = new Pixel(0x00, 0x00, 0x00, 0x00);
                }
                blender(backBuffer, backClutBuffer, frontBuffer, frontClutBuffer, ref output, background, backBufferAlphaMultiplier, backgroundBlendingMode, frontBufferAlphaMultiplayer, foregroundBlendingMode);
            }

            public PixelFormat BackBuffer { get; private set; }
            public PixelFormat FrontBuffer { get; private set; }
            public PixelFormat Output { get; private set; }

            private readonly BlendDelegate blender;
        }

        private static ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat?, Pixel>, IPixelConverter> convertersCache = new ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat?, Pixel>, IPixelConverter>();
        private static ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat, Endianess, Pixel, Tuple<Pixel>>, IPixelBlender> blendersCache = new ConcurrentDictionary<Tuple<PixelFormat, Endianess, PixelFormat, Endianess, PixelFormat, Endianess, Pixel, Tuple<Pixel>>, IPixelBlender>();

        private delegate void ConvertDelegate(byte[] inBuffer, byte[] clutBuffer, byte alpha, PixelBlendingMode alphaReplaceMode, ref byte[] outBuffer);
        private delegate void BlendDelegate(byte[] backBuffer, byte[] backClutBuffer, byte[] frontBuffer, byte[] frontClutBuffer, ref byte[] outBuffer, Pixel background = null, byte backBufferAlphaMulitplier = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply);

        private class PixelDescriptor
        {
            public PixelDescriptor()
            {
                RedChannel = Expression.Variable(typeof(uint), "red");
                GreenChannel = Expression.Variable(typeof(uint), "green");
                BlueChannel = Expression.Variable(typeof(uint), "blue");
                AlphaChannel = Expression.Variable(typeof(uint), "alpha");
            }

            public ParameterExpression RedChannel;
            public ParameterExpression GreenChannel;
            public ParameterExpression BlueChannel;
            public ParameterExpression AlphaChannel;
        }

        private class BufferDescriptor
        {
            public PixelFormat ColorFormat { get; set; }
            public Endianess DataEndianness { get; set; }
            public PixelFormat? ClutColorFormat { get; set; }
            public Pixel FixedColor { get; set; } // for A4 and A8 modes
        }
    }
}
