//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Extensions.Mocks
{
    /* Example usage:
    emulation MockSpiHost sysbus.spi_device
    host.MockSpiHost WriteBytes 0x0 "010203040506070809"
    host.MockSpiHost ReadBytes 9
    */

    public static class MockSpiHost
    {
        public static void AddMockSpiHost(this Emulation emulation, ISPIPeripheral device, string name = "MockSpiHost")
        {
            emulation.HostMachine.AddHostMachineElement(new SpiHost(device), name);
        }
    }

    public class SpiHost : IHostMachineElement
    {
        public SpiHost(ISPIPeripheral device)
        {
            this.device = device;
        }

        public void WriteBytes(string hexString)
        {
            var dataToSend = Misc.HexStringToByteArray(hexString);

            foreach(var b in dataToSend)
            {
                device.Transmit(b);
            }
            device.FinishTransmission();
        }

        public byte[] ReadBytes(int count)
        {
            var returnedData = new byte[count];
            for(var index = 0; index < count ; index++)
            {
                // This will block the monitor until finished.
                returnedData[index] = device.Transmit(0);
            }
            return returnedData;
        }

        private readonly ISPIPeripheral device;
    }
}
