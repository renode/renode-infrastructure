//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_EMU_Base : IEmulationElement
    {
        public SiLabs_EMU_Base(IMachine machine, IBusPeripheral parent = null)
        {
            this.machine = machine;
            this.parent = parent;

            isWFIHookAdded = false;
            inSleep = false; // EM1
            inDeepSleep = false; // EM2
            Em2CoreRetentionEnabled = true;
            noRetain = new List<IPeripheral>();
            currentBusFaulters = new Dictionary<IPeripheral, List<BusFaulter>>();
        }

        public void AddWFIHook()
        {
            if(!isWFIHookAdded)
            {
                CpuWfiState.Keys.ToList().ForEach(cpu => cpu.AddHookAtWfiStateChange(WFIStateChanged(cpu)));
            }
            isWFIHookAdded = true;
        }

        public void SetNoRetainPeripheralsList(List<IPeripheral> noRetain)
        {
            AddWFIHook();
            this.noRetain = noRetain;
        }

        public List<IPeripheral> GetNoRetainPeripherals()
        {
            return noRetain;
        }

        public void AddEnterSleepHook(Action hook)
        {
            AddWFIHook();
            EnterSleep += hook;
        }

        public void AddExitSleepHook(Action hook)
        {
            AddWFIHook();
            ExitSleep += hook;
        }

        public void AddEnterDeepSleepHook(Action hook)
        {
            AddWFIHook();
            EnterDeepSleep += hook;
        }

        public void AddExitDeepSleepHook(Action hook)
        {
            AddWFIHook();
            ExitDeepSleep += hook;
        }

        public void UpdatePeripheralEnabledStatus(IPeripheral peripheral, bool enabled)
        {
            if(peripheral == null)
            {
                throw new Exception("UpdatePeripheralEnabledStatus: peripheral is null");
            }

            if(enabled)
            {
                PowerUpPeripheral(peripheral);
            }
            else
            {
                PowerDownPeripheral(peripheral);
            }
        }

        public bool GetPeripheralEnabledStatus(IPeripheral peripheral)
        {
            if(peripheral == null)
            {
                return false;
            }
            return peripheral is ISiLabs_Retention retention && retention.Enabled;
        }

        public bool Em2CoreRetentionEnabled { get; set; }

        protected event Action EnterSleep;

        protected event Action ExitSleep;

        protected event Action EnterDeepSleep;

        protected event Action ExitDeepSleep;

        private Action<bool> WFIStateChanged(CortexM cpu)
        {
            return (wfi) =>
            {
                CpuWfiState[cpu] = wfi;

                if(!wfi && (inSleep || inDeepSleep))
                {
                    if(inDeepSleep)
                    {
                        ExitDeepSleep?.Invoke();
                        // The reset of the core must be done in the exit hook to
                        // keep the WFI state and the NVIC state for the wakeup sources
                        if(!Em2CoreRetentionEnabled)
                        {
                            ResetCortexMCores();
                        }
                        // This must be done after the reset of the cores because this modifies the NVIC state
                        PowerDownAllNoRetainPeripherals();
                        inDeepSleep = false;
                    }
                    else
                    {
                        ExitSleep?.Invoke();
                        inSleep = false;
                    }
                    return;
                }

                if(wfi && CpuWfiState.All(state => state.Value || state.Key.IsHalted))
                {
                    // A halted companion CPU is quiescent and does not prevent the
                    // remaining CPUs from entering a system energy mode.
                    var sleepingCpus = CpuWfiState.Where(state => state.Value).Select(state => state.Key);
                    if(sleepingCpus.All(sleepingCpu => sleepingCpu.NVIC.DeepSleepEnabled))
                    {
                        EnterDeepSleep?.Invoke();
                        inDeepSleep = true;
                    }
                    else
                    {
                        EnterSleep?.Invoke();
                        inSleep = true;
                    }
                }
            };
        }

        private void PowerDownAllNoRetainPeripherals()
        {
            foreach(var peripheral in noRetain)
            {
                string peripheralName = peripheral.GetName().Split('.')[1];
                var powerDownRegisters = (peripheral as ISiLabs_PartialRetention)?.GetPowerDownRegisters();
                if(powerDownRegisters == null || powerDownRegisters.Count == 0)
                {
                    this.Log(LogLevel.Noisy, "Powering down NoRetain peripheral {0}", peripheralName);
                    if(peripheral is ISiLabs_Retention retention)
                    {
                        retention.PowerDown();
                    }
                    else
                    {
                        peripheral.Reset();
                    }
                    continue;
                }

                this.Log(LogLevel.Noisy, "Powering down NoRetain peripheral {0} with partial retention", peripheralName);

                // Opt-in hook for peripherals that have C# state outside their DoubleWordRegister
                // collection (e.g. LDMA channels). No-op for peripherals that don't implement it.
                (peripheral as ISiLabs_PartialPowerDownHook)?.OnPartialPowerDown();

                foreach(var register in powerDownRegisters)
                {
                    register.Reset();
                }
            }
        }

        // emulate power down by adding a BusFaulter peripheral at
        // the same address which will intercept all transactions
        // targeting the peripheral.  Note, for a multi core
        // implementation (i.e. Logan) there could possibly be
        // collisions between the BusFaulter registrations (added
        // for em2 emulation) and the Interceptor registrations
        // (added for bus isolation modeling).  my idea here is,
        // if necessary, add a API that allows the IMU to register
        // a callback.  the PowerDown / PowerUp methods can then
        // invoke the callback to coordinate any overlap between
        // the BusFaulters and the Interceptors.
        private void PowerDownPeripheral(IPeripheral peripheral)
        {
            var sysbus = machine.GetSystemBus(parent);
            var sysbusRegistration = sysbus as IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>;

            foreach(var cpu in CpuWfiState.Keys)
            {
                string cpuName = cpu.GetName().Split('.')[1];
                string cpuConditionString = "initiator == " + cpuName;

                // don't create a new busfaulter if it already exists
                if(currentBusFaulters.ContainsKey(peripheral))
                {
                    continue;
                }

                // foreach registered address for the specified
                // peripheral, register a temporary busFaulter
                // peripheral into the memory map that overlaps
                // the same address range
                //
                // it is registered with a condition "initiator == cpu0"
                // so that the original peripheral does not need to be
                // unregistered from the memory map.
                //
                // renode allows a peripheral registered with a condition
                // to overlap the same address range as an existing
                // peripheral, and the peripheral with the condition
                // will supersede the original peripheral for any
                // transaction that meets the condition (in this case,
                // if "cpu" is the initiator that targets this address
                // range)
                //
                var rangesToFault = sysbus.GetRegistrationPoints(peripheral as IBusPeripheral)
                    .OfType<BusRangeRegistration>()
                    .Select(registration => registration.Range);

                foreach(var range in rangesToFault)
                {
                    var newBusFaulter = new BusFaulter(machine, cpu, range.StartAddress);
                    var busFaulterRegistration = new BusRangeRegistration(new Range(range.StartAddress, range.Size), cpuConditionString);
                    sysbusRegistration.Register(newBusFaulter, busFaulterRegistration.WithInitiatorAndStateMask(cpu, StateMask.AllAccess) as BusRangeRegistration);

                    string busFaulterName = string.Format("busfaulter_{0}_0x{1:X}", cpuName, range.StartAddress);
                    machine.SetLocalName(newBusFaulter, busFaulterName);
                    this.Log(LogLevel.Noisy, "busFaulter peripheral {0} added at {1}", busFaulterName, busFaulterRegistration);

                    // keep track of all busFaulters added to the memory map
                    if(currentBusFaulters.ContainsKey(peripheral))
                    {
                        currentBusFaulters[peripheral].Add(newBusFaulter);
                    }
                    else
                    {
                        currentBusFaulters.Add(peripheral, new List<BusFaulter>() { newBusFaulter });
                    }
                }
            }
        }

        private void PowerUpPeripheral(IPeripheral peripheral)
        {
            var sysbus = machine.GetSystemBus(parent);
            var sysbusRegistration = sysbus as IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>;

            if(currentBusFaulters.ContainsKey(peripheral))
            {
                // remove all busfaulters that overlap the peripheral in the memory map
                currentBusFaulters[peripheral].ForEach(x => sysbusRegistration.Unregister(x));
                currentBusFaulters.Remove(peripheral);
            }
        }

        private void ResetCortexMCores()
        {
            var cortexMCpus = machine.GetPeripheralsOfType<CortexM>().ToList();

            foreach(var cpu in cortexMCpus)
            {
                var vectorTableOffset = cpu.VectorTableOffset;
                var savedPendingByNvic = new Dictionary<NVIC, int[]>();

                cpu.IsHalted = true;
                cpu.CpuWaitSignal.Set(true);

                // Save pending interrupts before reset. On real hardware the IRQ line stays on
                // and the NVIC has IRQs pending when waking from EM2; we restore them after
                // reset to match that behavior.
                savedPendingByNvic[cpu.NVIC] = cpu.NVIC.GetPendingInterrupts().ToArray();

                // Reset the NVIC and the core
                // This resets CPU internal states that are not retained during EM2
                cpu.NVIC.Reset();
                cpu.Reset();

                // Reset vector table to the original value to have the valid initial PC
                cpu.VectorTableOffset = vectorTableOffset;

                // Re-apply pending interrupts so wake-from-EM2 matches hardware (IRQ stays pending)
                foreach(var kv in savedPendingByNvic)
                {
                    var nvic = kv.Key;
                    foreach(var irq in kv.Value)
                    {
                        nvic.SetPendingIRQ(irq);
                    }
                }

                cpu.CpuWaitSignal.Set(false);
            }
        }

        // keys are reference to the CPU(s) values are WFI state of the CPU
        private IDictionary<CortexM, bool> CpuWfiState
        {
            get
            {
                if(cpuWfiState == null)
                {
                    cpuWfiState = machine.SystemBus.GetCPUs().Where(cpu => cpu is CortexM).ToDictionary(cpu => cpu as CortexM, wfiState => false);
                }
                return cpuWfiState;
            }
        }

        private bool isWFIHookAdded;
        private bool inSleep;
        private bool inDeepSleep;
        private List<IPeripheral> noRetain;
        private IDictionary<CortexM, bool> cpuWfiState;
        private readonly IMachine machine;
        private readonly IBusPeripheral parent;

        // dictionary for keeping track of all busFaulters that
        // have been added to the memory map
        private readonly Dictionary<IPeripheral, List<BusFaulter>> currentBusFaulters;

        // dummy peripheral that always generates a bus fault on any read/write
        private class BusFaulter : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral
        {
            public BusFaulter(IMachine machine, CortexM cpu, ulong baseAddr)
            {
                this.machine = machine;
                this.cpu = cpu;
                this.baseAddr = baseAddr;
            }

            public void Reset()
            {
            }

            public uint ReadDoubleWord(long offset)
            {
                TriggerBusFault(offset);
                return 0;
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                TriggerBusFault(offset);
            }

            public ushort ReadWord(long offset)
            {
                TriggerBusFault(offset);
                return 0;
            }

            public void WriteWord(long offset, ushort value)
            {
                TriggerBusFault(offset);
            }

            public byte ReadByte(long offset)
            {
                TriggerBusFault(offset);
                return 0;
            }

            public void WriteByte(long offset, byte value)
            {
                TriggerBusFault(offset);
            }

            private void TriggerBusFault(long offset)
            {
                this.Log(LogLevel.Debug, "Access to powered down peripheral at 0x{0:X}", baseAddr + (ulong)offset);
                throw new BusAccessException(BusAccessError.GenericError);
            }

            private readonly IMachine machine;
            private readonly CortexM cpu;
            private readonly ulong baseAddr;
        }
    }
}
