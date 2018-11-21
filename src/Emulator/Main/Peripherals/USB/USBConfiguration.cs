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
    public class USBConfiguration : DescriptorProvider
    {
        public USBConfiguration(IUSBDevice device,
                                byte identifier,
                                string description = null,
                                bool selfPowered = false,
                                bool remoteWakeup = false,
                                short maximalPower = 0) : base(9, (byte)DescriptorType.Configuration)
        {
            if(maximalPower > 500 || maximalPower < 0)
            {
                throw new ConstructionException("Maximal power should be between 0 and 500 mA");
            }

            this.device = device;

            interfaces = new List<USBInterface>();

            Identifier = identifier;
            Description = description;
            MaximalPower = maximalPower;
            SelfPowered = selfPowered;
            RemoteWakeup = remoteWakeup;

            RegisterSubdescriptors(interfaces);
        }

        public USBConfiguration WithInterface<T>(byte subClassCode,
                                                 byte protocol,
                                                 string description = null,
                                                 Action<T> configure = null) where T : USBInterface
        {
            var newInterface = (T)Activator.CreateInstance(typeof(T), device, (byte)interfaces.Count, subClassCode, protocol, description);
            configure?.Invoke(newInterface);
            return WithInterface(newInterface);
        }

        public USBConfiguration WithInterface(USBInterface iface)
        {
            if(interfaces.Count == byte.MaxValue)
            {
                throw new ConstructionException("The maximal number of interfaces reached");
            }

            interfaces.Add(iface);
            return this;
        }

        public USBConfiguration WithInterface<T>(T iface, Action<USBInterface> configure = null) where T : USBInterface
        {
            if(interfaces.Count == byte.MaxValue)
            {
                throw new ConstructionException("The maximal number of interfaces reached");
            }

            configure?.Invoke(iface);
            interfaces.Add(iface);
            return this;
        }

        public USBConfiguration WithInterface(USBClassCode classCode = USBClassCode.NotSpecified,
                                              byte subClassCode = 0,
                                              byte protocol = 0,
                                              string description = null,
                                              Action<USBInterface> configure = null)
        {
            var newInterface = new USBInterface(device, (byte)interfaces.Count, classCode, subClassCode, protocol, description);
            configure?.Invoke(newInterface);
            return WithInterface(newInterface);
        }

        public byte Identifier { get; }
        public string Description { get; }
        public short MaximalPower { get; }
        public bool SelfPowered { get; }
        public bool RemoteWakeup { get; }

        public IReadOnlyCollection<USBInterface> Interfaces => interfaces;

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((short)RecursiveDescriptorLength)
                .Append((byte)Interfaces.Count)
                .Append(Identifier)
                .Append(USBString.FromString(Description).Index)
                .Append((byte)(((SelfPowered ? 1 : 0) << 6) | ((RemoteWakeup ? 1 : 0) << 5)))
                .Append((byte)((MaximalPower + 1) / 2));
        }

        private readonly List<USBInterface> interfaces;
        private readonly IUSBDevice device;
    }
}