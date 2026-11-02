//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals
{
    public abstract class BasicUARTBackendAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal
    {
        public UartExtensions.LineEnding LineEnding
        {
            get => ((UARTBackend)Backend).LineEnding;
            set => ((UARTBackend)Backend).LineEnding = value;
        }
    }
}