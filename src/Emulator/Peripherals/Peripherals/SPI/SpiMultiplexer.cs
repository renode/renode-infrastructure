//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class SPIMultiplexer : SimpleContainer<ISPIPeripheral>, IGPIOReceiver, ISPIPeripheral
    {
        public SPIMultiplexer(Machine machine) : base(machine)
        {
            chipSelects = new HashSet<int>();
        }

        public void OnGPIO(int number, bool value)
        {
            if(value)
            {
                chipSelects.Add(number);
            }
            else
            {
                chipSelects.Remove(number);
            }
        }

        public override void Reset()
        {
        }

        public byte Transmit(byte data)
        {
            if(chipSelects.Count == 0)
            {
                this.Log(LogLevel.Warning, "Tried to transmit byte 0x{0:X}, but no device is currently selected - ignoring transfer and returning a dummy byte", data);
                return 0xFF;
            }

            if(chipSelects.Count > 1)
            {
                this.Log(LogLevel.Warning, "Tried to transmit byte 0x{0:X}, but multiple devices are currently selected ({1}) - ignoring transfer and returning a dummy byte", data, Misc.PrettyPrintCollectionHex(chipSelects));
                return 0xFF;
            }

            var deviceAddress = chipSelects.First();
            if(!TryGetByAddress(deviceAddress, out var device))
            {
                this.Log(LogLevel.Warning, "Tried to transmit byte 0x{0:X} to device 0x{1:X}, but it's not connected - ignoring transfer and returning a dummy byte", data, deviceAddress);
                return 0xFF;
            }

            return device.Transmit(data);
        }

        public void FinishTransmission()
        {
            if(chipSelects.Count == 0)
            {
                this.Log(LogLevel.Warning, "Tried to finish transmission, but no device is currently selected");
                return;
            }

            if(chipSelects.Count > 1)
            {
                this.Log(LogLevel.Warning, "Tried to finish transmission, but multiple devices are currently selected ({0})", Misc.PrettyPrintCollectionHex(chipSelects));
                return;
            }

            var deviceAddress = chipSelects.First();
            if(!TryGetByAddress(deviceAddress, out var device))
            {
                this.Log(LogLevel.Warning, "Tried to finish transmission to device 0x{0:X}, but it's not connected", deviceAddress);
                return;
            }

            device.FinishTransmission();
        }

        private readonly HashSet<int> chipSelects;
    }
}

