//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using TermSharp.Misc;
using Antmicro.Renode.Exceptions;

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
        public void Run(ICommandInteraction writer, PathToken pathToImage)
        {
            if(!File.Exists(pathToImage.Value))
            {
                writer.WriteError($"No such file {pathToImage.Value}");
                return;
            }

            try
            {
                var image = new Bitmap(pathToImage.Value);
                if(!ImageFormat.Jpeg.Equals(image.RawFormat) && !ImageFormat.Png.Equals(image.RawFormat))
                {
                    writer.WriteError("Bad image format. Supported formats: jpeg, png");
                    return;
                }
                writer.WriteRaw(InlineImage.Encode(image));
            }
            catch(Exception e)
            {
                writer.WriteError($"There was an error when loading the image: {(e.Message)}");
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, DecimalIntegerToken width, DecimalIntegerToken height)
        {
            if(width.Value <= 0 || height.Value <= 0)
            {
                throw new RecoverableException("Width and height must be positive values");
            }

            var image = new Bitmap((int)width.Value, (int)height.Value);
            using(var graphics = Graphics.FromImage(image))
            {
                var imageSize = new Rectangle(0, 0, (int)width.Value, (int)height.Value);
                graphics.FillRectangle(Brushes.White, imageSize);
            }
            writer.WriteRaw(InlineImage.Encode(image));
        }

        public DisplayImageCommand(Monitor monitor)
            : base(monitor, "displayImage", "Displays image in Monitor")
        {
        }
    }
}
