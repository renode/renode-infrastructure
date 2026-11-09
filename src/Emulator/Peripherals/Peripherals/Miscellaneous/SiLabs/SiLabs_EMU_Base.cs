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
    public enum SiLabs_EnergyMode
    {
        EM0,
        EM1,
        EM1P,
        EM2,
    }

    public class SiLabs_EMU_Base : IEmulationElement
    {
        public SiLabs_EMU_Base(IMachine machine, IBusPeripheral parent = null)
        {
            this.machine = machine;
            this.parent = parent;

            isWFIHookAdded = false;
            CurrentEnergyMode = SiLabs_EnergyMode.EM0;
            pd1PoweredDownSinceEm0 = false;
            Em2CoreRetentionEnabled = true;
            noRetain = new List<IPeripheral>();
            currentBusFaulters = new Dictionary<IPeripheral, List<BusFaulter>>();
            em1pRequesters = new HashSet<object>();
            em1pHostClockRequesters = new HashSet<object>();
            qChannelBusyPeripherals = new HashSet<object>();
            radioSequencerCpus = new HashSet<CortexM>();
            HostClocksEnabled = true;
            HostAllClocksEnabled = true;
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

        public void AddEnterEm1pHook(Action hook)
        {
            AddWFIHook();
            EnterEm1p += hook;
        }

        public void AddExitEm1pHook(Action hook)
        {
            AddWFIHook();
            ExitEm1p += hook;
        }

        // A radio sequencer core (e.g. the LPW Cortex-M0+) executes a plain WFI while the
        // host is in deep sleep. Its WFI state must not select the system energy mode; the
        // radio instead keeps the system in EM1P through SetEm1pRequest.
        public void RegisterRadioSequencerCpu(CortexM cpu)
        {
            if(cpu == null)
            {
                throw new ArgumentNullException(nameof(cpu));
            }
            AddWFIHook();
            radioSequencerCpus.Add(cpu);
        }

        public void SetEm1pRequest(object requester, bool request)
        {
            if(requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            AddWFIHook();

            var changed = request ? em1pRequesters.Add(requester) : em1pRequesters.Remove(requester);
            if(!changed)
            {
                return;
            }
            this.Log(LogLevel.Noisy, "EM1P request from {0} {1}", requester, request ? "asserted" : "deasserted");
            UpdateEnergyMode();
        }

        public void AddEnterHostClockGateHook(Action hook)
        {
            AddWFIHook();
            EnterHostClockGate += hook;
        }

        public void AddExitHostClockGateHook(Action hook)
        {
            AddWFIHook();
            ExitHostClockGate += hook;
        }

        public void AddEnterHostAllClockGateHook(Action hook)
        {
            AddWFIHook();
            EnterHostAllClockGate += hook;
        }

        public void AddExitHostAllClockGateHook(Action hook)
        {
            AddWFIHook();
            ExitHostAllClockGate += hook;
        }

        public void SetEm1pHostClockRequest(object requester, bool request)
        {
            if(requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            AddWFIHook();

            var changed = request ? em1pHostClockRequesters.Add(requester) : em1pHostClockRequesters.Remove(requester);
            if(!changed)
            {
                return;
            }
            this.Log(LogLevel.Noisy, "EM1P host-clock request from {0} {1}", requester, request ? "asserted" : "deasserted");
            if(isUpdatingEnergyMode || isUpdatingHostClocks)
            {
                hostClockUpdatePending = true;
                return;
            }
            UpdateHostClocks();
        }

        // EM2 entry starts with a quiescence request to every Q-channel peripheral. A peripheral
        // that is not in a safe state delays the request, and the EMU keeps the host in sleep until
        // all of them accept.
        public void SetQChannelBusy(object peripheral, bool busy)
        {
            if(peripheral == null)
            {
                throw new ArgumentNullException(nameof(peripheral));
            }
            AddWFIHook();

            var changed = busy ? qChannelBusyPeripherals.Add(peripheral) : qChannelBusyPeripherals.Remove(peripheral);
            if(!changed)
            {
                return;
            }
            this.Log(LogLevel.Noisy, "Q-channel peripheral {0} {1}", peripheral, busy ? "busy" : "ready");
            UpdateEnergyMode();
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

        public SiLabs_EnergyMode CurrentEnergyMode { get; private set; }

        // Host EM1P clock domain: bus, memories, EUSART0, iADC, SYSRTC, LDMA, PRS, GPIO,
        // VDAC, HFXO, CMU. Runs in EM0 and EM1, and in EM1P while a host-clock requester is set.
        public bool HostClocksEnabled { get; private set; }

        // HostAll clock domain: the entire HCLK tree, including the general-purpose timers,
        // USART and SysTick. Runs in EM0 and EM1 only, never in EM1P (except for a debugger
        // wake-up, which is not modeled) and never in EM2.
        public bool HostAllClocksEnabled { get; private set; }

        public bool Em1pRequested => em1pRequesters.Count > 0;

        protected event Action EnterSleep;

        protected event Action ExitSleep;

        protected event Action EnterDeepSleep;

        protected event Action ExitDeepSleep;

        protected event Action EnterEm1p;

        protected event Action ExitEm1p;

        protected event Action EnterHostClockGate;

        protected event Action ExitHostClockGate;

        protected event Action EnterHostAllClockGate;

        protected event Action ExitHostAllClockGate;

        private Action<bool> WFIStateChanged(CortexM cpu)
        {
            return (wfi) =>
            {
                CpuWfiState[cpu] = wfi;
                if(radioSequencerCpus.Contains(cpu))
                {
                    return;
                }
                UpdateEnergyMode();
            };
        }

        private SiLabs_EnergyMode ComputeTargetEnergyMode()
        {
            // A halted companion CPU is quiescent and does not prevent the
            // remaining CPUs from entering a system energy mode.
            var hostCpus = CpuWfiState.Where(state => !radioSequencerCpus.Contains(state.Key)).ToList();
            if(!hostCpus.All(state => state.Value || state.Key.IsHalted))
            {
                return SiLabs_EnergyMode.EM0;
            }

            var sleepingCpus = hostCpus.Where(state => state.Value).Select(state => state.Key).ToList();
            if(sleepingCpus.Count == 0)
            {
                return SiLabs_EnergyMode.EM0;
            }
            if(!sleepingCpus.All(sleepingCpu => sleepingCpu.NVIC.DeepSleepEnabled))
            {
                return SiLabs_EnergyMode.EM1;
            }
            if(Em1pRequested)
            {
                return SiLabs_EnergyMode.EM1P;
            }
            // PD1 stays powered and the clocks keep running until every Q-channel peripheral
            // has accepted the quiescence request.
            return qChannelBusyPeripherals.Count > 0 ? SiLabs_EnergyMode.EM1 : SiLabs_EnergyMode.EM2;
        }

        private void UpdateEnergyMode()
        {
            // Transition hooks may change EM1P requests; those are folded into the outer loop.
            if(isUpdatingEnergyMode)
            {
                energyModeUpdatePending = true;
                return;
            }

            isUpdatingEnergyMode = true;
            try
            {
                do
                {
                    energyModeUpdatePending = false;
                    var target = ComputeTargetEnergyMode();
                    if(target != CurrentEnergyMode)
                    {
                        TransitionTo(target);
                    }
                    else if(hostClockUpdatePending)
                    {
                        UpdateHostClocks();
                    }
                }
                while(energyModeUpdatePending || hostClockUpdatePending);
            }
            finally
            {
                isUpdatingEnergyMode = false;
            }
        }

        private void TransitionTo(SiLabs_EnergyMode target)
        {
            var source = CurrentEnergyMode;
            this.Log(LogLevel.Noisy, "Energy mode transition {0} -> {1}", source, target);

            switch(source)
            {
            case SiLabs_EnergyMode.EM1:
                ExitSleep?.Invoke();
                break;
            case SiLabs_EnergyMode.EM1P:
                ExitEm1p?.Invoke();
                break;
            case SiLabs_EnergyMode.EM2:
                ExitDeepSleep?.Invoke();
                break;
            }

            // Leaving EM2 restores power to PD1, so its non-retained content is lost
            // regardless of whether the wake-up is to EM0 or to EM1P.
            if(source == SiLabs_EnergyMode.EM2)
            {
                // The reset of the core must be done after the exit hooks to keep the WFI
                // state and the NVIC state for the wakeup sources. In EM1P the host core
                // stays in deep sleep, so its reset is deferred to the wake-up to EM0.
                if(target == SiLabs_EnergyMode.EM0 && !Em2CoreRetentionEnabled)
                {
                    ResetCortexMCores();
                }
                // This must be done after the reset of the cores because this modifies the NVIC state
                PowerDownAllNoRetainPeripherals();
            }
            else if(source == SiLabs_EnergyMode.EM1P && target == SiLabs_EnergyMode.EM0
                    && pd1PoweredDownSinceEm0 && !Em2CoreRetentionEnabled)
            {
                ResetCortexMCores();
            }

            if(target == SiLabs_EnergyMode.EM2)
            {
                pd1PoweredDownSinceEm0 = true;
            }
            else if(target == SiLabs_EnergyMode.EM0)
            {
                pd1PoweredDownSinceEm0 = false;
            }

            CurrentEnergyMode = target;

            switch(target)
            {
            case SiLabs_EnergyMode.EM1:
                EnterSleep?.Invoke();
                break;
            case SiLabs_EnergyMode.EM1P:
                EnterEm1p?.Invoke();
                break;
            case SiLabs_EnergyMode.EM2:
                EnterDeepSleep?.Invoke();
                break;
            }

            UpdateHostClocks();
        }

        // EM0 and EM1 run the host clock domain. EM2 powers PD1 down, so the domain is
        // gated. EM1P keeps PD1 powered but gates host clocks unless a requester (the RAC
        // MCUEM1PMODE software request, or an EM2-capable EUSART/EUART with a pending DMA
        // request) asks for them.
        private bool ComputeHostClocksEnabled()
        {
            switch(CurrentEnergyMode)
            {
            case SiLabs_EnergyMode.EM0:
            case SiLabs_EnergyMode.EM1:
                return true;
            case SiLabs_EnergyMode.EM1P:
                return em1pHostClockRequesters.Count > 0;
            case SiLabs_EnergyMode.EM2:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(CurrentEnergyMode), CurrentEnergyMode, "Unhandled energy mode");
            }
        }

        private bool ComputeHostAllClocksEnabled()
        {
            return CurrentEnergyMode == SiLabs_EnergyMode.EM0 || CurrentEnergyMode == SiLabs_EnergyMode.EM1;
        }

        private void UpdateHostClocks()
        {
            if(isUpdatingHostClocks)
            {
                hostClockUpdatePending = true;
                return;
            }

            isUpdatingHostClocks = true;
            try
            {
                do
                {
                    hostClockUpdatePending = false;

                    var enabled = ComputeHostClocksEnabled();
                    if(enabled != HostClocksEnabled)
                    {
                        HostClocksEnabled = enabled;
                        this.Log(LogLevel.Noisy, "Host clocks {0}", enabled ? "enabled" : "gated");
                        (enabled ? ExitHostClockGate : EnterHostClockGate)?.Invoke();
                    }

                    var allEnabled = ComputeHostAllClocksEnabled();
                    if(allEnabled != HostAllClocksEnabled)
                    {
                        HostAllClocksEnabled = allEnabled;
                        this.Log(LogLevel.Noisy, "HostAll clocks {0}", allEnabled ? "enabled" : "gated");
                        (allEnabled ? ExitHostAllClockGate : EnterHostAllClockGate)?.Invoke();
                    }
                }
                while(hostClockUpdatePending);
            }
            finally
            {
                isUpdatingHostClocks = false;
            }
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
            // The radio sequencer is reset and restarted by the radio model itself.
            var cortexMCpus = machine.GetPeripheralsOfType<CortexM>().Where(cpu => !radioSequencerCpus.Contains(cpu)).ToList();

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
        // Set once PD1 has been powered down (EM2) since the last EM0, so that the
        // non-retained core state is dropped when the host finally wakes up.
        private bool pd1PoweredDownSinceEm0;
        private bool isUpdatingEnergyMode;
        private bool energyModeUpdatePending;
        private bool isUpdatingHostClocks;
        private bool hostClockUpdatePending;
        private List<IPeripheral> noRetain;
        private IDictionary<CortexM, bool> cpuWfiState;
        private readonly HashSet<object> em1pRequesters;
        private readonly HashSet<object> em1pHostClockRequesters;
        private readonly HashSet<object> qChannelBusyPeripherals;
        private readonly HashSet<CortexM> radioSequencerCpus;
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
