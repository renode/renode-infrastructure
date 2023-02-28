//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Network
{
    public struct Clause45Address : IFormattable
    {
        public Clause45Address(byte deviceAddress, ushort registerAddress)
        {
            DeviceAddress = deviceAddress;
            RegisterAddress = registerAddress;
        }

        public string ToString(string format, IFormatProvider provider)
        {
            return $"{DeviceAddress}.{RegisterAddress}";
        }

        public string PrettyString => $"Clause45Address {{ DeviceAddress: {DeviceAddress}, RegisterAddress: {RegisterAddress} }}";

        public byte DeviceAddress { get; }
        public ushort RegisterAddress { get; }
    }
}
