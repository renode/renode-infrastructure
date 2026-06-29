//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USB;

[LeastSignificantByteFirst]
public struct EndpointDescriptor : IDescriptor
{
    [PacketField, Offset(bytes: 0)]
    public byte Length;
    [PacketField, Offset(bytes: 1)]
    public byte Type;

    [PacketField, Offset(bytes: 2, bits: 0), Width(bits: 4)]
    public byte EndpointNumber;

    [PacketField, Offset(bytes: 2, bits: 7), Width(bits: 1)]
    public EndpointDirection Direction;

    [PacketField, Offset(bytes: 3)]
    public byte Attributes;
    [PacketField, Offset(bytes: 4)]
    public ushort MaxPacketSize;
    [PacketField, Offset(bytes: 6)]
    public byte Interval;

    byte IDescriptor.Type => Type;

    public byte[] AsBytes => Packet.Encode(this);

    public override string ToString()
    {
        return $"[EndpointDescriptor Length = {Length}, Type = {Type}, EndpointNumber = {EndpointNumber}, Direction = {Direction}, Attributes = {Attributes}, MaxPacketSize = {MaxPacketSize}, Interval = {Interval}]";
    }
}

public enum EndpointDirection : byte
{
    Out,
    In
}
