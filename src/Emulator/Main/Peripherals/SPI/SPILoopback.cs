//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.SPI
{
    //This is a dummy device, emulating connection between MISO and MOSI lines.
    public class SPILoopback : ISPIPeripheral
    {
        public void FinishTransmission()
        {
        }

        public void Reset()
        {
        }

        public byte Transmit(byte data)
        {
            return data;
        }
    }
}
