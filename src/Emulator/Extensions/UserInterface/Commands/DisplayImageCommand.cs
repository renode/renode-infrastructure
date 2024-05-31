//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Backends.Display;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class DisplayImageCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine($"{Name} width height");
            writer.WriteLine("Generates testing image");
            writer.WriteLine();
            writer.WriteLine($"{Name} path_to_image");
            writer.WriteLine("Supported file formats:");
            writer.WriteLine("jpeg");
            writer.WriteLine("png");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken pathToImage)
        {
            Run(writer, pathToImage.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, DecimalIntegerToken widthToken, DecimalIntegerToken heightToken)
        {
            // This method creates white rectangle, intended for testing purposes.
            var width = (int)widthToken.Value;
            var height = (int)heightToken.Value;
            if(width <= 0 || height <= 0)
            {
                throw new RecoverableException("Width and height must be positive values");
            }

            var bytes = new byte[width * height * RawImageData.PixelFormat.GetColorDepth()];
            for(var i = 0; i < bytes.Length; ++i)
            {
                bytes[i] = (byte)0xFF;
            }
            var image = new RawImageData(bytes, width, height);
            writer.WriteRaw(InlineImage.Encode(image.ToPng()));
        }

        public DisplayImageCommand(Monitor monitor)
            : base(monitor, "displayImage", "Displays image in Monitor")
        {
        }

        private void Run(ICommandInteraction writer, ReadFilePath pathToImage)
        {
            using(var file = new FileStream(pathToImage, FileMode.Open))
            {
                if(!CheckFormat(file))
                {
                    writer.WriteError("Bad image format. Supported formats: jpeg, png");
                    return;
                }

                writer.WriteRaw(InlineImage.Encode(file));
            }
        }

        private static bool CheckFormat(Stream file)
        {
            var head = new byte[8];
            file.Seek(0, SeekOrigin.Begin);
            file.Read(head, 0, 8);
            file.Seek(0, SeekOrigin.Begin);

            if(head.Take(JpegPrefix.Length).SequenceEqual(JpegPrefix))
            {
                return true;
            }

            if(head.Take(PngPrefix.Length).SequenceEqual(PngPrefix))
            {
                return true;
            }

            return false;
        }

        private static readonly byte[] JpegPrefix = {0xff, 0xd8, 0xff};
        private static readonly byte[] PngPrefix = {0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a};
    }
}
