//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Register field that provides a value of type T, where T is a <see cref="Packet"> struct.
    /// The maximum bit width of T is 64 bits.
    /// The field must not be larger than the packet's width, but it can be smaller.
    /// In this case, the remaining bits at the end of the packet will be zero-filled.
    /// </summary>
    public interface IPacketRegisterField<T> : IRegisterField<T> where T : struct
    {
    }
}