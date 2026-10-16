//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities;

public class BitStreamReader
{
    public BitStreamReader(BitStream bitStream)
    {
        this.bitStream = bitStream;
    }

    public BitStreamReader(byte[] bytes)
    {
        this.bitStream = new BitStream(bytes);
    }

    public bool ReadAsBool()
    {
        var value = bitStream.AsByte(Offset, 1);
        Offset += 1;
        return value == 1 ? true : false;
    }

    public byte ReadAsByte(uint size)
    {
        var value = bitStream.AsByte(Offset, (int)size);
        Offset += size;
        return value;
    }

    public uint ReadAsUInt32(uint size)
    {
        var value = bitStream.AsUInt32(Offset, (int)size);
        Offset += size;
        return value;
    }

    public ulong ReadAsUInt64(uint size)
    {
        var value = bitStream.AsUInt64(Offset, (int)size);
        Offset += size;
        return value;
    }

    public void AlignTo(uint size)
    {
        var complement = Offset % size;
        if(complement > 0)
        {
            Offset += size - complement;
        }
    }

    public uint Offset
    {
        get => offset;
        set
        {
            if(value > bitStream.Length)
            {
                throw new ArgumentOutOfRangeException("Offset out of BitStream");
            }
            offset = value;
        }
    }

    private uint offset;

    private readonly BitStream bitStream;
}
