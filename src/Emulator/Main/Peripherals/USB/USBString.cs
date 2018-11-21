//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Core.USB
{
    public class USBString
    {
        static USBString()
        {
            Empty = new USBString(string.Empty, 0);
            strings = new List<USBString>();
        }

        public static USBString FromString(string s)
        {
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

        public static USBString Empty { get; }

        public USBString(string value, byte id)
        {
            Value = value;
            Index = id;
        }

        public byte Index { get; }
        public string Value { get; }

        private static List<USBString> strings;
    }
}