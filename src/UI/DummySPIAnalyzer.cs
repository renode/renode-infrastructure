//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Extensions.Analyzers;

public class DummySPIAnalyzer : BasicPeripheralBackendAnalyzer<SPIBackend>, IExternal
{
    public override void Hide()
    {
    }

    public override void Show()
    {
        this.ErrorLog("SPI Analyzer is only supported in the UI mode");
    }

    public override void Clear()
    {
    }
}
