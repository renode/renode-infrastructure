//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusAccessException : RecoverableException
    {
        public BusAccessException(BusAccessError error) : base($"Bus access failed with error: {error}")
        {
            Error = error;
        }

        public BusAccessError Error { get; }
    }

    public enum BusAccessError
    {
        GenericError,
        AddressError,
        CommandError,
        BurstError,
        ByteEnableError,
    }
}
