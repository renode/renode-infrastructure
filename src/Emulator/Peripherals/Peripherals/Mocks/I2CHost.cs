//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Extensions.Mocks
{
    /* Example usage:
    emulation AddMockI2CHost sysbus.i2c0
    host.MockI2CHost WriteBytes 0x0 "010203040506070809"
    host.MockI2CHost ReadBytes 0x0 9
    host.MockI2CHost WriteBytes 0x0 "01"
    */
    public static class MockI2CHost
    {
        public static void AddMockI2CHost(this Emulation emulation, II2CPeripheral device, string name = "MockI2CHost")
        {
            emulation.HostMachine.AddHostMachineElement(new I2CHost(device), name);
        }
    }

    public class I2CHost : IHostMachineElement
    {
        public I2CHost(II2CPeripheral device)
        {
            currentSlave = device;
        }

        public void WriteBytes(uint addr, string hexString)
        {
            // addr + read_bit
            var firstByte = (byte)(addr << 1) | 0x0;
            var dataToSend = Misc.HexStringToByteArray(hexString);
            var bytes = new byte[dataToSend.Length + 1];

            bytes[0] = (byte)firstByte;
            Buffer.BlockCopy(dataToSend, 0, bytes, 1, dataToSend.Length);
            currentSlave.Write(bytes);
            currentSlave.FinishTransmission();
        }

        public byte[] ReadBytes(uint addr, int count)
        {
            var firstByte = (byte)(addr << 1) | 0x1;
            currentSlave.Write(new byte[] { (byte)firstByte, (byte)count });
            currentSlave.FinishTransmission();

            var returnedData = new List<byte>();
            while(returnedData.Count != count)
            {
                // Aggressive approach - this will block the monitor until finished.
                returnedData.AddRange(currentSlave.Read(count - returnedData.Count));
            }
            return returnedData.ToArray();
        }

        private readonly II2CPeripheral currentSlave;
    }
}
