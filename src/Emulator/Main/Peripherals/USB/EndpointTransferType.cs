//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core.USB
{
    public enum EndpointTransferType
    {
        Control = 0,
        Isochronous = 1,
        Bulk = 2,
        Interrupt = 3
    }
}