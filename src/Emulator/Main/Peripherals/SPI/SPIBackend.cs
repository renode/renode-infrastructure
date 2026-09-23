//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.SPI;

public class SPIBackend : IAnalyzableBackend<ISPIController>
{
    public void Attach(ISPIController spiController)
    {
        SPIController = spiController;
    }

    public void Detach()
    {
        SPIController = null;
    }

    public ISPIController SPIController { get; private set; }

    public IAnalyzable AnalyzableElement => SPIController;
}
