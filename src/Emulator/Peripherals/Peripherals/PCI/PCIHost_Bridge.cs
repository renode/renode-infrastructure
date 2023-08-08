//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.PCI
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class PCIHost_Bridge : SimpleContainer<IPCIePeripheral>, IPCIeRouter, IDoubleWordPeripheral, IAbsoluteAddressAware, IKnownSize
    {
        public PCIHost_Bridge(IMachine machine) : base(machine)
        {
            registers = CreateRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            var value = registers.Read(offset);
            return value;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void SetAbsoluteAddress(ulong address)
        {
            // Youngest bit denotes IO/Memory type, second one is reserved
            currentAccessAbsoluteAddress = address & ~3ul;
        }

        public override void Reset()
        {
            registers.Reset();

            address = 0;
        }

        public void RegisterBar(Range range, IPCIePeripheral peripheral, uint bar)
        {
            /* Unimplemented, dummy required by IPCIePeripheral interface */
            return;
        }

        public long Size => 0x10;

        protected struct TargetBar
        {
            public IPCIePeripheral TargetPeripheral;
            public uint BarNumber;
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ConfigAddress, new DoubleWordRegister(this)
                    .WithReservedBits(0, 2)
                    .WithValueField(2, 6, out registerNumber, name: "Register Number")
                    .WithValueField(8, 3, out functionNumber, name: "Function Number")
                    .WithValueField(11, 5, out deviceNumber, name: "Device Number")
                    .WithValueField(16, 8, out busNumber, name: "Bus Number")
                    .WithReservedBits(24, 7)
                    .WithFlag(31, out configEnabled, name: "Enable")
                    .WithWriteCallback((_, value) => { address = value; })
                },
                {(long)Registers.ConfigData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => WriteData((uint)value),
                        valueProviderCallback: _ => ReadData(), name: "CONFIG_DATA")
                },
            };
            return new DoubleWordRegisterCollection(this, registersDictionary);
        }

        private void WriteData(uint data)
        {
            if(!configEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Writing data to device in unhandled state, value : {0:X}", data);
                return;
            }

            if(selectedDevice != null)
            {
                selectedDevice.ConfigurationWriteDoubleWord((long)registerNumber.Value*4, data);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to write to unexsiting device. Value: {0:X}", data);
            }
        }

        private uint ReadData()
        {
            if(configEnabled.Value)
            {
                if(!ChildCollection.TryGetValue((int)deviceNumber.Value, out selectedDevice))
                {
                    this.Log(LogLevel.Warning, "Selected unregistered device number : {0}", deviceNumber.Value);
                    selectedDevice = null;
                    return 0;
                }
                //Address is multiplied by 4 cause Read function take offset as argument
                return selectedDevice.ConfigurationReadDoubleWord((long)registerNumber.Value * 4);
            }
            else
            {
                this.Log(LogLevel.Warning, "Reading data from device in unhandled state");
            }
            return 0;
        }

        private readonly DoubleWordRegisterCollection registers;
        private IFlagRegisterField configEnabled;
        private IValueRegisterField registerNumber;
        private IValueRegisterField functionNumber;
        private IValueRegisterField deviceNumber;
        private IValueRegisterField busNumber;
        private IPCIePeripheral selectedDevice;

        private ulong currentAccessAbsoluteAddress;
        private uint address;

        private enum Registers
        {
            ConfigAddress = 0x0,
            ConfigData = 0x4,
        }
    }
}
