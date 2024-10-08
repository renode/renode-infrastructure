//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.UserInterface;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals
{
    [Icon("box")]
    public interface IPeripheral : IEmulationElement, IAnalyzable
    {
        void Reset();
    }

    public static class IPeripheralExtensions
    {
        public static bool HasGPIO(this IPeripheral peripheral)
        {
            return peripheral is INumberedGPIOOutput || peripheral.GetType().GetProperties().Any(x => x.PropertyType == typeof(GPIO));
        }

        /// <summary>
        /// This method returns connected GPIO endpoints of a given peripheral.
        /// </summary>
        /// <returns>Collection of tuples: local GPIO name maped on endpoint to which it is connected. In case of INumberedGPIOOutput name is local number</returns>
        /// <param name="peripheral">Peripheral.</param>
        public static IEnumerable<Tuple<string, IGPIO>> GetGPIOs(this IPeripheral peripheral)
        {
            IEnumerable<Tuple<string, IGPIO>> result = null;
            var numberGPIOOuput = peripheral as INumberedGPIOOutput;
            if(numberGPIOOuput != null)
            {
                result = numberGPIOOuput.Connections.Select(x => Tuple.Create(x.Key.ToString(), x.Value));
            }

            var local = peripheral.GetType().GetProperties().Where(x => x.PropertyType == typeof(GPIO)).Select(x => Tuple.Create(x.Name, (IGPIO)((GPIO)x.GetValue(peripheral))));
            return result == null ? local : result.Union(local);
        }

        public static bool TryGetMachine(this IPeripheral @this, out IMachine machine)
        {
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(@this, out machine))
            {
                return true;
            }

            // let's try a fallback:
            // check if the provided object is not a container of peripherals -
            // in such case if and only if all peripherals belong to the same machine we can return it
            var simpleContainer = @this as ISimpleContainer;
            if(simpleContainer != null)
            {
                try
                {
                    var allMachines = simpleContainer.ChildCollection.Select(x => x.Value.GetMachine()).Distinct().ToArray();
                    if(allMachines.Length == 1)
                    {
                        machine = allMachines[0];
                        return true;
                    }
                }
                catch(Exception)
                {
                    // the try/catch here is to obtain a more readable stack trace;
                    // do nothing, we'll throw in a second anyway
                }
            }
            return false;
        }

        public static IMachine GetMachine(this IPeripheral @this)
        {
            if(@this.TryGetMachine(out var machine))
            {
                return machine;
            }
            throw new ArgumentException($"Couldn't find machine for a given peripheral of type {@this.GetType().FullName}.");
        }

        public static Endianess GetEndianness(this IPeripheral @this, Endianess? defaultEndianness = null)
        {
            if(@this is IEndiannessAware endiannessAwarePeripheral)
            {
                return endiannessAwarePeripheral.Endianness;
            }
            if(defaultEndianness != null)
            {
                return defaultEndianness.Value;
            }
            if(@this is IBusPeripheral busPeripheral)
            {
                return @this.GetMachine().GetSystemBus(busPeripheral).Endianess;
            }
            return @this.GetMachine().SystemBus.Endianess;
        }

        public static bool IsHostEndian(this IPeripheral @this)
        {
            return (@this.GetEndianness() == Endianess.LittleEndian) == BitConverter.IsLittleEndian;
        }

        public static string GetName(this IPeripheral @this)
        {
            var machine = @this.GetMachine();
            var machineName = EmulationManager.Instance.CurrentEmulation[machine];
            return $"{machineName}.{machine.GetLocalName(@this)}";
        }
    }
}
