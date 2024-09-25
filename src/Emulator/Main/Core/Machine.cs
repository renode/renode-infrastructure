//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.EventRecording;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Utilities.GDB;
using Microsoft.CSharp.RuntimeBinder;

namespace Antmicro.Renode.Core
{
    public class Machine : IMachine, IDisposable
    {
        public Machine(bool createLocalTimeSource = false)
        {
            InitAtomicMemoryState();

            collectionSync = new object();
            pausingSync = new object();
            disposedSync = new object();
            clockSource = new BaseClockSource();
            localNames = new Dictionary<IPeripheral, string>();
            PeripheralsGroups = new PeripheralsGroupsManager(this);
            ownLifes = new HashSet<IHasOwnLife>();
            pausedState = new PausedState(this);
            SystemBus = new SystemBus(this);
            registeredPeripherals = new MultiTree<IPeripheral, IRegistrationPoint>(SystemBus);
            peripheralsBusControllers = new Dictionary<IBusPeripheral, BusControllerWrapper>();
            userStateHook = delegate
            {
            };
            userState = string.Empty;
            SetLocalName(SystemBus, SystemBusName);
            gdbStubs = new Dictionary<int, GdbStub>();

            invalidatedAddressesByCpu = new Dictionary<ICPU, List<long>>();
            invalidatedAddressesByArchitecture = new Dictionary<string, List<long>>();
            invalidatedAddressesLock = new object();
            firstUnbroadcastedDirtyAddressIndex = new Dictionary<ICPU, int>();

            if(createLocalTimeSource)
            {
                LocalTimeSource = new SlaveTimeSource();
            }

            machineCreatedAt = new DateTime(CustomDateTime.Now.Ticks, DateTimeKind.Local);
        }

        [PreSerialization]
        private void SerializeAtomicMemoryState()
        {
            atomicMemoryState = new byte[AtomicMemoryStateSize];
            Marshal.Copy(atomicMemoryStatePointer, atomicMemoryState, 0, atomicMemoryState.Length);
            // the first byte of an atomic memory state contains value 0 or 1
            // indicating if the mutex has already been initialized;
            // the mutex must be restored after each deserialization, so here we force this value to 0
            atomicMemoryState[0] = 0;
        }

        [PostDeserialization]
        public void InitAtomicMemoryState()
        {
            atomicMemoryStatePointer = Marshal.AllocHGlobal(AtomicMemoryStateSize);

            // the beginning of an atomic memory state contains two 8-bit flags:
            // byte 0: information if the mutex has already been initialized
            // byte 1: information if the reservations array has already been initialized
            //
            // the first byte must be set to 0 at start and after each deserialization
            // as this is crucial for proper memory initialization;
            //
            // the second one must be set to 0 at start, but should not be overwritten after deserialization;
            // this is handled when saving `atomicMemoryState`
            if(atomicMemoryState != null)
            {
                Marshal.Copy(atomicMemoryState, 0, atomicMemoryStatePointer, atomicMemoryState.Length);
                atomicMemoryState = null;
            }
            else
            {
                // this write spans two 8-byte flags
                Marshal.WriteInt16(atomicMemoryStatePointer, 0);
            }
        }

        public IntPtr AtomicMemoryStatePointer => atomicMemoryStatePointer;

        [Transient]
        private IntPtr atomicMemoryStatePointer;
        private byte[] atomicMemoryState;

        // TODO: this probably should be dynamically get from Tlib, but how to nicely do that in `Machine` class?
        private const int AtomicMemoryStateSize = 25600;

        public IEnumerable<IPeripheral> GetParentPeripherals(IPeripheral peripheral)
        {
            var node = registeredPeripherals.TryGetNode(peripheral);
            return node == null ? new IPeripheral[0] : node.Parents.Select(x => x.Value).Distinct();
        }

        public IEnumerable<IPeripheral> GetChildrenPeripherals(IPeripheral peripheral)
        {
            var node = registeredPeripherals.TryGetNode(peripheral);
            return node == null ? new IPeripheral[0] : node.Children.Select(x => x.Value).Distinct();
        }

        public IEnumerable<IRegistrationPoint> GetPeripheralRegistrationPoints(IPeripheral parentPeripheral, IPeripheral childPeripheral)
        {
            var parentNode = registeredPeripherals.TryGetNode(parentPeripheral);
            return parentNode == null ? new IRegistrationPoint[0] : parentNode.GetConnectionWays(childPeripheral);
        }

        public void RegisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild, IRegistrationPoint registrationPoint)
        {
            Register(peripheralChild, registrationPoint, peripheralParent);
        }

        public void UnregisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild)
        {
            lock(collectionSync)
            {
                CollectGarbageStamp();
                IPeripheralsGroup group;
                if(PeripheralsGroups.TryGetActiveGroupContaining(peripheralChild, out group))
                {
                    throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                }

                var parentNode = registeredPeripherals.GetNode(peripheralParent);
                parentNode.RemoveChild(peripheralChild);
                EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(peripheralChild);
                CollectGarbage();
            }
        }

        public void UnregisterAsAChildOf(IPeripheral peripheralParent, IRegistrationPoint registrationPoint)
        {
            lock(collectionSync)
            {
                CollectGarbageStamp();
                try
                {
                    var parentNode = registeredPeripherals.GetNode(peripheralParent);
                    IPeripheral removedPeripheral = null;
                    parentNode.RemoveChild(registrationPoint, p =>
                    {
                        IPeripheralsGroup group;
                        if(PeripheralsGroups.TryGetActiveGroupContaining(p, out group))
                        {
                            throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                        }
                        removedPeripheral = p;
                        return true;
                    });
                    CollectGarbage();
                    if(removedPeripheral != null && registeredPeripherals.TryGetNode(removedPeripheral) == null)
                    {
                        EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(removedPeripheral);
                    }
                }
                catch(RegistrationException)
                {
                    CollectGarbage();
                    throw;
                }
            }
        }

        public void UnregisterFromParent(IPeripheral peripheral)
        {
            InnerUnregisterFromParent(peripheral);
            var operation = PeripheralsChangedEventArgs.PeripheralChangeType.CompleteRemoval;
            PeripheralsChanged?.Invoke(this, PeripheralsChangedEventArgs.Create(peripheral, operation));
        }

        public IEnumerable<T> GetPeripheralsOfType<T>()
        {
            return GetPeripheralsOfType(typeof(T)).Cast<T>();
        }

        public IEnumerable<IPeripheral> GetPeripheralsOfType(Type t)
        {
            lock(collectionSync)
            {
                return registeredPeripherals.Values.Where(t.IsInstanceOfType).ToList();
            }
        }

        public IEnumerable<PeripheralTreeEntry> GetRegisteredPeripherals()
        {
            var result = new List<PeripheralTreeEntry>();
            lock(collectionSync)
            {
                registeredPeripherals.TraverseWithConnectionWaysParentFirst((currentNode, regPoint, parent, level) =>
                {
                    string localName;
                    TryGetLocalName(currentNode.Value, out localName);
                    result.Add(new PeripheralTreeEntry(currentNode.Value, parent, currentNode.Value.GetType(), regPoint, localName, level));
                }, 0);
            }
            return result;
        }

        public bool TryGetByName<T>(string name, out T peripheral, out string longestMatch) where T : class, IPeripheral
        {
            if(name == null)
            {
                longestMatch = string.Empty;
                peripheral = null;
                return false;
            }
            var splitPath = name.Split(new [] { '.' }, 2);
            if(splitPath.Length == 1 && name == SystemBusName)
            {
                longestMatch = name;
                peripheral = (T)(IPeripheral)SystemBus;
                return true;
            }

            if(splitPath[0] != SystemBusName)
            {
                longestMatch = string.Empty;
                peripheral = null;
                return false;
            }

            MultiTreeNode<IPeripheral, IRegistrationPoint> result;
            if(TryFindSubnodeByName(registeredPeripherals.GetNode(SystemBus), splitPath[1], out result, SystemBusName, out longestMatch))
            {
                peripheral = (T)result.Value;
                return true;
            }
            peripheral = null;
            return false;
        }

        public bool TryGetByName<T>(string name, out T peripheral) where T : class, IPeripheral
        {
            string fake;
            return TryGetByName(name, out peripheral, out fake);
        }

        public string GetLocalName(IPeripheral peripheral)
        {
            string result;
            lock(collectionSync)
            {
                if(!TryGetLocalName(peripheral, out result))
                {
                    throw new KeyNotFoundException();
                }
                return result;
            }
        }

        public bool TryGetLocalName(IPeripheral peripheral, out string name)
        {
            lock(collectionSync)
            {
                return localNames.TryGetValue(peripheral, out name);
            }
        }

        public void SetLocalName(IPeripheral peripheral, string name)
        {
            if(string.IsNullOrEmpty(name))
            {
                throw new RecoverableException("The name of the peripheral cannot be null nor empty.");
            }
            lock(collectionSync)
            {
                if(!registeredPeripherals.ContainsValue(peripheral))
                {
                    throw new RecoverableException("Cannot name peripheral which is not registered.");
                }
                if(localNames.ContainsValue(name))
                {
                    throw new RecoverableException(string.Format("Given name '{0}' is already used.", name));
                }
                localNames[peripheral] = name;
            }
            var operation = PeripheralsChangedEventArgs.PeripheralChangeType.NameChanged;
            PeripheralsChanged?.Invoke(this, PeripheralsChangedEventArgs.Create(peripheral, operation));
        }

        public IEnumerable<string> GetAllNames()
        {
            var nameSegments = new AutoResizingList<string>();
            var names = new List<string>();
            lock(collectionSync)
            {
                registeredPeripherals.TraverseParentFirst((x, y) =>
                {
                    if(!localNames.ContainsKey(x))
                    {
                        // unnamed node
                        return;
                    }
                    var localName = localNames[x];
                    nameSegments[y] = localName;
                    var globalName = new StringBuilder();
                    for(var i = 0; i < y; i++)
                    {
                        globalName.Append(nameSegments[i]);
                        globalName.Append(PathSeparator);
                    }
                    globalName.Append(localName);
                    names.Add(globalName.ToString());
                }, 0);
            }
            return new ReadOnlyCollection<string>(names);
        }

        public bool TryGetAnyName(IPeripheral peripheral, out string name)
        {
            var names = GetNames(peripheral);
            if(names.Count > 0)
            {
                name = names[0];
                return true;
            }
            name = null;
            return false;
        }

        public string GetAnyNameOrTypeName(IPeripheral peripheral)
        {
            string name;
            if(!TryGetAnyName(peripheral, out name))
            {
                var managedThread = peripheral as IManagedThread;
                return managedThread != null ? managedThread.ToString() : peripheral.GetType().Name;
            }
            return name;
        }

        public IBusController RegisterBusController(IBusPeripheral peripheral, IBusController controller)
        {
            using(ObtainPausedState(true))
            {
                if(!peripheralsBusControllers.TryGetValue(peripheral, out var wrapper))
                {
                    wrapper = new BusControllerWrapper(controller);
                    peripheralsBusControllers.Add(peripheral, wrapper);
                }
                else
                {
                    if(wrapper.ParentController != SystemBus && wrapper.ParentController != controller)
                    {
                        throw new RecoverableException($"Trying to change the BusController from {wrapper.ParentController} to {controller} for the {peripheral} peripheral.");
                    }
                    wrapper.ChangeWrapped(controller);
                }
                return wrapper;
            }
        }

        public bool TryGetBusController(IBusPeripheral peripheral, out IBusController controller)
        {
            var exists = peripheralsBusControllers.TryGetValue(peripheral, out var wrapper);
            controller = wrapper;
            return exists;
        }

        public IBusController GetSystemBus(IBusPeripheral peripheral)
        {
            if(!TryGetBusController(peripheral, out var controller))
            {
                controller = RegisterBusController(peripheral, SystemBus);
            }
            return controller;
        }

        public bool IsRegistered(IPeripheral peripheral)
        {
            lock(collectionSync)
            {
                return registeredPeripherals.ContainsValue(peripheral);
            }
        }

        /// <summary>
        /// Pauses the machine and returns an <see cref="IDisposable">, disposing which will resume the machine.
        /// Can be nested, in this case the machine will only be resumed once the last paused state is disposed.
        /// </summary>
        /// <param name="internalPause">Specifies whether this pause is due to internal reasons and should not be visible to
        /// external software, such as GDB. For example, the pause to register a new peripheral is internal; the pause triggered
        /// by a CPU breakpoint is not.</param>
        public IDisposable ObtainPausedState(bool internalPause = false)
        {
            InternalPause = internalPause;
            pausedState.Enter();
            return DisposableWrapper.New(() =>
            {
                pausedState.Dispose();
                // Does not handle nesting, but only the outermost pause could possibly invoke halt callbacks
                InternalPause = false;
            });
        }

        /// <param name="startFilter">A function to test each own life whether it should be started along with the machine.
        /// Useful when unpausing the machine but we don't want to unpause what's already been paused before.</param>
        private void Start(Func<IHasOwnLife, bool> startFilter)
        {
            lock(pausingSync)
            {
                switch(state)
                {
                case State.Started:
                    return;
                case State.Paused:
                    Resume();
                    return;
                }
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 1 : 0))
                {
                    if(startFilter(ownLife))
                    {
                        this.NoisyLog("Starting {0}.", GetNameForOwnLife(ownLife));
                        ownLife.Start();
                    }
                }
                (LocalTimeSource as SlaveTimeSource)?.Resume();
                this.Log(LogLevel.Info, "Machine started.");
                state = State.Started;
                var machineStarted = StateChanged;
                if(machineStarted != null)
                {
                    machineStarted(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Started));
                }
            }
        }

        public void Start()
        {
            Start(_ => true);
        }

        public void Pause()
        {
            lock(pausingSync)
            {
                switch(state)
                {
                case State.Paused:
                    return;
                case State.NotStarted:
                    goto case State.Paused;
                }
                (LocalTimeSource as SlaveTimeSource)?.Pause();
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 0 : 1))
                {
                    var ownLifeName = GetNameForOwnLife(ownLife);
                    this.NoisyLog("Pausing {0}.", ownLifeName);
                    ownLife.Pause();
                    this.NoisyLog("{0} paused.", ownLifeName);
                }
                state = State.Paused;
                var machinePaused = StateChanged;
                if(machinePaused != null)
                {
                    machinePaused(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Paused));
                }
                this.Log(LogLevel.Info, "Machine paused.");
            }
        }

        public void PauseAndRequestEmulationPause(bool precise = false)
        {
            lock(pausingSync)
            {
                // Nothing to do if the emulation is already paused
                if(!EmulationManager.Instance.CurrentEmulation.IsStarted)
                {
                    return;
                }

                // Precise mode is only available when this method is run on the CPU thread
                // We silence the logging that would happen otherwise (for example if we came
                // here from a GPIO state change triggered by a timer) - in that case we log
                // our own warning.
                // We only attempt to prepare the CPU for a precise pause if the machine is currently running
                if(precise && !IsPaused)
                {
                    if(!TryRestartTranslationBlockOnCurrentCpu(quiet: true))
                    {
                        this.Log(LogLevel.Warning, "Failed to restart translation block for precise pause, " +
                            "the pause will happen at the end of the current block");
                    }
                }

                // We will pause this machine right now, but the whole emulation at the next sync point
                Action pauseEmulation = null;
                pauseEmulation = () =>
                {
                    EmulationManager.Instance.CurrentEmulation.PauseAll();
                    LocalTimeSource.SinksReportedHook -= pauseEmulation;
                };
                LocalTimeSource.SinksReportedHook += pauseEmulation;

                // Pause is harmless to call even if the machine is already paused
                Pause();
            }
        }

        public void Reset()
        {
            lock(pausingSync)
            {
                using(ObtainPausedState(true))
                {
                    foreach(var resetable in registeredPeripherals.Distinct().ToList())
                    {
                        if(resetable == this)
                        {
                            continue;
                        }
                        resetable.Reset();
                    }
                    var machineReset = MachineReset;
                    if(machineReset != null)
                    {
                        machineReset(this);
                    }
                }
            }
        }

        public bool InternalPause { get; private set; }

        public void RequestResetInSafeState(Action postReset = null, ICollection<IPeripheral> unresetable = null)
        {
            Action softwareRequestedReset = null;
            softwareRequestedReset = () =>
            {
                LocalTimeSource.SinksReportedHook -= softwareRequestedReset;
                using(ObtainPausedState(true))
                {
                    foreach(var peripheral in registeredPeripherals.Distinct().Where(p => p != this && !(unresetable?.Contains(p) ?? false)))
                    {
                        peripheral.Reset();
                    }
                }
                postReset?.Invoke();
            };
            LocalTimeSource.SinksReportedHook += softwareRequestedReset;
        }

        public void RequestReset()
        {
            LocalTimeSource.ExecuteInNearestSyncedState(_ => Reset());
        }

        public void Dispose()
        {
            lock(disposedSync)
            {
                if(alreadyDisposed)
                {
                    return;
                }
                alreadyDisposed = true;
            }
            Pause();
            if(recorder != null)
            {
                recorder.Dispose();
            }
            if(player != null)
            {
                player.Dispose();
                LocalTimeSource.SyncHook -= player.Play;
            }
            foreach(var stub in gdbStubs)
            {
                stub.Value.Dispose();
            }
            gdbStubs.Clear();

            // ordering below is due to the fact that the CPU can use other peripherals, e.g. Memory so it should be disposed first
            // Mapped memory can be used as storage by other disposable peripherals which may want to read it while being disposed
            foreach(var peripheral in GetPeripheralsOfType<IDisposable>().OrderBy(x => x is ICPU ? 0 : x is IMapped ? 2 : 1))
            {
                this.DebugLog("Disposing {0}.", GetAnyNameOrTypeName((IPeripheral)peripheral));
                peripheral.Dispose();
            }
            (LocalTimeSource as SlaveTimeSource)?.Dispose();
            this.Log(LogLevel.Info, "Disposed.");
            var disposed = StateChanged;
            if(disposed != null)
            {
                disposed(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Disposed));
            }
            Profiler?.Dispose();
            Profiler = null;

            Marshal.FreeHGlobal(AtomicMemoryStatePointer);

            EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(this);
        }

        public IManagedThread ObtainManagedThread(Action action, uint frequency, string name = "managed thread", IEmulationElement owner = null, Func<bool> stopCondition = null)
        {
            return new ManagedThreadWrappingClockEntry(this, action, frequency, name, owner, stopCondition);
        }

        public IManagedThread ObtainManagedThread(Action action, TimeInterval period, string name = "managed thread", IEmulationElement owner = null, Func<bool> stopCondition = null)
        {
            return new ManagedThreadWrappingClockEntry(this, action, period, name, owner, stopCondition);
        }

        private class ManagedThreadWrappingClockEntry : IManagedThread
        {
            public ManagedThreadWrappingClockEntry(IMachine machine, Action action, uint frequency, string name, IEmulationElement owner, Func<bool> stopCondition = null)
                : this(machine, action, stopCondition)
            {
                machine.ClockSource.AddClockEntry(new ClockEntry(1, frequency, this.action, owner ?? machine, name, enabled: false));
            }

            public ManagedThreadWrappingClockEntry(IMachine machine, Action action, TimeInterval period, string name, IEmulationElement owner, Func<bool> stopCondition = null)
                : this(machine, action, stopCondition)
            {
                machine.ClockSource.AddClockEntry(new ClockEntry(period.Ticks, (long)TimeInterval.TicksPerSecond, this.action, owner ?? machine, name, enabled: false));
            }

            public void Dispose()
            {
                machine.ClockSource.TryRemoveClockEntry(action);
            }

            public void Start()
            {
                machine.ClockSource.ExchangeClockEntryWith(
                    action, x => x.With(enabled: true));
            }

            public void StartDelayed(TimeInterval delay)
            {
                Action<TimeInterval> startThread = ts =>
                {
                    Start();

                    // Let's have the first action run precisely at the specified time.
                    action();
                };
                var name = machine.ClockSource.GetClockEntry(action).LocalName;
                machine.ScheduleAction(delay, startThread, name);
            }

            public void Stop()
            {
                machine.ClockSource.ExchangeClockEntryWith(action, x => x.With(enabled: false));
            }

            public uint Frequency
            {
                get => (uint)machine.ClockSource.GetClockEntry(action).Frequency;
                set => machine.ClockSource.ExchangeClockEntryWith(action, entry => entry.With(frequency: value));
            }

            private ManagedThreadWrappingClockEntry(IMachine machine, Action action, Func<bool> stopCondition = null)
            {
                this.action = () =>
                {
                    if(stopCondition?.Invoke() ?? false)
                    {
                        Stop();
                    }
                    else
                    {
                        action();
                    }
                };
                this.machine = machine;
            }

            private readonly Action action;
            private readonly IMachine machine;
        }

        private BaseClockSource clockSource;
        public IClockSource ClockSource { get { return clockSource; } }

        [UiAccessible]
        public string[,] GetClockSourceInfo()
        {
            var entries = ClockSource.GetAllClockEntries();

            var table = new Table().AddRow("Owner", "Enabled", "Frequency", "Limit", "Value", "Step", "Event frequency", "Event period");
            table.AddRows(entries,
                x =>
                {
                    var owner = x.Handler.Target;
                    var ownerAsPeripheral = owner as IPeripheral;
                    if(x.Owner != null)
                    {
                        if(EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName(x.Owner, out var name, out var _))
                        {
                            return name + (String.IsNullOrWhiteSpace(x.LocalName) ? String.Empty : $": {x.LocalName}");
                        }
                    }
                    return ownerAsPeripheral != null
                                ? GetAnyNameOrTypeName(ownerAsPeripheral)
                                : owner.GetType().Name;
                },
                x => x.Enabled.ToString(),
                x => Misc.NormalizeDecimal(x.Frequency) + "Hz",
                x => x.Period.ToString(),
                x => x.Value.ToString(),
                x => x.Step.ToString(),
                x => x.Period == 0 ? "---" : Misc.NormalizeDecimal((ulong)(x.Frequency * x.Step) / (double)x.Period) + "Hz",
                x => (x.Frequency == 0 || x.Period == 0) ? "---" :  Misc.NormalizeDecimal((ulong)x.Period / (x.Frequency * (double)x.Step))  + "s"
            );
            return table.ToArray();
        }

        public void AttachGPIO(IPeripheral source, int sourceNumber, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var sourceByNumber = source as INumberedGPIOOutput;
            IGPIO igpio;
            if(sourceByNumber == null)
            {
                throw new RecoverableException("Source peripheral cannot be connected by number.");
            }
            if(!sourceByNumber.Connections.TryGetValue(sourceNumber, out igpio))
            {
                throw new RecoverableException(string.Format("Source peripheral has no GPIO number: {0}", source));
            }
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            igpio.Connect(actualDestination, destinationNumber);
        }

        public void AttachGPIO(IPeripheral source, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var connectors = source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            DoAttachGPIO(source, connectors, actualDestination, destinationNumber);
        }

        public void AttachGPIO(IPeripheral source, string connectorName, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var connectors = source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name == connectorName && typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            DoAttachGPIO(source, connectors, actualDestination, destinationNumber);
        }

        public void HandleTimeDomainEvent<T>(Action<T> handler, T handlerArgument, TimeStamp eventTime, Action postAction = null)
        {
            switch(EmulationManager.Instance.CurrentEmulation.Mode)
            {
                case Emulation.EmulationMode.SynchronizedIO:
                {
                    LocalTimeSource.ExecuteInSyncedState(ts =>
                    {
                        HandleTimeDomainEvent(handler, handlerArgument, ts.Domain == LocalTimeSource.Domain);
                        postAction?.Invoke();
                    }, eventTime);
                    break;
                }
                
                case Emulation.EmulationMode.SynchronizedTimers:
                {
                    handler(handlerArgument);
                    postAction?.Invoke();
                    break;
                }

                default:
                    throw new Exception("Should not reach here");
            }
        }

        public void HandleTimeDomainEvent<T1, T2>(Action<T1, T2> handler, T1 handlerArgument1, T2 handlerArgument2, TimeStamp eventTime, Action postAction = null)
        {
            switch(EmulationManager.Instance.CurrentEmulation.Mode)
            {
                case Emulation.EmulationMode.SynchronizedIO:
                {
                    LocalTimeSource.ExecuteInSyncedState(ts =>
                    {
                        HandleTimeDomainEvent(handler, handlerArgument1, handlerArgument2, ts.Domain == LocalTimeSource.Domain);
                        postAction?.Invoke();
                    }, eventTime);
                    break;
                }
                
                case Emulation.EmulationMode.SynchronizedTimers:
                {
                    handler(handlerArgument1, handlerArgument2);
                    postAction?.Invoke();
                    break;
                }

                default:
                    throw new Exception("Should not reach here");
            }
        }

        public void HandleTimeDomainEvent<T>(Action<T> handler, T handlerArgument, bool timeDomainInternalEvent)
        {
            switch(EmulationManager.Instance.CurrentEmulation.Mode)
            {
                case Emulation.EmulationMode.SynchronizedIO:
                {
                    ReportForeignEventInner(
                            recorder == null ? (Action<TimeInterval, bool>)null : (timestamp, eventNotFromDomain) => recorder.Record(handlerArgument, handler, timestamp, eventNotFromDomain),
                            () => handler(handlerArgument), timeDomainInternalEvent);
                    break;
                }
                
                case Emulation.EmulationMode.SynchronizedTimers:
                {
                    handler(handlerArgument);
                    break;
                }

                default:
                    throw new Exception("Should not reach here");
            }
        }

        public long[] GetNewDirtyAddressesForCore(ICPU cpu)
        {
            if(!firstUnbroadcastedDirtyAddressIndex.ContainsKey(cpu))
            {
                throw new RecoverableException($"No entries for a cpu: {cpu.GetName()}. Was the cpu registered properly?");
            }

            long[] newAddresses;
            lock(invalidatedAddressesLock)
            {
                var firstUnsentIndex = firstUnbroadcastedDirtyAddressIndex[cpu];
                var addressesCount = invalidatedAddressesByCpu[cpu].Count - firstUnsentIndex;
                newAddresses = invalidatedAddressesByCpu[cpu].GetRange(firstUnsentIndex, addressesCount).ToArray();
                firstUnbroadcastedDirtyAddressIndex[cpu] += addressesCount;
            }
            return newAddresses;
        }

        public void AppendDirtyAddresses(ICPU cpu, long[] addresses)
        {
            if(!invalidatedAddressesByCpu.ContainsKey(cpu))
            {
                throw new RecoverableException($"Invalid cpu: {cpu.GetName()}");
            }

            lock(invalidatedAddressesLock)
            {
                if(invalidatedAddressesByCpu[cpu].Count + addresses.Length > invalidatedAddressesByCpu[cpu].Capacity)
                {
                    TryReduceBroadcastedDirtyAddresses(cpu);
                }
                invalidatedAddressesByCpu[cpu].AddRange(addresses);
            }
        }

        public void HandleTimeDomainEvent<T1, T2>(Action<T1, T2> handler, T1 handlerArgument1, T2 handlerArgument2, bool timeDomainInternalEvent)
        {
            switch(EmulationManager.Instance.CurrentEmulation.Mode)
            {
                case Emulation.EmulationMode.SynchronizedIO:
                {
                    ReportForeignEventInner(
                        recorder == null ? (Action<TimeInterval, bool>)null : (timestamp, eventNotFromDomain) => recorder.Record(handlerArgument1, handlerArgument2, handler, timestamp, eventNotFromDomain),
                        () => handler(handlerArgument1, handlerArgument2), timeDomainInternalEvent);
                    break;
                }
                
                case Emulation.EmulationMode.SynchronizedTimers:
                {
                    handler(handlerArgument1, handlerArgument2);
                    break;
                }

                default:
                    throw new Exception("Should not reach here");
            }
        }

        public bool HasRecorder => recorder != null;

        private object recorderPlayerLock = new object();

        public void RecordTo(string fileName, RecordingBehaviour recordingBehaviour)
        {
            lock(recorderPlayerLock)
            {
                if(EmulationManager.Instance.CurrentEmulation.Mode != Emulation.EmulationMode.SynchronizedIO)
                {
                    throw new RecoverableException($"Recording events only allowed in the synchronized IO emulation mode (current mode is {EmulationManager.Instance.CurrentEmulation.Mode}");
                }
                
                recorder = new Recorder(File.Create(fileName), this, recordingBehaviour);
            }
        }
        
        public bool HasPlayer => player != null;

        public void PlayFrom(ReadFilePath fileName)
        {
            lock(recorderPlayerLock)
            {
                if(EmulationManager.Instance.CurrentEmulation.Mode != Emulation.EmulationMode.SynchronizedIO)
                {
                    throw new RecoverableException($"Replying events only allowed in the synchronized IO emulation mode (current mode is {EmulationManager.Instance.CurrentEmulation.Mode}");
                }
                
                player = new Player(File.OpenRead(fileName), this);
                LocalTimeSource.SyncHook += player.Play;
            }
        }

        public void AddUserStateHook(Func<string, bool> predicate, Action<string> hook)
        {
            userStateHook += currentState =>
            {
                if(predicate(currentState))
                {
                    hook(currentState);
                }
            };
        }

        public void EnableGdbLogging(int port, bool enabled)
        {
            if(!gdbStubs.ContainsKey(port))
            {
                return;
            }
            gdbStubs[port].LogsEnabled = enabled;
        }

        public void StartGdbServer(int port, bool autostartEmulation = true, string cpuCluster = "")
        {
            var cpus = SystemBus.GetCPUs().OfType<ICpuSupportingGdb>();
            if(!cpus.Any())
            {
                throw new RecoverableException("Cannot start GDB server with no CPUs. Did you forget to load the platform description first?");
            }
            try
            {
                // If all the CPUs are only of one architecture, implicitly allow to connect, without prompting about anything
                if(cpus.Select(cpu => cpu.Model).Distinct().Count() <= 1)
                {
                    if(!String.IsNullOrEmpty(cpuCluster))
                    {
                        this.Log(LogLevel.Warning, "{0} setting has no effect on non-heterogenous systems, and will be ignored", nameof(cpuCluster));
                    }
                    AddCpusToGdbStub(port, autostartEmulation, cpus);
                    this.Log(LogLevel.Info, "GDB server with all CPUs started on port :{0}", port);
                }
                else
                {
                    // It's not recommended to connect GDB to all CPUs in heterogeneous platforms
                    // but let's permit this, if the user insists, with a log
                    if(cpuCluster.ToLowerInvariant() == "all")
                    {
                        this.Log(LogLevel.Info, "Starting GDB server for CPUs of different architectures. Make sure, that your debugger supports this configuration");
                        AddCpusToGdbStub(port, autostartEmulation, cpus);
                        return;
                    }

                    // Otherwise, simple clustering, based on architecture
                    var cpusOfArch = cpus.Where(cpu => cpu.Model == cpuCluster);
                    if(!cpusOfArch.Any())
                    {
                        var response = new StringBuilder();
                        if(String.IsNullOrEmpty(cpuCluster))
                        {
                            response.AppendLine("CPUs of different architectures are present in this platform. Specify cluster of CPUs to debug, or \"all\" to connect to all CPUs.");
                            response.AppendLine("NOTE: when selecting \"all\" make sure that your debugger can handle CPUs of different architectures.");
                        }
                        else
                        {
                            response.AppendFormat("No CPUs available or no cluster named: \"{0}\" exists.\n", cpuCluster);
                        }
                        response.Append("Available clusters are: ");
                        response.Append(Misc.PrettyPrintCollection(cpus.Select(c => c.Model).Distinct().Append("all"), c => $"\"{c}\""));
                        throw new RecoverableException(response.ToString());
                    }
                    AddCpusToGdbStub(port, autostartEmulation, cpusOfArch);
                }
            }
            catch(SocketException e)
            {
                throw new RecoverableException(string.Format("Could not start GDB server: {0}", e.Message));
            }
        }

        // Name of the last parameter is kept as 'cpu' for backward compatibility.
        public void StartGdbServer(int port, bool autostartEmulation, ICluster<ICpuSupportingGdb> cpu)
        {
            var cluster = cpu;
            foreach(var cpuSupportingGdb in cluster.Clustered)
            {
                try
                {
                    AddCpusToGdbStub(port, autostartEmulation, new [] { cpuSupportingGdb });
                }
                catch(SocketException e)
                {
                    throw new RecoverableException(string.Format("Could not start GDB server for {0}: {1}", cpuSupportingGdb.GetName(), e.Message));
                }
            }
        }

        public void StopGdbServer(int? port = null)
        {
            if(!gdbStubs.Any())
            {
                throw new RecoverableException("Nothing to stop.");
            }
            if(!port.HasValue)
            {
                if(gdbStubs.Count > 1)
                {
                    throw new RecoverableException("Port number required to stop a GDB server.");
                }
                gdbStubs.Single().Value.Dispose();
                gdbStubs.Clear();
                return;
            }
            if(!gdbStubs.ContainsKey(port.Value))
            {
                throw new RecoverableException(string.Format("There is no GDB server on port :{0}.", port.Value));
            }
            gdbStubs[port.Value].Dispose();
            gdbStubs.Remove(port.Value);
        }

        public override string ToString()
        {
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(this, out var machineName))
            {
                return machineName;
            }
            return "Unregistered machine";
        }

        public void EnableProfiler(string outputPath = null)
        {
            Profiler?.Dispose();
            Profiler = new Profiler(this, outputPath ?? TemporaryFilesManager.Instance.GetTemporaryFile("renode_profiler"));
        }
        
        public void CheckRecorderPlayer()
        {
            lock(recorderPlayerLock)
            {
                if(EmulationManager.Instance.CurrentEmulation.Mode != Emulation.EmulationMode.SynchronizedIO)
                {
                    if(recorder != null)
                    {
                        throw new RecoverableException("Detected existing event recorder attached to the machine - it won't work in the non-deterministic mode");
                    }
                    if(player != null)
                    {
                        throw new RecoverableException("Detected existing event player attached to the machine - it won't work in the non-deterministic mode");
                    }
                }
            }
        }

        public void ScheduleAction(TimeInterval delay, Action<TimeInterval> action, string name = null)
        {
            if(SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
            else
            {
                this.Log(LogLevel.Debug, "Couldn't synchronize time before scheduling action; a slight inaccuracy might occur.");
            }

            var currentTime = ElapsedVirtualTime.TimeElapsed;
            var startTime = currentTime + delay;

            // We can't do this in 1 assignment because we need to refer to clockEntryHandler
            // within the body of the lambda
            Action clockEntryHandler = null;
            clockEntryHandler = () =>
            {
                this.Log(LogLevel.Noisy, "{0}: Executing action scheduled at {1} (current time: {2})", name ?? "unnamed", startTime, currentTime);
                action(currentTime);
                if(!ClockSource.TryRemoveClockEntry(clockEntryHandler))
                {
                    this.Log(LogLevel.Error, "{0}: Failed to remove clock entry after running scheduled action", name ?? "unnamed");
                }
            };

            ClockSource.AddClockEntry(new ClockEntry(delay.Ticks, (long)TimeInterval.TicksPerSecond, clockEntryHandler, this, name, workMode: WorkMode.OneShot));

            // ask CPU to return to C# to recalculate internal timers and make the scheduled action trigger as soon as possible
            (cpu as IControllableCPU)?.RequestReturn();
        }

        // This method will only be effective when called from the CPU thread with the pause guard held.
        // In use cases where one of these conditions may sometimes not be met (for example a GPIO
        // state change callback, which can be triggered by a MMIO write or a timer limit event) and the
        // restart is not critical, the quiet parameter should be used to silence logging.
        public bool TryRestartTranslationBlockOnCurrentCpu(bool quiet = false)
        {
            if(!SystemBus.TryGetCurrentCPU(out var icpu))
            {
                if(!quiet)
                {
                    this.Log(LogLevel.Error, "Couldn't find the CPU requesting translation block restart.");
                }
                return false;
            }

            try
            {
                var cpu = (dynamic)icpu;
                if(!cpu.RequestTranslationBlockRestart(quiet))
                {
                    if(!quiet)
                    {
                        Logger.LogAs(icpu, LogLevel.Error, "Failed to restart translation block.");
                    }
                    return false;
                }
            }
            catch(RuntimeBinderException)
            {
                Logger.LogAs(icpu, LogLevel.Warning, "Translation block restarting is not supported by '{0}'", icpu.GetType().FullName);
                return false;
            }

            return true;
        }

        public Profiler Profiler { get; private set; }

        public IPeripheral this[string name]
        {
            get
            {
                return GetByName(name);
            }
        }

        public string UserState
        {
            get
            {
                return userState;
            }
            set
            {
                userState = value;
                userStateHook(userState);
            }
        }

        public IBusController SystemBus { get; private set; }

        public IPeripheralsGroupsManager PeripheralsGroups { get; private set; }

        public Platform Platform { get; set; }

        public bool IsPaused
        {
            get
            {
                // locking on pausingSync can cause a deadlock (when mach.Start() and AllMachineStarted are called together)
                var stateCopy = state;
                return stateCopy == State.Paused || stateCopy == State.NotStarted;
            }
        }

        public TimeStamp ElapsedVirtualTime
        {
            get
            {
                return new TimeStamp(LocalTimeSource.ElapsedVirtualTime, LocalTimeSource.Domain);
            }
        }

        public TimeSourceBase LocalTimeSource
        {
            get
            {
                return localTimeSource;
            }

            set
            {
                if(localTimeSource != null)
                {
                    throw new RecoverableException("Tried to set LocalTimeSource again.");
                }
                if(value == null)
                {
                    throw new RecoverableException("Tried to set LocalTimeSource to null.");
                }
                localTimeSource = value;
                localTimeSource.TimePassed += HandleTimeProgress;
                foreach(var timeSink in ownLifes.OfType<ITimeSink>())
                {
                    localTimeSource.RegisterSink(timeSink);
                }
            }
        }

        public DateTime RealTimeClockDateTime
        {
            get
            {
                if(SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                return RealTimeClockStart + ElapsedVirtualTime.TimeElapsed.ToTimeSpan();
            }
        }

        public RealTimeClockMode RealTimeClockMode
        {
            get => realTimeClockMode;
            set
            {
                realTimeClockMode = value;
                var realTimeClockModeChanged = RealTimeClockModeChanged;
                if(realTimeClockModeChanged != null)
                {
                    realTimeClockModeChanged(this);
                }
            }
        }

        public DateTime RealTimeClockStart
        {
            get
            {
                switch(RealTimeClockMode)
                {
                case RealTimeClockMode.Epoch:
                    return Misc.UnixEpoch;
                case RealTimeClockMode.HostTimeLocal:
                    return machineCreatedAt;
                case RealTimeClockMode.HostTimeUTC:
                    return TimeZoneInfo.ConvertTimeToUtc(machineCreatedAt, sourceTimeZone: TimeZoneInfo.Local);
                default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        [field: Transient]
        public event Action<IMachine> MachineReset;
        [field: Transient]
        public event Action<IMachine, PeripheralsChangedEventArgs> PeripheralsChanged;
        [field: Transient]
        public event Action<IMachine> RealTimeClockModeChanged;
        [field: Transient]
        public event Action<IMachine, MachineStateChangedEventArgs> StateChanged;

        public const char PathSeparator = '.';
        public const string SystemBusName = "sysbus";
        public const string UnnamedPeripheral = "[no-name]";
        public const string MachineKeyword = "machine";

        private void CheckIsCpuAlreadyAttached(ICpuSupportingGdb cpu)
        {
            var owningStub = gdbStubs.Values.FirstOrDefault(x => x.IsCPUAttached(cpu));
            if(owningStub != null)
            {
                throw new RecoverableException($"CPU: {cpu.GetName()} is already attached to an existing GDB server, running on port :{owningStub.Port}");
            }
        }

        private void AddCpusToGdbStub(int port, bool autostartEmulation, IEnumerable<ICpuSupportingGdb> cpus)
        {
            foreach(var cpu in cpus)
            {
                CheckIsCpuAlreadyAttached(cpu);
            }
            if(gdbStubs.ContainsKey(port))
            {
                foreach(var cpu in cpus)
                {
                    gdbStubs[port].AttachCPU(cpu);
                    this.Log(LogLevel.Info, "CPU: {0} was added to GDB server running on port :{1}", cpu.GetName(), port);
                }
            }
            else
            {
                gdbStubs.Add(port, new GdbStub(this, cpus, port, autostartEmulation));
                this.Log(LogLevel.Info, "CPUs: {0} were added to a new GDB server created on port :{1}", Misc.PrettyPrintCollection(cpus, c => $"\"{c.GetName()}\""), port);
            }
        }

        private void TryReduceBroadcastedDirtyAddresses(ICPU cpu)
        {
            var firstUnread = firstUnbroadcastedDirtyAddressIndex.Values.Min();
            if(firstUnread == 0)
            {
                return;
            }

            invalidatedAddressesByCpu[cpu].RemoveRange(0, (int)firstUnread);
            foreach(var key in firstUnbroadcastedDirtyAddressIndex.Keys.ToArray())
            {
                firstUnbroadcastedDirtyAddressIndex[key] -= firstUnread;
            }
        }

        private void InnerUnregisterFromParent(IPeripheral peripheral)
        {
            using(ObtainPausedState(true))
            {
                lock(collectionSync)
                {
                    var parents = GetParents(peripheral);
                    if(parents.Count > 1)
                    {
                        throw new RegistrationException(string.Format("Given peripheral is connected to more than one different parent, at least '{0}' and '{1}'.",
                            parents.Select(x => GetAnyNameOrTypeName(x)).Take(2).ToArray()));
                    }

                    IPeripheralsGroup group;
                    if(PeripheralsGroups.TryGetActiveGroupContaining(peripheral, out group))
                    {
                        throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                    }

                    var parent = parents.FirstOrDefault();
                    if(parent == null)
                    {
                        throw new RecoverableException(string.Format("Cannot unregister peripheral {0} since it does not have any parent.", peripheral));
                    }
                    ((dynamic)parent).Unregister((dynamic)peripheral);
                    EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(peripheral);
                }
            }
        }

        private void InitializeInvalidatedAddressesList(ICPU cpu)
        {
            lock(invalidatedAddressesLock)
            {
                if(!invalidatedAddressesByArchitecture.TryGetValue(cpu.Architecture, out var newInvalidatedAddressesList))
                {
                    newInvalidatedAddressesList = new List<long>() { Capacity = InitialDirtyListLength };
                    invalidatedAddressesByArchitecture.Add(cpu.Architecture, newInvalidatedAddressesList);
                }
                invalidatedAddressesByCpu[cpu] = newInvalidatedAddressesList;
            }
        }

        private void Register(IPeripheral peripheral, IRegistrationPoint registrationPoint, IPeripheral parent)
        {
            using(ObtainPausedState(true))
            {
                Action executeAfterLock = null;
                lock(collectionSync)
                {
                    var parentNode = registeredPeripherals.GetNode(parent);
                    parentNode.AddChild(peripheral, registrationPoint);
                    var ownLife = peripheral as IHasOwnLife;
                    if(peripheral is ICPU cpu)
                    {
                        if(cpu.Architecture == null)
                        {
                            throw new RecoverableException($"{cpu.Model ?? "Unknown model"}: CPU architecture not provided");
                        }
                        InitializeInvalidatedAddressesList(cpu);
                        firstUnbroadcastedDirtyAddressIndex[cpu] = 0;
                    }
                    if(ownLife != null)
                    {
                        ownLifes.Add(ownLife);
                        if(state == State.Paused)
                        {
                            executeAfterLock = delegate
                            {
                                ownLife.Start();
                                ownLife.Pause();
                            };
                        }
                    }
                }
                if(executeAfterLock != null)
                {
                    executeAfterLock();
                }

                if(peripheral is ITimeSink timeSink)
                {
                    LocalTimeSource?.RegisterSink(timeSink);
                }
            }

            PeripheralsChanged?.Invoke(this, PeripheralsAddedEventArgs.Create(peripheral, registrationPoint));
            EmulationManager.Instance.CurrentEmulation.BackendManager.TryCreateBackend(peripheral);
        }

        private bool TryFindSubnodeByName(MultiTreeNode<IPeripheral, IRegistrationPoint> from, string path, out MultiTreeNode<IPeripheral, IRegistrationPoint> subnode,
            string currentMatching, out string longestMatching)
        {
            lock(collectionSync)
            {
                var subpath = path.Split(new [] { PathSeparator }, 2);
                subnode = null;
                longestMatching = currentMatching;
                foreach(var currentChild in from.Children)
                {
                    string name;
                    if(!TryGetLocalName(currentChild.Value, out name))
                    {
                        continue;
                    }

                    if(name == subpath[0])
                    {
                        subnode = currentChild;
                        if(subpath.Length == 1)
                        {
                            return true;
                        }
                        return TryFindSubnodeByName(currentChild, subpath[1], out subnode, Subname(currentMatching, subpath[0]), out longestMatching);
                    }
                }
                return false;
            }
        }

        private IPeripheral GetByName(string path)
        {
            IPeripheral result;
            string longestMatching;
            if(!TryGetByName(path, out result, out longestMatching))
            {
                throw new InvalidOperationException(string.Format(
                    "Could not find node '{0}', the longest matching was '{1}'.", path, longestMatching));
            }
            return result;
        }

        private HashSet<IPeripheral> GetParents(IPeripheral child)
        {
            var parents = new HashSet<IPeripheral>();
            registeredPeripherals.TraverseChildrenFirst((parent, children, level) =>
            {
                if(children.Any(x => x.Value.Equals(child)))
                {
                    parents.Add(parent.Value);
                }
            }, 0);
            return parents;
        }

        private ReadOnlyCollection<string> GetNames(IPeripheral peripheral)
        {
            lock(collectionSync)
            {
                var paths = new List<string>();
                if(peripheral == SystemBus)
                {
                    paths.Add(SystemBusName);
                }
                else
                {
                    FindPaths(SystemBusName, peripheral, registeredPeripherals.GetNode(SystemBus), paths);
                }
                return new ReadOnlyCollection<string>(paths);
            }
        }

        private void FindPaths(string nameSoFar, IPeripheral peripheralToFind, MultiTreeNode<IPeripheral, IRegistrationPoint> currentNode, List<string> paths)
        {
            foreach(var child in currentNode.Children)
            {
                var currentPeripheral = child.Value;
                string localName;
                if(!TryGetLocalName(currentPeripheral, out localName))
                {
                    continue;
                }
                var name = Subname(nameSoFar, localName);
                if(currentPeripheral == peripheralToFind)
                {
                    paths.Add(name);
                    return; // shouldn't be attached to itself
                }
                FindPaths(name, peripheralToFind, child, paths);
            }
        }

        private static string Subname(string parent, string child)
        {
            return string.Format("{0}{1}{2}", parent, string.IsNullOrEmpty(parent) ? string.Empty : PathSeparator.ToString(), child);
        }

        private string GetNameForOwnLife(IHasOwnLife ownLife)
        {
            var peripheral = ownLife as IPeripheral;
            if(peripheral != null)
            {
                return GetAnyNameOrTypeName(peripheral);
            }
            return ownLife.ToString();
        }

        private static void DoAttachGPIO(IPeripheral source, PropertyInfo[] gpios, IGPIOReceiver destination, int destinationNumber)
        {
            if(gpios.Length == 0)
            {
                throw new RecoverableException("No GPIO connector found.");
            }
            if(gpios.Length > 1)
            {
                throw new RecoverableException("Ambiguous GPIO connector. Available connectors are: {0}."
                    .FormatWith(gpios.Select(x => x.Name).Aggregate((x, y) => x + ", " + y)));
            }
            (gpios[0].GetValue(source, null) as GPIO).Connect(destination, destinationNumber);
        }

        private static IGPIOReceiver GetActualReceiver(IGPIOReceiver receiver, int? localReceiverNumber)
        {
            var localReceiver = receiver as ILocalGPIOReceiver;
            if(localReceiverNumber.HasValue)
            {
                if(localReceiver != null)
                {
                    return localReceiver.GetLocalReceiver(localReceiverNumber.Value);
                }
                throw new RecoverableException("The specified receiver does not support localReceiverNumber.");
            }
            return receiver;
        }

        private void ReportForeignEventInner(Action<TimeInterval, bool> recordMethod, Action handlerMethod, bool timeDomainInternalEvent)
        {
            LocalTimeSource.ExecuteInNearestSyncedState(ts =>
            {
                recordMethod?.Invoke(ts.TimeElapsed, timeDomainInternalEvent);
                handlerMethod();
            }, true);
        }

        private void CollectGarbageStamp()
        {
            currentStampLevel++;
            if(currentStampLevel != 1)
            {
                return;
            }
            currentStamp = new List<IPeripheral>();
            registeredPeripherals.TraverseParentFirst((peripheral, level) => currentStamp.Add(peripheral), 0);
        }

        private void CollectGarbage()
        {
            currentStampLevel--;
            if(currentStampLevel != 0)
            {
                return;
            }
            var toDelete = currentStamp.Where(x => !IsRegistered(x)).ToArray();
            DetachIncomingInterrupts(toDelete);
            DetachOutgoingInterrupts(toDelete);
            foreach(var value in toDelete)
            {
                ((PeripheralsGroupsManager)PeripheralsGroups).RemoveFromAllGroups(value);
                var ownLife = value as IHasOwnLife;
                if(ownLife != null)
                {
                    ownLifes.Remove(ownLife);
                }
                EmulationManager.Instance.CurrentEmulation.Connector.DisconnectFromAll(value);

                localNames.Remove(value);
                var disposable = value as IDisposable;
                if(disposable != null)
                {
                    disposable.Dispose();
                }
            }
            currentStamp = null;
        }

        private void DetachIncomingInterrupts(IPeripheral[] detachedPeripherals)
        {
            foreach(var detachedPeripheral in detachedPeripherals)
            {
                // find all peripherials' GPIOs and check which one is connected to detachedPeripherial
                foreach(var peripheral in registeredPeripherals.Children.Select(x => x.Value).Distinct())
                {
                    foreach(var gpio in peripheral.GetGPIOs().Select(x => x.Item2))
                    {
                        var endpoints = gpio.Endpoints;
                        for(var i = 0; i < endpoints.Count; ++i)
                        {
                            if(endpoints[i].Receiver == detachedPeripheral)
                            {
                                gpio.Disconnect(endpoints[i]);
                            }
                        }
                    }
                }
            }
        }

        private static void DetachOutgoingInterrupts(IEnumerable<IPeripheral> peripherals)
        {
            foreach(var peripheral in peripherals)
            {
                foreach(var gpio in peripheral.GetGPIOs().Select(x => x.Item2))
                {
                    gpio.Disconnect();
                }
            }
        }

        private void Resume()
        {
            lock(pausingSync)
            {
                (LocalTimeSource as SlaveTimeSource)?.Resume();
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 1 : 0))
                {
                    this.NoisyLog("Resuming {0}.", GetNameForOwnLife(ownLife));
                    ownLife.Resume();
                }
                this.Log(LogLevel.Info, "Machine resumed.");
                state = State.Started;
                var machineStarted = StateChanged;
                if(machineStarted != null)
                {
                    machineStarted(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Started));
                }
            }
        }

        public void HandleTimeProgress(TimeInterval diff)
        {
            clockSource.Advance(diff);
        }

        public void PostCreationActions()
        {
            // Enable broadcasting dirty addresses on multicore platforms
            var cpus = SystemBus.GetCPUs().OfType<ICPUWithMappedMemory>().ToArray();
            if(cpus.Length > 1)
            {
                foreach(var cpu in cpus)
                {
                    cpu.SetBroadcastDirty(true);
                }
            }

            // Register io_executable flags for all ArrayMemory peripherals
            foreach(var context in SystemBus.GetAllContextKeys())
            {
                foreach(var registration in SystemBus.GetRegistrationsForPeripheralType<Peripherals.Memory.ArrayMemory>(context))
                {
                    var range = registration.RegistrationPoint.Range;
                    var perCore = registration.RegistrationPoint.CPU;
                    if(perCore == null)
                    {
                        cpus = SystemBus.GetCPUs().OfType<ICPUWithMappedMemory>().ToArray();
                        foreach(var cpu in cpus)
                        {
                            cpu.RegisterAccessFlags(range.StartAddress, range.Size, isIoMemory: true);
                        }
                    }
                    else
                    {
                        if(perCore is ICPUWithMappedMemory cpuWithMappedMemory)
                        {
                            cpuWithMappedMemory.RegisterAccessFlags(range.StartAddress, range.Size, isIoMemory: true);
                        }
                    }
                }
            }
        }

        public void ExchangeRegistrationPointForPeripheral(IPeripheral parent, IPeripheral child, IRegistrationPoint oldPoint, IRegistrationPoint newPoint)
        {
            // assert paused state or within per-core context
            var exitLock = false;
            try
            {
                if(!SystemBus.TryGetCurrentCPU(out var cpu) || !cpu.OnPossessedThread)
                {
                    Monitor.Enter(pausingSync, ref exitLock);
                    if(!IsPaused)
                    {
                        throw new RecoverableException("Attempted to exchange registration point while not in paused state nor on context's CPU thread");
                    }
                }
                lock(collectionSync)
                {
                    registeredPeripherals.GetNode(parent).ReplaceConnectionWay(oldPoint, newPoint);
                    var operation = PeripheralsChangedEventArgs.PeripheralChangeType.Moved;
                    PeripheralsChanged?.Invoke(this, PeripheralsChangedEventArgs.Create(child, operation));
                }
            }
            finally
            {
                if(exitLock)
                {
                    Monitor.Exit(pausingSync);
                }
            }
        }

        [Constructor]
        private Dictionary<int, GdbStub> gdbStubs;
        private string userState;
        private Action<string> userStateHook;
        private bool alreadyDisposed;
        private State state;
        private PausedState pausedState;
        private List<IPeripheral> currentStamp;
        private int currentStampLevel;
        private Recorder recorder;
        private Player player;
        private TimeSourceBase localTimeSource;
        private RealTimeClockMode realTimeClockMode;

        private readonly MultiTree<IPeripheral, IRegistrationPoint> registeredPeripherals;
        private readonly Dictionary<IBusPeripheral, BusControllerWrapper> peripheralsBusControllers;
        private readonly Dictionary<IPeripheral, string> localNames;
        private readonly HashSet<IHasOwnLife> ownLifes;
        private readonly object collectionSync;
        private readonly object pausingSync;
        private readonly object disposedSync;
        private readonly DateTime machineCreatedAt;

        /*
         *  Variables used for memory invalidation
         */
        private const int InitialDirtyListLength = 1 << 10;
        private readonly Dictionary<ICPU, int> firstUnbroadcastedDirtyAddressIndex;
        private readonly Dictionary<ICPU, List<long>> invalidatedAddressesByCpu;
        private readonly Dictionary<string, List<long>> invalidatedAddressesByArchitecture;
        private readonly object invalidatedAddressesLock;

        private enum State
        {
            NotStarted,
            Started,
            Paused
        }

        private class BusControllerWrapper : BusControllerProxy
        {
            public BusControllerWrapper(IBusController wrappedController) : base(wrappedController)
            {
            }

            public void ChangeWrapped(IBusController wrappedController)
            {
                ParentController = wrappedController;
            }
        }

        private sealed class PausedState : IDisposable
        {
            // PausedState is used only within Machine so we can take Machine as an argument, instead of IMachine.
            public PausedState(Machine machine)
            {
                this.machine = machine;
                sync = machine.pausingSync;
            }

            public PausedState Enter()
            {
                LevelUp();
                return this;
            }

            public void Exit()
            {
                LevelDown();
            }

            public void Dispose()
            {
                Exit();
            }

            private void LevelUp()
            {
                lock(sync)
                {
                    if(currentLevel == 0)
                    {
                        if(machine.IsPaused)
                        {
                            wasPaused = true;
                        }
                        else
                        {
                            wasPaused = false;
                            pausedLifes = machine.ownLifes.Where(ownLife => ownLife.IsPaused).ToArray();
                            machine.Pause();
                        }
                    }
                    currentLevel++;
                }
            }

            private void LevelDown()
            {
                lock(sync)
                {
                    if(currentLevel == 1)
                    {
                        if(!wasPaused)
                        {
                            machine.Start(ownLife => !pausedLifes.Contains(ownLife));
                        }
                    }
                    if(currentLevel == 0)
                    {
                        throw new InvalidOperationException("LevelDown without prior LevelUp");
                    }
                    currentLevel--;
                }
            }

            // As this mechanism is used to pause the emulation before the serialization - we have to drop value of this field to avoid starting with a non-zero value.
            // Otherwise, after the deserialization we won't be able to ever reach the currentLevel == 0 and restart the machine when needed.
            [Transient]
            private int currentLevel;
            private bool wasPaused;
            private IHasOwnLife[] pausedLifes;
            private readonly Machine machine;
            private readonly object sync;
        }

        private sealed class PeripheralsGroupsManager : IPeripheralsGroupsManager
        {
            public PeripheralsGroupsManager(IMachine machine)
            {
                this.machine = machine;
                groups = new List<PeripheralsGroup>();
            }

            public IPeripheralsGroup GetOrCreate(string name, IEnumerable<IPeripheral> peripherals)
            {
                IPeripheralsGroup existingResult = null;
                var result = (PeripheralsGroup)existingResult;
                if(!TryGetByName(name, out existingResult))
                {
                    result = new PeripheralsGroup(name, machine);
                    groups.Add(result);
                }

                foreach(var p in peripherals)
                {
                    result.Add(p);
                }

                return result;
            }

            public IPeripheralsGroup GetOrCreate(string name)
            {
                IPeripheralsGroup result;
                if(!TryGetByName(name, out result))
                {
                    result = new PeripheralsGroup(name, machine);
                    groups.Add((PeripheralsGroup)result);
                }

                return result;
            }

            public void RemoveFromAllGroups(IPeripheral value)
            {
                foreach(var group in ActiveGroups)
                {
                    ((List<IPeripheral>)group.Peripherals).Remove(value);
                }
            }

            public bool TryGetActiveGroupContaining(IPeripheral peripheral, out IPeripheralsGroup group)
            {
                group = ActiveGroups.SingleOrDefault(x => ((PeripheralsGroup)x).Contains(peripheral));
                return group != null;
            }

            public bool TryGetAnyGroupContaining(IPeripheral peripheral, out IPeripheralsGroup group)
            {
                group = groups.SingleOrDefault(x => x.Contains(peripheral));
                return group != null;
            }

            public bool TryGetByName(string name, out IPeripheralsGroup group)
            {
                group = ActiveGroups.SingleOrDefault(x => x.Name == name);
                return group != null;
            }

            public IEnumerable<IPeripheralsGroup> ActiveGroups
            {
                get
                {
                    return groups.Where(x => x.IsActive);
                }
            }

            private readonly List<PeripheralsGroup> groups;
            private readonly IMachine machine;

            private sealed class PeripheralsGroup : IPeripheralsGroup
            {
                public PeripheralsGroup(string name, IMachine machine)
                {
                    Machine = machine;
                    Name = name;
                    IsActive = true;
                    Peripherals = new List<IPeripheral>();
                }

                public void Add(IPeripheral peripheral)
                {
                    if(!Machine.IsRegistered(peripheral))
                    {
                        throw new RegistrationException("Peripheral must be registered prior to adding to the group");
                    }
                    ((List<IPeripheral>)Peripherals).Add(peripheral);
                }

                public bool Contains(IPeripheral peripheral)
                {
                    return Peripherals.Contains(peripheral);
                }

                public void Remove(IPeripheral peripheral)
                {
                    ((List<IPeripheral>)Peripherals).Remove(peripheral);
                }

                public void Unregister()
                {
                    IsActive = false;
                    using(Machine.ObtainPausedState(true))
                    {
                        foreach(var p in Peripherals.ToList())
                        {
                            Machine.UnregisterFromParent(p);
                        }
                    }
                    ((PeripheralsGroupsManager)Machine.PeripheralsGroups).groups.Remove(this);
                }

                public string Name { get; private set; }

                public bool IsActive { get; private set; }

                public IMachine Machine { get; private set; }

                public IEnumerable<IPeripheral> Peripherals { get; private set; }
            }
        }
    }
}

