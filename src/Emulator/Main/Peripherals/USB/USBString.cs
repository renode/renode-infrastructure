//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public class USBString : DescriptorProvider
    {
        static USBString()
        {
            Empty = new USBString(string.Empty, 0);
            strings = new List<USBString>();
        }

        public static USBString FromString(string s)
        {
            if(string.IsNullOrWhiteSpace(s))
            {
                return Empty;
            }

            var usbString = strings.FirstOrDefault(x => x.Value == s);
            if(usbString == null)
            {
                usbString = new USBString(s, checked((byte)(strings.Count + 1)));
                strings.Add(usbString);
            }

            return usbString;
        }

        public static USBString FromId(int id)
        {
            if(id <= 0 || id > strings.Count)
            {
                return null;
            }
            return strings[id - 1];
        }

        public static BitStream GetSupportedLanguagesDescriptor()
        {
            // for now we hardcode just one language
            return new BitStream()
                .Append(4)
                .Append((byte)DescriptorType.String)
                .Append((short)LanguageCode.EnglishUnitedStates);
        }

        public static USBString Empty { get; }

        private static List<USBString> strings;

        public byte Index { get; }
        public string Value { get; }

        public override int DescriptorLength => 2 + Encoding.Unicode.GetByteCount(Value);

        protected USBString(string value, byte id) : base((byte)DescriptorType.String)
        {
            Value = value;
            Index = id;
        }

        protected override void FillDescriptor(BitStream buffer)
        {
            var stringAsUnicodeBytes = Encoding.Unicode.GetBytes(Value);
            foreach(var b in stringAsUnicodeBytes)
            {
                buffer.Append(b);
            }
        }

        public enum LanguageCode : short
        {
            EnglishUnitedStates = 0x0409
        }
    }
}