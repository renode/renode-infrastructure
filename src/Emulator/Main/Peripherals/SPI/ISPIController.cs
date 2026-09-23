//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.SPI;

public interface ISPIController : IPeripheral
{
    event Action<(byte pico, byte poci), IPeripheral> BytesExchanged;
}
