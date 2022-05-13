//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPU : IPeripheral, IHasOwnLife
    {
        void MapMemory(IMappedSegment segment);
        void UnmapMemory(Range range);
        void SetPageAccessViaIo(ulong address);
        void ClearPageAccessViaIo(ulong address);
        string Model { get; }
        RegisterValue PC { get; set; }
        bool IsHalted { get; set; }
        SystemBus Bus { get; }
        void UpdateContext();
        /// <summary>
        /// Returns true if the thread calling this property is possesed
        /// by the object.
        /// </summary>
        bool OnPossessedThread { get; }
        ulong ExecutedInstructions {get;}
        void SyncTime();
        void EnableProfiling();
    }

    public static class ICPUExtensions
    {
        public static string GetCPUThreadName(this ICPU cpu, Machine machine)
        {
            string machineName;
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(machine, out machineName))
            {
                machineName += ".";
            }
            return "{0}{1}[{2}]".FormatWith(machineName, machine.GetLocalName(cpu), machine.SystemBus.GetCPUId(cpu));
        }
    }
}

