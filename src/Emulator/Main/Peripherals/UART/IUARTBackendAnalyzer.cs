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
    public interface IUARTBackendAnalyzer : IAnalyzableBackendAnalyzer<UARTBackend>, IExternal
    {
        public UartExtensions.LineEnding LineEnding { get; set; }

        public bool SelfEcho { get; set; }
    }
}