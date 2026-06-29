//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Extensions.Utilities.USB;

public struct RawDescriptor : IDescriptor
{
    public RawDescriptor(byte[] bytes)
    {
        if(bytes.Length < 2)
        {
            throw new ArgumentException("Descriptor must be at least two bytes long");
        }
        this.bytes = bytes;
    }

    public byte Type => bytes[1];

    public byte[] AsBytes => bytes;

    public override string ToString() => $"[RawDescriptor {Misc.PrettyPrintCollectionHex(bytes)}]";

    private readonly byte[] bytes;
}
