//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core.USB
{
    public enum DescriptorType : byte
    {
        Device = 1,
        Configuration = 2,
        String = 3,
        Interface = 4,
        Endpoint = 5,
        DeviceQualifier = 6,
        OtherSpeedConfiguration = 7,
        InterfacePower = 8
    }
}