//
// Copyright (c) 2010-2018 Antmicro
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
        public USBInterface(USBDeviceCore core,
                            byte identifier,
                            USBClassCode classCode = USBClassCode.NotSpecified,
                            byte subClassCode = 0,
                            byte protocol = 0,
                            string description = null) : base(9, (byte)DescriptorType.Interface)
        {
            this.core = core;
            endpoints = new List<USBEndpoint>();
            // endpoint 0 is always assumed to be a control endpoint
            endpoints.Add(core.ControlEndpoint);

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

        public USBInterface WithEndpoint(byte id, Direction direction, EndpointTransferType transferType, short maximumPacketSize, byte interval, out USBEndpoint createdEndpoint)
        {
            if(endpoints.Count == byte.MaxValue)
            {
                throw new ConstructionException("The maximal number of endpoints reached");
            }

            createdEndpoint = new USBEndpoint(core, id, direction, transferType, maximumPacketSize, interval);
            core.Device.USBCore.AddEndpoint(createdEndpoint);
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

        protected readonly USBDeviceCore core;

        private readonly List<USBEndpoint> endpoints;
    }
}
