//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB.HID
{
    public class ReportDescriptor : IProvidesDescriptor
    {
        public ReportDescriptor(byte[] rawDescriptor)
        {
            this.rawDescriptor = rawDescriptor;
        }

        public ReportDescriptor()
        {
            items = new List<UsbHidItem>();
        }

        public BitStream GetDescriptor(bool recursive, BitStream buffer = null)
        {
            if(buffer == null)
            {
                buffer = new BitStream();
            }

            if(rawDescriptor != null)
            {
                foreach(var b in rawDescriptor)
                {
                    buffer.Append(b);
                }
            }
            else
            {
                foreach(var item in items)
                {
                    item.FillDescriptor(buffer);
                }
            }

            return buffer;
        }

        public int RecursiveDescriptorLength => DescriptorLength;

        public int DescriptorLength => rawDescriptor != null
            ? rawDescriptor.Length
            : items.Sum(x => x.Data.Length + 1);

        private readonly List<UsbHidItem> items;
        private readonly byte[] rawDescriptor;

        public class UsbHidItem
        {
            public UsbHidItem()
            {
            }

            public void FillDescriptor(BitStream buffer)
            {
                buffer.Append((byte)((Tag << 4) | ((byte)Type << 2) | (byte)Size));
                for(var i = 0; i < Data.Length; i++)
                {
                    buffer.Append(Data[i]);
                }
            }

            public ItemSize Size { get; }
            public ItemType Type { get; }
            public byte Tag { get; }
            public byte[] Data { get; }
        }

        public enum ItemSize
        {
            Size0 = 0,
            Size1 = 1,
            Size2 = 2,
            Size4 = 3
        }

        public enum ItemType
        {
            Main = 0,
            Global = 1,
            Local = 2,
            Reserved = 3
        }
    }
}