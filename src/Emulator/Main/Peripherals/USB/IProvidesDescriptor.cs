//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public interface IProvidesDescriptor
    {
        BitStream GetDescriptor(bool recursive, BitStream buffer = null);

        int RecursiveDescriptorLength { get; }

        int DescriptorLength { get; }
    }
}