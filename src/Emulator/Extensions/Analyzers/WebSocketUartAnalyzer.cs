//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Analyzers
{
    [Transient]
    public class WebSocketUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal
    {
        public override void Show()
        {
        }

        public override void Hide()
        {
        }

        public override void Clear()
        {
        }

        public IUART UART => (Backend as UARTBackend)?.UART;
    }
}