//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPU : IPeripheral, IHasOwnLife, IHaltable
    {
        string Architecture { get; }
        uint MultiprocessingId { get; }
        string Model { get; }
        RegisterValue PC { get; set; }
        // Extend `IsHalted` with a getter by using the `new` keyword
        new bool IsHalted { get; set; }
        IBusController Bus { get; }
        /// <summary>
        /// Returns true if the thread calling this property is possesed
        /// by the object.
        /// </summary>
        bool OnPossessedThread { get; }
        ulong ExecutedInstructions { get; }
        void SyncTime();
        event Action<HaltArguments> Halted;
        TimeHandle TimeHandle { get; }

        ulong Step(int count = 1);
        ExecutionMode ExecutionMode { get; set; }

        ELFSharp.ELF.Endianess Endianness { get; }

        EmulationCPUState EmulationState { get; }
        event Action<ICPU, EmulationCPUState, EmulationCPUState> StateChanged;
    }

    public static class ICPUExtensions
    {
        public static string GetCPUThreadName(this ICPU cpu, IMachine machine)
        {
            string machineName;
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(machine, out machineName))
            {
                machineName += ".";
            }
            return "{0}{1}[{2}]".FormatWith(machineName, machine.GetLocalName(cpu), machine.SystemBus.GetCPUSlot(cpu));
        }
    }
}

