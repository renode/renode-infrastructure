//
// Copyright (c) 2010-2024 Antmicro
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
        public SPIMultiplexer(IMachine machine, bool suppressExplicitFinishTransmission = true) : base(machine)
        {
            this.suppressExplicitFinishTransmission = suppressExplicitFinishTransmission;
            chipSelects = new HashSet<int>();
            activeLowSignals = new HashSet<int>();
            inputState = new Dictionary<int, bool>();
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "GPIO #{0} set to {1}", number, value);
            inputState[number] = value;
            UpdateChipSelectState();
        }

        public void SetActiveLow(int number)
        {
            activeLowSignals.Add(number);
            UpdateChipSelectState();
        }

        public void SetActiveHigh(int number)
        {
            activeLowSignals.Remove(number);
            UpdateChipSelectState();
        }

        public override void Reset()
        {
            inputState.Clear();
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
            if(suppressExplicitFinishTransmission)
            {
                return;
            }

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
            FinishTransmissionByAddress(deviceAddress);
        }

        private void FinishTransmissionByAddress(int deviceAddress)
        {
            if(!TryGetByAddress(deviceAddress, out var device))
            {
                this.Log(LogLevel.Warning, "Tried to finish transmission to device 0x{0:X}, but it's not connected", deviceAddress);
                return;
            }

            this.Log(LogLevel.Noisy, "Finisihing transmission on device 0x{0:X}", deviceAddress);
            device.FinishTransmission();
        }

        private void UpdateChipSelectState()
        {
            foreach(var state in inputState)
            {
                var number = state.Key;
                var value = state.Value;

                var active = activeLowSignals.Contains(number) ? !value : value;
                if(active)
                {
                    chipSelects.Add(number);
                }
                else
                {
                    chipSelects.Remove(number);
                    FinishTransmissionByAddress(number);
                }
            }
        }

        private readonly HashSet<int> chipSelects;
        private readonly HashSet<int> activeLowSignals;
        private readonly Dictionary<int, bool> inputState;
        private readonly bool suppressExplicitFinishTransmission;
    }
}

