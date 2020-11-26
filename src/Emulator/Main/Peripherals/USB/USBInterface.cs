//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public class USBInterface : DescriptorProvider
    {
        public USBInterface(IUSBDevice device,
                            byte identifier,
                            USBClassCode classCode = USBClassCode.NotSpecified,
                            byte subClassCode = 0,
                            byte protocol = 0,
                            string description = null) : base(9, (byte)DescriptorType.Interface)
        {
            this.device = device;
            endpoints = new List<USBEndpoint>();

            Identifier = identifier;
            Class = classCode;
            SubClass = subClassCode;
            Protocol = protocol;
            Description = description;

            RegisterSubdescriptors(endpoints);
        }

        public virtual BitStream HandleRequest(SetupPacket packet)
        {
            return BitStream.Empty;
        }

        public USBInterface WithEndpoint(Direction direction, EndpointTransferType transferType, short maximumPacketSize, byte interval, out USBEndpoint createdEndpoint, byte? id = null)
        {
            if(!id.HasValue && endpoints.Count == byte.MaxValue)
            {
                throw new ConstructionException("The maximal number of endpoints reached");
            }

            var localId = id ?? (byte)(endpoints.Count + 1);
            if(endpoints.Any(x => x.Identifier == localId && x.Direction == direction))
            {
                throw new ConstructionException($"Endpoint with id {localId} in direction {direction} already definied");
            }

            createdEndpoint = new USBEndpoint(device, localId, direction, transferType, maximumPacketSize, interval);
            endpoints.Add(createdEndpoint);
            return this;
        }

        public USBClassCode Class { get; }
        public byte SubClass { get; }
        public byte Protocol { get; }
        public string Description { get; }
        public byte Identifier { get; }
        public IReadOnlyCollection<USBEndpoint> Endpoints => endpoints;

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append(Identifier)
                .Append(0) // TODO: implement alternate setting
                .Append((byte)Endpoints.Count)
                .Append((byte)Class)
                .Append(SubClass)
                .Append(Protocol)
                .Append(USBString.FromString(Description).Index);
        }

        protected readonly IUSBDevice device;

        private readonly List<USBEndpoint> endpoints;
    }
}
