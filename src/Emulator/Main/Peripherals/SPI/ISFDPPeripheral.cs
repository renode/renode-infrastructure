//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.SPI
{
    public interface ISFDPPeripheral : IPeripheral
    {
        byte[] SFDPSignature { get; set; }
    }
}
