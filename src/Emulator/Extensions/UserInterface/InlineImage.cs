//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;

namespace Antmicro.Renode.UserInterface
{
    public static class InlineImage
    {
        public static string Encode(MemoryStream image)
        {
            var encodedImage = Convert.ToBase64String(image.GetBuffer());
            return GenerateControlSequence(encodedImage);
        }

        public static string Encode(Stream image)
        {
            if(image is MemoryStream)
            {
                return Encode((MemoryStream)image);
            }
            using(var stream = new MemoryStream())
            {
                image.CopyTo(stream);
                return Encode(stream);
            }
        }

        private static string GenerateControlSequence(string encodedImage)
        {
            return $"{Escape}{OperatingSystemCommand}{InlineImageCode};File=inline=1:{encodedImage}{Bell}";
        }

        private const int InlineImageCode = 1337;
        private const char Escape = (char)0x1B;
        private const char OperatingSystemCommand = ']';
        private const char Bell = (char)0x7;
    }
}
