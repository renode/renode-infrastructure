//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Config.Devices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using System.Collections.Generic;
using Machine = Antmicro.Renode.Core.Machine;
using Antmicro.Renode.Core.Structure;
using System.IO;
using FdtSharp;
using Antmicro.Renode.Logging;
using System.Text;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public static class MachineExtensions
    {
        public static void LoadPeripheralsFromJSONFile(this IMachine machine, String fileName)
        {
            if(!File.Exists(fileName))
            {
                throw new RecoverableException("Cannot load devices configuration from file {0} as it does not exist.".FormatWith(fileName));
            }
            new DevicesConfig(File.ReadAllText(fileName), machine);
        }

        public static void LoadPeripheralsFromJSONString(this IMachine machine, String text)
        {
            new DevicesConfig(text, machine);
        }

        public static void LoadAtags(this IBusController bus, String bootargs, uint memorySize, uint beginAddress)
        {
            var atags = Misc.CreateAtags(bootargs, memorySize);
            //Fill ATAGs
            var addr = beginAddress;
            foreach(var elem in atags)
            {
                bus.WriteDoubleWord(addr, elem);
                addr += 4;
            }
        }

        public static void LoadFdt(this IBusController sysbus, string file, ulong address, string bootargs = null, bool append = true, string disabledNodes = "", ICPU context = null)
        {
            if(!File.Exists(file))
            {
                throw new RecoverableException("FDT file {0} not found".FormatWith(file));
            }

            var fdtBlob = File.ReadAllBytes(file);
            if(bootargs == null)
            {
                sysbus.WriteBytes(fdtBlob, address, true, context);
                return;
            }
            var fdt = new FlattenedDeviceTree(fdtBlob);
            var chosenNode = fdt.Root.Subnodes.FirstOrDefault(x => x.Name == "chosen");
            if(chosenNode == null)
            {
                chosenNode = new TreeNode { Name = "chosen" };
                fdt.Root.Subnodes.Add(chosenNode);
            }
            var bootargsProperty = chosenNode.Properties.FirstOrDefault(x => x.Name == "bootargs");
            if(bootargsProperty == null)
            {
                bootargsProperty = new Property("bootargs", new byte[] { 0 });
                chosenNode.Properties.Add(bootargsProperty);
            }
            var oldBootargs = bootargsProperty.GetDataAsString();
            if(append)
            {
                bootargs = oldBootargs + bootargs;
            }
            if(oldBootargs != bootargs)
            {
                sysbus.DebugLog("Bootargs altered from '{0}' to '{1}'.", oldBootargs, bootargs);
            }
            bootargsProperty.PutDataAsString(bootargs);

            var disabledNodeNames = disabledNodes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] disabledValue = Encoding.ASCII.GetBytes("disabled");
            foreach(var deviceName in disabledNodeNames)
            {
                TreeNode node = fdt.Root.Descendants.FirstOrDefault(x => x.Name == deviceName);
                if(node == null)
                {
                    throw new RecoverableException(String.Format("Device {0} not found.", deviceName));
                }
                else
                {
                    Property statusProperty = node.Properties.FirstOrDefault(x => x.Name == "status");
                    if(statusProperty != null)
                    {
                        node.Properties.Remove(statusProperty);
                    }
                    statusProperty = new Property("status", disabledValue);
                    node.Properties.Add(statusProperty);
                }
            }

            fdtBlob = fdt.GetBinaryBlob();
            sysbus.WriteBytes(fdtBlob, address, true, context);
        }

        public static void WriteASCIIString(this IBusController sysbus, ulong address, string stringToLoad, ICPU context = null)
        {
            sysbus.WriteBytes(Encoding.ASCII.GetBytes(stringToLoad), address, true, context);
        }

        public static Dictionary<PeripheralTreeEntry, IEnumerable<IRegistrationPoint>> GetPeripheralsWithAllRegistrationPoints(this IMachine machine)
        {
            var result = new Dictionary<PeripheralTreeEntry, IEnumerable<IRegistrationPoint>>();

            var peripheralEntries = machine.GetRegisteredPeripherals().ToArray();
            var sysbusEntry = peripheralEntries.First(x => x.Name == Machine.SystemBusName).Peripheral;
            foreach(var entryList in peripheralEntries.OrderBy(x => x.Name).GroupBy(x => x.Peripheral))
            {
                var uniqueEntryList = entryList.DistinctBy(x => x.RegistrationPoint).ToArray();
                var entry = uniqueEntryList.FirstOrDefault();
                if(entry != null)
                {
                    result.Add(entry, uniqueEntryList.Select(x => x.RegistrationPoint).ToList());
                }
                // The peripherals command will not print the entry under sysbus if its first occurence in entryList is not directly a child of sysbus.
                // This check prevents loosing sysbus registration info in peripherals command output.
                if(entry.Parent != sysbusEntry)
                {
                    entry = uniqueEntryList.FirstOrDefault(x => x.Parent == sysbusEntry);
                    if(entry != null)
                    {
                        result.Add(entry, uniqueEntryList.Where(x => x.Parent == sysbusEntry).Select(x => x.RegistrationPoint).ToList());
                    }
                }
            }

            return result;
        }
    }
}
