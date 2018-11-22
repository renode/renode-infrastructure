//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public abstract class DescriptorProvider : IProvidesDescriptor
    {
        public DescriptorProvider(byte type)
        {
            this.type = type;
            subdescriptors = new List<IEnumerable<IProvidesDescriptor>>();
        }

        public DescriptorProvider(byte descriptorLength, byte type) : this(type)
        {
            DescriptorLength = descriptorLength;
        }

        public BitStream GetDescriptor(bool recursive, BitStream buffer = null)
        {
            if(buffer == null)
            {
                buffer = new BitStream();
            }

            buffer
                .Append((byte)DescriptorLength)
                .Append((byte)type);

            FillDescriptor(buffer);

            if(recursive)
            {
                foreach(var sds in subdescriptors)
                {
                    foreach(var sd in sds)
                    {
                        sd.GetDescriptor(true, buffer);
                    }
                }
            }

            return buffer;
        }

        public virtual int DescriptorLength { get; }
        public int RecursiveDescriptorLength => DescriptorLength + subdescriptors.Sum(sds => sds.Sum(sd => sd.RecursiveDescriptorLength));

        protected abstract void FillDescriptor(BitStream buffer);

        protected void RegisterSubdescriptor(IProvidesDescriptor subdescriptor, int? index = null)
        {
            subdescriptors.Insert(index ?? subdescriptors.Count , new IProvidesDescriptor[] { subdescriptor });
        }

        protected void RegisterSubdescriptors(IEnumerable<IProvidesDescriptor> subdescriptors, int? index = null)
        {
            this.subdescriptors.Insert(index ?? this.subdescriptors.Count , subdescriptors);
        }

        private readonly List<IEnumerable<IProvidesDescriptor>> subdescriptors;
        private readonly byte type;
    }
}