//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    public interface IDelayableUART : IUART
    {
        TimeInterval CharacterTransmissionDelay { get; }
    }
}
