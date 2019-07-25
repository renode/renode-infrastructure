//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.GDB
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ArgumentAttribute : Attribute
    {
        public char Separator { get; set; }
        public ArgumentEncoding Encoding { get; set; }

        public enum ArgumentEncoding
        {
            DecimalNumber,
            HexNumber,
            HexBytesString, // two hex digits for each byte
            BinaryBytes,
            HexString, // two hex digits for every character
            String
        }
    }
}

