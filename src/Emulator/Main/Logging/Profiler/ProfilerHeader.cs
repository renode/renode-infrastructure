//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Logging.Profiling
{
    public class ProfilerHeader
    {
        public ProfilerHeader()
        {
            buffer = new List<byte>();
        }

        public void RegisterPeripherals(IMachine machine)
        {
            var peripherals = machine.GetRegisteredPeripherals();
            var cpus = peripherals.Where(x => x.Peripheral is ICPU).ToList();

            buffer.AddRange(BitConverter.GetBytes(cpus.Count));

            foreach(var cpu in cpus)
            {
                buffer.AddRange(BitConverter.GetBytes(machine.SystemBus.GetCPUSlot(cpu.Peripheral as ICPU)));
                buffer.AddRange(BitConverter.GetBytes(cpu.Name.Length));
                buffer.AddRange(Encoding.ASCII.GetBytes(cpu.Name));
            }

            var busPeripherals = peripherals.Where(x => x.RegistrationPoint is BusRangeRegistration && x.Name != null);
            buffer.AddRange(BitConverter.GetBytes(busPeripherals.Count()));
            foreach(var peripheral in busPeripherals)
            {
                var name = peripheral.Name;
                if(peripheral.RegistrationPoint is BusMultiRegistration bmr)
                {
                    name = $"{bmr.ConnectionRegionName}@{name}";
                }
                buffer.AddRange(BitConverter.GetBytes(name.Length));
                buffer.AddRange(Encoding.ASCII.GetBytes(name));
                var registrationPoint = peripheral.RegistrationPoint as BusRangeRegistration;
                buffer.AddRange(BitConverter.GetBytes(registrationPoint.Range.StartAddress));
                buffer.AddRange(BitConverter.GetBytes(registrationPoint.Range.EndAddress));
            }
        }

        public byte[] Bytes => buffer.ToArray();

        private readonly List<byte> buffer;
    }
}
